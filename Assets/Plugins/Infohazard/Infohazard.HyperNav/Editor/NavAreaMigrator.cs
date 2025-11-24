// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.Core.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Infohazard.HyperNav.Editor {
    /// <summary>
    /// Handles migration of NavArea data to new versions.
    /// </summary>
    [InitializeOnLoad]
    public static class NavAreaMigrator {
        static NavAreaMigrator() {
            NavAreaBase.MigrationToInstanceSettings += NavaAreaBase_MigrationToInstanceSettings;
        }

        private static void NavaAreaBase_MigrationToInstanceSettings(NavAreaBase navArea) =>
            EditorApplication.delayCall += () => {
                SerializedObject serializedObject = new(navArea);
                SerializedProperty instanceSettingsProp =
                    serializedObject.FindProperty(NavAreaBase.PropNames.InstanceSettings);

                instanceSettingsProp.CopyFrom(serializedObject);

                SerializedProperty hasMigratedProp =
                    serializedObject.FindProperty(NavAreaBase.PropNames.IsMigratedToInstanceSettings);

                hasMigratedProp.boolValue = true;

                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(navArea))) {
                    EditorUtility.SetDirty(navArea);
                } else {
                    EditorSceneManager.MarkSceneDirty(navArea.gameObject.scene);
                }
            };
    }
}
