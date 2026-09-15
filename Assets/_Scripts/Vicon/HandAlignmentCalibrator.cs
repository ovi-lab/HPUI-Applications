using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EditorAttributes;
using TMPro;
using ubco.ovilab.ViconUnityStream;
using ubco.ovilab.ViconUnityStream.Utils;
using UnityEngine;
using UnityEngine.Assertions;
using _Scripts.HPUI.Utils;

namespace _Scripts.Vicon
{
    /// <summary>
    /// Guided hand-alignment calibration: shows a frozen ghost hand at a series
    /// of authored target poses, has the user place their real hand on it, and
    /// solves a constant world-space translation that corrects the residual
    /// Vicon-to-Quest frame offset. The solved offset is applied via
    /// <see cref="HWDMerger.SetCalibrationOffset"/> and persisted to JSON.
    ///
    /// Error model (issue #27): the misalignment is a constant world-space
    /// translation. A head-local alternative is diagnosed (Model B) and, if it
    /// wins, reported and aborted rather than applied.
    /// </summary>
    public class HandAlignmentCalibrator : MonoBehaviour
    {
        public enum CalibrationState
        {
            Idle,
            WaitingForMergeSettle,
            Transition,
            Stabilizing,
            Sampling,
            Solving,
            Verifying,
            Complete,
            Failed,
        }

        /// <summary>
        /// The PostTransformCallback dictionary key for the wrist: CustomHandScript
        /// maps segment "Hand" to XRHandJointID.Wrist. Never use "PalmBase" — it is
        /// a palm normal vector masquerading as a position in the segments dict.
        /// </summary>
        public const string WristBoneKey = "Hand";

        private const string WristAnchorName = "WristAnchor";

        [Header("References")]
        [SerializeField, Tooltip("HWDMerger in continuous mode; the solved offset is applied via SetCalibrationOffset.")]
        private HWDMerger hwdMerger;

        [SerializeField, Tooltip("CustomSubjectScript of the live Vicon hand. The wrist (Hand bone) is read via PostTransformCallback.")]
        private CustomSubjectScript handSubject;

        [SerializeField, Tooltip("HandMatSwitcher on the live hand; kept hidden during sampling, shown for verification.")]
        private HandMatSwitcher handMatSwitcher;

        [SerializeField, Tooltip("Static ghost hand prefab (a copy of the hand without Vicon scripts) with a WristAnchor child placed exactly on the Hand bone.")]
        private GameObject ghostPrefab;

        [SerializeField, Tooltip("CenterEyeAnchor transform; head rotation recorded per sample for the Model B diagnostic.")]
        private Transform centerEye;

        [SerializeField, Tooltip("Optional TMP text used to display instructions and status.")]
        private TMP_Text instructionsText;

        [Header("Targets")]
        [SerializeField, Tooltip("JSON asset with authored calibration targets (see the Calibration Target Authoring window).")]
        private TextAsset calibrationTargetsFile;

        [Header("Persistence")]
        [SerializeField, Tooltip("File name (under Application.persistentDataPath) for the solved offset.")]
        private string saveFileName = "hand_alignment_calibration.json";

        [Header("Timing")]
        [SerializeField, Tooltip("Wait after triggering MergeHWDs before sampling, so the snap and filters settle.")]
        private float mergeSettleTime = 1.5f;

        [SerializeField, Tooltip("Time to wait for the user to move to a target before failing.")]
        private float phaseTimeout = 20f;

        [SerializeField, Tooltip("Wrist must stay below the stability deadband for this long before sampling starts.")]
        private float stabilityDuration = 0.5f;

        [SerializeField, Tooltip("Duration of the per-target wrist position averaging window (accumulated over visible frames only).")]
        private float samplingDuration = 1.5f;

        [SerializeField, Tooltip("Duration of the verification sampling window.")]
        private float verificationDuration = 1f;

