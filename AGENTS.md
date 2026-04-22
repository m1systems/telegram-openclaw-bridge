# AGENTS.md

## Repository Context

This repository is a Telegram → OpenClaw bridge.

Key constraints:
- Runs as a Linux systemd user service
- Uses external configuration (~/.config/telegram-openclaw)
- Must remain lightweight and dependency-minimal

## Purpose
This file defines the mandatory operating rules for AI agents working in this repository.

These rules are deterministic and take precedence over defaults, assumptions, memory, or general instructions.

---

## Required Startup Sequence

Before making any change, the agent MUST perform these steps in order:

```bash
git status
git checkout main
git pull origin main
git checkout -b feature/<task-name>
```

### Startup Rules
- If `git status` is not clean, STOP and report the dirty state
- If `git checkout main` fails, STOP and report
- If `git pull origin main` fails, STOP and report
- Never begin work from the current branch without first returning to `main`
- Never branch from an existing feature branch unless explicitly instructed to do so

---

## Scope of Work

The agent MUST:

- Make only the requested changes
- Keep changes minimal and focused
- Avoid unrelated refactors
- Avoid modifying unrelated files
- Preserve existing style and structure unless explicitly asked to change it

The agent MUST NOT:

- Expand the task beyond the request
- Introduce speculative cleanup
- Add new dependencies unless explicitly required
- Change CI, repo settings, secrets, or workflows unless explicitly requested

---

## Branching Rules

### Allowed
- Create a fresh branch for each task using:
  - `feature/<task-name>`
  - `fix/<task-name>`
  - `chore/<task-name>`

### Prohibited
- Reusing an existing feature branch
- Creating a new branch from another feature branch
- Pushing directly to `main`
- Merging pull requests
- Deleting branches
- Force pushing
- Rewriting history

If the intended branch name already exists, STOP and report instead of improvising.

---

## Commit Rules

Commits must be clear, concise, and directly related to the requested work.

### Preferred commit style
- `feat: add Telegram image handling`
- `fix: handle null parser result`
- `chore: update agent workflow contract`

### Commit process
```bash
git add <specific files>
git commit -m "<type>: <clear summary>"
```

### Commit guidance
- Prefer targeted `git add <file>` over `git add .` when practical
- Do not bundle unrelated changes into one commit
- Do not create empty commits
- Do not amend commits unless explicitly instructed

---

## Push Rules

Only push the newly created work branch.

```bash
git push -u origin <branch-name>
```

The agent MUST NOT:
- Push directly to `main`
- Push tags
- Push with `--force`
- Push any branch other than the task branch

---

## Validation Rules

Before declaring a task complete, the agent SHOULD do any applicable local validation, such as:

- Build the project
- Run relevant tests
- Run linting or formatting checks already used by the repo

If validation is skipped because no suitable command is known or available, the agent must say so explicitly.

---

## Error Handling

If any of the following occur, STOP and report clearly:

- Working tree is not clean
- Cannot checkout `main`
- Cannot pull latest `main`
- Branch creation fails
- Branch already exists
- Merge conflicts occur
- Build or test failures occur
- Authentication or push fails
- Any unexpected git state appears

Do not attempt clever recovery unless explicitly instructed.

---

## Required Final Report

When work is finished, the agent MUST report:

- What changed
- Files changed
- Branch name
- Commit hash
- Whether build/tests were run
- Any follow-up or manual merge considerations

---

## Definition of Done

A task is complete only when all of the following are true:

- Requested changes are implemented
- Changes are limited to the task scope
- Applicable validation has been attempted
- Changes are committed
- Branch is pushed
- Final report is provided

---

## Instruction Precedence

Before starting work, the agent MUST read this file.

If any instruction conflicts with this file, this file takes precedence unless the user explicitly states that this file should be ignored for the current task.
