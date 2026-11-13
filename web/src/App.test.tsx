import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'
import { ApiError } from './api'

const api = vi.hoisted(() => ({
  getWorkspace: vi.fn(),
  getTimeZones: vi.fn(),
  getTasks: vi.fn(),
  getCsrfToken: vi.fn(),
  createWorkspace: vi.fn(),
  createTask: vi.fn(),
}))

vi.mock('./api', async () => {
  const actual = await vi.importActual<typeof import('./api')>('./api')
  return { ...actual, ...api }
})

describe('first-use and first-task workflow', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
    api.getWorkspace.mockRejectedValue(new ApiError(401, 'Unauthorized'))
    api.getTimeZones.mockResolvedValue(['America/Los_Angeles', 'UTC'])
    api.getCsrfToken.mockResolvedValue('csrf-token')
    api.getTasks.mockResolvedValue([])
  })

  it('creates a private task space and requires recovery-secret acknowledgement', async () => {
    const user = userEvent.setup()
    api.createWorkspace.mockResolvedValue({
      workspaceId: 'workspace-1',
      timeZoneId: 'America/Los_Angeles',
      defaultListName: 'Tasks',
    })

    render(<App />)
    await user.click(await screen.findByRole('button', { name: /create a private task space/i }))
    const timeZone = await screen.findByLabelText(/task-space time zone/i)
    await user.selectOptions(timeZone, 'America/Los_Angeles')
    await user.click(screen.getByRole('button', { name: /create task space/i }))

    expect(await screen.findByRole('heading', { name: /save your recovery secret/i })).toBeInTheDocument()
    expect(screen.getByText(/tasks/i)).toBeInTheDocument()
    expect(screen.queryByLabelText(/task title/i)).not.toBeInTheDocument()
    expect(api.createWorkspace).toHaveBeenCalledWith(expect.objectContaining({
      timeZoneId: 'America/Los_Angeles',
      deviceCredential: expect.any(String),
      recoverySecret: expect.any(String),
      idempotencyKey: expect.any(String),
    }))
    const creationInput = api.createWorkspace.mock.calls[0][0]
    expect(creationInput.deviceCredential).toHaveLength(43)
    expect(creationInput.recoverySecret).toHaveLength(43)
    expect(creationInput.deviceCredential).not.toBe(creationInput.recoverySecret)
    expect(JSON.stringify(localStorage)).not.toContain(creationInput.deviceCredential)
    expect(JSON.stringify(sessionStorage)).not.toContain(creationInput.recoverySecret)
    expect(localStorage.getItem('task-manager:workspace-creation-pending')).toBeNull()

    await user.click(screen.getByRole('button', { name: /i've saved my recovery secret/i }))
    expect(await screen.findByLabelText(/task title/i)).toBeInTheDocument()
  })

  it('requires an explicit supported time zone when browser detection is invalid', async () => {
    const user = userEvent.setup()
    const detectedOptions = new Intl.DateTimeFormat().resolvedOptions()
    vi.spyOn(Intl.DateTimeFormat.prototype, 'resolvedOptions').mockReturnValue({
      ...detectedOptions,
      timeZone: 'Invalid/Zone',
    })

    render(<App />)
    await user.click(await screen.findByRole('button', { name: /create a private task space/i }))
    const timeZone = await screen.findByLabelText(/task-space time zone/i)
    const createButton = screen.getByRole('button', { name: /create task space/i })

    expect(timeZone).toHaveValue('')
    expect(createButton).toBeDisabled()
    await user.selectOptions(timeZone, 'UTC')
    expect(createButton).toBeEnabled()
  })

  it('keeps task draft after a failed save and shows only server-confirmed tasks', async () => {
    const user = userEvent.setup()
    const taskSpace = { workspaceId: 'workspace-1', timeZoneId: 'UTC', defaultListName: 'Tasks' }
    const savedTask = {
      id: 'task-1',
      title: 'Review the plan',
      description: 'Check the first release',
      dueDate: '2026-12-01',
      priority: 'High',
      listName: 'Tasks',
    }
    api.getWorkspace.mockResolvedValue(taskSpace)
    api.getTasks.mockResolvedValueOnce([]).mockResolvedValue([savedTask])
    api.createTask
      .mockRejectedValueOnce(new ApiError(503, 'The task could not be saved. Try again.'))
      .mockResolvedValueOnce(savedTask)

    render(<App />)
    const title = await screen.findByLabelText(/task title/i)
    await user.type(title, 'Review the plan')
    await user.type(screen.getByLabelText(/description/i), 'Check the first release')
    await user.type(screen.getByLabelText(/due date/i), '2026-12-01')
    await user.selectOptions(screen.getByLabelText(/priority/i), 'High')
    await user.click(screen.getByRole('button', { name: /^save task$/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/could not be saved/i)
    expect(title).toHaveValue('Review the plan')
    expect(screen.queryByRole('listitem')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /^save task$/i }))
    expect(await screen.findByText('Review the plan')).toBeInTheDocument()
    expect(within(screen.getByRole('list')).getByText('Review the plan')).toBeInTheDocument()
    expect(api.createTask).toHaveBeenCalledTimes(2)
    expect(api.createTask.mock.calls[0][0].idempotencyKey).toBe(api.createTask.mock.calls[1][0].idempotencyKey)
    await waitFor(() => expect(api.getTasks).toHaveBeenCalled())
  })

  it('requires explicit confirmation before restarting after page memory is lost', async () => {
    const user = userEvent.setup()
    localStorage.setItem('task-manager:workspace-creation-pending', 'true')

    render(<App />)

    expect(await screen.findByRole('heading', { name: /that task space cannot be recovered/i })).toBeInTheDocument()
    expect(api.createWorkspace).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: /i understand; start a separate task space/i }))
    expect(await screen.findByRole('heading', { name: /create your private task space/i })).toBeInTheDocument()
    expect(localStorage.getItem('task-manager:workspace-creation-pending')).toBeNull()
    expect(api.createWorkspace).not.toHaveBeenCalled()
  })

  it('retains the last confirmed tasks and shows a stale warning when refresh fails', async () => {
    const user = userEvent.setup()
    api.getWorkspace.mockResolvedValue({ workspaceId: 'workspace-1', timeZoneId: 'UTC', defaultListName: 'Tasks' })
    api.getTasks
      .mockResolvedValueOnce([{
        id: 'task-1',
        title: 'Confirmed task',
        description: null,
        dueDate: null,
        priority: null,
        listName: 'Tasks',
      }])
      .mockRejectedValueOnce(new ApiError(503, 'Unavailable'))

    render(<App />)
    expect(await screen.findByText('Confirmed task')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /refresh tasks/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/may be out of date/i)
    expect(screen.getByText('Confirmed task')).toBeInTheDocument()
  })

  it('retries loading the task space after a temporary startup failure', async () => {
    const user = userEvent.setup()
    api.getWorkspace
      .mockRejectedValueOnce(new ApiError(503, 'Unavailable'))
      .mockResolvedValueOnce({ workspaceId: 'workspace-1', timeZoneId: 'UTC', defaultListName: 'Tasks' })

    render(<App />)
    expect(await screen.findByRole('heading', { name: /task space unavailable/i })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /try again/i }))

    expect(await screen.findByRole('heading', { name: 'Tasks' })).toBeInTheDocument()
    expect(api.getWorkspace).toHaveBeenCalledTimes(2)
  })
})
