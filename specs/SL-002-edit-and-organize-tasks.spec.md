---
ai_generated: true
model: "unknown/unknown@unknown"
operator: "jMM-at-CODETraining"
chat_id: "57462e40-b5b1-4a82-b799-a1135603145a"
prompt: "create an implementation spec for slice 2"
started: "2026-11-13T07:51:44.293-08:00"
ended: "2026-11-13T08:00:04.874-08:00"
task_durations:
  - task: "Drafting and validating the Slice 2 specification"
    duration: "00:08:20"
total_duration: "00:08:20"
ai_log: "ai-logs/2026/11/13/57462e40-b5b1-4a82-b799-a1135603145a/conversation.md"
source: "user request; approved task-manager requirements, architecture, and vertical-slice plan"
---

# SL-002: Edit and Organize Tasks

- **Status:** Draft
- **Owner/reviewer:** Requester as Product Owner and technical reviewer
- **Implementation authorization:** Not granted by this specification. Review/approval and completion of prerequisite Slice 1 are required before implementation.
- **Sources:** [Implementation plan, Slice 2](../task-manager-implementation-plan.md#slice-2-edit-and-organize-tasks); [product requirements](../task-manager-requirements.md); [approved architecture](../task-manager-architecture.md); [prerequisite SL-001](./SL-001-first-use-first-task.spec.md)

## Goal

As an individual, I can update a task's details, create named lists, and move a task between lists in my private task space, with server-confirmed changes that remain correct after refresh.

## Scope and exclusions

**In scope**

- React task-detail editing and list organization workflows.
- ASP.NET Core workspace-scoped task-update, list-read/list-create, and task-move operations.
- PostgreSQL persistence and constraints required for trimmed, nonempty, case-insensitively unique list names and same-workspace task/list relationships.
- Version-checked task mutations, explicit validation/conflict/failure states, safe retries, and accessible status and form feedback.
- Existing task-space refresh behavior for the task and list data used by these workflows.

**Out of scope**

- Task creation, completion/reopening, deletion, search/filter/order, and due-status presentation.
- List rename/removal, task-space deletion, account registration, pairing, recovery, or device management.
- Changing the task-space time zone, due-date semantics, priority values, or refresh cadence.
- Polling, push updates, offline writes, automatic task-content persistence, or a new identity/session flow.
- Hosting, pilot operations, unapproved numeric performance/availability targets, and analytics.

## Context and dependencies

- **Existing implementation and conventions:** The current `main` baseline contains planning, instructions, and CI guidance but no tracked application source, test projects, solution, or frontend package manifest. The ignored/generated `bin`, `obj`, and `dist` folders are not implementation evidence. Do not assume routes, source paths, database schema, or test names already exist.
- **Prerequisite:** [SL-001](./SL-001-first-use-first-task.spec.md) must supply an accepted task space, default `Tasks` list, task read/create behavior, verified-device authorization, CSRF/Origin enforcement, and a working test foundation. Its implementation and acceptance are separate from this specification.
- **Product and technical basis:** TM-03 and TM-05, approved list/priority rules, same-workspace isolation, and atomic version checks are defined in the [requirements](../task-manager-requirements.md) and [architecture](../task-manager-architecture.md). Follow the repository's [React/TypeScript](../.github/instructions/react-typescript.instructions.md), [ASP.NET Core](../.github/instructions/aspnet-core.instructions.md), [PostgreSQL](../.github/instructions/postgresql.instructions.md), [TDD](../.github/instructions/test-driven-development.instructions.md), and [vertical-slice](../.github/instructions/vertical-slice-implementation.instructions.md) guidance.
- **Environment:** Use only the selected React/TypeScript, ASP.NET Core/.NET 10, EF Core/Npgsql, and PostgreSQL stack. The [Slice Validation workflow](../.github/workflows/slice-validation.yml) defines CI commands and separate API/E2E databases. Its commands cannot run on the current planning-only baseline; implement and commit the complete Slice 1 test foundation first.
- **Assumptions:** None beyond approved product and architecture decisions.
- **Open questions/blockers:** No additional product decision is required for this slice. During implementation, reconcile the contracts below with the accepted Slice 1 API/schema rather than inventing incompatible parallel conventions; update this draft for review if that prerequisite exposes a conflict.

## Behavior and contracts

### Task editing

- Read and edit only tasks authorized by the verified device credential. A client-supplied workspace ID is never authority.
- Support updating one or more of `title`, `description`, `dueDate`, and `priority`. An omitted field remains unchanged; `description`, `dueDate`, and `priority` may be explicitly cleared with `null`. A supplied title must remain nonblank after validation. Priority accepts only `Low`, `Medium`, `High`, or unset.
- Treat `dueDate` as a date-only value (`YYYY-MM-DD`), never as a timestamp or a value to convert through the browser's local zone. Do not trim or otherwise normalize task title/description beyond the approved validation rules.
- Use a conditional task version (`ETag`/`If-Match` or the already-approved Slice 1 equivalent) for each update. On success, atomically persist the changed fields and next version, then return the canonical task and version. Do not change its list, creation data, or completion state as a side effect.
- On a stale version, reject the mutation without overwriting newer data. Keep the submitted draft distinct from server-confirmed data and provide a way to review current data and resolve the conflict; never silently merge or discard the draft.

### Lists and moving tasks

- Read and display only the authenticated task space's lists, including its existing default `Tasks` list. Every task remains associated with exactly one list in that same task space.
- On list creation, trim leading/trailing whitespace and store the trimmed name. Reject an empty result. Names must be unique without regard to case within one task space; the rule applies to concurrent requests as well as sequential requests. A rejected or conflicting create leaves existing records unchanged.
- Bind each list-create operation key to its authenticated task space, immutable submitted payload, and result. Commit the list and idempotency result atomically. Replaying the same key and payload returns the original outcome; reusing the key with a different payload fails explicitly and creates nothing. The browser keeps an uncertain operation's key and immutable payload separate from any editable draft. A changed draft is a new operation with a new key after the uncertain operation is retried or reconciled.
- Move a task by submitting its task ID, destination list ID, and expected task version. Validate that both records belong to the authenticated task space and commit the list assignment and version increment atomically. Return the canonical moved task. A missing or foreign destination is unavailable and must not reveal whether it exists.
- Enforce required task/list association and same-workspace ownership in application logic and database constraints (including a composite relationship where appropriate). Do not add list deletion or an unfiled state.

### Request, error, and compatibility behavior

- Use the established same-origin HTTPS/JSON boundary and shared typed API client. Exact route names and serialization conventions must align with the completed Slice 1 implementation; this specification defines operations and payload semantics, not unverified source paths.
- Use the verified device credential to derive workspace scope. Protected writes retain the approved Origin and CSRF checks; a returning device with a missing CSRF token obtains one through the shared mutation boundary before sending the write.
- Return actionable field validation for invalid title, date, priority, or list name; use an explicit conflict for duplicate list names and the architecture's conditional-write conflict behavior for stale task versions. Unauthorized requests fail explicitly. Missing and cross-workspace task/list resources have the same unavailable response.
- A transient read/service failure is not an authentication failure. Preserve the current view and user draft, show an enabled retry of the failed operation, and do not create a workspace, replace a credential, or claim a save. Only show "Saved" after a confirmed or safely reconciled server outcome.
- Sanitize errors, logs, telemetry, and test diagnostics. Do not log task titles/descriptions, cookies, credentials, CSRF values, or raw request bodies. Render user text as text, not HTML.
- Preserve the approved refresh triggers (open, return to visible active view, or manual refresh); do not add polling or push. Older responses must not replace newer confirmed task/list state. Do not change existing first-task behavior or the default Tasks list.

### Persistence, migration, and operations

- Persist list creation, task edits, and moves using atomic transactions appropriate to each mutation. Concurrent duplicate list creation must result in one list, not two.
- Add or update reviewed migrations only where the accepted Slice 1 schema lacks the required constraints. Validate uniqueness and same-workspace relationships against isolated test data before enabling the constraints; do not silently rename or delete user data to make a migration pass.
- Keep any migration compatible with application deployment/rollback and do not remove existing task fields. Do not add a deletion ledger, backup policy, indexes, or operational targets unrelated to these access patterns.
- Apply existing task-write rate limiting without inventing a numeric limit. No additional metrics or task-content telemetry are required.

## Acceptance criteria

- **AC-001 - Edit task details:** Given an authorized task, when the user changes one or more editable fields and saves with the current version, then the server returns and persists the canonical values and the next version. Omitted fields remain unchanged; optional values can be cleared. Refresh shows the confirmed values without shifting the date-only due date.
- **AC-002 - Validate edits:** Given a blank/whitespace-only title, unsupported priority, or invalid date, when the user saves, then actionable field feedback appears, no invalid state is persisted, and the user's draft remains available.
- **AC-003 - Keep every task in a named list:** Given a task space, when its tasks and lists are read, then the default `Tasks` list and all current tasks are shown with their existing list associations; no task is unfiled or associated with another workspace's list.
- **AC-004 - Create lists safely:** Given an authorized user enters a name, when the trimmed name is nonempty and unique ignoring case in that task space, then exactly one list is created and appears after refresh. Empty/whitespace-only names and names duplicating an existing trimmed name with different casing are rejected without changing existing data. Concurrent attempts cannot create duplicate names.
- **AC-005 - Move within a task space:** Given a task and two lists in the same task space, when the user moves the task with its current version, then the task is associated with the destination, absent from the source-list view, present in the destination-list view after refresh, and its other fields are unchanged.
- **AC-006 - Isolate workspaces:** Given missing/invalid device authorization or task/list identifiers from another workspace, when an edit, list read/create, or move is attempted, then no cross-workspace data or existence signal is returned and no record is changed.
- **AC-007 - Reject stale writes:** Given another request has advanced a task version, when an edit or move submits the stale version, then the server returns an explicit conflict, preserves the current stored record, and the UI retains the unsaved draft for review.
- **AC-008 - Resolve list-create response loss:** Given list creation commits but its response is lost, when the user activates retry, then the same immutable name/key returns the original outcome and only one list exists. Reusing the key with another name is rejected; after resolving the original operation, a deliberately edited draft can be submitted as a distinct operation with a new key.
- **AC-009 - Preserve async state and offer retries:** Given delayed/out-of-order reads, edits during a pending save, or a transient task/list load failure, then older data cannot overwrite newer confirmed state, pending edits are disabled or remain unsaved after the earlier response, and an enabled retry recovers without page reload or identity replacement.
- **AC-010 - Accessible workflow and safe failures:** Given keyboard-only or assistive-technology use, task editing, list selection/creation/move, validation, save/conflict state, and retry are operable and announced without color-only cues. Expected and unexpected failures do not expose credentials, task content, request headers, or stack traces.

## Verification

The test names below are required planned checks, not existing tests or observed results. Place them in the appropriate API, frontend, test-infrastructure, and browser suites after inspecting the accepted Slice 1 implementation. The commands are the repository's documented CI commands; all application checks remain unavailable on the current planning-only baseline.

| Criterion | Planned test/check | Command and working directory | Expected result |
|---|---|---|---|
| AC-001, AC-002 | PostgreSQL-backed API tests for partial edits, null clearing, invalid fields, date-only round trip, and atomic version increment. | `dotnet test TaskManager.sln --configuration Release` from repository root; require the workflow's isolated `TASK_MANAGER_TEST_CONNECTION`. | Valid updates persist exactly; invalid updates do not alter the row; date is unchanged across browser zones. |
| AC-003, AC-004 | `ListCreateEnforcesTrimmedWorkspaceScopedUniqueness` and `ListAndTaskReadReturnsRequiredAssociations` using PostgreSQL, including simultaneous duplicate creates and the default Tasks list. | `dotnet test TaskManager.sln --configuration Release` from repository root. | One canonical trimmed list exists per case-insensitive name/workspace; every task has a same-workspace list. |
| AC-005, AC-006 | `TaskMoveIsAtomicAndWorkspaceScoped` with two task spaces, same-space and foreign destinations, and persisted before/after fields. | `dotnet test TaskManager.sln --configuration Release` from repository root. | Same-space move increments version and preserves other fields; foreign/missing IDs return the same unavailable outcome and change nothing. |
| AC-007 | `TaskUpdateAndMoveRejectStaleVersions` with competing PostgreSQL writes and a browser conflict/draft check. | `dotnet test TaskManager.sln --configuration Release` from repository root; `npm run test:e2e -- --project=chromium --trace=off --reporter=line` from `web`. | Only the current version commits; stale requests cannot overwrite data and the user's draft remains reviewable. |
| AC-008 | `LostListCreateResponseRetriesImmutableOperation` with a real API/PostgreSQL commit-then-response-loss, same-key retry, changed-payload reuse, then a distinct edited-draft submission. | `npm run test:regressions` from `web` for client key/payload state; `npm run test:e2e -- --project=chromium --trace=off --reporter=line` from `web` for real transport and persistence. | Exactly one original list exists; changed payload does not reuse the key; a distinct valid edited draft can create a separate list. |
| AC-009 | `PendingTaskSaveCannotEraseNewerDraft`, `OutOfOrderRefreshCannotReplaceNewerTaskListState`, and `TaskListLoadFailureCanBeRetriedWithoutIdentityReplacement` using deferred responses and an actual retry control. | `npm run test` and `npm run test:regressions` from `web`; browser retry check via `npm run test:e2e -- --project=chromium --trace=off --reporter=line`. | Submitted data alone is confirmed; newer draft/state survives; retry succeeds without reload, new workspace, or credential replacement. |
| AC-006, AC-010 | `ExpiredDeviceCredentialCannotMutateTasksOrCreateWorkspace` and inherited `ReturningDeviceAcquiresMissingCsrfBeforeMutation` through the real API client. | `npm run test:e2e -- --project=chromium --trace=off --reporter=line` from `web`, using trusted HTTPS and the isolated E2E API/database. | Expired access is rejected without mutation or identity replacement; a valid device missing CSRF obtains it before the write. |
| AC-010 | `CredentialTransportUsesTrustedHttps`, `AssertionFailuresDoNotExposeCredentialArguments`, `UnexpectedFailureIsSanitizedInEveryEnvironment`, `CredentialFlowsDoNotRetainSensitiveCaptures`, `BrowserTestsOwnTheirServerAndDatabase`, and `E2eIgnoresInheritedApiTestOverride`; reuse the named Slice 1 infrastructure checks where applicable. | `dotnet test TaskManager.sln --configuration Release` from repository root; `npm run test:regressions` from `web`; `npm run test:e2e -- --project=chromium --trace=off --reporter=line` from `web`. | Trusted HTTPS is used through entry point and proxy/API; safe synthetic failure diagnostics contain no sentinel; Development and Production errors are sanitized; no secret-bearing captures are retained; E2E uses only its own database even with both DB variables inherited. |
| AC-001 through AC-010 | Frontend type/build/lint and keyboard/accessibility checks over the complete workflow. | `npm run test`, `npm run lint`, and `npm run build` from `web`; manual keyboard and assistive-technology walkthrough. | All commands pass; labels, focus, errors, save/conflict announcements, and retry are perceivable and operable. |

These commands and outcomes are planned; no application test has been run or passed for this specification.

### Failure and lifecycle verification matrix

| Planned check | Scenario and observable outcome | Applicability |
|---|---|---|
| `TaskListOperationFailurePreservesDraftAndCanBeRetried` | Fail the API or database dependency after user input. Keep the previous view and draft, show an enabled retry, then recover without a reload or false saved state. | Applicable; frontend and API integration. |
| `OutOfOrderRefreshCannotReplaceNewerTaskListState` | Delay an earlier list/task read until after a newer confirmed mutation/read. The older response cannot replace newer state. | Applicable; controlled frontend response plus browser confirmation. |
| `PendingTaskSaveCannotEraseNewerDraft` | Hold an edit response, attempt to change the draft, and then release it. Either the controls are disabled while pending or newer input remains unsaved; only the submitted snapshot is confirmed. | Applicable; controlled frontend test. |
| `ExpiredDeviceCredentialCannotMutateTasksOrCreateWorkspace` | On a returning browser, expire the device credential independently of CSRF state. A protected write is rejected, no workspace or replacement credential is created, and no data changes. | Applicable; real API/browser check. |
| `ReturningDeviceAcquiresMissingCsrfBeforeMutation` | Keep a valid device credential but remove the CSRF cookie. Obtain a fresh CSRF token through the real client before writing; on token-fetch failure retain the draft and do not mutate. | Applicable; inherited Slice 1 security contract, exercised for task/list writes. |
| `TaskListLoadFailureCanBeRetriedWithoutIdentityReplacement` | Make an initial task/list read fail transiently, activate the accessible retry control, and recover. Separately verify confirmed 401 does not become a transient retry or new-workspace flow. | Applicable; real client and browser interaction. |
| `LostListCreateResponseRetriesImmutableOperation` | Commit a list then lose the response; retry the identical payload/key, reject changed-payload reuse, and submit an edited draft only with a distinct key. Verify PostgreSQL contains exactly one original and the intended later list. | Applicable; real API and PostgreSQL required. |
| `CredentialFlowsDoNotRetainSensitiveCaptures` and `AssertionFailuresDoNotExposeCredentialArguments` | Disable traces, screenshots, videos, HAR, storage-state exports, and retained credential-bearing diagnostics. Deliberately fail safe synthetic-sentinel assertions in an isolated child runner and inspect output in memory only. | Applicable; browser/API tests use credentials. Do not retain test artifacts containing cookies, task text, or secrets. |
| `UnexpectedFailureIsSanitizedInEveryEnvironment` | Inject request-time persistence failures in Development and Production. Return generic, non-sensitive errors; neither client responses nor diagnostics contain task content, cookies, headers, or stack traces. | Applicable; API integration. Inspect diagnostic safety without writing credential-bearing captures. |
| `BrowserTestsOwnTheirServerAndDatabase` and `E2eIgnoresInheritedApiTestOverride` | Run while an ordinary development server is listening, with both database variables exported. Start a fresh API on dedicated ports; verify effective child configuration targets only E2E DB and the API-test DB remains unchanged. Occupied ports or unsafe configuration fail before mutation. | Applicable; CI E2E. Do not reuse development services or databases. |
| `CredentialTransportUsesTrustedHttps` | Verify browser-to-entry-point and proxy-to-API HTTPS, matching URLs/Origins/cookie policy, and enabled certificate validation. | Applicable; real credentialed browser/API transport, not component mocks or localhost secure-context behavior. |

Do not treat UI mocks as evidence for cookie, CSRF, HTTPS, workspace isolation, idempotency, transaction, or PostgreSQL constraints. Configure captures as disabled rather than relying only on `--trace=off`. For inherited checks, inspect their implementation and execution in the Slice 1 test foundation; reuse only checks that actually cover this slice.

## Showcase and completion

1. In a disposable, isolated test task space created by the accepted Slice 1 flow, confirm `Tasks` and a task are visible.
2. Create a list with surrounding whitespace and confirm the displayed/stored name is trimmed. Attempt a case-only duplicate and observe actionable rejection with no extra list.
3. Edit a task's title, description, date-only due date, and priority; save, refresh, and confirm the values persist without a date shift. Clear optional fields and confirm they remain unset.
4. Move the task to the new list, refresh, and confirm it is absent from the old list and present in the destination with other fields unchanged.
5. In isolated automated checks, exercise stale-version rejection, transient-load retry, pending draft behavior, workspace isolation, and commit-then-response-loss retry.

- **Completion evidence:** Every AC maps to a passing named test or documented manual check; PostgreSQL constraints and migration are verified; all applicable failure/lifecycle checks above have observed results; links and generated metadata validate.
- **Execution evidence:** Not run; specification only.
- **Risks:** Slice 2 cannot start until Slice 1's implementation and test infrastructure are accepted. Route and schema choices must be reconciled with those actual artifacts; the current branch has no tracked application code.
- **Next dependency/handoff:** Requester reviews and approves this draft. After approval and completion of Slice 1, invoke the [implement-vertical-slice skill](../.github/skills/implement-vertical-slice/SKILL.md) with this specification. Slice 3 and other work remain outside this handoff.
