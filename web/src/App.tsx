import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ApiError,
  createOperationKey,
  createSecret,
  createTask,
  createWorkspace,
  getTasks,
  getTimeZones,
  getWorkspace,
} from './api';
import type { CreateTaskInput, CreateWorkspaceOperation, Task, Workspace } from './api';

type AccessState =
  | 'checking'
  | 'setup'
  | 'authorized'
  | 'workspace-error'
  | 'tasks-error'
  | 'unauthorized';

interface PendingTaskCreate {
  idempotencyKey: string;
  payload: CreateTaskInput;
}

const emptyDraft: CreateTaskInput = {
  title: '',
  description: '',
  dueDate: null,
  priority: null,
};

function isUnauthorized(error: unknown): error is ApiError {
  return error instanceof ApiError && error.status === 401;
}

function sameDraft(left: CreateTaskInput, right: CreateTaskInput): boolean {
  return left.title === right.title &&
    left.description === right.description &&
    left.dueDate === right.dueDate &&
    left.priority === right.priority;
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError && error.status > 0 && error.status < 500) {
    return error.message;
  }

  return 'The service could not be reached. Your input is preserved.';
}

export default function App() {
  const [access, setAccess] = useState<AccessState>('checking');
  const [workspace, setWorkspace] = useState<Workspace | null>(null);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [timeZones, setTimeZones] = useState<string[]>([]);
  const [timeZoneState, setTimeZoneState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [timeZone, setTimeZone] = useState(() => {
    try {
      return Intl.DateTimeFormat().resolvedOptions().timeZone || '';
    } catch {
      return '';
    }
  });
  const [workspaceOperation, setWorkspaceOperation] = useState<CreateWorkspaceOperation | null>(null);
  const [workspaceFailure, setWorkspaceFailure] = useState<string | null>(null);
  const [confirmNewWorkspace, setConfirmNewWorkspace] = useState(false);
  const [creatingWorkspace, setCreatingWorkspace] = useState(false);
  const [recoverySecret, setRecoverySecret] = useState<string | null>(null);
  const [draft, setDraft] = useState<CreateTaskInput>(emptyDraft);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);
  const [saveMessage, setSaveMessage] = useState('');
  const [savingTask, setSavingTask] = useState(false);
  const [pendingTask, setPendingTask] = useState<PendingTaskCreate | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const draftRef = useRef(draft);
  const readSequence = useRef(0);
  const refreshTasksRef = useRef<() => void>(() => undefined);

  const loadTimeZones = useCallback(async () => {
    setTimeZoneState('loading');
    try {
      const supported = await getTimeZones();
      setTimeZones(supported);
      setTimeZone(current => supported.includes(current) ? current : '');
      setTimeZoneState('ready');
    } catch {
      setTimeZoneState('error');
    }
  }, []);

  const loadTasks = useCallback(async () => {
    const sequence = ++readSequence.current;
    setRefreshing(true);
    try {
      const currentTasks = await getTasks();
      if (sequence === readSequence.current) {
        setTasks(currentTasks);
        setAccess('authorized');
        setSaveMessage('');
      }
    } catch (error) {
      if (sequence === readSequence.current) {
        setAccess(isUnauthorized(error) ? 'unauthorized' : 'tasks-error');
      }
    } finally {
      if (sequence === readSequence.current) {
        setRefreshing(false);
      }
    }
  }, []);

  const checkWorkspace = useCallback(async () => {
    setAccess('checking');
    try {
      const currentWorkspace = await getWorkspace();
      setWorkspace(currentWorkspace);
      setAccess('authorized');
      await loadTasks();
    } catch (error) {
      if (isUnauthorized(error)) {
        setAccess(error.code === 'missing_device' ? 'setup' : 'unauthorized');
      } else {
        setAccess('workspace-error');
      }
    }
  }, [loadTasks]);

  useEffect(() => {
    const startup = window.setTimeout(() => {
      void loadTimeZones();
      void checkWorkspace();
    }, 0);

    return () => window.clearTimeout(startup);
  }, [checkWorkspace, loadTimeZones]);

  useEffect(() => {
    refreshTasksRef.current = () => {
      if (access === 'authorized') {
        void loadTasks();
      }
    };
  }, [access, loadTasks]);

  useEffect(() => {
    let lastRefresh = 0;
    const refreshOnReturn = () => {
      const now = Date.now();
      if (!document.hidden && now - lastRefresh > 250) {
        lastRefresh = now;
        refreshTasksRef.current();
      }
    };

    window.addEventListener('focus', refreshOnReturn);
    document.addEventListener('visibilitychange', refreshOnReturn);
    return () => {
      window.removeEventListener('focus', refreshOnReturn);
      document.removeEventListener('visibilitychange', refreshOnReturn);
    };
  }, []);

  const updateDraft = <K extends keyof CreateTaskInput>(field: K, value: CreateTaskInput[K]) => {
    setDraft(current => {
      const updated = { ...current, [field]: value };
      draftRef.current = updated;
      return updated;
    });
    setValidationMessage(null);
  };

  const createWorkspaceNow = async () => {
    if (!timeZone || !timeZones.includes(timeZone) || creatingWorkspace) {
      return;
    }

    const operation = workspaceOperation ?? {
      idempotencyKey: createOperationKey(),
      timeZone,
      deviceToken: createSecret(),
      recoverySecret: createSecret(),
    };
    setWorkspaceOperation(operation);
    setWorkspaceFailure(null);
    setCreatingWorkspace(true);

    try {
      const created = await createWorkspace(operation);
      setWorkspace(created);
      setRecoverySecret(operation.recoverySecret);
      setWorkspaceOperation(null);
      setAccess('authorized');
      setTasks([]);
      setSaveMessage('');
      await loadTasks();
    } catch (error) {
      setWorkspaceFailure(errorMessage(error));
    } finally {
      setCreatingWorkspace(false);
    }
  };

  const saveTaskOperation = async (operation: PendingTaskCreate) => {
    if (savingTask) {
      return;
    }

    setSavingTask(true);
    setValidationMessage(null);
    setSaveMessage('Saving task...');
    try {
      const saved = await createTask(operation.payload, operation.idempotencyKey);
      readSequence.current += 1;
      setTasks(current => [saved, ...current.filter(task => task.id !== saved.id)]);
      setPendingTask(null);
      setSaveMessage('Task saved.');
      if (sameDraft(draftRef.current, operation.payload)) {
        draftRef.current = emptyDraft;
        setDraft(emptyDraft);
      }
    } catch (error) {
      if (error instanceof ApiError && error.code === 'csrf_unavailable') {
        setPendingTask(operation);
        setSaveMessage('Security verification failed. No task was sent; the original draft is preserved.');
      } else if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        setPendingTask(null);
        setSaveMessage(errorMessage(error));
      } else {
        setPendingTask(operation);
        setSaveMessage('We could not confirm the save. Retry the original task before saving an edited draft.');
      }
    } finally {
      setSavingTask(false);
    }
  };

  const submitTask = () => {
    if (pendingTask) {
      void saveTaskOperation(pendingTask);
      return;
    }

    const submitted = { ...draftRef.current };
    if (!submitted.title.trim()) {
      setValidationMessage('Enter a task title before saving.');
      setSaveMessage('');
      return;
    }

    void saveTaskOperation({
      idempotencyKey: createOperationKey(),
      payload: submitted,
    });
  };

  const refreshWorkspace = () => {
    void checkWorkspace();
  };

  const accessIsLoading = access === 'checking';
  const showTaskView = workspace !== null &&
    (access === 'authorized' || access === 'tasks-error');

  return (
    <main className="app-shell">
      <header className="page-header">
        <p className="eyebrow">Private task space</p>
        <h1>Task Manager</h1>
      </header>

      {accessIsLoading && <p role="status">Checking workspace access...</p>}

      {access === 'workspace-error' && (
        <section className="notice" role="alert">
          <p>Workspace access could not be checked. Your existing device identity has not been replaced.</p>
          <button type="button" onClick={refreshWorkspace}>Retry workspace access</button>
        </section>
      )}

      {access === 'unauthorized' && (
        <section className="notice" role="alert">
          <h2>Workspace access needs attention</h2>
          <p>Your existing workspace access could not be confirmed. No new workspace was created.</p>
          <p>Use an authorized device or your separately saved recovery secret to restore access.</p>
        </section>
      )}

      {access === 'setup' && recoverySecret === null && (
        <section className="panel" aria-labelledby="workspace-heading">
          <h2 id="workspace-heading">Start without an account</h2>
          <p>Create a private task space. If an earlier creation response was lost, that space may be inaccessible and is not automatically deleted.</p>
          {timeZoneState === 'error' && (
            <p className="notice" role="alert">
              Supported time zones could not be loaded.
              {' '}
              <button type="button" onClick={() => void loadTimeZones()}>Retry time zones</button>
            </p>
          )}
          <label htmlFor="time-zone">Workspace time zone</label>
          <select
            id="time-zone"
            value={timeZone}
            disabled={timeZoneState !== 'ready' || creatingWorkspace || workspaceOperation !== null}
            onChange={event => setTimeZone(event.target.value)}
          >
            <option value="">Select a supported time zone</option>
            {timeZones.map(zone => <option key={zone} value={zone}>{zone}</option>)}
          </select>
          {workspaceFailure && <p role="alert">{workspaceFailure}</p>}
          {workspaceOperation && workspaceFailure && !confirmNewWorkspace && (
            <button type="button" onClick={() => setConfirmNewWorkspace(true)}>
              Start a new workspace attempt
            </button>
          )}
          {confirmNewWorkspace && (
            <div className="notice" role="alert">
              <p>This may leave the earlier workspace inaccessible. Confirm only if you intend to start a separate task space.</p>
              <button
                type="button"
                onClick={() => {
                  setWorkspaceOperation(null);
                  setWorkspaceFailure(null);
                  setConfirmNewWorkspace(false);
                }}
              >
                Confirm new workspace
              </button>
              <button type="button" onClick={() => setConfirmNewWorkspace(false)}>Keep current attempt</button>
            </div>
          )}
          <button
            type="button"
            disabled={timeZoneState !== 'ready' || !timeZones.includes(timeZone) || creatingWorkspace}
            onClick={() => void createWorkspaceNow()}
          >
            {creatingWorkspace
              ? 'Creating task space...'
              : workspaceOperation
                ? 'Retry workspace creation'
                : 'Create task space'}
          </button>
        </section>
      )}

      {recoverySecret !== null && (
        <section className="panel secret-panel" role="status" aria-labelledby="recovery-heading">
          <h2 id="recovery-heading">Save your recovery secret</h2>
          <p>Keep this secret separately. It will not be stored in this browser and cannot be shown again.</p>
          <output aria-label="Recovery secret">{recoverySecret}</output>
          <button type="button" onClick={() => setRecoverySecret(null)}>I saved this secret</button>
        </section>
      )}

      {showTaskView && workspace && (
        <section className="panel" aria-labelledby="tasks-heading">
          <div className="section-heading">
            <div>
              <h2 id="tasks-heading">Your tasks</h2>
              <p>Workspace time zone: <strong>{workspace.timeZone}</strong></p>
            </div>
            <button type="button" onClick={() => void loadTasks()} disabled={refreshing}>
              {refreshing ? 'Refreshing...' : 'Refresh tasks'}
            </button>
          </div>

          {access === 'tasks-error' && (
            <p className="notice" role="alert">
              Tasks could not be refreshed. Your existing task view is unchanged.
              {' '}
              <button type="button" onClick={() => void loadTasks()}>Retry task loading</button>
            </p>
          )}

          <form
            onSubmit={event => {
              event.preventDefault();
              submitTask();
            }}
          >
            <label htmlFor="task-title">Task title</label>
            <input
              id="task-title"
              value={draft.title}
              onChange={event => updateDraft('title', event.target.value)}
              aria-invalid={validationMessage !== null}
              aria-describedby={validationMessage ? 'task-title-error' : undefined}
            />
            {validationMessage && <p id="task-title-error" className="field-error" role="alert">{validationMessage}</p>}

            <label htmlFor="task-description">Description (optional)</label>
            <textarea
              id="task-description"
              value={draft.description}
              onChange={event => updateDraft('description', event.target.value)}
              rows={3}
            />

            <label htmlFor="task-due-date">Due date (optional)</label>
            <input
              id="task-due-date"
              type="date"
              value={draft.dueDate ?? ''}
              onChange={event => updateDraft('dueDate', event.target.value || null)}
            />

            <label htmlFor="task-priority">Priority (optional)</label>
            <select
              id="task-priority"
              value={draft.priority ?? ''}
              onChange={event => {
                const selected = event.target.value;
                const priority: CreateTaskInput['priority'] =
                  selected === 'Low' || selected === 'Medium' || selected === 'High'
                    ? selected
                    : null;
                updateDraft('priority', priority);
              }}
            >
              <option value="">No priority</option>
              <option value="Low">Low</option>
              <option value="Medium">Medium</option>
              <option value="High">High</option>
            </select>

            <div className="form-actions">
              <button type="submit" disabled={savingTask}>
                {savingTask ? 'Saving task...' : pendingTask ? 'Retry original task' : 'Save task'}
              </button>
              {pendingTask && !savingTask && (
                <span>Edited text stays as a draft until the original save is confirmed.</span>
              )}
            </div>
            <p role="status" aria-live="polite">{saveMessage}</p>
          </form>

          <ul className="task-list" aria-label="Tasks">
            {tasks.map(task => (
              <li key={task.id}>
                <h3>{task.title}</h3>
                {task.description && <p>{task.description}</p>}
                {task.dueDate && <p>Due {task.dueDate}</p>}
                {task.priority && <p>Priority: {task.priority}</p>}
              </li>
            ))}
          </ul>
          {tasks.length === 0 && access === 'authorized' && <p>No tasks yet. Add your first task above.</p>}
        </section>
      )}
    </main>
  );
}
