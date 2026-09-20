using System;
using System.Collections.Generic;
using EditorAttributes;
using _Scripts.Utils;
using TMPro;
using ubco.ovilab.HPUI.Core.Interaction;
using UnityEngine;

namespace _Scripts.Keyboard.Text
{
    /// <summary>
    /// Maps keyboard layout letters onto the HPUI generated continuous surfaces of the fingers.
    /// For each finger row (one <see cref="HPUIGeneratedContinuousInteractable"/>), waits for the
    /// continuous surface to be created, enumerates the generated collider array, parents a blank
    /// anchor to the collider nearest each key's layout position, and spawns the key prefab under
    /// this object's 'Keys' child with its <see cref="SmoothedFollower"/> pointed at that anchor —
    /// so the key rides the collider's per-frame deformation, smoothed.
    ///
    /// Mapping: a key's layout cx (0-1, left to right on the keyboard) maps to the collider's
    /// Y index (along the finger): cx 0 -> Y{yDivisions - 1} (leftmost), cx 1 -> Y0 (rightmost).
    /// The X index (across the finger) is the center column of the generated grid; when the
    /// column count is even, the lower of the two middle columns is used.
    /// </summary>
    public class FingerRowKeyPlacer : MonoBehaviour
    {
        [Serializable]
        public class RowMapping
        {
            [Tooltip("The generated continuous interactable for this finger row.")]
            public HPUIGeneratedContinuousInteractable rowInteractable;

            [Tooltip("Which keyboard row band this finger represents: 0 = top, 1 = middle, 2 = bottom, ...")]
            [Min(0)]
            public int rowIndex;
        }

        [Serializable]
        public struct LetterAdjustment
        {
            [Tooltip("Letter to adjust (matched case-insensitively against layout letters).")]
            public char letter;

            [Tooltip("Offset added to the computed collider Y index for this letter, e.g. +1 anchors to the collider one row away.")]
            public int yOffset;
        }

        [Header("References")]
        [Tooltip("Prefab cloned for each key. Must carry a SmoothedFollower; it is pointed at a blank anchor parented to the target collider, and the key is spawned unmodified under this object's 'Keys' child.")]
        [SerializeField]
        private GameObject keyPrefab;

        [Tooltip("Material path format under Resources applied to each cloned key. '*' is substituted with the uppercase letter (e.g. 'Keyboard/Mats/Letters/* 1' -> 'Keyboard/Mats/Letters/Q 1').")]
        [SerializeField]
        private string letterMaterialPathFormat = "Keyboard/Mats/Letters/* 1";

        [Tooltip("Optional TMP label that receives placement logs.")]
        [SerializeField]
        private TMP_Text debugOutput;

        [Header("Rows")]
        [Tooltip("One entry per finger row. Row identity (not layout cz) decides which finger a key lands on.")]
        [SerializeField]
        private List<RowMapping> rows = new List<RowMapping>()
        {
            new RowMapping { rowIndex = 0 },
            new RowMapping { rowIndex = 1 },
            new RowMapping { rowIndex = 2 },
        };

        [Header("Layout")]
        [Tooltip("layout.json resource name under Resources.")]
        [SerializeField]
        private string layoutResourceName = "layout";

        [Tooltip("If true, places keys automatically when a row's continuous surface is created at runtime.")]
        [SerializeField]
        private bool placeOnSurfaceCreated = true;

        [Header("Adjustments")]
        [Tooltip("Optional per-letter adjustments for badly-oriented colliders: the letter's target collider Y index gets the given offset applied (then clamped).")]
        [SerializeField]
        private List<LetterAdjustment> adjustedLetters = new List<LetterAdjustment>();

        private readonly Dictionary<string, GameObject> placedKeys = new Dictionary<string, GameObject>();
        private Transform keysRoot;

