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
---

# SL-001: First Use and First Task

- **Status:** Blocked (draft; not implementation-ready)
- **Owner/reviewer:** Requester as Product Owner and Release Decision Owner; technical implementation owner unassigned
- **Implementation authorization:** Not granted
- **Sources:** [Implementation plan, Slice 1](../task-manager-implementation-plan.md#slice-1-first-use-and-first-task); [product requirements](../task-manager-requirements.md); [approved architecture](../task-manager-architecture.md)

## Table of Contents

- [Goal](#goal)
- [Scope and exclusions](#scope-and-exclusions)
- [Context and dependencies](#context-and-dependencies)
- [Behavior and contracts](#behavior-and-contracts)
- [Acceptance criteria](#acceptance-criteria)
- [Verification](#verification)
- [Showcase and completion](#showcase-and-completion)
- [Open decisions and blockers](#open-decisions-and-blockers)
- [Implementation handoff](#implementation-handoff)

## Goal

As an individual, I can start a private task space without registering an account, save a first task to its default Tasks list, and see the server-confirmed task after refreshing or reopening the app.

## Scope and exclusions

**In scope**

- React/TypeScript first-use, task-entry, and task-view experience.
- ASP.NET Core workspace creation, initial device credential and recovery-secret issuance, task creation, and workspace-scoped task reads.
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
- **Environment:** Local development and isolated test data are sufficient for this slice. Hosting is not a prerequisite. Exact SDK/runtime versions, libraries, test tooling, database setup, and safe local secret configuration must be selected before implementation.
- **Assumptions:** None beyond the approved requirements, architecture, and Slice 1 plan.
- **Open questions/blockers:** See [Open decisions and blockers](#open-decisions-and-blockers). Do not resolve them by silently choosing defaults.

## Behavior and contracts

### First use and workspace creation

1. Require an explicit user action to create a private task space. Do not request or require an account identity.
2. Accept the first browser's time-zone identifier and store a validated IANA identifier for the workspace. Display the selected workspace time zone to the user. Do not substitute the server or UTC time zone silently.
3. Create the workspace, its required default list named `Tasks`, its initial device credential verifier, and its recovery-secret verifier atomically. A failed operation must not leave a partially initialized workspace.
4. Generate opaque, high-entropy credentials with a cryptographically secure random source. Store only server-side verifiers, never raw credentials. The initial device credential expires 90 days after issuance without sliding extension.
5. Deliver the device credential only through a server-managed Secure, HttpOnly cookie with the approved SameSite and CSRF protections. Present the recovery secret for separate safekeeping; do not automatically retain it in browser storage. Never put either secret in a URL, log, telemetry, or error detail.
6. Do not report successful workspace creation until persistence is confirmed. Handle ambiguous responses and retries without silently creating duplicate task spaces or making secrets appear recoverable when they are not.

### First task and read

- Allow creation of a task with a required nonblank title; accept optional description, date-only due date, and priority of Low, Medium, High, or unset.
- Assign the task to the workspace's default `Tasks` list on the server. Do not accept a client-selected workspace or arbitrary list as authority.
- Treat due date as a calendar date, not a timestamp. Preserve the approved title/description values; do not add unapproved trimming, markup, or normalization behavior.
- Return or read server-confirmed task data scoped to the workspace authenticated by the device credential. The browser must not supply a workspace identifier as proof of access.
- Refresh current data when the app opens, returns to its visible active view, or the user requests refresh. Do not poll or push updates. On refresh failure, retain any prior view with an explicit stale/error indication; on save failure, preserve the draft and do not show a saved state.
- Make create retries safe against ambiguous responses so a retry cannot create a duplicate task. The specific idempotency mechanism remains an implementation decision to resolve before coding.

### Errors, compatibility, and operations

- Reject blank titles with actionable field feedback and do not persist a task.
- Fail unauthenticated or invalidly authorized reads/writes explicitly. Do not return data or disclose whether another workspace's resource exists.
- Distinguish validation, authorization, unavailable-resource, conflict where applicable, rate-limit, and service failures using the repository's eventual API conventions. Never expose credentials, task content, or stack traces in error details.
- Preserve the approved account-free experience, private workspace boundary, same-origin preference, and explicit refresh model. Do not introduce behavior excluded above.
- Use a database migration/schema mechanism selected during implementation planning. Use transactions for workspace/default-list/access-record initialization and for each task create. Ensure rollback leaves no partially initialized workspace or falsely confirmed task.
- Emit only sanitized operational signals. Do not log workspace titles, task titles/descriptions, device credentials, or recovery secrets.

## Acceptance criteria

- **AC-001 - Account-free initialization:** Given a person starts a task space, when creation succeeds, then the service has one private workspace, one default `Tasks` list, the initial time-zone identifier, and verifier records for the initial device and recovery secret, without account identity.
- **AC-002 - Time-zone display:** Given the first browser provides a valid IANA time-zone identifier, when workspace creation succeeds, then that identifier is stored and shown as the workspace time zone. Missing/invalid input behavior must be decided before implementation; it is not implicitly a server-local or UTC fallback.
- **AC-003 - Credential handling:** Given workspace creation succeeds, then the browser receives the device credential only as a Secure, HttpOnly cookie, the recovery secret is presented for separate safekeeping, and neither raw secret is stored in browser storage, persisted as a verifier value, logged, or exposed in a URL.
- **AC-004 - First task:** Given an authorized workspace and a nonblank title, when the user saves a task, then the server confirms the task with the submitted optional description, date-only due date, and approved priority (when provided), associates it with the same workspace's default `Tasks` list, and the task appears in the current view.
- **AC-005 - Required title:** Given a missing, empty, or whitespace-only title, when the user saves, then actionable validation feedback appears and no task is persisted or shown as saved.
- **AC-006 - Workspace isolation:** Given a request without a valid device credential or an attempt to access a resource from another workspace, when task data is requested or changed, then the request fails explicitly without returning or disclosing the other workspace's data.
- **AC-007 - Confirmed outcomes and retries:** Given workspace initialization or task creation fails, when the user sees the result, then the UI does not claim success and retains safe task-entry input where applicable. Retrying an ambiguous task-create response does not create a duplicate. Workspace initialization is atomic.
- **AC-008 - Refresh model:** Given a task was successfully saved, when the app opens, returns to its visible active view, or the user requests refresh, then the server-confirmed task is shown. A continuously open view makes no periodic or push request.
- **AC-009 - Accessible workflow:** Given keyboard-only or assistive-technology use, then workspace creation, time-zone confirmation, task entry, validation errors, save outcome, and refresh/stale state are understandable and operable without relying on color alone.

## Verification

There is no application or test project in the repository yet, so executable commands and test paths cannot be specified honestly. Add focused unit/business-rule tests, PostgreSQL integration tests, API/user-flow checks, and accessibility checks after selecting the toolchain. Follow the repository's [TDD guidance](../.github/instructions/test-driven-development.instructions.md) and stack-specific instructions.

| Criterion | Planned test/check | Command and working directory | Expected result |
|---|---|---|---|
| AC-001 | Proposed workspace-creation integration test: inspect persisted workspace, default list, time zone, and credential verifiers; verify absence of account identity fields. | Not established; no test project. Run the selected focused integration-test command from the repository root after implementation planning. | Exactly one complete initialization is persisted; no partial records or account identity. |
| AC-002 | Proposed time-zone integration/UI tests for a valid IANA value and the explicitly decided missing/invalid cases. | Not established; no test project. Repository root after toolchain selection. | Stored and displayed zone matches the approved behavior; no unapproved fallback. |
| AC-003 | Proposed API/browser integration test for cookie flags and recovery-secret presentation; inspect persistence and captured logs/URLs/browser storage. | Not established; no test project. Repository root after toolchain selection. | Cookie and verifier requirements hold; raw secrets are absent from prohibited surfaces. |
| AC-004 | Proposed task-create and scoped-read API integration test using a real isolated PostgreSQL test database; verify title and optional fields, date-only round trip, default-list association, and subsequent read. | Not established; no test project. Repository root after toolchain selection. | Server confirms and returns the persisted task under `Tasks` with no date shift. |
| AC-005 | Proposed business-rule/API tests for absent, empty, and whitespace-only titles. | Not established; no test project. Repository root after toolchain selection. | Validation is actionable; persisted task count does not increase. |
| AC-006 | Proposed authorization integration test using two independently created workspaces and absent, invalid, and cross-workspace credentials/identifiers. | Not established; no test project. Repository root after toolchain selection. | No cross-workspace data or existence signal is returned. |
| AC-007 | Proposed transaction-failure, task-save-failure, and ambiguous-response/retry integration tests; verify draft preservation and no duplicate creates. | Not established; no test project. Repository root after toolchain selection. | No false success, partial initialization, dropped safe draft, or duplicate task. |
| AC-008 | Proposed browser user-flow test: save, reload/open, return to visible view, manual refresh, and observe request activity during an idle interval. | Not established; no browser test project. Repository root after toolchain selection. | Saved task reappears on each approved trigger; no polling or push request occurs. |
| AC-009 | Proposed keyboard-only and assistive-technology review of first use, time-zone confirmation, task form, validation, and status announcements. | Manual; exact browser/tooling TBD. | All workflow actions and outcomes are perceivable and operable without color-only cues. |

These are planned checks, not execution evidence. Do not substitute mocks for PostgreSQL-backed workspace isolation, transaction, date-only persistence, or retry checks.

## Showcase and completion

1. In an isolated local environment, open the app without an account and explicitly create a task space. Confirm the displayed time zone and separately record the recovery secret in a safe test-only location.
2. Create a task with a title plus optional description, date-only due date, and priority. Confirm the saved state only after the server response.
3. Reload or reopen the app and manually refresh; confirm the task appears with unchanged date and data. Submit a blank title and confirm actionable validation with no saved task.
4. Run automated isolation and failure-path checks with a second test workspace; verify no secrets or task content appear in logs, URLs, telemetry, or browser storage.

- **Completion evidence:** Diff limited to this specification and approved discovery/index updates; all ACs mapped to observed checks; test commands and results recorded after toolchain selection; unresolved pilot blockers remain visible.
- **Execution evidence:** Not run; specification only.
- **Risks:** Secret-response loss or retries could strand a newly created workspace or duplicate resources unless the open idempotency and recovery-response decisions are settled. Missing/invalid browser time-zone behavior and exact credential/cookie controls also block implementation readiness.
- **Next dependency/handoff:** Resolve the blockers below, obtain technical review, then request approval for this specification. Only after approval and separate implementation authorization, invoke the [implement-vertical-slice skill](../.github/skills/implement-vertical-slice/SKILL.md) with this specification path. Slice 2 and later slices remain out of scope.

## Open decisions and blockers

1. **Supported toolchain and libraries:** Select compatible supported React/TypeScript, .NET/ASP.NET Core, and PostgreSQL versions and the frontend build, persistence, migration, and test tools. Keep hosting and provider decisions separate unless needed for local test execution.
2. **Initial time-zone failure behavior:** Decide what to do when the browser cannot supply a valid IANA identifier. The architecture requires initialization from the first browser and display to the user but does not approve a fallback.
3. **Credential and cookie policy:** Specify safe credential/secret formats and verifier strategy, cookie lifetime/attributes (including SameSite), and CSRF/origin enforcement details consistent with the approved architecture. Do not weaken the 90-day fixed device lifetime or expose secrets to JavaScript.
4. **Create-response loss and retry contract:** Define how workspace creation and task creation handle a committed operation whose response is lost, including how secrets can be delivered safely and how retries avoid duplicates. Do not assume replaying secrets or generating a second workspace is safe.
5. **Numeric abuse limits:** Endpoint-specific rate limiting is approved, but numeric thresholds remain deferred. This does not block isolated local verification; thresholds must be documented and tested before pilot launch.
6. **Technical owner and review:** Assign implementation ownership and review the API, schema, and access-policy details before marking this spec Approved.

## Implementation handoff

This specification defines one bounded slice and does not authorize implementation. Once blockers 1-4 and 6 are resolved and the requester approves the specification, invoke the repository's generic vertical-slice workflow with `specs/SL-001-first-use-first-task.spec.md`. The implementation workflow must propose its own bounded plan and wait for separate approval before editing code.
