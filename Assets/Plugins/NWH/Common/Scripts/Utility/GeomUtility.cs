// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

#endregion

namespace NWH.Common.Utility
{
    /// <summary>
    /// Collection of geometric utility functions for 3D math operations.
    /// Includes vector math, mesh calculations, triangle operations, and spatial queries.
    /// </summary>
    public static class GeomUtility
    {
        /// <summary>
        /// Checks if two Vector3 values are approximately equal within a threshold.
        /// Uses squared magnitude for performance.
        /// </summary>
        /// <param name="a">First vector.</param>
        /// <param name="b">Second vector.</param>
        /// <param name="threshold">Maximum squared distance to consider equal.</param>
        /// <returns>True if vectors are within threshold distance.</returns>
        public static bool NearEqual(this Vector3 a, Vector3 b, float threshold = 0.01f)
        {
            return Vector3.SqrMagnitude(a - b) < threshold;
        }


        /// <summary>
        /// Checks if two Quaternion values are approximately equal.
        /// </summary>
        /// <param name="a">First quaternion.</param>
        /// <param name="b">Second quaternion.</param>
        /// <returns>True if angle between quaternions is less than 0.1 degrees.</returns>
        public static bool Equal(this Quaternion a, Quaternion b)
        {
            return Mathf.Abs(Quaternion.Angle(a, b)) < 0.1f;
        }


        /// <summary>
        /// Clamps the magnitude of a vector between minimum and maximum values.
        /// </summary>
        /// <param name="v">Vector to clamp.</param>
        /// <param name="min">Minimum magnitude.</param>
        /// <param name="max">Maximum magnitude.</param>
        /// <returns>Vector with clamped magnitude.</returns>
        public static Vector3 ClampMagnitude(this Vector3 v, float min, float max)
        {
            float mag = v.magnitude;
            return Mathf.Clamp(mag, min, max) / mag * v;
        }


        /// <summary>
        /// Calculates a perpendicular vector to the given vector.
        /// </summary>
        /// <param name="v">Input vector.</param>
        /// <returns>Perpendicular vector.</returns>
        public static Vector3 Perpendicular(this Vector3 v)
        {
            return new Vector3(CopySign(v.z, v.x), CopySign(v.z, v.y), -CopySign(Mathf.Abs(v.x) + Mathf.Abs(v.y), v.z));
        }


        /// <summary>
        /// Copies the sign from one float to the magnitude of another.
        /// </summary>
        /// <param name="mag">Magnitude value.</param>
        /// <param name="sgn">Sign donor value.</param>
        /// <returns>Magnitude with the sign of sgn.</returns>
        public static float CopySign(float mag, float sgn)
        {
            ref uint magI   = ref UnsafeUtility.As<float, uint>(ref mag);
            ref uint sgnI   = ref UnsafeUtility.As<float, uint>(ref sgn);
            uint     result = (magI & ~(1u << 31)) | (sgnI & (1u << 31));
            return UnsafeUtility.As<uint, float>(ref result);
        }


