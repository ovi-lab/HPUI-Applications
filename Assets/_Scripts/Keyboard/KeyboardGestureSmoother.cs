using System.Collections.Generic;
using ubco.ovilab.ViconUnityStream;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.Keyboard
{
    /// <summary>
    /// Captures raw gesture input from <see cref="KeyboardInputCapture"/>, smooths it
    /// through a One Euro filter, and broadcasts the filtered cursor position in real
    /// time. When the gesture ends, the full raw trajectory is emitted via an event.
    /// A fresh filter is created for every new gesture so old data never leaks across.
    /// </summary>
    public class KeyboardGestureSmoother : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField, Tooltip("Source of raw gesture input and gesture lifecycle events.")]
        private KeyboardInputCapture inputCapture;

        [Header("One Euro Filter Parameters")]
        [SerializeField, Tooltip("Sampling frequency (Hz).")]
        private float filterFrequency = 72f;

        [SerializeField, Tooltip("Minimum cutoff frequency (Hz). Lower = smoother but more lag.")]
        private float minCutoff = 1.0f;

        [SerializeField, Tooltip("Speed coefficient. Higher = less lag during fast movement.")]
        private float beta = 0.0f;

        [SerializeField, Tooltip("Cutoff frequency for the derivative filter (Hz).")]
        private float dCutoff = 1.0f;

        [Header("Events")]
        [Tooltip("Broadcasts the smoothed cursor position every input frame.")]
        public UnityEvent<Vector2> OnFilteredPosition;

        [Tooltip("Broadcasts the complete raw gesture trajectory when the gesture completes.")]
        public UnityEvent<List<Vector2>> OnGestureTrajectory;

        private OneEuroFilter<Vector2> positionFilter;
        private List<Vector2> gestureTrajectory;

        private void Awake()
        {
            gestureTrajectory = new List<Vector2>();
        }

        private void OnEnable()
        {
            if (inputCapture != null)
            {
                inputCapture.OnKeyboardInputCapture.AddListener(HandleInput);
                inputCapture.OnGestureStarted.AddListener(HandleGestureStarted);
                inputCapture.OnGestureCompleted.AddListener(HandleGestureCompleted);
                inputCapture.OnGestureCancelled.AddListener(HandleGestureCancelled);
            }
        }

        private void OnDisable()
        {
            if (inputCapture != null)
            {
                inputCapture.OnKeyboardInputCapture.RemoveListener(HandleInput);
                inputCapture.OnGestureStarted.RemoveListener(HandleGestureStarted);
                inputCapture.OnGestureCompleted.RemoveListener(HandleGestureCompleted);
                inputCapture.OnGestureCancelled.RemoveListener(HandleGestureCancelled);
            }
        }

        private void HandleInput(Vector2 rawPosition)
        {
            // Only process when a gesture is actually active
            if (positionFilter == null)
                return;

            Vector2 filtered = positionFilter.Filter(rawPosition);

            gestureTrajectory.Add(rawPosition);
            OnFilteredPosition?.Invoke(filtered);
        }

        /// <summary>
        /// Create a brand-new filter for each gesture so that no internal state
        /// (velocity estimates, previous values, etc.) carries over.
        /// </summary>
        private void HandleGestureStarted()
        {
            positionFilter = new OneEuroFilter<Vector2>(filterFrequency, minCutoff, beta, dCutoff);
            gestureTrajectory.Clear();
        }

        private void HandleGestureCompleted()
        {
            if (gestureTrajectory.Count > 0)
            {
                OnGestureTrajectory?.Invoke(new List<Vector2>(gestureTrajectory));
            }

            gestureTrajectory.Clear();
            positionFilter = null;
        }

        private void HandleGestureCancelled()
        {
            gestureTrajectory.Clear();
            positionFilter = null;
        }
    }
}
