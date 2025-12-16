using System;
using System.Collections;
using ubco.ovilab.HPUI.Core.Interaction;
using UnityEngine;
using UnityEngine.Events;
namespace _Scripts
{
    /// <summary>
    /// Detects and manages various HPUI gestures such as Tap, Double Tap, and Long Press.
    /// This script attaches to a GameObject and listens for gesture events from HPUI interactables
    /// to process and dispatch specific gesture events.
    /// </summary>
    public class HPUIGestureDetector : MonoBehaviour
    {
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
        private IHPUIInteractable currentInteractable = null;
        private Coroutine tapTimeoutCoroutine;

        [Header("Double Tap Event Parameters")]

        [Tooltip("Maximum time allowed between two taps to be considered a double tap.")]
        [SerializeField] private float doubleTapInterval = 0.25f;

        private bool waitingForSecondTap = false;
        private Coroutine singleTapCoroutine;
        private IHPUIInteractable lastTapInteractable;

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
        private IHPUIInteractable currentLongPressInteractable = null;
        private Coroutine longPressTimeoutCoroutine;

        [Header("Flick Event Parameters")]

        [SerializeField] private float flickMinDistance = 0.09f;
        [SerializeField] private float flickMaxDuration = 0.2f;
        [SerializeField] private float flickTimeout = 0.2f;

        private bool flickCandidateActive = false;
        private IHPUIInteractable flickInitialInteractable = null;
        private bool flickDetectionComplete = false;
        private Coroutine flickTimeoutRoutine;

        private HPUIBaseInteractable[] interactables;
        #endregion

        #region Events
        [Header("Events")]

        [SerializeField] private HPUITapEvent tapEvent = new HPUITapEvent();
        /// <summary>
        /// Gets or sets the <see cref="HPUITapEvent"/> that is invoked when a tap gesture is detected.
        /// </summary>
        public HPUITapEvent TapEvent
        {
            get => tapEvent;
            set => tapEvent = value;
        }

        [SerializeField] private HPUIDoubleTapEvent doubleTapEvent = new HPUIDoubleTapEvent();
        /// <summary>
        /// Gets or sets the <see cref="HPUIDoubleTapEvent"/> that is invoked when a double tap gesture is detected.
        /// </summary>
        public HPUIDoubleTapEvent DoubleTapEvent
        {
            get => doubleTapEvent;
            set => doubleTapEvent = value;
        }

        [SerializeField] private HPUILongPressEvent longPressEvent = new HPUILongPressEvent();
        /// <summary>
        /// Gets or sets the <see cref="HPUILongPressEvent"/> that is invoked when a long press gesture is detected.
        /// </summary>
        public HPUILongPressEvent LongPressEvent
        {
            get => longPressEvent;
            set => longPressEvent = value;
        }

        [SerializeField] private HPUIFlickEvent flickEvent = new HPUIFlickEvent();
        public HPUIFlickEvent FlickEvent
        {
            get => flickEvent;
            set => flickEvent = value;
        }
        #endregion

        [SerializeField] private bool verboseLogging = false;

        #region UnityEvents
        /// <summary>
        /// Called when the object becomes enabled and active.
        /// Subscribes to gesture events from all <see cref="HPUIBaseInteractable"/> objects in the scene.
        /// </summary>
        private void OnEnable()
        {
            interactables = UnityEngine.Object.FindObjectsByType<HPUIBaseInteractable>(FindObjectsSortMode.None);
            foreach (var interactable in interactables)
            {
                interactable.GestureEvent.AddListener(OnTap);
                interactable.GestureEvent.AddListener(OnLongPress);
                interactable.GestureEvent.AddListener(DetectFlick);
            }
        }

        private void OnDisable()
        {
            foreach (var interactable in interactables)
            {
                interactable.GestureEvent.RemoveListener(OnTap);
                interactable.GestureEvent.RemoveListener(OnLongPress);
                interactable.GestureEvent.RemoveListener(DetectFlick);
            }
        }
        #endregion

        #region Tap Logic
        /// <summary>
        /// Handles incoming tap gesture events.
        /// Determines if the tap is valid based on duration and distance limits,
        /// and initiates the tap timeout coroutine.
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        private void OnTap(HPUIGestureEventArgs args)
        {
            if (isGestureActive)
            {
                if (args.interactableObject != currentInteractable)
                {
                    tapInvalid = true;
                    ResetTap();
                }
            }
            else
            {
                currentInteractable = args.interactableObject;
                isGestureActive = true;
            }

            if (tapTimeoutCoroutine != null) StopCoroutine(tapTimeoutCoroutine);
            if (!tapInvalid) tapTimeoutCoroutine = StartCoroutine(TapTimeout(args, tapEventDuration, tapTimeoutDuration, tapDistanceLimit));
        }

