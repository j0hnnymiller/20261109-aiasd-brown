---
ai_generated: true
model: "unknown/unknown@unknown"
operator: "jMM-at-CODETraining"
chat_id: "e9752f6f-5119-4a18-af86-2254b152ab42"
prompt: "create an implementation spec for slice 1"
started: "2026-11-12T21:20:48-08:00"
ended: "2026-11-12T21:28:58-08:00"
task_durations:
  - task: "Drafting and validating the Slice 1 implementation specification"
    duration: "00:08:10"
total_duration: "00:08:10"
ai_log: "ai-logs/2026/11/12/e9752f6f-5119-4a18-af86-2254b152ab42/conversation.md"
source: "user request; approved requirements, architecture, and implementation plan"
revisions:
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
    change: "Resolved toolchain, time-zone fallback, credentials/cookies, create retries, and technical review; moved the specification to Ready for technical review."
  - chat_id: "a0e9b1c3-fd41-4e9d-b025-2d81fe3ab4d2"
    model: "unknown/unknown@unknown"
    operator: "jMM-at-CODETraining"
    prompt: "i approve the slice 1 implementation spec"
    started: "2026-11-12T21:42:14-08:00"
    ended: "2026-11-12T21:42:54-08:00"
    task_durations:
      - task: "Recording Slice 1 specification approval"
        duration: "00:00:40"
    total_duration: "00:00:40"
    ai_log: "ai-logs/2026/11/12/a0e9b1c3-fd41-4e9d-b025-2d81fe3ab4d2/conversation.md"
    source: "user approval"
    change: "Recorded requester approval of the Slice 1 specification; implementation authorization remains ungranted."
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
    change: "Recorded implementation authorization, verified acceptance evidence, and remaining coverage gaps."
---

# SL-001: First Use and First Task

