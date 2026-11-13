---
ai_generated: true
model: "unknown/unknown@unknown"
operator: "johnmillerATcodemag-com"
chat_id: "5d5e4cc3-6034-4b34-9193-397eba0fac7a"
prompt: |
  create an implementation plan using vertical slices
started: "2026-11-12T21:15:03-08:00"
ended: "2026-11-12T21:18:55-08:00"
task_durations:
  - task: "Planning implementation as vertical slices"
    duration: "00:03:52"
total_duration: "00:03:52"
ai_log: "ai-logs/2026/11/12/5d5e4cc3-6034-4b34-9193-397eba0fac7a/conversation.md"
source: "user request; approved product requirements and architecture"
revisions:
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
    change: "Linked the blocked draft implementation specification for Slice 1."
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
    change: "Updated Slice 1 dependencies and linked specification status after the requester resolved implementation decisions."
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
    change: "Updated Slice 1 status to approved; implementation authorization remains ungranted."
  - chat_id: "57462e40-b5b1-4a82-b799-a1135603145a"
    model: "unknown/unknown@unknown"
    operator: "jMM-at-CODETraining"
    prompt: "create an implementation spec for slice 2"
    started: "2026-11-13T07:51:44.293-08:00"
    ended: "2026-11-13T08:00:04.874-08:00"
    task_durations:
      - task: "Drafting and validating the Slice 2 specification"
        duration: "00:08:20"
    total_duration: "00:08:20"
    ai_log: "ai-logs/2026/11/13/57462e40-b5b1-4a82-b799-a1135603145a/conversation.md"
    source: "user request"
    change: "Added a draft Slice 2 implementation specification and linked it from the plan."
---

# Task Manager Implementation Plan: Vertical Slices

**Status:** Proposed implementation sequence; Slice 1 specification is approved; no implementation authorization is implied.
**Date:** 2026-11-12

## Purpose and Basis

Break the approved task manager MVP into end-to-end, independently verifiable user outcomes. Each slice includes the interface, application behavior, persisted state where needed, and acceptance evidence. The plan follows the approved [product requirements](task-manager-requirements.md), [MVP plan](task-manager-mvp-plan.md), and [architecture](task-manager-architecture.md).

Milestone 1 is accepted. The requester is Product Owner, Release Decision Owner, and pilot operations/support owner. The MVP uses private task spaces without required accounts and includes TM-01 through TM-13. The requester selected React 19/TypeScript 5.9 with Node.js LTS/Vite, .NET 10 LTS/ASP.NET Core 10 with EF Core 10/Npgsql, PostgreSQL 18, Vitest/Testing Library, Playwright, and xUnit. Compatible patch versions remain to be pinned. Hosting, budget, workload, and several pilot operations decisions remain open in the architecture.

This document sequences implementation work; it does not authorize development, select unresolved tools, add product scope, or provide estimates. No developer capacity or release dates were supplied.

## Shared Slice Practices

- Deliver each slice through the React interface, ASP.NET Core application behavior, and PostgreSQL persistence or query behavior needed for that user outcome. Avoid standalone layer-only stories where a user-visible slice can be completed instead.
- Keep workspace scope derived from the verified device credential. Test that one private task space cannot read or change another.
- Include normal and failure outcomes in each slice's acceptance evidence. Preserve user input when an operation fails; only show a saved or completed state after server confirmation.
- Apply the architecture's version checks to concurrent mutations. Refresh current data on opening, returning to the app, and manual refresh; do not add polling or live updates.
- Never place task content, pairing tokens, device credentials, or recovery secrets in logs or telemetry.
- Add focused business-rule tests, persistence integration tests, and user-flow tests with each slice. Include accessibility checks for the workflows delivered.
- Complete each slice against its acceptance criteria before starting dependent slices. Slice estimates and calendar dates remain unassigned.

## Sequence at a Glance

