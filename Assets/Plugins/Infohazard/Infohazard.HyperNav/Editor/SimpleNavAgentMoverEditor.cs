// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.Core.Editor;
using UnityEditor;

namespace Infohazard.HyperNav.Editor {
    [CanEditMultipleObjects]
    [CustomEditor(typeof(SimpleNavAgentMover), true)]
    public class SimpleNavAgentMoverEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();

            EditorGUILayout.LabelField("Component References", EditorStyles.boldLabel);

            SerializedProperty navAgentProperty =
                serializedObject.FindProperty(SimpleNavAgentMover.PropNames.NavAgent);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(navAgentProperty);

            if (navAgentProperty.objectReferenceValue == null) {
                EditorGUILayout.HelpBox("NavAgent reference is required.", MessageType.Error);
            }

            SerializedProperty movementModeProperty =
                serializedObject.FindProperty(SimpleNavAgentMover.PropNames.MovementMode);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(movementModeProperty);
            SimpleNavAgentMover.MovementModeType mode =
                (SimpleNavAgentMover.MovementModeType) movementModeProperty.intValue;

            if (mode == SimpleNavAgentMover.MovementModeType.Rigidbody) {
                SerializedProperty rigidbodyProperty =
                    serializedObject.FindProperty(SimpleNavAgentMover.PropNames.Rigidbody);
                CoreDrawers.DrawPropertyWithHelpBoxSupport(rigidbodyProperty);
                if (rigidbodyProperty.objectReferenceValue == null) {
                    EditorGUILayout.HelpBox("Rigidbody reference is required for Rigidbody movement mode.", MessageType.Error);
                }
            }

            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                serializedObject.FindProperty(SimpleNavAgentMover.PropNames.DestinationTransform));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Pathfinding Settings", EditorStyles.boldLabel);

            CoreDrawers.DrawPropertyWithHelpBoxSupport(serializedObject.FindProperty(SimpleNavAgentMover.PropNames.SampleRadius));

            SerializedProperty enableRepathingProperty =
                serializedObject.FindProperty(SimpleNavAgentMover.PropNames.EnableRepathing);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(enableRepathingProperty);
            if (enableRepathingProperty.boolValue) {
                using EditorGUI.IndentLevelScope indent = new(1);
                SerializedProperty repathAtIntervalProperty =
                    serializedObject.FindProperty(SimpleNavAgentMover.PropNames.RepathAtInterval);
                CoreDrawers.DrawPropertyWithHelpBoxSupport(repathAtIntervalProperty);
                if (repathAtIntervalProperty.boolValue) {
                    using EditorGUI.IndentLevelScope indent2 = new(1);
                    CoreDrawers.DrawPropertyWithHelpBoxSupport(
                        serializedObject.FindProperty(SimpleNavAgentMover.PropNames.RepathInterval));
                }

                SerializedProperty repathOnDestinationTransformMoveProperty =
                    serializedObject.FindProperty(SimpleNavAgentMover.PropNames.RepathOnDestinationTransformMove);
                CoreDrawers.DrawPropertyWithHelpBoxSupport(repathOnDestinationTransformMoveProperty);
                if (repathOnDestinationTransformMoveProperty.boolValue) {
                    using EditorGUI.IndentLevelScope indent2 = new(1);
                    CoreDrawers.DrawPropertyWithHelpBoxSupport(
                        serializedObject.FindProperty(SimpleNavAgentMover.PropNames.RepathDistanceThreshold));
                }

                SerializedProperty repathOnReachEndProperty =
                    serializedObject.FindProperty(SimpleNavAgentMover.PropNames.RepathOnReachEnd);
                CoreDrawers.DrawPropertyWithHelpBoxSupport(repathOnReachEndProperty);
                if (repathOnReachEndProperty.boolValue) {
                    using EditorGUI.IndentLevelScope indent2 = new(1);
                    CoreDrawers.DrawPropertyWithHelpBoxSupport(
                        serializedObject.FindProperty(SimpleNavAgentMover.PropNames.RepathOnReachEndDistance));
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Movement Settings", EditorStyles.boldLabel);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(serializedObject.FindProperty(SimpleNavAgentMover.PropNames.MaxSpeedInVolume));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(serializedObject.FindProperty(SimpleNavAgentMover.PropNames.AccelerationInVolume));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(serializedObject.FindProperty(SimpleNavAgentMover.PropNames.MaxSpeedOnSurface));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(serializedObject.FindProperty(SimpleNavAgentMover.PropNames.AccelerationOnSurface));

            SerializedProperty rotateModeProperty =
                serializedObject.FindProperty(SimpleNavAgentMover.PropNames.RotateMode);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(rotateModeProperty);

            SimpleNavAgentMover.RotateModeType rotateMode =
                (SimpleNavAgentMover.RotateModeType) rotateModeProperty.enumValueIndex;

            if (rotateMode is not SimpleNavAgentMover.RotateModeType.None) {
                using EditorGUI.IndentLevelScope indent = new(1);
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(SimpleNavAgentMover.PropNames.RotationSpeed));
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(SimpleNavAgentMover.PropNames.RotateBySlerp));
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(SimpleNavAgentMover.PropNames.DistanceToRotateToSurfaceUp));
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(SimpleNavAgentMover.PropNames.KeepAlignedToGroundNormal));
            }

            SerializedProperty keepOnGroundProperty =
                serializedObject.FindProperty(SimpleNavAgentMover.PropNames.KeepOnGround);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(keepOnGroundProperty);

            if (keepOnGroundProperty.boolValue) {
                using EditorGUI.IndentLevelScope indent = new(1);
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(SimpleNavAgentMover.PropNames.GroundCheckLayerMask));
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(SimpleNavAgentMover.PropNames.GroundCheckQueryRadius));
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(SimpleNavAgentMover.PropNames.GroundCheckDistance));
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(SimpleNavAgentMover.PropNames.DesiredOffsetToGround));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
