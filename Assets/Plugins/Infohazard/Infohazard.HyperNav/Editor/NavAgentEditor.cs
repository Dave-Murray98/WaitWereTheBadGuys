// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System.Linq;
using Infohazard.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace Infohazard.HyperNav.Editor {
    [CanEditMultipleObjects]
    [CustomEditor(typeof(NavAgent))]
    public class NavAgentEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();

            EditorGUILayout.LabelField("Pathfinding Properties", EditorStyles.boldLabel);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(serializedObject.FindProperty(NavAgent.PropNames.SampleRadius));

            SerializedProperty areaTypeMaskProperty = serializedObject.FindProperty(NavAgent.PropNames.AreaTypeMask);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(areaTypeMaskProperty);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(serializedObject.FindProperty(NavAgent.PropNames.AreaLayerMask));
            serializedObject.ApplyModifiedProperties();
            NavAreaTypes areaTypeMask = (NavAreaTypes) areaTypeMaskProperty.intValue;
            bool isVolume = (areaTypeMask & NavAreaTypes.Volume) != 0;
            bool isSurface = (areaTypeMask & NavAreaTypes.Surface) != 0;

            if (isVolume && isSurface) {
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavAgent.PropNames.StartSamplingPriority));
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavAgent.PropNames.DestSamplingPriority));
            }

            if (isVolume) {
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavAgent.PropNames.VolumeSamplingTransform));
            }

            if (isSurface) {
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavAgent.PropNames.SurfaceSamplingTransform));

                SerializedProperty checkSurfaceUpProperty =
                    serializedObject.FindProperty(NavAgent.PropNames.CheckSurfaceUpVector);
                CoreDrawers.DrawPropertyWithHelpBoxSupport(checkSurfaceUpProperty);

                if (checkSurfaceUpProperty.boolValue) {
                    CoreDrawers.DrawPropertyWithHelpBoxSupport(
                        serializedObject.FindProperty(NavAgent.PropNames.MinSurfaceUpVectorDot));
                }
            }

            if (isVolume && isSurface) {
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavAgent.PropNames.CostToChangeToVolume));
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavAgent.PropNames.CostToChangeToSurface));
            }

            if (isVolume) {
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavAgent.PropNames.VolumeCostMultiplier));
            }

            if (isSurface) {
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavAgent.PropNames.SurfaceCostMultiplier));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Path Following Properties", EditorStyles.boldLabel);

            CoreDrawers.DrawPropertyWithHelpBoxSupport(serializedObject.FindProperty(NavAgent.PropNames.Acceptance));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                serializedObject.FindProperty(NavAgent.PropNames.AccelerationEstimate));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(serializedObject.FindProperty(NavAgent.PropNames.Rigidbody));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                serializedObject.FindProperty(NavAgent.PropNames.DesiredSpeedRatio));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(serializedObject.FindProperty(NavAgent.PropNames.DebugPath));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                serializedObject.FindProperty(NavAgent.PropNames.KeepPathWhileCalculating));

            SerializedProperty avoidanceAgentProperty =
                serializedObject.FindProperty(NavAgent.PropNames.AvoidanceAgent);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(avoidanceAgentProperty);

            if (targets.OfType<NavAgent>().Any(agent => agent.AvoidanceAgent == null)) {
                if (GUILayout.Button("Add Avoidance Agent")) {
                    foreach (NavAgent agent in targets.OfType<NavAgent>()) {
                        if (agent.AvoidanceAgent != null) continue;
                        Undo.RecordObject(agent, "Add Avoidance Agent");
                        AvoidanceAgent avoidanceAgent = Undo.AddComponent<AvoidanceAgent>(agent.gameObject);
                        agent.AvoidanceAgent = avoidanceAgent;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(agent);
                    }
                }
            }

            if (targets.OfType<NavAgent>().Any(agent => agent.AvoidanceAgent != null)) {
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavAgent.PropNames.ControlAvoidanceIsActive));
            }

            SerializedProperty skipCheckProperty =
                serializedObject.FindProperty(NavAgent.PropNames.CheckSkippingWithPhysicsQuery);
            SerializedProperty regressCheckProperty =
                serializedObject.FindProperty(NavAgent.PropNames.CheckBacktrackWithPhysicsQuery);

            CoreDrawers.DrawPropertyWithHelpBoxSupport(skipCheckProperty);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(regressCheckProperty);
            serializedObject.ApplyModifiedProperties();
            if (skipCheckProperty.boolValue || regressCheckProperty.boolValue) {
                using EditorGUI.IndentLevelScope indent = new(1);
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavAgent.PropNames.SkippingCheckLayerMask));
                SerializedProperty skipCheckColliderProperty =
                    serializedObject.FindProperty(NavAgent.PropNames.SkippingCheckCollider);
                CoreDrawers.DrawPropertyWithHelpBoxSupport(skipCheckColliderProperty);
                if (skipCheckColliderProperty.objectReferenceValue is MeshCollider) {
                    EditorGUILayout.HelpBox("MeshColliders are not supported for skipping checks. " +
                                            "Use a box, sphere, or capsule collider instead.",
                                            MessageType.Error);
                }
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavAgent.PropNames.SkippingCheckColliderPadding));

                if (skipCheckProperty.boolValue) {
                    CoreDrawers.DrawPropertyWithHelpBoxSupport(
                        serializedObject.FindProperty(NavAgent.PropNames.SkippingCheckStaticOnly));
                }

                if (regressCheckProperty.boolValue) {
                    CoreDrawers.DrawPropertyWithHelpBoxSupport(
                        serializedObject.FindProperty(NavAgent.PropNames.BacktrackCheckStaticOnly));
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
