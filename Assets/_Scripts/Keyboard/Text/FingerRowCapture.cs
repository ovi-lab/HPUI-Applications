using ubco.ovilab.HPUI.Core.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.Keyboard.Text
{
    /// <summary>
    /// Pure sensor. Subscribes to HPUI finger-row interactables, normalizes the
    /// raw center-relative Unity-unit coordinates between each row's mesh size
    /// bounds, band-maps the across-row axis into the row's keyboard-space
    /// third, and fires raw position events every frame.
    /// </summary>
    public class FingerRowCapture : MonoBehaviour
    {
        /// <summary>
        /// Fired every frame an HPUI finger is tracking on the keyboard surface.
        /// Position is normalized (0,1) keyboard-space.
        /// </summary>
        public UnityEvent<Vector2> OnRawPosition;

        /// <summary>
        /// Fired every frame alongside OnRawPosition with additional debug info
        /// (raw interactable-space coordinate and the interactable name).
        /// </summary>
        public UnityEvent<Vector2, string> OnRawPositionDebug;

        [Header("HPUI Interactable Parameters")]
        [SerializeField]
        private HPUIGeneratedContinuousInteractable topRow;

        [SerializeField]
        private HPUIGeneratedContinuousInteractable midRow;

        [SerializeField]
        private HPUIGeneratedContinuousInteractable bottomRow;

        private void OnEnable()
        {
            topRow.InteractableStateEvent.AddListener(CaptureTrackingState);
            midRow.InteractableStateEvent.AddListener(CaptureTrackingState);
            bottomRow.InteractableStateEvent.AddListener(CaptureTrackingState);
        }

        private void OnDisable()
        {
            topRow.InteractableStateEvent.RemoveListener(CaptureTrackingState);
            midRow.InteractableStateEvent.RemoveListener(CaptureTrackingState);
            bottomRow.InteractableStateEvent.RemoveListener(CaptureTrackingState);
        }

        /// <summary>
        /// Called per-frame for each interactable that is being detected.
        /// The interactable with <see cref="HPUIInteractableState.TrackingUpdate"/>
        /// or <see cref="HPUIInteractableState.TrackingStarted"/> is the current
        /// tracking target.
        /// </summary>
        private void CaptureTrackingState(HPUIInteractableStateEventArgs args)
        {
            if (args.State != HPUIInteractableState.TrackingUpdate && args.State != HPUIInteractableState.TrackingStarted)
                return;

            IHPUIInteractable currentInteractable = args.interactableObject;
            // args.Position is a Unity-unit offset relative to the interactable's
            // center, so each axis ranges over +/- half the mesh size. Normalize
            // between those bounds: +size/2 maps to 0, -size/2 maps to 1.
            // Keyboard-space x comes from the along-row axis (Position.y against
            // Y_size); keyboard-space y comes from the across-row axis
            // (Position.x against X_size), band-mapped per row: top 0-1/3,
            // mid 1/3-2/3, bottom 2/3-1.
            Debug.Assert(float.IsFinite(args.Position.x) && float.IsFinite(args.Position.y), $"Non-finite interactable position from {currentInteractable.transform.name}: {args.Position}");
            Debug.Assert(topRow.X_size > 0f && topRow.Y_size > 0f && midRow.X_size > 0f && midRow.Y_size > 0f && bottomRow.X_size > 0f && bottomRow.Y_size > 0f,
                "Interactable mesh sizes must be positive for normalization");
            Vector2 rawPosition = new Vector2(args.Position.y, args.Position.x);
            Vector2 normalizedPosition;

            switch (currentInteractable)
            {
                case var i when ReferenceEquals(i, topRow):
                    normalizedPosition = new Vector2(Mathf.Clamp01(0.5f - args.Position.y / topRow.Y_size), Mathf.Lerp(0f, 1f / 3f, Mathf.Clamp01(0.5f - args.Position.x / topRow.X_size)));
                    break;
                case var i when ReferenceEquals(i, midRow):
                    normalizedPosition = new Vector2(Mathf.Clamp01(0.5f - args.Position.y / midRow.Y_size), Mathf.Lerp(1f / 3f, 2f / 3f, Mathf.Clamp01(0.5f - args.Position.x / midRow.X_size)));
                    break;
                case var i when ReferenceEquals(i, bottomRow):
                    normalizedPosition = new Vector2(Mathf.Clamp01(0.5f - args.Position.y / bottomRow.Y_size), Mathf.Lerp(2f / 3f, 1f, Mathf.Clamp01(0.5f - args.Position.x / bottomRow.X_size)));
                    break;
                default:
                    Debug.LogError($"Unknown interactable sending data: {args.interactableObject.transform.name}");
                    return;
            }

            Debug.Assert(normalizedPosition.x >= 0f && normalizedPosition.x <= 1f, $"Keyboard-space x out of range: {normalizedPosition.x}");
            Debug.Assert(normalizedPosition.y >= 0f && normalizedPosition.y <= 1f, $"Keyboard-space y out of range: {normalizedPosition.y}");

            OnRawPosition?.Invoke(normalizedPosition);
            OnRawPositionDebug?.Invoke(rawPosition, args.interactableObject.transform.name);
        }
    }
}
