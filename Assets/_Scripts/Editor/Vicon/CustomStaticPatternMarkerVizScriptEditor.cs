using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ubco.ovilab.ViconUnityStream.Editor
{
    [CustomEditor(typeof(CustomStaticPatternMarkerVizScript), true)]
    [CanEditMultipleObjects]
    public class CustomStaticPatternMarkerVizScriptEditor : CustomSubjectScriptEditor
    {
        protected override string[] excludedSerializedNames => base.excludedSerializedNames.Union(new string[] { "specifyRight", "rightSegment1", "rightSegment2", "upSegment1", "upSegment2" }).ToArray();

        private CustomStaticPatternMarkerVizScriptEditor customStaticPatternMarkerVizScriptEditor;

        private SerializedProperty specifyRightProperty;
        private SerializedProperty rightSegment1Property;
        private SerializedProperty rightSegment2Property;
        private SerializedProperty upSegment1Property;
        private SerializedProperty upSegment2Property;

        protected override void OnEnable()
        {
            base.OnEnable();
            customStaticPatternMarkerVizScriptEditor = target as CustomStaticPatternMarkerVizScriptEditor;

            specifyRightProperty = serializedObject.FindProperty("specifyRight");

            rightSegment1Property = serializedObject.FindProperty("rightSegment1");
            rightSegment2Property = serializedObject.FindProperty("rightSegment2");
            upSegment1Property = serializedObject.FindProperty("upSegment1");
            upSegment2Property = serializedObject.FindProperty("upSegment2");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            bool specifyRight = specifyRightProperty.boolValue;

            bool guiEnabled = GUI.enabled;

            EditorGUILayout.PropertyField(specifyRightProperty);

            GUI.enabled = specifyRight;
            EditorGUILayout.PropertyField(rightSegment1Property);
            EditorGUILayout.PropertyField(rightSegment2Property);

            GUI.enabled = !specifyRight;
            EditorGUILayout.PropertyField(upSegment1Property);
            EditorGUILayout.PropertyField(upSegment2Property);
            GUI.enabled = guiEnabled;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