        [Header("Thresholds")]
        [SerializeField, Tooltip("Wrist speed (m/s) treated as a large transition motion towards a target.")]
        private float transitionSpeedThreshold = 0.25f;

        [SerializeField, Tooltip("Wrist speed (m/s) below which the hand counts as stable.")]
        private float stabilityVelocityDeadband = 0.05f;

        [SerializeField, Tooltip("Distance (m) from the ghost wrist within which the hand counts as being on target (bypasses the large-motion requirement).")]
        private float onTargetDistance = 0.08f;

        [SerializeField, Tooltip("Minimum number of targets required for the solve.")]
        private int minTargets = 5;

        [SerializeField, Tooltip("Samples dropped as outliers when their deviation from the mean exceeds this factor of the median deviation.")]
        private float outlierFactor = 2f;

        [SerializeField, Tooltip("Maximum residual spread (m) for the solve to be accepted; a larger spread fails loudly.")]
        private float residualClusterThreshold = 0.01f;

        [SerializeField, Tooltip("Maximum live residual (m) accepted at the verification target.")]
        private float verificationAcceptThreshold = 0.01f;

        [Header("Merge")]
        [SerializeField, Tooltip("Trigger MergeHWDs explicitly before waiting for settle (calibrating before the snap invalidates the solve).")]
        private bool triggerMergeExplicitly = true;

        /// <summary>
        /// Current state of the calibration routine.
        /// </summary>
        public CalibrationState State { get; private set; } = CalibrationState.Idle;

        /// <summary>
        /// The currently applied (or most recently solved) calibration offset.
        /// </summary>
        public Vector3 SolvedOffset { get; private set; }

        private GameObject ghostInstance;
        private Vector3 ghostWristLocalOffset;
        private Quaternion ghostWristLocalRotation;
        private List<CalibrationTarget> targets;
        private List<CalibrationTarget> solveTargets;
        private List<CalibrationTarget> verificationTargets;

        private Vector3 lastWristPosition;
        private float lastWristSpeed;
        private bool hasWristData;
        private int wristDataStamp = int.MinValue;
        private const int WristDataMaxAgeFrames = 2;
        private bool liveHandVisible;
        private bool subjectWasHidden;

        private Coroutine calibrationRoutine;

        [Serializable]
        private class SolvedCalibrationData
        {
            public float offsetX;
            public float offsetY;
            public float offsetZ;
            public string model;
            public float residualSpread;
            public int sampleCount;
            public string solvedUtc;
        }

        #region Unity methods
        /// <inheritdoc />
        protected void Awake()
        {
            Assert.IsNotNull(hwdMerger, "HandAlignmentCalibrator: hwdMerger is not set.");
            Assert.IsNotNull(handSubject, "HandAlignmentCalibrator: handSubject is not set.");
            Assert.IsNotNull(handMatSwitcher, "HandAlignmentCalibrator: handMatSwitcher is not set.");
            Assert.IsNotNull(ghostPrefab, "HandAlignmentCalibrator: ghostPrefab is not set.");

            Transform wristAnchor = FindChildByName(ghostPrefab.transform, WristAnchorName);
            Assert.IsNotNull(wristAnchor, $"HandAlignmentCalibrator: ghostPrefab must have a '{WristAnchorName}' child placed exactly on the Hand bone.");

            // Note: HandMatSwitcher.IsVisible initializes true in its Awake even
            // when the default state is invisible, so it is not trusted here;
            // visibility is tracked locally and forced via Show()/Hide().
            liveHandVisible = false;
            SolvedOffset = Vector3.zero;

            LoadPersistedOffset();
        }

        /// <inheritdoc />
        protected void OnEnable()
        {
            handSubject.PostTransformCallback += OnPostTransform;
            handSubject.OnHidingSubject.AddListener(OnSubjectHidden);
            handSubject.OnShowingSubject.AddListener(OnSubjectShown);
        }

