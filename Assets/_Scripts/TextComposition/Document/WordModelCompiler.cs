using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace _Scripts.TextComposition.Document
{
    /// <summary>
    /// Pure model→string compilation (ADR 0001): words joined with single
    /// blanks, each word's character range captured as a by-product, plus the
    /// reverse char-index→word-boundary mapping and a reparse used by the
    /// debug invariant check. Deterministic and cheap to assert on.
    /// </summary>
    public static class WordModelCompiler
    {
        /// <summary>Compiled output: the display string plus per-word char ranges.</summary>
        public readonly struct CompiledText
        {
            public string Text { get; }
            public int WordCount => wordStarts.Count;

            private readonly List<int> wordStarts;
            private readonly List<int> wordLengths;

            internal CompiledText(string text, List<int> starts, List<int> lengths)
            {
                Text = text;
                wordStarts = starts;
                wordLengths = lengths;
            }

            /// <summary>First char index of the word at <paramref name="wordIndex"/>.</summary>
            public int WordStart(int wordIndex) => RangeChecked(wordIndex, wordStarts);

            /// <summary>Length in chars of the word at <paramref name="wordIndex"/>.</summary>
            public int WordLength(int wordIndex) => RangeChecked(wordIndex, wordLengths);

            /// <summary>Char index one past the last char of the word.</summary>
            public int WordEnd(int wordIndex) => WordStart(wordIndex) + WordLength(wordIndex);

            private static int RangeChecked(int wordIndex, List<int> list)
            {
                if (wordIndex < 0 || wordIndex >= list.Count)
                    throw new ArgumentOutOfRangeException(nameof(wordIndex), wordIndex,
                        $"Word index must be within [0, {list.Count})");
                return list[wordIndex];
            }

            /// <summary>
            /// Maps a character index of <see cref="Text"/> back to a word
            /// boundary 0..WordCount. Exact at word starts and ends (end of
            /// word i resolves to boundary i+1); a strictly interior index
            /// snaps to the nearest boundary of its word, ties toward the
            /// start. Values outside [0, Text.Length] clamp.
            /// </summary>
            public int BoundaryFromCharIndex(int charIndex)
            {
                if (WordCount == 0)
                    return 0;
                if (charIndex <= 0)
                    return 0;
                if (charIndex >= Text.Length)
                    return WordCount;

                for (int i = 0; i < WordCount; i++)
                {
                    int start = wordStarts[i];
                    int end = start + wordLengths[i];
                    if (charIndex == start)
                        return i;
                    if (charIndex < end)
                    {
                        // Strictly interior: nearest of word start (i) and
                        // word end (i+1); ties snap to the start.
                        return charIndex - start <= end - charIndex ? i : i + 1;
                    }
                    if (charIndex == end)
                        return i + 1;
                }

                Debug.Assert(false, "Every char index within the text must resolve to a word boundary");
                return WordCount;
            }
        }

        /// <summary>Compiles the document's word sequence into display text.</summary>
        public static CompiledText Compile(TextDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            string[] parts = new string[document.Words.Count];
            for (int i = 0; i < parts.Length; i++)
                parts[i] = document.Words[i].Text;
            return Compile(parts);
        }

        /// <summary>Compiles a word sequence into display text (words joined by single blanks).</summary>
        public static CompiledText Compile(IReadOnlyList<string> words)
        {
            if (words == null)
                throw new ArgumentNullException(nameof(words));

            var starts = new List<int>(words.Count);
            var lengths = new List<int>(words.Count);
            var builder = new StringBuilder();

            for (int i = 0; i < words.Count; i++)
            {
                string word = words[i];
                if (word == null || word.Length == 0)
                    throw new ArgumentException(
                        $"Word {i} of the compiled sequence is null or empty — empty words are misuse", nameof(words));
                Debug.Assert(word.Trim() == word,
                    $"Word '{word}' carries leading/trailing blanks; tokens must be trimmed");

                if (i > 0)
                    builder.Append(' ');
                starts.Add(builder.Length);
                builder.Append(word);
                lengths.Add(word.Length);
            }

            return new CompiledText(builder.ToString(), starts, lengths);
        }

        /// <summary>
        /// Reparse — the reverse of compile: splits the text back into words
        /// on single blanks. Used by the debug invariant check; malformed
        /// text (leading/trailing blanks, double blanks in the middle, empty
        /// words) throws so delete/insert desync fails loudly.
        /// </summary>
        public static List<string> Reparse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var words = new List<string>();
            if (text.Length == 0)
                return words;

            // Split on single blanks only: empty parts mean malformed blanks.
            string[] parts = text.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0)
                    throw new FormatException(
                        $"Reparse found malformed blanks (leading/trailing or doubled) in '{text}'");
                words.Add(parts[i]);
            }

            return words;
        }
    }
}
