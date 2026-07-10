using System.Collections.Generic;
using _Scripts.HPUI;
using TMPro;
using ubco.ovilab.ViconUnityStream;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.Keyboard.Text
{
    /// <summary>
    /// Central pipeline for keyboard swipe gestures. Subscribes to raw positions
    /// from a <see cref="FingerRowCapture"/>, owns the gesture state machine,
    /// applies One Euro filtering, accumulates the trajectory, and sends completed
    /// gestures to the <see cref="SwipeRecognizer"/> for word recognition.
    ///
    /// Events are the primary output surface — visualizers, debug displays, and
    /// the future <c>TextCompositionController</c> all consume from here.
    /// </summary>
    public class KeyboardSwipePipeline : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField, Tooltip("Source of raw normalized positions from HPUI finger rows.")]
        private FingerRowCapture fingerRowCapture;

        [Header("Gesture State Parameters")]
        [SerializeField, Tooltip("Minimum gesture duration (s) to count as valid.")]
        private float alpha = 0.15f;

        [SerializeField, Tooltip("Idle time (s) before a gesture is considered finished.")]
        private float beta = 0.3f;

        [SerializeField, Tooltip("Enable premature trigger for short/button-like gestures.")]
        private bool enablePrematureTrigger;

        [SerializeField, Tooltip("Duration (s) of continuous input before a premature trigger fires.")]
        private float prematureTriggerTime = 0.1f;

        [Header("One Euro Filter Parameters")]
        [SerializeField, Tooltip("Sampling frequency (Hz).")]
        private float filterFrequency = 72f;

        [SerializeField, Tooltip("Minimum cutoff frequency (Hz). Lower = smoother but more lag.")]
        private float minCutoff = 1.0f;

        [SerializeField, Tooltip("Speed coefficient. Higher = less lag during fast movement.")]
        private float filterBeta = 0.0f;

        [SerializeField, Tooltip("Cutoff frequency for the derivative filter (Hz).")]
        private float dCutoff = 1.0f;

        [Header("Recognition & Output")]
        [SerializeField, Tooltip("Swipe recognition server client.")]
        private SwipeRecognizer swipeRecognizer;

        [SerializeField, Tooltip("TextMeshPro field to output recognized words into.")]
        private TMP_Text outputText;

        [Header("Events")]
        [Tooltip("Broadcasts the filtered cursor position every input frame.")]
        public UnityEvent<Vector2> OnCursorPosition;

        [Tooltip("Broadcasts the complete raw gesture trajectory when the gesture completes.")]
        public UnityEvent<List<Vector2>> OnGestureCompleted;

        public UnityEvent OnGestureStarted;
        public UnityEvent OnGestureCancelled;

        [Tooltip("Fired when a short/button-like gesture triggers before the full gesture times out.")]
        public UnityEvent OnPrematureTrigger;

        [Tooltip("Fired when swipe recognition returns a word.")]
        public UnityEvent<string> OnWordRecognized;

        private GestureStateMachine gestureState;
        private OneEuroFilter<Vector2> positionFilter;
        private List<Vector2> gestureTrajectory;

        private void Awake()
        {
            gestureTrajectory = new List<Vector2>();

            float? prematureTrigger = enablePrematureTrigger ? prematureTriggerTime : (float?)null;
            gestureState = new GestureStateMachine(alpha, beta, prematureTrigger);

            gestureState.OnGestureStarted += HandleGestureStarted;
            gestureState.OnPrematureTriggerReached += HandlePrematureTrigger;
            gestureState.OnGestureCompleted += HandleGestureCompleted;
            gestureState.OnGestureCancelled += HandleGestureCancelled;
        }

        private void Start()
        {
            if (outputText != null)
                outputText.text = "";
        }

        private void OnEnable()
        {
            if (fingerRowCapture != null)
                fingerRowCapture.OnRawPosition.AddListener(HandleRawPosition);
        }

        private void OnDisable()
        {
            if (fingerRowCapture != null)
                fingerRowCapture.OnRawPosition.RemoveListener(HandleRawPosition);
        }

        private void Update()
        {
            gestureState.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Called every frame a finger is tracking. Reports input to the state
        /// machine, filters the position, accumulates the trajectory, and
        /// broadcasts the filtered cursor position.
        /// </summary>
        private void HandleRawPosition(Vector2 rawPosition)
        {
            gestureState.ReportInput();

            if (positionFilter == null)
                return;

            Vector2 filtered = positionFilter.Filter(rawPosition);
            gestureTrajectory.Add(rawPosition);
            OnCursorPosition?.Invoke(filtered);
        }

        private void HandleGestureStarted()
        {
            positionFilter = new OneEuroFilter<Vector2>(filterFrequency, minCutoff, filterBeta, dCutoff);
            gestureTrajectory.Clear();
            OnGestureStarted?.Invoke();
        }

        private void HandlePrematureTrigger()
        {
            OnPrematureTrigger?.Invoke();
        }

        private void HandleGestureCompleted()
        {
            List<Vector2> trajectory = gestureTrajectory.Count > 0
                ? new List<Vector2>(gestureTrajectory)
                : null;

            gestureTrajectory.Clear();
            positionFilter = null;

            if (trajectory != null && trajectory.Count >= 2)
            {
                OnGestureCompleted?.Invoke(trajectory);
                RecognizeTrajectory(trajectory);
            }
        }

        private void HandleGestureCancelled()
        {
            gestureTrajectory.Clear();
            positionFilter = null;
            OnGestureCancelled?.Invoke();
        }

        /// <summary>
        /// Converts the raw trajectory to SwipePoints and sends it to the
        /// recognition server. Results are appended to the TMP output field
        /// and broadcast via <see cref="OnWordRecognized"/>.
        /// </summary>
        private void RecognizeTrajectory(List<Vector2> trajectory)
        {
            if (swipeRecognizer == null)
                return;

            string currentText = outputText != null ? outputText.text : "";

            List<SwipePoint> points = new List<SwipePoint>(trajectory.Count);
            const float delta = 1f / 60f;
            for (int i = 0; i < trajectory.Count; i++)
            {
                points.Add(new SwipePoint(trajectory[i].x, trajectory[i].y, i * delta));
            }

            swipeRecognizer.ContextWords = currentText.Trim();

            swipeRecognizer.RecognizeSwipe(
                points,
                onSuccess: results =>
                {
                    if (results == null || results.Count == 0)
                        return;

                    int count = Mathf.Min(results.Count, 5);
                    Debug.Log($"Swipe top {count}: {string.Join(", ", results.GetRange(0, count))}");

                    string topWord = results[0];
                    if (outputText != null)
                        outputText.text = currentText + topWord + " ";

                    OnWordRecognized?.Invoke(topWord);
                },
                onError: error =>
                {
                    Debug.LogError($"Swipe recognition error: {error}");
                }
            );
        }
    }
}