        /// <inheritdoc />
        protected void OnDisable()
        {
            handSubject.PostTransformCallback -= OnPostTransform;
            handSubject.OnHidingSubject.RemoveListener(OnSubjectHidden);
            handSubject.OnShowingSubject.RemoveListener(OnSubjectShown);
        }
        #endregion

        #region Data acquisition
        private void OnPostTransform(Dictionary<string, Transform> bones)
        {
            if (bones.TryGetValue(WristBoneKey, out Transform wrist))
            {
                // Velocity is tracked here, at the source, so the transition and
                // stability gates always read a fresh per-frame speed; computing
                // it lazily in the gates would measure across sampling gaps and
                // report inflated speeds.
                if (hasWristData)
                {
                    lastWristSpeed = Vector3.Distance(wrist.position, lastWristPosition) / Mathf.Max(Time.deltaTime, 1e-5f);
                }
                lastWristPosition = wrist.position;
                hasWristData = true;
                wristDataStamp = Time.frameCount;
            }
        }

        private void OnSubjectHidden()
        {
            subjectWasHidden = true;
        }

        private void OnSubjectShown()
        {
            subjectWasHidden = false;
        }

        /// <summary>
        /// The live hand's visibility, tracked by this component (not
        /// HandMatSwitcher.IsVisible, which is unreliable before the first
        /// explicit Show()/Hide()).
        /// </summary>
        public bool LiveHandVisible => liveHandVisible;

        /// <summary>
        /// True when a wrist sample exists and is fresh (received within the last
        /// couple of frames). Guards against stream stalls that keep serving
        /// frozen data without triggering the subject-hidden events.
        /// </summary>
        private bool HasFreshWristData()
        {
            return hasWristData && Time.frameCount - wristDataStamp <= WristDataMaxAgeFrames;
        }
        #endregion

        #region Public control
        /// <summary>
        /// Manually triggers the calibration routine. Runs the full pipeline:
        /// merge settle, ghost targets, dwell sampling, solve, apply, verify.
        /// </summary>
        [Button("Start Calibration")]
        public void StartCalibration()
        {
            Assert.IsTrue(Application.isPlaying, "Calibration can only run in play mode.");
            Assert.IsNull(calibrationRoutine, "Calibration already running.");

            if (calibrationRoutine != null)
            {
                Debug.LogError("HandAlignmentCalibrator: calibration is already running.", this);
                return;
            }

            if (!hwdMerger.ContinuousMode)
            {
                Fail("HWDMerger is not in continuous mode; the calibration offset is only applied in the continuous path.");
                return;
            }

            calibrationRoutine = StartCoroutine(CalibrationRoutine());
        }

        /// <summary>
        /// Resets the applied calibration offset to zero and deletes the persisted file.
        /// </summary>
        [Button("Reset Calibration Offset")]
        public void ResetCalibrationOffset()
        {
            SolvedOffset = Vector3.zero;
            hwdMerger.SetCalibrationOffset(Vector3.zero);
            string path = SavePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            SetInstructions("Calibration offset reset.");
            Debug.Log("HandAlignmentCalibrator: calibration offset reset.", this);
        }
        #endregion

