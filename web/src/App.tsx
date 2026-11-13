import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import {
  ApiError,
  createTask,
  createWorkspace,
  getCsrfToken,
  getTasks,
  getTimeZones,
  getWorkspace,
  type CreateTaskInput,
  type CreateWorkspaceInput,
  type Priority,
  type Task,
  type Workspace,
} from './api'
import './App.css'

const pendingCreationKey = 'task-manager:workspace-creation-pending'
const priorities: Priority[] = ['High', 'Medium', 'Low']

type Screen = 'loading' | 'welcome' | 'time-zone' | 'creating' | 'recovery-secret' | 'tasks' | 'unavailable' | 'creation-lost' | 'storage-error'

interface TaskDraft {
  title: string
  description: string
  dueDate: string
  priority: Priority | ''
  idempotencyKey: string
}

function App() {
  const [screen, setScreen] = useState<Screen>('loading')
  const [workspace, setWorkspace] = useState<Workspace | null>(null)
  const [timeZones, setTimeZones] = useState<string[]>([])
  const [timeZoneId, setTimeZoneId] = useState('')
  const [workspaceAttempt, setWorkspaceAttempt] = useState<CreateWorkspaceInput | null>(null)
  const [taskAttempt, setTaskAttempt] = useState<TaskDraft | null>(null)
  const [recoverySecret, setRecoverySecret] = useState<string | null>(null)
  const [storageError, setStorageError] = useState(false)
  const [startupError, setStartupError] = useState('')
  const [creationError, setCreationError] = useState('')
  const [tasks, setTasks] = useState<Task[]>([])
  const [refreshError, setRefreshError] = useState('')
  const [lastRefreshed, setLastRefreshed] = useState('')
  const [refreshing, setRefreshing] = useState(false)
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [dueDate, setDueDate] = useState('')
  const [priority, setPriority] = useState<Priority | ''>('')
  const [titleError, setTitleError] = useState('')
  const [saveError, setSaveError] = useState('')
  const [saveMessage, setSaveMessage] = useState('')
  const [saving, setSaving] = useState(false)
  const requestSequence = useRef(0)
  const lastResumeRefresh = useRef(0)

  const refreshTasks = useCallback(async () => {
    const sequence = ++requestSequence.current
    setRefreshing(true)
    setRefreshError('')
    try {
      const currentTasks = await getTasks()
      if (sequence === requestSequence.current) {
        setTasks(currentTasks)
        setLastRefreshed(new Date().toLocaleTimeString())
      }
    } catch {
      if (sequence === requestSequence.current) {
        setRefreshError('Tasks could not be refreshed. The list below may be out of date.')
      }
    } finally {
      if (sequence === requestSequence.current) {
        setRefreshing(false)
      }
    }
  }, [])

  const openWorkspace = useCallback(async () => {
    try {
      const currentWorkspace = await getWorkspace()
      setWorkspace(currentWorkspace)
      setScreen('tasks')
      await refreshTasks()
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        try {
          setScreen(localStorage.getItem(pendingCreationKey) === 'true' ? 'creation-lost' : 'welcome')
        } catch {
          setStorageError(true)
          setScreen('storage-error')
        }
      } else {
        setStartupError('The task space could not be loaded. Check your connection and try again.')
        setScreen('unavailable')
      }
    }
  }, [refreshTasks])

  useEffect(() => {
    void Promise.resolve().then(openWorkspace)
  }, [openWorkspace])

  useEffect(() => {
    if (screen !== 'tasks') {
      return
    }

    const refreshOnResume = () => {
      const now = Date.now()
      if (document.visibilityState === 'visible' && now - lastResumeRefresh.current > 1000) {
        lastResumeRefresh.current = now
        void refreshTasks()
      }
    }
    document.addEventListener('visibilitychange', refreshOnResume)
    window.addEventListener('focus', refreshOnResume)
    return () => {
      document.removeEventListener('visibilitychange', refreshOnResume)
      window.removeEventListener('focus', refreshOnResume)
    }
  }, [refreshTasks, screen])

  const beginWorkspaceCreation = async () => {
    setCreationError('')
    setWorkspaceAttempt(null)
    setScreen('time-zone')
    try {
      const supportedZones = await getTimeZones()
      setTimeZones(supportedZones)
      const detectedZone = Intl.DateTimeFormat().resolvedOptions().timeZone
      setTimeZoneId(supportedZones.includes(detectedZone) ? detectedZone : '')
    } catch {
      setTimeZones([])
      setCreationError('Supported time zones could not be loaded. Check your connection and try again.')
    }
  }

  const submitWorkspaceCreation = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!timeZoneId || !timeZones.includes(timeZoneId)) {
      setCreationError('Choose a supported time zone before creating a task space.')
      return
    }

    const currentAttempt = workspaceAttempt ?? {
      timeZoneId,
      deviceCredential: createSecret(),
      recoverySecret: createSecret(),
      idempotencyKey: createSecret(),
    }
    setWorkspaceAttempt(currentAttempt)
    setCreationError('')
    setScreen('creating')

    try {
      await getCsrfToken()
      try {
        localStorage.setItem(pendingCreationKey, 'true')
      } catch {
        setStorageError(true)
        setScreen('storage-error')
        return
      }

      const createdWorkspace = await createWorkspace(currentAttempt)
      localStorage.removeItem(pendingCreationKey)
      setWorkspace(createdWorkspace)
      setRecoverySecret(currentAttempt.recoverySecret)
      setWorkspaceAttempt(null)
      setScreen('recovery-secret')
    } catch (error) {
      const validationMessage = error instanceof ApiError
        ? Object.values(error.validationErrors).flat()[0]
        : undefined
      setCreationError(validationMessage ?? (error instanceof ApiError
        ? error.message
        : 'The task space could not be created. Retry this same attempt while this page remains open.'))
      setScreen('time-zone')
    }
  }

  const confirmRecoverySecretSaved = () => {
    setRecoverySecret(null)
    setScreen('tasks')
    void refreshTasks()
  }

  const startSeparateWorkspace = () => {
    try {
      localStorage.removeItem(pendingCreationKey)
      void beginWorkspaceCreation()
    } catch {
      setStorageError(true)
      setScreen('storage-error')
    }
  }

  const submitTask = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSaveError('')
    setSaveMessage('')
    setTitleError('')
    if (!title.trim()) {
      setTitleError('Enter a task title.')
      return
    }

    const sameDraft = taskAttempt
      && taskAttempt.title === title
      && taskAttempt.description === description
      && taskAttempt.dueDate === dueDate
      && taskAttempt.priority === priority
    const currentAttempt: TaskDraft = sameDraft ? taskAttempt : {
      title,
      description,
      dueDate,
      priority,
      idempotencyKey: createSecret(),
    }
    setTaskAttempt(currentAttempt)
    setSaving(true)
    try {
      const task = await createTask(toCreateTaskInput(currentAttempt))
      requestSequence.current += 1
      setTasks(currentTasks => [task, ...currentTasks.filter(current => current.id !== task.id)])
      setTaskAttempt(null)
      setTitle('')
      setDescription('')
      setDueDate('')
      setPriority('')
      setSaveMessage('Task saved.')
      void refreshTasks()
    } catch (error) {
      if (error instanceof ApiError && error.validationErrors.title?.[0]) {
        setTitleError(error.validationErrors.title[0])
      } else {
        setSaveError(error instanceof ApiError
          ? error.message
          : 'The task could not be saved. Your draft is still here; try again.')
      }
    } finally {
      setSaving(false)
    }
  }

  if (screen === 'loading') {
    return <main className="shell"><p role="status">Loading your task space…</p></main>
  }

  if (screen === 'unavailable') {
    return (
      <main className="shell">
        <h1>Task space unavailable</h1>
        <p role="alert">{startupError}</p>
        <button
          type="button"
          onClick={() => {
            setStartupError('')
            setScreen('loading')
            void openWorkspace()
          }}
        >
          Try again
        </button>
      </main>
    )
  }

  if (screen === 'storage-error' || storageError) {
    return (
      <main className="shell">
        <h1>Browser storage unavailable</h1>
        <p role="alert">This browser cannot keep the non-secret retry marker needed to explain an interrupted setup. No task-space credentials were sent.</p>
        <button type="button" onClick={() => window.location.reload()}>Try again</button>
      </main>
    )
  }

  if (screen === 'creation-lost') {
    return (
      <main className="shell">
        <section className="card">
          <p className="eyebrow">Setup interrupted</p>
          <h1>That task space cannot be recovered here</h1>
          <p>The previous setup may have reached the server, but its credentials were held only in the closed page. An inaccessible task space may remain. Start another only if you want to continue.</p>
          <button type="button" onClick={startSeparateWorkspace}>I understand; start a separate task space</button>
        </section>
      </main>
    )
  }

  if (screen === 'welcome') {
    return (
      <main className="shell">
        <section className="card welcome-card">
          <p className="eyebrow">A quieter way to keep track</p>
          <h1>Your tasks, in a private space</h1>
          <p>No account or personal details are needed. Your device keeps access with a private credential, and your task list stays unchanged until you refresh or reopen the app.</p>
          <button type="button" onClick={() => void beginWorkspaceCreation()}>Create a private task space</button>
          <p className="fine-print">A recovery secret will be shown once for separate safekeeping.</p>
        </section>
      </main>
    )
  }

  if (screen === 'time-zone' || screen === 'creating') {
    return (
      <main className="shell">
        <section className="card">
          <p className="eyebrow">First use</p>
          <h1>Create your private task space</h1>
          <p>Your task space uses one time zone across devices. Confirm the detected zone or choose another supported zone.</p>
          <form onSubmit={submitWorkspaceCreation} noValidate>
            <label htmlFor="time-zone">Task-space time zone</label>
            <select
              id="time-zone"
              value={timeZoneId}
              onChange={event => setTimeZoneId(event.target.value)}
              required
              disabled={screen === 'creating' || workspaceAttempt !== null}
              aria-describedby="time-zone-help"
            >
              <option value="">Choose a time zone</option>
              {timeZones.map(zone => <option key={zone} value={zone}>{zone}</option>)}
            </select>
            <p id="time-zone-help" className="field-help">No server or UTC default is selected for you.</p>
            {creationError && <p role="alert" className="error">{creationError}</p>}
            <button type="submit" disabled={screen === 'creating' || !timeZoneId}>
              {screen === 'creating' ? 'Creating task space…' : workspaceAttempt ? 'Retry creating task space' : 'Create task space'}
            </button>
          </form>
        </section>
      </main>
    )
  }

  if (screen === 'recovery-secret') {
    return (
      <main className="shell">
        <section className="card">
          <p className="eyebrow">Task space ready</p>
          <h1>Save your recovery secret</h1>
          <p>This secret is shown once. Save it somewhere separate from this device. Anyone with it can recover access to this task space.</p>
          <p className="fine-print">Your default list is {workspace?.defaultListName}.</p>
          <p className="secret"><span className="sr-only">Recovery secret: </span><code>{recoverySecret}</code></p>
          <button
            type="button"
            className="secondary-button"
            onClick={() => {
              if (!recoverySecret || !navigator.clipboard) {
                setCreationError('Clipboard access is unavailable. Select and copy the recovery secret manually.')
                return
              }
              void navigator.clipboard.writeText(recoverySecret).then(
                () => setCreationError('Recovery secret copied. Keep it somewhere separate from this device.'),
                () => setCreationError('Clipboard access failed. Select and copy the recovery secret manually.'),
              )
            }}
          >
            Copy recovery secret
          </button>
          {creationError && <p role="status">{creationError}</p>}
          <button type="button" onClick={confirmRecoverySecretSaved}>I've saved my recovery secret</button>
        </section>
      </main>
    )
  }

  return (
    <main className="shell app-shell">
      <header className="app-header">
        <div>
          <p className="eyebrow">Your private task space</p>
          <h1>{workspace?.defaultListName ?? 'Tasks'}</h1>
          <p className="fine-print">Time zone: {workspace?.timeZoneId}</p>
        </div>
        <button type="button" className="secondary-button" onClick={() => void refreshTasks()} disabled={refreshing}>
          {refreshing ? 'Refreshing…' : 'Refresh tasks'}
        </button>
      </header>

      <section className="card task-entry">
        <h2>Add a task</h2>
        <form onSubmit={submitTask} noValidate>
          <label htmlFor="task-title">Task title</label>
          <input
            id="task-title"
            name="title"
            value={title}
            onChange={event => {
              setTitle(event.target.value)
              setTitleError('')
            }}
            required
            aria-invalid={Boolean(titleError)}
            aria-describedby={titleError ? 'task-title-error' : undefined}
          />
          {titleError && <p id="task-title-error" className="error" role="alert">{titleError}</p>}

          <label htmlFor="task-description">Description <span className="optional">(optional)</span></label>
          <textarea
            id="task-description"
            name="description"
            value={description}
            onChange={event => setDescription(event.target.value)}
            rows={3}
          />

          <div className="field-row">
            <div>
              <label htmlFor="task-due-date">Due date <span className="optional">(optional)</span></label>
              <input
                id="task-due-date"
                name="dueDate"
                type="date"
                value={dueDate}
                onChange={event => setDueDate(event.target.value)}
              />
            </div>
            <div>
              <label htmlFor="task-priority">Priority <span className="optional">(optional)</span></label>
              <select id="task-priority" value={priority} onChange={event => setPriority(event.target.value as Priority | '')}>
                <option value="">No priority</option>
                {priorities.map(item => <option key={item} value={item}>{item}</option>)}
              </select>
            </div>
          </div>

          {saveError && <p className="error" role="alert">{saveError}</p>}
          <button type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save task'}</button>
        </form>
      </section>

      <section className="task-section" aria-labelledby="task-list-heading">
        <div className="list-heading">
          <h2 id="task-list-heading">Your tasks</h2>
          <p className="fine-print">{lastRefreshed ? `Last refreshed ${lastRefreshed}` : 'Not refreshed yet'}</p>
        </div>
        {refreshError && <p role="alert" className="error">{refreshError}</p>}
        {saveMessage && <p role="status">{saveMessage}</p>}
        {tasks.length === 0
          ? <p className="empty-state">No tasks yet. Add your first task above.</p>
          : (
            <ul className="task-list">
              {tasks.map(task => (
                <li key={task.id} className="task-card">
                  <div className="task-card-heading">
                    <h3>{task.title}</h3>
                    {task.priority && <span className="priority">{task.priority}</span>}
                  </div>
                  {task.description && <p>{task.description}</p>}
                  {task.dueDate && <p className="fine-print"><time dateTime={task.dueDate}>Due {task.dueDate}</time></p>}
                </li>
              ))}
            </ul>
          )}
      </section>
    </main>
  )
}

function toCreateTaskInput(draft: TaskDraft): CreateTaskInput {
  return {
    title: draft.title,
    description: draft.description || null,
    dueDate: draft.dueDate || null,
    priority: draft.priority || null,
    idempotencyKey: draft.idempotencyKey,
  }
}

function createSecret(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(32))
  const binary = Array.from(bytes, value => String.fromCharCode(value)).join('')
  return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/, '')
}

export default App
