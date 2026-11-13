import { resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { defineConfig, devices } from '@playwright/test';

const connectionString = process.env.ConnectionStrings__TaskManager ?? '';
const webOrigin = 'http://127.0.0.1:5180';
const apiOrigin = 'http://127.0.0.1:5279';
const apiProject = resolve(process.cwd(), '../src/TaskManager.Api/TaskManager.Api.csproj');

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: true,
  retries: 0,
  workers: 1,
  reporter: 'line',
  outputDir: resolve(tmpdir(), 'task-manager-playwright-results'),
  globalTeardown: './scripts/cleanup-e2e-artifacts.mjs',
  use: {
    baseURL: webOrigin,
    trace: 'off',
    screenshot: 'off',
    video: 'off',
    ...devices['Desktop Chrome'],
  },
  projects: [
    {
      name: 'chromium',
      use: { baseURL: webOrigin, ...devices['Desktop Chrome'] },
    },
  ],
  webServer: [
    {
      command: `dotnet run --no-launch-profile --project ${apiProject} --configuration Release`,
      cwd: resolve(process.cwd(), '..'),
      url: `${apiOrigin}/health/live`,
      reuseExistingServer: false,
      timeout: 120_000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'E2E',
        DOTNET_ENVIRONMENT: 'E2E',
        ASPNETCORE_URLS: apiOrigin,
        ConnectionStrings__TaskManager: connectionString,
        Security__AllowedOrigins__0: webOrigin,
      },
    },
    {
      command: 'npm run dev -- --host 127.0.0.1 --port 5180 --strictPort',
      cwd: process.cwd(),
      url: webOrigin,
      reuseExistingServer: false,
      timeout: 120_000,
      env: {
        VITE_PORT: '5180',
        VITE_API_PROXY_TARGET: apiOrigin,
      },
    },
  ],
});
