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
    change: "Added a required regression-script gate and competing inherited database configuration during E2E."
    ai_log: "ai-logs/2026/11/13/18e57cc1-aaa4-44b2-8160-ff185803a1f9/conversation.md"
---

# Slice validation workflow provenance

[Slice validation](slice-validation.yml) reports the planning-only baseline explicitly. Once tracked application files exist, it requires the agreed solution, manifests, lockfile, API tests, and browser-test setup, then runs API tests against PostgreSQL 18, frontend tests/lint/build, and Chromium E2E tests against a separate database.

The replacement implementation must provide a Playwright project named `chromium`, fresh servers on dedicated test ports, explicit safe test configuration, and disabled sensitive captures. The workflow disables traces and uses a line reporter; it does not upload test artifacts. Test code and configuration must also avoid screenshots, video, storage-state exports, and credential-bearing assertion output.

The database password is a public, disposable CI fixture, not a deployment credential. The workflow supplies no repository secrets to pull-request code. Syntax checks and planning-only CI execution do not prove application tests pass; application execution must be verified with the replacement implementation.

The application must also supply `npm run test:regressions`: a fail-on-zero-tests suite of actual frontend and test-infrastructure regressions mapped in [SL-001](../../specs/SL-001-first-use-first-task.spec.md#failure-and-lifecycle-verification-matrix). Include safe assertion-diagnostic fixtures, competing database override/preflight coverage, bootstrap read retries, and immutable operation retry state. PostgreSQL-backed lost-response/idempotency and trusted HTTPS/browser checks remain mandatory in the API/E2E suites. Missing scripts or tests fail; do not replace checks with successful placeholders.

E2E deliberately inherits both the API-test override and the intended E2E connection. The runner must clear the override in the child API environment, validate the effective configuration before mutation, verify the API-test database remains untouched, and run only against the separate E2E database. Do not remove the competing variable from CI to hide a precedence defect. The replacement must provision trusted test certificates for its HTTPS entry point and proxy/API path without disabling certificate validation.
