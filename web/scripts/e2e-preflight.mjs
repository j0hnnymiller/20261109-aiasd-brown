import { createServer } from 'node:net';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

const testPorts = [5279, 5180];

export function isSafeE2eConnectionString(connectionString) {
  if (!connectionString) {
    return false;
  }

  const values = new Map();
  for (const part of connectionString.split(';')) {
    const separator = part.indexOf('=');
    if (separator <= 0) {
      continue;
    }

    values.set(part.slice(0, separator).trim().toLowerCase(), part.slice(separator + 1).trim());
  }

  const host = values.get('host') ?? values.get('server') ?? '';
  const database = values.get('database') ?? '';
  const username = values.get('username') ?? values.get('user id') ?? values.get('userid') ?? '';
  return ['localhost', '127.0.0.1', '::1'].includes(host.toLowerCase())
    && database.toLowerCase().endsWith('_e2e')
    && username.length > 0;
}

export function isPortAvailable(port) {
  return new Promise((resolveAvailable) => {
    const server = createServer();
    server.once('error', () => resolveAvailable(false));
    server.listen(port, '127.0.0.1', () => {
      server.close(() => resolveAvailable(true));
    });
  });
}

export async function validateE2eEnvironment(environment = process.env, ports = testPorts) {
  if (!isSafeE2eConnectionString(environment.ConnectionStrings__TaskManager)) {
    throw new Error('E2E requires a local PostgreSQL connection string with a database name ending in _e2e.');
  }

  for (const port of ports) {
    if (!await isPortAvailable(port)) {
      throw new Error('A dedicated E2E port is already in use; stop the conflicting test service before running E2E.');
    }
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  validateE2eEnvironment().then(
    () => console.log('E2E database and dedicated ports are safe.'),
    (error) => {
      console.error(error.message);
      process.exitCode = 1;
    },
  );
}
