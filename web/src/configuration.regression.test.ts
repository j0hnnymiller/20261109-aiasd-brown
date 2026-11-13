import { describe, expect, it } from 'vitest';
import {
  createApiChildEnvironment,
  credentialCaptureSettings,
  e2eServerTopology,
  effectiveTaskManagerConnection,
  ensurePortsAvailable,
} from '../scripts/e2e-config.mjs';

describe('isolated browser-test configuration', () => {
  const apiTestConnection =
    'Host=127.0.0.1;Port=5432;Database=task_manager_test;Username=runner;******';
  const e2eConnection =
    'Host=127.0.0.1;Port=5432;Database=task_manager_e2e;Username=runner;******';

  it('E2eIgnoresInheritedApiTestOverride when both databases are exported', () => {
    const source = {
      TASK_MANAGER_TEST_CONNECTION: apiTestConnection,
      ConnectionStrings__TaskManager: e2eConnection,
      TASK_MANAGER_CERT_DIR: '/tmp/task-manager-certs',
    };
    const child = createApiChildEnvironment(source);
    const effective = effectiveTaskManagerConnection(child);
    const isolated = child.TASK_MANAGER_TEST_CONNECTION === undefined &&
      effective === e2eConnection &&
      child.TASK_MANAGER_EXPECTED_DATABASE === 'task_manager_e2e';

    expect(isolated).toBe(true);
  });

  it('CredentialFlowsDoNotRetainSensitiveCaptures', () => {
    const noCaptures = credentialCaptureSettings.trace === 'off' &&
      credentialCaptureSettings.screenshot === 'off' &&
      credentialCaptureSettings.video === 'off' &&
      credentialCaptureSettings.pageSnapshot === 'off';
    expect(noCaptures).toBe(true);
  });

  it('BrowserTestsOwnTheirServerAndDatabase on dedicated trusted ports', () => {
    const separatePorts = Number(e2eServerTopology.frontend.port) !==
        Number(e2eServerTopology.development.frontendPort) &&
      Number(e2eServerTopology.api.port) !== Number(e2eServerTopology.development.apiPort);
    const dedicatedHttpsServers = e2eServerTopology.frontend.origin.startsWith('https://') &&
      e2eServerTopology.api.url.startsWith('https://') &&
      !e2eServerTopology.frontend.reuseExistingServer &&
      !e2eServerTopology.api.reuseExistingServer;
    expect(separatePorts && dedicatedHttpsServers).toBe(true);
  });

  it('fails occupied browser-test ports before the runner can mutate a database', async () => {
    let rejected = false;
    try {
      await ensurePortsAvailable([e2eServerTopology.api.port], async () => true);
    } catch {
      rejected = true;
    }

    expect(rejected).toBe(true);
  });

  it('rejects a missing or misdirected E2E database before starting servers', () => {
    let rejected = false;
    try {
      createApiChildEnvironment({
        TASK_MANAGER_TEST_CONNECTION: apiTestConnection,
        ConnectionStrings__TaskManager: apiTestConnection,
      });
    } catch {
      rejected = true;
    }

    expect(rejected).toBe(true);
  });
});
