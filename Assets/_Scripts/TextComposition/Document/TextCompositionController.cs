using System;
using EditorAttributes;
using UnityEngine;

namespace _Scripts.TextComposition.Document
{
    /// <summary>
    /// The single integration seam for text composition: every editor state
    /// change flows through <see cref="Dispatch"/> as one atomic command
    /// (issue #4; ADRs 0001/0002). After each dispatch the document is
    /// recompiled and reparsed back, asserting model/string agreement before
    /// the change event fires.
    /// </summary>
    [DisallowMultipleComponent]
    public class TextCompositionController : MonoBehaviour
    {
        [Header("Seed")]
        [Tooltip("Initial seed words for scene testing; parsed with InsertWord's whitespace rules.")]
        [SerializeField] private string initialText = "hello world";

        [Header("Scene-test settings")]
        [Tooltip("Word inserted by the 'Insert sample word at caret' button.")]
        [SerializeField] private string sampleWord = "test";

        private readonly TextDocument document = new TextDocument();
        private CursorModel cursor;

        public TextDocument Document => document;
        public CursorModel Cursor => cursor;

        /// <summary>
        /// Fired after every successfully dispatched, invariant-verified
        /// atomic action (document or caret change).
        /// </summary>
        public event Action StateChanged;

        private void Awake()
        {
            cursor = new CursorModel(document);
            // Seeding intentionally bypasses Dispatch: it is configuration,
            // not an editor action, so it runs no invariant check and fires
            // no StateChanged. It completes during Awake, before any
            // renderer's OnEnable, so the initial render sees the seed.
            if (!string.IsNullOrEmpty(initialText))
                document.InsertWord(document.Words.Count, initialText);
        }

        /// <summary>The single integration seam: dispatch one atomic command.</summary>
        public void Dispatch(ITextCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            command.Execute(document, cursor);
            AssertInvariant();
            StateChanged?.Invoke();
        }

        // Public convenience dispatchers for later modules (swipe pipeline,
        // bindings, etc.).

        public void InsertWord(string text) =>
            Dispatch(new InsertWordCommand(cursor.BoundaryIndex, text));

        public void DeletePrecedingWord() =>
            Dispatch(new DeletePrecedingWordCommand());

        public void MoveCaret(int delta) =>
            Dispatch(new MoveCaretCommand(delta));

        // Editor buttons for scene testing.

        [Button("Insert sample word at caret")]
        private void InsertSampleWord() => InsertWord(sampleWord);

        [Button("Delete word before caret")]
        private void DeletePreceding() => DeletePrecedingWord();

        [Button("Move caret left")]
        private void CaretLeft() => MoveCaret(-1);

        [Button("Move caret right")]
        private void CaretRight() => MoveCaret(+1);

        /// <summary>
        /// Debug invariant check (ADR 0001): recompile the model, reparse the
        /// generated string back into words, and assert the round trip
        /// reproduces the model exactly before the change event fires.
        /// </summary>
        private void AssertInvariant()
        {
            var compiled = WordModelCompiler.Compile(document);
            var reparsed = WordModelCompiler.Reparse(compiled.Text);

            Debug.Assert(reparsed.Count == document.Words.Count,
                $"Invariant violated: reparsed word count {reparsed.Count} != model word count {document.Words.Count}");
            for (int i = 0; i < reparsed.Count; i++)
                Debug.Assert(reparsed[i] == document.Words[i].Text,
                    $"Invariant violated: word {i} reparsed as '{reparsed[i]}' but model holds '{document.Words[i].Text}'");
        }
    }
}
