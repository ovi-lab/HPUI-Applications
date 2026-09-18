using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ubco.ovilab.ViconUnityStream
{
    public class CustomStaticPatternMarkerVizScript : CustomSubjectScript
    {
        [Serializable]
        struct SegmentMarkerPattern
        {
            public string name;
            public List<string> markerNames;
        }

        [Tooltip("The patterns used. The markers names should match the names in Nexus. The segment name should match the values set in the forward/right segment and also the objects of the seleton")]
        [SerializeField]
        private List<SegmentMarkerPattern> patterns;

        [Tooltip("Prefab instantiated as a child of this transform for each marker in the pattern, positioned at the marker's world location each frame.")]
        [SerializeField]
        private GameObject markerPrefab;

        private Dictionary<string, GameObject> markerInstances = new();
        private Quaternion lastPatternRotation = Quaternion.identity;
        private bool hasPatternRotation;

        /// <summary>
        /// Live marker instances keyed by marker name. The instance transforms
        /// are positioned in world space every frame by <see cref="UpdateMarkerInstances"/>.
        /// </summary>
        public IReadOnlyDictionary<string, GameObject> MarkerInstances => markerInstances;

        /// <summary>
        /// The <see cref="SubjectDataManager"/> feeding this subject, for direct
        /// access to the raw streamed data.
        /// </summary>
        public SubjectDataManager SubjectDataManagerRef => subjectDataManager;

        /// <summary>
        /// World-space positions of all live marker instances. Returns false if
        /// no instances have been created yet (no data seen this session).
        /// </summary>
        public bool TryGetMarkerWorldPositions(out Dictionary<string, Vector3> positions)
        {
            positions = new Dictionary<string, Vector3>();
            if (markerInstances.Count == 0)
            {
                return false;
            }
            foreach (KeyValuePair<string, GameObject> pair in markerInstances)
            {
                if (pair.Value == null)
                {
                    positions.Clear();
                    return false;
                }
                positions[pair.Key] = pair.Value.transform.position;
            }
            return true;
        }

        /// <summary>
        /// Pattern rotation computed by the forward/right machinery this frame.
        /// Returns false when the pattern does not define the required segments
        /// or segment data was missing in the latest frame.
        /// </summary>
        public bool TryGetPatternRotation(out Quaternion rotation)
        {
            rotation = lastPatternRotation;
            return hasPatternRotation;
        }

        [Tooltip("The first segment to use to compute the forward vector")]
        [SerializeField]
        private string forwardSegment1;

        [Tooltip("The second segment to use to compute the forward vector")]
        [SerializeField]
        private string forwardSegment2;

        [Tooltip("If set use right segment 1 and 2 to compute right. Else use the up segments.")]
        [SerializeField]
        private bool specifyRight = true;

        [Tooltip("The first segment to use to compute the right vector")]
        [SerializeField]
        private string rightSegment1;

        [Tooltip("The second segment to use to compute the right vector")]
        [SerializeField]
        private string rightSegment2;

        [Tooltip("The first segment to use to compute the up vector")]
        [SerializeField]
        private string upSegment1;

        [Tooltip("The second segment to use to compute the up vector")]
        [SerializeField]
        private string upSegment2;

        protected override void Start()
        {
            base.Start();
            OnValidate();
        }

        /// <inheritdoc />
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                if (segmentMarkers == null)
                {
                    segmentMarkers = new Dictionary<string, List<string>>();
                }
                segmentMarkers.Clear();
                foreach (SegmentMarkerPattern pattern in patterns)
                {
                    segmentMarkers.Add(pattern.name, pattern.markerNames);
                }
            }
        }

        /// <inheritdoc />
        protected override Dictionary<string, Vector3> ProcessSegments(Dictionary<string, Vector3> segments, ViconStreamData viconStreamData)
        {
            UpdateMarkerInstances(viconStreamData);
            hasPatternRotation = false;

            Vector3 forward;
            Vector3 right;
            Vector3 up;

            if (segments.TryGetValue(forwardSegment1, out Vector3 forward1) && segments.TryGetValue(forwardSegment2, out Vector3 forward2))
            {
                forward = forward2 - forward1;
                if (forward.sqrMagnitude <= 0f)
                {
                    /// Degenerate forward (occluded / zero-filled segment markers);
                    /// LookRotation would warn and yield identity, which must not
                    /// be recorded as a valid pattern rotation.
                    Debug.LogError($"Degenerate forward vector from segments `{forwardSegment1}` and `{forwardSegment2}` (likely zero-filled data).");
                    return segments;
                }
            }
            else
            {
                Debug.LogError($"Missing segment. Make sure `forwardSegment1` and `forwardSegment2` are also in `pattern`");
                return segments;
            }

            if (specifyRight)
            {
                if (segments.TryGetValue(rightSegment1, out Vector3 right1) && segments.TryGetValue(rightSegment2, out Vector3 right2))
                {
                    right = right2 - right1;
                    if (right.sqrMagnitude <= 0f)
                    {
                        Debug.LogError($"Degenerate right vector from segments `{rightSegment1}` and `{rightSegment2}` (likely zero-filled data).");
                        return segments;
                    }
                }
                else
                {
                    Debug.LogError($"Missing segment. Make sure `rightSegment1` and `rightSegment2` are also in `pattern`");
                    return segments;
                }

                up = Vector3.Cross(right, forward);
                if (up.sqrMagnitude <= 0f)
                {
                    Debug.LogError($"Degenerate up vector: right and forward are collinear for this pattern.");
                    return segments;
                }
            }
            else
            {
                if (segments.TryGetValue(upSegment1, out Vector3 up1) && segments.TryGetValue(upSegment2, out Vector3 up2))
                {
                    up = up2 - up1;
                    if (up.sqrMagnitude <= 0f)
                    {
                        Debug.LogError($"Degenerate up vector from segments `{upSegment1}` and `{upSegment2}` (likely zero-filled data).");
                        return segments;
                    }
                }
                else
                {
                    Debug.LogError($"Missing segment. Make sure `upSegment1` and `upSegment2` are also in `pattern`");
                    return segments;
                }
            }

            Quaternion rot = Quaternion.LookRotation(forward, up);
            lastPatternRotation = rot;
            hasPatternRotation = true;
            foreach (string segmentName in segments.Keys)
            {
                segmentsRotation[segmentName] = rot;
            }

            return segments;
        }

        private void UpdateMarkerInstances(ViconStreamData viconStreamData)
        {
            if (markerPrefab == null || segmentMarkers == null)
            {
                return;
            }

            foreach (KeyValuePair<string, List<string>> segment in segmentMarkers)
            {
                foreach (string markerName in segment.Value)
                {
                    if (!viconStreamData.data.TryGetValue(markerName, out List<float> markerData) || markerData == null || markerData.Count < 3)
                    {
                        continue;
                    }

                    if (!markerInstances.TryGetValue(markerName, out GameObject markerInstance) || markerInstance == null)
                    {
                        markerInstance = Instantiate(markerPrefab, transform);
                        markerInstance.name = markerName;
                        markerInstances[markerName] = markerInstance;
                    }

                    markerInstance.transform.position = new Vector3(markerData[0], markerData[2], markerData[1]) * viconUnitsToUnityUnits;
                }
            }
        }
    }
}