        #region Calibration routine
        private IEnumerator CalibrationRoutine()
        {
            State = CalibrationState.WaitingForMergeSettle;
            SetInstructions("Waiting for the HWD merge to settle...");
            Debug.Log("HandAlignmentCalibrator: starting guided hand-alignment calibration.", this);

            if (!LoadTargets())
            {
                yield break;
            }

            if (!SetupGhost())
            {
                yield break;
            }

            // Precondition: the MergeHWDs snap must have fired and settled before
            // any sampling; calibrating before the snap invalidates the solve.
            if (triggerMergeExplicitly)
            {
                // Cancel a pending auto-merge (scheduled at +5 s when autoMerge is
                // on): the snap moving the XR rig mid-calibration would inject a
                // step into the sampled residuals.
                hwdMerger.CancelInvoke(nameof(HWDMerger.MergeHWDs));
                hwdMerger.MergeHWDs();
            }
            yield return new WaitForSeconds(mergeSettleTime);

            // Force the live hand to the invisible/occlusion material so it does
            // not visually fight the ghost during sampling.
            handMatSwitcher.Hide();
            liveHandVisible = false;

            // Force visibility tracking to a known state by reading the events.
            subjectWasHidden = handSubject.SubjectHidden;

            List<Vector3> residuals = new List<Vector3>();
            List<Quaternion> headRotations = new List<Quaternion>();

            for (int i = 0; i < solveTargets.Count; ++i)
            {
                CalibrationTarget target = solveTargets[i];
                SetInstructions($"Target {i + 1}/{solveTargets.Count} ({target.poseVariant}): place your hand on the ghost hand and hold still.");
                PlaceGhostAt(target);

                yield return TransitionToTarget(target);
                if (State == CalibrationState.Failed)
                {
                    yield break;
                }

                yield return Stabilize(target);
                if (State == CalibrationState.Failed)
                {
                    yield break;
                }

                yield return SampleAveraged(samplingDuration);
                if (!lastSampleSuccess)
                {
                    Fail($"Failed to sample target {i + 1}: subject hidden or no wrist data for too long.");
                    yield break;
                }

                residuals.Add(lastSampleWrist - target.position);
                headRotations.Add(lastSampleHeadRotation);
                Debug.Log($"HandAlignmentCalibrator: target '{target.name}' sampled, wrist residual {Vector3.Magnitude(lastSampleWrist - target.position) * 1000f:F1} mm.", this);
            }

            // Solve.
            State = CalibrationState.Solving;
            SetInstructions("Solving calibration offset...");

            if (!Solve(residuals, headRotations, out Vector3 offset, out string model, out float spread))
            {
                // Solve() has already failed loudly; keep the ghost for inspection.
                yield break;
            }

            // Apply.
            SolvedOffset = offset;
            hwdMerger.SetCalibrationOffset(offset);
            Debug.Log($"HandAlignmentCalibrator: applied calibration offset {offset.ToString("F4")} m (model {model}, spread {spread * 1000f:F1} mm).", this);

            // Verification: one extra (verification-only) target, live hand
            // visible, live residual.
            State = CalibrationState.Verifying;
            SetInstructions("Verification: place your hand on the ghost hand one more time.");
            CalibrationTarget verificationTarget = verificationTargets[0];
            PlaceGhostAt(verificationTarget);

            handMatSwitcher.Show();
            liveHandVisible = true;

            yield return TransitionToTarget(verificationTarget);
            if (State == CalibrationState.Failed)
            {
                yield break;
            }

            yield return Stabilize(verificationTarget);
            if (State == CalibrationState.Failed)
            {
                yield break;
            }

            yield return SampleAveraged(verificationDuration);
            if (!lastSampleSuccess)
            {
                Fail("Verification failed: subject hidden or no wrist data for too long.");
                yield break;
            }

            float verificationResidual = Vector3.Magnitude(lastSampleWrist - verificationTarget.position);
            Debug.Log($"HandAlignmentCalibrator: verification residual {verificationResidual * 1000f:F1} mm (accept below {verificationAcceptThreshold * 1000f:F1} mm).", this);

            if (verificationResidual > verificationAcceptThreshold)
            {
                // The offset stays applied (better than nothing), but the solve
                // is reported as failed so the user can redo the routine.
                SetInstructions($"Verification failed ({verificationResidual * 1000f:F0} mm residual). Run the calibration again.");
                Debug.LogError($"HandAlignmentCalibrator: verification residual {verificationResidual * 1000f:F1} mm exceeds the {verificationAcceptThreshold * 1000f:F1} mm threshold. Redo the calibration.", this);
                State = CalibrationState.Failed;
                calibrationRoutine = null;
                yield break;
            }

            Persist(offset, model, spread, residuals.Count);

            SetInstructions($"Calibration complete. Offset {offset.x * 1000f:F0}, {offset.y * 1000f:F0}, {offset.z * 1000f:F0} mm.");
            Debug.Log("HandAlignmentCalibrator: calibration complete and persisted.", this);

            // Restore the MR default (invisible/occlusion) hand material.
            handMatSwitcher.Hide();
            liveHandVisible = false;

            State = CalibrationState.Complete;
            calibrationRoutine = null;
        }

