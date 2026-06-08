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

        var decision = await ShowApprovalPrompt(result, result.Budget);
        return decision;
    }

    private static Task<ApprovalDecision> ShowApprovalPrompt(StepResult result, StepBudget? budget = null)
    {
        var previewText = "";
        if (result.ArtifactContent is not null)
        {
            var lines = result.ArtifactContent.Split('\n').Take(20);
            previewText = "\n" + string.Join("\n", lines);
        }

        var budgetLine = budget is not null && budget.CostUsd > 0
            ? $"\n\n[dim]Step cost: [/][yellow]${budget.CostUsd:F4}[/] [dim]({budget.InputTokens} in / {budget.OutputTokens} out)[/]"
            : "";

        var panel = new Panel(
            Align.Center(new Markup(
                $"[bold]Step:[/] {result.StepName}\n" +
                $"[bold]Artifact:[/] {result.ArtifactPath ?? "N/A"}\n\n" +
                (previewText.Length > 0 ? $"[dim]Preview (first 20 lines):[/]{previewText}" : "") +
                budgetLine)))
        {
            Header = new PanelHeader("Approval Required"),
            Border = BoxBorder.Heavy,
        };
        AnsiConsole.Write(panel);

        while (true)
        {
            var choices = new List<string> { "Approve and continue", "Reject and stop", "View full artifact" };
            if (result.ArtifactPath is not null)
                choices.Add("Edit artifact then approve");

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose action:")
                    .PageSize(6)
                    .AddChoices(choices));

            if (choice == "Reject and stop")
                return Task.FromResult(ApprovalDecision.Reject);

            if (choice == "View full artifact" && result.ArtifactContent is not null)
            {
                AnsiConsole.Write(new Panel(result.ArtifactContent)
                {
                    Header = new PanelHeader(result.StepName),
                    Border = BoxBorder.Heavy,
                });
                continue;
            }

            if (choice == "Edit artifact then approve" && result.ArtifactPath is not null)
            {
                OpenInEditor(result.ArtifactPath);
                AnsiConsole.MarkupLine("[green]Artifact saved. Approving.[/]");
                return Task.FromResult(ApprovalDecision.Approve);
            }

            return Task.FromResult(ApprovalDecision.Approve);
        }
    }

    private static void OpenInEditor(string filePath)
    {
        var editor = Environment.GetEnvironmentVariable("EDITOR")
            ?? (OperatingSystem.IsWindows() ? "notepad" : "nano");
        try
        {
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = editor,
                Arguments = $"\"{filePath}\"",
                UseShellExecute = true,
            });
            p?.WaitForExit();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Could not open editor '{editor}': {ex.Message}[/]");
        }
    }
}
