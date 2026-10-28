# Session Baseline

**This is the starting point for every fresh session.** It contains everything
the AI assistant needs to work on this repository. It travels with the repo
(embedded, not in local memory).

Use standard speed. Be conservative with tokens/tool calls. No subagents unless
the user explicitly asks. Inspect only what is needed.

## The Three-File System

Keep these three files apart; each has one job:

- **SESSION_BASELINE.md** (this file): How user and assistant work together
  (rules, tools, handovers, scope, whats and whys, learned behavior).
- **TASKLIST.md**: Complete work history. Every piece of work gets a T-ID.
  Nothing stays unrecorded. Even bugs found but not yet fixed are tasks.
- **README.md**: For humans reading years later (plain prose, no task numbers,
  no operator rules). What the repository is, key documents, requirements,
  lessons learned, challenges. The main README is in the repo root and points
  to any other READMEs in subfolders. Keep that list current when a README
  is added.

`.claude.md` is only the entry point. It holds no rules and is not edited.

After each code/config modification, update docs in the same session. Each
fact goes in the file that owns it: task status in TASKLIST, rules here,
technical detail in relevant documentation.

## Session Rules

### Rule 1: No Terminal Beeps (Communication Crash)

- Never send multi-line or here-string (`@' ... '@`) commands to the terminal.
  They make Windows beep and the session with the user crashes.
- Write files with the file tools (create/replace), not terminal commands.
- If the terminal is needed, use short single-line commands only.
- Never put markdown backticks inside terminal commands.

### Core Working Agreement

- Always use `caveman:caveman` skill for replies unless user says `stop caveman`
  or `normal mode`. If the skill is not available, reply short and plain.
- User makes decisions; I follow the lead. No assumptions about next steps.
- Ask before making changes to existing code. Explain before implementing.
- Never modify without understanding the impact.
- Test and validate all changes compile.
- Can create, modify, delete files (with understanding of context).
- Can run builds and unit tests to verify changes, but only when the user
  asks or confirms (see Testing & Commands).
- Never run destructive or hard-to-reverse operations (deleting source
  files, overwriting configs, force-push, history rewrite) without asking
  first, even when the goal seems obviously good.

### Documentation & Task Management

- Everything is a task, including setup, rules work and talking about rules.
  Open a new T-ID every time a new topic starts or the topic changes. Take
  the next free number in TASKLIST.md.
- Keep the active task updated in TASKLIST.md as we work (status, work done,
  findings, time). Do this on my own, without being told. The user never has
  to ask for task updates.
- Close the task as soon as it is done, on my own, without asking the user.
  Write `time-spent` at once: from real evidence (timestamps, session
  length) when available, else a rough `~` estimate. Then open the next one.
- Task granularity: one task per coherent piece of work, not per line changed.
  An hour on one topic = one new T-ID. Do not split into tiny tasks, but do
  not hide a distinct finding inside another task either.
- Every finding is its own task: each bug or discovery gets its own T-ID in
  TASKLIST (symptom, cause, fix, closed or open). Link from parent with one
  line. A problem found but not fixable now stays open until fixed.
- Time spent: every closed task ends with `time-spent: N hours` (indented).
  Use `~N hours` when unsure. Open tasks may carry `~N hours so far`. Never
  present a backfilled estimate as measured.
- Log work done before task existed (retrospective, checked).
- Keep root markdown at 80 characters/line (default). 80-100 allowed, avoid
  when clean wrap practical. Over 100 needs explicit explanation + approval.

### Observations & Facts

- When user states fact about firsthand observation (what they measured, what
  tool showed, what supplier said), treat as confirmed truth once stated.
- Do not re-litigate with competing theory. Challenging OK first time claim is
  ambiguous; once confirmed, drop alternate theory. Redirect effort at
  explanations consistent with confirmed fact.
- Read docs before asking: grep SESSION_BASELINE.md, TASKLIST.md, README.md
  before asking user to re-explain something. Assume it's documented until
  grep proves otherwise.

### Communication & Decisions

- Questions go straight to user (via question tool), not buried in plans/notes.
- On ambiguous request or directional change: ask one clarifying question
  before writing a lot. Questioning user is welcome; once they decide, execute
  and do not re-raise.

### File Handling

- Huge/blob-heavy files: never read whole. Map line lengths, preview with cuts,
  extract ranges, treat blobs as opaque.