        /// <summary>
        /// Returns a vector with only the largest component preserved (rounded to 1 or -1), others set to 0.
        /// </summary>
        /// <param name="v">Input vector.</param>
        /// <returns>Vector with dominant axis isolated.</returns>
        public static Vector3 RoundedMax(this Vector3 v)
        {
            int   maxIndex = -1;
            float maxValue = -Mathf.Infinity;
            for (int i = 0; i < 3; i++)
            {
                float value = Mathf.Abs(v[i]);
                if (value > maxValue)
                {
                    maxValue = value;
                    maxIndex = i;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                v[i] = i == maxIndex ? Mathf.Sign(v[i]) * 1f : 0f;
            }

            return v;
        }


        /// <summary>
        /// Finds the nearest point on an infinite line to a given point.
        /// </summary>
        /// <param name="linePnt">Point on the line.</param>
        /// <param name="lineDir">Direction of the line.</param>
        /// <param name="pnt">Point to find nearest point from.</param>
        /// <returns>Nearest point on the line.</returns>
        public static Vector3 NearestPointOnLine(Vector3 linePnt, Vector3 lineDir, Vector3 pnt)
        {
            lineDir.Normalize(); //this needs to be a unit vector
            Vector3 v = pnt - linePnt;
            float   d = Vector3.Dot(v, lineDir);
            return linePnt + lineDir * d;
        }


        /// <summary>
        /// Calculates the distance between a point and a line segment.
        /// </summary>
        /// <param name="pt">Point to measure from.</param>
        /// <param name="p1">First endpoint of segment.</param>
        /// <param name="p2">Second endpoint of segment.</param>
        /// <returns>Distance to the segment.</returns>
        public static float FindDistanceToSegment(Vector3 pt, Vector3 p1, Vector3 p2)
        {
            float dx = p2.x - p1.x;
            float dy = p2.y - p1.y;
            if (dx == 0 && dy == 0)
            {
                // It's a point not a line segment.
                dx = pt.x - p1.x;
                dy = pt.y - p1.y;
                return Mathf.Sqrt(dx * dx + dy * dy);
            }

            // Calculate the t that minimizes the distance.
            float t = ((pt.x - p1.x) * dx + (pt.y - p1.y) * dy) /
                      (dx * dx + dy * dy);

            // See if this represents one of the segment's
            // end points or a point in the middle.
            if (t < 0)
            {
                dx = pt.x - p1.x;
                dy = pt.y - p1.y;
            }
            else if (t > 1)
            {
                dx = pt.x - p2.x;
                dy = pt.y - p2.y;
            }
            else
            {
                Vector3 closest = new(p1.x + t * dx, p1.y + t * dy);
                dx = pt.x - closest.x;
                dy = pt.y - closest.y;
            }

            return Mathf.Sqrt(dx * dx + dy * dy);
        }


        /// <summary>
        /// Calculates squared distance between two points. Faster than regular distance.
        /// </summary>
        /// <param name="a">First point.</param>
        /// <param name="b">Second point.</param>
        /// <returns>Squared distance.</returns>
        public static float SquareDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float y = a.y - b.y;
            float z = a.z - b.z;
            return x * x + y * y + z * z;
        }


        /// <summary>
        /// Finds the intersection point between a line and a plane.
        /// </summary>
        /// <param name="planePoint">Point on the plane.</param>
        /// <param name="planeNormal">Normal vector of the plane.</param>
        /// <param name="linePoint">Point on the line.</param>
        /// <param name="lineDirection">Direction of the line.</param>
        /// <returns>Intersection point, or Vector3.zero if parallel.</returns>
        public static Vector3 LinePlaneIntersection(Vector3 planePoint, Vector3 planeNormal, Vector3 linePoint,
            Vector3                                         lineDirection)
        {
            if (Vector3.Dot(planeNormal, lineDirection.normalized) == 0)
            {
                return Vector3.zero;
            }

            float t = (Vector3.Dot(planeNormal, planePoint) - Vector3.Dot(planeNormal, linePoint)) /
                      Vector3.Dot(planeNormal, lineDirection.normalized);
            return linePoint + lineDirection.normalized * t;
        }


        /// <summary>
        /// Finds a point along the chord line of a quad at the specified percentage.
        /// </summary>
        /// <param name="a">First corner.</param>
        /// <param name="b">Second corner.</param>
        /// <param name="c">Third corner.</param>
        /// <param name="d">Fourth corner.</param>
        /// <param name="chordPercent">Position along chord (0-1).</param>
        /// <returns>Point on chord line.</returns>
        public static Vector3 FindChordLine(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float chordPercent)
        {
            return QuadLerp(a, b, c, d, 0.5f, chordPercent);
        }


        /// <summary>
        /// Finds a point along the span line of a quad at the specified percentage.
        /// </summary>
        /// <param name="a">First corner.</param>
        /// <param name="b">Second corner.</param>
        /// <param name="c">Third corner.</param>
        /// <param name="d">Fourth corner.</param>
        /// <param name="spanPercent">Position along span (0-1).</param>
        /// <returns>Point on span line.</returns>
        public static Vector3 FindSpanLine(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float spanPercent)
        {
            return QuadLerp(a, b, c, d, spanPercent, 0.5f);
        }


