using System.Collections;
using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;

/// <summary>
/// Example usage of SwipeRecognizer for testing
/// Attach this to a GameObject in your scene
/// </summary>
public class SwipeRecognizerExample : MonoBehaviour
{
    [Header("References")]
    public SwipeRecognizer swipeRecognizer;

    [Header("Test Settings")]
    public bool runTestOnStart = false;

    void Start()
    {
        if (swipeRecognizer == null)
        {
            swipeRecognizer = GetComponent<SwipeRecognizer>();
        }

        if (runTestOnStart)
        {
            StartCoroutine(RunTest());
        }
    }

    /// <summary>
    /// Example: Simulate a swipe typing "hello"
    /// </summary>
    [Button]
    public void TestSwipeHello()
    {
        // Create a simulated swipe for "hello" on QWERTY layout
        // These are example normalized coordinates (0-1 range)
        List<SwipePoint> swipePoints = new List<SwipePoint>
        {
            new SwipePoint(0.1f, 0.3f, 0.0f), // Start near 'h'
            new SwipePoint(0.15f, 0.35f, 0.05f),
            new SwipePoint(0.4f, 0.5f, 0.1f), // Swipe through 'e'
            new SwipePoint(0.45f, 0.55f, 0.15f),
            new SwipePoint(0.5f, 0.3f, 0.2f), // Swipe through 'l'
            new SwipePoint(0.55f, 0.35f, 0.25f),
            new SwipePoint(0.5f, 0.3f, 0.3f), // Swipe through 'l' again
            new SwipePoint(0.55f, 0.35f, 0.35f),
            new SwipePoint(0.8f, 0.3f, 0.4f), // End at 'o'
            new SwipePoint(0.85f, 0.35f, 0.45f),
        };

        swipeRecognizer.RecognizeSwipe(
            swipePoints,
            onSuccess: (results) =>
            {
                Debug.Log($"Recognition results: {string.Join(", ", results)}");
            },
            onError: (error) =>
            {
                Debug.LogError($"Recognition failed: {error}");
            }
        );
    }

    /// <summary>
    /// Check if the server is running
    /// </summary>
    [Button]
    public void CheckServerHealth()
    {
        StartCoroutine(
            swipeRecognizer.CheckHealth(
                (isHealthy) =>
                {
                    if (isHealthy)
                    {
                        Debug.Log("✓ Swipe server is healthy and responding!");
                    }
                    else
                    {
                        Debug.LogError("✗ Swipe server is not responding. Make sure swipesrv is running in WSL.");
                    }
                }
            )
        );
    }

    private IEnumerator RunTest()
    {
        // Wait a frame for initialization
        yield return null;

        Debug.Log("Testing swipe recognition...");
        CheckServerHealth();

        yield return new WaitForSeconds(1f);

        TestSwipeHello();
    }
}
