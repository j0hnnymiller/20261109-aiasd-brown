import { expect, test } from '@playwright/test'

test('creates a private task space, saves a task, and reloads server-confirmed data', async ({ page, context }) => {
  const taskReads: string[] = []
  page.on('request', request => {
    if (new URL(request.url()).pathname === '/api/tasks') {
      taskReads.push(request.method())
    }
  })

  await page.goto('/')
  await page.getByRole('button', { name: 'Create a private task space' }).click()
  await page.getByLabel('Task-space time zone').selectOption('America/Los_Angeles')
  await page.getByRole('button', { name: 'Create task space' }).click()

  await expect(page.getByRole('heading', { name: 'Save your recovery secret' })).toBeVisible()
  const recoverySecret = await page.locator('.secret code').textContent()
  expect(recoverySecret).toMatch(/^[A-Za-z0-9_-]{43}$/)
  expect(await page.evaluate(() => JSON.stringify(localStorage))).not.toContain(recoverySecret)
  expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toContain(recoverySecret)
  expect(await page.evaluate(() => localStorage.getItem('task-manager:workspace-creation-pending'))).toBeNull()

  const cookies = await context.cookies()
  const deviceCookie = cookies.find(cookie => cookie.name === '__Host-device')
  expect(deviceCookie).toMatchObject({
    secure: true,
    httpOnly: true,
    sameSite: 'Lax',
    path: '/',
  })
  expect(await page.evaluate(() => document.cookie)).not.toContain('__Host-device=')

  await page.getByRole('button', { name: "I've saved my recovery secret" }).click()
  await page.getByLabel('Task title').fill('Review the first slice')
  await page.getByLabel('Description (optional)').fill('Confirm the delivered behavior')
  await page.getByLabel('Due date (optional)').fill('2026-12-01')
  await page.getByLabel('Priority (optional)').selectOption('High')
  await page.getByRole('button', { name: 'Save task' }).click()

  await expect(page.getByRole('status').filter({ hasText: 'Task saved.' })).toBeVisible()
  await expect(page.getByRole('listitem').getByText('Review the first slice')).toBeVisible()
  await expect(page.getByText('Confirm the delivered behavior')).toBeVisible()
  await expect(page.locator('time[datetime="2026-12-01"]')).toBeVisible()

  const taskReadCountAfterSave = taskReads.length
  await page.waitForTimeout(1500)
  expect(taskReads).toHaveLength(taskReadCountAfterSave)

  await page.evaluate(() => window.dispatchEvent(new Event('focus')))
  await expect.poll(() => taskReads.length).toBe(taskReadCountAfterSave + 1)
  await page.waitForTimeout(1500)
  expect(taskReads).toHaveLength(taskReadCountAfterSave + 1)

  await page.reload()
  await expect(page.getByRole('listitem').getByText('Review the first slice')).toBeVisible()
  await expect(page.locator('time[datetime="2026-12-01"]')).toBeVisible()
})

test('rejects whitespace-only titles with actionable feedback', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('button', { name: 'Create a private task space' }).click()
  await page.getByLabel('Task-space time zone').selectOption('America/Los_Angeles')
  await page.getByRole('button', { name: 'Create task space' }).click()
  await page.getByRole('button', { name: "I've saved my recovery secret" }).click()
  await page.getByLabel('Task title').fill('   ')
  await page.getByRole('button', { name: 'Save task' }).click()

  await expect(page.getByRole('alert')).toHaveText('Enter a task title.')
  await expect(page.getByText('No tasks yet. Add your first task above.')).toBeVisible()
})

test('supports the first-use and first-task workflow using only the keyboard', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: /your tasks, in a private space/i })).toBeVisible()
  await page.keyboard.press('Tab')
  await expect(page.getByRole('button', { name: 'Create a private task space' })).toBeFocused()
  await page.keyboard.press('Enter')

  const timeZone = page.getByLabel('Task-space time zone')
  await expect(timeZone).toBeVisible()
  await page.keyboard.press('Tab')
  await expect(timeZone).toBeFocused()
  if (await timeZone.inputValue() === '') {
    await page.keyboard.press('Space')
    await page.keyboard.press('ArrowDown')
    await page.keyboard.press('Enter')
    await expect(timeZone).not.toHaveValue('')
  }
  await page.keyboard.press('Tab')
  await expect(page.getByRole('button', { name: 'Create task space' })).toBeFocused()
  await page.keyboard.press('Enter')

  await expect(page.getByRole('heading', { name: 'Save your recovery secret' })).toBeVisible()
  await page.keyboard.press('Tab')
  await page.keyboard.press('Tab')
  await expect(page.getByRole('button', { name: "I've saved my recovery secret" })).toBeFocused()
  await page.keyboard.press('Enter')

  const refreshButton = page.getByRole('button', { name: 'Refresh tasks' })
  await expect(refreshButton).toBeEnabled()
  const title = page.getByLabel('Task title')
  await page.keyboard.press('Tab')
  await expect(refreshButton).toBeFocused()
  await page.keyboard.press('Tab')
  await expect(title).toBeFocused()
  await page.keyboard.type('Keyboard-created task')
  await page.keyboard.press('Tab')
  await page.keyboard.type('Keyboard-accessible workflow')
  await page.keyboard.press('Tab')
  const dueDate = page.getByLabel('Due date (optional)')
  await expect(dueDate).toBeFocused()
  const priority = page.getByLabel('Priority (optional)')
  for (let attempt = 0; attempt < 5; attempt++) {
    if (await priority.evaluate(element => element === document.activeElement)) {
      break
    }
    await page.keyboard.press('Tab')
  }
  await expect(priority).toBeFocused()
  await page.keyboard.press('ArrowDown')
  await expect(priority).toHaveValue('High')
  await page.keyboard.press('Tab')
  await expect(page.getByRole('button', { name: 'Save task' })).toBeFocused()
  await page.keyboard.press('Enter')

  await expect(page.getByRole('listitem').getByText('Keyboard-created task')).toBeVisible()
  await expect(page.getByText('Keyboard-accessible workflow')).toBeVisible()
})
