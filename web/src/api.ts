export interface Workspace {
  id: string;
  timeZone: string;
}

export interface Task {
  id: string;
  title: string;
  description: string;
  dueDate: string | null;
  priority: 'Low' | 'Medium' | 'High' | null;
  createdAt: string;
}

export interface CreateWorkspaceOperation {
  idempotencyKey: string;
  timeZone: string;
  deviceToken: string;
  recoverySecret: string;
}

export interface CreateTaskInput {
  title: string;
  description: string;
  dueDate: string | null;
  priority: 'Low' | 'Medium' | 'High' | null;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message = 'The request could not be completed.',
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function isProblem(value: unknown): value is { code?: string; errors?: Record<string, string[]> } {
  if (!isRecord(value)) {
    return false;
  }

  const validCode = !('code' in value) || typeof value.code === 'string';
  const validErrors = !('errors' in value) ||
    (isRecord(value.errors) && Object.values(value.errors).every(
      messages => Array.isArray(messages) && messages.every(message => typeof message === 'string'),
    ));
  return validCode && validErrors;
}

function isWorkspace(value: unknown): value is Workspace {
  return isRecord(value) && typeof value.id === 'string' && typeof value.timeZone === 'string';
}

function isTask(value: unknown): value is Task {
  return isRecord(value) &&
    typeof value.id === 'string' &&
    typeof value.title === 'string' &&
    typeof value.description === 'string' &&
    (value.dueDate === null || typeof value.dueDate === 'string') &&
    (value.priority === null || value.priority === 'Low' || value.priority === 'Medium' || value.priority === 'High') &&
    typeof value.createdAt === 'string';
}

function readCookie(name: string): string | null {
  const prefix = `${name}=`;
  const pair = document.cookie.split(';').map(value => value.trim()).find(value => value.startsWith(prefix));
  return pair ? pair.slice(prefix.length) : null;
}

function tokenFromBytes(bytes: Uint8Array): string {
  let binary = '';
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }

  return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/, '');
}

export function createOperationKey(): string {
  return tokenFromBytes(crypto.getRandomValues(new Uint8Array(32)));
}

function errorFromResponse(status: number, value: unknown): ApiError {
  const problem = isProblem(value) ? value : null;
  if (status >= 500) {
    return new ApiError(status, 'service_failure', 'The service is temporarily unavailable. Your input is preserved.');
  }

  if (status === 401) {
    return new ApiError(status, problem?.code ?? 'unauthorized', 'Workspace access could not be confirmed.');
  }

  const fieldMessage = problem?.errors
    ? Object.values(problem.errors).flat()[0]
    : undefined;
  if (fieldMessage) {
    return new ApiError(status, 'validation', fieldMessage);
  }

  return new ApiError(status, problem?.code ?? 'request_failed', 'The request could not be completed.');
}

async function getProblem(response: Response): Promise<unknown> {
  try {
    return await response.json();
  } catch {
    return null;
  }
}

async function acquireCsrfToken(): Promise<string> {
  const current = readCookie('__Host-csrf');
  if (current) {
    return current;
  }

  let response: Response;
  try {
    response = await fetch('/api/csrf', {
      method: 'GET',
      credentials: 'include',
      headers: { Accept: 'application/json' },
    });
  } catch {
    throw new ApiError(0, 'csrf_unavailable', 'Security verification is unavailable. Your input is preserved.');
  }

  if (!response.ok) {
    throw new ApiError(
      response.status,
      'csrf_unavailable',
      'Security verification is unavailable. Your input is preserved.',
    );
  }

  const body: unknown = await response.json().catch(() => null);
  const token = typeof body === 'object' && body !== null && 'token' in body &&
      typeof body.token === 'string'
    ? body.token
    : null;
  const cookieToken = readCookie('__Host-csrf');
  if (!token || !cookieToken || token !== cookieToken) {
    throw new ApiError(0, 'csrf_unavailable', 'Security verification is unavailable. Your input is preserved.');
  }

  return cookieToken;
}

async function request(
  path: string,
  options: {
    method?: 'GET' | 'POST';
    body?: unknown;
    idempotencyKey?: string;
  } = {},
): Promise<unknown> {
  const method = options.method ?? 'GET';
  const headers = new Headers({ Accept: 'application/json' });
  if (method === 'POST') {
    if (window.location.protocol !== 'https:') {
      throw new ApiError(0, 'https_required', 'Secure HTTPS is required before sending this request.');
    }

    headers.set('Content-Type', 'application/json');
    headers.set('X-CSRF-Token', await acquireCsrfToken());
    if (options.idempotencyKey) {
      headers.set('Idempotency-Key', options.idempotencyKey);
    }
  }

  let response: Response;
  try {
    response = await fetch(path, {
      method,
      credentials: 'include',
      headers,
      body: method === 'POST' ? JSON.stringify(options.body) : undefined,
    });
  } catch {
    throw new ApiError(0, 'network_failure', 'The service could not be reached. Your input is preserved.');
  }

  if (!response.ok) {
    throw errorFromResponse(response.status, await getProblem(response));
  }

  return response.json();
}

export async function getTimeZones(): Promise<string[]> {
  const result = await request('/api/time-zones');
  if (!Array.isArray(result) || !result.every(zone => typeof zone === 'string')) {
    throw new ApiError(502, 'invalid_response', 'Supported time zones could not be loaded.');
  }

  return result;
}

export async function getWorkspace(): Promise<Workspace> {
  const result = await request('/api/workspace');
  if (!isWorkspace(result)) {
    throw new ApiError(502, 'invalid_response', 'Workspace access could not be confirmed.');
  }

  return result;
}

export async function getTasks(): Promise<Task[]> {
  const result = await request('/api/tasks');
  if (!Array.isArray(result) || !result.every(isTask)) {
    throw new ApiError(502, 'invalid_response', 'Tasks could not be refreshed.');
  }

  return result;
}

export async function createWorkspace(operation: CreateWorkspaceOperation): Promise<Workspace> {
  const result = await request('/api/workspaces', {
    method: 'POST',
    idempotencyKey: operation.idempotencyKey,
    body: {
      timeZone: operation.timeZone,
      deviceToken: operation.deviceToken,
      recoverySecret: operation.recoverySecret,
    },
  });

  if (!isRecord(result) || typeof result.workspaceId !== 'string' || typeof result.timeZone !== 'string') {
    throw new ApiError(502, 'invalid_response', 'Workspace creation could not be confirmed.');
  }

  return { id: result.workspaceId, timeZone: result.timeZone };
}

export async function createTask(task: CreateTaskInput, idempotencyKey: string): Promise<Task> {
  const result = await request('/api/tasks', {
    method: 'POST',
    idempotencyKey,
    body: task,
  });
  if (!isTask(result)) {
    throw new ApiError(502, 'invalid_response', 'Task creation could not be confirmed.');
  }

  return result;
}

export function createSecret(): string {
  return tokenFromBytes(crypto.getRandomValues(new Uint8Array(32)));
}
