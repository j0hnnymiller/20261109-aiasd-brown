import { spawn } from 'node:child_process';
import { rm } from 'node:fs/promises';
import { resolve } from 'node:path';
import { Client } from 'pg';
import {
  createApiChildEnvironment,
  e2eServerTopology,
  ensurePortsAvailable,
  parsePostgresConnection,
  playwrightOutputDirectory,
} from './e2e-config.mjs';

function databaseClient(connectionString) {
  const parsed = parsePostgresConnection(connectionString);
  if (!parsed.host || !parsed.database || !parsed.user || !parsed.password) {
    throw new Error('E2E database configuration is incomplete.');
  }

  return new Client({
    host: parsed.host,
    port: parsed.port,
    database: parsed.database,
    user: parsed.user,
    password: parsed.password,
    ssl: false,
    connectionTimeoutMillis: 5000,
  });
}

async function snapshot(connectionString) {
  const client = databaseClient(connectionString);
  try {
    await client.connect();
    const schema = await client.query(
      "SELECT to_regclass('public.workspaces') IS NOT NULL AS ready",
    );
    if (!schema.rows[0]?.ready) {
      return { ready: false, workspaces: 0, lists: 0, tasks: 0 };
    }

    const counts = await client.query(
      `SELECT
         (SELECT count(*)::text FROM public.workspaces) AS workspaces,
         (SELECT count(*)::text FROM public.task_lists) AS lists,
         (SELECT count(*)::text FROM public.tasks) AS tasks`,
    );
    return {
      ready: true,
      workspaces: Number(counts.rows[0].workspaces),
      lists: Number(counts.rows[0].lists),
      tasks: Number(counts.rows[0].tasks),
    };
  } catch {
    throw new Error('The isolated browser-test database preflight failed.');
  } finally {
    await client.end().catch(() => undefined);
  }
}

function runPlaywright(argumentsList, environment) {
  return new Promise(resolveResult => {
    const cli = resolve('node_modules/@playwright/test/cli.js');
    const child = spawn(
      process.execPath,
      [cli, 'test', ...argumentsList],
      { cwd: process.cwd(), env: environment, stdio: 'inherit' },
    );
    child.once('error', () => resolveResult(1));
    child.once('exit', code => resolveResult(code ?? 1));
  });
}

function equalSnapshot(left, right) {
  return left.ready === right.ready &&
    left.workspaces === right.workspaces &&
    left.lists === right.lists &&
    left.tasks === right.tasks;
}

async function main() {
  const source = { ...process.env };
  createApiChildEnvironment(source);
  await ensurePortsAvailable([
    e2eServerTopology.frontend.port,
    e2eServerTopology.api.port,
  ]);

  const apiTestBefore = await snapshot(source.TASK_MANAGER_TEST_CONNECTION);
  const e2eBefore = await snapshot(source.ConnectionStrings__TaskManager);
  await rm(playwrightOutputDirectory, { recursive: true, force: true });
  const browserEnvironment = {
    ...source,
    PLAYWRIGHT_NO_COPY_PROMPT: '1',
  };
  const browserExit = await runPlaywright(process.argv.slice(2), browserEnvironment);
  await rm(playwrightOutputDirectory, { recursive: true, force: true });
  const apiTestAfter = await snapshot(source.TASK_MANAGER_TEST_CONNECTION);
  const e2eAfter = await snapshot(source.ConnectionStrings__TaskManager);

  const apiTestUnchanged = equalSnapshot(apiTestBefore, apiTestAfter);
  const e2eDatabaseUsed = e2eAfter.workspaces > e2eBefore.workspaces ||
    e2eAfter.tasks > e2eBefore.tasks;
  if (apiTestUnchanged && e2eDatabaseUsed) {
    process.stdout.write('E2eIgnoresInheritedApiTestOverride: passed.\n');
  } else {
    process.stderr.write('E2eIgnoresInheritedApiTestOverride: failed; database isolation was not preserved.\n');
  }

  if (!apiTestUnchanged || !e2eDatabaseUsed) {
    process.exitCode = 1;
    return;
  }

  process.exitCode = browserExit;
}

main().catch(error => {
  process.stderr.write(
    error instanceof Error ? `${error.message}\n` : 'E2E preflight failed.\n',
  );
  process.exitCode = 1;
});
