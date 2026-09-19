using EditorAttributes;
using TMPro;
using UnityEngine;
using _Scripts.TextComposition.Document;
using _Scripts.Utils;

namespace _Scripts.TextComposition.Render
{
    /// <summary>
    /// TMP as a pure renderer (ADR 0001): subscribes to the composition
    /// controller and recompiles model→string on every state change. Holds no
    /// editor state of its own. Targets either a plain TMP_Text or an
    /// ExtendedTMPInputField (fed through .text so the field's caret and
    /// selection machinery stays in sync).
    /// </summary>
    public class TMPWordRenderer : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private TextCompositionController controller;

        [Tooltip("Extended input field target. When set, rendering goes through inputField.text so the field's caret/selection machinery stays in sync. Takes precedence over the plain TMP_Text target.")]
        [SerializeField] private ExtendedTMPInputField inputField;

        [Tooltip("Plain TMP_Text target. Used when no extended input field is assigned.")]
        [SerializeField] private TMP_Text target;

        private void OnEnable()
        {
            // Auto-wire when left unassigned so scene setup stays trivial.
            if (controller == null && !TryGetComponent(out controller))
            {
                Debug.LogError($"{nameof(TMPWordRenderer)} needs a {nameof(TextCompositionController)}", this);
                enabled = false;
                return;
            }
            if (inputField == null && target == null)
            {
                Debug.LogError($"{nameof(TMPWordRenderer)} needs an {nameof(ExtendedTMPInputField)} or a target TMP_Text", this);
                enabled = false;
                return;
            }

            controller.StateChanged += Render;
            Render();
        }

        private void OnDisable()
        {
            if (controller != null)
                controller.StateChanged -= Render;
        }

        [Button("Render now")]
        private void Render()
        {
            Debug.Assert(controller != null && (inputField != null || target != null),
                $"{nameof(TMPWordRenderer)} is not wired up; state changes will not render");
            string compiled = WordModelCompiler.Compile(controller.Document).Text;
            // TMP_InputField copies its own text string into its text
            // component and layers caret/selection rendering on top, so the
            // extended field must be fed through .text, not its text
            // component directly.
            if (inputField != null)
                inputField.text = compiled;
            else
                target.text = compiled;
        }
    }
}
