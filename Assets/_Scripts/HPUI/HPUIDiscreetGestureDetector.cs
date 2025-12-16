using System;
using System.Collections;
using ubco.ovilab.HPUI.Core.Interaction;
using UnityEngine;
using UnityEngine.Events;
namespace _Scripts
{
    /// <summary>
    /// Detects and manages various HPUI gestures such as Tap, Double Tap, Long Presses and Flicks.
    /// This script attaches to a GameObject and listens for gesture events from the HPUI interactable
    /// to process and dispatch specific gesture events. Intended for use with <see cref="HPUIBaseInteractable"/>
    /// </summary>
    [RequireComponent(typeof(IHPUIInteractable))]
    public class HPUIDiscreetGestureDetector : MonoBehaviour
    {
        #region Events
        [Header("Events")]

        [Tooltip("Callback that is invoked when a tap gesture is detected.")]
        [SerializeField] private HPUITapEvent onTap = new HPUITapEvent();

        /// <summary>
        /// Gets or sets the <see cref="HPUITapEvent"/> that is invoked when a tap gesture is detected.
        /// </summary>
        public HPUITapEvent OnTap
        {
            get => onTap;
            set => onTap = value;
        }

        [Tooltip("Callback that is invoked when a double tap gesture is detected.")]
        [SerializeField] private HPUIDoubleTapEvent onDoubleTap = new HPUIDoubleTapEvent();

        /// <summary>
        /// Gets or sets the <see cref="HPUIDoubleTapEvent"/> that is invoked when a double tap gesture is detected.
        /// </summary>
        public HPUIDoubleTapEvent OnDoubleTap
        {
            get => onDoubleTap;
            set => onDoubleTap = value;
        }

        [Tooltip("Callback that is invoked when a long press gesture is detected.")]
        [SerializeField] private HPUILongPressEvent onLongPress = new HPUILongPressEvent();
        /// <summary>
        /// Gets or sets the <see cref="HPUILongPressEvent"/> that is invoked when a long press gesture is detected.
        /// </summary>
        public HPUILongPressEvent OnLongPress
        {
            get => onLongPress;
            set => onLongPress = value;
        }

        [Tooltip("Callback that is invoked when a flick gesture is detected.")]
        [SerializeField] private HPUIFlickEvent onFlick = new HPUIFlickEvent();
        /// <summary>
        /// Gets or sets the <see cref="HPUIFlickEvent"/> that is invoked when a flick gesture is detected.
        /// </summary>
        public HPUIFlickEvent OnFlick
        {
            get => onFlick;
            set => onFlick = value;
        }
        #endregion

        #region Parameters
        [Header("Tap Event Parameters")]

        [Tooltip("The maximum duration for a valid tap event.")]
        [SerializeField] private float tapEventDuration = 0.2f;

        [Tooltip("The duration after which a tap gesture times out if not completed. ")]
        [SerializeField] private float tapTimeoutDuration = 0.0035f;

        [Tooltip("The maximum distance the pointer can move for a tap to be considered valid.")]
        [SerializeField] private float tapDistanceLimit = 0.008f;

        private bool isGestureActive = false;
        private bool tapInvalid = false;
        private Coroutine tapTimeoutCoroutine;

        [Header("Double Tap Event Parameters")]

        [Tooltip("Maximum time allowed between two taps to be considered a double tap.")]
        [SerializeField] private float doubleTapInterval = 0.25f;

        private bool waitingForSecondTap = false;
        private Coroutine singleTapCoroutine;

        [Header("Long Press Event Parameters")]

        [Tooltip("The minimum duration for a long press gesture to be triggered.")]
        [SerializeField] private float longPressDuration = 0.4f;

        [Tooltip("The maximum distance the pointer can move for a long press to be considered valid.")]
        [SerializeField] private float longPressDistanceLimit = 0.01f;

