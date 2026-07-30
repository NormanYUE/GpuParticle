using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GpuParticle.Runtime
{
    internal static class GpuParticleGeometryRenderer
    {
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
                if (!track.RendererRecipe.IsCameraCompatible(camera))
                {
                    instance.Owner?.RequestNativeFallback(
                        new GpuParticleFailure(
                            GpuParticleFailureCode.CameraDependentGeometry,
                            "Current camera does not match the baked camera profile.",
                            track.TransformPath),
                        instance.Elapsed,
                        instance.SeedVariant,
                        instance.TimeScale);
                    return;
                }

                int frameIndex = track.FindFrameIndex(instance.Elapsed);
                if (frameIndex < 0)
                {
                    continue;
                }

                GpuParticleGeometryFrame frame = track.Frames[frameIndex];
                DrawMesh(frame.Mesh, track.MaterialRecipes, track, instance.LocalToWorld, camera);
                DrawMesh(frame.TrailMesh, track.TrailMaterialRecipes, track, instance.LocalToWorld, camera);
            }
        }

        private static void DrawMesh(
            Mesh mesh,
            GpuParticleMaterialRecipe[] materials,
            GpuParticleGeometryTrack track,
            Matrix4x4 matrix,
            Camera camera)
        {
            if (mesh == null || mesh.vertexCount == 0)
            {
                return;
            }

            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            for (int i = 0; i < materials.Length; i++)
            {
                GpuParticleMaterialRecipe materialRecipe = materials[i];
                Material material = materialRecipe.Material;
                if (material == null)
                {
                    continue;
                }

                int subMesh = Mathf.Clamp(materialRecipe.SubMeshIndex, 0, subMeshCount - 1);
                RenderParams renderParams = new RenderParams(material)
                {
                    camera = camera,
                    layer = track.RendererRecipe.Layer,
                    rendererPriority = track.RendererRecipe.RendererPriority,
                    shadowCastingMode = track.RendererRecipe.ShadowCastingMode,
                    receiveShadows = track.RendererRecipe.ReceiveShadows,
                    lightProbeUsage = LightProbeUsage.Off,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                    worldBounds = TransformBounds(mesh.bounds, matrix),
                };
                Graphics.RenderMesh(renderParams, mesh, subMesh, matrix);
            }
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