        /// <summary>
        /// Calculates the area of a quadrilateral defined by four points.
        /// </summary>
        /// <param name="A">First corner.</param>
        /// <param name="B">Second corner.</param>
        /// <param name="C">Third corner.</param>
        /// <param name="D">Fourth corner.</param>
        /// <returns>Area of the quad.</returns>
        public static float FindArea(Vector3 A, Vector3 B, Vector3 C, Vector3 D)
        {
            return TriArea(A, B, D) + TriArea(B, C, D);
        }


        /// <summary>
        /// Finds the center point of a quad or triangle defined by 3 or 4 points.
        /// </summary>
        /// <param name="a">First corner.</param>
        /// <param name="b">Second corner.</param>
        /// <param name="c">Third corner.</param>
        /// <param name="d">Fourth corner (can equal first corner for triangle).</param>
        /// <returns>Center point.</returns>
        public static Vector3 FindCenter(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            if (a == d)
            {
                return (a + b + c) / 4f;
            }

            return (a + b + c + d) / 4f;
        }


        /// <summary>
        /// Calculates the distance between two points projected along a normal vector.
        /// </summary>
        /// <param name="a">First point.</param>
        /// <param name="b">Second point.</param>
        /// <param name="normal">Normal vector to project along.</param>
        /// <returns>Distance along normal.</returns>
        public static float DistanceAlongNormal(Vector3 a, Vector3 b, Vector3 normal)
        {
            Vector3 dir = b - a;
            return Vector3.Project(dir, normal).magnitude;
        }


        /// <summary>
        /// Checks if a point lies inside a triangle.
        /// </summary>
        /// <param name="A">First triangle vertex.</param>
        /// <param name="B">Second triangle vertex.</param>
        /// <param name="C">Third triangle vertex.</param>
        /// <param name="P">Point to test.</param>
        /// <param name="dotThreshold">Tolerance for point-on-plane test.</param>
        /// <returns>True if point is inside triangle.</returns>
        public static bool PointInTriangle(Vector3 A, Vector3 B, Vector3 C, Vector3 P, float dotThreshold = 0.001f)
        {
            if (SameSide(P, A, B, C) && SameSide(P, B, A, C) && SameSide(P, C, A, B))
            {
                Vector3 vc1 = Vector3.Cross(B - A, C - A).normalized;
                if (Mathf.Abs(Vector3.Dot(P - A, vc1)) <= dotThreshold)
                {
                    return true;
                }
            }

            return false;
        }


        private static bool SameSide(Vector3 p1, Vector3 p2, Vector3 A, Vector3 B)
        {
            Vector3 cp1 = Vector3.Cross(B - A, p1 - A).normalized;
            Vector3 cp2 = Vector3.Cross(B - A, p2 - A).normalized;
            if (Vector3.Dot(cp1, cp2) > 0)
            {
                return true;
            }

            return false;
        }


        /// <summary>
        /// Checks if a 2D point is inside the screen rectangle.
        /// </summary>
        /// <param name="point">Point to check.</param>
        /// <returns>True if point is inside screen bounds.</returns>
        public static bool PointIsInsideRect(Vector2 point)
        {
            return new Rect(0, 0, Screen.width, Screen.height).Contains(point);
        }


        /// <summary>
        /// Checks if two float values are nearly equal within an epsilon threshold.
        /// </summary>
        /// <param name="a">First value.</param>
        /// <param name="b">Second value.</param>
        /// <param name="epsilon">Maximum difference to consider equal.</param>
        /// <returns>True if values are within epsilon.</returns>
        public static bool NearlyEqual(this float a, float b, double epsilon)
        {
            return Mathf.Abs(a - b) < epsilon;
        }