| Order | Slice                                    | User outcome                                                                        | Requirements               | Depends on                                                |
| ----- | ---------------------------------------- | ----------------------------------------------------------------------------------- | -------------------------- | --------------------------------------------------------- |
| 1     | First use and first task                 | Start a private task space and save/view a first task.                              | TM-01, TM-10, TM-13        | Approved architecture; implementation prerequisites below |
| 2     | Edit and organize tasks                  | Update tasks, create named lists, and move tasks.                                   | TM-03, TM-05               | Slice 1                                                   |
| 3     | Pair and manage devices                  | Use the same task space on another device and manage device access.                 | TM-10, TM-11               | Slice 1                                                   |
| 4     | Recover task-space access                | Recover the existing task space and rotate the recovery secret.                     | TM-12                      | Slice 3; recovery-response decision                       |
| 5     | Find and order tasks                     | Search, filter, and sort the current task space.                                    | TM-06, TM-07               | Slices 1-2                                                |
| 6     | Complete tasks and understand due status | Complete/reopen tasks and interpret dates in the task-space time zone.              | TM-02, TM-04, TM-08, TM-13 | Slices 1-2                                                |
| 7     | Remove tasks safely                      | Remove a task immediately without allowing stale edits or restores to resurrect it. | TM-09                      | Slices 1 and 3; deletion/restore controls                 |
| Gate  | Pilot readiness                          | Verify the full MVP and operate the approved 30-day pilot.                          | TM-01 through TM-13        | Slices 1-7; hosting and operational decisions             |

## Slice 1: First Use and First Task

**Implementation specification:** [SL-001: First Use and First Task](specs/SL-001-first-use-first-task.spec.md) - Approved by the requester; implementation authorization remains separate.

**User story:** As an individual, I can start a private task space without registering an account and save and view my first task.

**Vertical scope:** React first-use and task-entry/view flow; ASP.NET Core workspace creation and task create/read behavior; PostgreSQL persistence of the workspace, its default Tasks list, task, task-space time zone, device credential verifier, and recovery-secret verifier.

**Acceptance criteria:**

- A person can create a private task space without providing an account identity.
- Creation makes a default Tasks list and initializes and displays the task-space time zone from the first browser.
- The user receives a device credential and separately displayed recovery secret; the secret is not automatically stored in browser storage.
- A task with a title can be saved to the default list and is visible after the approved refresh flow.
- Missing title is rejected with clear feedback; optional description, date-only due date, and approved priority values are accepted.
- An unauthorized or failed request does not show a successful save or expose another task space.

**Validation:** First-use-to-first-task walkthrough, reload/open refresh, title validation, and workspace-isolation tests.

**Dependencies and unresolved items:** Stack versions and test tooling are selected; pin compatible patch versions and provision isolated local PostgreSQL/test secrets before implementation. Hosting is not required for local slice validation but must be selected before the pilot.

## Slice 2: Edit and Organize Tasks

**User story:** As an individual, I can maintain task details and organize every task in named lists.

**Implementation specification:** [SL-002: Edit and Organize Tasks](specs/SL-002-edit-and-organize-tasks.spec.md) - Draft for requester review; implementation depends on accepted Slice 1.

**Requirements:** TM-03, TM-05, plus approved list and priority rules.

**Vertical scope:** React task editing and list management; ASP.NET Core update/list-create/move behavior; PostgreSQL scoped persistence and constraints.

**Acceptance criteria:**

- A user can edit title, description, date-only due date, and priority; invalid or missing required values receive actionable feedback.
- Every task belongs to a named list in its own task space; there is no unfiled state.
- A user can create a list and move a task to another list in the same task space.
- List names are trimmed, nonempty, and unique without regard to case within the task space; rejected names leave existing data unchanged.
- Moving a task does not expose or alter another task space's lists or tasks.

**Validation:** Create/edit/move walkthroughs; empty, whitespace-only, and case-insensitive duplicate list-name cases; cross-space move rejection; persistence after refresh.

**Dependency:** Slice 1.

## Slice 3: Pair and Manage Devices

**User story:** As an individual, I can authorize another device to use my existing private task space and revoke a device I no longer trust.