        /// <summary>
        /// Parent for spawned key objects; created lazily under this component.
        /// </summary>
        private Transform KeysRoot
        {
            get
            {
                if (keysRoot == null)
                {
                    GameObject root = new GameObject("Keys");
                    root.transform.SetParent(transform, false);
                    keysRoot = root.transform;
                }
                return keysRoot;
            }
        }

        /// <summary>
        /// Placed key objects keyed by uppercase letter.
        /// </summary>
        public IReadOnlyDictionary<string, GameObject> PlacedKeys => placedKeys;

        private void OnEnable()
        {
            if (rows == null)
            {
                return;
            }
            foreach (RowMapping mapping in rows)
            {
                if (mapping != null && mapping.rowInteractable != null)
                {
                    mapping.rowInteractable.ContinuousSurfaceEvent.AddListener(HandleSurfaceCreated);
                }
            }
        }

        private void OnDisable()
        {
            if (rows == null)
            {
                return;
            }
            foreach (RowMapping mapping in rows)
            {
                if (mapping != null && mapping.rowInteractable != null)
                {
                    mapping.rowInteractable.ContinuousSurfaceEvent.RemoveListener(HandleSurfaceCreated);
                }
            }
        }

        private void HandleSurfaceCreated(HPUIContinuousSurfaceCreatedEventArgs args)
        {
            if (!placeOnSurfaceCreated)
            {
                return;
            }

            foreach (RowMapping mapping in rows)
            {
                if (mapping != null && mapping.rowInteractable != null && ReferenceEquals(args.interactableObject, mapping.rowInteractable))
                {
                    PlaceRow(mapping);
                    return;
                }
            }
        }

        /// <summary>
        /// Place keys on all configured rows. Requires each row's continuous surface to exist
        /// (run the interactable's ManualRecompute first if needed).
        /// </summary>
        [Button("Place Keys")]
        public void PlaceAll()
        {
            if (rows == null || rows.Count == 0)
            {
                Debug.LogError("[FingerRowKeyPlacer] No rows configured.");
                return;
            }

            ClearPlacedKeys();
            foreach (RowMapping mapping in rows)
            {
                if (mapping != null)
                {
                    PlaceRow(mapping);
                }
            }
        }

        /// <summary>
        /// Removes all placed key clones.
        /// </summary>
        [Button("Clear Placed Keys")]
        public void ClearPlacedKeys()
        {
            foreach (KeyValuePair<string, GameObject> pair in placedKeys)
            {
                if (pair.Value != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        DestroyImmediate(pair.Value);
                    }
                    else
                    {
                        Destroy(pair.Value);
                    }
#else
                    Destroy(pair.Value);
#endif
                }
            }
            placedKeys.Clear();
        }

