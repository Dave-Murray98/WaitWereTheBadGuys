// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.Core.Editor;
using UnityEditor;

namespace Infohazard.HyperNav.Editor {
    [CustomEditor(typeof(AvoidanceManager))]
    public class AvoidanceManagerEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();

            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                serializedObject.FindProperty(AvoidanceManager.PropNames.UpdateMode));

            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                serializedObject.FindProperty(AvoidanceManager.PropNames.TimeHorizon));

            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                serializedObject.FindProperty(AvoidanceManager.PropNames.MaxObstaclesConsidered));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
