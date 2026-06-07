import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 2 : 1,
  reporter: [['html'], ['list']],
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
  },
  webServer: [
    {
      command: 'dotnet run --project ../KataFlow.Api --urls http://localhost:5100',
      port: 5100,
      reuseExistingServer: !process.env.CI,
      timeout: 60000,
    },
    {
      command: 'npx ng serve --port 4200',
      port: 4200,
      reuseExistingServer: !process.env.CI,
      timeout: 60000,
    },
  ],
});
