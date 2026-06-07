export interface WorkflowSummary {
  name: string;
  description: string | null;
}

export interface WorkflowDetail {
  name: string;
  yaml: string;
}

export interface WorkflowDef {
  name: string;
  description?: string;
  defaultMode?: string;
  variables?: Record<string, string>;
  steps: StepDef[];
}

export interface StepDef {
  name: string;
  agent: string;
  role: string;
  promptTemplate: string;
  model?: string;
  channelOverride?: string;
  approval?: string;
  contextArtifacts?: string[];
  outputArtifactName?: string;
  timeoutMinutes?: number;
  type?: string;
}

export interface CreateWorkflowRequest {
  name: string;
  yaml: string;
}

export interface UpdateWorkflowRequest {
  yaml: string;
}