        private bool LoadTargets()
        {
            if (calibrationTargetsFile == null)
            {
                Fail("calibrationTargetsFile is not set.");
                return false;
            }

            CalibrationTargetList list = CalibrationTargetList.FromJson(calibrationTargetsFile.text);
            List<CalibrationTarget> loaded = list.targets ?? new List<CalibrationTarget>();
            solveTargets = loaded.Where(t => !t.verificationOnly).ToList();
            verificationTargets = loaded.Where(t => t.verificationOnly).ToList();

            if (solveTargets.Count < minTargets)
            {
                Fail($"Calibration targets file has {solveTargets.Count} solve target(s), but at least {minTargets} are required for the solve.");
                return false;
            }

            if (verificationTargets.Count < 1)
            {
                Fail("Calibration targets file has no verification-only target. Add one (verificationOnly) so the solve can be checked against an extra target.");
                return false;
            }

            targets = loaded;
            return true;
        }

        private bool SetupGhost()
        {
            // A redo after a failed run must not leak the previous ghost.
            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
            }

            ghostInstance = Instantiate(ghostPrefab, transform);
            ghostInstance.name = $"{ghostPrefab.name}_CalibrationGhost";
            ghostInstance.SetActive(true);

            // Placement math assumes unit scale on the ghost root (and on this
            // component's transform, which the ghost is parented to).
            Debug.Assert(ghostInstance.transform.lossyScale == Vector3.one,
                         $"HandAlignmentCalibrator: ghost root scale {ghostInstance.transform.lossyScale} != 1; placement assumes unit scale.", this);

            // Defensive: a static ghost must not be driven by any Vicon scripts.
            foreach (CustomSubjectScript subjectScript in ghostInstance.GetComponentsInChildren<CustomSubjectScript>(true))
            {
                Debug.LogWarning($"HandAlignmentCalibrator: disabling {subjectScript.GetType().Name} on the ghost ({subjectScript.name}).", this);
                subjectScript.enabled = false;
            }

            Transform wristAnchor = FindChildByName(ghostInstance.transform, WristAnchorName);
            if (wristAnchor == null)
            {
                Fail($"Ghost prefab instance has no '{WristAnchorName}' child.");
                return false;
            }

            // Root to wrist offset is constant (bind pose; ghost bones are not driven).
            ghostWristLocalOffset = ghostInstance.transform.InverseTransformPoint(wristAnchor.position);
            ghostWristLocalRotation = Quaternion.Inverse(ghostInstance.transform.rotation) * wristAnchor.rotation;
            return true;
        }

        /// <summary>
        /// Places the ghost so that its WristAnchor lands exactly on the target pose.
        /// The ghost is authored by moving its root; the effective wrist target is
        /// root.localToWorldMatrix * wristLocalOffset.
        /// </summary>
        private void PlaceGhostAt(CalibrationTarget target)
        {
            Transform ghostRoot = ghostInstance.transform;
            Quaternion rootRotation = target.rotation * Quaternion.Inverse(ghostWristLocalRotation);
            Vector3 rootPosition = target.position - rootRotation * ghostWristLocalOffset;
            ghostRoot.SetPositionAndRotation(rootPosition, rootRotation);
        }

