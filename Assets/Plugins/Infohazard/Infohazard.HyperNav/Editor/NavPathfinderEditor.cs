// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav;
using UnityEditor;
using UnityEngine;

namespace Infohazard.HyperNav.Editor {
    [CustomEditor(typeof(NavPathfinder))]
    public class NavPathfinderEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();

            EditorGUILayout.LabelField("Instance Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(NavPathfinder.PropNames.IsMainInstance));
            SerializedProperty modeProp = serializedObject.FindProperty(NavPathfinder.PropNames.Mode);
            EditorGUILayout.PropertyField(modeProp);
            NavPathfindingMode mode = (NavPathfindingMode)modeProp.intValue;

            // Draw help box explaining the mode.
            if (mode == NavPathfindingMode.MainThreadEndOfFrame) {
                EditorGUILayout.HelpBox(
                    "Pathfinding will be completed and the provided callback invoked in the next LateUpdate call.",
                    MessageType.Info);
            } else if (mode == NavPathfindingMode.Instantaneous) {
                EditorGUILayout.HelpBox(
                    "Pathfinding will be completed immediately, blocking the main thread until it is finished.",
                    MessageType.Info);
            } else {
                EditorGUILayout.HelpBox(
                    "Pathfinding will be completed on another thread using Unity's C# Job System.",
                    MessageType.Info);
            }

            if (mode == NavPathfindingMode.JobThread) {
                EditorGUILayout.Space(NavEditorUtility.NarrowVerticalSpacing);
                EditorGUILayout.LabelField("Algorithm Settings", EditorStyles.boldLabel);

                SerializedProperty concurrentProp =
                    serializedObject.FindProperty(NavPathfinder.PropNames.MaxConcurrentJobs);
                EditorGUILayout.PropertyField(concurrentProp);
                concurrentProp.intValue = Mathf.Max(1, concurrentProp.intValue);

                SerializedProperty framesProp = serializedObject.FindProperty(NavPathfinder.PropNames.MaxCompletionFrames);
                EditorGUILayout.PropertyField(framesProp);
                framesProp.intValue = Mathf.Max(1, framesProp.intValue);
                if (framesProp.intValue > 3) {
                    EditorGUILayout.HelpBox(
                        "A value greater than 3 frames will reduce memory performance due to the internals of Unity's Job System.",
                        MessageType.Warning);
                }
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty(NavPathfinder.PropNames.PathTighteningIterations));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
