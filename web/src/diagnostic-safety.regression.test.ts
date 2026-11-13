import { spawnSync } from 'node:child_process';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const sentinel = 'SYNTHETIC-VITEST-CREDENTIAL-SENTINEL';

describe('assertion diagnostic safety', () => {
  it('AssertionFailuresDoNotExposeCredentialArguments for setup-mock matchers', () => {
    const child = spawnSync(
      process.execPath,
      [
        resolve('node_modules/vitest/vitest.mjs'),
        'run',
        'src/diagnostic-sentinel.fixture.test.ts',
        '--reporter=default',
      ],
      {
        cwd: process.cwd(),
        encoding: 'utf8',
        timeout: 30000,
        env: {
          ...process.env,
          TASK_MANAGER_DIAGNOSTIC_FIXTURE: '1',
        },
      },
    );
    const diagnostics = `${child.stdout ?? ''}${child.stderr ?? ''}`;
    const failedAsExpected = child.status !== 0;
    const sentinelAbsent = !diagnostics.includes(sentinel);

    expect(failedAsExpected && sentinelAbsent).toBe(true);
  });
});
