using System;

namespace _Scripts.TextComposition.Document
{
    /// <summary>
    /// A single atomic editor command, dispatched through
    /// <see cref="TextCompositionController.Dispatch"/> — the one integration
    /// seam for all editor state changes. Implementations must be
    /// misuse-rejecting: guard clauses fail loudly rather than corrupting
    /// state.
    /// </summary>
    public interface ITextCommand
    {
        /// <summary>Executes against the document/cursor pair.</summary>
        void Execute(TextDocument document, CursorModel cursor);
    }

    /// <summary>
    /// Inserts a word — or a whitespace-separated run of words — at a
    /// boundary; the caret ends on the boundary after the last inserted word.
    /// </summary>
    public sealed class InsertWordCommand : ITextCommand
    {
        private readonly int boundaryIndex;
        private readonly string text;

        public InsertWordCommand(int boundaryIndex, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("InsertWordCommand rejects empty (or whitespace-only) words", nameof(text));
            if (boundaryIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(boundaryIndex), boundaryIndex,
                    "Boundary must be non-negative");

            this.boundaryIndex = boundaryIndex;
            this.text = text;
        }

        public void Execute(TextDocument document, CursorModel cursor)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (cursor == null)
                throw new ArgumentNullException(nameof(cursor));

            int inserted = document.InsertWord(boundaryIndex, text);
            cursor.BoundaryIndex = boundaryIndex + inserted;
        }
    }

    /// <summary>
    /// Deletes the whole word before the current caret boundary. Removing a
    /// word before the caret leaves the caret's boundary index valid, so no
    /// caret adjustment happens.
    /// </summary>
    public sealed class DeletePrecedingWordCommand : ITextCommand
    {
        public void Execute(TextDocument document, CursorModel cursor)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (cursor == null)
                throw new ArgumentNullException(nameof(cursor));

            document.DeletePrecedingWord(cursor.BoundaryIndex);
        }
    }

    /// <summary>
    /// Moves the caret by a signed word-boundary delta; clamped at the
    /// first/last boundary.
    /// </summary>
    public sealed class MoveCaretCommand : ITextCommand
    {
        private readonly int delta;

        public MoveCaretCommand(int delta)
        {
            this.delta = delta;
        }

        public void Execute(TextDocument document, CursorModel cursor)
        {
            if (cursor == null)
                throw new ArgumentNullException(nameof(cursor));
            cursor.Move(delta);
        }
    }
}
