using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace KataFlow.Engine.Gates;

public class ManualApprovalGate : IApprovalGate
{
    private readonly ILogger<ManualApprovalGate> _logger;
    private readonly ApprovalFileSignal _fileSignal;

    public ApprovalMode Mode => ApprovalMode.Manual;

    public ManualApprovalGate(ILogger<ManualApprovalGate> logger, ApprovalFileSignal fileSignal)
    {
        _logger = logger;
        _fileSignal = fileSignal;
    }

    public async Task<ApprovalDecision> RequestApprovalAsync(StepResult result, CancellationToken ct = default)
    {
        var sessionDir = result.ArtifactPath is not null
            ? Path.GetDirectoryName(result.ArtifactPath) ?? ""
            : "";

        if (_fileSignal.HasApproval(sessionDir))
        {
            _fileSignal.ClearApproval(sessionDir);
            return ApprovalDecision.Approve;
        }

        if (_fileSignal.HasRejection(sessionDir))
        {
            _fileSignal.ClearRejection(sessionDir);
            return ApprovalDecision.Reject;
        }

        if (!string.IsNullOrEmpty(sessionDir))
            _fileSignal.WritePending(sessionDir, result.StepName);

        var decision = await ShowApprovalPrompt(result);
        return decision;
    }

    private static Task<ApprovalDecision> ShowApprovalPrompt(StepResult result)
    {
        var previewText = "";
        if (result.ArtifactContent is not null)
        {
            var lines = result.ArtifactContent.Split('\n').Take(20);
            previewText = "\n" + string.Join("\n", lines);
        }

        var panel = new Panel(
            Align.Center(new Markup(
                $"[bold]Step:[/] {result.StepName}\n" +
                $"[bold]Artifact:[/] {result.ArtifactPath ?? "N/A"}\n\n" +
                (previewText.Length > 0 ? $"[dim]Preview (first 20 lines):[/]{previewText}" : ""))))
        {
            Header = new PanelHeader("Approval Required"),
            Border = BoxBorder.Heavy,
        };
        AnsiConsole.Write(panel);

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose action:")
                .PageSize(5)
                .AddChoices("Approve and continue", "Reject and stop", "View full artifact"));

        if (choice == "Reject and stop")
            return Task.FromResult(ApprovalDecision.Reject);

        if (choice == "View full artifact" && result.ArtifactContent is not null)
        {
            AnsiConsole.Write(new Panel(result.ArtifactContent)
            {
                Header = new PanelHeader(result.StepName),
                Border = BoxBorder.Heavy,
            });

            var secondChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose action:")
                    .PageSize(3)
                    .AddChoices("Approve and continue", "Reject and stop"));

            return Task.FromResult(
                secondChoice == "Reject and stop" ? ApprovalDecision.Reject : ApprovalDecision.Approve);
        }

        return Task.FromResult(ApprovalDecision.Approve);
    }
}
