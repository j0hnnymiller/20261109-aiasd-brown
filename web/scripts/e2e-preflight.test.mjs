import { describe, expect, it } from 'vitest';
import { createServer } from 'node:net';
import { access, mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { isPortAvailable, isSafeE2eConnectionString, validateE2eEnvironment } from './e2e-preflight.mjs';
import cleanupE2eArtifacts from './cleanup-e2e-artifacts.mjs';

describe('E2E preflight', () => {
  it('rejects missing and unsafe database settings without displaying them', async () => {
    expect(isSafeE2eConnectionString(undefined)).toBe(false);
    expect(isSafeE2eConnectionString('Host=example.invalid;Database=task_manager_e2e;Username=test')).toBe(false);
    expect(isSafeE2eConnectionString('Host=127.0.0.1;Database=task_manager_dev;Username=test')).toBe(false);
    await expect(validateE2eEnvironment({})).rejects.toThrow(/local PostgreSQL connection string/);
  });

  it('detects an occupied dedicated port before any API starts', async () => {
    const server = createServer();
    await new Promise((resolveListen, reject) => {
      server.once('error', reject);
      server.listen(0, '127.0.0.1', resolveListen);
    });
    const address = server.address();
    if (!address || typeof address === 'string') {
      throw new Error('Expected a local test socket.');
    }

    try {
      expect(await isPortAvailable(address.port)).toBe(false);
      await expect(validateE2eEnvironment({
        ConnectionStrings__TaskManager: 'Host=127.0.0.1;Database=task_manager_e2e;Username=test',
      }, [address.port])).rejects.toThrow(/dedicated E2E port is already in use/);
    } finally {
      await new Promise((resolveClose) => server.close(() => resolveClose()));
    }
  });

  it('removes browser output artifacts after test runs', async () => {
    const outputDirectory = resolve(tmpdir(), 'task-manager-playwright-results');
    await mkdir(outputDirectory, { recursive: true });
    await writeFile(resolve(outputDirectory, 'sentinel'), 'test artifact');

    await cleanupE2eArtifacts();

    await expect(access(outputDirectory)).rejects.toThrow();
  });
});
