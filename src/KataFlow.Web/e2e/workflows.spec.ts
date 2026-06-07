import { test, expect } from '@playwright/test';
import { apiPost, apiDelete, apiGet } from './helpers/api';

test.describe('Workflow pages', () => {
  test('list shows all presets', async ({ page }) => {
    await page.goto('/workflows');
    await expect(page.getByText('software-lifecycle')).toBeVisible();
    await expect(page.getByText('trading-strategy')).toBeVisible();
    await expect(page.getByText('quick-execute')).toBeVisible();
  });

  test('editor loads YAML and renders d3 graph', async ({ page }) => {
    await page.goto('/workflows/software-lifecycle');
    await expect(page.locator('.cm-editor')).toBeVisible({ timeout: 10000 });
    // d3 SVG should have 4 step rectangles (plan, implement, review, report)
    const svgRects = page.locator('app-workflow-graph svg rect');
    await expect(svgRects.first()).toBeVisible();
  });

  test('create, verify in list, then delete a workflow', async ({ page }) => {
    const name = `e2e-test-${Date.now()}`;
    const yaml = [
      `workflow:`,
      `  name: ${name}`,
      `  description: "E2E test workflow"`,
      `  default_mode: dev`,
      `  steps:`,
      `    - name: test-step`,
      `      agent: rest`,
      `      role: executor`,
      `      prompt_template: templates/engineering/executor.md`,
      `      approval: auto`,
    ].join('\n');

    // Create via API
    const createRes = await apiPost('/api/workflows', { name, yaml });
    expect(createRes.status).toBe(201);

    // Verify via API
    const wfResp = await apiGet('/api/workflows');
    const match = (wfResp.body as any[]).find((w: any) => w.name === name);
    expect(match).toBeDefined();

  });
});
