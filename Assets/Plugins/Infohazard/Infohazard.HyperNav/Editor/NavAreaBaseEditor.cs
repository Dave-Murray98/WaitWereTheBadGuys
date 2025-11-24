// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Infohazard.Core;
using Infohazard.Core.Editor;
using Infohazard.HyperNav.Jobs.Utility;
using Infohazard.HyperNav.Settings;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Infohazard.HyperNav.Editor {
    [CanEditMultipleObjects]
    public abstract class NavAreaBaseEditor : UnityEditor.Editor {
        private static readonly Color HandleColor = new Color(127f, 214f, 244f, 100f) / 255;
        private static readonly Color HandleColorSelected = new Color(127f, 214f, 244f, 210f) / 255;

        private static readonly Color HandleColorDisabled =
            new Color(127f * 0.75f, 214f * 0.75f, 244f * 0.75f, 100f) / 255;

        private readonly BoxBoundsHandle _boundsHandle = new();

        private static Vector3[] _previewVertices;
        private static int[][] _previewIndices;
        private static Dictionary<int, string> _previewVertexRegions;
        private static Mesh _previewMesh;
        private static readonly string[] NavLayerNames = new string[HyperNavSettings.LayerCount];
        private NavAreaBase[] _allAreas;

        private int _mouseButton;

        private SerializedObject _serializedObjectForSettings;

        protected bool EditingCollider =>
            EditMode.editMode == EditMode.SceneViewEditMode.Collider && EditMode.IsOwner(this);

        protected static bool EnableSanityChecks {
            get => EditorPrefs.GetBool("NavAreaBaseEditor.EnableSanityChecks", false);
            set => EditorPrefs.SetBool("NavAreaBaseEditor.EnableSanityChecks", value);
        }

        protected static bool VisualizeAllSurfaces {
            get => EditorPrefs.GetBool("NavAreaBaseEditor.VisualizeAllSurfaces", false);
            set => EditorPrefs.SetBool("NavAreaBaseEditor.VisualizeAllSurfaces", value);
        }

        protected static NavLayerMask VisualizeAllSurfacesMask {
            get => EditorPrefs.GetInt("NavAreaBaseEditor.VisualizeAllSurfacesMask", -1);
            set => EditorPrefs.SetInt("NavAreaBaseEditor.VisualizeAllSurfacesMask", value);
        }

        protected static bool VisualizeAllVolumes {
            get => EditorPrefs.GetBool("NavAreaBaseEditor.VisualizeAllVolumes", false);
            set => EditorPrefs.SetBool("NavAreaBaseEditor.VisualizeAllVolumes", value);
        }

        protected static NavLayerMask VisualizeAllVolumesMask {
            get => EditorPrefs.GetInt("NavAreaBaseEditor.VisualizeAllVolumesMask", -1);
            set => EditorPrefs.SetInt("NavAreaBaseEditor.VisualizeAllVolumesMask", value);
        }

        protected void DoBakeActionWithProfile(string text, Action<bool> bakeAction) {
            if (_mouseButton != 1) {
                bakeAction(false);
            } else {
                GenericMenu menu = new();
                menu.AddItem(new GUIContent(text), false, () => bakeAction(false));
                menu.AddItem(new GUIContent($"{text} with Profile"), false, () => bakeAction(true));
                menu.ShowAsContext();
            }
        }

        public override void OnInspectorGUI() {
            if (Event.current.type == EventType.MouseDown) {
                _mouseButton = Event.current.button;
            }

            serializedObject.Update();

            DrawProperties();

            EditorGUILayout.Space(NavEditorUtility.NarrowVerticalSpacing);

            DrawButtons();

            serializedObject.ApplyModifiedProperties();
        }

        protected virtual void DrawProperties() {
            serializedObject.Update();

            EditorGUILayout.LabelField("Runtime Settings", EditorStyles.boldLabel);

            DrawRuntimeProperties();

            EditorGUILayout.Space(NavEditorUtility.NarrowVerticalSpacing);
            EditorGUILayout.LabelField("Baking Settings", EditorStyles.boldLabel);

            DrawStartLocationProperties();

            SerializedProperty sharedSettingsProperty =
                serializedObject.FindProperty(NavAreaBase.PropNames.SharedSettings);
            SerializedProperty instanceSettingsProperty =
                serializedObject.FindProperty(NavAreaBase.PropNames.InstanceSettings);

            CoreDrawers.DrawPropertyWithHelpBoxSupport(sharedSettingsProperty);

            if (sharedSettingsProperty.objectReferenceValue == null) {
                if (instanceSettingsProperty != null) {
                    DrawBakingAndExternalLinkProperties(instanceSettingsProperty);
                }

                if (GUILayout.Button("Create Shared Settings")) {
                    Type type = ((NavAreaBase) target).SettingsAssetType;
                    Object newSettings = CoreEditorUtility.CreateAndSaveNewAsset(
                        target.name + type.Name, type, null, null, target);

                    sharedSettingsProperty.objectReferenceValue = newSettings;
                    sharedSettingsProperty.isExpanded = true;

                    _serializedObjectForSettings = new SerializedObject(newSettings);
                    SerializedProperty sharedSettingsDataProperty =
                        _serializedObjectForSettings.FindProperty(NavAreaBaseSettingsAsset.PropNames.Data);

                    sharedSettingsDataProperty.CopyFrom(instanceSettingsProperty);
                    _serializedObjectForSettings.ApplyModifiedProperties();
                }
            } else if (sharedSettingsProperty.isExpanded && sharedSettingsProperty.hasMultipleDifferentValues) {
                EditorGUILayout.HelpBox("Multiple different shared settings.", MessageType.Info);
            } else if (sharedSettingsProperty.isExpanded) {
                Object settingsObject = sharedSettingsProperty.objectReferenceValue;
                if (_serializedObjectForSettings == null ||
                    _serializedObjectForSettings.targetObject != settingsObject) {
                    _serializedObjectForSettings = new SerializedObject(settingsObject);
                }

                _serializedObjectForSettings.Update();

                SerializedProperty sharedSettingsDataProperty =
                    _serializedObjectForSettings.FindProperty(NavAreaBaseSettingsAsset.PropNames.Data);

                if (sharedSettingsDataProperty != null) {
                    using (new EditorGUI.IndentLevelScope()) {
                        EditorGUILayout.HelpBox(
                            "Editing ScriptableObject settings. Changes will be saved to the asset.",
                            MessageType.Info);
                        DrawBakingAndExternalLinkProperties(sharedSettingsDataProperty);
                    }
                }

                _serializedObjectForSettings.ApplyModifiedProperties();

                if (GUILayout.Button("To Instance Settings")) {
                    instanceSettingsProperty.CopyFrom(sharedSettingsDataProperty);
                    sharedSettingsProperty.objectReferenceValue = null;
                }
            }

            EditorGUILayout.Space(NavEditorUtility.NarrowVerticalSpacing);

            DrawVisualizationProperties();
        }

        protected virtual void DrawBakingAndExternalLinkProperties(SerializedProperty property) {
            DrawBakingProperties(property);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("External Links", EditorStyles.boldLabel);

            DrawExternalLinkProperties(property);
        }

        protected virtual void DrawRuntimeProperties() {
            DrawBakedDataProperties();
            DrawInstanceIDProperties();

            DrawBoundsProperties();
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                serializedObject.FindProperty(NavAreaBase.PropNames.AutoDetectMovement));
        }

        protected virtual void DrawBakedDataProperties() {
            // No editing Data directly.
            EditorGUI.BeginDisabledGroup(true);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(serializedObject.FindProperty(NavAreaBase.PropNames.Data));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                serializedObject.FindProperty(NavAreaBase.PropNames.ExternalLinks));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                serializedObject.FindProperty(NavAreaBase.PropNames.ExternalLinkRanges));
            EditorGUI.EndDisabledGroup();
        }

        protected virtual void DrawInstanceIDProperties() {
            // No editing InstanceID directly.
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(serializedObject.FindProperty(NavAreaBase.PropNames.InstanceID));
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("↻", GUILayout.Width(32))) {
                serializedObject.ApplyModifiedProperties();

                foreach (NavAreaBase area in targets.Cast<NavAreaBase>()) {
                    area.UpdateUniqueID();
                }

                serializedObject.Update();
            }

            NavAreaBase failedToCalculateUniqueID =
                targets.Cast<NavAreaBase>().FirstOrDefault(a => a.FailedToCalculateUniqueID);

            EditorGUILayout.EndHorizontal();

            if (failedToCalculateUniqueID) {
                EditorGUILayout.HelpBox(
                    $"Failed to calculate the ID for {failedToCalculateUniqueID.name}. " +
                    "If you are editing a prefab, try saving the prefab to resolve this.",
                    MessageType.Warning);
            } else {
                foreach (NavAreaBase area in targets.Cast<NavAreaBase>()) {
                    GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(area);
                    if (prefabRoot == null) {
                        PrefabStage stage = PrefabStageUtility.GetPrefabStage(area.gameObject);
                        if (stage != null) {
                            prefabRoot = stage.prefabContentsRoot;
                        }
                    }

                    if (prefabRoot != null && !prefabRoot.transform.IsPathUnique(area.transform)) {
                        EditorGUILayout.HelpBox(
                            "Determining the ID for an area in a prefab requires a unique path. " +
                            "Please ensure the area and every parent object have unique names within their respective parents.",
                            MessageType.Warning);
                        break;
                    }
                }
            }

            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                serializedObject.FindProperty(NavAreaBase.PropNames.RandomInstanceID));
        }

        protected virtual void DrawBoundsProperties() {
            // Bounds field and button to edit in the scene view (like a box collider).
            SerializedProperty boundsProperty = serializedObject.FindProperty(NavAreaBase.PropNames.Bounds);
            EditMode.DoEditModeInspectorModeButton(EditMode.SceneViewEditMode.Collider, "Edit Bounds",
                                                   EditorGUIUtility.IconContent("EditCollider"),
                                                   () => boundsProperty.boundsValue, this);

            CoreDrawers.DrawPropertyWithHelpBoxSupport(boundsProperty);
        }

        protected virtual void DrawStartLocationProperties() {
            SerializedProperty useStartLocationsProp =
                serializedObject.FindProperty(NavAreaBase.PropNames.UseStartLocations);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(useStartLocationsProp);

            // Draw StartLocations only if UseStartLocations is true.
            if (useStartLocationsProp.boolValue) {
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavAreaBase.PropNames.StartLocations));
            }
        }

        protected virtual void DrawBakingProperties(SerializedProperty property) {
            DrawPhysicsQueryProperties(property);
        }

        protected virtual void DrawPhysicsQueryProperties(SerializedProperty property) {
            DrawLayerMaskProperties(property);
            DrawAgentSizeProperties(property);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                property.FindPropertyRelative(NavAreaBaseSettings.PropNames.VoxelSize));
        }

        protected virtual void DrawExternalLinkProperties(SerializedProperty property) {
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                property.FindPropertyRelative(NavAreaBaseSettings.PropNames.MaxExternalLinkDistanceToVolume));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                property.FindPropertyRelative(NavAreaBaseSettings.PropNames.MaxExternalLinkDistanceToSurface));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                property.FindPropertyRelative(NavAreaBaseSettings.PropNames.ExternalLinkTargetLayers));
        }

        protected virtual void DrawAgentSizeProperties(SerializedProperty property) {
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                property.FindPropertyRelative(NavAreaBaseSettings.PropNames.Layer));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                property.FindPropertyRelative(NavAreaBaseSettings.PropNames.MaxAgentRadius));
        }

        protected virtual void DrawLayerMaskProperties(SerializedProperty property) {
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                property.FindPropertyRelative(NavAreaBaseSettings.PropNames.BlockingLayers));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                property.FindPropertyRelative(NavAreaBaseSettings.PropNames.StaticOnly));
        }

        protected virtual void DrawVisualizationProperties() { }

        protected virtual void DrawVisualizeAllProperties() {
            HyperNavSettings settings = HyperNavSettings.Instance;
            if (settings) {
                for (int i = 0; i < NavLayerNames.Length; i++) {
                    NavLayerNames[i] = settings.GetLayerName(i);
                }
            }

            bool needsRepaint = false;

            bool allSurfaces = VisualizeAllSurfaces;
            EditorGUI.BeginChangeCheck();
            allSurfaces = EditorGUILayout.Toggle("Visualize All Surfaces", allSurfaces);
            if (EditorGUI.EndChangeCheck()) {
                VisualizeAllSurfaces = allSurfaces;
                needsRepaint = true;
            }

            if (allSurfaces) {
                EditorGUI.indentLevel++;
                int surfaceMask = VisualizeAllSurfacesMask;
                EditorGUI.BeginChangeCheck();
                surfaceMask = EditorGUILayout.MaskField(new GUIContent("Layers"), surfaceMask, NavLayerNames);
                if (EditorGUI.EndChangeCheck()) {
                    VisualizeAllSurfacesMask = surfaceMask;
                    needsRepaint = true;
                }

                EditorGUI.indentLevel--;
            }

            bool allVolumes = VisualizeAllVolumes;
            EditorGUI.BeginChangeCheck();
            allVolumes = EditorGUILayout.Toggle("Visualize All Volumes", allVolumes);
            if (EditorGUI.EndChangeCheck()) {
                VisualizeAllVolumes = allVolumes;
                needsRepaint = true;
            }

            if (allVolumes) {
                EditorGUI.indentLevel++;
                int volumeMask = VisualizeAllVolumesMask;
                EditorGUI.BeginChangeCheck();
                volumeMask = EditorGUILayout.MaskField(new GUIContent("Layers"), volumeMask, NavLayerNames);
                if (EditorGUI.EndChangeCheck()) {
                    VisualizeAllVolumesMask = volumeMask;
                    needsRepaint = true;
                }

                EditorGUI.indentLevel--;
            }

            if (needsRepaint) {
                EditorApplication.delayCall += SceneView.RepaintAll;
            }
        }

        public virtual void DrawButtons() {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            bool sanityChecksEnabled = EnableSanityChecks;
            EditorGUI.BeginChangeCheck();
            sanityChecksEnabled = EditorGUILayout.Toggle("Enable Sanity Checks", sanityChecksEnabled);
            if (EditorGUI.EndChangeCheck()) {
                EnableSanityChecks = sanityChecksEnabled;
            }

            EditorGUILayout.BeginHorizontal();

            // Draw progress bar if baking or bake button otherwise.
            bool isAnyBaking = false;
            foreach (NavAreaBase area in targets.Cast<NavAreaBase>()) {
                bool isBaking = EditorNavAreaBakingUtility.TryGetBakeProgress(area, out NavAreaBakeProgress value);

                if (isBaking) {
                    // ProgressBar is not available in layout drawing mode,
                    // so draw an empty space then use that rect to draw the progress bar.
                    EditorGUILayout.Space(EditorGUIUtility.singleLineHeight, true);
                    Rect rect = GUILayoutUtility.GetLastRect();
                    EditorGUI.ProgressBar(rect, value.Progress,
                                          $"{value.Operation}: {Mathf.RoundToInt(value.Progress * 100)}%");

                    isAnyBaking = true;
                }
            }

            if (!isAnyBaking) {
                if (GUILayout.Button("Bake", GUILayout.ExpandWidth(true))) {
                    serializedObject.ApplyModifiedProperties();
                    DoBakeActionWithProfile("Bake", profile => {
                        foreach (NavAreaBase area in targets.Cast<NavAreaBase>()) {
                            area.UpdateUniqueID();
                            EditorNavAreaBakingUtility.GetOrCreateData(area);
                            EditorNavAreaBakingUtility.BakeDataAsync(area, sanityChecksEnabled, profile).Forget();
                        }

                        serializedObject.Update();
                    });
                }
            }

            // Draw cancel button if baking or clear button otherwise.
            if (isAnyBaking) {
                if (GUILayout.Button("Cancel", GUILayout.Width(75))) {
                    foreach (NavAreaBase area in targets.Cast<NavAreaBase>()) {
                        EditorNavAreaBakingUtility.CancelBake(area);
                    }
                }
            } else {
                if (GUILayout.Button("Clear", GUILayout.Width(75))) {
                    foreach (NavAreaBase area in targets.Cast<NavAreaBase>()) {
                        area.UpdateUniqueID();
                    }

                    serializedObject.Update();

                    foreach (NavAreaBase area in targets.Cast<NavAreaBase>()) {
                        EditorNavAreaBakingUtility.ClearData(area);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(EditorNavAreaBakingUtility.BakeHandlers.Count > 0);

            UniTask? previousTask = null;
            if (GUILayout.Button("Bake All", GUILayout.ExpandWidth(true))) {
                serializedObject.ApplyModifiedProperties();
                foreach (NavAreaBase otherArea in _allAreas) {
                    if (EditorNavAreaBakingUtility.TryGetBakeProgress(otherArea, out NavAreaBakeProgress _)) continue;
                    otherArea.UpdateUniqueID();
                    EditorNavAreaBakingUtility.GetOrCreateData(otherArea);
                    UniTaskCompletionSource completionSource =
                        EditorNavAreaBakingUtility.BakeDataWithCompletionSource(
                            otherArea, sanityChecksEnabled, false, previousTask);

                    completionSource.Task.Forget();
                    previousTask = completionSource.Task;
                }

                serializedObject.Update();
            }

            if (GUILayout.Button("Clear All", GUILayout.Width(75))) {
                serializedObject.ApplyModifiedProperties();
                foreach (NavAreaBase otherArea in _allAreas) {
                    otherArea.UpdateUniqueID();
                    EditorNavAreaBakingUtility.ClearData(otherArea);
                }

                serializedObject.Update();
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();


            if (EditorNavAreaBakingUtility.BakeHandlers.Count > 0) {
                foreach (NavAreaBase otherArea in _allAreas) {
                    if (!EditorNavAreaBakingUtility.TryGetBakeProgress(otherArea,
                                                                       out NavAreaBakeProgress progress)) continue;

                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.Space(EditorGUIUtility.singleLineHeight, true);
                    Rect rect = GUILayoutUtility.GetLastRect();
                    EditorGUI.ProgressBar(rect, progress.Progress,
                                          $"{progress.Operation}: {Mathf.RoundToInt(progress.Progress * 100)}%");

                    if (GUILayout.Button("Cancel", GUILayout.Width(75))) {
                        EditorNavAreaBakingUtility.CancelBake(otherArea);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.BeginHorizontal();

            bool anyBaked = targets.Cast<INavArea>().Any(a => (Object) a.Data != null && a.Data.IsBaked);
            EditorGUI.BeginDisabledGroup(!anyBaked);

            if (GUILayout.Button("Generate External Links")) {
                ExternalLinkEditorUtility.GenerateExternalLinks(targets.Cast<NavAreaBase>().ToList());
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Clear", GUILayout.Width(75))) {
                foreach (NavAreaBase area in targets.Cast<NavAreaBase>()) {
                    ExternalLinkEditorUtility.ClearExternalLinks(area);
                }

                SceneView.RepaintAll();
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate All External Links")) {
                ExternalLinkEditorUtility.GenerateAllExternalLinks();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Clear All", GUILayout.Width(75))) {
                foreach (NavAreaBase otherArea in _allAreas) {
                    ExternalLinkEditorUtility.ClearExternalLinks(otherArea);
                }

                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            if (targets.Length == 1) {
                NavAreaBase areaBase = (NavAreaBase) target;
                EditorGUI.BeginDisabledGroup(areaBase.PreviewMesh == null);
                if (GUILayout.Button("Export Preview")) {
                    NavEditorUtility.ExportPreviewMesh(areaBase.PreviewMesh);
                }

                EditorGUI.EndDisabledGroup();
            }
        }

        private void OnEnable() {
            // Camera.onPreCull used to render visualization mesh in built-in RP.
            Camera.onPreCull -= Camera_OnPreCull;
            Camera.onPreCull += Camera_OnPreCull;

            // RenderPipelineManager.beginCameraRendering used to render visualization mesh in SRPs.
            RenderPipelineManager.beginCameraRendering -= RenderPipelineManager_BeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += RenderPipelineManager_BeginCameraRendering;

            // EditorNavAreaBakingUtility.BakeProgressUpdated used to repaint the progress bar.
            EditorNavAreaBakingUtility.BakeProgressUpdated -= NavAreaUtil_BakeProgressUpdated;
            EditorNavAreaBakingUtility.BakeProgressUpdated += NavAreaUtil_BakeProgressUpdated;

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();

            // In prefab editing mode we don't want to render all areas in the scene.
            // Rather we search the prefab stage hierarchy.
            if (stage == null) {
                _allAreas = FindObjectsOfType<NavAreaBase>();
            } else {
                _allAreas = stage.prefabContentsRoot.GetComponentsInChildren<NavAreaBase>();
            }
        }

        private void OnDisable() {
            Camera.onPreCull -= Camera_OnPreCull;
            RenderPipelineManager.beginCameraRendering -= RenderPipelineManager_BeginCameraRendering;
            EditorNavAreaBakingUtility.BakeProgressUpdated -= NavAreaUtil_BakeProgressUpdated;
        }

        private void RenderPipelineManager_BeginCameraRendering(ScriptableRenderContext ctx, Camera camera) {
            CameraWillRender(camera);
        }

        private void Camera_OnPreCull(Camera camera) {
            CameraWillRender(camera);
        }

        private void NavAreaUtil_BakeProgressUpdated(NavAreaBase area) {
            Repaint();
        }

        private void CameraWillRender(Camera camera) {
            if (!camera || camera.gameObject.scene.isLoaded) return;

            bool visualizeAllSurfaces = VisualizeAllSurfaces;
            NavLayerMask surfaceMask = VisualizeAllSurfacesMask;
            bool visualizeAllVolumes = VisualizeAllVolumes;
            NavLayerMask volumeMask = VisualizeAllVolumesMask;

            if (visualizeAllSurfaces || visualizeAllVolumes) {
                foreach (NavAreaBase area in _allAreas) {
                    bool visualizeThis = Array.IndexOf(targets, area) >= 0 || area switch {
                        NavSurface => visualizeAllSurfaces && surfaceMask.Contains(area.Layer),
                        NavVolume => visualizeAllVolumes && volumeMask.Contains(area.Layer),
                        _ => false,
                    };

                    if (visualizeThis)
                        RenderVisualization(area, camera);
                }
            } else {
                foreach (NavAreaBase area in targets.Cast<NavAreaBase>()) {
                    RenderVisualization(area, camera);
                }
            }
        }

        private void RenderVisualization(NavAreaBase area, Camera camera) {
            if (!area.isActiveAndEnabled) return;

            // If area is baked and mode is Blocking or Final, generate the mesh.
            // All other modes must be generated while baking.
            INavArea navArea = area;
            bool hasData = area.IsNativeDataCreated ||
                           ((Object) navArea.Data != null && navArea.Data.IsBaked);

            if (hasData && area.PreviewMesh == null) {
                area.Register();
                NavAreaPreviewUtility.RebuildPreviewMesh(area);
            }

            // Draw the mesh.
            Mesh mesh = area.PreviewMesh;
            Material[] mats = area.PreviewMaterials;
            Matrix4x4 matrix = area.transform.localToWorldMatrix;
            if (mesh != null && mats != null) {
                for (int i = 0; i < mesh.subMeshCount && i < mats.Length; i++) {
                    //if (i != 0 && i != mesh.subMeshCount / 2) continue;
                    Graphics.DrawMesh(mesh, matrix, mats[i], 0, camera, i, null, ShadowCastingMode.Off, false);
                }
            }
        }

        public void OnSceneGUI() {
            NavAreaBase area = (NavAreaBase) target;
            // Draw collider editing handle.
            if (EditingCollider) {
                Bounds bounds = area.Bounds;
                Color color = area.enabled ? HandleColor : HandleColorDisabled;
                Matrix4x4 localToWorld =
                    Matrix4x4.TRS(area.transform.position, area.transform.rotation, Vector3.one);
                using (new Handles.DrawingScope(color, localToWorld)) {
                    _boundsHandle.center = bounds.center;
                    _boundsHandle.size = bounds.size;

                    EditorGUI.BeginChangeCheck();
                    _boundsHandle.DrawHandle();
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(area, "Modify NavArea Bounds");
                        Vector3 center = _boundsHandle.center;
                        Vector3 size = _boundsHandle.size;
                        Bounds newBounds = new(center, size);
                        area.Bounds = newBounds;
                        EditorUtility.SetDirty(target);

                        foreach (NavAreaBase other in area.GetComponents<NavAreaBase>()) {
                            if (other == area || other.Bounds != bounds) continue;
                            Undo.RecordObject(other, "Modify NavArea Bounds");
                            other.Bounds = newBounds;
                        }
                    }
                }
            }

            // Draw handles for each start location.
            if (area.UseStartLocations) {
                float voxelSize = area.BaseSettings.VoxelSize;
                for (int i = 0; i < area.StartLocations.Count; i++) {
                    Vector3 worldPos = area.transform.TransformPoint(area.StartLocations[i]);
                    Vector3 movedPos = Handles.PositionHandle(worldPos, area.transform.rotation);
                    if (movedPos != worldPos) {
                        movedPos.x = Mathf.Round(movedPos.x / voxelSize) * voxelSize;
                        movedPos.y = Mathf.Round(movedPos.y / voxelSize) * voxelSize;
                        movedPos.z = Mathf.Round(movedPos.z / voxelSize) * voxelSize;

                        Undo.RecordObject(area, "Modify Start Location");
                        area.SetStartLocation(i, area.transform.InverseTransformPoint(movedPos));
                    }
                }
            }

            // Draw vertex numbers.
            if (area.ShowVertexNumbers) {
                DrawVertexNumbers(area);
            }
        }

        // Cache info on vertex indices to make drawing them faster.
        private static void CacheVertexNumbers(NavAreaBase area) {
            _previewVertices = area.PreviewMesh.vertices;
            _previewVertexRegions = new Dictionary<int, string>();
            _previewMesh = area.PreviewMesh;

            _previewIndices = new int[area.PreviewMesh.subMeshCount][];
            for (int i = 0; i < area.PreviewMesh.subMeshCount; i++) {
                _previewIndices[i] = area.PreviewMesh.GetIndices(i);
            }

            Dictionary<int, HashSet<int>> vertexRegionSets = new Dictionary<int, HashSet<int>>();

            for (int i = 0; i < area.PreviewMesh.subMeshCount; i++) {
                int[] indices = area.PreviewMesh.GetIndices(i);
                for (int j = 0; j < indices.Length; j++) {
                    int index = indices[j];
                    if (!vertexRegionSets.TryGetValue(index, out HashSet<int> regions)) {
                        regions = new HashSet<int>();
                        vertexRegionSets[index] = regions;
                    }

                    regions.Add(i);
                    _previewVertexRegions[index] = $"{index}: [{string.Join(", ", regions)}]";
                }
            }
        }

        // Draw cached vertex numbers.
        private static void DrawVertexNumbers(NavAreaBase area) {
            Camera cam = Camera.current;

            Mesh mesh = area.PreviewMesh;
            if (mesh == null || cam == null) return;

            if (_previewVertices == null || _previewVertexRegions == null ||
                _previewMesh != area.PreviewMesh) {
                CacheVertexNumbers(area);
            }

            float range2 = area.ShowVertexNumbersRange * area.ShowVertexNumbersRange;
            if (mesh != null && cam != null) {
                foreach (var pair in _previewVertexRegions) {
                    Vector3 v = area.transform.TransformPoint(_previewVertices[pair.Key]);
                    if (Vector3.SqrMagnitude(v - cam.transform.position) < range2) {
                        Handles.Label(v, pair.Value);
                    }
                }

                for (int i = 0; i < mesh.subMeshCount; i++) {
                    if (mesh.GetTopology(i) != MeshTopology.Triangles) continue;

                    int[] indices = _previewIndices[i];
                    for (int j = 0; j < indices.Length; j += 3) {
                        Vector3 v0 = area.transform.TransformPoint(_previewVertices[indices[j]]);
                        Vector3 v1 = area.transform.TransformPoint(_previewVertices[indices[j + 1]]);
                        Vector3 v2 = area.transform.TransformPoint(_previewVertices[indices[j + 2]]);

                        float triArea = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;

                        Vector3 center = (v0 + v1 + v2) / 3;
                        if (Vector3.SqrMagnitude(center - cam.transform.position) < range2) {
                            Handles.Label(center, $"{j}: {i} - area: {triArea}");
                        }
                    }
                }
            }
        }


        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.Pickable)]
        private static void RenderBoxGizmoSelected(NavAreaBase area, GizmoType gizmoType) {
            // Draw the bounds editor gizmo.
            RenderBoxGizmo(area, gizmoType, true);
        }

        [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Pickable)]
        private static void RenderBoxGizmoNotSelected(NavAreaBase area, GizmoType gizmoType) {
            RenderBoxGizmo(area, gizmoType, false);
        }

        // Draw the bounds editor gizmo.
        private static void RenderBoxGizmo(NavAreaBase area, GizmoType gizmoType, bool selected) {
            Color color = selected ? HandleColorSelected : HandleColor;
            if (!area.enabled)
                color = HandleColorDisabled;

            Color oldColor = Gizmos.color;
            Matrix4x4 oldMatrix = Gizmos.matrix;

            // Use the unscaled matrix for the NavMeshSurface
            Matrix4x4 localToWorld = Matrix4x4.TRS(area.transform.position, area.transform.rotation, Vector3.one);
            Gizmos.matrix = localToWorld;

            Bounds bounds = area.Bounds;

            // Draw wireframe bounds.
            Gizmos.color = color;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            // If selected, draw filled bounds.
            if (selected && area.enabled) {
                Color colorTrans = new Color(color.r * 0.75f, color.g * 0.75f, color.b * 0.75f, color.a * 0.15f);
                Gizmos.color = colorTrans;
                Gizmos.DrawCube(bounds.center, bounds.size);
            }

            Gizmos.matrix = oldMatrix;
            Gizmos.color = oldColor;
        }

        protected static void DrawExternalLinkVisualization<TArea, TData, TNativeData, TPointers, TSettings>(
            NavArea<TArea, TData, TNativeData, TPointers, TSettings> area)
            where TArea : NavArea<TArea, TData, TNativeData, TPointers, TSettings>
            where TData : ScriptableObject, INavAreaData<TNativeData, TPointers>
            where TNativeData : unmanaged, IDisposable, INativeNavAreaData
            where TPointers : struct, IDisposable, INativeNavAreaDataPointers
            where TSettings : NavAreaBaseSettings<TSettings>, new() {
            if (!area.VisualizeExternalLinks) return;

            TNativeData nativeData = area.NativeData;
            if (nativeData.ExternalLinkCount == 0) return;
            Color color = Gizmos.color;
            Color color2 = Handles.color;
            Gizmos.color = Color.green;
            Handles.color = Color.green;

            for (int i = 0; i < nativeData.RegionCount; i++) {
                SerializableRange linkRange = nativeData.GetExternalLinkRange(i);
                NativeBounds regionBounds = nativeData.GetRegionBounds(i);
                for (int j = linkRange.Start; j < linkRange.End; j++) {
                    NativeNavExternalLinkData link = nativeData.GetExternalLinkData(j);
                    Vector3 linkPos = area.transform.TransformPoint(link.FromPosition.xyz);
                    Vector3 toPos = area.transform.TransformPoint(link.ToPosition.xyz);

                    Vector3 vector;
                    if (Vector3.SqrMagnitude(link.FromPosition.xyz - link.ToPosition.xyz) < 0.0001f) {
                        Vector3 regionCenter = area.transform.TransformPoint(regionBounds.Center.xyz);
                        float3 pointInRegion = linkPos - regionCenter;
                        pointInRegion /= regionBounds.Extents.xyz;
                        float absX = Mathf.Abs(pointInRegion.x);
                        float absY = Mathf.Abs(pointInRegion.y);
                        float absZ = Mathf.Abs(pointInRegion.z);

                        if (absX > absY && absX > absZ) {
                            vector = new Vector3(Mathf.Sign(pointInRegion.x), 0, 0);
                        } else if (absY > absX && absY > absZ) {
                            vector = new Vector3(0, Mathf.Sign(pointInRegion.y), 0);
                        } else {
                            vector = new Vector3(0, 0, Mathf.Sign(pointInRegion.z));
                        }

                        vector = area.transform.TransformDirection(vector);
                    } else {
                        vector = toPos - linkPos;
                        Handles.DrawLine(linkPos, toPos);
                    }

                    Handles.ConeHandleCap(0, toPos, Quaternion.LookRotation(vector), 0.25f, EventType.Repaint);
                }
            }

            Gizmos.color = color;
            Handles.color = color2;
        }
    }
}
