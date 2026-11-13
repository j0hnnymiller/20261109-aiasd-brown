---
ai_generated: true
model: "OpenAI/gpt-6.1-sol@unknown"
operator: "jMM-at-CODETraining"
chat_id: "7c5afae0-9dc2-465f-a19e-58cc8812f033"
prompt: |
  create an architecture specification md
started: "2026-11-12T20:21:54-08:00"
ended: "2026-11-12T20:24:38-08:00"
task_durations:
  - task: "Reviewing evidence and specifying architecture"
    duration: "00:02:44"
total_duration: "00:02:44"
ai_log: "ai-logs/2026/11/12/7c5afae0-9dc2-465f-a19e-58cc8812f033/conversation.md"
source: "user request; architecture documentation index update"
revisions:
  - chat_id: "7c5afae0-9dc2-465f-a19e-58cc8812f033"
    model: "OpenAI/gpt-6.1-sol@unknown"
    operator: "jMM-at-CODETraining"
    prompt: "help me resolve the open decisions"
    started: "2026-11-12T20:52:34-08:00"
    ended: "2026-11-12T20:55:39-08:00"
    task_durations:
      - task: "Summarizing confirmed decisions and remaining deferrals"
        duration: "00:03:05"
    total_duration: "00:03:05"
    ai_log: "ai-logs/2026/11/12/7c5afae0-9dc2-465f-a19e-58cc8812f033/conversation.md"
    source: "user decision-form responses"
    change: "Added the confirmed product/access/retention decisions and explicit remaining deferrals."
  - chat_id: "7c5afae0-9dc2-465f-a19e-58cc8812f033"
    model: "OpenAI/gpt-6.1-sol@unknown"
    operator: "jMM-at-CODETraining"
    prompt: "help me choose a tech stack"
    started: "2026-11-12T21:00:33-08:00"
    ended: "2026-11-12T21:00:37-08:00"
    task_durations:
      - task: "Summarizing stack selection"
        duration: "00:00:04"
    total_duration: "00:00:04"
    ai_log: "ai-logs/2026/11/12/7c5afae0-9dc2-465f-a19e-58cc8812f033/conversation.md"
    source: "user stack-selection form"
    change: "Summarized the selected stack and retained hosting and operational deferrals."
  - chat_id: "e9752f6f-5119-4a18-af86-2254b152ab42"
    model: "unknown/unknown@unknown"
    operator: "jMM-at-CODETraining"
    prompt: "create instruction files for the tech stack"
    started: "2026-11-12T21:03:43-08:00"
    ended: "2026-11-12T21:08:49-08:00"
    task_durations:
      - task: "Creating stack instruction files and integration records"
        duration: "00:05:06"
    total_duration: "00:05:06"
    ai_log: "ai-logs/2026/11/12/e9752f6f-5119-4a18-af86-2254b152ab42/conversation.md"
    source: "user request"
    change: "Added workspace instructions for React/TypeScript, ASP.NET Core, and PostgreSQL and indexed them in the README."
  - chat_id: "e9752f6f-5119-4a18-af86-2254b152ab42"
    model: "unknown/unknown@unknown"
    operator: "jMM-at-CODETraining"
    prompt: "create an implementation spec for slice 1"
    started: "2026-11-12T21:20:48-08:00"
    ended: "2026-11-12T21:28:58-08:00"
    task_durations:
      - task: "Drafting and validating the Slice 1 implementation specification"
        duration: "00:08:10"
    total_duration: "00:08:10"
    ai_log: "ai-logs/2026/11/12/e9752f6f-5119-4a18-af86-2254b152ab42/conversation.md"
    source: "user request"
    change: "Created a draft implementation specification for Slice 1 and linked it from the implementation plan."
  - chat_id: "e9752f6f-5119-4a18-af86-2254b152ab42"
    model: "unknown/unknown@unknown"
    operator: "jMM-at-CODETraining"
    prompt: "help me unblock slice 1"
    started: "2026-11-12T21:30:19-08:00"
    ended: "2026-11-12T21:40:00-08:00"
    task_durations:
      - task: "Resolving Slice 1 implementation decisions"
        duration: "00:09:41"
    total_duration: "00:09:41"
    ai_log: "ai-logs/2026/11/12/e9752f6f-5119-4a18-af86-2254b152ab42/conversation.md"
    source: "user decision-form responses"
    change: "Updated architecture and stack guidance with selected Slice 1 technical decisions; specification is ready for review, not implementation."
  - chat_id: "a5e99ad6-f562-4860-a968-f64a1223ca97"
    model: "unknown/unknown@unknown"
    operator: "jMM-at-CODETraining"
    prompt: "implement slice 1"
    started: "2026-11-12T21:49:40-08:00"
    ended: "2026-11-12T22:37:35-08:00"
    task_durations:
      - task: "Implementing, verifying, and documenting Slice 1"
        duration: "00:47:55"
    total_duration: "00:47:55"
    ai_log: "ai-logs/2026/11/12/a5e99ad6-f562-4860-a968-f64a1223ca97/conversation.md"
    source: "User request and approved SL-001 specification"
    change: "Recorded the completed local Slice 1 implementation, verification commands, and setup instructions."
