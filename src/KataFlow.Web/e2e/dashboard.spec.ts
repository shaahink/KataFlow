import { test, expect } from '@playwright/test';
import { listWorkflows } from './helpers/api';

test.describe('Dashboard page', () => {
  test('page loads and shows workflow count', async ({ page }) => {
    const workflows = await listWorkflows();
    await page.goto('/');
    await expect(page.getByText(workflows.length.toString())).toBeVisible();
  });

  test('recent sessions table or empty state', async ({ page }) => {
    await page.goto('/');
    // Either a table exists or "No sessions yet" is shown
    const table = page.locator('table');
    const empty = page.getByText('No sessions yet');
    await expect(table.or(empty)).toBeVisible();
  });
});
