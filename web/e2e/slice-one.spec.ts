import { expect, test } from '@playwright/test';
import playwrightConfig from '../playwright.config';
import { isSafeE2eConnectionString } from '../scripts/e2e-preflight.mjs';

test.afterEach(async ({ page }) => {
  try {
    await page.evaluate(() => {
      document.querySelectorAll('[aria-label="Recovery secret"]').forEach((element) => {
        element.textContent = '[redacted]';
      });
    });
  } catch {
    return;
  }
});

async function createWorkspace(page: import('@playwright/test').Page) {
  const timeZoneResponsePromise = page.waitForResponse(
    (response) => new URL(response.url()).pathname === '/api/time-zones',
    { timeout: 10_000 },
  );
  await page.goto('/', { waitUntil: 'domcontentloaded', timeout: 10_000 });
  const timeZoneResponse = await timeZoneResponsePromise;
  expect(timeZoneResponse.status()).toBe(200);
  const timeZone = page.getByRole('combobox', { name: 'Workspace time zone' });
  await expect(timeZone).toBeEnabled();
  await timeZone.selectOption('UTC');
  const workspaceResponsePromise = page.waitForResponse(
    (response) => new URL(response.url()).pathname === '/api/workspaces'
      && response.request().method() === 'POST',
    { timeout: 10_000 },
  );
  await page.getByRole('button', { name: 'Create my private task space' }).click();
  const workspaceResponse = await workspaceResponsePromise;
  expect(workspaceResponse.status()).toBe(201);
  await continueFromRecovery(page);
  await expect(page.getByRole('heading', { name: 'Tasks', exact: true })).toBeVisible();
}

async function waitForRecoverySecret(page: import('@playwright/test').Page) {
  await page.waitForFunction(
    () => {
      const output = document.querySelector('[aria-label="Recovery secret"]');
      return output !== null && output.textContent !== '';
    },
    undefined,
    { timeout: 5_000 },
  );
}

async function continueFromRecovery(page: import('@playwright/test').Page) {
  await waitForRecoverySecret(page);
  await page.getByRole('button', { name: "I've saved the recovery secret" }).click();
}

test('BrowserTestsOwnTheirServerAndDatabase', async ({ page, context }) => {
  expect(isSafeE2eConnectionString(process.env.ConnectionStrings__TaskManager)).toBe(true);

  await createWorkspace(page);
  expect(new URL(page.url()).port).toBe('5180');
  const cookieNames = (await context.cookies('http://127.0.0.1:5180')).map((cookie) => cookie.name);
  expect(cookieNames).toContain('__Host-device');

  await page.reload();
  await expect(page.getByRole('heading', { name: 'Tasks', exact: true })).toBeVisible();
  await expect(page.getByText('Workspace time zone:')).toContainText('UTC');
});

test('CredentialFlowsDoNotRetainSensitiveCaptures', async () => {
  const captureSettings = playwrightConfig.use;
  expect(captureSettings.trace).toBe('off');
  expect(captureSettings.screenshot).toBe('off');
  expect(captureSettings.video).toBe('off');
  expect(test.info().attachments).toHaveLength(0);
});

test('TimeZoneLoadFailureCanBeRetried', async ({ page }) => {
  let requests = 0;
  await page.route('**/api/time-zones', async (route) => {
    requests += 1;
    if (requests === 1) {
      await route.fulfill({ status: 503, contentType: 'application/problem+json', body: '{"title":"Unavailable"}' });
      return;
    }
    await route.continue();
  });

  await page.goto('/', { waitUntil: 'domcontentloaded', timeout: 10_000 });
  await expect(page.getByRole('alert', { name: 'Time zone loading error' })).toBeVisible();
  await page.getByRole('button', { name: 'Retry time zone loading' }).click();
  await expect(page.getByRole('option', { name: 'UTC', exact: true })).toBeAttached();
  await page.getByRole('combobox', { name: 'Workspace time zone' }).selectOption('UTC');
  await page.getByRole('button', { name: 'Create my private task space' }).click();
  await waitForRecoverySecret(page);
  expect(requests).toBe(2);
});

