using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.Keyboard.Text
{
    /// <summary>
    /// Generates a virtual keyboard in the XZ plane based on layout.json
    /// Places key quads within a 1x1 area (X: 0→1, Z: 0→-1, Y: 0)
    /// Includes cursor visualization with slider controls
    /// </summary>
    public class Keyboard : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Prefab for each letter key (LetterQuad)")]
        [SerializeField]
        private GameObject letterQuadPrefab;

        [Tooltip("Cursor object to visualize position in the keyboard space")]
        [SerializeField]
        private Transform cursor;

        [Header("Cursor Control")]
        [Tooltip("Normalized X position (0-1) for cursor")]
        [Range(0f, 1f)]
        [SerializeField]
        private float cursorX = 0.5f;

        [Tooltip("Normalized Y position (0-1) for cursor")]
        [Range(0f, 1f)]
        [SerializeField]
        private float cursorY = 0.5f;

        [SerializeField]
        private KeyboardSwipePipeline swipePipeline;

        [Header("Debug")]
        [Tooltip("When enabled, draws small spheres for every point of the last completed gesture trajectory.")]
        [SerializeField]
        private bool showGestureTrace = false;

        [Header("Events")]
        [Tooltip("Event invoked when cursor position changes, passes normalized x and y (0-1 range)")]
        public CursorPositionEvent onCursorPositionChanged;

        private List<GameObject> spawnedKeys = new List<GameObject>();

        private string layoutResourceName = "layout";

        private float yPosition = 0f;

        /// <summary>
        /// Stores the last completed gesture trajectory for debug visualization.
        /// </summary>
        private List<Vector2> lastGestureTrajectory;

        private void OnEnable()
        {
            swipePipeline.OnCursorPosition.AddListener(UpdateCursorPosition);
            swipePipeline.OnGestureCompleted.AddListener(CacheGestureTrajectory);
        }

        private void OnDisable()
        {
            swipePipeline.OnCursorPosition.RemoveListener(UpdateCursorPosition);
            swipePipeline.OnGestureCompleted.RemoveListener(CacheGestureTrajectory);
        }

        /// <summary>
        /// Generates the keyboard by instantiating keys from layout.json
        /// </summary>
        [Button("Generate Keyboard")]
        public void GenerateKeyboard()
        {
            // Clean up existing keys
            ClearKeyboard();

            // Load and parse layout
            TextAsset layoutJson = Resources.Load<TextAsset>(layoutResourceName);
            if (layoutJson == null)
            {
                Debug.LogError($"Layout file '{layoutResourceName}' not found in Resources!");
                return;
            }

            LayoutData layout = JsonUtility.FromJson<LayoutData>(layoutJson.text);
            if (layout == null || layout.keys == null || layout.keys.Length == 0)
            {
                Debug.LogError("Failed to parse layout.json or no keys found!");
                return;
            }

            // Instantiate each key
            foreach (KeyData key in layout.keys)
            {
                SpawnKey(key);
            }

            Debug.Log($"Generated {spawnedKeys.Count} keys from layout '{layout.name}'");

            // Update cursor position after generating keyboard
            UpdateCursorPosition(cursorX, cursorY);
        }

        /// <summary>
        /// Updates cursor position using normalized coordinates (0-1 range)
        /// </summary>
        public void UpdateCursorPosition(float normalizedX, float normalizedY)
        {
            if (cursor == null)
                return;

            // Clamp to valid range
            normalizedX = Mathf.Clamp01(normalizedX);
            normalizedY = Mathf.Clamp01(normalizedY);

            // Update slider values
            cursorX = normalizedX;
            cursorY = normalizedY;

            // Map to world coordinates (same as keyboard layout)
            float xPos = normalizedX; // Maps to Unity X (0→1)
            float zPos = -normalizedY; // Maps to Unity -Z (0→-1)

            Vector3 worldPosition = new Vector3(xPos, yPosition, zPos);
            cursor.position = transform.position + worldPosition;

            // Invoke event
            onCursorPositionChanged?.Invoke(normalizedX, normalizedY);
        }

        /// <summary>
        /// Updates cursor position using Vector2 (normalized 0-1 range)
        /// </summary>
        public void UpdateCursorPosition(Vector2 normalizedPosition)
        {
            UpdateCursorPosition(normalizedPosition.x, normalizedPosition.y);
        }

        /// <summary>
        /// Clears all spawned key objects
        /// </summary>
        [Button("Clear Keyboard")]
        public void ClearKeyboard()
        {
            foreach (GameObject key in spawnedKeys)
            {
                if (key != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        UnityEditor.EditorApplication.delayCall += () =>
                        {
                            if (key != null)
                                DestroyImmediate(key);
                        };
                    else
                        Destroy(key);
#else
                    Destroy(key);
#endif
                }
            }
            spawnedKeys.Clear();
        }

        /// <summary>
        /// Spawns a single key quad at the position specified in layout
        /// </summary>
        private void SpawnKey(KeyData keyData)
        {
            if (letterQuadPrefab == null)
            {
                Debug.LogError("LetterQuad prefab is not assigned!");
                return;
            }

            // Map layout coordinates to Unity world coordinates
            // cx (layout center x) → Unity X
            // cy (layout center y) → Unity -Z (inverted for correct visual orientation)
            float xPos = keyData.cx; // Maps to Unity X (0→1 range)
            float zPos = -keyData.cy; // Maps to Unity -Z (0→-1 range)

            Vector3 position = new Vector3(xPos, yPosition, zPos);

            // Instantiate as child of this GameObject
            GameObject keyObj = Instantiate(letterQuadPrefab, transform);
            keyObj.name = $"Key_{keyData.letter.ToUpper()}";
            keyObj.transform.position = position;

            // Load and assign material for this letter
            Material keyMaterial = LoadMaterialForLetter(keyData.letter);
            if (keyMaterial != null)
            {
                MeshRenderer renderer = keyObj.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material = keyMaterial;
                }
            }
            else
            {
                Debug.LogWarning($"Material for letter '{keyData.letter}' not found!");
            }

            spawnedKeys.Add(keyObj);
        }

        /// <summary>
        /// Loads the material for a specific letter from Resources/Keyboard/Mats/Letters/
        /// </summary>
        private Material LoadMaterialForLetter(string letter)
        {
            // Convert to uppercase for material name
            string materialName = letter.ToUpper();
            string materialPath = $"Keyboard/Mats/Letters/{materialName}";

            Material mat = Resources.Load<Material>(materialPath);
            return mat;
        }

        /// <summary>
        /// Caches the last completed gesture trajectory so it can be visualized.
        /// </summary>
        private void CacheGestureTrajectory(List<Vector2> trajectory)
        {
            lastGestureTrajectory = trajectory;
        }

        private void OnValidate()
        {
            // Update cursor position when sliders change in inspector
            if (cursor != null)
            {
                UpdateCursorPosition(cursorX, cursorY);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Use the transform's matrix so all gizmos are drawn in local space,
            // automatically handling translation, rotation, and scale.
            Gizmos.matrix = transform.localToWorldMatrix;

            // Draw wireframe cube to visualize the 1x1 keyboard area
            // X: 0→1, Z: 0→-1 (cy maps to -z)
            Gizmos.color = Color.yellow;
            Vector3 center = new Vector3(0.5f, yPosition, -0.5f);
            Vector3 size = new Vector3(1f, 0.01f, 1f);
            Gizmos.DrawWireCube(center, size);

            // Draw debug gesture trace points if enabled and a trajectory exists
            if (showGestureTrace && lastGestureTrajectory != null && lastGestureTrajectory.Count > 0)
            {
                const float pointRadius = 0.01f;
                int count = lastGestureTrajectory.Count;

                for (int i = 0; i < count; i++)
                {
                    float t = count > 1 ? (float)i / (count - 1) : 0f;
                    Gizmos.color = Color.Lerp(Color.red, Color.green, t);

                    Vector2 pt = lastGestureTrajectory[i];
                    Vector3 localPos = new Vector3(pt.x, yPosition, -pt.y);
                    Gizmos.DrawSphere(localPos, pointRadius);
                }
            }

            // Restore identity so other gizmos aren't affected
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif

        /// <summary>
        /// Data structures for JSON deserialization
        /// </summary>
        [System.Serializable]
        public class LayoutData
        {
            public string name;
            public string letters;
            public KeyData[] keys;
        }

        [System.Serializable]
        public class KeyData
        {
            public string letter;
            public float cx; // Center X in layout (maps to Unity X)
            public float cy; // Center Y in layout (maps to Unity -Z)
            public float rx; // Radius X (not used currently)
            public float ry; // Radius Y (not used currently)
        }

        /// <summary>
        /// Custom UnityEvent to pass cursor position (normalized x, y)
        /// </summary>
        [System.Serializable]
        public class CursorPositionEvent : UnityEvent<float, float> { }
    }
}
