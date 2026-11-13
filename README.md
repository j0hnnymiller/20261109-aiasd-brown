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
---

# Welcome

This repository contains an approved product requirements specification for a task manager web app.

- [Task Manager Web App Requirements](task-manager-requirements.md)
- [Task Manager MVP Project Plan](task-manager-mvp-plan.md)
- [Task Manager MVP Architecture Specification](task-manager-architecture.md) - Requester-approved hosted architecture with React/TypeScript, ASP.NET Core, and PostgreSQL; hosting and explicit open decisions remain unresolved.
- [React and TypeScript Instructions](.github/instructions/react-typescript.instructions.md) - Frontend guidance for typed, accessible UI and explicit server refresh behavior.
- [ASP.NET Core Instructions](.github/instructions/aspnet-core.instructions.md) - Backend guidance for modular boundaries, device authorization, workspace isolation, and explicit errors.
- [PostgreSQL Instructions](.github/instructions/postgresql.instructions.md) - Database guidance for workspace integrity, transactions, concurrency, and restore safeguards.
- [Architecture Design Skill](.github/skills/architecture-design/SKILL.md) - Requirements-grounded architecture workflow with semantically colored Mermaid diagrams and data flow diagrams for principal data exchanges.

Milestone 1 (Product Readiness) is accepted. The MVP has private individual task spaces with no required accounts. The requester owns product acceptance and release decisions; the plan records the approved pilot measures and deferred account-specific questions.

Follow-up decisions define task/list rules, 10-minute pairing, 90-day device access, revoke-and-rotate recovery, a shared configurable time zone, snapshot pilot measurement, and immediate live task deletion with 30-day backup retention. The requester owns best-effort pilot operations/support. Hosting, budget, workload, performance targets, recovery objectives, and numeric abuse limits remain deferred; implementation is not authorized by these decisions.
