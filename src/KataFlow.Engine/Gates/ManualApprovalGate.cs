using KataFlow.Core.Enums;
using KataFlow.Core.Interfaces;
using KataFlow.Core.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace KataFlow.Engine.Gates;

public class ManualApprovalGate : IApprovalGate
{
    private readonly ILogger<ManualApprovalGate> _logger;

    public ApprovalMode Mode => ApprovalMode.Manual;

    public ManualApprovalGate(ILogger<ManualApprovalGate> logger)
    {
        _logger = logger;
    }

    public async Task<ApprovalDecision> RequestApprovalAsync(StepResult result, CancellationToken ct = default)
    {
        var sessionDir = result.ArtifactPath is not null
            ? Path.GetDirectoryName(result.ArtifactPath) ?? ""
            : "";

        var parentDir = Path.GetDirectoryName(sessionDir) ?? "";
        var pendingFile = Path.Combine(parentDir, ".pending-approval");
        var approvedFile = Path.Combine(parentDir, ".approved");
        var rejectedFile = Path.Combine(parentDir, ".rejected");

        if (File.Exists(approvedFile))
        {
            File.Delete(approvedFile);
            File.Delete(pendingFile);
            return ApprovalDecision.Approve;
        }

        if (File.Exists(rejectedFile))
        {
            File.Delete(rejectedFile);
            File.Delete(pendingFile);
            return ApprovalDecision.Reject;
        }

        if (result.ArtifactPath is not null)
            File.WriteAllText(pendingFile, result.StepName);

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
            return ApprovalDecision.Reject;

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

            return secondChoice == "Reject and stop" ? ApprovalDecision.Reject : ApprovalDecision.Approve;
        }

        if (File.Exists(pendingFile))
            File.Delete(pendingFile);

        return ApprovalDecision.Approve;
    }
}
