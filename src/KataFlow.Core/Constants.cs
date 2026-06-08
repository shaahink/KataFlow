namespace KataFlow.Core;

public static class Constants
{
    public const string WorkflowsDefaultPath = "./workflows";
    public const string TemplatesDefaultPath = "./templates";
    public const string SessionsDefaultPath = "./sessions";
    public const string DefaultMode = "dev";

    public const string ClaudeApiKeyEnv = "ANTHROPIC_API_KEY";
    public const string DeepSeekApiKeyEnv = "DEEPSEEK_API_KEY";
    public const string OpenAiApiKeyEnv = "OPENAI_API_KEY";

    public const string SessionIdVar = "_session_id";
    public const string StepNameVar = "_step_name";
    public const string WorkflowNameVar = "_workflow_name";
    public const string OutputPathVar = "_output_path";

    public const string PendingApprovalFile = ".pending-approval";
    public const string ApprovedFile = ".approved";
    public const string RejectedFile = ".rejected";

    public const string ConfigSectionKataFlow = "KataFlow";
    public const string ConfigSectionClaude = "Agents:Claude";
    public const string ConfigSectionRest = "Agents:Rest";
    public const string ConfigSectionCliExecute = "Agents:CliExecute";
    public const string ConfigKeyWorkflowsPath = "KataFlow:WorkflowsPath";
    public const string ConfigKeyTemplatesPath = "KataFlow:TemplatesPath";
    public const string ConfigKeySessionsPath = "KataFlow:SessionsPath";

    public const string ClaudeApiUrl = "https://api.anthropic.com";
    public const string SystemInstructionsFile = "_system/output-instructions.md";
}

public static class ModelPricing
{
    private static readonly Dictionary<string, (decimal InputPer1M, decimal OutputPer1M)> Rates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-sonnet-4-6"]           = (3.00m,  15.00m),
            ["claude-haiku-4-5-20251001"]   = (0.80m,   4.00m),
            ["claude-opus-4-8"]             = (15.00m,  75.00m),
            ["deepseek-chat"]               = (0.14m,   0.28m),
            ["deepseek-reasoner"]           = (0.55m,   2.19m),
            ["gpt-4o"]                      = (2.50m,  10.00m),
        };

    public static decimal Calculate(string model, int inputTokens, int outputTokens)
    {
        if (!Rates.TryGetValue(model, out var rate)) return 0m;
        return (inputTokens * rate.InputPer1M + outputTokens * rate.OutputPer1M) / 1_000_000m;
    }

    public static bool IsKnown(string model) => Rates.ContainsKey(model);
}
