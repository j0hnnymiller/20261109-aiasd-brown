---
ai_generated: true
model: "unknown/unknown@unknown"
operator: "jMM-at-CODETraining"
chat_id: "e9752f6f-5119-4a18-af86-2254b152ab42"
prompt: "create instruction files for the tech stack"
started: "2026-11-12T21:03:43-08:00"
ended: "2026-11-12T21:08:49-08:00"
task_durations:
  - task: "Creating stack instruction files and integration records"
    duration: "00:05:06"
total_duration: "00:05:06"
ai_log: "ai-logs/2026/11/12/e9752f6f-5119-4a18-af86-2254b152ab42/conversation.md"
source: "user request"
name: "aspnet-core"
description: "Use when building or changing the task-manager C# and ASP.NET Core backend, including API endpoints, authorization, persistence boundaries, and errors."
applyTo: "**/*.cs"
version: "1.0.0"
author: "jMM-at-CODETraining"
tags: ["aspnet-core", "csharp", "backend", "task-manager"]
owner: "Development Team"
reviewedDate: "2026-11-12"
nextReview: "2027-02-12"
---

# ASP.NET Core Instructions

## Overview

Apply these rules to the task-manager backend. The approved stack is C# and ASP.NET Core in a single modular web application/API. Endpoint style, exact versions, data-access, migration, and test libraries remain unselected. Follow the [architecture specification](../../task-manager-architecture.md); these instructions do not authorize implementation.

## Table of Contents

- [Application Structure and Contracts](#application-structure-and-contracts)
- [Access and Security](#access-and-security)
- [Persistence and Concurrency](#persistence-and-concurrency)
- [Operations and Verification](#operations-and-verification)
- [Verification Checklist](#verification-checklist)
- [Summary](#summary)

## Application Structure and Contracts

- Organize backend code around the approved modular-monolith boundaries and end-to-end feature slices: workspace access, tasks, lists, and persistence.
- Keep endpoint handlers, validation, application behavior, and persistence responsibilities clear. Reuse shared authorization, validation, persistence, and error-handling mechanisms rather than duplicating them.
- Use typed request/response contracts and explicit validation. Return consistent, non-sensitive errors for validation failures, unauthenticated access, unavailable resources, version conflicts, rate limits, and service failures.
- Choose controllers or minimal APIs only after implementation planning; do not introduce a new architectural pattern or library by assumption.
- Keep workspace scope derived from the verified device credential. A route, query parameter, or request body workspace ID is never authorization.

## Access and Security

- Authenticate each device using its distinct, opaque credential. Store only a secure verifier server-side; never log or return raw credentials after issuance.
- Enforce expiry and revocation on every protected request. Use the approved Secure, HttpOnly cookie policy, appropriate SameSite behavior, CSRF defenses, and origin checks for state-changing requests.
- Make pairing grants short-lived and single-use; verify issuer authorization and consume a grant atomically with device issuance. Do not consume grants on link fetch.
- Rate-limit workspace creation, pairing, recovery, and task read/write endpoints independently; numeric limits are open decisions and must be documented and tested before pilot launch.
- Never log task content, device credentials, recovery secrets, pairing payloads, or sensitive request bodies/URLs. Do not expose stack traces, secret values, or cross-workspace existence in client errors.
- Keep deployment secrets outside source control. Use explicit environment configuration and least-privilege database access.

## Persistence and Concurrency

- Treat PostgreSQL as the authoritative store. Use workspace-scoped reads and writes and enforce same-workspace task/list ownership in application logic and database constraints.
- Commit state changes and version increments atomically. Use conditional version checks for updates and removals; report stale writes as explicit conflicts rather than overwriting newer data.
- Make creates and other non-idempotent operations safe against ambiguous responses and retries using stable operation identifiers or an approved equivalent.
- Ensure workspace creation and its required default Tasks list succeed or fail together.
- Keep date-only due dates distinct from UTC instants. Compute calendar behavior using the configured workspace time zone, not a server or browser local default.
- Keep the deletion ledger independent from the restored application database. Do not expose restored data until ledger reapplication and access-state reconciliation succeed; define consistency and failure behavior before implementing restore.

## Operations and Verification

- Provide health/readiness behavior without disclosing secrets or task data. Emit sanitized structured logs and useful operational signals.
- Use transactions and migrations appropriate to the selected provider and library. Make deployment and rollback compatible with schema evolution; never assume application rollback reverses a database migration.
- Add unit and integration coverage for validation, workspace isolation, authorization expiry/revocation, pairing replay/expiry, recovery rotation, version conflicts, retry behavior, and explicit error responses.
- Test restore and deletion-ledger failure paths before pilot readiness; keep the service unavailable if required safeguards cannot be applied.

## Verification Checklist

- [ ] Protected requests enforce credential validity and workspace scoping on every request.
- [ ] Concurrent stale updates cannot overwrite or recreate newer/deleted records.
- [ ] Pairing, recovery, and task failures return explicit safe outcomes.
- [ ] Logs and errors exclude credentials, recovery material, and task content.
- [ ] Database writes preserve all required workspace/list/task invariants.
- [ ] Restore cannot serve data before deletion-ledger and access-state checks complete.
- [ ] No unselected library, hosting provider, or numeric operational target is assumed.

## Summary

Keep ASP.NET Core as a secure modular monolith: enforce server-side device authorization and workspace isolation, commit versioned mutations atomically, and make all failure states explicit and testable.

**Related Instructions**: [React/TypeScript](react-typescript.instructions.md), [PostgreSQL](postgresql.instructions.md)
**Document Version**: 1.0.0
**Last Updated**: 2026-11-12
**Maintainer**: Development Team
