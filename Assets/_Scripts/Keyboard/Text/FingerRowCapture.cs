using ubco.ovilab.HPUI.Core.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.Keyboard.Text
{
    /// <summary>
    /// Pure sensor. Subscribes to HPUI finger-row interactables, remaps each row's
    /// spatial coordinates into normalized (0,1) keyboard space, and fires raw
    /// position events every frame.
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

        [SerializeField, Tooltip("Range of data capture along the top finger interactable")]
        private Vector2 minMaxTopRowLength;

        [SerializeField, Tooltip("Range of data capture across the top finger interactable")]
        private Vector2 minMaxTopRowHeight;

        [SerializeField]
        private HPUIGeneratedContinuousInteractable midRow;

        [SerializeField, Tooltip("Range of data capture along the middle finger interactable")]
        private Vector2 minMaxMidRowLength;

        [SerializeField, Tooltip("Range of data capture across the middle finger interactable")]
        private Vector2 minMaxMidRowHeight;

        [SerializeField]
        private HPUIGeneratedContinuousInteractable bottomRow;

        [SerializeField, Tooltip("Range of data capture along the ring finger interactable")]
        private Vector2 minMaxBottomRowLength;

        [SerializeField, Tooltip("Range of data capture across the ring finger interactable")]
        private Vector2 minMaxBottomRowHeight;

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
            Vector2 rawPosition = new Vector2(args.Position.y, args.Position.x);
            Vector2 normalizedPosition;

            switch (currentInteractable)
            {
                case var i when ReferenceEquals(i, topRow):
                    normalizedPosition = ComputeRowPosition(args.Position, minMaxTopRowLength, minMaxTopRowHeight, 0f, 1f / 3f);
                    break;
                case var i when ReferenceEquals(i, midRow):
                    normalizedPosition = ComputeRowPosition(args.Position, minMaxMidRowLength, minMaxMidRowHeight, 1f / 3f, 2f / 3f);
                    break;
                case var i when ReferenceEquals(i, bottomRow):
                    normalizedPosition = ComputeRowPosition(args.Position, minMaxBottomRowLength, minMaxBottomRowHeight, 2f / 3f, 1f);
                    break;
                default:
                    Debug.LogError($"Unknown interactable sending data: {args.interactableObject.transform.name}");
                    return;
            }

            OnRawPosition?.Invoke(normalizedPosition);
            OnRawPositionDebug?.Invoke(rawPosition, args.interactableObject.transform.name);
        }

        /// <summary>
        /// Remaps an interactable-space position (x = across row, y = along row)
        /// into a normalized (0,1) keyboard-space coordinate.
        /// </summary>
        private static Vector2 ComputeRowPosition(Vector2 position, Vector2 minMaxLength, Vector2 minMaxHeight, float yOutMin, float yOutMax)
        {
            float x = Mathf.Clamp01(Mathf.InverseLerp(minMaxLength.x, minMaxLength.y, position.y));
            float yLocal = Mathf.Clamp01(Mathf.InverseLerp(minMaxHeight.x, minMaxHeight.y, position.x));
            float y = Mathf.Lerp(yOutMin, yOutMax, yLocal);
            return new Vector2(x, y);
        }
    }
}
