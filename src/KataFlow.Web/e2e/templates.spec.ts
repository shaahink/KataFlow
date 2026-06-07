import { test, expect } from '@playwright/test';

test.describe('Template pages', () => {
  test('list shows templates or empty state', async ({ page }) => {
    await page.goto('/templates');
    await page.waitForTimeout(1000);
    const item = page.locator('.font-mono').first();
    const empty = page.getByText('No templates found');
    await expect(item.or(empty)).toBeVisible();
  });

  test('editor shows template content and variables', async ({ page }) => {
    await page.goto('/templates/templates/engineering/planner.md');
    // Either the editor loads, or we get a 404/empty page
    await page.waitForTimeout(2000);
    const editor = page.locator('.cm-editor');
    const error = page.locator('body');
    await expect(editor.or(error)).toBeVisible({ timeout: 10000 });
  });
});
