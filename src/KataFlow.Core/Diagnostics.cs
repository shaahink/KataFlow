using System.Diagnostics;

namespace KataFlow.Core;

public static class Diagnostics
{
    public static readonly ActivitySource ActivitySource = new("KataFlow", "1.0.0");

    public static class SpanNames
    {
        public const string WorkflowRun = "kataflow.workflow.run";
        public const string StepExecute = "kataflow.step.execute";
        public const string AdapterSend = "kataflow.adapter.send";
        public const string ApprovalGate = "kataflow.approval.gate";
    }

    public static class Tags
    {
        public const string SessionId = "kataflow.session_id";
        public const string WorkflowName = "kataflow.workflow_name";
        public const string StepName = "kataflow.step_name";
        public const string AgentType = "kataflow.agent_type";
        public const string ChannelType = "kataflow.channel_type";
        public const string Success = "kataflow.success";
        public const string RetryAttempt = "kataflow.retry_attempt";
    }

    public static class Meters
    {
        public const string Name = "KataFlow";
        public const string StepsCompleted = "kataflow.steps.completed";
        public const string StepsFailed = "kataflow.steps.failed";
        public const string StepsRetried = "kataflow.steps.retried";
        public const string WorkflowDuration = "kataflow.workflow.duration_ms";
    }
}