        /// <summary>
        /// Calculates the area of a triangle from three points.
        /// </summary>
        /// <param name="p1">First point.</param>
        /// <param name="p2">Second point.</param>
        /// <param name="p3">Third point.</param>
        /// <returns>Area of the triangle.</returns>
        public static float AreaFromThreePoints(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Vector3 u, v;

            u.x = p2.x - p1.x;
            u.y = p2.y - p1.y;
            u.z = p2.z - p1.z;

            v.x = p3.x - p1.x;
            v.y = p3.y - p1.y;
            v.z = p3.z - p1.z;

            Vector3 crossUV = Vector3.Cross(u, v);
            return Mathf.Sqrt(crossUV.x * crossUV.x + crossUV.y * crossUV.y + crossUV.z * crossUV.z) * 0.5f;
        }


        /// <summary>
        /// Calculates the area of a quadrilateral from four points.
        /// </summary>
        /// <param name="p1">First point.</param>
        /// <param name="p2">Second point.</param>
        /// <param name="p3">Third point.</param>
        /// <param name="p4">Fourth point.</param>
        /// <returns>Area of the quadrilateral.</returns>
        public static float AreaFromFourPoints(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
        {
            return AreaFromThreePoints(p1, p2, p4) + AreaFromThreePoints(p2, p3, p4);
        }


        /// <summary>
        /// Calculates area of a single triangle from it's three points.
        /// </summary>
        public static float TriArea(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Vector3 u, v, crossUV;

            u.x = p2.x - p1.x;
            u.y = p2.y - p1.y;
            u.z = p2.z - p1.z;

            v.x = p3.x - p1.x;
            v.y = p3.y - p1.y;
            v.z = p3.z - p1.z;

            crossUV = Vector3.Cross(u, v);
            return Mathf.Sqrt(crossUV.x * crossUV.x + crossUV.y * crossUV.y + crossUV.z * crossUV.z) * 0.5f;
        }


        /// <summary>
        /// Calculates area of a complete mesh.
        /// </summary>
        public static float MeshArea(Mesh mesh)
        {
            if (mesh.vertices.Length == 0)
            {
                return 0;
            }

            float area = 0;

            Vector3[] verts = mesh.vertices;
            int[]     tris  = mesh.triangles;

            for (int i = 0; i < tris.Length; i += 3)
            {
                area += TriArea(verts[tris[i]], verts[tris[i + 1]], verts[tris[i + 2]]);
            }

            return area;
        }


        /// <summary>
        /// Calculates area of a mesh as viewed from the direction vector.
        /// </summary>
        public static float ProjectedMeshArea(Mesh mesh, Vector3 direction)
        {
            float area = 0;

            Vector3[] verts   = mesh.vertices;
            int[]     tris    = mesh.triangles;
            Vector3[] normals = mesh.normals;

            int count = 0;
            for (int i = 0; i < tris.Length; i += 3)
            {
                area += TriArea(verts[tris[i]], verts[tris[i + 1]], verts[tris[i + 2]], direction);
                count++;
            }

            return area;
        }


        /// <summary>
        /// Calculates the area of a rectangle from four corner points.
        /// </summary>
        /// <param name="p1">First corner.</param>
        /// <param name="p2">Second corner.</param>
        /// <param name="p3">Third corner.</param>
        /// <param name="p4">Fourth corner.</param>
        /// <returns>Area of the rectangle.</returns>
        public static float RectArea(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
        {
            return TriArea(p1, p2, p4) + TriArea(p2, p3, p4);
        }


        /// <summary>
        /// Find mesh center by averaging. Returns local center.
        /// </summary>
        public static Vector3 FindMeshCenter(Mesh mesh)
        {
            if (mesh.vertices.Length == 0)
            {
                return Vector3.zero;
            }

            Vector3 sum   = Vector3.zero;
            int     count = 0;
            if (mesh != null)
            {
                foreach (Vector3 vert in mesh.vertices)
                {
                    sum += vert;
                    count++;
                }
            }

            return sum / count;
        }


        public static float TriArea(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 view)
        {
            Vector3 u, v, crossUV, normal;
            float   crossMagnitude;

            u.x = p2.x - p1.x;
            u.y = p2.y - p1.y;
            u.z = p2.z - p1.z;

            v.x = p3.x - p1.x;
            v.y = p3.y - p1.y;
            v.z = p3.z - p1.z;

            crossUV        = Vector3.Cross(u, v);
            crossMagnitude = Mathf.Sqrt(crossUV.x * crossUV.x + crossUV.y * crossUV.y + crossUV.z * crossUV.z);

            // Normal
            if (crossMagnitude == 0)
            {
                normal.x = normal.y = normal.z = 0f;
            }
            else
            {
                normal.x = crossUV.x / crossMagnitude;
                normal.y = crossUV.y / crossMagnitude;
                normal.z = crossUV.z / crossMagnitude;
            }

            float angle = Vector3.Angle(normal, view);
            float cos   = Mathf.Cos(angle);

            if (cos < 0)
            {
                return 0;
            }

            return Mathf.Sqrt(crossUV.x * crossUV.x + crossUV.y * crossUV.y + crossUV.z * crossUV.z) * 0.5f * cos;
        }


        /// <summary>
        /// Calculates the signed volume contribution of a triangle relative to the origin.
        /// </summary>
        /// <param name="p1">First vertex.</param>
        /// <param name="p2">Second vertex.</param>
        /// <param name="p3">Third vertex.</param>
        /// <returns>Signed volume.</returns>
        public static float SignedVolumeOfTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float v321 = p3.x * p2.y * p1.z;
            float v231 = p2.x * p3.y * p1.z;
            float v312 = p3.x * p1.y * p2.z;
            float v132 = p1.x * p3.y * p2.z;
            float v213 = p2.x * p1.y * p3.z;
            float v123 = p1.x * p2.y * p3.z;
            return 1.0f / 6.0f * (-v321 + v231 + v312 - v132 - v213 + v123);
        }


        /// <summary>
        /// Calculates the volume enclosed by a mesh.
        /// </summary>
        /// <param name="mesh">Mesh to calculate volume for.</param>
        /// <returns>Volume of the mesh.</returns>
        public static float VolumeOfMesh(Mesh mesh)
        {
            float     volume    = 0;
            Vector3[] vertices  = mesh.vertices;
            int[]     triangles = mesh.triangles;
            for (int i = 0; i < mesh.triangles.Length; i += 3)
            {
                Vector3 p1 = vertices[triangles[i + 0]];
                Vector3 p2 = vertices[triangles[i + 1]];
                Vector3 p3 = vertices[triangles[i + 2]];
                volume += SignedVolumeOfTriangle(p1, p2, p3);
            }

            return Mathf.Abs(volume);
        }


        /// <summary>
        /// Transforms a point from local to world space without applying scale.
        /// </summary>
        /// <param name="transform">Transform to use.</param>
        /// <param name="position">Local position.</param>
        /// <returns>World position without scale.</returns>
        public static Vector3 TransformPointUnscaled(this Transform transform, Vector3 position)
        {
            return Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one).MultiplyPoint3x4(position);
        }


