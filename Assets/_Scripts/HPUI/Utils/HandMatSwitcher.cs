using EditorAttributes;
using UnityEngine;

namespace _Scripts.HPUI.Utils
{
    /// <summary>
    /// Utility to swap the hand mesh between a visible material (for calibration)
    /// and an invisible depth-only material (for MR occlusion). Attach to the
    /// same GameObject that has the hand SkinnedMeshRenderer, or assign it.
    /// </summary>
    public class HandMatSwitcher : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("The hand's SkinnedMeshRenderer. Auto-detected if on same GameObject.")]
        private SkinnedMeshRenderer handRenderer;

        [Header("Materials")]
        [SerializeField, Tooltip("Visible material shown during calibration.")]
        private Material visibleMaterial;

        [SerializeField, Tooltip("Invisible depth-only material for MR occlusion (e.g. HPUI/HandOcclusion).")]
        private Material invisibleMaterial;

        [SerializeField]
        [Tooltip("Default state of the hand")]
        private bool defaultStateIsInvisible = true;

        /// <summary>
        /// Is the hand currently using the visible material?
        /// </summary>
        public bool IsVisible { get; private set; }

        private void Awake()
        {
            if (handRenderer == null)
                handRenderer = GetComponent<SkinnedMeshRenderer>();

            // Default to visible on start
            if (handRenderer != null)
                handRenderer.material = defaultStateIsInvisible ? invisibleMaterial : visibleMaterial;
            IsVisible = true;
        }

        /// <summary>
        /// Swap between visible and invisible material.
        /// </summary>
        [Button("Toggle Hand Visibility")]
        public void Toggle()
        {
            if (handRenderer == null)
            {
                Debug.LogError("HandMatSwitcher: handRenderer is null!", this);
                return;
            }

            IsVisible = !IsVisible;
            handRenderer.material = IsVisible ? visibleMaterial : invisibleMaterial;
        }

        /// <summary>
        /// Force the hand to be visible.
        /// </summary>
        [Button]
        public void Show()
        {
            if (handRenderer == null)
                return;
            IsVisible = true;
            handRenderer.material = visibleMaterial;
        }

        /// <summary>
        /// Force the hand to be invisible (depth-only).
        /// </summary>
        [Button]
        public void Hide()
        {
            if (handRenderer == null)
                return;
            IsVisible = false;
            handRenderer.material = invisibleMaterial;
        }
    }
}
