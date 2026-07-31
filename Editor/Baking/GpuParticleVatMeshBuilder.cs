using UnityEngine;

namespace GpuParticle.Editor.Baking
{
    public static class GpuParticleVatMeshBuilder
    {
        private static readonly int[] QuadIndices = { 0, 1, 2, 2, 3, 0 };

        public static Mesh Build(int maxParticles)
        {
            int vertexCount = maxParticles * 6;
            var vertices = new Vector3[vertexCount];
            var uvs0 = new Vector2[vertexCount];
            var uvs1 = new Vector2[vertexCount];
            var triangles = new int[maxParticles * 6];

            Vector2[] quadUv =
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(0, 0),
            };

            for (int p = 0; p < maxParticles; p++)
            {
                int baseVertex = p * 6;
                int baseTri = p * 6;

                for (int v = 0; v < 6; v++)
                {
                    vertices[baseVertex + v] = Vector3.zero;
                    uvs0[baseVertex + v] = quadUv[v];
                    uvs1[baseVertex + v] = new Vector2(p, 0f);
                    triangles[baseTri + v] = baseVertex + QuadIndices[v];
                }
            }

            var mesh = new Mesh
            {
                vertices = vertices,
                uv = uvs0,
                uv2 = uvs1,
                triangles = triangles,
                indexFormat = vertexCount > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh BuildFromSource(Mesh source, int maxParticles)
        {
            if (source == null || maxParticles <= 0)
            {
                return Build(maxParticles);
            }

            int sourceVertexCount = source.vertexCount;
            if (sourceVertexCount == 0)
            {
                return Build(maxParticles);
            }

            int subMeshIndex = 0;
            int sourceTriangleCount = source.subMeshCount > 0 ? (int)source.GetIndexCount(subMeshIndex) : 0;
            if (sourceTriangleCount == 0)
            {
                return Build(maxParticles);
            }

            Vector3[] sourceVertices = source.vertices;
            Vector3[] sourceNormals = source.normals;
            Vector2[] sourceUv = source.uv;
            int[] sourceTriangles = source.GetTriangles(subMeshIndex);

            int verticesPerParticle = sourceVertexCount;
            int trianglesPerParticle = sourceTriangles.Length;
            long totalVertexCount = (long)verticesPerParticle * maxParticles;
            long totalTriangleCount = (long)trianglesPerParticle * maxParticles;

            if (totalVertexCount > int.MaxValue || totalTriangleCount > int.MaxValue)
            {
                Debug.LogWarning($"[GpuParticle] Source mesh duplicated {maxParticles} times exceeds index limits; falling back to billboard quad.");
                return Build(maxParticles);
            }

            int vertexCount = (int)totalVertexCount;
            int triangleCount = (int)totalTriangleCount;

            var vertices = new Vector3[vertexCount];
            var normals = sourceNormals != null && sourceNormals.Length == sourceVertexCount ? new Vector3[vertexCount] : null;
            var uvs0 = sourceUv != null && sourceUv.Length == sourceVertexCount ? new Vector2[vertexCount] : null;
            var uvs1 = new Vector2[vertexCount];
            var triangles = new int[triangleCount];

            for (int p = 0; p < maxParticles; p++)
            {
                int baseVertex = p * verticesPerParticle;
                int baseTriangle = p * trianglesPerParticle;

                for (int v = 0; v < verticesPerParticle; v++)
                {
                    int dst = baseVertex + v;
                    vertices[dst] = sourceVertices[v];
                    if (normals != null)
                    {
                        normals[dst] = sourceNormals![v];
                    }

                    if (uvs0 != null)
                    {
                        uvs0[dst] = sourceUv![v];
                    }

                    uvs1[dst] = new Vector2(p, 0f);
                }

                for (int t = 0; t < trianglesPerParticle; t++)
                {
                    triangles[baseTriangle + t] = baseVertex + sourceTriangles[t];
                }
            }

            var mesh = new Mesh
            {
                vertices = vertices,
                uv = uvs0,
                uv2 = uvs1,
                triangles = triangles,
                indexFormat = vertexCount > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            };

            if (normals != null)
            {
                mesh.normals = normals;
            }

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
