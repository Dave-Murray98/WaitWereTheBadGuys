// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav.Jobs {
    /// <summary>
    /// Job used to find a HyperNav path.
    /// </summary>
    //[BurstCompile]
    public struct NavPathJob : IJob {
        /// <summary>
        /// Map containing all loaded NavVolumes, keyed by their instance ID>
        /// </summary>
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeParallelHashMap<long, NativeNavVolumeData> Volumes;

        /// <summary>
        /// Map containing all loaded NavSurfaces, keyed by their instance ID>
        /// </summary>
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeParallelHashMap<long, NativeNavSurfaceData> Surfaces;

        /// <summary>
        /// Map indicating whether manual links (keyed by their instance ID) are enabled.
        /// </summary>
        [ReadOnly]
        public NativeHashMap<long, bool> ManualLinksEnabled;

        /// <summary>
        /// Position where the path starts (world space).
        /// </summary>
        public float4 StartPosition;

        /// <summary>
        /// Nav query result where the path starts.
        /// </summary>
        public NavSampleResult StartHit;

        /// <summary>
        /// Nav query result where the path ends.
        /// </summary>
        public NavSampleResult EndHit;

        /// <summary>
        /// Types of areas that the path can traverse.
        /// </summary>
        public NavAreaTypes AllowedAreaTypes;

        /// <summary>
        /// Layers that the path can traverse.
        /// </summary>
        public NavLayerMask AllowedLayers;

        /// <summary>
        /// Allows adding an additional cost to changing from a surface to a volume.
        /// </summary>
        public float CostToChangeToVolume;

        /// <summary>
        /// Allows adding an additional cost to changing from a volume to a surface.
        /// </summary>
        public float CostToChangeToSurface;

        /// <summary>
        /// Multiplier for the cost of traversing volume areas.
        /// </summary>
        public float VolumeCostMultiplier;

        /// <summary>
        /// Multiplier for the cost of traversing surface areas.
        /// </summary>
        public float SurfaceCostMultiplier;

        /// <summary>
        /// Number of times to apply the path tightening operation to attempt to shorten the path.
        /// </summary>
        public int PathTighteningIterations;

        /// <summary>
        /// Used to return the result path (as a list of waypoints) back to managed code.
        /// </summary>
        [WriteOnly]
        public NativeList<NativeNavWaypoint> OutPathWaypoints;

        private UnsafeHashMap<PendingPathNode, VisitedNodeInfo> _nodeTable;
        private UnsafeList<VisitedNodeInfo> _waypoints;
        private UnsafeHeap<PendingPathNode> _frontier;

        private bool _hasDestNodeInfo;

        private static readonly ProfilerMarker CalculateNodesTableMarker = new("CalculateNodesTable");

        /// <summary>
        /// Execute the pathfinding operation all the way through.
        /// </summary>
        public void Execute() {
            if (!Initialize()) {
                return;
            }

            PendingPathNode last;
            NavPathState state;
            using (CalculateNodesTableMarker.Auto()) {
                CalculatePath(out state, out last);
            }

            if (state == NavPathState.Success) {
                CompletePath(last);
            }
        }

        private bool Initialize() {
            if ((StartHit.Type & AllowedAreaTypes) == 0 || (EndHit.Type & AllowedAreaTypes) == 0) {
                return false;
            }

            if (!AllowedLayers.Contains(StartHit.Layer) || !AllowedLayers.Contains(EndHit.Layer)) {
                return false;
            }

            // Allocate data structures.
            _nodeTable = new UnsafeHashMap<PendingPathNode, VisitedNodeInfo>(256, Allocator.Temp);

            _frontier = new UnsafeHeap<PendingPathNode>(32, Allocator.Temp);
            _waypoints = new UnsafeList<VisitedNodeInfo>(16, Allocator.Temp);

            // Initial node to add to the frontier and visit first.
            PendingPathNode start = new() {
                AreaID = StartHit.AreaID,
                AreaType = StartHit.Type,
                RegionIndex = StartHit.Region,
            };

            float heuristic = GetHeuristic(StartHit.Position);
            _nodeTable[start] = new VisitedNodeInfo {
                Node = start,
                HasPrevious = false,
                Previous = default,
                EntryPoint = StartHit.Position,
                ExitPointFromPrevious = StartHit.Position,
                Heuristic = heuristic,
                CumulativeCost = 0,
                Visited = false,
                ConnectionIndex = -1,
                IsExternalConnection = false,
            };

            // Add initial data to NodeTable.
            _frontier.Add(start, -heuristic);

            return true;
        }

        // Execute pathfinding algorithm up to the given number of steps.
        private void CalculatePath(out NavPathState state, out PendingPathNode last) {
            // Update path until it completes, fails, or operation limit has been reached.
            while (_frontier.TryRemove(out PendingPathNode node)) {
                // Color color = node.IsExternalConnection ? Color.magenta : Color.red;
                // if (_nodeTable[node].HasPrevious) {
                //     Debug.DrawLine(_nodeTable[node].Previous.To.Position.xyz, node.From.Position.xyz, color);
                //     Debug.DrawLine(node.From.Position.xyz, node.From.Position.xyz + new float3(0, 0.2f, 0), Color.cyan);
                //     Debug.DrawLine(node.From.Position.xyz, node.To.Position.xyz, Color.yellow);
                // }

                // Check if algorithm has reached the destination node.
                if (node.AreaID == EndHit.AreaID && node.RegionIndex == EndHit.Region) {
                    last = node;
                    state = NavPathState.Success;
                    return;
                }

                // Mark node as visited.
                if (!_nodeTable.TryGetValue(node, out VisitedNodeInfo info)) {
                    info.Node = node;
                    info.ConnectionIndex = -1;
                }

                info.Visited = true;
                _nodeTable[node] = info;

                // Visit the current node, adding its neighbors to the frontier if needed.
                // If we visit the node containing the destination, the only neighbor we need to add is the
                // destination itself.
                if (node.AreaType == NavAreaTypes.Surface) {
                    NativeNavSurfaceData surfaceData = Surfaces[node.AreaID];
                    VisitSurfaceInternalLinks(node, in info, surfaceData);
                    VisitAreaExternalLinks(node, in info, surfaceData);
                } else if (node.AreaType == NavAreaTypes.Volume) {
                    NativeNavVolumeData volumeData = Volumes[node.AreaID];
                    VisitVolumeInternalLinks(node, in info, volumeData);
                    VisitAreaExternalLinks(node, in info, volumeData);
                } else {
                    Debug.LogError("Unsupported area type.");
                }
            }

            last = default;
            state = _frontier.Count > 0 ? NavPathState.Pending : NavPathState.Failure;
        }

        // Visit the current node, adding its neighbors to the frontier if needed.
        private static readonly ProfilerMarker ProfilerMarkerVisitAreaExternalLinks =
            new(nameof(VisitAreaExternalLinks));

        private static readonly ProfilerMarker ProfilerMarkerVisitAreaExternalLinksNearest =
            new(nameof(VisitAreaExternalLinks) + " Nearest Point");

        private void VisitAreaExternalLinks<TData>(
            PendingPathNode node,
            in VisitedNodeInfo nodeInfo,
            TData areaData)
            where TData : unmanaged, INativeNavAreaData {
            //using ProfilerMarker.AutoScope scope = ProfilerMarkerVisitAreaExternalLinks.Auto();
            SerializableRange externalLinkRange = areaData.GetExternalLinkRange(node.RegionIndex);

            // Loop through all the external links of the region the node leads into.
            int externalLinkCount = externalLinkRange.Length;
            int externalLinkStart = externalLinkRange.Start;
            for (int i = 0; i < externalLinkCount; i++) {
                // Ignore connection that leads back to region node originates from.
                int linkIndex = externalLinkStart + i;
                NativeNavExternalLinkData connection = areaData.GetExternalLinkData(linkIndex);
                if ((connection.ToAreaType & AllowedAreaTypes) == 0) continue;

                // External links may be invalid, may be to a volume in an unloaded scene, may be disabled,
                // or may be to a disallowed layer.
                // These links can be ignored.
                switch (connection.ToAreaType) {
                    case NavAreaTypes.Volume when
                        !Volumes.TryGetValue(connection.ToArea, out NativeNavVolumeData volume) ||
                        !AllowedLayers.Contains(volume.Layer):
                    case NavAreaTypes.Surface when
                        !Surfaces.TryGetValue(connection.ToArea, out NativeNavSurfaceData surface) ||
                        !AllowedLayers.Contains(surface.Layer):
                        continue;
                }

                PendingPathNode to = new() {
                    AreaID = connection.ToArea,
                    AreaType = connection.ToAreaType,
                    RegionIndex = connection.ToRegion,
                };

                bool wasDiscovered = _nodeTable.TryGetValue(to, out VisitedNodeInfo toInfo);
                if (toInfo.Visited) continue;

                if (connection.ManualLinkID != 0 &&
                    (!ManualLinksEnabled.TryGetValue(connection.ManualLinkID, out bool enabled) || !enabled)) {
                    continue;
                }

                // Calculate link cost.
                float4x4 areaTransform = areaData.Transform;
                float4 linkStartPos = math.mul(areaTransform, connection.FromPosition);
                float4 linkEndPos = math.mul(areaTransform, connection.ToPosition);

                float costToReachLink = math.distance(nodeInfo.EntryPoint, linkStartPos);
                float costOfLink = connection.InternalCost;
                if (node.AreaType == NavAreaTypes.Surface) {
                    costToReachLink *= SurfaceCostMultiplier;
                    if (connection.ToAreaType == NavAreaTypes.Volume) {
                        costOfLink += CostToChangeToVolume;
                    }
                } else {
                    costToReachLink *= VolumeCostMultiplier;
                    if (connection.ToAreaType == NavAreaTypes.Surface) {
                        costOfLink += CostToChangeToSurface;
                    }
                }

                float linkCost = costToReachLink + costOfLink;
                float nextCumulativeCost = nodeInfo.CumulativeCost + linkCost;
                float nextHeuristic = GetHeuristic(linkEndPos);
                float nextTotal = nextCumulativeCost + nextHeuristic;

                if (wasDiscovered && nextTotal >= toInfo.CumulativeCost + toInfo.Heuristic) continue;

                toInfo.CumulativeCost = nextCumulativeCost;
                toInfo.Heuristic = nextHeuristic;
                toInfo.ExitPointFromPrevious = linkStartPos;
                toInfo.EntryPoint = linkEndPos;
                toInfo.Previous = node;
                toInfo.HasPrevious = true;
                toInfo.ConnectionIndex = linkIndex;
                toInfo.IsExternalConnection = true;
                toInfo.Node = to;

                // Update cached node info for the PendingPathNode.
                UpdateNodeTable(in to, in toInfo, wasDiscovered);
            }
        }

        private static readonly ProfilerMarker ProfilerMarkerVisitVolumeInternalLinks =
            new(nameof(VisitVolumeInternalLinks));

        private void VisitVolumeInternalLinks(
            PendingPathNode node,
            in VisitedNodeInfo nodeInfo,
            NativeNavVolumeData data) {
            //using ProfilerMarker.AutoScope scope = ProfilerMarkerVisitVolumeInternalLinks.Auto();

            NativeNavVolumeRegionData region = data.Regions[node.RegionIndex];

            // Loop through all the internal links of the region the node leads into.
            int linkCount = region.InternalLinkRange.Length;
            int linkStart = region.InternalLinkRange.Start;

            for (int i = 0; i < linkCount; i++) {
                // Ignore connection that leads back to region node originates from.
                int linkIndex = linkStart + i;
                NativeNavVolumeInternalLinkData connection = data.InternalLinks[linkIndex];

                PendingPathNode to = new() {
                    AreaID = node.AreaID,
                    AreaType = NavAreaTypes.Volume,
                    RegionIndex = connection.ToRegion,
                };

                bool wasDiscovered = _nodeTable.TryGetValue(to, out VisitedNodeInfo toInfo);
                if (toInfo.Visited) continue;

                // Get nearest position on this link, then use that to calculate link cost.
                float4 nextPosition = GetNearestPointOnVolumeInternalLink(nodeInfo.EntryPoint, in data, linkIndex);
                float linkCost = math.distance(nodeInfo.EntryPoint, nextPosition);
                linkCost *= VolumeCostMultiplier;
                float nextCumulativeCost = nodeInfo.CumulativeCost + linkCost;
                float nextHeuristic = GetHeuristic(nextPosition);
                float nextTotal = nextCumulativeCost + nextHeuristic;

                if (wasDiscovered && nextTotal >= toInfo.CumulativeCost + toInfo.Heuristic) continue;

                toInfo.CumulativeCost = nextCumulativeCost;
                toInfo.Heuristic = nextHeuristic;
                toInfo.ExitPointFromPrevious = nextPosition;
                toInfo.EntryPoint = nextPosition;
                toInfo.Previous = node;
                toInfo.HasPrevious = true;
                toInfo.ConnectionIndex = linkIndex;
                toInfo.IsExternalConnection = false;
                toInfo.Node = to;

                // Update cached node info for the PendingPathNode.
                UpdateNodeTable(in to, in toInfo, wasDiscovered);
            }
        }

        private static readonly ProfilerMarker ProfilerMarkerVisitSurfaceInternalLinks =
            new(nameof(VisitSurfaceInternalLinks));

        private void VisitSurfaceInternalLinks(
            PendingPathNode node,
            in VisitedNodeInfo nodeInfo,
            NativeNavSurfaceData data) {
            //using ProfilerMarker.AutoScope scope = ProfilerMarkerVisitSurfaceInternalLinks.Auto();

            NativeNavSurfaceRegionData region = data.Regions[node.RegionIndex];

            // Loop through all the internal links of the region the node leads into.
            int linkCount = region.InternalLinkRange.Length;
            int linkStart = region.InternalLinkRange.Start;

            for (int i = 0; i < linkCount; i++) {
                // Ignore connection that leads back to region node originates from.
                int linkIndex = linkStart + i;
                NativeNavSurfaceInternalLinkData connection = data.InternalLinks[linkIndex];
                PendingPathNode to = new() {
                    AreaID = node.AreaID,
                    AreaType = NavAreaTypes.Surface,
                    RegionIndex = connection.ToRegion,
                };

                bool wasDiscovered = _nodeTable.TryGetValue(to, out VisitedNodeInfo toInfo);
                if (toInfo.Visited) continue;

                // Get nearest position on this link, then use that to calculate link cost.
                float4 nextPosition = GetNearestPointOnSurfaceInternalLink(nodeInfo.EntryPoint, in data, linkIndex);
                float linkCost = math.distance(nodeInfo.EntryPoint, nextPosition);
                linkCost *= SurfaceCostMultiplier;
                float nextCumulativeCost = nodeInfo.CumulativeCost + linkCost;
                float nextHeuristic = GetHeuristic(nextPosition);
                float nextTotal = nextCumulativeCost + nextHeuristic;

                if (wasDiscovered && nextTotal >= toInfo.CumulativeCost + toInfo.Heuristic) continue;

                toInfo.CumulativeCost = nextCumulativeCost;
                toInfo.Heuristic = nextHeuristic;
                toInfo.ExitPointFromPrevious = nextPosition;
                toInfo.EntryPoint = nextPosition;
                toInfo.Previous = node;
                toInfo.HasPrevious = true;
                toInfo.ConnectionIndex = linkIndex;
                toInfo.IsExternalConnection = false;
                toInfo.Node = to;

                // Update cached node info for the PendingPathNode.
                UpdateNodeTable(in to, in toInfo, wasDiscovered);
            }
        }

        // Update the info in the node table for the given node,
        // if the found cost is lower than known cost or node isn't discovered yet.
        private static readonly ProfilerMarker ProfilerMarkerUpdateNodeTable = new(nameof(UpdateNodeTable));

        private void UpdateNodeTable(in PendingPathNode to, in VisitedNodeInfo toInfo, bool wasDiscovered) {
            _nodeTable[to] = toInfo;
            if (wasDiscovered) {
                _frontier.Update(to, -(toInfo.Heuristic + toInfo.CumulativeCost));
            } else {
                _frontier.Add(to, -(toInfo.Heuristic + toInfo.CumulativeCost));
            }
        }

        private static readonly ProfilerMarker ProfilerMarkerProcessTableIntoList = new(nameof(ProcessTableIntoList));
        private static readonly ProfilerMarker ProfilerMarkerTightenPath = new(nameof(TightenPath));
        private static readonly ProfilerMarker ProfilerMarkerSimplifyPath = new(nameof(SimplifyPath));
        private static readonly ProfilerMarker ProfilerMarkerCreateOutputList = new("CreateOutputList");

        // Process a completed (successful) path into the output waypoints list.
        private void CompletePath(in PendingPathNode lastNode) {
            // Table format provides the previous node in the path for each node.
            // Convert this to a list.
            ProcessTableIntoList(lastNode);

            // Attempt to shorten the path by pulling nodes closer to each other within their bounds.
            for (int i = 0; i < PathTighteningIterations; i++) {
                TightenPath();
            }

            // Remove unnecessary nodes from the path by raycasting.
            SimplifyPath();

            // Convert the internal waypoints to the output list.
            for (int i = 0; i < _waypoints.Length; i++) {
                VisitedNodeInfo cur = _waypoints[i];

                // Determine the node type based on its from and to volumes.
                NavWaypointType entryType;
                long entryAreaID;
                int entryRegion;
                float4 up = float4.zero;
                if (cur.IsExternalConnection) {
                    entryType = cur.Previous.AreaType == NavAreaTypes.Volume
                        ? NavWaypointType.ExitVolume
                        : NavWaypointType.ExitSurface;
                    entryAreaID = cur.Previous.AreaID;
                    entryRegion = cur.Previous.RegionIndex;
                } else {
                    entryType = cur.HasPrevious
                        ? cur.Node.AreaType == NavAreaTypes.Volume
                            ? NavWaypointType.InsideVolume
                            : NavWaypointType.InsideSurface
                        : cur.Node.AreaType == NavAreaTypes.Volume
                            ? NavWaypointType.EnterVolume
                            : NavWaypointType.EnterSurface;
                    entryAreaID = cur.Node.AreaID;
                    entryRegion = cur.Node.RegionIndex;
                }

                if (entryType == NavWaypointType.ExitSurface) {
                    NativeNavSurfaceData surface = Surfaces[cur.Previous.AreaID];
                    float4 localUp = surface.Regions[cur.Previous.RegionIndex].UpVector;
                    up = math.mul(surface.Transform, localUp);
                } else if (entryType is NavWaypointType.InsideSurface or NavWaypointType.EnterSurface) {
                    NativeNavSurfaceData surface = Surfaces[cur.Node.AreaID];
                    float4 localUp = surface.Regions[cur.Node.RegionIndex].UpVector;
                    up = math.mul(surface.Transform, localUp);
                }

                OutPathWaypoints.Add(new NativeNavWaypoint(cur.ExitPointFromPrevious, up, entryType, entryAreaID, entryRegion));

                // External connections need two waypoints because they have a connection point at each volume.
                if (cur.IsExternalConnection) {
                    NavWaypointType exitType = cur.Node.AreaType == NavAreaTypes.Volume
                        ? NavWaypointType.EnterVolume
                        : NavWaypointType.EnterSurface;

                    float4 toUp = float4.zero;
                    if (exitType == NavWaypointType.EnterSurface) {
                        NativeNavSurfaceData surface = Surfaces[cur.Node.AreaID];
                        float4 localUp = surface.Regions[cur.Node.RegionIndex].UpVector;
                        toUp = math.mul(surface.Transform, localUp);
                    }

                    OutPathWaypoints.Add(new NativeNavWaypoint(cur.EntryPoint, toUp, exitType, cur.Node.AreaID, cur.Node.RegionIndex));
                }
            }
        }

        // Table format provides the previous node in the path for each node.
        // Convert this to a list.
        private void ProcessTableIntoList(in PendingPathNode lastNode) {
            // Add all points along the path to the list, in reverse order.
            PendingPathNode current = lastNode;
            while (true) {
                VisitedNodeInfo curInfo = _nodeTable[current];
                _waypoints.Add(curInfo);

                if (!curInfo.HasPrevious) break;
                current = curInfo.Previous;
            }

            // Reverse the list so the points are in the correct order.
            int count = _waypoints.Length;
            int halfCount = count / 2;
            for (int i = 0; i < halfCount; i++) {
                VisitedNodeInfo temp = _waypoints[i];
                _waypoints[i] = _waypoints[count - i - 1];
                _waypoints[count - i - 1] = temp;
            }

            // Add the last node to the list.
            _waypoints.Add(new VisitedNodeInfo {
                Node = new PendingPathNode {
                    AreaID = EndHit.AreaID,
                    AreaType = EndHit.Type,
                    RegionIndex = EndHit.Region,
                },
                HasPrevious = true,
                Previous = _waypoints[^1].Node,
                EntryPoint = EndHit.Position,
                ExitPointFromPrevious = EndHit.Position,
                Visited = true,
                ConnectionIndex = -1,
            });
        }

        // Attempt to shorten the path by pulling nodes closer to each other within their bounds.
        private void TightenPath() {
            int numTightens = _waypoints.Length - 2;
            if (numTightens <= 0) return;

            int numIterations = numTightens / 2;

            for (int i = 0; i < numIterations; i++) {
                TightenWaypoint(i + 1, false);
                TightenWaypoint(_waypoints.Length - 2 - i, true);
            }

            if (numTightens % 2 == 1) {
                TightenWaypoint(numIterations + 1, false);
            }
        }

        // Must only be called for a waypoint that is not the first or last in the path.
        private void TightenWaypoint(int index, bool isCloserToEnd) {
            // External links are at a fixed position.
            VisitedNodeInfo waypoint = _waypoints[index];
            if (waypoint.IsExternalConnection) {
                TightenWaypointForExternalConnection(index, isCloserToEnd);
            } else if (waypoint.Previous.AreaType == NavAreaTypes.Volume) {
                TightenWaypointForVolumeConnection(index);
            } else if (waypoint.Previous.AreaType == NavAreaTypes.Surface) {
                TightenWaypointForSurfaceConnection(index);
            }
        }

        private void TightenWaypointForExternalConnection(int index, bool isCloserToEnd) {
            float4 prevPos = _waypoints[index - 1].EntryPoint;
            float4 nextPos = _waypoints[index + 1].ExitPointFromPrevious;

            VisitedNodeInfo waypoint = _waypoints[index];

            float costMultiplierTo =
                waypoint.Node.AreaType == NavAreaTypes.Surface ? SurfaceCostMultiplier : VolumeCostMultiplier;
            float costMultiplierFrom =
                waypoint.Previous.AreaType == NavAreaTypes.Surface ? SurfaceCostMultiplier : VolumeCostMultiplier;

            float4 linkStartPos;
            float4 linkEndPos;

            bool projectEnterPointFirst;
            if (costMultiplierTo > costMultiplierFrom) {
                projectEnterPointFirst = true;
            } else if (costMultiplierTo < costMultiplierFrom) {
                projectEnterPointFirst = false;
            } else {
                projectEnterPointFirst = isCloserToEnd;
            }

            if (projectEnterPointFirst) {
                linkStartPos = ProjectPointOnArea(waypoint.Previous, nextPos);
                linkEndPos = ProjectPointOnArea(waypoint.Node, linkStartPos);
            } else {
                linkEndPos = ProjectPointOnArea(waypoint.Node, prevPos);
                linkStartPos = ProjectPointOnArea(waypoint.Previous, linkEndPos);
            }

            waypoint.ExitPointFromPrevious = linkStartPos;
            waypoint.EntryPoint = linkEndPos;

            _waypoints[index] = waypoint;
        }

        private float4 ProjectPointOnArea(in PendingPathNode node, float4 targetPos) {
            if (node.AreaType == NavAreaTypes.Volume) {
                NativeNavVolumeData toVolume = Volumes[node.AreaID];
                return NativeMathUtility.GetNearestPointOnVolumeRegion(
                    toVolume, node.RegionIndex, targetPos);
            } else {
                NativeNavSurfaceData toSurface = Surfaces[node.AreaID];
                return NativeMathUtility.GetNearestPointOnSurfaceRegion(
                    toSurface, node.RegionIndex, targetPos);
            }
        }

        private void TightenWaypointForVolumeConnection(int index) {
            float4 prevPos = _waypoints[index - 1].EntryPoint;
            float4 nextPos = _waypoints[index + 1].ExitPointFromPrevious;

            VisitedNodeInfo waypoint = _waypoints[index];
            NativeNavVolumeData volume = Volumes[waypoint.Node.AreaID];
            NativeNavVolumeInternalLinkData link = volume.InternalLinks[waypoint.ConnectionIndex];

            float4 nearestPoint;
            if (link.TriangleRange.Length > 0) {
                int3 tri = volume.LinkTriangles[link.TriangleRange.Start];
                float4 tri0 = math.mul(volume.Transform, volume.Vertices[tri.x]);
                float4 tri1 = math.mul(volume.Transform, volume.Vertices[tri.y]);
                float4 tri2 = math.mul(volume.Transform, volume.Vertices[tri.z]);

                NativeRay line = new(prevPos, nextPos - prevPos);
                float3 cross = math.normalize(math.cross(tri1.xyz - tri0.xyz, tri2.xyz - tri0.xyz));
                NativePlane plane = new(new float4(cross, 0), tri0);

                bool intersect = plane.Raycast(line.Origin, line.Direction, out float distance);
                if (!intersect) return;

                nearestPoint = line.GetPoint(distance);
            } else if (link.EdgeRange.Length > 0) {
                int2 edge = volume.LinkEdges[link.EdgeRange.Start];
                float4 edgeStart = math.mul(volume.Transform, volume.Vertices[edge.x]);
                float4 edgeEnd = math.mul(volume.Transform, volume.Vertices[edge.y]);

                NativeRay line1 = new(prevPos, nextPos - prevPos);
                NativeRay line2 = new(edgeStart, edgeEnd - edgeStart);

                bool intersect = NativeMathUtility.GetNearestPointOnLines(line1, line2, out float t1, out float _);
                if (!intersect) return;

                nearestPoint = line1.GetPoint(t1);
            } else {
                return;
            }

            float4 nearestPointOnLink =
                GetNearestPointOnVolumeInternalLink(nearestPoint, volume, waypoint.ConnectionIndex);

            waypoint.ExitPointFromPrevious = nearestPointOnLink;
            waypoint.EntryPoint = nearestPointOnLink;
            _waypoints[index] = waypoint;
        }

        private void TightenWaypointForSurfaceConnection(int index) {
            float4 prevPos = _waypoints[index - 1].EntryPoint;
            float4 nextPos = _waypoints[index + 1].ExitPointFromPrevious;

            VisitedNodeInfo waypoint = _waypoints[index];
            NativeNavSurfaceData surface = Surfaces[waypoint.Previous.AreaID];
            NativeNavSurfaceInternalLinkData link = surface.InternalLinks[waypoint.ConnectionIndex];

            // A link that does not share edges only has a fixed position.
            if (link.EdgeRange.Length == 0) return;

            int2 edge = surface.LinkEdges[link.EdgeRange.Start];
            float4 edgeStart = math.mul(surface.Transform, surface.Vertices[edge.x]);
            float4 edgeEnd = math.mul(surface.Transform, surface.Vertices[edge.y]);

            NativeRay line1 = new(prevPos, nextPos - prevPos);
            NativeRay line2 = new(edgeStart, edgeEnd - edgeStart);

            bool intersect = NativeMathUtility.GetNearestPointOnLines(line1, line2, out float t1, out float _);
            if (!intersect) return;

            float4 nearestPoint = line1.GetPoint(t1);
            float4 nearestPointOnLink =
                GetNearestPointOnSurfaceInternalLink(nearestPoint, surface, waypoint.ConnectionIndex);

            waypoint.ExitPointFromPrevious = nearestPointOnLink;
            waypoint.EntryPoint = nearestPointOnLink;
            _waypoints[index] = waypoint;
        }

        // Remove unnecessary nodes from the path by raycasting against the volumes' blocking triangles.
        private void SimplifyPath() {
            // Each point needs to check if there is any later point that it has a clear path to.
            // This is an n^2 algorithm but the number of waypoints should be fairly low.
            for (int startIndex = 0; startIndex < _waypoints.Length - 2; startIndex++) {
                VisitedNodeInfo startWaypoint = _waypoints[startIndex];

                NavAreaTypes startType = startWaypoint.Node.AreaType;

                if (startType is not (NavAreaTypes.Surface or NavAreaTypes.Volume)) {
                    continue;
                }

                // If last waypoint in path, cannot remove more.
                if (startWaypoint.Node.AreaID < 0) {
                    startIndex++;
                    continue;
                }

                // Get position where path leaves the waypoint.
                float4 startPos = startWaypoint.EntryPoint;

                // Loop through all waypoints after the current one.
                for (int nextIndex = _waypoints.Length - 1; nextIndex > startIndex + 1; nextIndex--) {
                    // Waypoint after the one being considered for removal.
                    VisitedNodeInfo nextNextWaypoint = _waypoints[nextIndex];

                    // Only support removing waypoints that are in the same area.
                    if (startWaypoint.Node.AreaID != nextNextWaypoint.Previous.AreaID) {
                        continue;
                    }

                    // If line is clear, update last removable index.
                    // If not clear, keep going because a future node might be clear.
                    bool isClear;
                    if (startType == NavAreaTypes.Surface) {
                        NativeNavSurfaceData surfaceData = Surfaces[startWaypoint.Node.AreaID];
                        float4 start = math.mul(surfaceData.InverseTransform, startPos);
                        float4 end = math.mul(surfaceData.InverseTransform, nextNextWaypoint.ExitPointFromPrevious);
                        isClear = NativeMathUtility.NavSurfacePathCheck(
                            startWaypoint.Node.RegionIndex,
                            nextNextWaypoint.Previous.RegionIndex,
                            start, end, 0.97f, surfaceData);
                    } else {
                        NativeNavVolumeData volumeData = Volumes[startWaypoint.Node.AreaID];
                        float4 start = math.mul(volumeData.InverseTransform, startPos);
                        float4 end = math.mul(volumeData.InverseTransform, nextNextWaypoint.ExitPointFromPrevious);
                        isClear = !NativeMathUtility.NavVolumeRaycast(start, end, true, volumeData, out _);
                    }

                    if (isClear) {
                        _waypoints.RemoveRange(startIndex + 1, nextIndex - startIndex - 1);
                        break;
                    }
                }
            }
        }

        // Get distance from a node to the destination.
        private float GetHeuristic(float4 position) {
            float heuristic = math.distance(position, EndHit.Position);
            return heuristic;
        }

        // Given an internal link and a previous position,
        // get the position on the internal link nearest the previous position.
        private float4 GetNearestPointOnVolumeInternalLink(float4 reference, in NativeNavVolumeData volume,
                                                           int linkIndex) {
            float4 localPos = math.mul(volume.InverseTransform, reference);

            float4 closestPoint = default;
            float closestDistance = float.PositiveInfinity;

            // Helper function to check if a point is closer than the current best.
            static void TestPoint(float4 point, float4 localPos, ref float4 closestPoint, ref float closestDistance) {
                float dist2 = math.distancesq(point, localPos);
                if (dist2 < closestDistance) {
                    closestPoint = point;
                    closestDistance = dist2;
                }
            }

            ref readonly NativeNavVolumeInternalLinkData link = ref volume.InternalLinks[linkIndex];

            // Check all triangles.
            int triStart = link.TriangleRange.Start;
            int triCount = link.TriangleRange.Length;
            for (int i = 0; i < triCount; i++) {
                int3 tri = volume.LinkTriangles[triStart + i];

                if (NativeMathUtility.GetNearestPointOnTriangle(volume.Vertices[tri.x], volume.Vertices[tri.y],
                        volume.Vertices[tri.z], localPos,
                        out float4 triPoint)) {
                    TestPoint(triPoint, localPos, ref closestPoint, ref closestDistance);
                }
            }

            // Check all edges.
            int edgeStart = link.EdgeRange.Start;
            int edgeCount = link.EdgeRange.Length;
            for (int i = 0; i < edgeCount; i++) {
                int2 edge = volume.LinkEdges[edgeStart + i];

                if (NativeMathUtility.GetNearestPointOnSegment(volume.Vertices[edge.x], volume.Vertices[edge.y],
                        localPos, out float4 edgePoint)) {
                    TestPoint(edgePoint, localPos, ref closestPoint, ref closestDistance);
                }
            }

            // Check all vertices.
            int vertexStart = link.VertexRange.Start;
            int vertexCount = link.VertexRange.Length;
            for (int i = 0; i < vertexCount; i++) {
                TestPoint(volume.Vertices[volume.LinkVertices[vertexStart + i]],
                    localPos, ref closestPoint, ref closestDistance);
            }

            float4 closest = math.mul(volume.Transform, closestPoint);
            return closest;
        }

        // Given an internal link and a previous position,
        // get the position on the internal link nearest the previous position.
        private float4 GetNearestPointOnSurfaceInternalLink(float4 reference, in NativeNavSurfaceData surface,
                                                            int linkIndex) {
            float4 localPos = math.mul(surface.InverseTransform, reference);

            float4 closestPoint = default;
            float closestDistance = float.PositiveInfinity;

            // Helper function to check if a point is closer than the current best.
            static void TestPoint(float4 point, float4 localPos, ref float4 closestPoint, ref float closestDistance) {
                float dist2 = math.distancesq(point, localPos);
                if (dist2 < closestDistance) {
                    closestPoint = point;
                    closestDistance = dist2;
                }
            }

            ref readonly NativeNavSurfaceInternalLinkData link = ref surface.InternalLinks[linkIndex];

            // Check all edges.
            int edgeStart = link.EdgeRange.Start;
            int edgeCount = link.EdgeRange.Length;
            for (int i = 0; i < edgeCount; i++) {
                int2 edge = surface.LinkEdges[edgeStart + i];

                if (NativeMathUtility.GetNearestPointOnSegment(surface.Vertices[edge.x], surface.Vertices[edge.y],
                        localPos, out float4 edgePoint)) {
                    TestPoint(edgePoint, localPos, ref closestPoint, ref closestDistance);
                }
            }

            // Check all vertices.
            int vertexStart = link.VertexRange.Start;
            int vertexCount = link.VertexRange.Length;
            for (int i = 0; i < vertexCount; i++) {
                TestPoint(surface.Vertices[surface.LinkVertices[vertexStart + i]],
                    localPos, ref closestPoint, ref closestDistance);
            }

            float4 closest = math.mul(surface.Transform, closestPoint);
            return closest;
        }

        private struct PendingPathNode : IEquatable<PendingPathNode> {
            public long AreaID;

            public NavAreaTypes AreaType;

            public int RegionIndex;

            public bool Equals(PendingPathNode other) {
                return AreaID == other.AreaID && RegionIndex == other.RegionIndex;
            }

            public override bool Equals(object obj) {
                return obj is PendingPathNode other && Equals(other);
            }

            public override int GetHashCode() {
                unchecked {
                    int hashCode = AreaID.GetHashCode();
                    hashCode = (hashCode * 397) ^ RegionIndex.GetHashCode();
                    return hashCode;
                }
            }
        }

        private struct VisitedNodeInfo {
            public PendingPathNode Node;
            public bool HasPrevious;
            public PendingPathNode Previous;
            public float4 EntryPoint;
            public float4 ExitPointFromPrevious;
            public float Heuristic;
            public float CumulativeCost;
            public bool Visited;
            public int ConnectionIndex;
            public bool IsExternalConnection;
        }
    }
}
