// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System.Linq;
using Infohazard.Core.Editor;
using Infohazard.HyperNav.Jobs.Utility;
using Infohazard.HyperNav.Settings;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Infohazard.HyperNav.Editor {
    [CustomEditor(typeof(NavSurface))]
    [CanEditMultipleObjects]
    public class NavSurfaceEditor : NavAreaBaseEditor {
        private static bool ShowAdvancedSettings {
            get => EditorPrefs.GetBool("NavSurfaceEditor.ShowAdvancedSettings", false);
            set => EditorPrefs.SetBool("NavSurfaceEditor.ShowAdvancedSettings", value);
        }

        protected override void DrawLayerMaskProperties(SerializedProperty property) {
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                property.FindPropertyRelative(NavSurfaceSettings.PropNames.WalkableLayers));
            base.DrawLayerMaskProperties(property);
        }

        protected override void DrawAgentSizeProperties(SerializedProperty property) {
            base.DrawAgentSizeProperties(property);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                property.FindPropertyRelative(NavSurfaceSettings.PropNames.MaxAgentHeight));
            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                property.FindPropertyRelative(NavSurfaceSettings.PropNames.ErosionDistance));

            SerializedProperty uprightDirectionMode =
                property.FindPropertyRelative(NavSurfaceSettings.PropNames.UprightDirectionMode);
            CoreDrawers.DrawPropertyWithHelpBoxSupport(uprightDirectionMode);

            NavSurfaceUprightDirectionMode mode = (NavSurfaceUprightDirectionMode) uprightDirectionMode.intValue;
            EditorGUI.indentLevel++;
            if (mode is NavSurfaceUprightDirectionMode.FixedWorldDirection
                or NavSurfaceUprightDirectionMode.FixedLocalDirection) {
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    property.FindPropertyRelative(NavSurfaceSettings.PropNames.FixedUprightDirection));
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    property.FindPropertyRelative(NavSurfaceSettings.PropNames.SlopeAngleLimit));
            } else if (mode == NavSurfaceUprightDirectionMode.Custom) {
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    property.FindPropertyRelative(NavSurfaceSettings.PropNames.CustomUprightDirectionHandler));
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    property.FindPropertyRelative(NavSurfaceSettings.PropNames.SlopeAngleLimit));
            }

            EditorGUI.indentLevel--;

            if (ShowAdvancedSettings) {
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    property.FindPropertyRelative(
                        NavSurfaceSettings.PropNames.MaxAngleBetweenUpDirectionsWithinTriangle));

                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    property.FindPropertyRelative(
                        NavSurfaceSettings.PropNames.MaxAngleBetweenUpDirectionsBetweenTriangles));
            }

            CoreDrawers.DrawPropertyWithHelpBoxSupport(
                property.FindPropertyRelative(NavSurfaceSettings.PropNames.MaxTriangleDivisions));
        }

        protected override void DrawBakingProperties(SerializedProperty property) {
            base.DrawBakingProperties(property);

            if (ShowAdvancedSettings) {
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    property.FindPropertyRelative(NavSurfaceSettings.PropNames.MinIslandSurfaceArea));
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    property.FindPropertyRelative(NavSurfaceSettings.PropNames.DecimationThreshold));
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    property.FindPropertyRelative(NavSurfaceSettings.PropNames.BoundaryTriangleClippingThreshold));
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    property.FindPropertyRelative(NavSurfaceSettings.PropNames.MinTriangleAreaFraction));
            }
        }

        protected override void DrawExternalLinkProperties(SerializedProperty property) {
            base.DrawExternalLinkProperties(property);

            if (ShowAdvancedSettings) {
                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    property.FindPropertyRelative(NavSurfaceSettings.PropNames.MaxSurfaceVolumeApproachAngle));
            }

            if (ShowAdvancedSettings && GUILayout.Button("Hide Advanced Settings")) {
                ShowAdvancedSettings = false;
            } else if (!ShowAdvancedSettings && GUILayout.Button("Show Advanced Settings")) {
                ShowAdvancedSettings = true;
            }
        }

        protected override void DrawVisualizationProperties() {
            SerializedProperty visProp = serializedObject.FindProperty(NavSurface.PropNames.VisualizationMode);

            // Allow visualization settings to be hidden to reduce clutter.
            // Can use isExpanded on the property even though it is not normally expandable.
            visProp.isExpanded =
                EditorGUILayout.Foldout(visProp.isExpanded, "Visualization Settings", EditorStyles.foldoutHeader);

            if (visProp.isExpanded) {
                EditorGUI.indentLevel++;
                DrawVisualizeAllProperties();

                EditorGUI.BeginChangeCheck();
                CoreDrawers.DrawPropertyWithHelpBoxSupport(visProp);
                NavSurfaceVisualizationMode visMode = (NavSurfaceVisualizationMode) visProp.intValue;

                if (visMode is >= NavSurfaceVisualizationMode.ShrinkwrappedMesh
                    and < NavSurfaceVisualizationMode.SplitVertexGroups) {

                    CoreDrawers.DrawPropertyWithHelpBoxSupport(
                        serializedObject.FindProperty(NavSurface.PropNames.VisualizedFilterIteration));
                }

                // When changing visualization mode, delete current visualization mesh to force regen.
                if (EditorGUI.EndChangeCheck()) {
                    foreach (NavSurface surface in targets.Cast<NavSurface>()) {
                        surface.PreviewMesh = null;
                    }
                }

                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavSurface.PropNames.VisualizeNormals));

                SerializedProperty showVertsProp =
                    serializedObject.FindProperty(NavAreaBase.PropNames.ShowVertexNumbers);
                CoreDrawers.DrawPropertyWithHelpBoxSupport(showVertsProp);

                // Draw ShowVertexNumbersRange only if ShowVertexNumbers is true.
                if (showVertsProp.boolValue) {
                    EditorGUI.indentLevel++;
                    SerializedProperty showVertsRangeProp =
                        serializedObject.FindProperty(NavAreaBase.PropNames.ShowVertexNumbersRange);
                    CoreDrawers.DrawPropertyWithHelpBoxSupport(showVertsRangeProp, new GUIContent("Range"));
                    showVertsRangeProp.floatValue = Mathf.Max(0, showVertsRangeProp.floatValue);
                    EditorGUI.indentLevel--;
                }

                SerializedProperty visualizeNeighborsProp =
                    serializedObject.FindProperty(NavAreaBase.PropNames.VisualizeNeighbors);

                CoreDrawers.DrawPropertyWithHelpBoxSupport(visualizeNeighborsProp);
                if (visualizeNeighborsProp.boolValue) {
                    EditorGUI.indentLevel++;
                    CoreDrawers.DrawPropertyWithHelpBoxSupport(
                        serializedObject.FindProperty(NavAreaBase.PropNames.VisualizeNeighborsRegion));
                    EditorGUI.indentLevel--;
                }

                CoreDrawers.DrawPropertyWithHelpBoxSupport(
                    serializedObject.FindProperty(NavAreaBase.PropNames.VisualizeExternalLinks));

                NavSurfaceUpdate.VisIndex = EditorGUILayout.IntField(
                    new GUIContent("Visualization Index", "Can be used for debugging. Does nothing by default."),
                    NavSurfaceUpdate.VisIndex);

                EditorGUI.indentLevel--;
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.Pickable)]
        private static void RenderBoxGizmoSelected(NavSurface surface, GizmoType gizmoType) {
            // Draw neighbor connections of a chosen region.
            if (surface.VisualizeNeighbors && surface.VisualizeNeighborsRegion >= 0) {
                DrawNeighborVisualization(surface);
            }

            if (VisualizeAllSurfaces) {
                NavLayerMask surfaceMask = VisualizeAllSurfacesMask;
                foreach (NavSurface other in FindObjectsOfType<NavSurface>()) {
                    bool visualizeThis = other == surface || surfaceMask.Contains(other.Layer);
                    if (visualizeThis)
                        DrawSurfaceGizmos(other);
                }
            } else {
                DrawSurfaceGizmos(surface);
            }

            DrawExternalLinkVisualization(surface);
        }

        private static void DrawSurfaceGizmos(NavSurface surface) {
            DrawExternalLinkVisualization(surface);
        }

        private static void DrawNeighborVisualization(NavSurface surface) {
            surface.Register();

            if (surface.NativeData.Vertices.IsNull ||
                surface.VisualizeNeighborsRegion >= surface.NativeData.Regions.Length) {
                return;
            }

            NativeNavSurfaceData data = surface.NativeData;
            NativeNavSurfaceRegionData region = data.Regions[surface.VisualizeNeighborsRegion];

            Matrix4x4 mat = Gizmos.matrix;
            Color color = Gizmos.color;
            Matrix4x4 volumeTransform = surface.transform.localToWorldMatrix;
            Gizmos.matrix *= volumeTransform;
            Gizmos.color = Color.green;
            using Handles.DrawingScope _ = new(new Color(0, 1, 0, 0.3f), volumeTransform);

            for (int i = 0; i < region.InternalLinkRange.Length; i++) {
                NativeNavSurfaceInternalLinkData link = data.InternalLinks[region.InternalLinkRange.Start + i];

                for (int j = 0; j < link.EdgeRange.Length; j++) {
                    int2 edge = data.LinkEdges[link.EdgeRange.Start + j];
                    Vector3 v0 = data.Vertices[edge.x].xyz;
                    Vector3 v1 = data.Vertices[edge.y].xyz;
                    Gizmos.DrawLine(v0, v1);
                }

                for (int j = 0; j < link.VertexRange.Length; j++) {
                    int vertex = data.LinkVertices[link.VertexRange.Start + j];
                    Vector3 v = data.Vertices[vertex].xyz;
                    Gizmos.DrawSphere(v, 0.05f);
                }
            }

            Gizmos.matrix = mat;
            Gizmos.color = color;
        }
    }
}
