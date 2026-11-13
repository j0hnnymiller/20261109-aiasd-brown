import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, createTask } from './api';

const taskPayload = {
  title: 'Save after refresh',
  description: '',
  dueDate: null,
  priority: null,
} as const;

describe('shared mutation API client', () => {
  const clearCookies = () => {
    for (const cookie of document.cookie.split(';')) {
      const name = cookie.split('=', 1)[0]?.trim();
      if (name) {
        document.cookie = `${name}=; Max-Age=0; Path=/; Secure`;
      }
    }
  };

  beforeEach(() => {
    clearCookies();
    document.cookie = '__Host-device=device-cookie-sentinel; Secure; Path=/';
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    clearCookies();
  });

  it('ReturningDeviceAcquiresMissingCsrfBeforeMutation', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockImplementation(async input => {
      if (input === '/api/csrf') {
        document.cookie = '__Host-csrf=csrf-cookie-sentinel; Secure; Path=/';
        return new Response(JSON.stringify({ token: 'csrf-cookie-sentinel' }), { status: 200 });
      }

      return new Response(JSON.stringify({
        id: 'task-1',
        title: taskPayload.title,
        description: '',
        dueDate: null,
        priority: null,
        createdAt: '2026-11-13T12:00:00Z',
      }), { status: 201 });
    });

    await createTask(taskPayload, 'operation-key-sentinel');
    const calls = fetchMock.mock.calls;
    const acquisitionFirst = calls.length === 2 &&
      calls[0][0] === '/api/csrf' &&
      calls[0][1]?.method === 'GET';
    const mutationAfter = calls.length === 2 &&
      calls[1][0] === '/api/tasks' &&
      calls[1][1]?.method === 'POST' &&
      new Headers(calls[1][1]?.headers).get('X-CSRF-Token') === 'csrf-cookie-sentinel' &&
      new Headers(calls[1][1]?.headers).get('Idempotency-Key') === 'operation-key-sentinel';
    expect(acquisitionFirst && mutationAfter).toBe(true);
  });

  it('does not send a mutation when CSRF acquisition fails', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockRejectedValueOnce(new TypeError('Temporary security endpoint failure.'));

    let failureCode = '';
    try {
      await createTask(taskPayload, 'operation-key-sentinel');
    } catch (error) {
      failureCode = error instanceof ApiError ? error.code : 'unexpected_error';
    }

    const noMutationWasSent = fetchMock.mock.calls.length === 1 &&
      fetchMock.mock.calls[0][0] === '/api/csrf';
    expect(failureCode).toBe('csrf_unavailable');
    expect(noMutationWasSent).toBe(true);
  });
});
