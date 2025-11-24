// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using UnityEngine;

#endregion

namespace NWH.DWP2
{
    /// <summary>
    /// Utility class for geometric calculations used in buoyancy simulation.
    /// Provides methods for calculating centroids, areas, and surface-weighted centers of triangles and quadrilaterals.
    /// </summary>
    public static class GeometryUtils
    {
        /// <summary>
        /// Calculates the centroid of a quadrilateral defined by four points.
        /// </summary>
        /// <param name="pointA">First corner point.</param>
        /// <param name="pointB">Second corner point.</param>
        /// <param name="pointC">Third corner point.</param>
        /// <param name="pointD">Fourth corner point.</param>
        /// <returns>The centroid position of the quadrilateral.</returns>
        public static Vector3 CalculateCentroid(Vector3 pointA, Vector3 pointB, Vector3 pointC, Vector3 pointD)
        {
            Vector3 centroid = (pointA + pointB + pointC + pointD) / 4;
            return centroid;
        }


        /// <summary>
        /// Calculates the surface area of a quadrilateral by dividing it into two triangles.
        /// </summary>
        /// <param name="pointA">First corner point.</param>
        /// <param name="pointB">Second corner point.</param>
        /// <param name="pointC">Third corner point.</param>
        /// <param name="pointD">Fourth corner point.</param>
        /// <returns>Total surface area of the quadrilateral.</returns>
        public static float CalculateQuadrilateralArea(Vector3 pointA, Vector3 pointB, Vector3 pointC, Vector3 pointD)
        {
            // Calculate the area of the first triangle (pointA, pointB, pointC)
            float areaTriangle1 = CalculateTriangleArea(pointA, pointB, pointC);

            // Calculate the area of the second triangle (pointA, pointC, pointD)
            float areaTriangle2 = CalculateTriangleArea(pointA, pointC, pointD);

            // Return the sum of both triangle areas
            return areaTriangle1 + areaTriangle2;
        }


        /// <summary>
        /// Calculates the surface area of a triangle using the cross product method.
        /// </summary>
        /// <param name="pointA">First vertex of the triangle.</param>
        /// <param name="pointB">Second vertex of the triangle.</param>
        /// <param name="pointC">Third vertex of the triangle.</param>
        /// <returns>Surface area of the triangle.</returns>
        public static float CalculateTriangleArea(Vector3 pointA, Vector3 pointB, Vector3 pointC)
        {
            Vector3 vectorAB = pointB - pointA;
            Vector3 vectorAC = pointC - pointA;

            // Calculate the cross product of vectorAB and vectorAC
            Vector3 crossProduct = Vector3.Cross(vectorAB, vectorAC);

            // Calculate the magnitude of the cross product and divide by 2 to get the area
            return crossProduct.magnitude / 2;
        }


        /// <summary>
        /// Calculates the surface-weighted center of a quadrilateral.
        /// The center is weighted by the area of the two triangles that make up the quadrilateral.
        /// </summary>
        /// <param name="pointA">First corner point.</param>
        /// <param name="pointB">Second corner point.</param>
        /// <param name="pointC">Third corner point.</param>
        /// <param name="pointD">Fourth corner point.</param>
        /// <returns>The surface-weighted center position.</returns>
        public static Vector3 CalculateSurfaceWeightedCenter(Vector3 pointA, Vector3 pointB, Vector3 pointC,
            Vector3                                                  pointD)
        {
            // Calculate the area and centroid of the first triangle (pointA, pointB, pointC)
            float   areaTriangle1     = CalculateTriangleArea(pointA, pointB, pointC);
            Vector3 centroidTriangle1 = CalculateCentroid(pointA, pointB, pointC);

            // Calculate the area and centroid of the second triangle (pointA, pointC, pointD)
            float   areaTriangle2     = CalculateTriangleArea(pointA, pointC, pointD);
            Vector3 centroidTriangle2 = CalculateCentroid(pointA, pointC, pointD);

            // Calculate the surface-weighted center
            float totalArea = areaTriangle1 + areaTriangle2;
            Vector3 surfaceWeightedCenter =
                (centroidTriangle1 * areaTriangle1 + centroidTriangle2 * areaTriangle2) / totalArea;

            return surfaceWeightedCenter;
        }


        private static Vector3 CalculateCentroid(Vector3 pointA, Vector3 pointB, Vector3 pointC)
        {
            return (pointA + pointB + pointC) / 3;
        }
    }
}