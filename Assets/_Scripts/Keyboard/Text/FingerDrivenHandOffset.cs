using ubco.ovilab.HPUI.Core.Interaction;
using ubco.ovilab.ViconUnityStream;
using UnityEngine;

namespace _Scripts.Keyboard.Text
{
    /// <summary>
    /// TEMPORARY script. Hooks into the same HPUI finger-row interactables as
    /// <see cref="FingerRowCapture"/> and maps each finger's normalized
    /// along-row position to the <see cref="CustomSubjectScript.RootPositionOffset"/>
    /// on a target <see cref="CustomHandScript"/>.
    ///
    /// Index finger → X offset
    /// Middle finger → Y offset
    /// Ring finger  → Z offset
    ///
    /// Each axis is remapped from the normalized [0,1] finger position to
    /// the range [-offsetMagnitude, +offsetMagnitude] (default ±0.1 Unity units).
    ///
    /// This lets you tune the hand's positional offset at runtime using finger
    /// gestures instead of fiddling with the inspector while wearing a headset.
    /// </summary>
    public class FingerDrivenHandOffset : MonoBehaviour
    {
        [Header("HPUI Finger Interactables")]
        [SerializeField, Tooltip("Index finger row → drives X offset")]
        private HPUIGeneratedContinuousInteractable topRow;

        [SerializeField, Tooltip("Middle finger row → drives Y offset")]
        private HPUIGeneratedContinuousInteractable midRow;

        [SerializeField, Tooltip("Ring finger row → drives Z offset")]
        private HPUIGeneratedContinuousInteractable bottomRow;

        [Header("Row Ranges (mirrors FingerRowCapture)")]
        [SerializeField, Tooltip("Range of data capture along the top finger interactable")]
        private Vector2 minMaxTopRowLength;

        [SerializeField, Tooltip("Range of data capture along the middle finger interactable")]
        private Vector2 minMaxMidRowLength;

        [SerializeField, Tooltip("Range of data capture along the ring finger interactable")]
        private Vector2 minMaxBottomRowLength;

        [Header("Target")]
        [SerializeField, Tooltip("The CustomHandScript whose RootPositionOffset will be driven at runtime.")]
        private CustomHandScript targetHandScript;

        [Header("Offset")]
        [SerializeField, Tooltip("Maximum displacement per axis (Unity units). Normalized [0,1] maps to [-mag, +mag].")]
        private float offsetMagnitude = 0.1f;

        // Per-finger normalized values (0-1), updated each frame the finger is tracking.
        // Defaults to 0.5 (centre of row → zero offset).
        private float _indexValue = 0.5f;
        private float _middleValue = 0.5f;
        private float _ringValue = 0.5f;

        private void OnEnable()
        {
            if (topRow != null)
                topRow.InteractableStateEvent.AddListener(HandleTopRow);
            if (midRow != null)
                midRow.InteractableStateEvent.AddListener(HandleMidRow);
            if (bottomRow != null)
                bottomRow.InteractableStateEvent.AddListener(HandleBottomRow);
        }

        private void OnDisable()
        {
            if (topRow != null)
                topRow.InteractableStateEvent.RemoveListener(HandleTopRow);
            if (midRow != null)
                midRow.InteractableStateEvent.RemoveListener(HandleMidRow);
            if (bottomRow != null)
                bottomRow.InteractableStateEvent.RemoveListener(HandleBottomRow);
        }

        private void Update()
        {
            if (targetHandScript == null)
                return;

            float x = Mathf.Lerp(-offsetMagnitude, offsetMagnitude, _indexValue);
            float y = Mathf.Lerp(-offsetMagnitude, offsetMagnitude, _middleValue);
            float z = Mathf.Lerp(-offsetMagnitude, offsetMagnitude, _ringValue);

            targetHandScript.RootPositionOffset = new Vector3(x, y, z);
        }

        private void HandleTopRow(HPUIInteractableStateEventArgs args)
        {
            if (args.State != HPUIInteractableState.TrackingUpdate &&
                args.State != HPUIInteractableState.TrackingStarted)
                return;

            // args.Position.y is along the row (same as ComputeRowPosition uses for the x-axis)
            _indexValue = Mathf.Clamp01(
                Mathf.InverseLerp(minMaxTopRowLength.x, minMaxTopRowLength.y, args.Position.y));
        }

        private void HandleMidRow(HPUIInteractableStateEventArgs args)
        {
            if (args.State != HPUIInteractableState.TrackingUpdate &&
                args.State != HPUIInteractableState.TrackingStarted)
                return;

            _middleValue = Mathf.Clamp01(
                Mathf.InverseLerp(minMaxMidRowLength.x, minMaxMidRowLength.y, args.Position.y));
        }

        private void HandleBottomRow(HPUIInteractableStateEventArgs args)
        {
            if (args.State != HPUIInteractableState.TrackingUpdate &&
                args.State != HPUIInteractableState.TrackingStarted)
                return;

            _ringValue = Mathf.Clamp01(
                Mathf.InverseLerp(minMaxBottomRowLength.x, minMaxBottomRowLength.y, args.Position.y));
        }
    }
}