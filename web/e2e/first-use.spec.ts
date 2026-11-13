import { expect, test } from '@playwright/test';

const appUrl = 'https://localhost:4173';
const apiUrl = 'https://localhost:4174';

async function createWorkspace(page: import('@playwright/test').Page) {
  await page.goto(appUrl);
  await page.getByRole('button', { name: 'Create task space' }).waitFor();

  const zone = page.getByLabel('Workspace time zone');
  await expect(zone).toBeEnabled();
  const selectedZone = await zone.inputValue();
  if (!selectedZone) {
    const losAngelesOption = await zone.locator('option[value="America/Los_Angeles"]').count();
    await zone.selectOption(losAngelesOption ? 'America/Los_Angeles' : { index: 1 });
  }

  await page.getByRole('button', { name: 'Create task space' }).click();
  await page.getByRole('heading', { name: 'Save your recovery secret' }).waitFor();

  const secret = await page.getByRole('status', { name: 'Recovery secret', exact: true }).textContent();
  const secretWasPresented = typeof secret === 'string' && secret.length >= 40;
  const browserStorageIsEmpty = await page.evaluate(
    () => localStorage.length === 0 && sessionStorage.length === 0,
  );
  const urlDoesNotContainSecret = !secret || !page.url().includes(secret);
  expect(secretWasPresented && browserStorageIsEmpty && urlDoesNotContainSecret).toBe(true);

  await page.getByRole('button', { name: 'I saved this secret' }).click();
  await page.getByRole('heading', { name: 'Your tasks' }).waitFor();
  const cookies = await page.context().cookies(appUrl);
  const device = cookies.find(cookie => cookie.name === '__Host-device');
  return device?.value;
}

test('CredentialTransportUsesTrustedHttps for browser and proxy/API requests', async ({ page }) => {
  await page.goto(appUrl);
  const browserUsesHttps = await page.evaluate(() => location.protocol === 'https:');
  const apiResponse = await page.request.get(`${apiUrl}/api/health/ready`);
  const apiUsesHttps = apiResponse.url().startsWith('https://') && apiResponse.ok();
  expect(browserUsesHttps && apiUsesHttps).toBe(true);

  const deviceToken = await createWorkspace(page);
  const deviceCookie = (await page.context().cookies(appUrl))
    .find(cookie => cookie.name === '__Host-device');
  const cookiePolicyIsSecure = Boolean(deviceToken) &&
    deviceCookie?.secure === true &&
    deviceCookie.httpOnly === true &&
    deviceCookie.sameSite === 'Lax' &&
    deviceCookie.path === '/' &&
    !deviceCookie.domain.startsWith('.');
  expect(cookiePolicyIsSecure).toBe(true);
});

test('first task persists with its date-only value and appears after reopening', async ({ page }) => {
  await createWorkspace(page);

  await page.getByLabel('Task title').fill('Slice one task');
  await page.getByLabel('Description (optional)').fill('First saved item.');
  await page.getByLabel('Due date (optional)').fill('2026-12-05');
  await page.getByLabel('Priority (optional)').selectOption('High');
  await page.getByRole('button', { name: 'Save task' }).click();
  await page.locator('ul[aria-label="Tasks"] li').first().waitFor();

  const taskFieldsPreserved = await page.locator('ul[aria-label="Tasks"] li').evaluate(element => {
    const text = element.textContent ?? '';
    return text.includes('Slice one task') &&
      text.includes('First saved item.') &&
      text.includes('Due 2026-12-05') &&
      text.includes('Priority: High');
  });
  expect(taskFieldsPreserved).toBe(true);

  await page.reload();
  await page.locator('ul[aria-label="Tasks"] li').first().waitFor();
  const persistedCount = await page.locator('ul[aria-label="Tasks"] li').count();
  const dateRoundTrip = await page.locator('ul[aria-label="Tasks"] li').first().evaluate(element =>
    (element.textContent ?? '').includes('Due 2026-12-05'),
  );
  expect(persistedCount === 1 && dateRoundTrip).toBe(true);

  const taskReads: number[] = [];
  page.on('request', request => {
    if (request.method() === 'GET' && new URL(request.url()).pathname === '/api/tasks') {
      taskReads.push(Date.now());
    }
  });
  await page.waitForTimeout(100);
  const baselineReads = taskReads.length;
  await page.waitForTimeout(1200);
  const noPolling = taskReads.length === baselineReads;
  expect(noPolling).toBe(true);

  await page.getByLabel('Task title').fill('   ');
  const beforeInvalidSave = await page.locator('ul[aria-label="Tasks"] li').count();
  await page.getByRole('button', { name: 'Save task' }).click();
  const validationVisible = await page.getByRole('alert').count();
  const afterInvalidSave = await page.locator('ul[aria-label="Tasks"] li').count();
  expect(validationVisible > 0 && beforeInvalidSave === afterInvalidSave).toBe(true);
});

