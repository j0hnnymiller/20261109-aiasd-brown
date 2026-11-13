import { createServer } from 'node:net';
import { resolve } from 'node:path';

export const e2eServerTopology = {
  frontend: {
    port: 4173,
    origin: 'https://localhost:4173',
    reuseExistingServer: false,
  },
  api: {
    port: 4174,
    url: 'https://localhost:4174',
    reuseExistingServer: false,
  },
  development: {
    frontendPort: 5173,
    apiPort: 5001,
  },
};

export const playwrightOutputDirectory = '/tmp/task-manager-playwright-results';

export const credentialCaptureSettings = {
  trace: 'off',
  screenshot: 'off',
  video: 'off',
  pageSnapshot: 'off',
};

export function effectiveTaskManagerConnection(environment) {
  return environment.TASK_MANAGER_TEST_CONNECTION ?? environment.ConnectionStrings__TaskManager;
}

export function parsePostgresConnection(connectionString) {
  const values = new Map();
  let item = '';
  let quote = null;
  for (let index = 0; index < connectionString.length; index += 1) {
    const character = connectionString[index];
    if (quote) {
      if (character === quote && connectionString[index + 1] === quote) {
        item += character;
        index += 1;
      } else if (character === quote) {
        quote = null;
      } else {
        item += character;
      }
    } else if ((character === "'" || character === '"') && item.includes('=')) {
      quote = character;
    } else if (character === ';') {
      saveEntry(values, item);
      item = '';
    } else {
      item += character;
    }
  }
  saveEntry(values, item);

  const get = (...keys) => {
    for (const key of keys) {
      const value = values.get(key);
      if (value !== undefined) {
        return value;
      }
    }
    return undefined;
  };
  return {
    host: get('host', 'server'),
    port: Number(get('port') ?? 5432),
    database: get('database', 'initial catalog'),
    user: get('username', 'user id', 'userid', 'user'),
    password: get('password', 'pwd'),
  };
}

function saveEntry(values, item) {
  const separator = item.indexOf('=');
  if (separator < 0) {
    return;
  }
  const key = item.slice(0, separator).trim().toLowerCase();
  const value = item.slice(separator + 1).trim();
  values.set(key, value);
}

function databaseName(connectionString) {
  if (!connectionString) {
    return null;
  }
  return parsePostgresConnection(connectionString).database ?? null;
}

export function createApiChildEnvironment(source) {
  const apiTestConnection = source.TASK_MANAGER_TEST_CONNECTION;
  const e2eConnection = source.ConnectionStrings__TaskManager;
  const apiTestConfig = apiTestConnection ? parsePostgresConnection(apiTestConnection) : null;
  const e2eConfig = e2eConnection ? parsePostgresConnection(e2eConnection) : null;
  const apiTestHostIsLocal = apiTestConfig &&
    ['127.0.0.1', 'localhost', '::1'].includes(apiTestConfig.host?.toLowerCase() ?? '');
  const e2eHostIsLocal = e2eConfig &&
    ['127.0.0.1', 'localhost', '::1'].includes(e2eConfig.host?.toLowerCase() ?? '');
  if (databaseName(apiTestConnection) !== 'task_manager_test' ||
      databaseName(e2eConnection) !== 'task_manager_e2e' ||
      apiTestConnection === e2eConnection ||
      !apiTestHostIsLocal ||
      !e2eHostIsLocal) {
    throw new Error('E2E requires separate task_manager_test and task_manager_e2e connections.');
  }

  const environment = { ...source };
  delete environment.TASK_MANAGER_TEST_CONNECTION;
  const certificateDirectory = source.TASK_MANAGER_CERT_DIR ?? resolve(process.cwd(), '.certs');
  environment.ASPNETCORE_ENVIRONMENT = 'E2E';
  environment.ASPNETCORE_URLS = e2eServerTopology.api.url;
  environment.ASPNETCORE_Kestrel__Certificates__Default__Path =
    resolve(certificateDirectory, 'localhost.pfx');
  environment.ASPNETCORE_Kestrel__Certificates__Default__Password = '';
  environment.ConnectionStrings__TaskManager = e2eConnection;
  environment.NODE_EXTRA_CA_CERTS = resolve(certificateDirectory, 'root-ca.crt');
  environment.TASK_MANAGER_API_TARGET = e2eServerTopology.api.url;
  environment.TASK_MANAGER_CERT_DIR = certificateDirectory;
  environment.TASK_MANAGER_EXPECTED_DATABASE = 'task_manager_e2e';
  environment.TaskManager__AllowedOrigins__0 = e2eServerTopology.frontend.origin;
  environment.VITE_USE_HTTPS = 'true';
  return environment;
}

export async function ensurePortsAvailable(ports, isListening = isPortListening) {
  for (const port of ports) {
    if (await isListening(port)) {
      throw new Error('A dedicated E2E port is already in use; no browser test was started.');
    }
  }
}

function isPortListening(port) {
  return new Promise(resolveResult => {
    const server = createServer();
    server.once('error', () => resolveResult(true));
    server.listen(port, '127.0.0.1', () => {
      server.close(() => resolveResult(false));
    });
  });
}
