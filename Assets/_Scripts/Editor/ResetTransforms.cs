using UnityEditor;
using UnityEngine;

namespace _Scripts.Legacy.v1.Editor
{
    public static class ResetTransforms
    {
        [MenuItem("GameObject/Reset Transform #r")]
        public static void ResetTransform()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null) return;
            Undo.RegisterCompleteObjectUndo(go.transform, "Transform Reset");
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
    }
}