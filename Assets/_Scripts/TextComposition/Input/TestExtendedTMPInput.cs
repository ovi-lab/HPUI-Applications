using EditorAttributes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TMPro
{
    /// <summary>
    /// Simple runtime/editor tester for ExtendedTMPInputField.
    /// Uses EditorAttributes buttons to exercise caret movement, coordinate-based
    /// caret placement, visual selection mode, and selection inspection.
    ///
    /// Assumes the target ExtendedTMPInputField lives on a World Space canvas,
    /// so a camera reference is required to convert the normalized (0-1)
    /// click position into a screen-space position.
    /// </summary>
    [AddComponentMenu("UI (Canvas)/TextMeshPro - Extended Input Field Tester")]
    public class TestExtendedTMPInput : MonoBehaviour
    {
        [Header("Target")]
        [Required]
        [SerializeField]
        private ExtendedTMPInputField targetInputField;

        [Header("Camera (World Space Canvas)")]
        [Tooltip("The camera used to convert the normalized click position into a screen-space position. Change this / move it around to see how camera angle affects SetCaretFromScreenPosition.")]
        [Required]
        [SerializeField]
        private Camera eventCamera;

        [Header("Simulated Click Position")]
        [Tooltip("Normalized viewport position (0-1) on eventCamera used for the click simulation and the gizmo.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float clickViewportX = 0.5f;

        [Range(0f, 1f)]
        [SerializeField]
        private float clickViewportY = 0.5f;

        [Header("Gizmo")]
        [SerializeField]
        private bool drawGizmo = true;

        [SerializeField]
        private float gizmoRadius = 0.05f;

        [SerializeField]
        private Color gizmoColor = Color.red;

        [Tooltip("Ignore raycast hits further than this from the camera (safety net for cameras facing away from the canvas).")]
        [SerializeField]
        private float gizmoMaxRayDistance = 100f;

        // ---------------------------------------------------------------
        // Focus
        // ---------------------------------------------------------------

        [Button("Ensure Focused", 22f)]
        public void TestEnsureFocused()
        {
            if (!ValidateTarget())
                return;

            targetInputField.EnsureFocused();
            Debug.Log("[Tester] EnsureFocused called.", this);
        }

        // ---------------------------------------------------------------
        // Caret movement
        // ---------------------------------------------------------------

        [Button("Move Left", 22f)]
        public void TestMoveLeft() => MoveAndLog(Vector2Int.left);

        [Button("Move Right", 22f)]
        public void TestMoveRight() => MoveAndLog(Vector2Int.right);

        [Button("Move Up", 22f)]
        public void TestMoveUp() => MoveAndLog(Vector2Int.up);

        [Button("Move Down", 22f)]
        public void TestMoveDown() => MoveAndLog(Vector2Int.down);

        private void MoveAndLog(Vector2Int dir)
        {
            if (!ValidateTarget())
                return;

            targetInputField.MoveCaret(dir);

            targetInputField.GetSelectionStringRange(out int start, out int end);
            Debug.Log($"[Tester] MoveCaret({dir}) -> selection range = [{start}, {end})", this);
        }

        // ---------------------------------------------------------------
        // Click simulation
        // ---------------------------------------------------------------

        [Button("Simulate Screen Click", 24f)]
        public void TestSimulateClick()
        {
            if (!ValidateTarget())
                return;

            if (eventCamera == null)
            {
                Debug.LogWarning("[Tester] No event camera assigned, cannot compute a screen position.", this);
                return;
            }

            Vector2 screenPos = GetScreenPositionFromViewport();
            targetInputField.SetCaretFromScreenPosition(screenPos, eventCamera);

            targetInputField.GetSelectionStringRange(out int start, out int end);
            Debug.Log($"[Tester] SetCaretFromScreenPosition(viewport=({clickViewportX:F2}, {clickViewportY:F2}) -> screen={screenPos}) -> selection range = [{start}, {end})", this);
        }

        private Vector2 GetScreenPositionFromViewport()
        {
            Vector3 screen3D = eventCamera.ViewportToScreenPoint(new Vector3(clickViewportX, clickViewportY, 0f));
            return new Vector2(screen3D.x, screen3D.y);
        }

        // ---------------------------------------------------------------
        // Visual selection mode
        // ---------------------------------------------------------------

        [Button("Toggle Visual Selection Mode", 24f)]
        public void TestToggleSelectionMode()
        {
            if (!ValidateTarget())
                return;

            targetInputField.ToggleVisualSelectionMode();
            Debug.Log($"[Tester] VisualSelectionMode = {targetInputField.VisualSelectionMode} (origin string pos = {targetInputField.SelectionOriginStringPosition})", this);
        }

        [Button("Clear Selection", 22f)]
        public void TestClearSelection()
        {
            if (!ValidateTarget())
                return;

            targetInputField.ClearSelection();
            Debug.Log("[Tester] ClearSelection called.", this);
        }

        // ---------------------------------------------------------------
        // Selection inspection
        // ---------------------------------------------------------------

        [Button("Print Current Selection", 24f)]
        public void TestPrintSelection()
        {
            if (!ValidateTarget())
                return;

            targetInputField.GetSelectionStringRange(out int start, out int end);
            bool hasSelection = targetInputField.HasSelection();
            string selectedText = targetInputField.GetSelectedText();

            Debug.Log($"[Tester] HasSelection={hasSelection}, Range=[{start}, {end}), Text=\"{selectedText}\"", this);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private bool ValidateTarget()
        {
            if (targetInputField == null)
            {
                Debug.LogWarning("[Tester] No target input field assigned.", this);
                return false;
            }

            return true;
        }

        // ---------------------------------------------------------------
        // Gizmo
        // ---------------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (!drawGizmo || eventCamera == null || targetInputField == null)
                return;

            RectTransform fieldRect = targetInputField.transform as RectTransform;
            if (fieldRect == null)
                return;

            // Plane matching the input field's canvas surface.
            Plane canvasPlane = new Plane(fieldRect.forward, fieldRect.position);
            Ray ray = eventCamera.ViewportPointToRay(new Vector3(clickViewportX, clickViewportY, 0f));

            if (canvasPlane.Raycast(ray, out float distance) && distance <= gizmoMaxRayDistance)
            {
                Vector3 hitPoint = ray.GetPoint(distance);

                Gizmos.color = gizmoColor;
                Gizmos.DrawSphere(hitPoint, gizmoRadius);
                Gizmos.DrawWireSphere(hitPoint, gizmoRadius * 1.5f);
                Gizmos.DrawLine(eventCamera.transform.position, hitPoint);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Repaint immediately so dragging the viewport sliders updates the gizmo live.
            SceneView.RepaintAll();
        }
#endif
    }
}