        /// <summary>
        /// Transforms a point from world to local space without applying scale.
        /// </summary>
        /// <param name="transform">Transform to use.</param>
        /// <param name="position">World position.</param>
        /// <returns>Local position without scale.</returns>
        public static Vector3 InverseTransformPointUnscaled(this Transform transform, Vector3 position)
        {
            return Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one).inverse
                            .MultiplyPoint3x4(position);
        }


        /// <summary>
        /// Changes the layer of a transform and all its children recursively.
        /// </summary>
        /// <param name="trans">Root transform.</param>
        /// <param name="name">Layer name.</param>
        public static void ChangeLayersRecursively(this Transform trans, string name)
        {
            trans.gameObject.layer = LayerMask.NameToLayer(name);
            foreach (Transform child in trans)
            {
                child.ChangeLayersRecursively(name);
            }
        }


        /// <summary>
        /// Changes the color of a GameObject's material.
        /// </summary>
        /// <param name="gameObject">GameObject to modify.</param>
        /// <param name="color">New color.</param>
        public static void ChangeObjectColor(GameObject gameObject, Color color)
        {
            gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", color);
        }


        /// <summary>
        /// Changes the alpha value of a GameObject's material color.
        /// </summary>
        /// <param name="gameObject">GameObject to modify.</param>
        /// <param name="alpha">New alpha value (0-1).</param>
        public static void ChangeObjectAlpha(GameObject gameObject, float alpha)
        {
            MeshRenderer mr           = gameObject.GetComponent<MeshRenderer>();
            Color        currentColor = mr.material.GetColor("_Color");
            currentColor.a = alpha;
            mr.material.SetColor("_Color", currentColor);
        }


