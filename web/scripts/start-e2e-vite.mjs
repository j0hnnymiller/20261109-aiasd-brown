import { spawn } from 'node:child_process';
import { createApiChildEnvironment, e2eServerTopology } from './e2e-config.mjs';

let environment;
try {
  environment = createApiChildEnvironment(process.env);
} catch {
  process.stderr.write('E2E frontend configuration was rejected before startup.\n');
  process.exit(1);
}

environment.VITE_PORT = String(e2eServerTopology.frontend.port);
const child = spawn(
  process.execPath,
  [
    'node_modules/vite/bin/vite.js',
    '--host',
    'localhost',
    '--port',
    String(e2eServerTopology.frontend.port),
    '--strictPort',
  ],
  {
    cwd: process.cwd(),
    env: environment,
    stdio: ['ignore', 'ignore', 'ignore'],
  },
);

for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => child.kill(signal));
}

child.once('error', () => {
  process.stderr.write('The isolated E2E frontend could not be started.\n');
  process.exitCode = 1;
});
child.once('exit', code => {
  if (code !== 0 && code !== null) {
    process.stderr.write('The isolated E2E frontend exited before browser checks completed.\n');
  }
  process.exitCode = code ?? 1;
});