        /// <summary>
        /// Coroutine that waits for a specified timeout duration to determine if a tap gesture is valid.
        /// If valid, it calls <see cref="HandleTapCandidate"/>.
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        /// <param name="tapEventDuration">The maximum duration for a valid tap event.</param>
        /// <param name="tapTimeoutDuration">The duration after which a tap gesture times out.</param>
        /// <param name="tapDistanceLimit">The maximum distance the pointer can move for a tap to be considered valid.</param>
        /// <returns>An IEnumerator for the coroutine.</returns>
        private IEnumerator TapTimeout(HPUIGestureEventArgs args, float tapEventDuration, float tapTimeoutDuration, float tapDistanceLimit)
        {
            yield return new WaitForSeconds(tapTimeoutDuration);
            // checking if the duration is greater than the minimum duration
            // the total distance and total magnitude is lesser than the distance tapDistanceLimit
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
            currentInteractable = null;
        }

        /// <summary>
        /// Handles a potential tap gesture. If a second tap occurs within the <see cref="doubleTapInterval"/>,
        /// a double tap is invoked. Otherwise, it starts a coroutine to detect a single tap.
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        private void HandleTapCandidate(HPUIGestureEventArgs args)
        {
            if (waitingForSecondTap &&
                lastTapInteractable == args.interactableObject)
            {
                // Double tap confirmed
                if (singleTapCoroutine != null)
                    StopCoroutine(singleTapCoroutine);

                waitingForSecondTap = false;
                lastTapInteractable = null;

                DoubleTapEvent?.Invoke(args);
                if (verboseLogging)
                {
                    Debug.Log($"<b><color=#ffcc00>Double Tap</color></b> Invoked for Interactable {args.interactableObject.transform.name}");
                }
            }
            else
            {
                // First tap – wait to see if a second tap occurs
                waitingForSecondTap = true;
                lastTapInteractable = args.interactableObject;

                singleTapCoroutine = StartCoroutine(SingleTapRoutine(args));
            }
        }

        /// <summary>
        /// Coroutine that waits for a specified interval to determine if a single tap should be invoked.
        /// If no second tap occurs within the <see cref="doubleTapInterval"/>, the single tap event is invoked.
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        /// <returns>An IEnumerator for the coroutine.</returns>
        private IEnumerator SingleTapRoutine(HPUIGestureEventArgs args)
        {
            yield return new WaitForSeconds(doubleTapInterval);

            if (waitingForSecondTap)
            {
                waitingForSecondTap = false;
                lastTapInteractable = null;

                TapEvent?.Invoke(args);
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
        /// Handles incoming long press gesture events.
        /// Determines if a long press is triggered based on duration and distance limits,
        /// and invokes the long press event.
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        private void OnLongPress(HPUIGestureEventArgs args)
        {
            if (isLongPressGestureActive)
            {
                if (args.interactableObject != currentLongPressInteractable)
                {
                    longPressInvalid = true;
                    ResetLongPress();
                }
            }
            else
            {
                currentLongPressInteractable = args.interactableObject;
                isLongPressGestureActive = true;
                longPressTriggered = false;
            }

            if (!longPressTriggered &&
                isLongPressGestureActive &&
                args.TimeDelta > longPressDuration &&
                args.CumulativeDirection.magnitude < longPressDistanceLimit)
            {
                longPressTriggered = true;
                LongPressEvent?.Invoke(args);
                if (verboseLogging) Debug.Log($"<b><color=#5dff52>Long Press</color></b> Invoked for Interactable {args.interactableObject.transform.name}");
            }

            if (longPressTimeoutCoroutine != null) StopCoroutine(longPressTimeoutCoroutine);
            if (!longPressInvalid) longPressTimeoutCoroutine = StartCoroutine(LongPressTimer(args, longPressTimeoutDuration, longPressDuration));
        }

        /// <summary>
        /// Coroutine that waits for a specified timeout duration to reset the long press state.
        /// </summary>
        /// <param name="args">The <see cref="HPUIGestureEventArgs"/> containing information about the gesture.</param>
        /// <param name="longPressTimeoutDuration">The duration after which a long press gesture times out.</param>
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
            currentLongPressInteractable = null;
        }
        #endregion

        private void DetectFlick(HPUIGestureEventArgs args)
        {
            if (!flickCandidateActive)
            {
                flickCandidateActive = true;
                flickInitialInteractable = args.interactableObject;
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

                FlickEvent?.Invoke(args, flickInitialInteractable, direction);

                if (verboseLogging)
                {
                    Debug.Log($"<b><color=#ff4dff>Flick</color></b> on {flickInitialInteractable.transform.name}; with direction: {direction}");
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
            flickInitialInteractable = null;
            flickCandidateActive = false;
        }
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

    [Serializable]
    public class HPUIFlickEvent : UnityEvent<HPUIGestureEventArgs, IHPUIInteractable, Vector2>
    { }
}
