import { test, expect } from '@playwright/test';
import { apiGet } from './helpers/api';

test.describe('API smoke tests', () => {
  test('GET /api/workflows returns preset list', async () => {
    const { status, body } = await apiGet('/api/workflows');
    expect(status).toBe(200);
    expect(Array.isArray(body)).toBe(true);
    expect(body.length).toBeGreaterThanOrEqual(5);
    expect(body.some((w: any) => w.name === 'software-lifecycle')).toBe(true);
  });

  test('GET /api/debug/paths shows resolved directories', async () => {
    const { status, body } = await apiGet('/api/debug/paths');
    expect(status).toBe(200);
    expect(body).toHaveProperty('workspaceRoot');
    expect(body).toHaveProperty('templatesPath');
  });

  test('GET /api/templates returns template paths', async () => {
    const { status, body } = await apiGet('/api/templates');
    expect(status).toBe(200);
    expect(Array.isArray(body)).toBe(true);
    if (body.length > 0) {
      expect(body.some((p: string) => p.includes('planner.md'))).toBe(true);
    }
  });

  test('GET /api/sessions returns empty array', async () => {
    const { status, body } = await apiGet('/api/sessions');
    expect(status).toBe(200);
    expect(Array.isArray(body)).toBe(true);
  });
});
