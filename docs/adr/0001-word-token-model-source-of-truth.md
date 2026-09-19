# Word-token model as the single source of truth; TextMeshPro as a pure renderer

The editor's document state must be robust against every kind of misuse, and undo must restore document contents, formatting, caret, selection, and clipboard exactly. We therefore make a plain C# word-token model the single source of truth for all logical editor state, and demote TextMeshPro to a pure renderer fed by a one-way model→string compilation that captures each word's character range as a by-product; placement maps a TMP character index back to a word boundary through those stored ranges. This eliminates the desync failure class of keeping a derived word index synchronized with TMP edits — there is only one writer, and snapshot restore is plain field assignment plus re-render.

## Considered options

- TMP text as the source of truth with a derived word-index view: rejected because two writers cannot be made corruption-proof, and because "derive target state from current state" is the exact failure mode the snapshot design forbids.
- A hand-built VR text renderer: rejected; TMP already provides rendering, wrapping, and rich-text support, and rebuilding it is high-risk.
- Rebuilding the extended field's editing machinery from scratch: rejected. A fully custom caret-rendering / touch→caret-conversion / visual-selection implementation was attempted before and is genuinely hard to engineer; `ExtendedTMPInputField` already provides that machinery, so it is reused as the base display/placement component.

## Consequences

- Only two pure functions bridge model and display: model→string compilation, and char-index→word-boundary mapping. Both are deterministic and cheap to assert on.
- Formatting attributes are model data; rich-text tags are generated, never parsed from user content.
- `ExtendedTMPInputField` is the base display/placement component: its caret rendering, screen/touch→caret-position conversion (`SetCaretFromScreenPosition`), visual selection mode, and formatting/caret rendering on a world-space canvas are reused rather than rebuilt. The reuse covers its *machinery*, not its *state ownership*: the field consumes compiled model→string text as a pure renderer and feeds placements/commands back through the composition facade, so the model remains the single writer of logical editor state and the two-pure-bridge-functions invariant holds at the model boundary.
- A debug invariant check (reparse the generated string, compare against the model) can run before every ledger append.
