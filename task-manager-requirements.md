---
ai_generated: true
model: "unknown/unknown@unknown"
operator: "johnmillerATcodemag-com"
chat_id: "5d5e4cc3-6034-4b34-9193-397eba0fac7a"
prompt: |
  create a requirements specification for task manager web app. don't include any technical requirements
started: "2026-11-12T19:36:38-08:00"
ended: "2026-11-12T19:52:10-08:00"
task_durations:
  - task: "Drafting and validating product requirements"
    duration: "00:04:04"
  - task: "Applying requester decisions and approval"
    duration: "00:03:01"
total_duration: "00:07:05"
ai_log: "ai-logs/2026/11/12/5d5e4cc3-6034-4b34-9193-397eba0fac7a/conversation.md"
source: "user request"
revisions:
  - chat_id: "5d5e4cc3-6034-4b34-9193-397eba0fac7a"
    model: "unknown/unknown@unknown"
    operator: "johnmillerATcodemag-com"
    prompt: "help me complete milestone 1"
    started: "2026-11-12T20:04:13-08:00"
    ended: "2026-11-12T20:06:33-08:00"
    task_durations:
      - task: "Confirming and recording Product Readiness decisions"
        duration: "00:02:20"
    total_duration: "00:02:20"
    source: "user request and decision-form responses"
    change: "Aligned account expectations, ownership, and measures with accepted Milestone 1 decisions."
    ai_log: "ai-logs/2026/11/12/5d5e4cc3-6034-4b34-9193-397eba0fac7a/conversation.md"
  - chat_id: "7c5afae0-9dc2-465f-a19e-58cc8812f033"
    model: "OpenAI/gpt-6.1-sol@unknown"
    operator: "jMM-at-CODETraining"
    prompt: "help me resolve the open decisions"
    started: "2026-11-12T20:52:34-08:00"
    ended: "2026-11-12T20:55:39-08:00"
    task_durations:
      - task: "Recording confirmed product refinements"
        duration: "00:03:05"
    total_duration: "00:03:05"
    source: "user request and six decision-form responses; prior approved cross-device architecture"
    change: "Clarified task/list, access, calendar, deletion and pilot measurement behavior; added TM-10 through TM-13 without technical implementation requirements."
    ai_log: "ai-logs/2026/11/12/7c5afae0-9dc2-465f-a19e-58cc8812f033/conversation.md"
---

# Task Manager Web App Requirements

**Status:** Approved by requester on 2026-11-12
**Date:** 2026-11-12

## Objective and Context

Define the product behavior for a task manager web app that helps individuals capture, organize, and follow through on tasks. The requester has confirmed the product decisions and measurement approach below. No research, user interviews, or measured business baselines were supplied; the approved pilot will establish baselines.

## Problem

People need a dependable way to keep track of their work, decide what needs attention, and see what they have completed. A shared, reviewable task list can reduce reliance on memory and make progress easier to understand.

## Goals

- Let a user capture and maintain personal tasks.
- Help a user organize tasks and identify what needs attention.
- Make task state and progress clear to the user.

## Intended Users and Assumptions

- **Primary user:** An individual managing their own tasks.
- **Assumption:** A task has one owner: the individual managing it. Team collaboration is outside the initial scope.

## Product Decisions Provided by Requester

- The primary audience is individuals.
- Named lists are sufficient for organizing tasks in the initial scope.
- Due dates are calendar dates only, with no time of day.
- Reminders and recurring tasks should be considered for a later release; they are not included in the proposed first release.
- Each individual has a private task space; no account is required to use the MVP.
- Account-specific closure and data-removal questions are deferred outside the account-free MVP. TM-09 individual task removal remains included; subsequent task-space deletion and retention decisions are recorded below.
- The requester is the Product Owner and Release Decision Owner.

### Follow-up Decisions Confirmed on 2026-11-12

