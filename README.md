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
  - chat_id: "18e57cc1-aaa4-44b2-8160-ff185803a1f9"
    model: "unknown/unknown@unknown"
    date: "2026-11-13"
    change: "Documented PR 3 prevention rules, required review regressions, and the fresh issue 2 implementation handoff."
    ai_log: "ai-logs/2026/11/13/18e57cc1-aaa4-44b2-8160-ff185803a1f9/conversation.md"
  - chat_id: "18e57cc1-aaa4-44b2-8160-ff185803a1f9"
    model: "unknown/unknown@unknown"
    date: "2026-11-13"
    change: "Indexed review-driven generation safeguards and conditional application CI for the replacement Slice 1 implementation."
    ai_log: "ai-logs/2026/11/13/18e57cc1-aaa4-44b2-8160-ff185803a1f9/conversation.md"
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
---

# Welcome

This repository contains an approved product requirements specification for a task manager web app.

- [Task Manager Web App Requirements](task-manager-requirements.md)
- [Task Manager MVP Project Plan](task-manager-mvp-plan.md)
- [Task Manager Implementation Plan: Vertical Slices](task-manager-implementation-plan.md)
- [SL-001: First Use and First Task](specs/SL-001-first-use-first-task.spec.md) - Approved by the requester; replacement implementation separately authorized and assigned to Copilot in [issue 2](https://github.com/j0hnnymiller/20261109-aiasd-brown/issues/2). The superseded [PR 1](https://github.com/j0hnnymiller/20261109-aiasd-brown/pull/1) was closed without merging; implementation and acceptance remain incomplete.
- [Task Manager MVP Architecture Specification](task-manager-architecture.md) - Requester-approved hosted architecture with React/TypeScript, ASP.NET Core, and PostgreSQL; hosting and explicit open decisions remain unresolved.
- [React and TypeScript Instructions](.github/instructions/react-typescript.instructions.md) - Frontend guidance for typed, accessible UI and explicit server refresh behavior.
- [ASP.NET Core Instructions](.github/instructions/aspnet-core.instructions.md) - Backend guidance for modular boundaries, device authorization, workspace isolation, and explicit errors.
- [PostgreSQL Instructions](.github/instructions/postgresql.instructions.md) - Database guidance for workspace integrity, transactions, concurrency, and restore safeguards.
- [Architecture Design Skill](.github/skills/architecture-design/SKILL.md) - Requirements-grounded architecture workflow with semantically colored Mermaid diagrams and data flow diagrams for principal data exchanges.
- [Copilot Implementation Entry Point](.github/copilot-instructions.md) - Loads the applicable stack and slice guidance and requires failure/lifecycle regression evidence.
- [Slice Implementation Skill](.github/skills/implement-vertical-slice/SKILL.md) - Bounded implementation with acceptance checks, a failure/lifecycle matrix, and safe test infrastructure.
- [TDD Instructions](.github/instructions/test-driven-development.instructions.md), [Vertical-Slice Instructions](.github/instructions/vertical-slice-implementation.instructions.md), and [Specification Instructions](.github/instructions/implementation-specifications.instructions.md) - React-aware discovery, risk-driven regression checks, and explicit verification requirements.
- [Slice Validation Workflow](.github/workflows/slice-validation.yml) - Explicit planning-only result until application files exist; then requires API, frontend, named review-regression, and Chromium checks using disposable PostgreSQL databases without artifact uploads. The replacement implementation must supply the documented manifests, npm scripts (including `test:regressions`), and Chromium project.

The stack and TDD instructions require sanitized exception responses in all environments, no credential-bearing test captures, isolated test-owned servers, pending-save draft protection, actionable prerequisite retries, and missing-CSRF acquisition for returning devices. These safeguards address review feedback from the superseded Slice 1 PR; the unmerged application commits are not part of this baseline.

The requester also directed retirement of [PR 3](https://github.com/j0hnnymiller/20261109-aiasd-brown/pull/3) and a fresh implementation through [issue 2](https://github.com/j0hnnymiller/20261109-aiasd-brown/issues/2). Its six findings are mapped to [named regression checks](specs/SL-001-first-use-first-task.spec.md#failure-and-lifecycle-verification-matrix): trusted HTTPS, secret-safe cookie/mock assertion failures, effective E2E database selection despite inherited overrides, retriable access checks that preserve identity, and immutable payload/key retries after lost create responses. CI requires the focused regression script and deliberately exports both database variables during E2E. The replacement must supply actual tests, trusted certificate setup, and observed results; planning-only CI is not application acceptance.

Milestone 1 (Product Readiness) is accepted. The MVP has private individual task spaces with no required accounts. The requester owns product acceptance and release decisions; the plan records the approved pilot measures and deferred account-specific questions.

Follow-up decisions define task/list rules, 10-minute pairing, 90-day device access, revoke-and-rotate recovery, a shared configurable time zone, snapshot pilot measurement, and immediate live task deletion with 30-day backup retention. The requester owns best-effort pilot operations/support. Hosting, budget, workload, performance targets, recovery objectives, and numeric abuse limits remain deferred; implementation is not authorized by these decisions.