- **Status:** Implemented locally; automated verification completed on 2026-11-12; product acceptance pending
- **Owner/reviewer:** Requester as Product Owner, Release Decision Owner, and technical reviewer; implementation completed by the Copilot SDK assistant
- **Implementation authorization:** Granted by the requester's explicit "implement slice 1" request
- **Approval:** The requester approved this specification on 2026-11-12. Implementation was separately authorized by the explicit request recorded in the execution log.
- **Sources:** [Implementation plan, Slice 1](../task-manager-implementation-plan.md#slice-1-first-use-and-first-task); [product requirements](../task-manager-requirements.md); [approved architecture](../task-manager-architecture.md)

## Table of Contents

- [Goal](#goal)
- [Scope and exclusions](#scope-and-exclusions)
- [Context and dependencies](#context-and-dependencies)
- [Behavior and contracts](#behavior-and-contracts)
- [Acceptance criteria](#acceptance-criteria)
- [Verification](#verification)
- [Showcase and completion](#showcase-and-completion)
- [Resolved decisions and remaining gates](#resolved-decisions-and-remaining-gates)
- [Implementation handoff](#implementation-handoff)

## Goal

As an individual, I can start a private task space without registering an account, save a first task to its default Tasks list, and see the server-confirmed task after refreshing or reopening the app.

## Scope and exclusions

**In scope**

- React/TypeScript first-use, task-entry, and task-view experience.
- ASP.NET Core workspace initialization, initial credential verification and cookie setting, task creation, and workspace-scoped task reads.
- PostgreSQL persistence for the workspace, its default Tasks list, initial workspace time zone, credential verifiers, and first task.
- Server-confirmed save and refresh behavior, explicit validation and failure feedback, and accessibility for this workflow.

**Out of scope**

- Account registration, email, password, or external identity-provider integration.
- Pairing another device, device listing/revocation, or recovery operations; those belong to later slices. This slice issues the initial device credential and recovery secret only.
- Editing or removing tasks; creating/removing lists; search, filters, ordering, completion, due-status display, or changing the workspace time zone.
- Polling, push updates, offline writes, browser task-content persistence, whole-workspace deletion, hosting selection, and pilot operations.

## Context and dependencies

- **Existing implementation and conventions:** The repository contains planning and instruction documents but no application source, dependency manifests, test projects, or runnable test commands. The approved stack guides are [React/TypeScript](../.github/instructions/react-typescript.instructions.md), [ASP.NET Core](../.github/instructions/aspnet-core.instructions.md), and [PostgreSQL](../.github/instructions/postgresql.instructions.md). Do not infer a folder layout or library from examples in general guidance.
- **Prerequisites:** Approved architecture and selected technologies. No earlier implementation slice is required.
- **Environment:** Local development and isolated test data are sufficient for this slice. Hosting is not a prerequisite. Toolchain and test choices are recorded below; pin compatible patch versions and configure an isolated local PostgreSQL instance and safe test secrets before implementation.
- **Assumptions:** None beyond the approved requirements, architecture, and Slice 1 plan.
- **Remaining gates:** See [Resolved decisions and remaining gates](#resolved-decisions-and-remaining-gates). Do not resolve remaining pilot decisions by silently choosing defaults.

## Behavior and contracts

### First use and workspace creation

1. Require an explicit user action to create a private task space. Do not request or require an account identity.
2. Detect the first browser's IANA time-zone identifier and validate it. If missing or invalid, require the user to select a supported IANA zone before creation; never silently substitute the server's zone or UTC. Display the selected workspace zone.
3. Generate an independent 256-bit device credential, recovery secret, and workspace-creation idempotency key with the browser's cryptographically secure random source. Keep all raw values in page memory only. Send credential material only over HTTPS, suppress request-body logging, and store SHA-256 verifiers only. Persist a non-secret idempotency key digest, request fingerprint, and operation result with workspace initialization. If the response is lost while the page remains open, retry with the same operation key and credential values. A retry with the same key and request reissues the same device cookie and outcome without creating another workspace; reusing the key with a different request fails explicitly.
4. Create the workspace, its required default list named `Tasks`, and credential verifiers atomically. The device expires 90 days after issuance without sliding extension. A repeated idempotency key with a different request fingerprint must fail explicitly.
5. On confirmed creation, set the device credential only in a Secure, HttpOnly, SameSite=Lax, host-only `__Host-` cookie with `Path=/` and no `Domain`; enforce CSRF token and Origin checks on mutations. Present the recovery secret for separate safekeeping only after confirmation; never store it in browser storage or place either secret in a URL, log, telemetry, or error detail.
6. If the page reloads or closes before a lost creation response can be retried, do not persist or reconstruct the raw secrets. Explain that access to that attempted creation cannot be recovered; require an explicit user decision before starting a new workspace. An inaccessible incomplete workspace may remain; do not silently retry or auto-delete it.

### First task and read

- Allow creation of a task with a required nonblank title; accept optional description, date-only due date, and priority of Low, Medium, High, or unset.
- Assign the task to the workspace's default `Tasks` list on the server. Do not accept a client-selected workspace or arbitrary list as authority.
- Treat due date as a calendar date, not a timestamp. Preserve the approved title/description values; do not add unapproved trimming, markup, or normalization behavior.
- Return or read server-confirmed task data scoped to the workspace authenticated by the device credential. The browser must not supply a workspace identifier as proof of access.
- Refresh current data when the app opens, returns to its visible active view, or the user requests refresh. Do not poll or push updates. On refresh failure, retain any prior view with an explicit stale/error indication; on save failure, preserve the draft and do not show a saved state.
- Give each task-create operation an idempotency key scoped to the authenticated workspace. Persist the key digest, request fingerprint, and task identifier transactionally with the task. Repeating the same key and request returns the same task outcome; reusing the key with a different request fails explicitly. Do not retain task content in operational logs.

### Errors, compatibility, and operations

- Reject blank titles with actionable field feedback and do not persist a task.
- Fail unauthenticated or invalidly authorized reads/writes explicitly. Do not return data or disclose whether another workspace's resource exists.
- Distinguish validation, authorization, unavailable-resource, conflict where applicable, rate-limit, and service failures using the repository's eventual API conventions. Never expose credentials, task content, or stack traces in error details.
- Preserve the approved account-free experience, private workspace boundary, same-origin preference, and explicit refresh model. Do not introduce behavior excluded above.
- Use a database migration/schema mechanism selected during implementation planning. Use transactions for workspace/default-list/access-record initialization and for each task create. Ensure rollback leaves no partially initialized workspace or falsely confirmed task.
- Emit only sanitized operational signals. Do not log workspace titles, task titles/descriptions, device credentials, or recovery secrets.

## Acceptance criteria

- **AC-001 - Account-free initialization:** Given a person starts a task space, when creation succeeds, then the service has one private workspace, one default `Tasks` list, the initial time-zone identifier, and verifier records for the initial device and recovery secret, without account identity.
- **AC-002 - Time-zone display:** Given the first browser provides a valid IANA identifier, when workspace creation succeeds, then that identifier is stored and shown. Given detection fails or yields an invalid identifier, then creation remains blocked until the user explicitly selects a supported IANA zone; no server-local or UTC fallback is silent.
- **AC-003 - Credential handling:** Given workspace creation succeeds, then a 256-bit device credential is set only in a Secure, HttpOnly, SameSite=Lax, host-only `__Host-` cookie with `Path=/` and no `Domain`, and the 256-bit recovery secret is presented for separate safekeeping. The server persists SHA-256 verifiers only; raw secrets remain in page memory only and are absent from browser storage, logs, telemetry, and URLs.
- **AC-004 - First task:** Given an authorized workspace and a nonblank title, when the user saves a task, then the server confirms the task with the submitted optional description, date-only due date, and approved priority (when provided), associates it with the same workspace's default `Tasks` list, and the task appears in the current view.
- **AC-005 - Required title:** Given a missing, empty, or whitespace-only title, when the user saves, then actionable validation feedback appears and no task is persisted or shown as saved.
- **AC-006 - Workspace isolation:** Given a request without a valid device credential or an attempt to access a resource from another workspace, when task data is requested or changed, then the request fails explicitly without returning or disclosing the other workspace's data.
- **AC-007 - Confirmed outcomes and retries:** Given workspace initialization or task creation fails, then the UI does not claim success and retains safe task-entry input where applicable. Retrying the same workspace-create operation while its key and credential values remain in page memory, or retrying task creation with the same workspace-scoped key and request, does not create duplicates. Reuse with a different request fails. If workspace-creation secrets are lost with the page, the user receives an explicit unrecoverable outcome and must confirm before starting a separate workspace; no silent restart or cleanup occurs. Workspace initialization is atomic.
- **AC-008 - Refresh model:** Given a task was successfully saved, when the app opens, returns to its visible active view, or the user requests refresh, then the server-confirmed task is shown. A continuously open view makes no periodic or push request.
- **AC-009 - Accessible workflow:** Given keyboard-only or assistive-technology use, then workspace creation, time-zone confirmation, task entry, validation errors, save outcome, and refresh/stale state are understandable and operable without relying on color alone.

## Verification

Verification used isolated local PostgreSQL databases, the repository's pinned .NET and npm toolchains, and a real browser. The backend command requires `TASK_MANAGER_TEST_CONNECTION` to point to a dedicated test database; browser tests require `ConnectionStrings__TaskManager` to point to a separate E2E database. Follow the repository's [TDD guidance](../.github/instructions/test-driven-development.instructions.md) and stack-specific instructions.

| Criterion | Verification performed | Command and working directory | Result |
|---|---|---|---|
| AC-001 | PostgreSQL integration test verifies atomic initialization, default list, selected zone, verifier digests, fixed expiry, and absence of account schema. | `dotnet test TaskManager.sln --no-restore` from repository root with the isolated test connection. | Passed. |
| AC-002 | API exposes supported zones; browser walkthrough explicitly selects and displays `America/Los_Angeles`; component test simulates an unsupported detected zone and confirms explicit selection is required. | `dotnet test TaskManager.sln --no-restore`; `npm run test`; `npm run test:e2e` from `web`. | Passed. |
| AC-003 | API test checks cookie flags and persisted SHA-256 verifiers; browser test checks the recovery secret is absent from local/session storage and the device cookie is HttpOnly. | `dotnet test TaskManager.sln --no-restore`; `npm run test:e2e` from `web`. | Passed. |
| AC-004 | PostgreSQL API integration and real-browser flow verify server-confirmed task fields, default-list assignment, date-only value, and reload persistence. | `dotnet test TaskManager.sln --no-restore`; `npm run test:e2e` from `web`. | Passed. |
| AC-005 | API and browser tests reject whitespace-only titles; component test verifies failed saves preserve the draft and do not claim success. | `dotnet test TaskManager.sln --no-restore`; `npm run test`; `npm run test:e2e` from `web`. | Passed. |
| AC-006 | PostgreSQL API test checks unauthenticated reads and attempts a workspace query using another workspace's credential; no tasks are returned. | `dotnet test TaskManager.sln --no-restore` from repository root with the isolated test connection. | Passed. |
| AC-007 | API tests verify workspace/task idempotency and conflicts; component tests verify retry-safe task drafts and explicit recovery-loss confirmation. | `dotnet test TaskManager.sln --no-restore`; `npm run test` from `web`. | Passed. |
| AC-008 | Browser test verifies reload persistence, a focus-triggered refresh, and no extra task reads during observed idle intervals; component test covers manual refresh and stale-state messaging. | `npm run test:e2e`; `npm run test` from `web`. | Passed. |
| AC-009 | Playwright drives first-use, time-zone selection, recovery acknowledgement, task entry, and save with keyboard navigation only; component tests verify accessible labels and status/error roles. | `npm run test:e2e`; `npm run test` from `web`. | Passed. A screen-reader-specific review was not performed. |

Overall verification on 2026-11-12: all 4 PostgreSQL-backed API tests, all 6 frontend component tests, and all 3 Playwright browser tests passed. `npm run build`, `npm run lint`, and full `npm audit` also passed; the audit reported zero vulnerabilities.

## Showcase and completion

1. In an isolated local environment, open the app without an account and explicitly create a task space. Confirm the displayed time zone and separately record the recovery secret in a safe test-only location.
2. Create a task with a title plus optional description, date-only due date, and priority. Confirm the saved state only after the server response.
3. Reload or reopen the app and manually refresh; confirm the task appears with unchanged date and data. Submit a blank title and confirm actionable validation with no saved task.
4. Run automated isolation and failure-path checks with a second test workspace; verify no secrets or task content appear in logs, URLs, telemetry, or browser storage.

- **Approval evidence:** The requester approved this specification on 2026-11-12 and separately authorized implementation with the explicit "implement slice 1" request. Automated evidence is listed above; product acceptance remains with the requester.
- **Execution evidence:** Slice 1 is implemented and verified locally against isolated PostgreSQL databases. Hosting and pilot deployment were not part of this work.
- **Risks and remaining checks:** If page memory is lost before a workspace-create retry completes, its credentials cannot be recovered and an inaccessible workspace may remain. A screen-reader-specific review was not performed. Numeric abuse thresholds are deferred and must be documented and tested before pilot launch. Recovery response-loss handling belongs to the later recovery slice.
- **Next dependency/handoff:** Review the showcase flow before product acceptance. Slice 2 and later slices remain separate work and were not started.

## Resolved decisions and remaining gates

The requester selected the following implementation baseline on 2026-11-12:

- React 19 and TypeScript 5.9, supported Node.js LTS, Vite, Vitest, Testing Library, and Playwright.
- .NET 10 LTS / ASP.NET Core 10, EF Core 10 with Npgsql, and xUnit.
- PostgreSQL 18.
- Pin exact patch versions in project manifests during setup; confirm runtime/support compatibility when implementation begins.
- Local test environment only; hosting/provider and cost decisions remain deferred to pilot readiness.

The requester also selected browser-detected IANA zone with explicit user selection on failed detection; 256-bit random credentials with SHA-256 verifiers and the cookie/CSRF policy above; and same-page-memory retry with explicit recovery-loss handling. These decisions resolve Slice 1's previously open technical choices.

1. **Technical review and implementation authorization:** Specification approved on 2026-11-12; implementation authorized separately by the requester's explicit "implement slice 1" request.
2. **Exact versions and setup:** Pinned package versions, .NET SDK, local PostgreSQL, and isolated test databases are configured and verified for this local slice.
3. **Abuse thresholds:** Endpoint-specific rate limiting is approved, but numeric thresholds remain deferred. This does not block local Slice 1 verification; thresholds must be documented and tested before pilot launch.

## Implementation handoff

The bounded Slice 1 implementation is complete. Do not infer authorization for later slices from this request; use their approved specifications and obtain any required implementation authorization before starting them.
