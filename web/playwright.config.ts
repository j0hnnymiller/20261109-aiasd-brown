import { defineConfig, devices } from '@playwright/test';
import {
  credentialCaptureSettings,
  e2eServerTopology,
  playwrightOutputDirectory,
} from './scripts/e2e-config.mjs';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: process.env.CI === 'true',
  retries: 0,
  workers: 1,
  reporter: 'line',
  outputDir: playwrightOutputDirectory,
  timeout: 45_000,
  use: {
    baseURL: e2eServerTopology.frontend.origin,
    ignoreHTTPSErrors: false,
    ...devices['Desktop Chrome'],
    trace: credentialCaptureSettings.trace,
    screenshot: credentialCaptureSettings.screenshot,
    video: credentialCaptureSettings.video,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: [
    {
      command: 'node scripts/start-e2e-api.mjs',
      url: `${e2eServerTopology.api.url}/api/health/ready`,
      reuseExistingServer: e2eServerTopology.api.reuseExistingServer,
      timeout: 120_000,
    },
    {
      command: 'node scripts/start-e2e-vite.mjs',
      url: e2eServerTopology.frontend.origin,
      reuseExistingServer: e2eServerTopology.frontend.reuseExistingServer,
      timeout: 120_000,
    },
  ],
});
