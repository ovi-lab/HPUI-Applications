# AGENTS.md

## Project

HPUI-Applications builds demo applications on HPUI (Hand Proximate User Interface) — interacting with UI by pointing at it with the hand. The core interaction machinery lives in the upstream HPUI packages (git submodules under `Packages/`, separate repos); this repo holds the applications themselves, one scene per application under `Assets/_Scenes`.

Current focus: the text input interface — a swipe keyboard driven by finger rows (ThumbSwype-style), with word recognition and text editing on top.

## Layout

- `Assets/_Scenes/` — one Unity scene per application
- `Assets/_Scripts/HPUI/` — gesture detection: discrete gesture detector, gesture state machine
- `Assets/_Scripts/Keyboard/` — swipe keyboard: finger-row capture, swipe pipeline, word recognition
- `Assets/_Scripts/TextComposition/`, `Assets/_Scripts/Utils/ExtendedTMPInputField.cs` — text editing (caret, selection)
- `Assets/_Scripts/ColorPicker/`, `Assets/_Scripts/Numpad/` — demo apps for continuous and discrete interaction
- `Assets/_Scripts/Utils/` — shared utilities
- `Assets/_Scripts/Editor/` — editor tooling
- `Packages/ubc.ok.ovilab.*` — upstream HPUI packages (see "HPUI packages" below)

## HPUI packages

The `Packages/ubc.ok.ovilab.*` submodules may be edited when bugs or needed upgrades crop up, but first surface the issue to the dev and clarify what changes are required; only edit with their permission. Never commit or push anything inside the submodule repositories — leave submodule changes as local working-tree edits only; the dev handles upstream.

## Agent skills

### Issue tracker

GitHub Issues (ovi-lab/HPUI-Applications) via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Domain docs

Single-context: `CONTEXT.md` at the repo root, ADRs in `docs/adr/`. See `docs/agents/domain.md`.

## Implementation workflow

Before starting an implementation, check out to an appropriately named branch.

For implementations, do not touch Unity generated files (scene files, meta files, etc.)

## Testing policy

All testing and validation is done by the developer. Do not write automated test suites, test assemblies, or harnesses. Instead, write assertion-dense pure C# classes: guard clauses, invariant checks, and `Debug.Assert`/exception-based validation at boundaries so that misuse and state corruption fail loudly at runtime.

After an implementation is complete (to the best of your ability), follow this sequence exactly:

1. **Review**: spin up a reviewer subagent with project context (README, spec issue #1, and the specific issue being implemented) and have it review the diff only. Fix any issues it notes.
2. **Report, don't test**: do not run any tests or build verification. Just inform the user that the implementation is complete and how they can test it.
3. **No git/issue actions without approval**: do not commit, push, or close issues until the user has given explicit approval.

Do not perform ad-hoc testing of your own (scratch harnesses, scratch builds, CLI verification runs). The user decides whether and how to test.

Once user approves with "lgtm", commit, push, open a pr, link the issue with the pr such that it auto closes when the issue is done.
For commit message, use the following style (combine them when multiple files have been edited in a commit):

- [U] for Unity files
- [C] for Code
- [P] for Package Edits
- [A] for Asset Changes
- [D] for Docs
- [S] for Shader Files