test('TimeZoneLoadFailureCanBeRetried without a reload', async ({ page }) => {
  let firstAttempt = true;
  await page.route('**/api/time-zones', async route => {
    if (firstAttempt) {
      firstAttempt = false;
      await route.fulfill({
        status: 503,
        contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Unavailable', status: 503 }),
      });
      return;
    }

    await route.continue();
  });

  await page.goto(appUrl);
  await page.getByRole('button', { name: 'Retry time zones' }).click();
  const zone = page.getByLabel('Workspace time zone');
  await expect(zone).toBeEnabled();
  const retrySucceeded = (await zone.locator('option').count()) > 1;
  expect(retrySucceeded).toBe(true);

  const pageUrl = page.url();
  await zone.selectOption('America/Los_Angeles');
  await page.getByRole('button', { name: 'Create task space' }).click();
  await page.getByRole('heading', { name: 'Save your recovery secret' }).waitFor();
  expect(page.url() === pageUrl).toBe(true);
});

test('ReturningDeviceAcquiresMissingCsrfBeforeMutation and preserves the draft on token failure', async ({ page }) => {
  await createWorkspace(page);
  const requests: Array<{ path: string; method: string; csrfHeader: boolean }> = [];
  page.on('request', request => {
    const path = new URL(request.url()).pathname;
    if (path === '/api/csrf' || (path === '/api/tasks' && request.method() === 'POST')) {
      requests.push({
        path,
        method: request.method(),
        csrfHeader: Boolean(request.headers()['x-csrf-token']),
      });
    }
  });

  await page.evaluate(() => {
    document.cookie = '__Host-csrf=; Max-Age=0; Path=/; Secure';
  });
  await page.getByLabel('Task title').fill('Returning device task');
  await page.getByRole('button', { name: 'Save task' }).click();
  await page.locator('ul[aria-label="Tasks"] li').first().waitFor();
  const csrfWasAcquiredBeforeWrite = requests.length === 2 &&
    requests[0].path === '/api/csrf' &&
    requests[0].method === 'GET' &&
    requests[1].path === '/api/tasks' &&
    requests[1].method === 'POST' &&
    requests[1].csrfHeader;
  expect(csrfWasAcquiredBeforeWrite).toBe(true);

  requests.length = 0;
  await page.evaluate(() => {
    document.cookie = '__Host-csrf=; Max-Age=0; Path=/; Secure';
  });
  await page.route('**/api/csrf', route => route.abort());
  await page.getByLabel('Task title').fill('Preserved after CSRF failure');
  await page.getByRole('button', { name: 'Save task' }).click();
  await page.getByRole('button', { name: 'Retry original task' }).waitFor();

  const draftPreserved = await page.getByLabel('Task title').inputValue() ===
    'Preserved after CSRF failure';
  const noWriteAfterTokenFailure = requests.every(request =>
    request.path !== '/api/tasks' || request.method !== 'POST',
  );
  expect(draftPreserved && noWriteAfterTokenFailure).toBe(true);
});

test('TransientAccessCheckFailureCanBeRetried without replacing device identity', async ({ page }) => {
  const originalDevice = await createWorkspace(page);
  let failWorkspaceRead = true;
  let failTaskRead = true;
  let workspaceCreates = 0;
  page.on('request', request => {
    if (request.url().endsWith('/api/workspaces') && request.method() === 'POST') {
      workspaceCreates += 1;
    }
  });
  await page.route('**/api/workspace', async route => {
    if (failWorkspaceRead && route.request().method() === 'GET') {
      failWorkspaceRead = false;
      await route.fulfill({ status: 503, body: '' });
      return;
    }
    await route.continue();
  });
  await page.route('**/api/tasks', async route => {
    if (failTaskRead && route.request().method() === 'GET') {
      failTaskRead = false;
      await route.fulfill({ status: 503, body: '' });
      return;
    }
    await route.continue();
  });

  await page.reload();
  await page.getByRole('button', { name: 'Retry workspace access' }).waitFor();
  await page.getByRole('button', { name: 'Retry workspace access' }).click();
  await page.getByRole('button', { name: 'Retry task loading' }).waitFor();
  await page.getByRole('button', { name: 'Retry task loading' }).click();
  await page.getByRole('heading', { name: 'Your tasks' }).waitFor();

  const finalDevice = (await page.context().cookies(appUrl))
    .find(cookie => cookie.name === '__Host-device')?.value;
  const identityPreserved = Boolean(originalDevice) &&
    originalDevice === finalDevice &&
    workspaceCreates === 0;
  expect(identityPreserved).toBe(true);
});