        [Tooltip("The duration after which a long press gesture times out if not completed. ")]
        [SerializeField] private float longPressTimeoutDuration = 0.05f;

        private bool isLongPressGestureActive = false;
        private bool longPressInvalid = false;
        private bool longPressTriggered = false;
        private Coroutine longPressTimeoutCoroutine;

        [Header("Flick Event Parameters")]

        [Tooltip("The minimum distance the pointer must move for a flick gesture to be detected.")]
        [SerializeField] private float flickMinDistance = 0.09f;

        [Tooltip("The maximum duration allowed for the pointer movement to be considered a flick.")]
        [SerializeField] private float flickMaxDuration = 0.2f;

        [Tooltip("The duration after which the flick detection state is reset.")]
        [SerializeField] private float flickTimeout = 0.2f;

        private bool flickCandidateActive = false;
        private bool flickDetectionComplete = false;
        private Coroutine flickTimeoutRoutine;

        private IHPUIInteractable interactable;
        #endregion

        /// <summary>
        /// If true, enables verbose logging to the console for gesture detection processes.
        /// </summary>
        [SerializeField] private bool verboseLogging = false;

        #region UnityEvents
        private void OnEnable()
        {
            interactable = GetComponent<IHPUIInteractable>();
            interactable.GestureEvent.AddListener(DetectGesture);
        }

        private void OnDisable()
        {
            interactable.GestureEvent.RemoveListener(DetectGesture);
        }
        #endregion

        /// <summary>
        /// Sends gesture argument information to all individual detection modules
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        private void DetectGesture(HPUIGestureEventArgs args)
        {
            DetectTap(args);
            DetectLongPress(args);
            DetectFlick(args);
        }

        #region Tap Logic
        /// <summary>
        /// Handles incoming tap gesture events.
        /// Determines if the tap is valid based on duration and distance limits,
        /// and initiates the tap timeout coroutine.
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        private void DetectTap(HPUIGestureEventArgs args)
        {
            if (isGestureActive)
            {
                if (args.interactableObject != interactable)
                {
                    tapInvalid = true;
                    ResetTap();
                }
            }
            else
            {
                isGestureActive = true;
            }

            if (tapTimeoutCoroutine != null) StopCoroutine(tapTimeoutCoroutine);
            if (!tapInvalid) tapTimeoutCoroutine = StartCoroutine(TapTimeout(args, tapEventDuration, tapTimeoutDuration, tapDistanceLimit));
        }

        /// <summary>
        /// Coroutine that waits for a specified timeout duration to determine if a tap gesture is valid.
        /// If the gesture's duration and movement are within the defined limits, it proceeds to call <see cref="HandleTapCandidate"/>.
        /// Otherwise, the tap is rejected.
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        /// <param name="tapEventDuration">The maximum allowed duration for the tap event.</param>
        /// <param name="tapTimeoutDuration">The timeout period before evaluating the tap gesture.</param>
        /// <param name="tapDistanceLimit">The maximum allowed pointer movement for a valid tap.</param>
        /// <returns>An IEnumerator for the coroutine.</returns>
        private IEnumerator TapTimeout(HPUIGestureEventArgs args, float tapEventDuration, float tapTimeoutDuration, float tapDistanceLimit)
        {
            yield return new WaitForSeconds(tapTimeoutDuration);
            // Check if the gesture duration is within the tap event duration and if the cumulative pointer movement
            // is within the tap distance limit.
            if (args.TimeDelta < tapEventDuration &&
                args.CumulativeDirection.magnitude < tapDistanceLimit)
            {
                HandleTapCandidate(args);
            }
            else if (verboseLogging)
            {
                Debug.Log($"<b><color=#03f4fc>Tap</color></b> was <color=red><b>rejected</b></color>! \n TapInvalid: {tapInvalid} \n Duration: {args.TimeDelta}, \n Cumulative Direction: {args.CumulativeDirection.magnitude}");
            }
            ResetTap();
        }

