import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import {
  ApiError,
  createTask,
  createWorkspace,
  getSupportedTimeZones,
  getTasks,
  getWorkspace,
  type Task,
  type TaskDraft,
  type WorkspaceCreateRequest,
} from './api';

type View = 'checking' | 'setup' | 'lost' | 'recovery' | 'tasks';
type WorkspaceAttempt = WorkspaceCreateRequest;

const pendingWorkspaceKey = 'task-manager-workspace-creation-pending';
const emptyDraft: TaskDraft = {
  title: '',
  description: '',
  dueDate: '',
  priority: '',
};

function detectedTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone ?? '';
  } catch {
    return '';
  }
}

function randomToken(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(32));
  let binary = '';
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }
  return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/u, '');
}

function hasPendingWorkspaceAttempt(): boolean {
  try {
    return localStorage.getItem(pendingWorkspaceKey) === 'true';
  } catch {
    return false;
  }
}

function setPendingWorkspaceAttempt(pending: boolean): void {
  try {
    if (pending) {
      localStorage.setItem(pendingWorkspaceKey, 'true');
    } else {
      localStorage.removeItem(pendingWorkspaceKey);
    }
  } catch {
    if (pending) {
      throw new Error('This browser cannot remember that an unconfirmed setup needs an explicit restart.');
    }
  }
}

function apiErrorStatus(error: unknown): number | undefined {
  return error instanceof ApiError ? error.status : undefined;
}

