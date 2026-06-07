import { request } from '@playwright/test';

const API = process.env.API_URL || 'http://localhost:5100';

export async function apiGet(path: string) {
  const ctx = await request.newContext();
  const res = await ctx.get(`${API}${path}`);
  return { status: res.status(), body: await res.json() };
}

export async function apiPost(path: string, data?: any) {
  const ctx = await request.newContext();
  const res = await ctx.post(`${API}${path}`, { data });
  const body = res.headers()['content-type']?.includes('json')
    ? await res.json()
    : null;
  return { status: res.status(), body };
}

export async function apiDelete(path: string) {
  const ctx = await request.newContext();
  const res = await ctx.delete(`${API}${path}`);
  return { status: res.status() };
}

export async function listWorkflows() {
  const { body } = await apiGet('/api/workflows');
  return body as any[];
}

export async function listTemplates() {
  const { body } = await apiGet('/api/templates');
  return body as string[];
}

export async function listSessions() {
  const { body } = await apiGet('/api/sessions');
  return body as any[];
}

export async function startRun(workflow: string, autoApprove = true, variables = {}) {
  const { body } = await apiPost('/api/runs', { workflow, autoApprove, variables });
  return body as { sessionId: string };
}

export async function deleteSession(id: string) {
  await apiDelete(`/api/sessions/${id}`);
}

export async function deleteAllSessions() {
  await apiDelete('/api/sessions');
}
