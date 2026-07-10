using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace _Scripts.Keyboard.Text
{
    /// <summary>
    /// Unity client for the FUTO Swipe Library HTTP server (swipesrv)
    /// Communicates with the server running on WSL to recognize swipe patterns
    /// For more information, check out https://gitlab.futo.org/keyboard/swipe-library
    /// and https://huggingface.co/futo-org/futo-swipe
    /// </summary>
    public class SwipeRecognizer : MonoBehaviour
    {
        [Header("Server Configuration")]
        [Tooltip("WSL IP address (run 'ip addr show eth0' in WSL to find it)")]
        [SerializeField]
        private string serverIP = "172.21.218.69";

        [Tooltip("Port number the swipesrv is listening on")]
        [SerializeField]
        private int serverPort = 8012;

        [Tooltip("Context words for language model (space-separated)")]
        [SerializeField]
        private string contextWords = "";

        /// <summary>
        /// Public accessor so external scripts can set context before recognition.
        /// </summary>
        public string ContextWords { get => contextWords; set => contextWords = value; }

        [SerializeField]
        [Tooltip("Debug Performance")]
        private bool showPerf = false;

        private string serverUrl;
        private HttpClient httpClient;
        private float startTime;

        void Start()
        {
            serverUrl = $"http://{serverIP}:{serverPort}";
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// Recognize a swipe pattern from normalized coordinates
        /// </summary>
        /// <param name="points">List of swipe points with x, y, and timestamp</param>
        /// <param name="onSuccess">Callback with list of recognized words</param>
        /// <param name="onError">Callback with error message</param>
        public void RecognizeSwipe(List<SwipePoint> points, Action<List<string>> onSuccess, Action<string> onError = null)
        {
            if (showPerf)
            {
                startTime = Time.realtimeSinceStartup;
            }
            StartCoroutine(RecognizeSwipeCoroutine(points, onSuccess, onError));
        }

        private IEnumerator RecognizeSwipeCoroutine(List<SwipePoint> points, Action<List<string>> onSuccess, Action<string> onError)
        {
            if (points == null || points.Count < 2)
            {
                onError?.Invoke("Need at least 2 points");
                yield break;
            }

            // Build the request body in the format swipesrv expects
            string requestBody = BuildRequestBody(points, contextWords);

            // Use UnityWebRequest for better Unity integration
            using (UnityWebRequest request = new UnityWebRequest($"{serverUrl}/recognize", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "text/plain");

                // Send the request
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string error = request.error ?? "Unknown error";
                    Debug.LogError($"Swipe recognition failed: {error}");
                    onError?.Invoke(error);
                }
                else
                {
                    string response = request.downloadHandler.text;
                    List<string> results = ParseResponse(response);

                    float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
                    if (showPerf)
                    {
                        Debug.Log($"SwipeRecognizer: RecognizeSwipe completed in {elapsedMs:F2} ms");
                    }

                    onSuccess?.Invoke(results);
                }
            }
        }

        /// <summary>
        /// Check if the server is healthy
        /// </summary>
        public IEnumerator CheckHealth(Action<bool> onResult)
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{serverUrl}/health"))
            {
                yield return request.SendWebRequest();

                bool isHealthy = request.result == UnityWebRequest.Result.Success && request.downloadHandler.text == "meow";
                onResult?.Invoke(isHealthy);
            }
        }

        private string BuildRequestBody(List<SwipePoint> points, string context)
        {
            StringBuilder sb = new StringBuilder();

            // First line: context words (space-separated)
            if (!string.IsNullOrWhiteSpace(context))
            {
                sb.AppendLine(context.Trim());
            }
            else
            {
                sb.AppendLine(""); // Empty context
            }

            // Subsequent lines: [x.xx; y.yy]t.tt
            // Normalize timestamps to start from 0
            float firstTime = points[0].timestamp;

            foreach (var point in points)
            {
                float normalizedTime = (point.timestamp - firstTime) * 1000f; // Convert to milliseconds
                sb.AppendLine($"[{point.x:F4}; {point.y:F4}]{normalizedTime:F2}");
            }

            return sb.ToString();
        }

        private List<string> ParseResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return new List<string>();

            // Response is comma-separated words
            string[] words = response.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> results = new List<string>();

            foreach (string word in words)
            {
                string trimmed = word.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    results.Add(trimmed);
                }
            }

            return results;
        }

        void OnDestroy()
        {
            httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Represents a point in a swipe gesture
    /// </summary>
    [Serializable]
    public class SwipePoint
    {
        public float x; // Normalized x coordinate (0.0 to 1.0)
        public float y; // Normalized y coordinate (0.0 to 1.0)
        public float timestamp; // Timestamp in seconds

        public SwipePoint(float x, float y, float timestamp)
        {
            this.x = x;
            this.y = y;
            this.timestamp = timestamp;
        }
    }
}
