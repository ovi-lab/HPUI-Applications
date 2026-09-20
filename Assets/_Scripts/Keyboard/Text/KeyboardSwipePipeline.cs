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
    /// from a <see cref="FingerRowCapture"/>, gates gesture start on sustained
    /// contact (alpha), stitches momentary contact losses (beta), owns the
    /// gesture state machine, applies One Euro filtering, accumulates the
    /// filtered trajectory, and sends completed gestures to the <see cref="SwipeRecognizer"/>
    /// for word recognition.
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
        [SerializeField, Tooltip("Minimum contact duration (s) before a contact registers as a gesture; shorter contacts are discarded as transients.")]
        private float alpha = 0.15f;

        [SerializeField, Tooltip("Maximum contact loss (s) stitched into one ongoing gesture; longer losses end the gesture.")]
        private float beta = 0.3f;

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

        [Tooltip("Broadcasts the complete filtered gesture trajectory when the gesture completes.")]
        public UnityEvent<List<Vector2>> OnGestureCompleted;

        public UnityEvent OnGestureStarted;
        public UnityEvent OnGestureCancelled;

        [Tooltip("Fired when swipe recognition returns a word.")]
        public UnityEvent<string> OnWordRecognized;

        /// <summary>
        /// Minimum contact duration (s) before a contact registers as a gesture.
        /// Contacts shorter than this are discarded as transients.
        /// Applied live; safe to change mid-gesture.
        /// </summary>
        public float Alpha
        {
            get => alpha;
            set { alpha = Mathf.Max(0f, value); gestureState?.UpdateThresholds(alpha, beta); }
        }

        /// <summary>
        /// Maximum contact loss (s) stitched into one ongoing gesture.
        /// Longer losses end the gesture. Applied live; safe to change mid-gesture.
        /// </summary>
        public float Beta
        {
            get => beta;
            set { beta = Mathf.Max(0f, value); gestureState?.UpdateThresholds(alpha, beta); }
        }

        private GestureStateMachine gestureState;
        private OneEuroFilter<Vector2> positionFilter;
        private List<Vector2> gestureTrajectory;

        // Contact-level filtering state: alpha gates gesture start on sustained
        // contact; beta stitches momentary losses by holding the last position.
        private enum ContactState { Idle, Gating, Confirmed }
        private ContactState contactState = ContactState.Idle;
        private float contactElapsed;
        private float sinceLastInput;
        private bool inputThisFrame;
        private Vector2 lastRawPosition;
        private readonly List<Vector2> pendingContactBuffer = new();

        private void Awake()
        {
            gestureTrajectory = new List<Vector2>();
            gestureState = new GestureStateMachine(alpha, beta);

            gestureState.OnGestureStarted += HandleGestureStarted;
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

            contactState = ContactState.Idle;
            pendingContactBuffer.Clear();

            // Cancel any gesture left ongoing by a mid-gesture disable so a
            // re-enable can't complete the stale trajectory.
            gestureState?.Cancel();
        }

        private void Update()
        {
            sinceLastInput += Time.deltaTime;

            switch (contactState)
            {
                case ContactState.Gating:
                    if (sinceLastInput >= beta)
                    {
                        // Contact ended before alpha elapsed - discard as transient.
                        contactState = ContactState.Idle;
                        pendingContactBuffer.Clear();
                    }
                    break;
                case ContactState.Confirmed:
                    if (sinceLastInput >= beta)
                    {
                        // Loss longer than beta ends the contact; resolve the gesture now.
                        contactState = ContactState.Idle;
                        gestureState.EndIfOngoing();
                    }
                    else if (!inputThisFrame)
                    {
                        HoldPosition();
                    }
                    break;
            }

            gestureState.Tick(Time.deltaTime);

            inputThisFrame = false;
        }

        /// <summary>
        /// Called every frame a finger is tracking. Contacts shorter than
        /// <see cref="alpha"/> are buffered and discarded; confirmed contacts
        /// report to the state machine, filter the position, accumulate the
        /// trajectory, and broadcast the filtered cursor position.
        /// </summary>
        private void HandleRawPosition(Vector2 rawPosition)
        {
            Debug.Assert(float.IsFinite(rawPosition.x) && float.IsFinite(rawPosition.y),
                $"Non-finite raw position: {rawPosition}");
            lastRawPosition = rawPosition;
            sinceLastInput = 0f;
            inputThisFrame = true;

            switch (contactState)
            {
                case ContactState.Idle:
                    contactState = ContactState.Gating;
                    contactElapsed = 0f;
                    pendingContactBuffer.Clear();
                    pendingContactBuffer.Add(rawPosition);
                    break;
                case ContactState.Gating:
                    contactElapsed += Time.deltaTime;
                    pendingContactBuffer.Add(rawPosition);
                    if (contactElapsed >= alpha)
                        ConfirmContact();
                    break;
                case ContactState.Confirmed:
                    Feed(rawPosition);
                    break;
            }
        }

        /// <summary>
        /// Promotes a gated contact to confirmed: replays the buffered positions
        /// through the normal path so a real gesture keeps its true start.
        /// </summary>
        private void ConfirmContact()
        {
            contactState = ContactState.Confirmed;
            foreach (Vector2 buffered in pendingContactBuffer)
                Feed(buffered);
            pendingContactBuffer.Clear();
        }

        /// <summary>
        /// Reports input to the state machine, filters the position, accumulates
        /// the filtered trajectory, and broadcasts the filtered cursor position.
        /// </summary>
        private void Feed(Vector2 rawPosition)
        {
            gestureState.ReportInput();

            if (positionFilter == null)
                return;

            Vector2 filtered = positionFilter.Filter(rawPosition);
            gestureTrajectory.Add(filtered);
            OnCursorPosition?.Invoke(filtered);
        }

        /// <summary>
        /// Stitches a contact loss shorter than beta by holding the last known
        /// position: the state machine and trajectory stay continuous.
        /// </summary>
        private void HoldPosition()
        {
            gestureState.ReportInput();

            if (positionFilter == null)
                return;

            Vector2 filtered = positionFilter.Filter(lastRawPosition);
            gestureTrajectory.Add(filtered);
            OnCursorPosition?.Invoke(filtered);
        }

        private void HandleGestureStarted()
        {
            positionFilter = new OneEuroFilter<Vector2>(filterFrequency, minCutoff, filterBeta, dCutoff);
            gestureTrajectory.Clear();
            OnGestureStarted?.Invoke();
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