        /// <summary>
        /// Waits for a large wrist motion (transition to the new target) or for
        /// the hand to already be on target and stable. Fails on timeout.
        /// </summary>
        private IEnumerator TransitionToTarget(CalibrationTarget target)
        {
            State = CalibrationState.Transition;
            float elapsed = 0f;
            while (elapsed < phaseTimeout)
            {
                elapsed += Time.deltaTime;

                if (subjectWasHidden || !HasFreshWristData())
                {
                    // Motion detection paused while the subject data is missing
                    // or stale; the timeout still advances.
                    yield return null;
                    continue;
                }

                float speed = lastWristSpeed;
                float distanceToTarget = Vector3.Distance(lastWristPosition, target.position);
                if (speed > transitionSpeedThreshold || (distanceToTarget < onTargetDistance && speed < stabilityVelocityDeadband))
                {
                    yield break;
                }

                yield return null;
            }

            Fail($"Timeout waiting for the user to move to target '{target.name}'.");
        }

        /// <summary>
        /// Waits until the wrist is on target (within onTargetDistance of the
        /// ghost wrist) and stays below the velocity deadband for
        /// stabilityDuration (fresh-data frames only). Fails on timeout.
        /// </summary>
        private IEnumerator Stabilize(CalibrationTarget target)
        {
            State = CalibrationState.Stabilizing;
            float stableFor = 0f;
            float elapsed = 0f;
            while (stableFor < stabilityDuration && elapsed < phaseTimeout)
            {
                elapsed += Time.deltaTime;

                if (subjectWasHidden || !HasFreshWristData())
                {
                    // Pause stability accumulation while the subject is hidden
                    // or the data is stale; the timeout still advances.
                    yield return null;
                    continue;
                }

                if (Vector3.Distance(lastWristPosition, target.position) < onTargetDistance &&
                    lastWristSpeed < stabilityVelocityDeadband)
                {
                    stableFor += Time.deltaTime;
                }
                else
                {
                    stableFor = 0f;
                }

                yield return null;
            }

            if (stableFor < stabilityDuration)
            {
                Fail($"Timeout waiting for the hand to stabilize on target '{target.name}'.");
            }
        }

        /// <summary>
        /// Averages the wrist world position and camera rotation over the given
        /// duration (accumulated over visible frames only), gating on subject
        /// visibility/data quality. Hidden frames extend the window. Results
        /// are written to <see cref="lastSampleWrist"/>,
        /// <see cref="lastSampleHeadRotation"/> and <see cref="lastSampleSuccess"/>.
        /// </summary>
        private IEnumerator SampleAveraged(float duration)
        {
            State = CalibrationState.Sampling;
            lastSampleWrist = Vector3.zero;
            lastSampleHeadRotation = Quaternion.identity;
            lastSampleSuccess = false;

            Vector3 positionSum = Vector3.zero;
            Vector4 rotationSum = Vector4.zero;
            int frameCount = 0;
            float accumulated = 0f;
            float elapsed = 0f;

            while (accumulated < duration && elapsed < duration + phaseTimeout)
            {
                if (subjectWasHidden || !HasFreshWristData())
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                    continue;
                }

                positionSum += lastWristPosition;
                Quaternion r = (centerEye != null ? centerEye.rotation : Quaternion.identity);
                rotationSum += new Vector4(r.x, r.y, r.z, r.w);
                ++frameCount;
                accumulated += Time.deltaTime;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (frameCount > 0 && accumulated >= duration)
            {
                lastSampleWrist = positionSum / frameCount;
                Quaternion averaged = new Quaternion(rotationSum.x / frameCount, rotationSum.y / frameCount, rotationSum.z / frameCount, rotationSum.w / frameCount);
                // Component-wise averaging is degenerate when the head swings far
                // within the window (near-zero quaternion); fall back to the last
                // raw rotation instead of normalizing to NaNs.
                lastSampleHeadRotation = averaged.sqrMagnitude < 1e-6f
                    ? (centerEye != null ? centerEye.rotation : Quaternion.identity)
                    : averaged.normalized;
                lastSampleSuccess = true;
            }
        }

