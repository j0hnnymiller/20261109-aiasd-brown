import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: 'http://127.0.0.1:5173',
    trace: 'retain-on-failure',
  },
  webServer: [
    {
      command: 'dotnet run --project ../src/TaskManager.Api/TaskManager.Api.csproj --urls http://127.0.0.1:5000',
      url: 'http://127.0.0.1:5000/health/ready',
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: process.env,
    },
    {
      command: 'npm run dev',
      url: 'http://127.0.0.1:5173',
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
  ],
})
