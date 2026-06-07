namespace KataFlow.Api;

public record CreateWorkflowRequest(string Name, string Yaml);
public record UpdateWorkflowRequest(string Yaml);
public record UpdateTemplateRequest(string Content);
public record ApproveRequest(bool Approve);
public record StartRunRequest(string Workflow, Dictionary<string, string>? Variables = null, bool AutoApprove = false);
