// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Baking.Surface {
    [BurstCompile]
    public struct TriangulateSurfaceJob : IJob {
        [ReadOnly]
        public Fast3DArray Regions;

        [WriteOnly] public NativeList<int4> Vertices;

        [WriteOnly] public NativeList<float4> Normals;

        public int Value;
        public int AlternateValue;

        public void Execute() {
            for (int x = -1; x < Regions.SizeX; x++) {
                for (int y = -1; y < Regions.SizeY; y++) {
                    for (int z = -1; z < Regions.SizeZ; z++) {
                        int4 currentV = new(2 * x + 1, 2 * y + 1, 2 * z + 1, 0);

                        // Find index in marching cubes tables.
                        byte caseIndex;
                        if (Value == AlternateValue) {
                            caseIndex = MarchingCubes.GetMarchingCubesIndex(Regions, Value, x, y, z);
                        } else {
                            caseIndex = MarchingCubes.GetMarchingCubesIndex(Regions, Value, AlternateValue, x, y, z);
                        }

                        // Use Marching Cubes tables to determine edge indices to connect.
                        TriTableEntry edgeIndices = MarchingCubesTables.TriTable[caseIndex];
                        int triCount = edgeIndices.Length / 3;

                        // Add all triangles to mesh.
                        for (int triIndex = 0; triIndex < triCount; triIndex++) {
                            int triStart = triIndex * 3;

                            int crossPoint1 = -1;
                            int crossPoint2 = -1;
                            int nonCrossPoint = -1;

                            // Loop through edges of triangle to create.
                            // If an edge passes through the center, split it and create two triangles.
                            // This avoids most cases where opposing quads from bordering regions have the opposite
                            // triangulation, and thus have no shared faces.
                            // for (int i = 0; i < 3; i++) {
                            //     int n = (i + 1) % 3;
                            //     int o = (i + 2) % 3;
                            //
                            //     if (MarchingCubesTables.AcrossCenterEdges[edgeIndices[triStart + i]] !=
                            //         edgeIndices[triStart + n])
                            //         continue;
                            //
                            //     crossPoint1 = triStart + i;
                            //     crossPoint2 = triStart + n;
                            //     nonCrossPoint = triStart + o;
                            //     break;
                            // }

                            //crossPoint1 = -1;
                            if (crossPoint1 == -1) {
                                // Get vertices based on the edge index.
                                int4 vertex1 = currentV + MarchingCubes.GetMarchingCubesVertex(
                                    edgeIndices[triStart + 0], caseIndex);
                                int4 vertex2 = currentV + MarchingCubes.GetMarchingCubesVertex(
                                    edgeIndices[triStart + 1], caseIndex);
                                int4 vertex3 = currentV + MarchingCubes.GetMarchingCubesVertex(
                                    edgeIndices[triStart + 2], caseIndex);

                                int4 normalAlignedVector = GetNormalAlignedVector(vertex1, vertex2, vertex3);

                                // Add vertices to the mesh.
                                AddTriangle(vertex1, vertex2, vertex3, normalAlignedVector);
                            } else {
                                int4 vertexCross1 = currentV + MarchingCubes.GetMarchingCubesVertex(
                                    edgeIndices[crossPoint1], caseIndex);
                                int4 vertexCross2 = currentV + MarchingCubes.GetMarchingCubesVertex(
                                    edgeIndices[crossPoint2], caseIndex);
                                int4 vertexNonCross = currentV + MarchingCubes.GetMarchingCubesVertex(
                                    edgeIndices[nonCrossPoint], caseIndex);

                                int4 vertexCenter = currentV + new int4(1, 1, 1, 0);

                                int4 normalAlignedVector =
                                    GetNormalAlignedVector(vertexCross1, vertexCross2, vertexNonCross);

                                AddTriangle(vertexCross1, vertexCenter, vertexNonCross, normalAlignedVector);
                                AddTriangle(vertexCenter, vertexCross2, vertexNonCross, normalAlignedVector);
                            }
                        }
                    }
                }
            }
        }

        private int4 GetNormalAlignedVector(int4 v1, int4 v2, int4 v3) {
            return MarchingCubes.GetNormalAlignedVector(v1) +
                   MarchingCubes.GetNormalAlignedVector(v2) +
                   MarchingCubes.GetNormalAlignedVector(v3);
        }

        private void AddTriangle(int4 vertex1, int4 vertex2, int4 vertex3, int4 normalAlignedVector) {
            // Calculate normal.
            int4 edge1 = vertex2 - vertex1;
            int4 edge2 = vertex3 - vertex1;
            float3 normal = math.normalize(math.cross(edge1.xyz, edge2.xyz));

            if (math.dot(normal, normalAlignedVector.xyz) < 0) {
                normal = -normal;
                (vertex1, vertex2) = (vertex2, vertex1);
            }

            // Add vertices to the mesh.
            Vertices.Add(vertex1);
            Vertices.Add(vertex2);
            Vertices.Add(vertex3);

            // Add normal to the mesh.
            Normals.Add(new float4(normal, 0));
        }
    }
}
