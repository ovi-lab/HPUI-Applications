using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace ubco.ovilab.ViconUnityStream
{
    /// <summary>
    /// Room-locked Vicon→Unity calibration capture tooling (issue #28, phase 1).
    /// The plate subject must have <c>driveSkeleton</c> on so its transform is
    /// driven from the streamed segment data — that driven transform pose is
    /// the primary signal. Freeze: bake the plate's driven pose and spawn an
    /// untracked scene-root clone of the plate's marker spheres, then hide the
    /// live renderers. Record: append one JSONL sample line with the frozen and
    /// live plate poses (transform-based, primary), the frozen/live marker
    /// point sets (redundancy), raw Vicon data, head pose, and per-marker
    /// alignment residuals. Unfreeze: destroy the clone and restore the live
    /// renderers. No solve is performed here; analysis is external.
    /// </summary>
    public class CalibrationCapture : MonoBehaviour
    {
        [Tooltip("The plate subject visualizing the calibration object's markers.")]
        [SerializeField]
        private CustomStaticPatternMarkerVizScript plate;

        [Tooltip("Center-eye camera providing the head pose reference at record time.")]
        [SerializeField]
        private Camera centerEyeCamera;

        [Tooltip("Marker names expected on the plate's pattern. Record fails if the frozen clone does not have exactly these.")]
        [SerializeField]
        private List<string> expectedMarkerNames = new List<string>();

        [Tooltip("Optional: the Vicon-reported HWD transform (e.g. HWDMerger.viconHWD). When set, its pose is logged at freeze and record time as merge-state provenance — a per-sample viconHWD-vs-center-eye comparison directly estimates the eye offset and detects failed/skipped merges.")]
        [SerializeField]
        private Transform viconHWD;

        [Tooltip("Base file name (without extension) of the JSONL capture log under Application.persistentDataPath.")]
        [SerializeField]
        private string logFileName = "calibration_capture";

        /// <summary>Markers parked at/near world origin (zero-filled gap fill) are treated as corrupt data.</summary>
        private const float OriginParkThresholdMeters = 0.05f;

        /// <summary>Bump when the Sample schema changes; analysis keys off this.</summary>
        private const int SchemaVersion = 2;

        private const string PointSetUnits = "meters, Unity axes (y-up), world space, post-knob";

        private const string RawViconUnits = "millimetres, Vicon axes (z-up), post-gap-fill and post-knob (rawViconJson is the untouched pre-processing stream)";

        /// <summary>Location id of the plate's physical spot for the current batch (set from the editor window before Record).</summary>
        public int PlateLocationId { get; set; }

        /// <summary>Location id of the viewer/headset spot for the current sample (set from the editor window before Record).</summary>
        public int ViewerLocationId { get; set; }

        private GameObject frozenClone;
        private PoseData frozenPlatePose;
        private PoseData frozenHeadPose;
        private PoseData frozenViconHWDPose;
        private string frozenTimestamp;
        private int frozenFrameCount;
        private double frozenUnscaledTime;
        private readonly Dictionary<string, Transform> frozenMarkerTransforms = new Dictionary<string, Transform>();
        private readonly Dictionary<string, bool> preFreezeRendererStates = new Dictionary<string, bool>();
        private StreamWriter logWriter;
        private string logPath;
        private int sampleCount;

        /// <summary>Number of samples recorded this session.</summary>
        public int SampleCount => sampleCount;

        /// <summary>Path of the JSONL capture log, or null before the first record.</summary>
        public string LogPath => logPath;

        /// <summary>True while a frozen clone exists.</summary>
        public bool IsFrozen => frozenClone != null;

        #region Unity methods
        /// <inheritdoc />
        private void Start()
        {
            Debug.Assert(plate != null, "CalibrationCapture: plate reference is not set.");
            Debug.Assert(centerEyeCamera != null, "CalibrationCapture: centerEyeCamera reference is not set.");
            if (plate == null || centerEyeCamera == null)
            {
                enabled = false;
                return;
            }

            Debug.Assert(expectedMarkerNames != null && expectedMarkerNames.Count > 0, "CalibrationCapture: expectedMarkerNames must list the plate's marker names.");
            if (expectedMarkerNames == null || expectedMarkerNames.Count == 0)
            {
                Debug.LogError("CalibrationCapture: expectedMarkerNames must be filled with the plate's marker names, otherwise the marker-count guards are inert.");
                enabled = false;
            }
        }

        /// <inheritdoc />
        private void OnDestroy()
        {
            logWriter?.Close();
            logWriter = null;
        }
        #endregion

        #region Capture actions
        /// <summary>
        /// Bake the plate's driven transform pose (the primary signal), spawn an
        /// untracked scene-root clone with baked copies of the live marker
        /// spheres, and disable the live instances' MeshRenderers (the
        /// transforms keep updating).
        /// </summary>
        public void Freeze()
        {
            Debug.Assert(plate != null, "CalibrationCapture.Freeze: plate reference is not set.");
            Debug.Assert(!IsFrozen, "CalibrationCapture.Freeze: a frozen clone already exists; unfreeze first.");
            if (plate == null || IsFrozen)
            {
                Debug.LogError("CalibrationCapture.Freeze failed: missing plate reference or a frozen clone already exists.");
                return;
            }

            if (!plate.TryGetMarkerWorldPositions(out Dictionary<string, Vector3> livePositions))
            {
                Debug.LogError("CalibrationCapture.Freeze failed: no live marker data has been seen yet (no marker instances exist).");
                return;
            }

            if (plate.SubjectHidden)
            {
                Debug.LogError("CalibrationCapture.Freeze failed: the plate subject is currently hidden by data quality.");
                return;
            }

            foreach (string markerName in expectedMarkerNames)
            {
                Debug.Assert(livePositions.ContainsKey(markerName), $"CalibrationCapture.Freeze: expected marker `{markerName}` has no live instance.");
                if (!livePositions.ContainsKey(markerName))
                {
                    Debug.LogError($"CalibrationCapture.Freeze failed: expected marker `{markerName}` has no live instance.");
                    return;
                }
            }

            // With UseRemote gap fill a fully-gapped marker streams zeros and its
            // instance parks at world origin; baking that into the ghost corrupts
            // the whole sample. (Note the SubjectHidden guard above can be inert:
            // it requires zeroMarkers > dataQualityThreshold, which the plate's
            // segment/threshold config may never satisfy.)
            foreach (string markerName in expectedMarkerNames)
            {
                if (livePositions[markerName].sqrMagnitude < OriginParkThresholdMeters * OriginParkThresholdMeters)
                {
                    Debug.LogError($"CalibrationCapture.Freeze failed: marker `{markerName}` is at/near world origin (likely zero-filled gap fill); refusing to freeze.");
                    return;
                }
            }

            // Warn at freeze time, not only at record: a ghost frozen with a
            // stale/identity rotation bakes the error into the alignment view.
            if (!plate.TryGetPatternRotation(out _))
            {
                Debug.LogWarning("CalibrationCapture.Freeze: pattern rotation not computed this frame; the frozen plate pose rotation is stale or identity. Configure the plate's forward/right segments and re-freeze.");
            }

            // Freeze-time provenance: the head pose at alignment time (the
            // primary analysis frame) plus frame counters for the freeze→record skew.
            frozenHeadPose = new PoseData(centerEyeCamera.transform.position, centerEyeCamera.transform.rotation);
            frozenViconHWDPose = viconHWD != null ? new PoseData(viconHWD.position, viconHWD.rotation) : null;
            frozenTimestamp = DateTime.UtcNow.ToString("o");
            frozenFrameCount = Time.frameCount;
            frozenUnscaledTime = (double)Time.unscaledTime;

            // Capture the driven plate transform pose first: this is the
            // primary signal and must reflect the same frame the clone bakes.
            frozenPlatePose = new PoseData(plate.transform.position, plate.transform.rotation);

            frozenClone = new GameObject("CalibrationFrozenClone");
            // Embody the frozen plate pose on the clone root as well; the marker
            // copies below are placed world-space, so this only aids inspection.
            frozenClone.transform.SetPositionAndRotation(plate.transform.position, plate.transform.rotation);

            frozenMarkerTransforms.Clear();
            preFreezeRendererStates.Clear();
            foreach (string markerName in expectedMarkerNames)
            {
                Vector3 position = livePositions[markerName];
                GameObject markerCopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                // Match the live marker's rendered size; the default 1 m primitive
                // sphere would swamp the alignment view.
                if (plate.MarkerInstances.TryGetValue(markerName, out GameObject markerInstance) && markerInstance != null)
                {
                    markerCopy.transform.localScale = markerInstance.transform.lossyScale;
                }
                // No colliders on frozen clones; they must never intercept pointer/raycast interactions.
                Destroy(markerCopy.GetComponent<Collider>());
                markerCopy.name = markerName;
                markerCopy.transform.SetParent(frozenClone.transform, false);
                markerCopy.transform.position = position;
                frozenMarkerTransforms[markerName] = markerCopy.transform;
            }

            foreach (GameObject markerInstance in plate.MarkerInstances.Values)
            {
                if (markerInstance != null && markerInstance.TryGetComponent(out MeshRenderer renderer))
                {
                    preFreezeRendererStates[markerInstance.name] = renderer.enabled;
                    renderer.enabled = false;
                }
            }

            Debug.Log($"CalibrationCapture: frozen {frozenMarkerTransforms.Count} marker(s) at scene root.");
        }

        /// <summary>
        /// Append one JSONL sample to the capture log with the frozen and live
        /// plate poses (driven transform, primary), the frozen/live marker point
        /// sets (redundancy), raw Vicon data, freeze-time and record-time head
        /// poses and frame counters, and per-marker residual distances.
        /// </summary>
        public void Record()
        {
            Debug.Assert(plate != null, "CalibrationCapture.Record: plate reference is not set.");
            Debug.Assert(IsFrozen, "CalibrationCapture.Record: no frozen clone exists; freeze first.");
            if (plate == null || !IsFrozen)
            {
                Debug.LogError("CalibrationCapture.Record failed: missing plate reference or no frozen clone (freeze first).");
                return;
            }

            if (plate.SubjectHidden)
            {
                Debug.LogError("CalibrationCapture.Record failed: the plate subject is currently hidden by data quality.");
                return;
            }

            Debug.Assert(frozenMarkerTransforms.Count == expectedMarkerNames.Count, $"CalibrationCapture.Record: frozen marker count {frozenMarkerTransforms.Count} does not match expected {expectedMarkerNames.Count}.");
            if (frozenMarkerTransforms.Count != expectedMarkerNames.Count)
            {
                Debug.LogError($"CalibrationCapture.Record failed: frozen marker count {frozenMarkerTransforms.Count} != expected {expectedMarkerNames.Count}.");
                return;
            }

            if (!plate.TryGetMarkerWorldPositions(out Dictionary<string, Vector3> livePositions))
            {
                Debug.LogError("CalibrationCapture.Record failed: no live marker data (no marker instances exist).");
                return;
            }

            // Key sets must match exactly, not just in count: a renamed or
            // missing marker would otherwise pair the wrong points silently.
            if (livePositions.Count != frozenMarkerTransforms.Count || frozenMarkerTransforms.Keys.Any(key => !livePositions.ContainsKey(key)))
            {
                Debug.LogError($"CalibrationCapture.Record failed: live/frozen marker name sets differ (live: {string.Join(", ", livePositions.Keys.OrderBy(k => k))}, frozen: {string.Join(", ", frozenMarkerTransforms.Keys.OrderBy(k => k))}).");
                return;
            }

            if (centerEyeCamera == null)
            {
                Debug.LogError("CalibrationCapture.Record failed: centerEyeCamera reference is not set.");
                return;
            }

            SubjectDataManager dataManager = plate.SubjectDataManagerRef;
            Debug.Assert(dataManager != null, "CalibrationCapture.Record: the plate's SubjectDataManager reference is not set.");
            Debug.Assert(viconHWD != null, "CalibrationCapture.Record: viconHWD reference is not set; merge-state provenance (re-merge / failed merge / changed HWD offsets) is not logged.");

            bool patternRotationValid = plate.TryGetPatternRotation(out _);

            // With UseRemote gap fill a fully-gapped marker parks its instance at
            // world origin; reject the sample rather than log corrupt points.
            foreach (string markerName in expectedMarkerNames)
            {
                if (livePositions[markerName].sqrMagnitude < OriginParkThresholdMeters * OriginParkThresholdMeters)
                {
                    Debug.LogError($"CalibrationCapture.Record failed: marker `{markerName}` is at/near world origin (likely zero-filled gap fill); sample rejected.");
                    return;
                }
            }

            Sample sample = new Sample
            {
                schemaVersion = SchemaVersion,
                pointSetUnits = PointSetUnits,
                rawViconUnits = RawViconUnits,
                timestamp = DateTime.UtcNow.ToString("o"),
                plateSubjectName = plate.SubejectName,
                headPose = new PoseData(centerEyeCamera.transform.position, centerEyeCamera.transform.rotation),
                platePoseFrozen = frozenPlatePose,
                platePoseLive = new PoseData(plate.transform.position, plate.transform.rotation),
                plateLocationId = PlateLocationId,
                viewerLocationId = ViewerLocationId,
                viconWorldTransform = dataManager != null ? new PoseData(dataManager.ViconWorldTransform.position, dataManager.ViconWorldTransform.rotation) : null,
                frozenMarkers = new Dictionary<string, Vector3Data>(),
                liveMarkers = new Dictionary<string, Vector3Data>(),
                residualDistances = new Dictionary<string, float>(),
                rawViconData = new Dictionary<string, List<float>>(),
                // Freeze-time provenance: the alignment-time head pose and frame
                // counters quantifying the freeze→record skew.
                headPoseFrozen = frozenHeadPose,
                timestampFrozen = frozenTimestamp,
                frameCountFrozen = frozenFrameCount,
                unscaledTimeFrozen = frozenUnscaledTime,
                frameCount = Time.frameCount,
                unscaledTime = (double)Time.unscaledTime,
                streamTick = dataManager != null ? dataManager.LastStreamTick : 0,
                // Merge-state provenance: viconHWD (Vicon-reported HWD pose) vs
                // headPose/center-eye per sample directly reflects the merge
                // state and the eye offset being solved.
                viconHWDPoseFrozen = frozenViconHWDPose,
                viconHWDPoseLive = viconHWD != null ? new PoseData(viconHWD.position, viconHWD.rotation) : null,
                patternRotationValid = patternRotationValid,
                rawViconDataAvailable = false,
            };

            // Log the HWD offsets actually applied at record time: a stale or
            // changed offset shows up in the data instead of corrupting the
            // head-local analysis silently.
            if (viconHWD != null && viconHWD.TryGetComponent(out CustomHWDScript hwdScript))
            {
                sample.hwdPositionOffset = new Vector3Data(hwdScript.HmdPositionOffset);
                sample.hwdRotationOffset = new QuaternionData(hwdScript.HmdRotationOffset);
                sample.hwdPosFilter = hwdScript.ApplyPosFilter;
                sample.hwdRotFilter = hwdScript.ApplyRotFilter;
            }
            else
            {
                Debug.LogWarning("CalibrationCapture.Record: no CustomHWDScript on viconHWD; HWD offset provenance not logged.");
            }

            foreach (KeyValuePair<string, Transform> pair in frozenMarkerTransforms)
            {
                sample.frozenMarkers[pair.Key] = new Vector3Data(pair.Value.position);
                sample.liveMarkers[pair.Key] = new Vector3Data(livePositions[pair.Key]);
                sample.residualDistances[pair.Key] = Vector3.Distance(pair.Value.position, livePositions[pair.Key]);
            }

            if (dataManager != null && dataManager.StreamType == StreamType.LiveStream && dataManager.StreamedRawData.TryGetValue(plate.SubejectName, out string rawJson) && rawJson != null && dataManager.StreamedData.TryGetValue(plate.SubejectName, out ViconStreamData streamData) && streamData?.data != null)
            {
                // Truly raw, untouched by gap-fill and the global world transform.
                sample.rawViconJson = rawJson;
                foreach (KeyValuePair<string, List<float>> entry in streamData.data)
                {
                    if (entry.Value != null)
                    {
                        sample.rawViconData[entry.Key] = new List<float>(entry.Value);
                    }
                }
            }
            else
            {
                Debug.LogWarning("CalibrationCapture.Record: raw Vicon data unavailable this frame; sample logged without it.");
            }
            sample.rawViconDataAvailable = sample.rawViconData.Count > 0 || !string.IsNullOrEmpty(sample.rawViconJson);

            // The plate pose rotation comes from the pattern machinery via
            // segmentsRotation; if it was not computed this frame the driven
            // rotation is stale, which the analyst must know about.
            if (!plate.TryGetPatternRotation(out _))
            {
                Debug.LogWarning("CalibrationCapture.Record: pattern rotation was not computed this frame (pattern segments not configured or degenerate); the logged plate pose rotation may be stale.");
            }

            WriteSample(sample);
            sampleCount++;
            Debug.Log($"CalibrationCapture: recorded sample {sampleCount} to {logPath}");
        }

        /// <summary>
        /// Destroy the frozen clone and re-enable the live marker renderers,
        /// restoring their pre-freeze enabled states exactly.
        /// </summary>
        public void Unfreeze()
        {
            Debug.Assert(IsFrozen, "CalibrationCapture.Unfreeze: no frozen clone exists.");
            if (!IsFrozen)
            {
                Debug.LogError("CalibrationCapture.Unfreeze failed: no frozen clone exists.");
                return;
            }

            Destroy(frozenClone);
            frozenClone = null;
            frozenPlatePose = null;
            frozenHeadPose = null;
            frozenViconHWDPose = null;
            frozenTimestamp = null;
            frozenFrameCount = 0;
            frozenUnscaledTime = 0;
            frozenMarkerTransforms.Clear();

            foreach (GameObject markerInstance in plate.MarkerInstances.Values)
            {
                if (markerInstance != null && markerInstance.TryGetComponent(out MeshRenderer renderer))
                {
                    renderer.enabled = preFreezeRendererStates.TryGetValue(markerInstance.name, out bool wasEnabled) ? wasEnabled : true;
                }
            }
            preFreezeRendererStates.Clear();

            Debug.Log("CalibrationCapture: unfrozen; live renderers restored.");
        }
        #endregion

        #region Logging
        private void WriteSample(Sample sample)
        {
            if (logWriter == null)
            {
                logPath = Path.Combine(Application.persistentDataPath, logFileName + "_" + DateTime.Now.ToString("dd-MM-yy_HH-mm-ss") + ".jsonl");
                logWriter = new StreamWriter(logPath, append: true);
                Debug.Log("CalibrationCapture: writing capture log to " + logPath);
            }

            logWriter.WriteLine(JsonConvert.SerializeObject(sample));
            logWriter.Flush();
        }
        #endregion

        #region Serialization types
        [Serializable]
        private class Sample
        {
            /// <summary>Schema version; bump on any field change (see CalibrationCapture.SchemaVersion).</summary>
            public int schemaVersion;

            /// <summary>Units/axes conventions for the logged point sets and raw data.</summary>
            public string pointSetUnits;
            public string rawViconUnits;

            public string timestamp;
            public string plateSubjectName;

            /// <summary>Plate's physical spot id for the batch; mapping to lab positions lives in lab notes.</summary>
            public int plateLocationId;

            /// <summary>Viewer/headset spot id for this sample.</summary>
            public int viewerLocationId;
            public PoseData headPose;

            /// <summary>Head pose at freeze time — the frame in which the plate↔ghost alignment was performed (primary analysis frame).</summary>
            public PoseData headPoseFrozen;

            /// <summary>Freeze-time UTC timestamp and frame counters, quantifying the freeze→record skew.</summary>
            public string timestampFrozen;
            public int frameCountFrozen;
            public double unscaledTimeFrozen;
            public int frameCount;
            public double unscaledTime;

            /// <summary>Unix-ms tick of the most recent stream message (SubjectDataManager.LastStreamTick); bounds which Vicon update the captured data corresponds to.</summary>
            public long streamTick;

            /// <summary>HWD offset values actually applied at record time (provenance; null when unavailable).</summary>
            public Vector3Data hwdPositionOffset;
            public QuaternionData hwdRotationOffset;
            public bool hwdPosFilter;
            public bool hwdRotFilter;

            /// <summary>Vicon-reported HWD pose at freeze/record time. Compared against headPose/center-eye per sample, this directly reflects merge state (failed/skipped re-merge, changed HWD offsets) and is a per-sample estimate of the eye offset.</summary>
            public PoseData viconHWDPoseFrozen;
            public PoseData viconHWDPoseLive;

            /// <summary>False when the pattern rotation was not computed this frame — the logged plate pose rotation is then stale.</summary>
            public bool patternRotationValid;

            /// <summary>False when raw stream data was unavailable and the sample was logged without it.</summary>
            public bool rawViconDataAvailable;

            /// <summary>Global Vicon→Unity knob value at record time, provenance only — it cancels out of the per-sample candidate transform.</summary>
            public PoseData viconWorldTransform;

            /// <summary>Plate's driven transform pose at freeze time (primary signal, P_u pose-pair).</summary>
            public PoseData platePoseFrozen;

            /// <summary>Plate's driven transform pose at record time (primary signal, P_v pose-pair).</summary>
            public PoseData platePoseLive;
            public Dictionary<string, Vector3Data> frozenMarkers;
            public Dictionary<string, Vector3Data> liveMarkers;
            public Dictionary<string, float> residualDistances;
            public Dictionary<string, List<float>> rawViconData;

            /// <summary>Untouched raw subject JSON from the stream (pre gap-fill, pre world transform).</summary>
            public string rawViconJson;
        }

        [Serializable]
        private class Vector3Data
        {
            public double x;
            public double y;
            public double z;

            public Vector3Data(Vector3 v)
            {
                x = v.x;
                y = v.y;
                z = v.z;
            }
        }

        [Serializable]
        private class QuaternionData
        {
            public double x;
            public double y;
            public double z;
            public double w;

            public QuaternionData(Quaternion q)
            {
                x = q.x;
                y = q.y;
                z = q.z;
                w = q.w;
            }
        }

        [Serializable]
        private class PoseData
        {
            public Vector3Data position;
            public QuaternionData rotation;

            public PoseData(Vector3 position, Quaternion rotation)
            {
                this.position = new Vector3Data(position);
                this.rotation = new QuaternionData(rotation);
            }
        }
        #endregion
    }
}
