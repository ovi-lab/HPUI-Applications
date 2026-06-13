using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TMPro
{
    /// <summary>
    /// Extension of TMP_InputField that exposes programmatic caret movement,
    /// coordinate-based caret placement, and a simple "visual selection mode".
    ///
    /// Key features:
    /// - Set caret from a UI screen position.
    /// - Move caret left / right / up / down by one step.
    /// - Toggle selection mode on/off.
    /// - While selection mode is active, remember the original caret location and
    ///   update the visual selection bounds every time the caret moves.
    /// - Query the selected text and the exact string-index bounds.
    ///
    /// Notes:
    /// - Selection is tracked using string positions, not just visual caret indices.
    ///   This is usually what you want for substring extraction.
    /// - Coordinate input is expected in SCREEN SPACE, same as PointerEventData.position.
    /// - For Screen Space - Overlay canvas, pass null for the camera.
    /// - For Screen Space - Camera / World Space canvas, pass the canvas event/world camera.
    /// </summary>
    [AddComponentMenu("UI (Canvas)/TextMeshPro - Extended Input Field", 12)]
    public class ExtendedTMPInputField : TMP_InputField
    {
        [Header("Extended Selection")]
        [SerializeField]
        [Tooltip("When enabled, caret movement updates a visual text selection from the original caret position.")]
        private bool m_VisualSelectionMode = false;

        [SerializeField]
        [Tooltip("Original string position captured when visual selection mode is enabled.")]
        private int m_SelectionOriginStringPosition = 0;

        /// <summary>
        /// Returns true if programmatic visual selection mode is currently active.
        /// </summary>
        public bool VisualSelectionMode => m_VisualSelectionMode;

        /// <summary>
        /// Returns the original string index captured when selection mode began.
        /// </summary>
        public int SelectionOriginStringPosition => m_SelectionOriginStringPosition;

        /// <summary>
        /// Enables or disables visual selection mode.
        ///
        /// When enabled:
        /// - The current caret/string position is captured as the selection origin.
        /// - Future caret movement through this helper will extend/shrink selection visually.
        ///
        /// When disabled:
        /// - The selection collapses to the current caret position.
        /// </summary>
        /// <param name="enabled">Whether visual selection mode should be active.</param>
        public void SetVisualSelectionMode(bool enabled)
        {
            if (m_VisualSelectionMode == enabled)
                return;

            m_VisualSelectionMode = enabled;

            if (enabled)
            {
                m_SelectionOriginStringPosition = stringPosition;
                selectionStringAnchorPosition = m_SelectionOriginStringPosition;
                selectionStringFocusPosition = stringPosition;
            }
            else
            {
                int current = stringPosition;
                selectionStringAnchorPosition = current;
                selectionStringFocusPosition = current;
            }

            ForceLabelUpdate();
            MarkGeometryDirtyExternally();
        }

        /// <summary>
        /// Convenience toggle for visual selection mode.
        /// </summary>
        public void ToggleVisualSelectionMode()
        {
            SetVisualSelectionMode(!m_VisualSelectionMode);
        }

        /// <summary>
        /// Places the caret from a screen-space UI coordinate.
        ///
        /// Coordinate format:
        /// - screenPos must be in screen coordinates, same format as PointerEventData.position
        ///   or Input.mousePosition.
        ///
        /// Camera rules:
        /// - Overlay canvas: pass null
        /// - Camera/World canvas: pass the relevant UI camera
        ///
        /// If visual selection mode is active, the current selection origin is preserved and the
        /// moved caret becomes the new selection focus.
        /// If visual selection mode is inactive, selection is collapsed to the new caret location.
        /// </summary>
        /// <param name="screenPos">Screen-space UI position.</param>
        /// <param name="cam">UI camera, or null for overlay canvas.</param>
        public void SetCaretFromScreenPosition(Vector2 screenPos, Camera cam = null)
        {
            if (m_TextComponent == null || m_TextComponent.textInfo == null || m_TextComponent.textInfo.characterCount == 0)
                return;

            CaretPosition insertionSide;
            int insertionIndex = TMP_TextUtilities.GetCursorIndexFromPosition(m_TextComponent, screenPos, cam, out insertionSide);

            int newStringPosition;

            if (m_isRichTextEditingAllowed)
            {
                if (insertionSide == CaretPosition.Left)
                    newStringPosition = m_TextComponent.textInfo.characterInfo[insertionIndex].index;
                else
                    newStringPosition = m_TextComponent.textInfo.characterInfo[insertionIndex].index +
                                        m_TextComponent.textInfo.characterInfo[insertionIndex].stringLength;
            }
            else
            {
                if (insertionSide == CaretPosition.Left)
                {
                    newStringPosition = insertionIndex == 0
                        ? m_TextComponent.textInfo.characterInfo[0].index
                        : m_TextComponent.textInfo.characterInfo[insertionIndex - 1].index +
                          m_TextComponent.textInfo.characterInfo[insertionIndex - 1].stringLength;
                }
                else
                {
                    newStringPosition = m_TextComponent.textInfo.characterInfo[insertionIndex].index +
                                        m_TextComponent.textInfo.characterInfo[insertionIndex].stringLength;
                }
            }

            ApplyCaretMove(newStringPosition);
        }

        /// <summary>
        /// Moves the caret by one logical step in the given direction.
        ///
        /// Supported inputs:
        /// - (-1, 0) = left
        /// - ( 1, 0) = right
        /// - ( 0, 1) = up
        /// - ( 0,-1) = down
        ///
        /// Any other value is ignored.
        ///
        /// If visual selection mode is active, movement updates the visual selection.
        /// If not active, movement collapses the selection to the moved caret position.
        /// </summary>
        /// <param name="dir">Direction vector.</param>
        public void MoveCaret(Vector2Int dir)
        {
            if (m_TextComponent == null || m_TextComponent.textInfo == null || m_TextComponent.textInfo.characterCount == 0)
                return;

            if (dir == Vector2Int.left)
            {
                MoveLeftInternal();
            }
            else if (dir == Vector2Int.right)
            {
                MoveRightInternal();
            }
            else if (dir == Vector2Int.up)
            {
                MoveUpInternal();
            }
            else if (dir == Vector2Int.down)
            {
                MoveDownInternal();
            }
        }

        /// <summary>
        /// Returns the currently selected text based on string selection bounds.
        /// Returns empty string if there is no active selection.
        /// </summary>
        public string GetSelectedText()
        {
            GetSelectionStringRange(out int start, out int end);

            if (start == end || string.IsNullOrEmpty(text))
                return string.Empty;

            start = Mathf.Clamp(start, 0, text.Length);
            end = Mathf.Clamp(end, 0, text.Length);

            if (end < start)
            {
                int tmp = start;
                start = end;
                end = tmp;
            }

            return text.Substring(start, end - start);
        }

        /// <summary>
        /// Gets the current selection bounds in string indices.
        ///
        /// The returned range is normalized:
        /// - start <= end
        /// - both are clamped to valid string bounds
        ///
        /// Example:
        /// If the visual selection spans characters 14 back to 3,
        /// this returns start=3, end=14.
        /// </summary>
        public void GetSelectionStringRange(out int start, out int end)
        {
            start = selectionStringAnchorPosition;
            end = selectionStringFocusPosition;

            start = Mathf.Clamp(start, 0, text != null ? text.Length : 0);
            end = Mathf.Clamp(end, 0, text != null ? text.Length : 0);

            if (end < start)
            {
                int tmp = start;
                start = end;
                end = tmp;
            }
        }

        /// <summary>
        /// Returns true if there is currently a visual/string selection.
        /// </summary>
        public bool HasSelection()
        {
            return selectionStringAnchorPosition != selectionStringFocusPosition;
        }

        /// <summary>
        /// Clears any active selection and collapses the caret to the current focus position.
        /// Also disables visual selection mode.
        /// </summary>
        public void ClearSelection()
        {
            int current = stringPosition;
            m_VisualSelectionMode = false;
            selectionStringAnchorPosition = current;
            selectionStringFocusPosition = current;
            ForceLabelUpdate();
            MarkGeometryDirtyExternally();
        }

        /// <summary>
        /// Ensures the field is focused before external programmatic control.
        /// Optional helper if you are driving this from code only.
        /// </summary>
        public void EnsureFocused()
        {
            if (!isFocused)
            {
                ActivateInputField();

                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
                    EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        private void ApplyCaretMove(int newStringPosition)
        {
            newStringPosition = Mathf.Clamp(newStringPosition, 0, text != null ? text.Length : 0);

            if (m_VisualSelectionMode)
            {
                selectionStringAnchorPosition = m_SelectionOriginStringPosition;
                selectionStringFocusPosition = newStringPosition;
            }
            else
            {
                selectionStringAnchorPosition = newStringPosition;
                selectionStringFocusPosition = newStringPosition;
            }

            ForceLabelUpdate();
            MarkGeometryDirtyExternally();
        }

        private void MoveLeftInternal()
        {
            int current = stringPosition;
            int next;

            if (m_isRichTextEditingAllowed)
            {
                if (current <= 0)
                    next = 0;
                else if (char.IsLowSurrogate(text[current - 1]))
                    next = Mathf.Max(0, current - 2);
                else
                    next = current - 1;
            }
            else
            {
                int caret = selectionFocusPosition;
                if (caret <= 0)
                {
                    next = 0;
                }
                else
                {
                    int prevCaret = caret - 1;
                    next = m_TextComponent.textInfo.characterInfo[prevCaret].index;

                    if (prevCaret > 0 &&
                        m_TextComponent.textInfo.characterInfo[prevCaret].character == '\n' &&
                        m_TextComponent.textInfo.characterInfo[prevCaret - 1].character == '\r')
                    {
                        next = m_TextComponent.textInfo.characterInfo[prevCaret - 1].index;
                    }
                }
            }

            ApplyCaretMove(next);
        }

        private void MoveRightInternal()
        {
            int current = stringPosition;
            int next;

            if (m_isRichTextEditingAllowed)
            {
                if (current >= text.Length)
                    next = text.Length;
                else if (char.IsHighSurrogate(text[current]))
                    next = Mathf.Min(text.Length, current + 2);
                else
                    next = current + 1;
            }
            else
            {
                int caret = selectionFocusPosition;
                int charCount = m_TextComponent.textInfo.characterCount;

                if (caret >= charCount - 1)
                {
                    next = text.Length;
                }
                else
                {
                    var charInfo = m_TextComponent.textInfo.characterInfo[caret];
                    next = charInfo.index + charInfo.stringLength;

                    if (charInfo.character == '\r' &&
                        caret + 1 < charCount &&
                        m_TextComponent.textInfo.characterInfo[caret + 1].character == '\n')
                    {
                        next = m_TextComponent.textInfo.characterInfo[caret + 1].index +
                               m_TextComponent.textInfo.characterInfo[caret + 1].stringLength;
                    }
                }
            }

            ApplyCaretMove(next);
        }

        private void MoveUpInternal()
        {
            int currentCaret = selectionFocusPosition;
            int targetCaret = multiLine
                ? LineUpCharacterPositionExternal(currentCaret, true)
                : 0;

            int next = GetSafeStringIndexFromCaretPosition(targetCaret);
            ApplyCaretMove(next);
        }

        private void MoveDownInternal()
        {
            int currentCaret = selectionFocusPosition;
            int targetCaret = multiLine
                ? LineDownCharacterPositionExternal(currentCaret, true)
                : Mathf.Max(0, m_TextComponent.textInfo.characterCount - 1);

            int next = GetSafeStringIndexFromCaretPosition(targetCaret);

            if (!multiLine && m_TextComponent.textInfo.characterCount > 0)
            {
                int lastCaret = m_TextComponent.textInfo.characterCount - 1;
                next = m_TextComponent.textInfo.characterInfo[lastCaret].index +
                       m_TextComponent.textInfo.characterInfo[lastCaret].stringLength;
            }

            ApplyCaretMove(next);
        }

        private int GetSafeStringIndexFromCaretPosition(int caretPosition)
        {
            if (m_TextComponent == null || m_TextComponent.textInfo.characterCount == 0)
                return 0;

            caretPosition = Mathf.Clamp(caretPosition, 0, m_TextComponent.textInfo.characterCount - 1);
            return m_TextComponent.textInfo.characterInfo[caretPosition].index;
        }

        /// <summary>
        /// Local copy of TMP_InputField's private LineUpCharacterPosition logic.
        /// Needed because the original method is private.
        /// </summary>
        private int LineUpCharacterPositionExternal(int originalPos, bool goToFirstChar)
        {
            if (originalPos >= m_TextComponent.textInfo.characterCount)
                originalPos -= 1;

            TMP_CharacterInfo originChar = m_TextComponent.textInfo.characterInfo[originalPos];
            int originLine = originChar.lineNumber;

            if (originLine - 1 < 0)
                return goToFirstChar ? 0 : originalPos;

            int endCharIdx = m_TextComponent.textInfo.lineInfo[originLine].firstCharacterIndex - 1;

            int closest = -1;
            float distance = TMP_Math.FLOAT_MAX;
            float range = 0;

            for (int i = m_TextComponent.textInfo.lineInfo[originLine - 1].firstCharacterIndex; i < endCharIdx; ++i)
            {
                TMP_CharacterInfo currentChar = m_TextComponent.textInfo.characterInfo[i];

                float d = originChar.origin - currentChar.origin;
                float r = d / (currentChar.xAdvance - currentChar.origin);

                if (r >= 0 && r <= 1)
                    return r < 0.5f ? i : i + 1;

                d = Mathf.Abs(d);

                if (d < distance)
                {
                    closest = i;
                    distance = d;
                    range = r;
                }
            }

            if (closest == -1)
                return endCharIdx;

            return range < 0.5f ? closest : closest + 1;
        }

        /// <summary>
        /// Local copy of TMP_InputField's private LineDownCharacterPosition logic.
        /// Needed because the original method is private.
        /// </summary>
        private int LineDownCharacterPositionExternal(int originalPos, bool goToLastChar)
        {
            if (originalPos >= m_TextComponent.textInfo.characterCount)
                return m_TextComponent.textInfo.characterCount - 1;

            TMP_CharacterInfo originChar = m_TextComponent.textInfo.characterInfo[originalPos];
            int originLine = originChar.lineNumber;

            if (originLine + 1 >= m_TextComponent.textInfo.lineCount)
                return goToLastChar ? m_TextComponent.textInfo.characterCount - 1 : originalPos;

            int endCharIdx = m_TextComponent.textInfo.lineInfo[originLine + 1].lastCharacterIndex;

            int closest = -1;
            float distance = TMP_Math.FLOAT_MAX;
            float range = 0;

            for (int i = m_TextComponent.textInfo.lineInfo[originLine + 1].firstCharacterIndex; i < endCharIdx; ++i)
            {
                TMP_CharacterInfo currentChar = m_TextComponent.textInfo.characterInfo[i];

                float d = originChar.origin - currentChar.origin;
                float r = d / (currentChar.xAdvance - currentChar.origin);

                if (r >= 0 && r <= 1)
                    return r < 0.5f ? i : i + 1;

                d = Mathf.Abs(d);

                if (d < distance)
                {
                    closest = i;
                    distance = d;
                    range = r;
                }
            }

            if (closest == -1)
                return endCharIdx;

            return range < 0.5f ? closest : closest + 1;
        }

        /// <summary>
        /// Small wrapper because TMP_InputField.MarkGeometryAsDirty is private.
        /// ForceLabelUpdate usually covers most cases, but this helps ensure redraw.
        /// </summary>
        private void MarkGeometryDirtyExternally()
        {
            CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);
        }
    }
}
