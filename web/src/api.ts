export type WorkspaceCreateRequest = {
  timeZoneId: string;
  deviceCredential: string;
  recoverySecret: string;
  idempotencyKey: string;
};

export type WorkspaceCreated = {
  workspaceId: string;
  timeZoneId: string;
  defaultListName: string;
};

export type WorkspaceInfo = {
  workspaceId: string;
  timeZoneId: string;
};

export type TaskDraft = {
  title: string;
  description: string;
  dueDate: string;
  priority: '' | 'Low' | 'Medium' | 'High';
};

export type Task = {
  id: string;
  title: string;
  description: string | null;
  dueDate: string | null;
  priority: 'Low' | 'Medium' | 'High' | null;
  listName: string;
  createdAtUtc: string;
};

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

const csrfCookieName = '__Host-csrf';
let rememberedCsrfToken: string | undefined;

export async function getSupportedTimeZones(): Promise<string[]> {
  return requestJson<string[]>('/api/time-zones');
}

export async function getTasks(): Promise<Task[]> {
  return requestJson<Task[]>('/api/tasks');
}

export async function getWorkspace(): Promise<WorkspaceInfo> {
  return requestJson<WorkspaceInfo>('/api/workspace');
}

export async function createWorkspace(request: WorkspaceCreateRequest): Promise<WorkspaceCreated> {
  const token = await ensureCsrfToken();
  return requestJson<WorkspaceCreated>('/api/workspaces', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': token,
    },
    body: JSON.stringify(request),
  });
}

export async function createTask(draft: TaskDraft, idempotencyKey: string): Promise<Task> {
  const token = await ensureCsrfToken();
  return requestJson<Task>('/api/tasks', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': token,
      'Idempotency-Key': idempotencyKey,
    },
    body: JSON.stringify({
      title: draft.title,
      description: draft.description === '' ? null : draft.description,
      dueDate: draft.dueDate === '' ? null : draft.dueDate,
      priority: draft.priority === '' ? null : draft.priority,
    }),
  });
}

async function ensureCsrfToken(): Promise<string> {
  const cookieToken = readCookie(csrfCookieName);
  if (rememberedCsrfToken && cookieToken === rememberedCsrfToken) {
    return rememberedCsrfToken;
  }

  rememberedCsrfToken = undefined;
  let response: Response;
  try {
    response = await fetch('/api/security/csrf', {
      credentials: 'same-origin',
      cache: 'no-store',
    });
  } catch {
    throw new ApiError('Could not prepare a secure request. Your changes are still here; try again.', 0);
  }

  if (!response.ok) {
    throw new ApiError('Could not prepare a secure request. Your changes are still here; try again.', response.status);
  }

  const result = await response.json() as { token?: string };
  const freshCookieToken = readCookie(csrfCookieName);
  if (!result.token || freshCookieToken !== result.token) {
    throw new ApiError('Could not verify the security token. Your changes are still here; try again.', 0);
  }

  rememberedCsrfToken = result.token;
  return result.token;
}

async function requestJson<T>(path: string, init: RequestInit = {}): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, {
      ...init,
      credentials: 'same-origin',
      cache: 'no-store',
      headers: {
        Accept: 'application/json',
        ...init.headers,
      },
    });
  } catch {
    throw new ApiError('The service could not be reached. Your changes are still here; try again.', 0);
  }

  if (!response.ok) {
    let payload: { title?: string; errors?: Record<string, string[]> } = {};
    try {
      payload = await response.json() as typeof payload;
    } catch {
      payload = {};
    }

    const message = response.status === 401
      ? 'This browser does not have access to a task space. Create one explicitly or use another device with access.'
      : payload.title ?? 'The request could not be completed. Your changes are still here; try again.';
    throw new ApiError(message, response.status, payload.errors ?? {});
  }

  return response.json() as Promise<T>;
}

function readCookie(name: string): string | undefined {
  const prefix = `${name}=`;
  const entry = document.cookie.split(';').map((cookie) => cookie.trim()).find((cookie) => cookie.startsWith(prefix));
  return entry?.slice(prefix.length);
}
