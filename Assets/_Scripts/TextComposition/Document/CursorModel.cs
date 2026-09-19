using System;
using UnityEngine;

namespace _Scripts.TextComposition.Document
{
    /// <summary>
    /// Caret state: a word-boundary index into the document (ADR 0001). The
    /// caret only ever lands on word boundaries (0..WordCount) and clamps at
    /// the first/last boundary.
    /// </summary>
    public sealed class CursorModel
    {
        private readonly TextDocument document;
        private int boundaryIndex;

        public CursorModel(TextDocument document)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            // Document mutations can shrink the word sequence (delete); keep
            // the caret clamped onto a valid boundary whenever that happens.
            this.document.Changed += ClampToDocument;
        }

        private void ClampToDocument() => BoundaryIndex = boundaryIndex;

        /// <summary>Boundary index 0..WordCount; assignments clamp in-range.</summary>
        public int BoundaryIndex
        {
            get => boundaryIndex;
            set => boundaryIndex = Clamp(value);
        }

        /// <summary>Word immediately before the caret, or null at boundary 0.</summary>
        public WordToken PrecedingWord =>
            boundaryIndex == 0 ? null : document.Words[boundaryIndex - 1];

        /// <summary>Word immediately after the caret, or null at the last boundary.</summary>
        public WordToken FollowingWord =>
            boundaryIndex >= document.Words.Count ? null : document.Words[boundaryIndex];

        /// <summary>Moves the caret by a signed boundary delta, clamped.</summary>
        public void Move(int delta) => BoundaryIndex = boundaryIndex + delta;

        private int Clamp(int value) =>
            Mathf.Clamp(value, 0, document.Words.Count);
    }
}
