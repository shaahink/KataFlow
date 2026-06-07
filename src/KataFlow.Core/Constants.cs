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
