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
                new Vector2(1, 1),
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
    }
}
