using UnityEditor;
using UnityEngine;

namespace ubco.ovilab.ViconUnityStream.Editor
{
    /// <summary>
    /// Editor window for the calibration capture tooling (issue #28, phase 1).
    /// Play-mode only: Freeze / Record / Unfreeze buttons, sample count, and a
    /// status line. Communicates with the scene's <see cref="CalibrationCapture"/> via
    /// <see cref="Object.FindAnyObjectByType{T}"/>.
    /// </summary>
    public class CalibrationCaptureWindow : EditorWindow
    {
        [MenuItem("Tools/Calibration Capture")]
        public static void ShowWindow()
        {
            GetWindow<CalibrationCaptureWindow>("Calibration Capture");
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter play mode to use the calibration capture tooling.", MessageType.Info);
                CalibrationCapture target = FindAnyObjectByType<CalibrationCapture>();
                if (target == null)
                {
                    EditorGUILayout.HelpBox("No CalibrationCapture component found in the open scene. Add it to the scene and wire the plate and center-eye camera references.", MessageType.Warning);
                }
                return;
            }

            CalibrationCapture capture = FindAnyObjectByType<CalibrationCapture>();
            if (capture == null)
            {
                EditorGUILayout.HelpBox("No CalibrationCapture component found in the scene. Add it to the scene and wire the plate and center-eye camera references.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Status", capture.IsFrozen ? "Frozen" : "Live");
            EditorGUILayout.LabelField("Samples recorded", capture.SampleCount.ToString());
            if (!string.IsNullOrEmpty(capture.LogPath))
            {
                EditorGUILayout.LabelField("Log", capture.LogPath);
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Capture matrix labels (set before Record)");
            capture.PlateLocationId = EditorGUILayout.IntField(new GUIContent("Plate location id", "Id of the plate's physical spot for this batch; mapping to lab positions lives in lab notes."), capture.PlateLocationId);
            capture.ViewerLocationId = EditorGUILayout.IntField(new GUIContent("Viewer location id", "Id of the viewing/headset spot for this sample."), capture.ViewerLocationId);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(capture.IsFrozen))
            {
                if (GUILayout.Button("Freeze"))
                {
                    capture.Freeze();
                }
            }

            using (new EditorGUI.DisabledScope(!capture.IsFrozen))
            {
                if (GUILayout.Button("Record"))
                {
                    capture.Record();
                }

                if (GUILayout.Button("Unfreeze"))
                {
                    capture.Unfreeze();
                }
            }
        }
    }
}