- This file is the only memory that travels. Everything a later session must
  know goes into root markdown files. Do NOT store project knowledge or rules
  in local memory folder; it does not sync to other machines.

### Testing & Commands

- Never run tests, builds, CLI commands or anything metered unless user
  explicitly asks for that exact thing or confirms after being asked.
  Reading the session files at start is already approved (see .claude.md).
- Tell the cost first (time, load, side effects). Command timeouts: 10
  minutes or less.
- Every test or verification run gets a short report in the same session:
  setup, expected, measured, result, findings with T-IDs, open items.
- A test must be at least as strict as what it certifies. A test that
  passes while the real function fails is a defect in the test and gets its
  own task. When a check is added to a function, add it to its test too.

### Debugging (Evidence First)

- Faults (hangs, timing problems, crashes) are read, not guessed. First
  step is evidence: logs, stack traces of all threads, timestamps. Not a
  configuration experiment.
- One change per experiment, so each result has exactly one cause.
- Do not filter logs or output while looking for a cause.
- Nothing on a startup or main path may wait without a timeout.
- Production must run unattended. Nothing in a production path may stop,
  refuse or slow down because of a diagnostic, debug logging or a check.
  Debug aids must not change the timing they are meant to measure.
- Deploy only from a clean build. An incremental build can miss changes.

### Planning Phase (Before Code)

- Every new feature or significant change starts with planning.
- Step 1: Understand requirements (what are we building, why, for whom).
- Step 2: Design the approach (architecture, structure, key decisions).
- Step 3: Get user approval on the plan before writing code.
- No code is written until user confirms the plan.

### Architecture

- Always use clean architecture principles unless user explicitly says otherwise.
- Separate concerns: business logic from UI, database, and external services.
- Dependency rule: inner layers depend on no outer layer (use abstractions).
- When in doubt, prefer composition over inheritance and interfaces over concrete classes.

### Knowledge Capture

- Nothing stays only in the chat. Pictures the user pastes are saved in
  the repo (e.g. `Attachments/`) in the same session. Facts found while
  working (specs, web pages, data sheet values) are written into the docs
  next to the work, with their source.
- Evidence and derived files (logs, captures, analysis output) go in a
  visible repo folder, not in temp folders, so later sessions reuse them.
- Wrong comments or docs are replaced, not corrected by appending.
- User sketches and mock-ups show the intent, not pixel-exact specs. Keep
  what is where and what it does; make the real design consistent.

### Confidentiality

- Keep exposure of private or company data low.
- Do not paste whole files (configs, logs, data dumps) into replies.
  Process them locally and report only the values or excerpts needed.
- Do not repeat sensitive details (customers, orders, prices, contacts)
  unless the task needs them.
- No private details in any local or cross-session memory.
- Screenshots pasted into chat are sent for processing: keep them cropped
  to what is relevant.

## Git Operations - CRITICAL RULES

### Commit vs Push (ABSOLUTE)
- **Commit** = Local only (stage changes + commit to local branch)
- **Push** = Upload to remote (ONLY when user explicitly says "push")
- **NEVER assume a push after a commit** - These are separate operations
- Wait for explicit instruction before: push, pull, fetch, or rebase
- When ambiguous, ask for clarification
- Always confirm the action before executing
- Report what was done after execution
- Approval is per batch: "commit and push" covers that one batch only. It
  does not carry over to later changes, not even a one-line doc fix.
- After any `git pull`, check TASKLIST.md for duplicate T-IDs (two
  machines can hand out the same number) and renumber the local ones.

### Commit Protocol
- No automatic commit after every step
- Finish the actual work, update tasks/docs, say what is uncommitted
- Commit only when user says so or after a block they have reviewed
- Commits never include a push
- Before committing, check if any dirty files belong to user (ask, don't stage
  around them silently)

### Git Account & Identity
- **Commit as**: `Gustav Vanderdi <Vanderdi.Gustav@gmail.com>`
- Never commit as different account without explicit user request
- Verify config at session start: `git config user.name` and `git config
  user.email`

## Current Project Status

### Branch & Repository
- **Active Branch**: `<branch>`  (FILL IN)
- **Repository**: `<url>`  (FILL IN)

### Current Scope
- First session: fill in this section together with the assistant.

## Pending Items & Scratchpad

- (empty)
