export type Priority = 'Low' | 'Medium' | 'High'

export interface Workspace {
  workspaceId: string
  timeZoneId: string
  defaultListName: string
}

export interface Task {
  id: string
  title: string
  description: string | null
  dueDate: string | null
  priority: Priority | null
  listName: string
}

export interface CreateWorkspaceInput {
  timeZoneId: string
  deviceCredential: string
  recoverySecret: string
  idempotencyKey: string
}

export interface CreateTaskInput {
  title: string
  description: string | null
  dueDate: string | null
  priority: Priority | null
  idempotencyKey: string
}

interface ProblemResponse {
  title?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly validationErrors: Record<string, string[]>

  constructor(
    status: number,
    message: string,
    validationErrors: Record<string, string[]> = {},
  ) {
    super(message)
    this.status = status
    this.validationErrors = validationErrors
    this.name = 'ApiError'
  }
}

export async function getWorkspace(): Promise<Workspace> {
  return getJson<Workspace>('/api/workspaces/current')
}

export async function getTimeZones(): Promise<string[]> {
  return getJson<string[]>('/api/time-zones')
}

export async function getTasks(): Promise<Task[]> {
  return getJson<Task[]>('/api/tasks')
}

export async function getCsrfToken(): Promise<string> {
  const response = await fetch('/api/security/csrf', { credentials: 'same-origin', cache: 'no-store' })
  const payload = await readResponse<{ token: string }>(response)
  return payload.token
}

export async function createWorkspace(input: CreateWorkspaceInput): Promise<Workspace> {
  return postJson<Workspace>('/api/workspaces', {
    body: {
      timeZoneId: input.timeZoneId,
      deviceCredential: input.deviceCredential,
      recoverySecret: input.recoverySecret,
    },
    idempotencyKey: input.idempotencyKey,
  })
}

export async function createTask(input: CreateTaskInput): Promise<Task> {
  return postJson<Task>('/api/tasks', {
    body: {
      title: input.title,
      description: input.description,
      dueDate: input.dueDate,
      priority: input.priority,
    },
    idempotencyKey: input.idempotencyKey,
  })
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path, { credentials: 'same-origin', cache: 'no-store' })
  return readResponse<T>(response)
}

async function postJson<T>(
  path: string,
  request: { body: unknown; idempotencyKey: string },
): Promise<T> {
  const csrfToken = readCsrfCookie()
  const response = await fetch(path, {
    method: 'POST',
    credentials: 'same-origin',
    cache: 'no-store',
    headers: {
      'Content-Type': 'application/json',
      'Idempotency-Key': request.idempotencyKey,
      'X-CSRF-Token': csrfToken,
    },
    body: JSON.stringify(request.body),
  })
  return readResponse<T>(response)
}

async function readResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let problem: ProblemResponse | null = null
    try {
      problem = await response.json() as ProblemResponse
    } catch {
      problem = null
    }

    throw new ApiError(
      response.status,
      problem?.title ?? 'The request could not be completed. Try again.',
      problem?.errors ?? {},
    )
  }

  try {
    return await response.json() as T
  } catch {
    throw new ApiError(response.status, 'The service returned an invalid response. Try again.')
  }
}

function readCsrfCookie(): string {
  const cookie = document.cookie
    .split('; ')
    .find(value => value.startsWith('__Host-csrf='))
  if (!cookie) {
    throw new ApiError(403, 'The request could not be verified. Reload the page and try again.')
  }

  return cookie.slice('__Host-csrf='.length)
}
