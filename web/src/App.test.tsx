import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, type Task, type WorkspaceCreateRequest } from './api';
import { App } from './App';

vi.mock('./api', () => ({
  ApiError: class ApiError extends Error {
    constructor(
      message: string,
      readonly status: number,
      readonly fieldErrors: Record<string, string[]> = {},
    ) {
      super(message);
    }
  },
  createTask: vi.fn(),
  createWorkspace: vi.fn(),
  getSupportedTimeZones: vi.fn(),
  getTasks: vi.fn(),
  getWorkspace: vi.fn(),
}));

import * as api from './api';

const taskResult: Task = {
  id: 'task-1',
  title: 'Submitted draft',
  description: null,
  dueDate: null,
  priority: null,
  listName: 'Tasks',
  createdAtUtc: '2026-11-13T00:00:00Z',
};

describe('first-use and task workflow', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    localStorage.clear();
    vi.mocked(api.getTasks)
      .mockRejectedValueOnce(new ApiError('Unauthorized', 401))
      .mockResolvedValue([]);
    vi.mocked(api.getWorkspace).mockResolvedValue({
      workspaceId: 'workspace-1',
      timeZoneId: 'Etc/UTC',
    });
    vi.mocked(api.getSupportedTimeZones).mockResolvedValue(['Etc/UTC', 'Europe/London']);
    vi.mocked(api.createWorkspace).mockResolvedValue({
      workspaceId: 'workspace-1',
      timeZoneId: 'Europe/London',
      defaultListName: 'Tasks',
    });
  });

  it('TimeZoneLoadFailureCanBeRetried', async () => {
    vi.mocked(api.getSupportedTimeZones)
      .mockRejectedValueOnce(new Error('unavailable'))
      .mockResolvedValueOnce(['Etc/UTC', 'Europe/London']);
    const user = userEvent.setup();
    render(<App />);

    await screen.findByRole('alert', { name: 'Time zone loading error' });
    const retry = screen.getByRole('button', { name: 'Retry time zone loading' });
    await user.click(retry);

    const zoneSelect = await screen.findByRole('combobox', { name: 'Workspace time zone' });
    await screen.findByRole('option', { name: 'Europe/London' });
    await user.selectOptions(zoneSelect, 'Europe/London');
    await user.click(screen.getByRole('button', { name: 'Create my private task space' }));

    expect(await screen.findByText('Save this recovery secret separately.')).toBeInTheDocument();
    expect(api.createWorkspace).toHaveBeenCalledWith(expect.objectContaining<Partial<WorkspaceCreateRequest>>({
      timeZoneId: 'Europe/London',
    }));
    expect(api.getSupportedTimeZones).toHaveBeenCalledTimes(2);
  });

  it('PendingSaveCannotEraseNewerDraft', async () => {
    let resolveSave: ((task: Task) => void) | undefined;
    let resolveRefresh: ((tasks: Task[]) => void) | undefined;
    vi.mocked(api.getTasks).mockReset()
      .mockRejectedValueOnce(new ApiError('Unauthorized', 401))
      .mockImplementationOnce(() => new Promise((resolve) => {
        resolveRefresh = resolve;
      }));
    vi.mocked(api.createTask).mockImplementation(() => new Promise((resolve) => {
      resolveSave = resolve;
    }));
    const user = userEvent.setup();
    render(<App />);

    await screen.findByRole('option', { name: 'Etc/UTC' });
    await user.selectOptions(screen.getByRole('combobox', { name: 'Workspace time zone' }), 'Etc/UTC');
    await user.click(screen.getByRole('button', { name: 'Create my private task space' }));
    await screen.findByRole('button', { name: "I've saved the recovery secret" });
    await user.click(screen.getByRole('button', { name: "I've saved the recovery secret" }));

    const title = await screen.findByRole('textbox', { name: 'Task title' });
    await user.type(title, 'Submitted draft');
    const save = screen.getByRole('button', { name: 'Save task' });
    await user.click(save);

    expect(title).toBeDisabled();
    fireEvent.change(title, { target: { value: 'Newer draft' } });
    expect(title).toHaveValue('Submitted draft');

    resolveSave?.(taskResult);
    expect(await screen.findByRole('status')).toHaveTextContent('Saved');
    await waitFor(() => expect(title).toHaveValue(''));
    expect(screen.getByText('Submitted draft')).toBeInTheDocument();
    resolveRefresh?.([]);
    await waitFor(() => expect(screen.getByText('Submitted draft')).toBeInTheDocument());
  });
});
