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
---

# Welcome

This repository contains an approved product requirements specification for a task manager web app.

- [Task Manager Web App Requirements](task-manager-requirements.md)
- [Task Manager MVP Project Plan](task-manager-mvp-plan.md)
- [Task Manager MVP Architecture Specification](task-manager-architecture.md) - Requester-approved hosted architecture covering cross-device access, device pairing and recovery, and refresh-on-use behavior; stack selection and explicit open decisions remain unresolved.
- [Architecture Design Skill](.github/skills/architecture-design/SKILL.md) - Requirements-grounded architecture workflow with semantically colored Mermaid diagrams and data flow diagrams for principal data exchanges.

Milestone 1 (Product Readiness) is accepted. The MVP has private individual task spaces with no required accounts. The requester owns product acceptance and release decisions; the plan records the approved pilot measures and deferred account-specific questions.

Follow-up decisions define task/list rules, 10-minute pairing, 90-day device access, revoke-and-rotate recovery, a shared configurable time zone, snapshot pilot measurement, and immediate live task deletion with 30-day backup retention. The requester owns best-effort pilot operations/support. Stack, hosting, budget, workload, performance targets, recovery objectives, and numeric abuse limits remain deferred; implementation is not authorized by these decisions.