**Requirements:** TM-10, TM-11, and approved refresh behavior.

**Vertical scope:** React pairing, device-list, and revoke flows; ASP.NET Core grant issue/redemption, credential validation, expiry, revocation, and scoped reads; PostgreSQL access and grant state.

**Acceptance criteria:**

- An authorized device can issue a single-use pairing link or QR code that expires after 10 minutes.
- Redemption by the new device authorizes the same task space without creating another one.
- Expired, replayed, revoked, or unauthorized grants are rejected; a lost redemption response is recovered by issuing a fresh grant from the still-authorized device.
- Device access expires 90 days after issuance without automatic extension; the user can revoke an individual device.
- The new device reads current state on opening, returning to the app, or manual refresh. No periodic polling or live update occurs.
- Unauthorized, expired, or revoked credentials do not expose cached private data as current.

**Validation:** Two-device walkthrough, expiry boundary, one-success-only replay, revoke-on-next-protected-operation, refresh triggers, cross-space isolation, and lost-response recovery.

**Dependencies:** Slice 1; resolve remaining numeric abuse thresholds before pilot, not as invented constants in this slice.

## Slice 4: Recover Task-Space Access

**User story:** As an individual who has lost device access, I can use my separately stored recovery secret to regain the same task space.

**Requirements:** TM-12 and approved revoke-and-rotate behavior.

**Vertical scope:** React recovery and replacement-secret experience; ASP.NET Core recovery proof, rate limiting, atomic device/grant revocation, and secret rotation; PostgreSQL credential state changes.

**Acceptance criteria:**

- A valid recovery secret restores access to the existing task space, not a new task space.
- Successful recovery revokes prior devices and pending grants, invalidates the old secret, and provides a replacement secret for separate safekeeping.
- The recovered device receives a fixed 90-day credential; previous credentials cannot be reused.
- Invalid or unavailable recovery does not disclose whether another task space exists.
- Recovery attempts that exceed approved limits fail without revealing secrets.

**Validation:** Successful recovery, prior-device/grant invalidation, old-secret rejection, invalid-secret response, concurrent recovery attempts, and replacement-secret display tests.

**Blocking decision before implementation:** The architecture leaves lost recovery-response behavior open. Specify and approve whether recovery has any safe replay/reconciliation mechanism or whether a lost response leaves the user without usable access before marking this slice ready.

**Dependency:** Slice 3 establishes the access and credential lifecycle.

## Slice 5: Find and Order Tasks

**User story:** As an individual, I can find relevant tasks and control how the current task list is presented.

**Requirements:** TM-06, TM-07, and approved search/filter/order rules.

**Vertical scope:** React search, filter, and ordering controls; ASP.NET Core scoped query behavior; PostgreSQL query behavior for the selected criteria.

**Acceptance criteria:**

- Search matches case-insensitive literal substrings in task titles only; description-only matches are excluded.
- Selected state, list, priority, and due-date filters combine with AND; removing search/filters restores the full task view.
- Due-date order is soonest first with undated tasks last; priority order is High, Medium, Low, unset; default order is newest creation first.
- Ties follow newest creation, then stable task identity.
- Results remain scoped to the authorized task space and refresh on the approved triggers.

**Validation:** Mixed-case and literal-character search, description-only exclusion, each filter and combinations, clearing, null ordering, tie order, and cross-space tests.

**Dependencies:** Slices 1-2.

## Slice 6: Complete Tasks and Understand Due Status

**User story:** As an individual, I can track completion and tell whether an incomplete task is upcoming, due today, or past due in my task space's time zone.

**Requirements:** TM-02, TM-04, TM-08, TM-13, and approved completion snapshot rules.

**Vertical scope:** React active/completed views, completion controls, due-status labels, and time-zone settings; ASP.NET Core completion/reopen and time-zone behavior; PostgreSQL current completion evidence and workspace setting.

**Acceptance criteria:**

