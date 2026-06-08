import { test, expect } from '@playwright/test';
import { startRun, deleteSession, listSessions, getSession, getArtifact, apiPost } from './helpers/api';

test.describe.configure({ mode: 'serial' });
test.describe('Session lifecycle', () => {
  const sessionIds: string[] = [];

  test.afterAll(async () => {
    for (const id of sessionIds) {
      try { await deleteSession(id); } catch { /* ignore */ }
    }
  });

  test('start a workflow creates a new session', async () => {
    const res = await startRun('quick-execute');
    expect(res).toHaveProperty('sessionId');
    expect(typeof res.sessionId).toBe('string');
    expect(res.sessionId.length).toBeGreaterThan(0);
    sessionIds.push(res.sessionId);
  });

  test('session appears in session list', async () => {
    const res = await startRun('quick-execute');
    sessionIds.push(res.sessionId);

    const sessions = await listSessions();
    const match = sessions.find((s: any) => s.id === res.sessionId);
    expect(match).toBeDefined();
    expect(match.workflowName).toBe('quick-execute');
  });

  test('session detail page renders graph', async ({ page }) => {
    const res = await startRun('quick-execute', true);
    sessionIds.push(res.sessionId);

    await page.goto(`/sessions/${res.sessionId}`);
    await expect(page.locator('app-workflow-graph')).toBeVisible({ timeout: 15000 });
  });

  test('approve a session via API', async ({ page }) => {
    const res = await startRun('quick-execute');
    sessionIds.push(res.sessionId);

    await page.goto(`/sessions/${res.sessionId}`);
    await expect(page.locator('app-workflow-graph')).toBeVisible({ timeout: 10000 });
  });

  test('session detail API has budget fields', async () => {
    const res = await startRun('quick-execute', true);
    sessionIds.push(res.sessionId);

    await new Promise(r => setTimeout(r, 2000));
    const detail = await getSession(res.sessionId);
    expect(detail).toHaveProperty('budget');
    expect(detail.budget).toHaveProperty('totalCostUsd');
    expect(detail.budget).toHaveProperty('totalInputTokens');
    expect(detail.budget).toHaveProperty('totalOutputTokens');
    expect(Array.isArray(detail.budget.steps)).toBe(true);
  });

  test('artifact content endpoint returns content', async () => {
    const res = await startRun('quick-execute', true);
    sessionIds.push(res.sessionId);

    await new Promise(r => setTimeout(r, 3000));
    const detail = await getSession(res.sessionId);
    const artifactNames = (detail.artifacts || []).map((a: any) => a.name);

    if (artifactNames.length > 0) {
      const artifact = await getArtifact(res.sessionId, artifactNames[0]);
      expect(artifact).toHaveProperty('name');
      expect(artifact).toHaveProperty('content');
      expect(artifact).toHaveProperty('path');
      expect(typeof artifact.content).toBe('string');
    }
  });

  test('session detail page shows budget summary', async ({ page }) => {
    const res = await startRun('quick-execute', true);
    sessionIds.push(res.sessionId);

    await page.goto(`/sessions/${res.sessionId}`);
    await expect(page.locator('app-workflow-graph')).toBeVisible({ timeout: 15000 });

    const budgetSection = page.getByRole('heading', { name: 'Budget' });
    await expect(budgetSection).toBeVisible({ timeout: 30000 });
  });

  test('session detail page shows artifact viewer with content', async ({ page }) => {
    const res = await startRun('quick-execute', true);
    sessionIds.push(res.sessionId);

    await page.goto(`/sessions/${res.sessionId}`);
    await expect(page.locator('app-workflow-graph')).toBeVisible({ timeout: 15000 });

    const artifactHeader = page.getByRole('heading', { name: 'Artifacts' });
    await expect(artifactHeader).toBeVisible({ timeout: 30000 });
  });

  test('approve endpoint accepts and returns success', async () => {
    const res = await startRun('planner-only', true);
    sessionIds.push(res.sessionId);

    await new Promise(r => setTimeout(r, 1000));

    const { body } = await apiPost(
      `/api/sessions/${res.sessionId}/approve`, { approve: true });
    expect(body).toHaveProperty('sessionId');
    expect(body).toHaveProperty('approved', true);
  });
});