        private void PlaceRow(RowMapping mapping)
        {
            HPUIGeneratedContinuousInteractable row = mapping.rowInteractable;
            if (row == null)
            {
                Debug.LogWarning("[FingerRowKeyPlacer] Row interactable not set; skipping row.");
                return;
            }

            Keyboard.LayoutData layout = LoadLayout();
            if (layout == null)
            {
                return;
            }

            int yDivisions = row.Y_divisions;
            if (yDivisions <= 0)
            {
                Debug.LogError($"[FingerRowKeyPlacer] '{row.name}' has invalid Y_divisions ({yDivisions}); surface not generated yet?");
                return;
            }

            // Group generated colliders by their Y index (parsed from "X{x}-Y{y}" names).
            Dictionary<int, List<Collider>> columns = new Dictionary<int, List<Collider>>();
            foreach (BoxCollider collider in row.GetComponentsInChildren<BoxCollider>(false))
            {
                if (!TryParseColliderY(collider.name, out int y))
                {
                    continue;
                }

                if (!columns.TryGetValue(y, out List<Collider> column))
                {
                    column = new List<Collider>();
                    columns[y] = column;
                }
                column.Add(collider);
            }

            if (columns.Count == 0)
            {
                Debug.LogError($"[FingerRowKeyPlacer] No generated colliders found under '{row.name}'. Run ManualRecompute on the interactable first.");
                return;
            }

            int centerX = GetCenterColumn(columns);
            Dictionary<char, int> offsets = GetLetterOffsets();

            int placed = 0;
            int rowCount = rows.Count;
            foreach (Keyboard.KeyData key in layout.keys)
            {
                if (!MatchesRow(key.cy, mapping.rowIndex, rowCount))
                {
                    continue;
                }

                char letter = key.letter.Length > 0 ? char.ToUpperInvariant(key.letter[0]) : '\0';
                int yIndex = TargetYIndex(key.cx, yDivisions);
                if (offsets.TryGetValue(letter, out int yOffset))
                {
                    yIndex = Mathf.Clamp(yIndex + yOffset, 0, yDivisions - 1);
                }

                if (!columns.TryGetValue(yIndex, out List<Collider> column) || column.Count == 0)
                {
                    Debug.LogWarning($"[FingerRowKeyPlacer] No collider column for key '{key.letter}' (cx {key.cx}, target y index {yIndex}).");
                    continue;
                }

                // Clamp to the actual column size; centerX is derived from the max, but a
                // ragged grid could leave shorter columns.
                Collider target = column[Mathf.Clamp(centerX, 0, column.Count - 1)];
                PlaceKey(key, target);
                placed++;
            }

            string message = $"[FingerRowKeyPlacer] Placed {placed} keys on '{row.name}' (y divisions {yDivisions}, {columns.Count} y columns, center x {centerX}).";
            Debug.Log(message);
            if (debugOutput != null)
            {
                debugOutput.text = message;
            }
        }

        /// <summary>
        /// Instantiates the key prefab and applies the letter material. A blank anchor is
        /// parented to the target collider (inheriting its orientation and per-frame
        /// deformation) and the key is spawned unmodified under this object's 'Keys' child
        /// with its SmoothedFollower pointed at that anchor.
        /// </summary>
        private void PlaceKey(Keyboard.KeyData key, Collider targetCollider)
        {
            if (keyPrefab == null)
            {
                Debug.LogError("[FingerRowKeyPlacer] Key prefab not assigned.");
                return;
            }

            string letter = key.letter.ToUpperInvariant();

            // Anchor rides the collider (and thus the skin); the key itself stays under
            // 'Keys' and follows it smoothly.
            GameObject anchor = new GameObject($"Anchor_{letter}");
            anchor.transform.SetParent(targetCollider.transform, false);

            GameObject keyObj = Instantiate(keyPrefab, KeysRoot);
            keyObj.name = $"Key_{letter}";
            SmoothedFollower follower = keyObj.GetComponent<SmoothedFollower>();
            if (follower == null)
            {
                Debug.LogError($"[FingerRowKeyPlacer] Key prefab '{keyPrefab.name}' has no SmoothedFollower.");
            }
            else
            {
                follower.FollowTarget = anchor.transform;
                follower.OrientFromTargetUp = true;
            }

            string materialPath = MaterialPathFor(letter);
            Material material = Resources.Load<Material>(materialPath);
            if (material == null)
            {
                Debug.LogWarning($"[FingerRowKeyPlacer] Material for letter '{letter}' not found at 'Resources/{materialPath}'.");
                return;
            }

            MeshRenderer renderer = keyObj.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = keyObj.GetComponentInChildren<MeshRenderer>();
            }

            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
            else
            {
                Debug.LogWarning($"[FingerRowKeyPlacer] No MeshRenderer on key '{letter}' clone; material not applied.");
            }