        private Vector3 lastSampleWrist;
        private Quaternion lastSampleHeadRotation;
        private bool lastSampleSuccess;
        #endregion

        #region Solve
        /// <summary>
        /// Solves the residual model. Model A (expected): a constant world-space
        /// translation (robust mean of the residuals). Model B (diagnostic): a
        /// head-local offset (residuals rotated into camera space). The model
        /// with the tighter residual spread wins; Model B is reported and
        /// aborted, and a spread above the threshold fails loudly either way.
        /// </summary>
        private bool Solve(List<Vector3> residuals, List<Quaternion> headRotations, out Vector3 offset, out string model, out float spread)
        {
            Assert.AreEqual(residuals.Count, headRotations.Count, "Residual and head rotation counts must match.");
            offset = Vector3.zero;
            model = null;
            spread = float.MaxValue;

            List<Vector3> headLocal = new List<Vector3>(residuals.Count);
            for (int i = 0; i < residuals.Count; ++i)
            {
                headLocal.Add(Quaternion.Inverse(headRotations[i]) * residuals[i]);
            }

            Vector3 modelAMean = RobustMean(residuals, outlierFactor, minTargets, out List<Vector3> modelACore);
            float modelASpread = Spread(modelACore, modelAMean);

            Vector3 modelBMean = RobustMean(headLocal, outlierFactor, minTargets, out List<Vector3> modelBCore);
            float modelBSpread = Spread(modelBCore, modelBMean);

            Debug.Log($"HandAlignmentCalibrator: Model A (constant world offset) spread {modelASpread * 1000f:F1} mm, mean {modelAMean.ToString("F4")} m; " +
                      $"Model B (head-local offset) spread {modelBSpread * 1000f:F1} mm, mean {modelBMean.ToString("F4")} m.", this);

            if (modelBSpread < modelASpread * 0.8f && modelBSpread < residualClusterThreshold)
            {
                // Unexpected: the error clusters head-locally, which would
                // indicate an eyeOffset-type error instead. Report and abort.
                Debug.LogError($"HandAlignmentCalibrator: Model B (head-local) wins with spread {modelBSpread * 1000f:F1} mm vs Model A {modelASpread * 1000f:F1} mm. " +
                               "This indicates a head-local/eyeOffset-type error, which is out of scope for this calibration. Report this to the dev; not applying any offset.", this);
                SetInstructions("Unexpected head-local error detected (Model B). Calibration aborted; report this result.");
                State = CalibrationState.Failed;
                calibrationRoutine = null;
                return false;
            }

            if (modelASpread > residualClusterThreshold)
            {
                Debug.LogError($"HandAlignmentCalibrator: residual spread {modelASpread * 1000f:F1} mm exceeds the {residualClusterThreshold * 1000f:F0} mm threshold; neither model clusters. " +
                               "The offset hypothesis is not supported by this data. Calibration aborted.", this);
                SetInstructions($"Residuals do not cluster ({modelASpread * 1000f:F0} mm spread). Calibration aborted; redo the routine.");
                State = CalibrationState.Failed;
                calibrationRoutine = null;
                return false;
            }

            offset = modelAMean;
            model = "ConstantWorldOffset";
            spread = modelASpread;
            return true;
        }