        /// <summary>
        /// Resets the state variables related to tap detection, preparing for a new tap gesture.
        /// </summary>
        private void ResetTap()
        {
            isGestureActive = false;
            tapInvalid = false;
        }

        /// <summary>
        /// Handles a potential tap gesture. If a second tap occurs within the <see cref="doubleTapInterval"/>,
        /// a double tap is invoked. Otherwise, it starts a coroutine to detect a single tap.
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        private void HandleTapCandidate(HPUIGestureEventArgs args)
        {
            if (waitingForSecondTap &&
                interactable == args.interactableObject)
            {
                // Double tap confirmed
                if (singleTapCoroutine != null)
                    StopCoroutine(singleTapCoroutine);

                waitingForSecondTap = false;

                OnDoubleTap?.Invoke(args);
                if (verboseLogging)
                {
                    Debug.Log($"<b><color=#ffcc00>Double Tap</color></b> Invoked for Interactable {args.interactableObject.transform.name}");
                }
            }
            else
            {
                // First tap – wait to see if a second tap occurs
                waitingForSecondTap = true;
                singleTapCoroutine = StartCoroutine(SingleTapRoutine(args));
            }
        }

        /// <summary>
        /// Coroutine that waits for the <see cref="doubleTapInterval"/> to determine if a single tap should be invoked.
        /// If a second tap does not occur within this interval, the <see cref="OnTap"/> event is invoked.
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        /// <returns>An IEnumerator for the coroutine.</returns>
        private IEnumerator SingleTapRoutine(HPUIGestureEventArgs args)
        {
            yield return new WaitForSeconds(doubleTapInterval);

            if (waitingForSecondTap)
            {
                waitingForSecondTap = false;

                OnTap?.Invoke(args);
                if (verboseLogging)
                {
                    Debug.Log($"<b><color=#ffcc00>Double Tap</color></b> was <color=red><b>rejected</b></color>! \n TapInvalid: {tapInvalid} \n Waiting for Second Tap {waitingForSecondTap} \n Duration: {args.TimeDelta}, \n Cumulative Direction: {args.CumulativeDirection.magnitude}");
                    Debug.Log($"<b><color=#03f4fc>Tap</color></b> Invoked for Interactable {args.interactableObject.transform.name}");
                }
            }
        }
        #endregion

        #region Long Press Logic
        /// <summary>
        /// Handles incoming long press gesture events, detecting if the gesture meets the criteria for a long press.
        /// If the pointer is held down for longer than <see cref="longPressDuration"/> and moves less than <see cref="longPressDistanceLimit"/>,
        /// the <see cref="OnLongPress"/> event is invoked.
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        private void DetectLongPress(HPUIGestureEventArgs args)
        {
            if (isLongPressGestureActive)
            {
                if (args.interactableObject != interactable)
                {
                    longPressInvalid = true;
                    ResetLongPress();
                }
            }
            else
            {
                isLongPressGestureActive = true;
                longPressTriggered = false;
            }

            if (!longPressTriggered &&
                isLongPressGestureActive &&
                args.TimeDelta > longPressDuration &&
                args.CumulativeDirection.magnitude < longPressDistanceLimit)
            {
                longPressTriggered = true;
                OnLongPress?.Invoke(args);
                if (verboseLogging) Debug.Log($"<b><color=#5dff52>Long Press</color></b> Invoked for Interactable {args.interactableObject.transform.name}");
            }

            if (longPressTimeoutCoroutine != null) StopCoroutine(longPressTimeoutCoroutine);
            if (!longPressInvalid) longPressTimeoutCoroutine = StartCoroutine(LongPressTimer(args, longPressTimeoutDuration, longPressDuration));
        }

