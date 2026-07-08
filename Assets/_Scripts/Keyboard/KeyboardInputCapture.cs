using ubco.ovilab.HPUI.Core.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.Keyboard
{
    public class KeyboardInputCapture : MonoBehaviour
    {
        /// <summary>
        /// Fired every time on HPUI gesture event on the keyboard surface
        /// </summary>
        public UnityEvent<Vector2> OnKeyboardInputCapture;

        /// <summary>
        /// Fired every time on HPUI gesture event on the keyboard surface
        /// </summary>
        public UnityEvent<Vector2, string> OnKeyboardInputCaptureRaw;

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

        [Header("Gesture Collection Parameters")]
        [SerializeField, Tooltip("Minimum gesture duration (s) to count as valid")]
        private float alpha;

        [SerializeField, Tooltip("Idle time (s) before a gesture is considered finished")]
        private float beta;

        [SerializeField, Tooltip("For more button and short gesture components")]
        private bool enablePrematureTrigger;

        [SerializeField, Tooltip("Duration (s) of continuous input before a premature trigger fires")]
        private float prematureTriggerTime;

        private Vector2 interactableTouchPosition;
        private Vector2 interactableTouchPositionRaw;

        private GestureStateMachine gestureState;

        public UnityEvent OnGestureStarted;
        public UnityEvent OnPrematureTrigger;
        public UnityEvent OnGestureCompleted;
        public UnityEvent OnGestureCancelled;

        private void Awake()
        {
            float? prematureTrigger = enablePrematureTrigger ? prematureTriggerTime : (float?)null;
            gestureState = new GestureStateMachine(alpha, beta, prematureTrigger);
            gestureState.OnGestureStarted += () => OnGestureStarted?.Invoke();
            gestureState.OnPrematureTriggerReached += () => OnPrematureTrigger?.Invoke();
            gestureState.OnGestureCompleted += () => OnGestureCompleted?.Invoke();
            gestureState.OnGestureCancelled += () => OnGestureCancelled?.Invoke();
        }

        private void OnEnable()
        {
            // Subscribe to InteractableStateEvent on each row to get per-frame
            // tracking state and position. The interactable whose state is
            // TrackingUpdate or TrackingStarted is the current tracking target
            // (equivalent to the old CurrentTrackingInteractable).
            topRow.InteractableStateEvent.AddListener(CaptureTrackingState);
            midRow.InteractableStateEvent.AddListener(CaptureTrackingState);
            bottomRow.InteractableStateEvent.AddListener(CaptureTrackingState);
        }

        private void Update()
        {
            gestureState.Tick(Time.deltaTime);
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
        /// tracking target (replaces the old CurrentTrackingInteractable).
        /// </summary>
        private void CaptureTrackingState(HPUIInteractableStateEventArgs args)
        {
            if (args.State != HPUIInteractableState.TrackingUpdate && args.State != HPUIInteractableState.TrackingStarted)
            {
                return;
            }

            IHPUIInteractable currentInteractable = args.interactableObject;
            interactableTouchPositionRaw = new Vector2(args.Position.y, args.Position.x);

            switch (currentInteractable)
            {
                case var i when ReferenceEquals(i, topRow):
                    interactableTouchPosition = ComputeRowPosition(args.Position, minMaxTopRowLength, minMaxTopRowHeight, 0f, 1f / 3f);
                    break;
                case var i when ReferenceEquals(i, midRow):
                    interactableTouchPosition = ComputeRowPosition(args.Position, minMaxMidRowLength, minMaxMidRowHeight, 1f / 3f, 2f / 3f);
                    break;
                case var i when ReferenceEquals(i, bottomRow):
                    interactableTouchPosition = ComputeRowPosition(args.Position, minMaxBottomRowLength, minMaxBottomRowHeight, 2f / 3f, 1f);
                    break;
                default:
                    Debug.LogError($"Unknown interactable sending data: {args.interactableObject.transform.name}");
                    return;
            }

            gestureState.ReportInput();
            OnKeyboardInputCapture?.Invoke(interactableTouchPosition);
            OnKeyboardInputCaptureRaw?.Invoke(interactableTouchPositionRaw, args.interactableObject.transform.name);
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