The requester confirmed these refinements while resolving the [architecture specification's open decisions](task-manager-architecture.md#13-open-decisions-and-approval). They supplement the original requirements approval without adding implementation choices:

- Every task belongs to a named list. Each new task space starts with a Tasks list. List names are trimmed, nonempty, and unique without regard to case within that task space. List removal is deferred; creating lists and moving tasks remain included.
- Priority is Low, Medium, High, or unset.
- Text search matches literal substrings in task titles only, without regard to case. Description-only matches are excluded. Search and selected filters combine with AND.
- Default ordering is newest creation first. Due-date order is soonest first with undated tasks last. Priority order is High, Medium, Low, then unset. Ties use newest creation, then stable task identity.
- The same private task space works across authorized devices without required accounts. Add a device using a single-use link/QR code valid for 10 minutes. Device access expires 90 days after issuance without automatic extension; individual device access can be revoked.
- Recovery uses a separately stored secret. Successful recovery restores the same task space, revokes previous devices and pending pairing links, and replaces the secret. The replacement secret must be saved separately. Losing all authorized devices and the secret makes recovery unavailable.
- Refresh current tasks on opening, returning to the app, or manual refresh. A continuously open view may remain stale; no periodic or live updates are included.
- Use one configurable task-space time zone, initialized from the first browser and shown to the user. Changes require explicit user action; other devices use that setting after refresh.
- Completion records the current completion time and its calendar date in the task-space time zone. Reopening clears the current completion record; completing again replaces it. Later time-zone changes do not rewrite the recorded completion calendar date.
- Confirmed task removal is immediate from live data, with no trash or undo. Protected backups may retain deleted content for up to 30 days; restoration must not reintroduce deleted tasks.
- Whole-task-space deletion is deferred outside the MVP. There is no automatic inactive-task-space deletion during the pilot; review the policy before broader availability.
- Pilot availability is best effort, without a numerical uptime commitment. The requester owns pilot operations and support; this does not imply continuous coverage.

## Scope

The proposed first release covers creating, viewing, organizing, finding, updating, completing, reopening, and removing personal tasks. It includes user-visible task status and calendar-date due awareness, private cross-device access, device pairing/revocation, recovery, and a configurable task-space time zone. Named lists are the agreed organization method for this scope.

Team workspaces, task assignment, comments, file attachments, reminders, recurring tasks, time tracking, calendar or email connections, and billing are not included in the proposed first release. Reminders and recurring tasks are candidates for a later release; their specific behaviors and priority remain to be defined.

Technical requirements and implementation choices are intentionally outside this specification.

## Product Requirements

The requirements and proposed first-release grouping below are approved as product scope; they do not establish delivery dates or authorize implementation.

| ID    | Priority               | Requirement                                                                                                | Acceptance criteria                                                                                                                                                                                                  |
| ----- | ---------------------- | ---------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| TM-01 | Proposed first release | A user can create a task with a title and may add a description, calendar due date, and priority.          | Given a user enters a title, when they save the task, it appears in their task list with the entered information. If the title is missing, the task is not saved and the user is told what is needed.                |
| TM-02 | Proposed first release | A user can view their tasks and distinguish tasks that are active from tasks that are complete.            | Given the user has active and completed tasks, when they view their tasks, each task's current state is visible and the two states can be viewed separately.                                                         |
| TM-03 | Proposed first release | A user can update a task's title, description, calendar due date, and priority.                            | Given an existing task, when the user changes one or more details and saves, the updated details are shown on that task.                                                                                             |
| TM-04 | Proposed first release | A user can mark an active task complete and reopen a completed task.                                       | Given an active task, when the user marks it complete, its state changes to complete. Given a completed task, when the user reopens it, its state changes to active.                                                 |
| TM-05 | Proposed first release | A user can group tasks into named lists and move a task between lists.                                     | Given two lists and a task, when the user assigns the task to another list, it appears under the selected list and no longer appears under its previous list.                                                        |
| TM-06 | Proposed first release | A user can find tasks by text and narrow the visible tasks by state, list, priority, and due-date status.  | Given tasks with different titles, states, lists, priorities, and due dates, when the user searches or applies a filter, only matching tasks are shown; clearing the search and filters restores the full task view. |
| TM-07 | Proposed first release | A user can order the task list by due date, priority, or creation date.                                    | Given multiple tasks with different values for a selected ordering, when the user selects that ordering, tasks appear in the corresponding order.                                                                    |
| TM-08 | Proposed first release | A user can identify incomplete tasks as upcoming, due today, or past due based on their calendar due date. | Given an incomplete task with a due date, it is shown as upcoming before that date, due today on that date, and past due after that date. A completed task is not presented as still needing completion.             |
| TM-09 | Proposed first release | A user can remove a task they no longer need.                                                              | Given a task, when the user confirms its removal, it no longer appears among their active or completed tasks. If the user cancels, the task remains unchanged.                                                       |
| TM-10 | Proposed first release | A user can access the same private task space from authorized devices without registering an account. | Given two authorized devices, when a task is saved on one and the other opens, returns to the app, or manually refreshes, the saved state is shown. A continuously open screen need not update automatically. |
| TM-11 | Proposed first release | A user can authorize another device and revoke individual device access. | A single-use pairing link/QR code works within 10 minutes; expired or reused links fail. Device access expires after 90 days without extension. Revoked access is rejected on the next protected operation. If the new device loses the pairing response, the original device can issue a fresh link. |
| TM-12 | Proposed first release | A user can recover their existing task space using a separately stored recovery secret. | Successful recovery provides access to the same tasks, revokes prior devices and pending pairing links, and presents a replacement secret. The old secret no longer works. No recovery is promised without an authorized device or valid secret. |
| TM-13 | Proposed first release | A user can view and explicitly change the task-space time zone used for calendar due status. | The initial setting comes from the first browser and is shown to the user. After refresh, authorized devices use the same setting even if their local zones differ. Completion dates already recorded are not rewritten when the setting changes. |

The follow-up decisions above are acceptance conditions for TM-01 through TM-09 as well: capture/edit accepts only approved priority values; tasks require a same-space list; blank/duplicate list names fail with feedback; search excludes description-only matches; ordering follows the stated null/tie rules; confirmed removal has no undo and does not reappear after restoration. TM-10 through TM-13 record the subsequently approved access and time-zone scope.

## Success Measures

The requester approved a 30-day limited pilot, participant walkthroughs, and a short survey to collect these baseline measures:

- Percentage of pilot participants who create at least one task within 7 days of first use.
- Percentage of tasks completed on or before their calendar due date, among tasks with due dates falling within the pilot period.
- Confidence in keeping track of personal commitments, rated from 1 (not confident) to 5 (very confident) using the same question at pilot start and end.

Numeric targets will be set after reviewing pilot baselines. They are excluded from release decisions until separately approved. The [MVP plan](task-manager-mvp-plan.md#success-measures) records the approved collection approach. No pilot start date is set.

**Approved snapshot interpretation:** At the participant walkthrough, the on-time denominator is the currently available tasks whose current due dates fall within the pilot period. Its numerator is those tasks that are currently complete and whose recorded completion calendar date is on or before their current due date. Reopened tasks are incomplete; deleted tasks are unavailable, not reconstructed. This does not reconstruct historical changes to due dates or completion state. Disclose this limitation; do not present the result as longitudinal tracking.

## Non-Goals

- Shared team planning, task ownership transfer, or assignment to other people.
- Automated reminders or notifications in the first release; these remain a later-release candidate.
- Recurring-task behavior in the first release; this remains a later-release candidate.
- Integrations with calendars, email, or other services.
- Technical requirements, architecture, or implementation design.
- List removal, whole-task-space deletion, task trash/undo, automatic pilot inactivity deletion, and historical completion/due-date analytics.

## Risks and Dependencies

- Reminder and recurrence behaviors have not been defined; treating them as later-release candidates avoids adding unconfirmed behavior to the first release.
- Baselines will be collected during the approved pilot; numeric targets remain deferred until baseline review.
- Adding account-related scope would require the Product Owner to resolve the deferred account closure and data-removal expectations.

## Open Questions

| Question                                                                                         | Decision owner            | Status |
| ------------------------------------------------------------------------------------------------ | ------------------------- | ------ |
| Which reminder behaviors and recurring-task patterns should be considered for a later release?   | Requester / Product Owner | Deferred outside MVP |
| What numeric targets should follow the pilot baselines?                                          | Requester / Product Owner | Deferred until baseline review; not a release gate |
| What account closure and account-level data-removal behavior should apply if accounts are added? | Requester / Product Owner | Deferred outside the account-free MVP |

## Review and Approval

**Approval:** The requester approved this specification on 2026-11-12 and subsequently confirmed the Milestone 1 product decisions, measurement approach, and explicit deferrals. The requester designated themselves Product Owner and Release Decision Owner and accepted Milestone 1 as complete. These approvals do not by themselves authorize implementation.

The requester subsequently approved the architecture and the follow-up decision-form responses recorded above on the same date. The new access/time-zone scope and clarified task behavior apply to MVP acceptance; dates, costs, and implementation authorization remain separate.
