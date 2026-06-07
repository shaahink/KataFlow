using System.Diagnostics;
using KataFlow.Core;
using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using Microsoft.Extensions.Logging;

namespace KataFlow.Engine;

public class StepExecutor
{
    private readonly ContextBuilder _contextBuilder;
    private readonly IPromptRenderer _promptRenderer;
    private readonly IArtifactStore _artifactStore;
    private readonly ILogger<StepExecutor> _logger;

    public StepExecutor(
        ContextBuilder contextBuilder,
        IPromptRenderer promptRenderer,
        IArtifactStore artifactStore,
        ILogger<StepExecutor> logger)
    {
        _contextBuilder = contextBuilder;
        _promptRenderer = promptRenderer;
        _artifactStore = artifactStore;
        _logger = logger;
    }

    public virtual async Task<StepResult> ExecuteAsync(
        Session session,
        StepDefinition step,
        Func<AgentType, IAgentAdapter> adapterResolver,
        CancellationToken ct)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity(Diagnostics.SpanNames.StepExecute);
        activity?.SetTag(Diagnostics.Tags.StepName, step.Name);
        activity?.SetTag(Diagnostics.Tags.AgentType, step.Agent.ToString());
        activity?.SetTag(Diagnostics.Tags.SessionId, session.Id);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(step.Timeout);
        var linkedCt = timeoutCts.Token;

        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                var vars = _contextBuilder.Build(session, step);
                var prompt = _promptRenderer.Render(step.PromptTemplate, vars);

                var adapter = adapterResolver(step.Agent);
                var channel = ResolveChannel(step, session.Mode);
                ValidateChannel(adapter, channel, step);

                activity?.SetTag(Diagnostics.Tags.ChannelType, channel.ToString());

                var request = new AgentRequest
                {
                    SessionId = session.Id,
                    StepName = step.Name,
                    RenderedPrompt = prompt,
                    Metadata = step.Model is not null
                        ? new() { ["model"] = step.Model }
                        : new(),
                };

                using var sendActivity = Diagnostics.ActivitySource.StartActivity(Diagnostics.SpanNames.AdapterSend);
                var response = await adapter.SendAsync(request, channel, linkedCt);

                if (!response.Success || string.IsNullOrWhiteSpace(response.Content))
                    throw new AgentResponseException(response.ErrorMessage ?? "Empty response");

                if (step.OutputArtifactName is not null)
                {
                    await _artifactStore.SaveAsync(session, step.OutputArtifactName, response.Content);
                    session.Artifacts[step.OutputArtifactName] = _artifactStore.GetPath(session, step.OutputArtifactName);
                }

                var sessionStep = new SessionStep
                {
                    StepName = step.Name,
                    Status = SessionStatus.Complete,
                    OutputArtifactPath = step.OutputArtifactName is not null
                        ? session.Artifacts[step.OutputArtifactName] : null,
                    CompletedAt = DateTimeOffset.UtcNow,
                };
                session.History.Add(sessionStep);

                return new StepResult
                {
                    StepName = step.Name,
                    Success = true,
                    ArtifactPath = sessionStep.OutputArtifactPath,
                    ArtifactContent = response.Content,
                };
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                _logger.LogWarning("Step {Step} timed out after {Timeout}", step.Name, step.Timeout);
                var msg = $"Step timed out after {step.Timeout}";
                session.History.Add(new SessionStep
                {
                    StepName = step.Name,
                    Status = SessionStatus.Failed,
                    ErrorMessage = msg,
                    CompletedAt = DateTimeOffset.UtcNow,
                });
                return new StepResult { StepName = step.Name, Success = false, ErrorMessage = msg };
            }
            catch (Exception ex) when (attempt <= step.MaxRetries && ex is not InvalidOperationException)
            {
                activity?.SetTag(Diagnostics.Tags.RetryAttempt, attempt);
                _logger.LogWarning(ex, "Step {Step} attempt {Attempt} failed, retrying", step.Name, attempt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Step {Step} failed after {Attempt} attempt(s)", step.Name, attempt);
                session.History.Add(new SessionStep
                {
                    StepName = step.Name,
                    Status = SessionStatus.Failed,
                    ErrorMessage = ex.Message,
                    CompletedAt = DateTimeOffset.UtcNow,
                });
                return new StepResult { StepName = step.Name, Success = false, ErrorMessage = ex.Message };
            }
        }
    }

    private static ChannelType ResolveChannel(StepDefinition step, OrchestratorMode mode)
        => step.ChannelOverride ?? (mode == OrchestratorMode.Dev
            ? ChannelType.FileDrop
            : ChannelType.ApiDirect);

    private static void ValidateChannel(IAgentAdapter adapter, ChannelType channel, StepDefinition step)
    {
        if (!adapter.SupportedChannels.Contains(channel))
            throw new InvalidOperationException(
                $"Adapter '{adapter.Name}' does not support channel '{channel}' (step: {step.Name}).");
    }
}

internal class AgentResponseException(string message) : Exception(message) { }