        /// <summary>
        /// Mean of the samples, dropping outliers (deviation more than
        /// outlierFactor times the median deviation) once enough samples exist.
        /// Outputs the samples actually used.
        /// </summary>
        private static Vector3 RobustMean(List<Vector3> samples, float outlierFactor, int minSamplesForOutlierRemoval, out List<Vector3> usedSamples)
        {
            Assert.IsNotEmpty(samples, "RobustMean: no samples.");

            usedSamples = samples;
            Vector3 mean = Mean(samples);

            if (samples.Count >= minSamplesForOutlierRemoval)
            {
                List<float> deviations = new List<float>(samples.Count);
                foreach (Vector3 sample in samples)
                {
                    deviations.Add(Vector3.Distance(sample, mean));
                }

                float medianDeviation = Median(deviations);
                List<Vector3> kept = new List<Vector3>();
                for (int i = 0; i < samples.Count; ++i)
                {
                    if (deviations[i] <= outlierFactor * medianDeviation)
                    {
                        kept.Add(samples[i]);
                    }
                }

                // Only drop outliers if enough samples survive.
                if (kept.Count >= 3 && kept.Count < samples.Count)
                {
                    Debug.Log($"HandAlignmentCalibrator: dropped {samples.Count - kept.Count} outlier sample(s).");
                    usedSamples = kept;
                    mean = Mean(kept);
                }
            }

            return mean;
        }

        private static Vector3 Mean(List<Vector3> samples)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 sample in samples)
            {
                sum += sample;
            }
            return sum / samples.Count;
        }

        /// <summary>
        /// Mean distance of the samples from the given center.
        /// </summary>
        private static float Spread(List<Vector3> samples, Vector3 center)
        {
            float sum = 0f;
            foreach (Vector3 sample in samples)
            {
                sum += Vector3.Distance(sample, center);
            }
            return sum / samples.Count;
        }

        private static float Median(List<float> values)
        {
            List<float> sorted = new List<float>(values);
            sorted.Sort();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) * 0.5f;
        }
        #endregion

        #region Persistence
        private string SavePath()
        {
            return Path.Combine(Application.persistentDataPath, saveFileName);
        }

        private void Persist(Vector3 offset, string model, float spread, int sampleCount)
        {
            SolvedCalibrationData data = new SolvedCalibrationData
            {
                offsetX = offset.x,
                offsetY = offset.y,
                offsetZ = offset.z,
                model = model,
                residualSpread = spread,
                sampleCount = sampleCount,
                solvedUtc = DateTime.UtcNow.ToString("o"),
            };
            File.WriteAllText(SavePath(), JsonUtility.ToJson(data, true));
            Debug.Log($"HandAlignmentCalibrator: persisted offset to {SavePath()}.", this);
        }

        /// <summary>
        /// Loads the persisted offset, if any, and applies it. The verification
        /// target in the calibration routine is the cheap per-session validity
        /// check: a stale offset (the offset is measured relative to the
        /// TrackingSpace origin that MergeHWDs re-snaps per session) shows up as
        /// a verification failure.
        /// </summary>
        private void LoadPersistedOffset()
        {
            string path = SavePath();
            if (!File.Exists(path))
            {
                return;
            }

            SolvedCalibrationData data = JsonUtility.FromJson<SolvedCalibrationData>(File.ReadAllText(path));
            if (data == null)
            {
                Debug.LogWarning($"HandAlignmentCalibrator: failed to parse {path}; ignoring saved offset.", this);
                return;
            }

            SolvedOffset = new Vector3(data.offsetX, data.offsetY, data.offsetZ);
            hwdMerger.SetCalibrationOffset(SolvedOffset);
            Debug.Log($"HandAlignmentCalibrator: loaded persisted offset {SolvedOffset.ToString("F4")} m (solved {data.solvedUtc}, spread {data.residualSpread * 1000f:F1} mm).", this);
        }
        #endregion

        #region Helpers
        private void Fail(string message)
        {
            Debug.LogError($"HandAlignmentCalibrator: {message}", this);
            SetInstructions($"Calibration failed: {message}");
            State = CalibrationState.Failed;
            calibrationRoutine = null;
        }

        private void SetInstructions(string message)
        {
            if (instructionsText != null)
            {
                instructionsText.text = message;
            }
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform result = FindChildByName(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
        #endregion
    }
}
