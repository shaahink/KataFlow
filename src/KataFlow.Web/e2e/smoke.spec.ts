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
    expect(body).toHaveProperty('root');
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

  test('GET /api/workflows includes agentic-dev preset', async () => {
    const { status, body } = await apiGet('/api/workflows');
    expect(status).toBe(200);
    expect(body.some((w: any) => w.name === 'agentic-dev')).toBe(true);
    expect(body.some((w: any) => w.name === 'bug-fix')).toBe(true);
    expect(body.some((w: any) => w.name === 'code-review-agentic')).toBe(true);
  });

  test('agentic-dev preset loads with Script build and test steps', async () => {
    const { status, body } = await apiGet('/api/workflows/agentic-dev');
    expect(status).toBe(200);
    expect(body).toHaveProperty('yaml');
    const yaml = body.yaml as string;
    expect(yaml).toContain('agent: script');
    expect(yaml).toContain('script_command');
    expect(yaml).toContain('build');
    expect(yaml).toContain('test');
  });
});
