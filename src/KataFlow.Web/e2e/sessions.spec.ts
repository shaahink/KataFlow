import { test, expect } from '@playwright/test';
import { startRun, deleteSession, listSessions } from './helpers/api';

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
});