test('PendingSaveCannotEraseNewerDraft', async ({ page }) => {
  await createWorkspace(page);
  let releaseResponse = () => {};
  let signalRequest = () => {};
  const requestStarted = new Promise<void>((resolve) => {
    signalRequest = resolve;
  });
  const responseReleased = new Promise<void>((resolve) => {
    releaseResponse = resolve;
  });

  await page.route('**/api/tasks', async (route) => {
    if (route.request().method() === 'POST') {
      signalRequest();
      await responseReleased;
    }
    await route.continue();
  });

  const title = page.getByRole('textbox', { name: 'Task title' });
  await title.fill('Submitted draft');
  await page.getByRole('button', { name: 'Save task' }).click();
  await requestStarted;

  await expect(title).toBeDisabled();
  await expect(title).toHaveValue('Submitted draft');
  await expect(page.getByRole('button', { name: 'Saving task' })).toBeDisabled();

  releaseResponse();
  await expect(page.getByRole('status')).toHaveText('Saved');
  await expect(title).toHaveValue('');
  await expect(page.getByText('Submitted draft')).toBeVisible();
});

test('ReturningDeviceAcquiresMissingCsrfBeforeMutation', async ({ page, context }) => {
  await createWorkspace(page);
  await context.clearCookies({ name: '__Host-csrf' });
  const remainingCookieNames = (await context.cookies('http://127.0.0.1:5180')).map((cookie) => cookie.name);
  expect(remainingCookieNames).toContain('__Host-device');
  expect(remainingCookieNames).not.toContain('__Host-csrf');

  const apiRequests: string[] = [];
  page.on('request', (request) => {
    const url = new URL(request.url());
    if (url.pathname.startsWith('/api/')) {
      apiRequests.push(`${request.method()} ${url.pathname}`);
    }
  });

  await page.getByRole('textbox', { name: 'Task title' }).fill('Returning device task');
  await page.getByRole('button', { name: 'Save task' }).click();
  await expect(page.getByRole('status')).toHaveText('Saved');
  expect(apiRequests.indexOf('GET /api/security/csrf')).toBeGreaterThanOrEqual(0);
  expect(apiRequests.indexOf('GET /api/security/csrf')).toBeLessThan(apiRequests.indexOf('POST /api/tasks'));
  await expect(page.getByText('Returning device task')).toBeVisible();
});

test('CsrfAcquisitionFailurePreservesDraftAndSendsNoMutation', async ({ page, context }) => {
  await createWorkspace(page);
  await context.clearCookies({ name: '__Host-csrf' });
  let taskWrites = 0;
  await page.route('**/api/security/csrf', (route) =>
    route.fulfill({ status: 503, contentType: 'application/problem+json', body: '{"title":"Unavailable"}' }));
  page.on('request', (request) => {
    if (request.url().includes('/api/tasks') && request.method() === 'POST') {
      taskWrites += 1;
    }
  });

  const title = page.getByRole('textbox', { name: 'Task title' });
  await title.fill('Preserved draft');
  await page.getByRole('button', { name: 'Save task' }).click();
  await expect(page.getByRole('alert')).toContainText('Your changes are still here');
  await expect(title).toHaveValue('Preserved draft');
  expect(taskWrites).toBe(0);
});

test('TaskRefreshDoesNotPollWhileThePageIsIdle', async ({ page }) => {
  await createWorkspace(page);
  await expect(page.getByText('No tasks yet.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Refresh tasks' })).toBeEnabled();
  let taskReads = 0;
  page.on('request', (request) => {
    if (new URL(request.url()).pathname === '/api/tasks' && request.method() === 'GET') {
      taskReads += 1;
    }
  });

  await page.waitForTimeout(1500);
  expect(taskReads).toBe(0);
  await page.getByRole('button', { name: 'Refresh tasks' }).click();
  await expect.poll(() => taskReads).toBe(1);
});

test('LostWorkspaceResponseRequiresAnExplicitSeparateWorkspaceDecision', async ({ page }) => {
  let createRequests = 0;
  await page.route('**/api/workspaces', async (route) => {
    createRequests += 1;
    await route.abort();
  });
  await page.goto('/', { waitUntil: 'domcontentloaded', timeout: 10_000 });
  const timeZone = page.getByRole('combobox', { name: 'Workspace time zone' });
  await expect(timeZone).toBeEnabled();
  await timeZone.selectOption('UTC');
  await page.getByRole('button', { name: 'Create my private task space' }).click();
  await expect(page.getByRole('alert')).toContainText('Your changes are still here');

  await page.reload();
  await expect(page.getByRole('heading', { name: 'Setup could not be recovered' })).toBeVisible();
  expect(createRequests).toBe(1);
  await page.getByRole('button', { name: 'I understand, start a separate task space' }).click();
  await expect(page.getByRole('button', { name: 'Create my private task space' })).toBeVisible();
  expect(createRequests).toBe(1);
});
