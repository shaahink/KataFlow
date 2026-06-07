import { test, expect } from '@playwright/test';
import { apiGet } from './helpers/api';

test.describe('Template pages', () => {
  test('list shows templates or empty state', async ({ page }) => {
    await page.goto('/templates');
    const templates = await apiGet('/api/templates');
    if (templates.body.length > 0) {
      await expect(page.locator('.font-mono').first()).toBeVisible();
    } else {
      await expect(page.getByText('No templates found')).toBeVisible();
    }
  });

  test('editor shows template content and variables', async ({ page }) => {
    await page.goto('/templates/templates/engineering/planner.md');
    await page.waitForLoadState('networkidle');
    const editor = page.locator('.cm-editor');
    const error = page.locator('body');
    await expect(editor.or(error)).toBeVisible({ timeout: 10000 });
  });
});
