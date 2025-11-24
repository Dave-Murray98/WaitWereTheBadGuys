// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Linq;
using Infohazard.HyperNav.Jobs;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Infohazard.HyperNav.Editor {
    [CustomEditor(typeof(ManualNavLink))]
    [CanEditMultipleObjects]
    public class ManualNavLinkEditor : UnityEditor.Editor {
        public static readonly Color ValidLinkColor = new(0.25f, 0.5f, 1.0f, 0.5f);
        public static readonly Color InvalidLinkColor = new(1.0f, 0.25f, 0.25f, 0.5f);

        private void OnEnable() {
            foreach (NavVolume volume in FindObjectsOfType<NavVolume>()) {
                volume.Register();
            }
        }

        private void OnSceneGUI() {

            ManualNavLink link = (ManualNavLink)target;
            Span<NavSampleQuery> queries = stackalloc NavSampleQuery[2];
            Span<NavSampleResult> results = stackalloc NavSampleResult[2];

            queries[0] = new NavSampleQuery(link.WorldStartPoint, 0, link.StartTypes);
            queries[1] = new NavSampleQuery(link.WorldEndPoint, 0, link.EndTypes);

            if (NavVolume.NativeDataMap.IsCreated) {
                NavSampleJob.SamplePositionsInAllAreas(queries, results);
            }

            Vector3 center = link.transform.position;
            Vector3 startPos = link.WorldStartPoint;
            Vector3 endPos = link.WorldEndPoint;

            if (startPos != endPos) {
                DrawLinkGizmos(link, results[0].AreaID != 0, results[1].AreaID != 0);
            }

            bool isLocal = Tools.pivotRotation == PivotRotation.Local;
            Quaternion handleRotation = isLocal ? link.transform.rotation : Quaternion.identity;

            if (startPos != endPos && startPos != center) {
                Vector3 newPos = Handles.PositionHandle(startPos, handleRotation);
                if (newPos != startPos) {
                    Undo.RecordObject(link, "Move Link Start");
                    link.WorldStartPoint = newPos;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(link);
                }
            }

            if (endPos != startPos && endPos != center) {
                Vector3 newPos = Handles.PositionHandle(endPos, handleRotation);
                if (newPos != endPos) {
                    Undo.RecordObject(link, "Move Link End");
                    link.WorldEndPoint = newPos;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(link);
                }
            }
        }

        private void DrawLinkGizmos(ManualNavLink link, bool startHit, bool endHit) {
            bool isValid = startHit && endHit;
            Color c = Handles.color;
            Handles.color = isValid ? ValidLinkColor : InvalidLinkColor;

            Vector3 start = link.WorldStartPoint;
            Vector3 end = link.WorldEndPoint;

            Handles.DrawLine(start, end, 5);

            Handles.color = startHit ? ValidLinkColor : InvalidLinkColor;
            Vector3 dirStartHandle = end - start;
            if (link.IsBidirectional) dirStartHandle *= -1;

            Handles.ConeHandleCap(0, start, Quaternion.LookRotation(dirStartHandle), 0.5f, EventType.Repaint);

            Handles.color = endHit ? ValidLinkColor : InvalidLinkColor;
            Vector3 dirEndHandle = end - start;

            Handles.ConeHandleCap(0, end, Quaternion.LookRotation(dirEndHandle), 0.5f, EventType.Repaint);

            Handles.color = c;
        }
    }
}
