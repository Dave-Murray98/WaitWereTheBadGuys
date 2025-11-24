// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using UnityEditor;

namespace Infohazard.HyperNav.Editor {
    [CanEditMultipleObjects]
    [CustomEditor(typeof(SplineNavAgent))]
    public class SplineNavAgentEditor : NavAgentEditor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            SerializedProperty areaTypeMaskProperty = serializedObject.FindProperty(NavAgent.PropNames.AreaTypeMask);
            NavAreaTypes areaTypeMask = (NavAreaTypes) areaTypeMaskProperty.intValue;
            bool isVolume = (areaTypeMask & NavAreaTypes.Volume) != 0;

            if (!isVolume) {
                EditorGUILayout.HelpBox("SplineNavAgent only works when navigating in a volume.", MessageType.Error);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Spline Generation Properties", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(SplineNavAgent.PropNames.TangentScale));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(SplineNavAgent.PropNames.RaycastTangents));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(SplineNavAgent.PropNames.DistanceSamplesPerSegment));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(SplineNavAgent.PropNames.DebugPointCount));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Spline Following Properties", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(SplineNavAgent.PropNames.MaxAlignmentVelocityDistance));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(SplineNavAgent.PropNames.CurvatureSampleDistance));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(SplineNavAgent.PropNames.CurvatureOfMaxSlowdown));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(SplineNavAgent.PropNames.MaxCurvatureSlowdown));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(SplineNavAgent.PropNames.DebugProjectOnSpline));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Blocked Detection Properties", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(SplineNavAgent.PropNames.BlockedDetectionDistance));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(SplineNavAgent.PropNames.BlockedDetectionBackDistance));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(SplineNavAgent.PropNames.BlockedDetectionMinSplineDistance));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(SplineNavAgent.PropNames.BlockedDetectionPhysicsQuery));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(SplineNavAgent.PropNames.BlockedDetectionPhysicsQueryRigidbody));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
