using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GpuParticle.Runtime
{
    internal static class GpuParticleGeometryRenderer
    {
        private static readonly int ParticleStatesId = Shader.PropertyToID("_ParticleStates");
        private static readonly int MeshTransformsId = Shader.PropertyToID("_MeshTransforms");
        private static readonly int LocalToWorldId = Shader.PropertyToID("_LocalToWorld");
        private static readonly int CameraRightId = Shader.PropertyToID("_CameraRight");
        private static readonly int CameraUpId = Shader.PropertyToID("_CameraUp");

        public static void Render(ArraySegment<GpuParticleInstancePool.Instance> instances, Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            for (int i = 0; i < instances.Count; i++)
            {
                GpuParticleInstancePool.Instance instance = instances.Array![instances.Offset + i];
                RenderInstance(instance, camera);
            }
        }

        private static void RenderInstance(GpuParticleInstancePool.Instance instance, Camera camera)
        {
            GpuParticleGeometryTrack[] tracks = instance.Clip.GeometryTracks;
            for (int t = 0; t < tracks.Length; t++)
            {
                GpuParticleGeometryTrack track = tracks[t];
                switch (track.RenderMode)
                {
                    case GpuParticleRenderMode.Billboard:
                        RenderBillboard(track, instance, camera);
                        break;
                    case GpuParticleRenderMode.StretchedBillboard:
                        RenderStretch(track, instance, camera);
                        break;
                    case GpuParticleRenderMode.Mesh:
                        RenderMesh(track, instance, camera);
                        break;
                }

                if (track.TrailMaterialRecipes.Length > 0)
                {
                    RenderTrails(track, instance, camera);
                }
            }
        }

        private static void RenderBillboard(GpuParticleGeometryTrack track, GpuParticleInstancePool.Instance instance, Camera camera)
        {
            var material = track.MaterialRecipes[0].Material;
            if (material == null || instance.ParticleStateBuffer == null)
            {
                return;
            }

            material.SetBuffer(ParticleStatesId, instance.ParticleStateBuffer);
            material.SetMatrix(LocalToWorldId, instance.LocalToWorld);
            material.SetVector(CameraRightId, camera.transform.right);
            material.SetVector(CameraUpId, camera.transform.up);

            var renderParams = new RenderParams(material)
            {
                camera = camera,
                layer = track.RendererRecipe.Layer,
                worldBounds = TransformBounds(track.LocalBounds, instance.LocalToWorld),
            };

            int count = instance.ParticleStateBuffer.count;
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, count);
        }

        private static void RenderStretch(GpuParticleGeometryTrack track, GpuParticleInstancePool.Instance instance, Camera camera)
        {
            var material = track.MaterialRecipes[0].Material;
            if (material == null || instance.ParticleStateBuffer == null)
            {
                return;
            }

            material.SetBuffer(ParticleStatesId, instance.ParticleStateBuffer);
            material.SetMatrix(LocalToWorldId, instance.LocalToWorld);
            material.SetVector(CameraRightId, camera.transform.right);
            material.SetFloat(Shader.PropertyToID("_StretchScale"), 0.1f);

            var renderParams = new RenderParams(material)
            {
                camera = camera,
                layer = track.RendererRecipe.Layer,
                worldBounds = TransformBounds(track.LocalBounds, instance.LocalToWorld),
            };

            int count = instance.ParticleStateBuffer.count;
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, count);
        }

        private static void RenderMesh(GpuParticleGeometryTrack track, GpuParticleInstancePool.Instance instance, Camera camera)
        {
            if (track.SharedMesh == null || instance.MeshTransformBuffer == null)
            {
                return;
            }

            var material = track.MaterialRecipes[0].Material;
            if (material == null)
            {
                return;
            }

            material.SetBuffer(MeshTransformsId, instance.MeshTransformBuffer);
            material.SetMatrix(LocalToWorldId, instance.LocalToWorld);

            var renderParams = new RenderParams(material)
            {
                camera = camera,
                layer = track.RendererRecipe.Layer,
                worldBounds = TransformBounds(track.LocalBounds, instance.LocalToWorld),
            };

            int count = instance.MeshTransformBuffer.count;
            Graphics.DrawMeshInstancedProcedural(track.SharedMesh, 0, material, renderParams.worldBounds, count);
        }

        private static void RenderTrails(GpuParticleGeometryTrack track, GpuParticleInstancePool.Instance instance, Camera camera)
        {
            var material = track.TrailMaterialRecipes[0].Material;
            if (material == null || instance.TrailStateBuffer == null)
            {
                return;
            }

            material.SetBuffer(Shader.PropertyToID("_TrailStates"), instance.TrailStateBuffer);
            material.SetMatrix(LocalToWorldId, instance.LocalToWorld);

            var renderParams = new RenderParams(material)
            {
                camera = camera,
                layer = track.RendererRecipe.Layer,
                worldBounds = TransformBounds(track.LocalBounds, instance.LocalToWorld),
            };

            int count = instance.TrailStateBuffer.count;
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, count);
        }

        private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);
            Vector3 extents = localBounds.extents;
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
            extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
            extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);
            return new Bounds(center, extents * 2f);
        }
    }
}