---

# Task Manager

This repository contains the first implemented vertical slice of a private task manager web app. Slice 1 supports account-free workspace creation, an initial recovery secret, and creating and refreshing tasks in the default `Tasks` list. Hosting and later slices remain out of scope.

- [Task Manager Web App Requirements](task-manager-requirements.md)
- [Task Manager MVP Project Plan](task-manager-mvp-plan.md)
- [Task Manager Implementation Plan: Vertical Slices](task-manager-implementation-plan.md)
- [SL-001: First Use and First Task](specs/SL-001-first-use-first-task.spec.md) - Approved and implemented locally; automated verification is recorded in the specification.
- [Task Manager MVP Architecture Specification](task-manager-architecture.md) - Requester-approved hosted architecture with React/TypeScript, ASP.NET Core, and PostgreSQL; hosting and explicit open decisions remain unresolved.
- [React and TypeScript Instructions](.github/instructions/react-typescript.instructions.md) - Frontend guidance for typed, accessible UI and explicit server refresh behavior.
- [ASP.NET Core Instructions](.github/instructions/aspnet-core.instructions.md) - Backend guidance for modular boundaries, device authorization, workspace isolation, and explicit errors.
- [PostgreSQL Instructions](.github/instructions/postgresql.instructions.md) - Database guidance for workspace integrity, transactions, concurrency, and restore safeguards.
- [Architecture Design Skill](.github/skills/architecture-design/SKILL.md) - Requirements-grounded architecture workflow with semantically colored Mermaid diagrams and data flow diagrams for principal data exchanges.

Milestone 1 (Product Readiness) is accepted. The MVP has private individual task spaces with no required accounts. The requester owns product acceptance and release decisions; the plan records the approved pilot measures and deferred account-specific questions.

Follow-up decisions define task/list rules, 10-minute pairing, 90-day device access, revoke-and-rotate recovery, a shared configurable time zone, snapshot pilot measurement, and immediate live task deletion with 30-day backup retention. The requester owns best-effort pilot operations/support. Hosting, budget, workload, performance targets, recovery objectives, and numeric abuse limits remain deferred; Slice 1 does not authorize later slices or pilot deployment.

## Local development

### Prerequisites

- .NET 10 SDK
- Node.js 22 or later
- Docker Compose

### Start PostgreSQL

Copy `.env.example` to `.env`, replace `POSTGRES_PASSWORD` with a strong local-only value, then start the database:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
docker compose up -d
```

PostgreSQL is bound to `127.0.0.1:5433`. The development database is `task_manager_dev`. Do not commit `.env`.

### Run the API and web app

In the API terminal, load the local connection settings and start ASP.NET Core:

```powershell
$local = @{}
Get-Content .env | ForEach-Object {
  if ($_ -match '^([^#=]+)=(.*)$') { $local[$matches[1]] = $matches[2] }
}
$env:ConnectionStrings__TaskManager = "Host=127.0.0.1;Port=5433;Database=$($local['POSTGRES_DB']);Username=$($local['POSTGRES_USER']);Password=$($local['POSTGRES_PASSWORD'])"
dotnet run --project src/TaskManager.Api --urls http://127.0.0.1:5000
```

In a second terminal:

```powershell
Set-Location web
npm ci
npm run dev
```

Open `http://127.0.0.1:5173`. The API applies database migrations at startup.

### Test and build

Run the frontend checks from `web`:

```powershell
npm run test
npm run lint
npm run build
npm audit
```

The API integration tests require a separate PostgreSQL database named `task_manager_test`. Create it using a PostgreSQL administration tool, then set `TASK_MANAGER_TEST_CONNECTION` to its connection string before running `dotnet test TaskManager.sln`. The browser tests similarly require a separate `task_manager_e2e` database selected by `ConnectionStrings__TaskManager`; install the browser once with `npx playwright install chromium`, then run `npm run test:e2e` from `web`. Keep both test databases isolated from `task_manager_dev`.
