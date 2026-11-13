---
ai_generated: true
model: "unknown/unknown@unknown"
operator: "jMM-at-CODETraining"
chat_id: "18e57cc1-aaa4-44b2-8160-ff185803a1f9"
prompt: |
  make the recommended changes.
  close pr 1 and delete the branch.
  create an issue to implement slice 1
  assign the issue to copilot
started: "2026-11-13T00:42:48-08:00"
ended: "2026-11-13T00:46:55-08:00"
task_durations:
  - task: "Designing and validating review-driven generation safeguards"
    duration: "00:04:07"
total_duration: "00:04:07"
ai_log: "ai-logs/2026/11/13/18e57cc1-aaa4-44b2-8160-ff185803a1f9/conversation.md"
source: "user; PR 1 review comments"
revisions:
  - chat_id: "18e57cc1-aaa4-44b2-8160-ff185803a1f9"
    model: "unknown/unknown@unknown"
    date: "2026-11-13"
    change: "Linked PR 3 transport, assertion-diagnostic, configuration-precedence, access-retry, and immutable-operation safeguards."
    ai_log: "ai-logs/2026/11/13/18e57cc1-aaa4-44b2-8160-ff185803a1f9/conversation.md"
---

# Repository implementation entry point

- Implement only the explicitly authorized slice from its approved [specification](../specs). Read the [implementation workflow](skills/implement-vertical-slice/SKILL.md) and [specification guidance](instructions/implementation-specifications.instructions.md).
- Load the applicable [ASP.NET Core](instructions/aspnet-core.instructions.md), [React/TypeScript](instructions/react-typescript.instructions.md), [PostgreSQL](instructions/postgresql.instructions.md), [TDD](instructions/test-driven-development.instructions.md), and [vertical-slice](instructions/vertical-slice-implementation.instructions.md) instructions explicitly; do not assume file matching alone loaded them.
- Before coding, map applicable failure/lifecycle scenarios to named checks: dependency failure, delayed responses, edits during saves, independently expired browser cookies, actionable retries, sensitive captures, and test-service isolation. Justify exclusions.
- Preserve newer drafts, acquire missing CSRF state at the mutation boundary, sanitize unexpected errors in every environment, and run mutating tests only against isolated databases and fresh test-owned servers. Never retain credentials through browser captures, logs, or CI attachments.
- Apply the scoped rules for trusted HTTPS, secret-safe assertion diagnostics, effective child-process database configuration, recoverable access-check states, and immutable retry payloads. Map them to named regression checks before coding; mocks and success-only checks cannot establish these guarantees.
- Run the repository's configured checks and provide observed acceptance/regression evidence. Missing checks are not passing checks. Keep implementation, demonstration, and pilot readiness distinct.
- Follow [AI provenance and logging requirements](instructions/ai-assisted-output.instructions.md). Do not invent model identities or validation results.
