using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace _Scripts.Vicon.Editor
{
    /// <summary>
    /// Editor window for authoring hand-alignment calibration targets. Place
    /// the ghost instance in the scene, orient it at the desired pose, capture
    /// the WristAnchor world pose, repeat for 5-7 targets spread across the
    /// FOV in yaw/pitch and depth, then save the JSON file that
    /// <see cref="HandAlignmentCalibrator"/> loads at runtime.
    ///
    /// Targets are authored in Unity world/scene coordinates: frame-consistent
    /// with the solve (both the ghost target and the sampled wrist are sampled
    /// in the same world frame; the runtime MergeHWDs snap moves TrackingSpace
    /// and the XR camera, but not scene objects).
    /// </summary>
    public class CalibrationTargetAuthoring : EditorWindow
    {
        private const string DefaultSavePath = "Assets/_Scripts/Vicon/CalibrationTargets.json";
        private const string WristAnchorName = "WristAnchor";

        [SerializeField] private GameObject ghostInstance;
        [SerializeField] private Transform reference;
        [SerializeField] private List<CalibrationTarget> targets = new List<CalibrationTarget>();
        [SerializeField] private string nextPoseVariant = "palm-down";
        [SerializeField] private bool nextVerificationOnly;
        [SerializeField] private string savePath = DefaultSavePath;

        [SerializeField, Min(1)] private int minTargets = 5;

        [SerializeField, Min(0.01f)] private float minPairSeparation = 0.1f;
        [SerializeField, Min(0.01f)] private float maxReachDistance = 0.75f;

        private Vector2 scrollPosition;

        [MenuItem("Tools/Calibration/Hand Alignment Target Authoring")]
        public static void Open()
        {
            CalibrationTargetAuthoring window = GetWindow<CalibrationTargetAuthoring>("Calibration Targets");
            window.minSize = new Vector2(400, 500);
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Ghost", EditorStyles.boldLabel);
            ghostInstance = (GameObject)EditorGUILayout.ObjectField("Ghost instance", ghostInstance, typeof(GameObject), true);
            EditorGUILayout.HelpBox("Place the ghost hand instance in the scene (root placement), oriented at the desired pose, then capture. The WristAnchor child defines the pose that is recorded and used by the solve.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reference", EditorStyles.boldLabel);
            reference = (Transform)EditorGUILayout.ObjectField("Reference (XR Origin / CalibrationOrigin)", reference, typeof(Transform), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Next capture", EditorStyles.boldLabel);
            nextPoseVariant = EditorGUILayout.TextField("Pose variant", nextPoseVariant);
            nextVerificationOnly = EditorGUILayout.Toggle(new GUIContent("Verification only", "Exclude this target from the solve; use it only as the extra target the corrected hand is verified against."), nextVerificationOnly);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Targets ({targets.Count})", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(ghostInstance == null))
            {
                if (GUILayout.Button("Capture Target"))
                {
                    CaptureTarget();
                }
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < targets.Count; ++i)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawTarget(i);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(targets.Count == 0))
            {
                if (GUILayout.Button("Clear All Targets"))
                {
                    if (EditorUtility.DisplayDialog("Clear targets", "Remove all captured targets?", "Yes", "No"))
                    {
                        targets.Clear();
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            minPairSeparation = EditorGUILayout.FloatField("Min pairwise separation (m)", minPairSeparation);
            maxReachDistance = EditorGUILayout.FloatField("Max reach distance (m)", maxReachDistance);
            DrawValidation();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Save / Load", EditorStyles.boldLabel);
            savePath = EditorGUILayout.TextField("JSON path", savePath);
            using (new EditorGUI.DisabledScope(targets.Count == 0))
            {
                if (GUILayout.Button("Save Targets"))
                {
                    SaveTargets();
                }
            }
            if (GUILayout.Button("Load Targets"))
            {
                LoadTargets();
            }
        }

        private void CaptureTarget()
        {
            Assert.IsNotNull(ghostInstance, "Ghost instance is not assigned.");
            if (PrefabUtility.IsPartOfPrefabAsset(ghostInstance))
            {
                Debug.LogError("CalibrationTargetAuthoring: the assigned ghost is a prefab asset. Assign a scene instance instead (capturing from an asset would record asset-space poses).", ghostInstance);
                EditorUtility.DisplayDialog("Invalid ghost", "Assign a ghost scene instance, not a prefab asset.", "OK");
                return;
            }

            Transform ghostRoot = ghostInstance.transform;
            Transform wristAnchor = FindChildByName(ghostRoot, WristAnchorName);
            if (wristAnchor == null)
            {
                Debug.LogError($"CalibrationTargetAuthoring: ghost instance has no '{WristAnchorName}' child.", ghostInstance);
                return;
            }

            targets.Add(new CalibrationTarget
            {
                name = $"Target_{targets.Count}",
                poseVariant = nextPoseVariant,
                position = wristAnchor.position,
                rotation = wristAnchor.rotation,
                verificationOnly = nextVerificationOnly,
            });
        }

        private void DrawTarget(int index)
        {
            CalibrationTarget target = targets[index];
            target.name = EditorGUILayout.TextField("Name", target.name);
            target.poseVariant = EditorGUILayout.TextField("Pose variant", target.poseVariant);
            target.position = EditorGUILayout.Vector3Field("Wrist position", target.position);
            target.rotation = Quaternion.Euler(EditorGUILayout.Vector3Field("Wrist rotation (euler)", target.rotation.eulerAngles));
            target.verificationOnly = EditorGUILayout.Toggle(new GUIContent("Verification only", "Excluded from the solve; used only as the verification target."), target.verificationOnly);
            if (target.verificationOnly)
            {
                EditorGUILayout.HelpBox("Verification-only target: excluded from the solve.", MessageType.None);
            }

            if (reference != null)
            {
                Vector3 toTarget = target.position - reference.position;
                EditorGUILayout.LabelField($"Distance from reference: {toTarget.magnitude:F3} m");
                EditorGUILayout.LabelField($"{YawPitchString(toTarget)}");
                if (toTarget.magnitude > maxReachDistance)
                {
                    EditorGUILayout.HelpBox("Beyond comfortable reach.", MessageType.Warning);
                }
            }

            float minSeparation = MinPairwiseSeparation(index);
            if (minSeparation < minPairSeparation)
            {
                EditorGUILayout.HelpBox($"Too close to another target ({minSeparation * 1000f:F0} mm). Ill-conditioned spread.", MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            if (index > 0 && GUILayout.Button("Move Up"))
            {
                (targets[index - 1], targets[index]) = (targets[index], targets[index - 1]);
            }
            if (GUILayout.Button("Remove"))
            {
                targets.RemoveAt(index);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawValidation()
        {
            if (reference != null)
            {
                Vector3 referenceFlatForward = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
                if (referenceFlatForward.sqrMagnitude < 1e-6f)
                {
                    EditorGUILayout.HelpBox("Reference forward is vertical; yaw is ill-defined.", MessageType.Warning);
                }
            }

            if (targets.Count(t => !t.verificationOnly) < minTargets)
            {
                EditorGUILayout.HelpBox($"Fewer than {minTargets} solve targets (excluding verification-only); the solve requires at least {minTargets}.", MessageType.Warning);
            }

            if (targets.Count(t => t.verificationOnly) < 1)
            {
                EditorGUILayout.HelpBox("No verification-only target; add one so the solve can be checked against an extra pose.", MessageType.Warning);
            }
        }

        private string YawPitchString(Vector3 toTarget)
        {
            Vector3 flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            Vector3 referenceFlatForward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            float yaw = Vector3.SignedAngle(referenceFlatForward, flat.normalized, Vector3.up);
            float pitch = Vector3.Angle(flat, toTarget) * Mathf.Sign(toTarget.y);
            return $"Yaw {yaw:F1}°, pitch {pitch:F1}° (relative to reference forward)";
        }

        private float MinPairwiseSeparation(int index)
        {
            float min = float.MaxValue;
            for (int i = 0; i < targets.Count; ++i)
            {
                if (i == index)
                {
                    continue;
                }
                min = Mathf.Min(min, Vector3.Distance(targets[index].position, targets[i].position));
            }
            return min;
        }

        private void SaveTargets()
        {
            CalibrationTargetList list = new CalibrationTargetList { targets = targets };
            File.WriteAllText(savePath, list.ToJson());
            AssetDatabase.Refresh();
            Debug.Log($"CalibrationTargetAuthoring: saved {targets.Count} targets to {savePath}.");
        }

        private void LoadTargets()
        {
            if (!File.Exists(savePath))
            {
                Debug.LogError($"CalibrationTargetAuthoring: file not found: {savePath}");
                return;
            }

            CalibrationTargetList list = CalibrationTargetList.FromJson(File.ReadAllText(savePath));
            targets = list.targets;
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
    }
}
