using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace _Scripts.Keyboard
{
    /// <summary>
    /// Bridges gesture recognition output to a TextMeshPro field for swipe typing.
    /// Subscribes to <see cref="KeyboardGestureSmoother.OnGestureTrajectory"/>, sends
    /// the trajectory to <see cref="SwipeRecognizer"/>, and appends the top result to
    /// the TMP text.
    /// </summary>
    public class KeyboardOutput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Source of completed gesture trajectories.")]
        private KeyboardGestureSmoother gestureSmoother;

        [SerializeField, Tooltip("Swipe recognition server client.")]
        private SwipeRecognizer swipeRecognizer;

        [SerializeField, Tooltip("TextMeshPro field to output recognized words into.")]
        private TMP_Text outputText;

        private void Start()
        {
            if (outputText != null)
                outputText.text = "";
        }

        private void OnEnable()
        {
            if (gestureSmoother != null)
                gestureSmoother.OnGestureTrajectory.AddListener(HandleGestureTrajectory);
        }

        private void OnDisable()
        {
            if (gestureSmoother != null)
                gestureSmoother.OnGestureTrajectory.RemoveListener(HandleGestureTrajectory);
        }

        private void HandleGestureTrajectory(List<Vector2> trajectory)
        {
            if (trajectory == null || trajectory.Count < 2)
                return;

            // Snapshot current text before the async call
            string currentText = outputText != null ? outputText.text : "";

            // Convert Vector2 trajectory → SwipePoints with roughly 60 fps timestamps
            List<SwipePoint> points = new List<SwipePoint>(trajectory.Count);
            const float delta = 1f / 60f;
            for (int i = 0; i < trajectory.Count; i++)
            {
                points.Add(new SwipePoint(trajectory[i].x, trajectory[i].y, i * delta));
            }

            // Pass current TMP text as context for the language model
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
                },
                onError: error =>
                {
                    Debug.LogError($"Swipe recognition error: {error}");
                }
            );
        }
    }
}