        /// <summary>
        /// Returns a vector with absolute values of all components.
        /// </summary>
        /// <param name="v">Input vector.</param>
        /// <returns>Vector with absolute values.</returns>
        public static Vector3 Vector3Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }


        /// <summary>
        /// Rounds all components of a vector to nearest integer.
        /// </summary>
        /// <param name="v">Input vector.</param>
        /// <returns>Rounded vector.</returns>
        public static Vector3 Vector3RoundToInt(Vector3 v)
        {
            return new Vector3(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
        }


        /// <summary>
        /// Returns a vector with reciprocal values (1/x, 1/y, 1/z).
        /// </summary>
        /// <param name="v">Input vector.</param>
        /// <returns>Vector with reciprocal values.</returns>
        public static Vector3 Vector3OneOver(Vector3 v)
        {
            return new Vector3(1f / v.x, 1f / v.y, 1f / v.z);
        }


        /// <summary>
        /// Rounds a value to the nearest multiple of step.
        /// </summary>
        /// <param name="value">Value to round.</param>
        /// <param name="step">Step size.</param>
        /// <returns>Rounded value.</returns>
        public static float RoundToStep(float value, float step)
        {
            return Mathf.Round(value / step) * step;
        }


        /// <summary>
        /// Rounds a value to the nearest multiple of step.
        /// </summary>
        /// <param name="value">Value to round.</param>
        /// <param name="step">Step size.</param>
        /// <returns>Rounded value.</returns>
        public static float RoundToStep(int value, int step)
        {
            return Mathf.RoundToInt(Mathf.Round(value / step) * step);
        }


        /// <summary>
        /// Rotates a point around a pivot by the specified angles.
        /// </summary>
        /// <param name="point">Point to rotate.</param>
        /// <param name="pivot">Pivot point.</param>
        /// <param name="angles">Euler angles for rotation.</param>
        /// <returns>Rotated point.</returns>
        public static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 angles)
        {
            return Quaternion.Euler(angles) * (point - pivot) + pivot;
        }


        /// <summary>
        /// Performs bilinear interpolation on a quad defined by four points.
        /// </summary>
        /// <param name="a">First corner.</param>
        /// <param name="b">Second corner.</param>
        /// <param name="c">Third corner.</param>
        /// <param name="d">Fourth corner.</param>
        /// <param name="u">U parameter (0-1).</param>
        /// <param name="v">V parameter (0-1).</param>
        /// <returns>Interpolated point.</returns>
        public static Vector3 QuadLerp(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u, float v)
        {
            Vector3 abu = Vector3Lerp(a, b, u);
            Vector3 dcu = Vector3Lerp(d, c, u);
            return Vector3Lerp(abu, dcu, v);
        }


        /// <summary>
        /// Linear interpolation between two vectors with value clamping.
        /// </summary>
        /// <param name="v1">Start vector.</param>
        /// <param name="v2">End vector.</param>
        /// <param name="value">Interpolation value (0-1).</param>
        /// <returns>Interpolated vector.</returns>
        public static Vector3 Vector3Lerp(Vector3 v1, Vector3 v2, float value)
        {
            if (value > 1.0f)
            {
                return v2;
            }

            if (value < 0.0f)
            {
                return v1;
            }

            return new Vector3(v1.x + (v2.x - v1.x) * value,
                               v1.y + (v2.y - v1.y) * value,
                               v1.z + (v2.z - v1.z) * value);
        }


        /// <summary>
        /// Calculates the magnitude of a quaternion.
        /// </summary>
        /// <param name="q">Quaternion.</param>
        /// <returns>Magnitude.</returns>
        public static float QuaternionMagnitude(Quaternion q)
        {
            return Mathf.Sqrt(q.w * q.w + q.x * q.x + q.y * q.y + q.z * q.z);
        }
    }
}