- Active and completed tasks are distinguishable; users can complete and reopen tasks.
- Completing stores the current completion instant and calendar date in the task-space time zone; reopening clears the current completion record; completing again replaces it.
- Incomplete due tasks are shown as upcoming, due today, or past due; completed tasks are not presented as needing completion.
- The time zone is visible, can be changed explicitly, and is shared by authorized devices after refresh; later changes do not rewrite recorded completion dates.
- UI date transitions update labels locally without implying remote refresh or live synchronization.

**Validation:** Completion/reopen/recomplete sequence, due-date boundaries, differing device local zones, explicit zone change, refresh behavior, and approved pilot-snapshot calculation.

**Dependencies:** Slice 1 for task space/time zone; Slice 2 for task fields and priorities.

## Slice 7: Remove Tasks Safely

**User story:** As an individual, I can remove a task and trust that another device's stale edit or a database restore will not bring it back.

**Requirements:** TM-09 and approved immediate-removal, no-undo, backup-retention, and restore rules.

**Vertical scope:** React confirmation/cancel and removed-task state; ASP.NET Core version-checked removal and restore safeguards; PostgreSQL live task deletion plus protected deletion-ledger recording.

**Acceptance criteria:**

- Confirmed removal removes the live task immediately and exposes no trash or undo; cancellation leaves it unchanged.
- Removal is workspace-scoped and checks the task's current version.
- A stale edit from another device cannot restore or overwrite the removed task.
- The deletion ledger records only approved identifiers/timestamp and is independent of database backup restoration.
- Before restored data is available, the latest deletion ledger is reapplied; if unavailable or not applied, restored data remains unavailable.
- Deleted task content may exist in protected backups for no longer than 30 days and is not reintroduced after restoration.

**Validation:** Confirm/cancel, concurrent remove/edit, cross-space remove attempt, ledger write failure, restore with and without a deleted task, expired backup, and ledger-unavailable fail-closed scenarios.

**Dependencies:** Slice 1; access-state reconciliation and restore handling from architecture must be specified and approved before pilot.

## Pilot Readiness Gate

This gate is not a feature slice. It closes the delivery plan after Slices 1-7.

- Resolve hosting, budget, supported stack versions/libraries, workload expectations, numeric abuse limits, recovery objectives, and operational procedures as listed in the approved architecture.
- Verify deployment and rollback, backup retention, deletion-ledger restoration, and access-state reconciliation before exposing pilot data.
- Run an acceptance walkthrough for TM-01 through TM-13 with representative individual users.
- Start the approved 30-day limited pilot only after the Release Decision Owner accepts the evidence and operational dependencies.
- Collect first-task creation within 7 days, the approved on-time completion snapshot, and start/end confidence ratings. Report baselines without claiming unapproved targets.
- Review pilot feedback and baselines before setting numeric targets or expanding availability.

## Dependency and Readiness Notes

- Slices 2, 3, 5, and 6 can proceed after Slice 1, subject to their stated shared prerequisites. Slice 4 depends on access lifecycle completion and its open recovery-response decision. Slice 7 depends on versioned task mutation and an approved restore/deletion-ledger procedure.
- Exact framework, library, migration, and test-tool versions are open. Record those in slice implementation specifications after checking supported versions and the approved architecture.
- Hosting and cost are pilot blockers, not prerequisites for local validation of every early slice.
- No effort estimates or calendar dates are assigned. The team should estimate each slice after capacity and the remaining technical decisions are known.
- Reminders, recurring tasks, team collaboration, list deletion, whole-task-space deletion, offline editing, and live synchronization are outside these slices.

## Definition of Done for Each Slice

- The user story works end-to-end through the interface and authoritative application behavior, with persisted results where applicable.
- Every listed acceptance criterion has recorded passing evidence; relevant failure paths and workspace isolation are covered.
- Concurrent writes, refresh behavior, and error states follow the approved architecture where the slice touches them.
- The interface preserves drafts on failure, avoids claiming an unconfirmed save, and meets the repository's accessibility guidance.
- No secrets or task content appear in logs or telemetry.
- User-facing behavior and any changed open decisions are reflected in the requirements, architecture, and release evidence as appropriate.
