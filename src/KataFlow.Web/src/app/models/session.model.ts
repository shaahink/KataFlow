export interface SessionSummary {
  id: string;
  workflowName: string;
  status: string;
  currentStepIndex: number;
  createdAt: string;
  completedAt: string | null;
}

export interface SessionDetail {
  id: string;
  workflowName: string;
  status: string;
  mode: string;
  currentStepIndex: number;
  createdAt: string;
  completedAt: string | null;
  artifacts: { name: string; path: string }[];
  steps: SessionStep[];
}

export interface SessionStep {
  stepName: string;
  status: string;
  outputArtifactPath: string | null;
  errorMessage: string | null;
  startedAt: string;
  completedAt: string | null;
}

export interface StartRunRequest {
  workflow: string;
  variables?: Record<string, string>;
  autoApprove?: boolean;
}

export interface StartRunResponse {
  sessionId: string;
}

export interface ApproveRequest {
  approve: boolean;
}
