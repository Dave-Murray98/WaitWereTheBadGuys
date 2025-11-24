// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System.Linq;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Infohazard.HyperNav.Settings;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Infohazard.HyperNav.Editor {
    /// <summary>
    /// Custom editor for <see cref="Infohazard.HyperNav.NavVolume"/>.
    /// </summary>
    [CustomEditor(typeof(NavVolume))]
    [CanEditMultipleObjects]
    public class NavVolumeEditor : NavAreaBaseEditor {
        protected override void DrawPhysicsQueryProperties(SerializedProperty property) {
            base.DrawPhysicsQueryProperties(property);
            EditorGUILayout.PropertyField(
                property.FindPropertyRelative(NavVolumeSettings.PropNames.EnableMultiQuery));
        }

        protected override void DrawVisualizationProperties() {
            SerializedProperty visProp = serializedObject.FindProperty(NavVolume.PropNames.VisualizationMode);

            // Allow visualization settings to be hidden to reduce clutter.
            // Can use isExpanded on the property even though it is not normally expandable.
            visProp.isExpanded =
                EditorGUILayout.Foldout(visProp.isExpanded, "Visualization Settings", EditorStyles.foldoutHeader);
            if (visProp.isExpanded) {
                EditorGUI.indentLevel++;
                DrawVisualizeAllProperties();

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(visProp);
                if (visProp.enumValueIndex >= (int)NavVolumeVisualizationMode.InitialRegions) {
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty(NavAreaBase.PropNames.VisualizationSoloRegion));
                }

                // When changing visualization mode, delete current visualization mesh to force regen.
                if (EditorGUI.EndChangeCheck()) {
                    foreach (NavVolume volume in targets.Cast<NavVolume>()) {
                        volume.PreviewMesh = null;
                    }
                }

                SerializedProperty visNeighborsProp =
                    serializedObject.FindProperty(NavAreaBase.PropNames.VisualizeNeighbors);
                EditorGUILayout.PropertyField(visNeighborsProp);

                // Draw VisualizeNeighborsRegion only if VisualizeNeighbors is true.
                if (visNeighborsProp.boolValue) {
                    EditorGUI.indentLevel++;
                    SerializedProperty visNeighborRegionProp =
                        serializedObject.FindProperty(NavAreaBase.PropNames.VisualizeNeighborsRegion);
                    EditorGUILayout.PropertyField(visNeighborRegionProp, new GUIContent("Region"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty(NavAreaBase.PropNames.VisualizeExternalLinks));

                SerializedProperty showVertsProp = serializedObject.FindProperty(NavAreaBase.PropNames.ShowVertexNumbers);
                EditorGUILayout.PropertyField(showVertsProp);

                // Draw ShowVertexNumbersRange only if ShowVertexNumbers is true.
                if (showVertsProp.boolValue) {
                    EditorGUI.indentLevel++;
                    SerializedProperty showVertsRangeProp =
                        serializedObject.FindProperty(NavAreaBase.PropNames.ShowVertexNumbersRange);
                    EditorGUILayout.PropertyField(showVertsRangeProp, new GUIContent("Range"));
                    showVertsRangeProp.floatValue = Mathf.Max(0, showVertsRangeProp.floatValue);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.PropertyField(serializedObject.FindProperty(NavAreaBase.PropNames.VisualizeRegionBounds));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(NavAreaBase.PropNames.VisualizeVoxelQueries));
                EditorGUI.indentLevel--;
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.Pickable)]
        private static void RenderBoxGizmoSelected(NavVolume volume, GizmoType gizmoType) {
            // Draw neighbor connections of a chosen region.
            if (volume.VisualizeNeighbors && volume.VisualizeNeighborsRegion >= 0) {
                DrawNeighborVisualization(volume);
            }

            if (VisualizeAllVolumes) {
                NavLayerMask volumeMask = VisualizeAllVolumesMask;
                foreach (NavVolume other in FindObjectsOfType<NavVolume>()) {
                    bool visualizeThis = other == volume || volumeMask.Contains(other.Layer);
                    if (visualizeThis)
                        DrawVolumeGizmos(other);
                }
            } else {
                DrawVolumeGizmos(volume);
            }
        }

        private static void DrawVolumeGizmos(NavVolume volume) {
            if (volume.VisualizeRegionBounds) {
                DrawRegionBoundsVisualization(volume);
            }

            DrawExternalLinkVisualization(volume);
        }

        private static void DrawNeighborVisualization(NavVolume volume) {
            volume.Register();

            if (volume.NativeData.Vertices.IsNull ||
                volume.VisualizeNeighborsRegion >= volume.NativeData.Regions.Length) {
                return;
            }

            NativeNavVolumeData data = volume.NativeData;
            NativeNavVolumeRegionData region = data.Regions[volume.VisualizeNeighborsRegion];

            Matrix4x4 mat = Gizmos.matrix;
            Color color = Gizmos.color;
            Matrix4x4 volumeTransform = volume.transform.localToWorldMatrix;
            Gizmos.matrix *= volumeTransform;
            Gizmos.color = Color.green;
            using Handles.DrawingScope _ = new(new Color(0, 1, 0, 0.3f), volumeTransform);

            for (int i = 0; i < region.InternalLinkRange.Length; i++) {
                NativeNavVolumeInternalLinkData link = data.InternalLinks[region.InternalLinkRange.Start + i];

                for (int j = 0; j < link.TriangleRange.Length; j++) {
                    int3 tri = data.LinkTriangles[link.TriangleRange.Start + j];
                    Vector3 v0 = data.Vertices[tri.x].xyz;
                    Vector3 v1 = data.Vertices[tri.y].xyz;
                    Vector3 v2 = data.Vertices[tri.z].xyz;
                    Handles.DrawAAConvexPolygon(v0, v1, v2);
                }

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

        private static void DrawRegionBoundsVisualization(NavVolume volume) {
            if (volume.NativeData.Regions.IsNull) return;
            Color color = Gizmos.color;
            Gizmos.color = Color.green;
            Matrix4x4 matrix = Gizmos.matrix;
            Gizmos.matrix = volume.transform.localToWorldMatrix;

            for (int i = 0; i < volume.NativeData.Regions.Length; i++) {
                if (volume.VisualizationSoloRegion >= 0 && i != volume.VisualizationSoloRegion) continue;

                NativeNavVolumeRegionData region = volume.NativeData.Regions[i];
                Gizmos.DrawWireCube(region.Bounds.Center.xyz, region.Bounds.Size.xyz);
            }

            Gizmos.color = color;
            Gizmos.matrix = matrix;
        }


    }
}