            placedKeys[letter] = keyObj;
        }

        /// <summary>
        /// Layout cx (0-1, left to right) to collider Y index: leftmost key (cx 0) -> Y{yDivisions - 1},
        /// rightmost (cx 1) -> Y0. Rounded to the nearest index and clamped.
        /// </summary>
        internal static int TargetYIndex(float cx, int yDivisions)
        {
            if (yDivisions <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(yDivisions), yDivisions, "Y divisions must be positive.");
            }
            if (cx < 0f || cx > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(cx), cx, "Layout cx must be within 0-1.");
            }

            int index = yDivisions - 1 - Mathf.RoundToInt(cx * (yDivisions - 1));
            return Mathf.Clamp(index, 0, yDivisions - 1);
        }

        /// <summary>
        /// Center of the generated x columns; lower middle when the column count is even.
        /// </summary>
        private static int GetCenterColumn(Dictionary<int, List<Collider>> columns)
        {
            int maxCount = 0;
            foreach (List<Collider> column in columns.Values)
            {
                maxCount = Mathf.Max(maxCount, column.Count);
            }

            if (maxCount == 0)
            {
                throw new InvalidOperationException("No colliders in any column.");
            }

            return (maxCount - 1) / 2;
        }

        /// <summary>
        /// True when the key's layout cy falls in this row's band. The keyboard's 0-1 cy space
        /// is split into <paramref name="rowCount"/> equal bands; e.g. with 3 rows the layout
        /// cys 0.1667 / 0.5 / 0.8333 resolve to row indices 0 / 1 / 2.
        /// </summary>
        internal static bool MatchesRow(float keyCy, int rowIndex, int rowCount)
        {
            if (rowCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rowCount), rowCount, "Row count must be positive.");
            }
            if (keyCy < 0f || keyCy > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(keyCy), keyCy, "Layout cy must be within 0-1.");
            }

            int keyRow = Mathf.Clamp(Mathf.FloorToInt(keyCy * rowCount), 0, rowCount - 1);
            return keyRow == rowIndex;
        }

        /// <summary>
        /// Parses the y index from a generated collider's "X{x}-Y{y}" name.
        /// </summary>
        internal static bool TryParseColliderY(string colliderName, out int y)
        {
            y = -1;
            if (string.IsNullOrEmpty(colliderName))
            {
                return false;
            }

            int separator = colliderName.IndexOf("-Y", StringComparison.Ordinal);
            if (separator < 0 || !int.TryParse(colliderName.Substring(separator + 2), out y))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Builds the letter -> yOffset lookup from adjustedLetters. Duplicate letters log
        /// an error and the first entry wins.
        /// </summary>
        private Dictionary<char, int> GetLetterOffsets()
        {
            Dictionary<char, int> offsets = new Dictionary<char, int>();
            if (adjustedLetters == null)
            {
                return offsets;
            }

            foreach (LetterAdjustment adjustment in adjustedLetters)
            {
                char letter = char.ToUpperInvariant(adjustment.letter);
                if (offsets.ContainsKey(letter))
                {
                    Debug.LogError($"[FingerRowKeyPlacer] Duplicate adjustment for letter '{letter}'; keeping the first entry.");
                    continue;
                }
                offsets[letter] = adjustment.yOffset;
            }
            return offsets;
        }

        private string MaterialPathFor(string uppercaseLetter)
        {
            if (string.IsNullOrEmpty(letterMaterialPathFormat) || !letterMaterialPathFormat.Contains("*"))
            {
                throw new InvalidOperationException($"letterMaterialPathFormat must contain '*' as the letter placeholder (got '{letterMaterialPathFormat}').");
            }
            return letterMaterialPathFormat.Replace("*", uppercaseLetter);
        }

        private Keyboard.LayoutData LoadLayout()
        {
            TextAsset layoutJson = Resources.Load<TextAsset>(layoutResourceName);
            if (layoutJson == null)
            {
                Debug.LogError($"[FingerRowKeyPlacer] Layout file '{layoutResourceName}' not found in Resources!");
                return null;
            }

            Keyboard.LayoutData layout = JsonUtility.FromJson<Keyboard.LayoutData>(layoutJson.text);
            if (layout == null || layout.keys == null || layout.keys.Length == 0)
            {
                Debug.LogError("[FingerRowKeyPlacer] Failed to parse layout.json or no keys found!");
                return null;
            }
            return layout;
        }
    }
}
