import { spawn } from 'node:child_process';
import { resolve } from 'node:path';
import { createApiChildEnvironment } from './e2e-config.mjs';

const repositoryRoot = resolve(process.cwd(), '..');
let environment;
try {
  environment = createApiChildEnvironment(process.env);
} catch {
  process.stderr.write('E2E API configuration was rejected before startup.\n');
  process.exit(1);
}

const child = spawn(
  'dotnet',
  [
    'run',
    '--project',
    resolve(repositoryRoot, 'src/TaskManager.Api/TaskManager.Api.csproj'),
    '--configuration',
    'Release',
  ],
  {
    cwd: repositoryRoot,
    env: environment,
    stdio: ['ignore', 'ignore', 'ignore'],
  },
);

for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => child.kill(signal));
}

child.once('error', () => {
  process.stderr.write('The E2E API server could not be started.\n');
  process.exitCode = 1;
});
child.once('exit', code => {
  if (code !== 0 && code !== null) {
    process.stderr.write('The E2E API server exited before the browser checks completed.\n');
  }
  process.exitCode = code ?? 1;
});