export function App() {
  const [view, setView] = useState<View>('checking');
  const [tasks, setTasks] = useState<Task[]>([]);
  const [timeZones, setTimeZones] = useState<string[] | null>(null);
  const [timeZone, setTimeZone] = useState(detectedTimeZone);
  const [workspaceTimeZone, setWorkspaceTimeZone] = useState('');
  const [timeZoneError, setTimeZoneError] = useState('');
  const [timeZoneRetryCount, setTimeZoneRetryCount] = useState(0);
  const [workspaceError, setWorkspaceError] = useState('');
  const [creatingWorkspace, setCreatingWorkspace] = useState(false);
  const [recoverySecret, setRecoverySecret] = useState('');
  const [lostRecoveryNotice, setLostRecoveryNotice] = useState(false);
  const [taskDraft, setTaskDraft] = useState<TaskDraft>(emptyDraft);
  const [taskError, setTaskError] = useState('');
  const [taskStatus, setTaskStatus] = useState('');
  const [savingTask, setSavingTask] = useState(false);
  const [refreshError, setRefreshError] = useState('');
  const [refreshing, setRefreshing] = useState(false);
  const workspaceAttempt = useRef<WorkspaceAttempt | null>(null);
  const taskOperationKey = useRef<string | null>(null);
  const refreshSequence = useRef(0);
  const refreshInFlight = useRef<Promise<void> | null>(null);

  useEffect(() => {
    let active = true;
    const pendingAttempt = hasPendingWorkspaceAttempt();
    Promise.all([getTasks(), getWorkspace()]).then(([serverTasks, workspace]) => {
      if (!active) {
        return;
      }

      setTasks(serverTasks);
      setWorkspaceTimeZone(workspace.timeZoneId);
      setView('tasks');
      if (pendingAttempt) {
        setPendingWorkspaceAttempt(false);
        setLostRecoveryNotice(true);
      }
    }).catch((error: unknown) => {
      if (!active) {
        return;
      }

      if (apiErrorStatus(error) === 401 && pendingAttempt) {
        setView('lost');
      } else {
        setView('setup');
        if (apiErrorStatus(error) !== 401) {
          setWorkspaceError('Could not check this browser for an existing task space. You can retry setup or create a space explicitly.');
        }
      }
    });

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (view !== 'setup') {
      return;
    }

    let active = true;
    setTimeZoneError('');
    getSupportedTimeZones().then((zones) => {
      if (!active) {
        return;
      }

      setTimeZones(zones);
      setTimeZone((current) => zones.includes(current) ? current : '');
    }).catch(() => {
      if (active) {
        setTimeZoneError('Could not load supported time zones. Your selection is safe; retry to continue setup.');
      }
    });

    return () => {
      active = false;
    };
  }, [timeZoneRetryCount, view]);

  const refreshTasks = useCallback((): Promise<void> => {
    if (refreshInFlight.current) {
      return refreshInFlight.current;
    }

    const requestId = ++refreshSequence.current;
    setRefreshing(true);
    setRefreshError('');
    const operation = Promise.all([getTasks(), getWorkspace()]).then(([serverTasks, workspace]) => {
      if (requestId === refreshSequence.current) {
        setTasks(serverTasks);
        setWorkspaceTimeZone(workspace.timeZoneId);
        setRefreshError('');
      }
    }).catch(() => {
      if (requestId === refreshSequence.current) {
        setRefreshError('Refresh failed. Showing the last confirmed task list.');
      }
    }).finally(() => {
      if (refreshInFlight.current === operation) {
        refreshInFlight.current = null;
      }
      if (requestId === refreshSequence.current) {
        setRefreshing(false);
      }
    });

    refreshInFlight.current = operation;
    return operation;
  }, []);

  useEffect(() => {
    if (view !== 'tasks') {
      return;
    }

    const refreshWhenActive = () => {
      if (document.visibilityState === 'visible') {
        void refreshTasks();
      }
    };
    window.addEventListener('focus', refreshWhenActive);
    document.addEventListener('visibilitychange', refreshWhenActive);
    return () => {
      window.removeEventListener('focus', refreshWhenActive);
      document.removeEventListener('visibilitychange', refreshWhenActive);
    };
  }, [refreshTasks, view]);

  async function submitWorkspace(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!timeZones?.includes(timeZone)) {
      setWorkspaceError('Choose a supported time zone before creating a task space.');
      return;
    }

    let attempt = workspaceAttempt.current;
    if (!attempt) {
      try {
        attempt = {
          timeZoneId: timeZone,
          deviceCredential: randomToken(),
          recoverySecret: randomToken(),
          idempotencyKey: randomToken(),
        };
        setPendingWorkspaceAttempt(true);
        workspaceAttempt.current = attempt;
      } catch {
        setWorkspaceError('Secure browser storage is unavailable. No task space was created.');
        return;
      }
    }

    setCreatingWorkspace(true);
    setWorkspaceError('');
    try {
      const created = await createWorkspace(attempt);
      setWorkspaceTimeZone(created.timeZoneId);
      setRecoverySecret(attempt.recoverySecret);
      workspaceAttempt.current = null;
      setPendingWorkspaceAttempt(false);
      setView('recovery');
    } catch (error) {
      setWorkspaceError(error instanceof Error
        ? error.message
        : 'Workspace creation could not be confirmed. Retry while this page remains open.');
    } finally {
      setCreatingWorkspace(false);
    }
  }

  function startSeparateWorkspace() {
    workspaceAttempt.current = null;
    setPendingWorkspaceAttempt(false);
    setWorkspaceError('');
    setView('setup');
  }

  function continueToTasks() {
    setRecoverySecret('');
    setView('tasks');
    void refreshTasks();
  }

  function updateTaskDraft(field: keyof TaskDraft, value: string) {
    if (savingTask) {
      return;
    }

    setTaskDraft((current) => ({ ...current, [field]: value }));
    setTaskError('');
    setTaskStatus('');
  }

  async function submitTask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!taskDraft.title.trim()) {
      setTaskError('Enter a task title.');
      setTaskStatus('');
      return;
    }

    if (!taskOperationKey.current) {
      try {
        taskOperationKey.current = randomToken();
      } catch {
        setTaskError('This browser cannot create a secure operation key. Your draft is unchanged.');
        return;
      }
    }

    const submittedDraft = { ...taskDraft };
    setSavingTask(true);
    setTaskError('');
    setTaskStatus('Saving task.');
    try {
      const createdTask = await createTask(submittedDraft, taskOperationKey.current);
      refreshSequence.current += 1;
      setRefreshing(false);
      setTasks((current) => [createdTask, ...current.filter((task) => task.id !== createdTask.id)]);
      setTaskDraft(emptyDraft);
      taskOperationKey.current = null;
      setTaskStatus('Saved');
    } catch (error) {
      setTaskStatus('');
      setTaskError(error instanceof Error
        ? `${error.message} Your draft is unchanged.`
        : 'Save could not be confirmed. Your draft is unchanged; retry the same task.');
    } finally {
      setSavingTask(false);
    }
  }

  if (view === 'checking') {
    return <main><h1>Private task space</h1><p role="status">Checking this browser for a task space.</p></main>;
  }

  if (view === 'lost') {
    return (
      <main>
        <h1>Setup could not be recovered</h1>
        <section>
          <p role="alert">
            The setup credentials existed only in the previous page. This attempted task space cannot be recovered from this browser.
            It may be inaccessible if its request reached the service.
          </p>
          <p>Start a separate task space only if you choose to. Nothing will be created until you confirm again.</p>
          <button type="button" onClick={startSeparateWorkspace}>I understand, start a separate task space</button>
        </section>
      </main>
    );
  }

  if (view === 'setup') {
    const canCreate = Boolean(timeZones?.includes(timeZone)) && !creatingWorkspace;
    return (
      <main>
        <h1>Start a private task space</h1>
        <section>
          <p>No account is required. Your task space is private to this browser until you set up another device.</p>
          <form onSubmit={submitWorkspace}>
            <div className="field">
              <label htmlFor="workspace-time-zone">Workspace time zone</label>
              <select
                id="workspace-time-zone"
                value={timeZone}
                onChange={(event) => {
                  setTimeZone(event.currentTarget.value);
                  setWorkspaceError('');
                }}
                disabled={!timeZones || creatingWorkspace || Boolean(workspaceAttempt.current)}
                required
              >
                <option value="">Select a supported time zone</option>
                {timeZones?.map((zone) => <option key={zone} value={zone}>{zone}</option>)}
              </select>
            </div>
            {timeZoneError && (
              <div>
                <p className="error" role="alert" aria-label="Time zone loading error">{timeZoneError}</p>
                <button
                  type="button"
                  onClick={() => setTimeZoneRetryCount((count) => count + 1)}
                  disabled={creatingWorkspace}
                >
                  Retry time zone loading
                </button>
              </div>
            )}
            {workspaceError && <p className="error" role="alert">{workspaceError}</p>}
            <button type="submit" disabled={!canCreate}>
              {creatingWorkspace
                ? 'Creating task space'
                : workspaceAttempt.current
                  ? 'Retry workspace creation'
                  : 'Create my private task space'}
            </button>
          </form>
          <p>Creating a task space does not save credentials in browser storage. If setup is interrupted before confirmation, access to that attempt cannot be recovered.</p>
        </section>
      </main>
    );
  }

  if (view === 'recovery') {
    return (
      <main>
        <h1>Your private task space is ready</h1>
        <section>
          <p>Workspace time zone: <strong>{workspaceTimeZone}</strong></p>
          <p>Save this recovery secret separately.</p>
          <output className="recovery-secret" aria-label="Recovery secret">{recoverySecret}</output>
          <p>This secret is shown once and is not saved in browser storage.</p>
          <button type="button" onClick={continueToTasks}>I've saved the recovery secret</button>
        </section>
      </main>
    );
  }

  return (
    <main>
      <div className="toolbar">
        <h1>Tasks</h1>
        <button type="button" onClick={() => void refreshTasks()} disabled={refreshing}>
          {refreshing ? 'Refreshing' : 'Refresh tasks'}
        </button>
      </div>
      <p>Workspace time zone: <strong>{workspaceTimeZone}</strong></p>
      {lostRecoveryNotice && (
        <p className="notice" role="status">
          This browser has access to the task space, but setup was interrupted. If the recovery secret was not saved separately, it cannot be recovered here.
        </p>
      )}
      {refreshError && <p className="error" role="alert">{refreshError}</p>}
      <section>
        <h2>Add a task</h2>
        <form onSubmit={submitTask} noValidate>
          <div className="field">
            <label htmlFor="task-title">Task title</label>
            <input
              id="task-title"
              value={taskDraft.title}
              onChange={(event) => updateTaskDraft('title', event.currentTarget.value)}
              aria-describedby={taskError ? 'task-error' : undefined}
              disabled={savingTask}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="task-description">Description (optional)</label>
            <textarea
              id="task-description"
              value={taskDraft.description}
              onChange={(event) => updateTaskDraft('description', event.currentTarget.value)}
              disabled={savingTask}
            />
          </div>
          <div className="field">
            <label htmlFor="task-due-date">Due date (optional)</label>
            <input
              id="task-due-date"
              type="date"
              value={taskDraft.dueDate}
              onChange={(event) => updateTaskDraft('dueDate', event.currentTarget.value)}
              disabled={savingTask}
            />
          </div>
          <div className="field">
            <label htmlFor="task-priority">Priority (optional)</label>
            <select
              id="task-priority"
              value={taskDraft.priority}
              onChange={(event) => updateTaskDraft('priority', event.currentTarget.value as TaskDraft['priority'])}
              disabled={savingTask}
            >
              <option value="">No priority</option>
              <option value="Low">Low</option>
              <option value="Medium">Medium</option>
              <option value="High">High</option>
            </select>
          </div>
          {taskError && <p id="task-error" className="error" role="alert">{taskError}</p>}
          {taskStatus && <p className="status" role="status">{taskStatus}</p>}
          <button type="submit" disabled={savingTask}>{savingTask ? 'Saving task' : 'Save task'}</button>
        </form>
      </section>
      <section className="panel" aria-labelledby="task-list-title">
        <h2 id="task-list-title">Your tasks</h2>
        {tasks.length === 0
          ? <p>No tasks yet.</p>
          : (
            <ul className="task-list">
              {tasks.map((task) => (
                <li key={task.id}>
                  <strong>{task.title}</strong>
                  {task.description && <p>{task.description}</p>}
                  {task.dueDate && <p>Due {task.dueDate}</p>}
                  {task.priority && <p>Priority: {task.priority}</p>}
                </li>
              ))}
            </ul>
          )}
      </section>
    </main>
  );
}
