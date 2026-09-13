# Word-token model as the single source of truth; TextMeshPro as a pure renderer

The editor's document state must be robust against every kind of misuse, and undo must restore document contents, formatting, caret, selection, and clipboard exactly. We therefore make a plain C# word-token model the single source of truth for all logical editor state, and demote TextMeshPro to a pure renderer fed by a one-way model→string compilation that captures each word's character range as a by-product; placement maps a TMP character index back to a word boundary through those stored ranges. This eliminates the desync failure class of keeping a derived word index synchronized with TMP edits — there is only one writer, and snapshot restore is plain field assignment plus re-render.

## Considered options

- TMP text as the source of truth with a derived word-index view: rejected because two writers cannot be made corruption-proof, and because "derive target state from current state" is the exact failure mode the snapshot design forbids.
- A hand-built VR text renderer: rejected; TMP already provides rendering, wrapping, and rich-text support, and rebuilding it is high-risk.

## Consequences

- Only two pure functions bridge model and display: model→string compilation, and char-index→word-boundary mapping. Both are deterministic and cheap to assert on.
- Formatting attributes are model data; rich-text tags are generated, never parsed from user content.
- A debug invariant check (reparse the generated string, compare against the model) can run before every ledger append.
