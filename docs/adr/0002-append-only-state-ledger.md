# Append-only state ledger for undo

Undo must reverse any atomic action exactly, restoring document contents, formatting, caret, selection, and internal clipboard state. Long-press redo re-appends the snapshot the last undo stepped past (looked up in the ledger; no separate redo stack is maintained). We record one full absolute snapshot of logical editor state per atomic action in an append-only JSONL ledger with monotonic version ids. Undo appends a new snapshot whose content is identical to the target version and whose metadata notes what it restores; lines are never deleted or mutated, the current state is simply the ledger tail, and there is no separate rollback tracker. The ledger doubles as the live C# event stream consumed by external logging.

## Considered options

- Command pattern with inverse operations per action type: rejected; absolute snapshots require no per-action inverse logic and eliminate state-derivation bugs, at a storage cost that is trivial at document scale.

## Consequences

- Restoring is always "set absolute state" — the same operation used for configuration-driven re-instantiation.
- The append-only file is a complete audit trail of editor state transitions; consumers can subscribe to the stream or read the file.
- Undo/redo input: tap = undo (repeated taps walk further back the ledger); long press = redo (re-appends the snapshot the last undo stepped past). This does not change the ledger format.
