import { beforeEach, describe, expect, it, vi } from 'vitest';

const testDraft = {
  title: 'A task title',
  description: '',
  dueDate: '',
  priority: '' as const,
};

describe('shared API mutation boundary', () => {
  let cookieValue = '';

  beforeEach(() => {
    vi.resetModules();
    vi.unstubAllGlobals();
    cookieValue = '';
    Object.defineProperty(document, 'cookie', {
      configurable: true,
      get: () => cookieValue,
      set: (value: string) => {
        cookieValue = value.split(';', 1)[0] ?? '';
      },
    });
  });

  it('ReturningDeviceAcquiresMissingCsrfBeforeMutation', async () => {
    const calls: Array<{ url: string; init?: RequestInit }> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      calls.push({ url, init });
      if (url === '/api/security/csrf') {
        document.cookie = '__Host-csrf=fresh-token; Path=/; Secure';
        return new Response(JSON.stringify({ token: 'fresh-token' }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      return new Response(JSON.stringify({
        id: 'task-1',
        title: testDraft.title,
        description: null,
        dueDate: null,
        priority: null,
        listName: 'Tasks',
        createdAtUtc: '2026-11-13T00:00:00Z',
      }), {
        status: 201,
        headers: { 'Content-Type': 'application/json' },
      });
    }));

    const api = await import('./api');
    await api.createTask(testDraft, 'stable-operation-key');

    expect(calls.map((call) => call.url)).toEqual(['/api/security/csrf', '/api/tasks']);
    expect(calls[0].init?.method).toBeUndefined();
    expect(new Headers(calls[1].init?.headers).get('Content-Type')).toBe('application/json');
    expect(new Headers(calls[1].init?.headers).get('X-CSRF-Token')).toBe('fresh-token');
    expect(new Headers(calls[1].init?.headers).get('Idempotency-Key')).toBe('stable-operation-key');
    expect(calls[1].init?.credentials).toBe('same-origin');
  });

  it('preserves a failed mutation when CSRF acquisition fails and sends no write', async () => {
    const fetchMock = vi.fn(async () => new Response(null, { status: 503 }));
    vi.stubGlobal('fetch', fetchMock);
    const api = await import('./api');

    await expect(api.createTask(testDraft, 'stable-operation-key')).rejects.toMatchObject({ status: 503 });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
