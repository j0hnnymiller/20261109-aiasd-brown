---
name: implement-vertical-slice
description: "Implement one bounded vertical slice from an approved specification or plan. Use when a slice has defined acceptance criteria and needs repository-grounded implementation, focused verification, and an evidence-based handoff."
argument-hint: "[slice specification path]"
ai_generated: true
model: "unknown/unknown@unknown"
operator: "johnmillerATcodemag-com"
chat_id: "0ee42cac-b369-4e34-9193-397eba0fac7a"
prompt: |
  Continue broader config review.
  Approved skill changes: all of them.
started: "2026-10-17T20:39:34.0421109-07:00"
ended: "2026-10-17T20:45:14.3269450-07:00"
task_durations:
  - task: "Skill authoring and integration"
    duration: "00:05:40"
total_duration: "00:05:40"
ai_log: "ai-logs/2026/10/17/0ee42cac-b369-4e34-9193-397eba0fac7a/conversation.md"
source: "AIASD/AI-Assisted-Software-Development-Course/.github/skills/implement-vertical-slice/SKILL.md"
revisions:
  - chat_id: "18e57cc1-aaa4-44b2-8160-ff185803a1f9"
    model: "unknown/unknown@unknown"
    date: "2026-11-13"
    change: "Required named PR 3 regression mappings and observed failure-path evidence rather than happy-path proxies."
    ai_log: "ai-logs/2026/11/13/18e57cc1-aaa4-44b2-8160-ff185803a1f9/conversation.md"
  - chat_id: "18e57cc1-aaa4-44b2-8160-ff185803a1f9"
    model: "unknown/unknown@unknown"
    date: "2026-11-13"
    change: "Repaired the implementation instruction link and required risk-matrix, safe infrastructure, and regression evidence."
    ai_log: "ai-logs/2026/11/13/18e57cc1-aaa4-44b2-8160-ff185803a1f9/conversation.md"
  - chat_id: "0ee42cac-b369-4e34-9193-397eba0fac7a"
    model: "unknown/unknown@unknown"
    date: "2026-10-18"
    change: "Linked the bounded implementation workflow to the repository TDD instruction and required behavior-focused RED-GREEN-REFACTOR verification."
    ai_log: "ai-logs/2026/10/17/0ee42cac-b369-4e34-9193-397eba0fac7a/conversation.md"
---

# Implement One Vertical Slice

Use the user-supplied specification as the authority for one slice. Follow the repository's [vertical-slice implementation guidance](../../instructions/vertical-slice-implementation.instructions.md), [test-driven development guidance](../../instructions/test-driven-development.instructions.md), and any domain-specific instructions that apply.

## Procedure

1. Resolve the supplied specification or plan. If it is missing or ambiguous, ask which committed source defines the slice.
2. Read the specification, prerequisites, acceptance criteria, applicable instructions, and relevant implementation and test patterns. Identify conflicts, missing decisions, dependencies, exclusions, and verification commands. Apply TDD where the applicable repository guidance requires it.
   Build a compact failure/lifecycle matrix covering applicable dependency failures, delayed responses, pending-save edits, returning-browser credentials, actionable retries, sensitive captures, and test isolation. Name a planned check or justify exclusion for every scenario; reuse the specification's matrix when present.
   For credential-bearing/bootstrap/create flows, include trusted HTTPS on every relevant hop, safe assertion failure diagnostics, effective child-server configuration under inherited overrides, transient access-check retry without identity replacement, and commit-then-response-loss retry with an immutable payload/key. Map each applicable named check to a real test case, test layer, and CI command before coding.
3. Present a bounded implementation plan with scope, acceptance criteria, and verification. Wait for approval before editing.
4. Implement only the approved slice in small, reviewable changes. For new or changed behavior, write or update a focused test first, confirm it fails for the expected behavior gap when feasible, then implement the minimum change to pass and refactor with tests green. Do not break existing behavior merely to manufacture a failing test. Preserve unrelated user work and follow existing patterns.
5. Run the specified focused tests and other required checks. If a check fails, report the evidence and repair only within the approved scope before rerunning it.
   Before mutating tests, verify isolated databases, fresh test-owned servers, and secret-safe capture configuration. Exercise transport/cookie behavior below mocked UI boundaries. Run the risk-matrix checks as well as happy paths; do not substitute a coverage percentage for missing behavior tests.
   Run the configured review-regression suite as well as API/browser checks. Observe trusted-certificate transport, both database variables exported together, safe deliberately failing assertion fixtures, working bootstrap-read retry, and exactly-once persistence after response loss. Matchers must receive only safe projections/booleans. Inspect effective child configuration without logging secrets; do not infer isolation from preflight's intended variable alone.
6. Compare the implementation against each acceptance criterion and inspect the diff. Prepare user-facing showcase steps only when requested or required by the specification.
7. Summarize changed files, verification commands and results, acceptance coverage, unresolved risks, and the next dependency in the plan. Include each required regression name, its actual test case/command, observed outcome, and any unverified check; aggregate pass counts are not sufficient.

## Boundaries

- Do not silently expand scope or begin a dependent slice.
- Do not commit, push, merge, or discard user work without explicit authorization.
- Do not claim checks passed unless they were run and their results were observed.
- Treat a worktree as Git-change isolation, not as a security boundary.