test('PendingSaveCannotEraseNewerDraft after a real delayed response', async ({ page }) => {
  await createWorkspace(page);
  let releaseResponse: (() => void) | undefined;
  let markCommitted: (() => void) | undefined;
  const responseGate = new Promise<void>(resolve => {
    releaseResponse = resolve;
  });
  const committed = new Promise<void>(resolve => {
    markCommitted = resolve;
  });

  await page.route('**/api/tasks', async route => {
    if (route.request().method() === 'POST') {
      const response = await route.fetch();
      markCommitted?.();
      await responseGate;
      await route.fulfill({ response });
      return;
    }
    await route.continue();
  });

  await page.getByLabel('Task title').fill('Submitted while pending');
  await page.getByRole('button', { name: 'Save task' }).click();
  await committed;
  await page.getByLabel('Task title').fill('Newer editable draft');
  releaseResponse?.();
  await page.locator('ul[aria-label="Tasks"] li').first().waitFor();

  const draftSurvived = await page.getByLabel('Task title').inputValue() === 'Newer editable draft';
  const submittedTaskPersisted = await page.locator('ul[aria-label="Tasks"] li').count() === 1;
  expect(draftSurvived && submittedTaskPersisted).toBe(true);
});

test('LostCreateResponseRetriesImmutableOperation exactly once before a distinct draft save', async ({ page }) => {
  await createWorkspace(page);
  let loseFirstResponse = true;
  const operations: Array<{ key: string | undefined; title: string | undefined }> = [];
  page.on('request', request => {
    if (request.url().endsWith('/api/tasks') && request.method() === 'POST') {
      const body: unknown = request.postDataJSON();
      const title = typeof body === 'object' && body !== null &&
          'title' in body && typeof body.title === 'string'
        ? body.title
        : undefined;
      operations.push({
        key: request.headers()['idempotency-key'],
        title,
      });
    }
  });
  await page.route('**/api/tasks', async route => {
    if (route.request().method() === 'POST' && loseFirstResponse) {
      loseFirstResponse = false;
      await route.fetch();
      await route.abort('failed');
      return;
    }
    await route.continue();
  });

  await page.getByLabel('Task title').fill('Original committed task');
  await page.getByRole('button', { name: 'Save task' }).click();
  await page.getByRole('button', { name: 'Retry original task' }).waitFor();
  await page.getByLabel('Task title').fill('Intentionally edited task');
  await page.getByRole('button', { name: 'Retry original task' }).click();
  await page.locator('ul[aria-label="Tasks"] li').first().waitFor();
  await page.getByRole('button', { name: 'Save task' }).click();

  const storedTasks = await page.evaluate(async () => {
    const response = await fetch('/api/tasks');
    const result: unknown = await response.json();
    return Array.isArray(result) ? result : [];
  });
  const taskTitles = storedTasks
    .filter((value): value is { title: string } =>
      typeof value === 'object' && value !== null &&
      'title' in value && typeof value.title === 'string',
    )
    .map(value => value.title);
  const sameOriginalPayloadAndKey = operations.length === 3 &&
    operations[0].key !== undefined &&
    operations[0].key === operations[1].key &&
    operations[0].title === 'Original committed task' &&
    operations[1].title === 'Original committed task';
  const distinctEditedOperation = operations[2]?.key !== undefined &&
    operations[2].key !== operations[1]?.key &&
    operations[2].title === 'Intentionally edited task';
  const persistedExactlyOnce = taskTitles.filter(title => title === 'Original committed task').length === 1 &&
    taskTitles.filter(title => title === 'Intentionally edited task').length === 1 &&
    taskTitles.length === 2;
  expect(sameOriginalPayloadAndKey && distinctEditedOperation && persistedExactlyOnce).toBe(true);
  operations.length = 0;
  taskTitles.length = 0;
});
