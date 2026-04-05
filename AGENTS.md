# AGENTS

This repository is a Unity 6 project on macOS. The standard workflow is Codex CLI for repo work and uloop for Unity compile, console, runtime inspection, screenshots, and play mode checks.

## Startup Checklist
- Read `memory.md` before making changes.
- Run `git status --short` at the start of each session and account for any existing user changes.
- Treat the current main performance focus as startup cost in `HeatwaveRoomNPCSystem.Awake()`.

## Working Rules
- Keep changes minimal, targeted, and safe.
- Avoid unrelated refactors, cleanup passes, or style churn.
- Do not reopen already-settled performance theories without new evidence.
- Treat the known uloop `1.6.4` mismatch as a tooling warning unless concrete failures appear.

## Investigation mode
For regressions or unclear bugs, use a subagent workflow instead of a single-threaded investigation.
Default split:
- one subagent for runtime/performance symptoms
- one subagent for gameplay/dialogue/content symptoms
- one subagent for diff/blame analysis
- one subagent for repair planning and verification design

Do not make broad fixes before synthesizing the subagent results.
Prefer the smallest safe repair that preserves previously working behavior.

## Memory File Rules
- Update `memory.md` after any meaningful investigation, verified finding, or code change that future sessions should inherit.
- Include `memory.md` in git changes when it is relevant to the work performed.

## Verification Rules
- Before claiming success, run compile and console checks when the task is applicable.
- Prefer the normal validation loop: compile, inspect console, then do focused runtime verification.
