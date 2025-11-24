// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Unity.Burst;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Infohazard.HyperNav.Jobs.Utility {
    /// <summary>
    /// Provides math operations that are compatible with Burst.
    /// </summary>
    /// <remarks>
    /// Managed side versions are available in the Infohazard.Core library under MathUtility.
    /// </remarks>
    public static class NativeMathUtility {
        /// <summary>
        /// Project a vector onto a the plane defined by a normal.
        /// </summary>
        /// <param name="vector">The vector to project.</param>
        /// <param name="normal">The normal of the plane.</param>
        /// <returns>The projected vector.</returns>
        public static float4 ProjectOnPlane(in float4 vector, in float3 normal) {
            float3 proj = math.project(vector.xyz, normal);
            return vector - new float4(proj, 0);
        }

        /// <summary>
        /// Find the point on a bounded line segment where it is nearest to a position,
        /// and return whether that point is in the segment's bounds.
        /// </summary>
        /// <remarks>
        /// Does not return points on the ends of the segment.
        /// If the nearest point on the segment's line is outside the segment,
        /// will fail and not return a valid point.
        /// </remarks>
        /// <param name="v1">The start of the segment.</param>
        /// <param name="v2">The end of the segment.</param>
        /// <param name="point">The point to search for.</param>
        /// <param name="pointOnSegment">The point on the segment closest to the input point.</param>
        /// <returns>Whether the nearest point is within the segment's bounds.</returns>
        public static bool GetNearestPointOnSegment(in float4 v1, in float4 v2, in float4 point,
                                                    out float4 pointOnSegment) {
            pointOnSegment = default;

            float4 v1ToV2 = v2 - v1;

            if (math.dot(v1ToV2, point - v1) < 0) return false;
            if (math.dot(-v1ToV2, point - v2) < 0) return false;

            float4 proj = math.project(point - v1, v1ToV2);
            pointOnSegment = v1 + proj;
            return true;
        }

        /// <summary>
        /// Find the point on a triangle where it is nearest to a position,
        /// and return whether that point is in the triangle's bounds.
        /// </summary>
        /// <remarks>
        /// Does not return points on the edge of the triangle.
        /// If the nearest point on the triangle's plane is outside the triangle,
        /// will fail and not return a valid point.
        /// </remarks>
        /// <param name="v1">The first triangle point.</param>
        /// <param name="v2">The second triangle point.</param>
        /// <param name="v3">The third triangle point.</param>
        /// <param name="point">The point to search for.</param>
        /// <param name="pointOnTriangle">The point on the triangle closest to the input point.</param>
        /// <returns>Whether the nearest point is within the triangle's bounds.</returns>
        public static bool GetNearestPointOnTriangle(in float4 v1, in float4 v2, in float4 v3, in float4 point,
                                                     out float4 pointOnTriangle) {
            pointOnTriangle = default;

            float3 normal = math.cross((v3 - v2).xyz, (v1 - v2).xyz);

            if (!IsPointInsideBound(v1, v2, normal, point) ||
                !IsPointInsideBound(v2, v3, normal, point) ||
                !IsPointInsideBound(v3, v1, normal, point)) {
                return false;
            }

            float4 proj = ProjectOnPlane(point - v1, normal);
            pointOnTriangle = v1 + proj;
            return true;
        }

        /// <summary>
        /// Find the point on a triangle (including its bounds) where it is nearest to a position.
        /// </summary>
        /// <remarks>
        /// If nearest point is on the triangle's bounds, that point will be returned,
        /// unlike <see cref="GetNearestPointOnTriangle"/>.
        /// </remarks>
        /// <param name="v1">The first triangle point.</param>
        /// <param name="v2">The second triangle point.</param>
        /// <param name="v3">The third triangle point.</param>
        /// <param name="point">The point to search for.</param>
        /// <returns>The nearest point on the triangle to the given point.</returns>
        [BurstCompile]
        public static float4 GetNearestPointOnTriangleIncludingBounds(in float4 v1, in float4 v2, in float4 v3,
                                                                      in float4 point) {
            // Check the triangle itself to see if the nearest point is within the triangle.
            if (GetNearestPointOnTriangle(v1, v2, v3, point, out float4 tPos)) {
                return tPos;
            }

            float4 nearest = float4.zero;
            float nearestSqrDist = float.PositiveInfinity;

            // Check each edge of the triangle to see if the nearest point is within that edge.
            bool e12 = CheckNearestPointOnTriangleEdge(v1, v2, point, ref nearest, ref nearestSqrDist);
            bool e23 = CheckNearestPointOnTriangleEdge(v2, v3, point, ref nearest, ref nearestSqrDist);
            bool e31 = CheckNearestPointOnTriangleEdge(v3, v1, point, ref nearest, ref nearestSqrDist);

            // Check each vertex of the triangle to see if they are closer than the current best point.
            // Each vertex only needs to be checked if neither adjacent edge has a valid nearest point.
            // If either adjacent edge does have a valid nearest point,
            // then a closer point than that vertex has already been found.
            if (!e12 && !e31) CheckTrianglePointNearest(v1, point, ref nearest, ref nearestSqrDist);
            if (!e12 && !e23) CheckTrianglePointNearest(v2, point, ref nearest, ref nearestSqrDist);
            if (!e23 && !e31) CheckTrianglePointNearest(v3, point, ref nearest, ref nearestSqrDist);

            return nearest;

            // Helper function to check a single point.
            static void CheckTrianglePointNearest(float4 pos, float4 point, ref float4 nearestOnEdge,
                                                  ref float shortestEdgeSqrDist) {

                float sqrDist = math.distancesq(pos, point);
                if (sqrDist < shortestEdgeSqrDist) {
                    nearestOnEdge = pos;
                    shortestEdgeSqrDist = sqrDist;
                }
            }

            // Helper function to check a single edge of the triangle.
            static bool CheckNearestPointOnTriangleEdge(in float4 v1, in float4 v2, in float4 point,
                                                        ref float4 nearestOnEdge, ref float shortestEdgeSqrDist) {

                if (GetNearestPointOnSegment(v1, v2, point, out float4 edgePos)) {
                    CheckTrianglePointNearest(edgePos, point, ref nearestOnEdge, ref shortestEdgeSqrDist);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Returns true if a given point is on the inner side (defined by a given normal) of a segment.
        /// </summary>
        /// <param name="v1">The start of the segment.</param>
        /// <param name="v2">The end of the segment.</param>
        /// <param name="normal">The normal, defining which side is inside.</param>
        /// <param name="point">The point to search for.</param>
        /// <returns>Whether the point is on the inner side.</returns>
        public static bool IsPointInsideBound(in float4 v1, in float4 v2, in float3 normal, in float4 point) {
            float3 edge = (v2 - v1).xyz;
            float3 cross = math.normalize(math.cross(normal, edge));
            float4 pointOffset = math.normalize(point - v1);

            float dot = math.dot(pointOffset.xyz, cross);
            return dot > -.00001f;
        }

        /// <summary>
        /// Raycast a line segment against a triangle, and return whether they intersect.
        /// </summary>
        /// <param name="v1">The first triangle point.</param>
        /// <param name="v2">The second triangle point.</param>
        /// <param name="v3">The third triangle point.</param>
        /// <param name="s1">The start of the segment.</param>
        /// <param name="s2">The end of the segment.</param>
        /// <param name="t">The point along the input segment where it intersects the triangle, or -1.</param>
        /// <returns>Whether the segment intersects the triangle.</returns>
        public static bool DoesSegmentIntersectTriangle(in float4 v1, in float4 v2, in float4 v3, in float4 s1,
                                                        in float4 s2, out float t) {
            // Implements the Möller–Trumbore intersection algorithm
            // Ported from Wikipedia's C++ implementation:
            // https://en.wikipedia.org/wiki/M%C3%B6ller%E2%80%93Trumbore_intersection_algorithm

            float4 rayVector = s2 - s1;
            float4 rayOrigin = s1;
            t = -1;

            const float epsilon = float.Epsilon;

            float4 edge1 = v2 - v1;
            float4 edge2 = v3 - v1;
            float4 h = new float4(math.cross(rayVector.xyz, edge2.xyz), 0);
            float a = math.dot(edge1, h);
            if (a > -epsilon && a < epsilon) {
                return false; // This ray is parallel to this triangle.
            }

            float f = 1.0f / a;
            float4 s = rayOrigin - v1;
            float u = f * math.dot(s, h);
            if (u < 0.0 || u > 1.0) {
                return false;
            }

            float4 q = new float4(math.cross(s.xyz, edge1.xyz), 0);
            float v = f * math.dot(rayVector, q);
            if (v < 0.0 || u + v > 1.0) {
                return false;
            }

            // At this stage we can compute t to find out where the intersection point is on the line.
            t = f * math.dot(edge2, q);
            return t > 0 && t < 1;
        }

        public static bool GetNearestPointOnLines(NativeRay line1, NativeRay line2, out float t1, out float t2) {
            // https://stackoverflow.com/a/2316934
            // mua = ( d1343 d4321 - d1321 d4343 ) / ( d2121 d4343 - d4321 d4321 )

            float4 p1 = line1.Origin;
            float4 v1 = line1.Direction;
            float4 p2 = line2.Origin;
            float4 v2 = line2.Direction;

            t1 = ((D(p1 - p2, v2) * D(v2, v1)) - (D(p1 - p2, v1) * D(v2, v2))) /
                 ((D(v1, v1) * D(v2, v2)) - (D(v2, v1) * D(v2, v1)));

            t2 = (D(p1 - p2, v2) + t1 * D(v2, v1)) / D(v2, v2);

            return !math.isnan(t1) && !math.isnan(t2) && !math.isinf(t1) && !math.isinf(t2);

            static float D(float4 a, float4 b) => math.dot(a, b);
        }

        /// <summary>
        /// Cast a ray against the blocking triangles of the volume, and return the nearest hit.
        /// </summary>
        /// <param name="start">The position to start the query at in local space of the volume.</param>
        /// <param name="end">The position to end the query at in local space of the volume.</param>
        /// <param name="earlyReturn">If true, will return true as soon as any triangle is hit, not necessarily giving you the closest hit point.</param>
        /// <param name="volume">The volume in which to raycast.</param>
        /// <param name="t">If the query hits a triangle, the ratio between start and end at which the hit occurred.</param>
        /// <returns>Whether a triangle was hit.</returns>
        [BurstCompile]
        public static unsafe bool NavVolumeRaycast(float4 start, float4 end, bool earlyReturn,
                                                   in NativeNavVolumeData volume, out float t) {
            float4 offset = end - start;
            float offsetSq = math.lengthsq(offset);

            t = -1;
            bool didHit = false;

            // Loop through all triangles and perform line check.
            int indexCount = volume.BlockingTriangleIndexCount;
            int triCount = indexCount / 3;

            int3* indexPtr = ((int3*) volume.TriangleIndices.Pointer)!;
            float4* vertexPointer = ((float4*) volume.Vertices.Pointer)!;

            for (int triIndex = 0; triIndex < triCount; triIndex++) {
                // Squeezing out as much performance as possible by reading the pointers directly.
                int3 i = indexPtr[triIndex];

                float4 v1 = vertexPointer[i.x];
                float4 v2 = vertexPointer[i.y];
                float4 v3 = vertexPointer[i.z];

                // Project all points of the triangle on the line axis.
                float d1 = math.dot(v1 - start, offset);
                float d2 = math.dot(v2 - start, offset);
                float d3 = math.dot(v3 - start, offset);

                // If they are all outside the segment on its own axis and in the same direction,
                // there must be no hit and this triangle can be skipped.
                // Overall this additional check improves performance because it is significantly faster than
                // the call to DoesSegmentIntersectTriangle, and filters out the vast majority of triangles in a volume.
                if ((d1 < 0 && d2 < 0 && d3 < 0) || (d1 > offsetSq && d2 > offsetSq && d3 > offsetSq)) continue;

                // Check if the segment intersects the triangle, and if it does,
                // check if it is closer than the current nearest hit.
                if (DoesSegmentIntersectTriangle(v1, v2, v3, start, end, out float tempHit) &&
                    tempHit > 0.01f &&
                    (!didHit || tempHit < t)) {
                    t = tempHit;
                    didHit = true;

                    // Any line that intersects with the triangle means it's not clear.
                    if (earlyReturn) return true;
                }
            }

            return didHit;
        }

        /// <summary>
        /// Check a path through the surface to see if the line is passable.
        /// This is significantly more complex than a volume raycast, because it must consider the surface's walkable
        /// geometry rather than just blocking triangles.
        /// </summary>
        /// <param name="region1">The start region of the path.</param>
        /// <param name="region2">The end region of the path.</param>
        /// <param name="pos1">The start position of the path in local space of the surface.</param>
        /// <param name="pos2">The end position of the path in local space of the surface.</param>
        /// <param name="angleLimitDot">All regions along the path must be within this angle of the start.</param>
        /// <param name="surface">The surface to check.</param>
        /// <returns>True if the path is directly passable, otherwise false.</returns>
        [BurstCompile]
        public static bool NavSurfacePathCheck(int region1, int region2, float4 pos1, float4 pos2, float angleLimitDot,
                                             in NativeNavSurfaceData surface) {

            float4 startUp = surface.Regions[region1].NormalVector;
            float4 endUp = surface.Regions[region2].NormalVector;

            if (math.dot(startUp, endUp) < angleLimitDot) {
                return false;
            }

            float4 pathVector = pos2 - pos1;
            NativeRay pathRay = new(pos1, pathVector);

            int prevRegionId = -1;
            int curRegionId = region1;
            float progress = 0;

            while (curRegionId != region2) {
                NativeNavSurfaceRegionData region = surface.Regions[curRegionId];

                int nextRegion = -1;
                for (int i = 0; i < region.InternalLinkRange.Length; i++) {
                    int linkIndex = region.InternalLinkRange.Start + i;
                    NativeNavSurfaceInternalLinkData link = surface.InternalLinks[linkIndex];

                    if (link.ToRegion == prevRegionId) continue;

                    NativeNavSurfaceRegionData toRegion = surface.Regions[link.ToRegion];
                    if (math.dot(startUp, toRegion.NormalVector) < angleLimitDot) {
                        return false;
                    }

                    bool didHit = false;

                    for (int j = 0; j < link.EdgeRange.Length; j++) {
                        int edgeIndex = link.EdgeRange.Start + j;
                        int2 edge = surface.LinkEdges[edgeIndex];
                        float4 v1 = surface.Vertices[edge.x];
                        float4 v2 = surface.Vertices[edge.y];
                        float4 edgeVector = v2 - v1;
                        NativeRay edgeRay = new(v1, edgeVector);

                        bool intersect = GetNearestPointOnLines(pathRay, edgeRay, out float t1, out float t2);
                        if (!intersect || t1 <= progress || t1 >= 1 || t2 <= 0 || t2 >= 1) continue;

                        progress = t1;
                        didHit = true;
                        break;
                    }

                    if (!didHit) continue;

                    nextRegion = link.ToRegion;
                    break;
                }

                if (nextRegion == -1) return false;
                prevRegionId = curRegionId;
                curRegionId = nextRegion;
            }

            return true;
        }

        /// <summary>
        /// Returns an arbitrary vector that is perpendicular to the given vector.
        /// </summary>
        /// <param name="vector">Input vector.</param>
        /// <returns>A perpendicular vector.</returns>
        public static float4 GetPerpendicularVector(float4 vector) {
            float4 crossRight = new float4(math.cross(vector.xyz, new float3(1, 0, 0)), 0);
            if (math.lengthsq(crossRight) > 0) return math.normalize(crossRight);
            return math.normalize(new float4(math.cross(vector.xyz, new float3(0, 1, 0)), 0));
        }

        /// <summary>
        /// Simple bounds check for a 3D coordinate.
        /// </summary>
        /// <param name="boundSize">The size of the bounds.</param>
        /// <param name="n">The coordinate to check.</param>
        /// <returns>Whether the coordinate is outside the bounds (equal to size is considered outside).</returns>
        public static bool IsOutOfBounds(in int3 boundSize, in int3 n) {
            return n.x < 0 || n.x >= boundSize.x ||
                   n.y < 0 || n.y >= boundSize.y ||
                   n.z < 0 || n.z >= boundSize.z;
        }

        /// <summary>
        /// Get the direction index for the given plane normal.
        /// This returns a value in the range [0, 26]. It assumes the only directions are the six cardinal directions,
        /// and the diagonals combining two or three of those directions.
        /// </summary>
        /// <param name="normal">Plane normal, which does not need to be normalized.</param>
        /// <param name="epsilon">Epsilon for comparing normal components.</param>
        /// <returns>Direction index for given normal.</returns>
        public static int GetDirectionIndex(float3 normal, float epsilon = 0.001f) {
            int x = normal.x > epsilon ? 2 : normal.x < -epsilon ? 0 : 1;
            int y = normal.y > epsilon ? 2 : normal.y < -epsilon ? 0 : 1;
            int z = normal.z > epsilon ? 2 : normal.z < -epsilon ? 0 : 1;

            return x * 9 + y * 3 + z;
        }

        public static void SmallestEnclosingCircle(float3 a, float3 b, float3 c, out float3 center, out float radius) {
            float3 ab = b - a;
            float3 bc = c - b;
            float3 ca = a - c;

            float ab2 = math.lengthsq(ab);
            float bc2 = math.lengthsq(bc);
            float ca2 = math.lengthsq(ca);

            // If triangle is obtuse, the center of the circle is the midpoint of the longest side.
            if (ab2 > bc2 + ca2) {
                center = (a + c) * 0.5f;
                radius = math.sqrt(ab2) * 0.5f;
            } else if (bc2 > ab2 + ca2) {
                center = (b + a) * 0.5f;
                radius = math.sqrt(bc2) * 0.5f;
            } else if (ca2 > ab2 + bc2){
                center = (c + b) * 0.5f;
                radius = math.sqrt(ca2) * 0.5f;
            }

            // Otherwise, the center is the circumcenter of the triangle.
            Circumscribe(a, b, c, out center, out radius);
        }

        public static void Circumscribe(float3 a, float3 b, float3 c, out float3 center, out float radius) {
            // https://gamedev.stackexchange.com/questions/60630/how-do-i-find-the-circumcenter-of-a-triangle-in-3d

            float3 ac = c - a ;
            float3 ab = b - a ;
            float3 abXac = math.cross(ab, ac);

            float3 toCenter = (math.cross(abXac, ab) * math.lengthsq(ac) +
                               math.cross(ac, abXac) * math.lengthsq(ab)) /
                              (2 * math.lengthsq(abXac));
            radius = math.length(toCenter);

            center = a + toCenter;
        }

        private static readonly ProfilerMarker ProfilerMarkerNearPointOnSurfaceRegion =
            new("GetNearestPointOnSurfaceRegion");

        public static float4 GetNearestPointOnSurfaceRegion(in NativeNavSurfaceData surface, int regionIndex,
                                                            float4 point) {

            using ProfilerMarker.AutoScope scope = ProfilerMarkerNearPointOnSurfaceRegion.Auto();

            float4 localPos = math.mul(surface.InverseTransform, point);

            float4 closestPoint = default;
            float closestDistance = float.PositiveInfinity;

            NativeNavSurfaceRegionData region = surface.Regions[regionIndex];

            // Loop through all triangles, and check each one for closest point.
            int triCount = region.TriangleIndexRange.Length / 3;
            for (int triIndex = 0; triIndex < triCount; triIndex++) {
                int triStart = region.TriangleIndexRange.Start + triIndex * 3;

                int v1 = surface.TriangleIndices[triStart + 0];
                int v2 = surface.TriangleIndices[triStart + 1];
                int v3 = surface.TriangleIndices[triStart + 2];

                float4 v1Pos = surface.Vertices[v1];
                float4 v2Pos = surface.Vertices[v2];
                float4 v3Pos = surface.Vertices[v3];

                float4 testPos = GetNearestPointOnTriangleIncludingBounds(v1Pos, v2Pos, v3Pos, localPos);

                float dist2 = math.distancesq(testPos, localPos);
                if (dist2 < closestDistance) {
                    closestPoint = testPos;
                    closestDistance = dist2;
                }
            }

            return math.mul(surface.Transform, closestPoint);
        }

        private static readonly ProfilerMarker ProfilerMarkerNearPointOnVolumeRegion =
            new("GetNearestPointOnVolumeRegion");

        public static float4 GetNearestPointOnVolumeRegion(in NativeNavVolumeData volume, int regionIndex, float4 point) {

            using ProfilerMarker.AutoScope scope = ProfilerMarkerNearPointOnVolumeRegion.Auto();

            float4 localPos = math.mul(volume.InverseTransform, point);

            NativeNavVolumeRegionData region = volume.Regions[regionIndex];

            bool inside = true;
            for (int i = 0; i < region.BoundPlaneRange.Length; i++) {
                NativePlane plane = volume.BoundPlanes[region.BoundPlaneRange.Start + i];
                float dot = math.dot(-plane.Normal, localPos);
                if (dot > plane.Distance) continue;

                inside = false;
                break;
            }

            if (inside) {
                return point;
            }

            float4 closestPoint = default;
            float farDistance = float.PositiveInfinity;
            float closestDistance = farDistance;

            // Loop through all triangles, and check each one for closest point.
            int triCount = region.TriangleIndexRange.Length / 3;

            for (int triIndex = 0; triIndex < triCount; triIndex++) {
                int triStart = region.TriangleIndexRange.Start + triIndex * 3;

                int v1 = volume.TriangleIndices[triStart + 0];
                int v2 = volume.TriangleIndices[triStart + 1];
                int v3 = volume.TriangleIndices[triStart + 2];

                float4 v1Pos = volume.Vertices[v1];
                float4 v2Pos = volume.Vertices[v2];
                float4 v3Pos = volume.Vertices[v3];

                float4 testPos = GetNearestPointOnTriangleIncludingBounds(v1Pos, v2Pos, v3Pos, localPos);

                float dist2 = math.distancesq(testPos, localPos);

                if (dist2 < closestDistance) {
                    closestPoint = testPos;
                    closestDistance = dist2;
                }
            }

            return math.mul(volume.Transform, closestPoint);
        }
    }
}
