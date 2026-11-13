import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App';
import { ApiError, createTask, createWorkspace, getTasks, getTimeZones, getWorkspace } from './api';
import type { Task, Workspace } from './api';

vi.mock('./api', () => ({
  ApiError: class ApiError extends Error {
    constructor(
      public readonly status: number,
      public readonly code: string,
    ) {
      super('Request failed.');
    }
  },
  createOperationKey: vi.fn(() => `operation-key-sentinel-${crypto.randomUUID()}`),
  createSecret: vi.fn(() => 'credential-sentinel-'.repeat(3)),
  createTask: vi.fn(),
  createWorkspace: vi.fn(),
  getTasks: vi.fn(),
  getTimeZones: vi.fn(),
  getWorkspace: vi.fn(),
}));

const workspace: Workspace = {
  id: 'workspace-1',
  timeZone: 'America/Los_Angeles',
};

const task: Task = {
  id: 'task-1',
  title: 'Submitted task',
  description: '',
  dueDate: null,
  priority: null,
  createdAt: '2026-11-13T12:00:00Z',
};

function useFirstTimeWorkspace() {
  vi.mocked(getWorkspace).mockRejectedValue(new ApiError(401, 'missing_device'));
  vi.mocked(getTimeZones).mockResolvedValue(['America/Los_Angeles', 'UTC']);
  vi.mocked(createWorkspace).mockResolvedValue(workspace);
  vi.mocked(getTasks).mockResolvedValue([]);
}

function useAuthorizedWorkspace() {
  vi.mocked(getWorkspace).mockResolvedValue(workspace);
  vi.mocked(getTimeZones).mockResolvedValue(['America/Los_Angeles', 'UTC']);
  vi.mocked(getTasks).mockResolvedValue([]);
}

describe('first-use and task workflow', () => {
  beforeEach(() => {
    useFirstTimeWorkspace();
    vi.mocked(createTask).mockResolvedValue(task);
  });

  it('creates a workspace only after explicit setup and shows the one-time recovery secret', async () => {
    render(<App />);

    await screen.findByRole('button', { name: 'Create task space' });
    expect(createWorkspace).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Create task space' }));

    await waitFor(() => {
      expect(screen.queryByRole('heading', { name: 'Save your recovery secret' }) !== null).toBe(true);
    });
    const calls = vi.mocked(createWorkspace).mock.calls;
    const generatedCredentialLengths = calls.length === 1 &&
      calls[0][0].deviceToken.length >= 40 &&
      calls[0][0].recoverySecret.length >= 40 &&
      calls[0][0].idempotencyKey.length >= 40;
    expect(generatedCredentialLengths).toBe(true);
    expect(Object.keys(localStorage)).toHaveLength(0);

    const savedButton = screen.queryByRole('button', { name: 'I saved this secret' });
    expect(savedButton !== null).toBe(true);
    if (savedButton) {
      fireEvent.click(savedButton);
    }
    await screen.findByRole('heading', { name: 'Your tasks' });
  });

  it('TimeZoneLoadFailureCanBeRetried without losing the detected zone', async () => {
    vi.spyOn(Intl.DateTimeFormat.prototype, 'resolvedOptions').mockReturnValue({
      timeZone: 'America/Los_Angeles',
    } as Intl.ResolvedDateTimeFormatOptions);
    vi.mocked(getTimeZones)
      .mockRejectedValueOnce(new Error('Temporary service failure.'))
      .mockResolvedValue(['America/Los_Angeles', 'UTC']);
    render(<App />);

    const retry = await screen.findByRole('button', { name: 'Retry time zones' });
    fireEvent.click(retry);

    await waitFor(() => {
      expect(screen.getByLabelText('Workspace time zone')).toHaveValue('America/Los_Angeles');
    });
  });

  it('TransientAccessCheckFailureCanBeRetried without creating a workspace', async () => {
    vi.mocked(getWorkspace)
      .mockRejectedValueOnce(new Error('Temporary service failure.'))
      .mockResolvedValue(workspace);
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: 'Retry workspace access' }));
    await screen.findByRole('heading', { name: 'Your tasks' });

    expect(getWorkspace).toHaveBeenCalledTimes(2);
    expect(createWorkspace).not.toHaveBeenCalled();
  });

  it('retries a failed task read without replacing the authorized workspace', async () => {
    useAuthorizedWorkspace();
    vi.mocked(getTasks)
      .mockRejectedValueOnce(new Error('Temporary service failure.'))
      .mockResolvedValue([task]);
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: 'Retry task loading' }));
    await screen.findByText('Submitted task');

    expect(getWorkspace).toHaveBeenCalledTimes(1);
    expect(getTasks).toHaveBeenCalledTimes(2);
    expect(createWorkspace).not.toHaveBeenCalled();
  });

  it('keeps confirmed 401 separate from transient access errors', async () => {
    vi.mocked(getWorkspace).mockRejectedValue(new ApiError(401, 'invalid_device'));
    render(<App />);

    await screen.findByRole('alert');
    expect(screen.getByText(/existing workspace access could not be confirmed/i)).toBeInTheDocument();
    expect(createWorkspace).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: 'Retry workspace access' })).not.toBeInTheDocument();
  });

  it('PendingSaveCannotEraseNewerDraft after a deferred response', async () => {
    useAuthorizedWorkspace();
    let resolveSave: ((saved: Task) => void) | undefined;
    vi.mocked(createTask).mockImplementation(() => new Promise(resolve => {
      resolveSave = resolve;
    }));
    render(<App />);
    const title = await screen.findByLabelText('Task title');

    fireEvent.change(title, { target: { value: 'Submitted task' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save task' }));
    fireEvent.change(title, { target: { value: 'Newer draft' } });
    resolveSave?.(task);

    await screen.findByText('Submitted task');
    expect(title).toHaveValue('Newer draft');
  });

  it('LostCreateResponseRetriesImmutableOperation before saving an edited draft', async () => {
    useAuthorizedWorkspace();
    vi.mocked(createTask)
      .mockRejectedValueOnce(new TypeError('Network response was lost.'))
      .mockResolvedValueOnce(task)
      .mockResolvedValueOnce({ ...task, id: 'task-2', title: 'Edited draft' });
    render(<App />);
    const title = await screen.findByLabelText('Task title');

    fireEvent.change(title, { target: { value: 'Submitted task' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save task' }));
    fireEvent.change(title, { target: { value: 'Edited draft' } });
    fireEvent.click(await screen.findByRole('button', { name: 'Retry original task' }));

    await screen.findByText('Submitted task');
    expect(title).toHaveValue('Edited draft');
    const calls = vi.mocked(createTask).mock.calls;
    const sameOriginalOperation = calls.length === 2 &&
      calls[0][1] === calls[1][1] &&
      calls[0][0].title === 'Submitted task' &&
      calls[1][0].title === 'Submitted task';
    expect(sameOriginalOperation).toBe(true);

    fireEvent.click(screen.getByRole('button', { name: 'Save task' }));
    await screen.findByText('Edited draft');
    const updatedCalls = vi.mocked(createTask).mock.calls;
    expect(updatedCalls.length === 3 && updatedCalls[2][1] !== updatedCalls[1][1]).toBe(true);
  });
});
