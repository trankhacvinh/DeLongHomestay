# Codex working rules — ultra-lean existing-repo mode

## Goal
Treat context and tokens as scarce. Prefer repository evidence over generic framework knowledge.

## Default workflow
1. Search first with `rg`, `rg --files`, and `git status --short`.
2. Find one or two nearest analogous implementations.
3. Open only the files needed to trace the requested behavior.
4. Make the smallest coherent change that follows existing conventions.
5. Run the narrowest useful verification.
6. Review `git diff` and stop when the task is complete.

## Context budget
- Normal change: target an initial working set of 3–8 source files.
- Do not inspect the whole solution/repository before searching.
- Do not read framework documentation or generic framework skills merely because the repository uses that framework.
- Do not open `Program.cs`, startup/hosting files, manifests, DbContext, global configuration, or architecture docs unless the task touches them or search evidence makes them necessary.
- Do not reread files already understood unless the change requires it.
- Expand context only when concrete evidence requires it.

## Repository-first rule
Implementation priority:
1. Nearby code and tests.
2. Relevant project memory/domain rules.
3. Installed library/framework knowledge.
4. External/general framework guidance only as fallback.

Existing architecture and conventions win for normal maintenance. Do not introduce a new pattern when a nearby established pattern solves the task.

## Project memory
Persistent repository memory lives in `.agents/memory/`.
- Do not scan memory on every task.
- Consult `.agents/memory/INDEX.md` only when the task depends on durable business rules, architecture decisions, conventions, or long-running feature contracts that nearby code may not express cheaply or safely.
- After the index, open only the relevant memory file(s), normally 1–2.
- Do not update memory after routine edits.
- Update memory only when the user explicitly asks to remember/standardize something, or when a task establishes/changes a durable invariant or architectural decision that will affect future work.
- Memory is current project truth, not a chronological work log. Git remains the history.
- Never store secrets, credentials, tokens, personal data, or transient debugging notes in project memory.
- If memory conflicts with current code/tests, treat the conflict as evidence to investigate; do not silently trust stale memory.

Use `$project-memory` explicitly to add, revise, compact, or inspect repository memory.

## Skill loading
- Do not load a skill only because its technology name matches the repository.
- Load a skill when the requested task needs its specific workflow or domain knowledge.
- Generic ASP.NET/React/Vue/framework guidance is a fallback for architecture, upgrade, unfamiliar API, or missing repository evidence—not a default for routine edits.

## Changes
- Avoid unrelated refactors, formatting churn, dependency upgrades, and speculative cleanup.
- Preserve public behavior unless the task requests a behavior change.
- For bugs, fix the root cause at the narrowest correct boundary.
- For new behavior, prefer extending an existing feature slice/pattern.

## Verification
- Start targeted: affected test, test class/filter, project build, lint/typecheck for the touched package.
- Do not run the full solution/test suite after every small change.
- Broaden verification only for cross-cutting changes, shared infrastructure, release work, or when targeted checks expose uncertainty.

## Output
Keep progress and final summaries concise. Report changed files, verification run, and any unresolved risk. Do not narrate routine repository exploration.