        /// <summary>
        /// Coroutine that waits for a specified timeout duration before resetting the long press state.
        /// This ensures that the long press detection is properly reset after a potential long press or its rejection.
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        /// <param name="longPressTimeoutDuration">The duration after which the long press gesture state is evaluated for reset.</param>
        /// <param name="longPressDuration">The minimum duration for a long press gesture to be triggered (used for logging).</param>
        /// <returns>An IEnumerator for the coroutine.</returns>
        private IEnumerator LongPressTimer(HPUIGestureEventArgs args, float longPressTimeoutDuration, float longPressDuration)
        {
            yield return new WaitForSeconds(longPressTimeoutDuration);
            if (verboseLogging && args.TimeDelta < longPressDuration)
            {
                Debug.Log($"<b><color=#5dff52>Long Press</color></b> was <color=red><b>rejected</b></color>! \n longPressInvalid: {tapInvalid} \n Duration: {args.TimeDelta}, \n Cumulative Direction: {args.CumulativeDirection.magnitude}");
            }
            ResetLongPress();
        }

        /// <summary>
        /// Resets the state variables related to long press detection, preparing for a new long press gesture.
        /// </summary>
        private void ResetLongPress()
        {
            isLongPressGestureActive = false;
            longPressInvalid = false;
            longPressTriggered = false;
        }
        #endregion

        #region Flick Logic
        /// <summary>
        /// Handles incoming gesture events to detect a flick gesture.
        /// A flick is detected if the pointer moves a minimum distance within a maximum duration.
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        private void DetectFlick(HPUIGestureEventArgs args)
        {
            if (!flickCandidateActive)
            {
                flickCandidateActive = true;
                return;
            }

            if (!flickDetectionComplete && args.TimeDelta > flickMaxDuration)
            {
                flickDetectionComplete = true;
                Debug.Log($"<b><color=#ff4dff>Flick</color></b> was <color=red><b>rejected</b></color>. Cumulative direction: {args.CumulativeDirection.magnitude}, Time: {args.TimeDelta}");
            }

            if (!flickDetectionComplete && args.CumulativeDirection.magnitude >= flickMinDistance)
            {
                Vector2 direction = args.CumulativeDirection.normalized;

                OnFlick?.Invoke(direction, args);

                if (verboseLogging)
                {
                    Debug.Log($"<b><color=#ff4dff>Flick</color></b> on {interactable.transform.name}; with direction: {direction}");
                }

                flickDetectionComplete = true;
            }

            if (flickTimeoutRoutine != null) StopCoroutine(flickTimeoutRoutine);
            flickTimeoutRoutine = StartCoroutine(FlickReset(flickTimeout));
        }

        private IEnumerator FlickReset(float flickTimeout)
        {
            yield return new WaitForSeconds(flickTimeout);
            flickDetectionComplete = false;
            flickCandidateActive = false;
        }
        #endregion
    }

    /// <summary>
    /// Represents a tap gesture event within the HPUI system.
    /// Inherits from <see cref="HPUIGestureEvent"/>.
    /// </summary>
    [Serializable]
    public class HPUITapEvent : HPUIGestureEvent
    { }

    /// <summary>
    /// Represents a double tap gesture event within the HPUI system.
    /// Inherits from <see cref="HPUIGestureEvent"/>.
    /// </summary>
    [Serializable]
    public class HPUIDoubleTapEvent : HPUIGestureEvent
    { }

    /// <summary>
    /// Represents a long press gesture event within the HPUI system.
    /// Inherits from <see cref="HPUIGestureEvent"/>.
    /// </summary>
    [Serializable]
    public class HPUILongPressEvent : HPUIGestureEvent
    { }

    /// <summary>
    /// Represents a flick gesture event within the HPUI system.
    /// This event provides the direction of the flick as a <see cref="Vector2"/> and the <see cref="HPUIGestureEventArgs"/>.
    /// </summary>
    [Serializable]
    public class HPUIFlickEvent : UnityEvent<Vector2, HPUIGestureEventArgs>
    { }
}
