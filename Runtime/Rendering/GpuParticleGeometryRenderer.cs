using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GpuParticle.Runtime
{
    internal static class GpuParticleGeometryRenderer
    {
        private static readonly int ParticleStatesId = Shader.PropertyToID("_ParticleStates");
        private static readonly int MeshTransformsId = Shader.PropertyToID("_MeshTransforms");
        private static readonly int TrailStatesId = Shader.PropertyToID("_TrailStates");
        private static readonly int LocalToWorldId = Shader.PropertyToID("_LocalToWorld");
        private static readonly int CameraRightId = Shader.PropertyToID("_CameraRight");
        private static readonly int CameraUpId = Shader.PropertyToID("_CameraUp");
        private static readonly int StretchScaleId = Shader.PropertyToID("_StretchScale");

        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly Dictionary<(GpuParticleGeometryTrack track, GpuParticleRenderMode mode), Material> MaterialCache = new();

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
            Material? material = ResolveMaterial(instance.Clip, track, track.MaterialRecipes[0], GpuParticleRenderMode.Billboard);
            if (material == null || instance.ParticleStateBuffer == null)
            {
                return;
            }

            PropertyBlock.Clear();
            PropertyBlock.SetBuffer(ParticleStatesId, instance.ParticleStateBuffer);
            PropertyBlock.SetMatrix(LocalToWorldId, instance.LocalToWorld);
            PropertyBlock.SetVector(CameraRightId, camera.transform.right);
            PropertyBlock.SetVector(CameraUpId, camera.transform.up);
            SetAlignmentKeyword(material, track.Alignment);

            var renderParams = new RenderParams(material)
            {
                camera = camera,
                layer = track.RendererRecipe.Layer,
                worldBounds = TransformBounds(track.LocalBounds, instance.LocalToWorld),
                matProps = PropertyBlock,
            };

            int count = instance.ParticleStateBuffer.count;
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, count);
        }

        private static void RenderStretch(GpuParticleGeometryTrack track, GpuParticleInstancePool.Instance instance, Camera camera)
        {
            Material? material = ResolveMaterial(instance.Clip, track, track.MaterialRecipes[0], GpuParticleRenderMode.StretchedBillboard);
            if (material == null || instance.ParticleStateBuffer == null)
            {
                return;
            }

            PropertyBlock.Clear();
            PropertyBlock.SetBuffer(ParticleStatesId, instance.ParticleStateBuffer);
            PropertyBlock.SetMatrix(LocalToWorldId, instance.LocalToWorld);
            PropertyBlock.SetVector(CameraRightId, camera.transform.right);
            PropertyBlock.SetFloat(StretchScaleId, 0.1f);
            SetAlignmentKeyword(material, track.Alignment);

            var renderParams = new RenderParams(material)
            {
                camera = camera,
                layer = track.RendererRecipe.Layer,
                worldBounds = TransformBounds(track.LocalBounds, instance.LocalToWorld),
                matProps = PropertyBlock,
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

            Material? material = ResolveMaterial(instance.Clip, track, track.MaterialRecipes[0], GpuParticleRenderMode.Mesh);
            if (material == null)
            {
                return;
            }

            PropertyBlock.Clear();
            PropertyBlock.SetBuffer(MeshTransformsId, instance.MeshTransformBuffer);
            PropertyBlock.SetMatrix(LocalToWorldId, instance.LocalToWorld);

            var renderParams = new RenderParams(material)
            {
                camera = camera,
                layer = track.RendererRecipe.Layer,
                worldBounds = TransformBounds(track.LocalBounds, instance.LocalToWorld),
                matProps = PropertyBlock,
            };

            int count = instance.MeshTransformBuffer.count;
            Graphics.DrawMeshInstancedProcedural(track.SharedMesh, 0, material, renderParams.worldBounds, count, PropertyBlock);
        }

        private static void RenderTrails(GpuParticleGeometryTrack track, GpuParticleInstancePool.Instance instance, Camera camera)
        {
            Material? material = ResolveMaterial(instance.Clip, track, track.TrailMaterialRecipes[0], GpuParticleRenderMode.Billboard, true);
            if (material == null || instance.TrailStateBuffer == null)
            {
                return;
            }

            PropertyBlock.Clear();
            PropertyBlock.SetBuffer(TrailStatesId, instance.TrailStateBuffer);
            PropertyBlock.SetMatrix(LocalToWorldId, instance.LocalToWorld);

            var renderParams = new RenderParams(material)
            {
                camera = camera,
                layer = track.RendererRecipe.Layer,
                worldBounds = TransformBounds(track.LocalBounds, instance.LocalToWorld),
                matProps = PropertyBlock,
            };

            int count = instance.TrailStateBuffer.count;
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, count);
        }

        private static Material? ResolveMaterial(
            GpuParticleClip clip,
            GpuParticleGeometryTrack track,
            GpuParticleMaterialRecipe recipe,
            GpuParticleRenderMode mode,
            bool isTrail = false)
        {
            if (recipe?.Material == null)
            {
                return null;
            }

            (GpuParticleGeometryTrack track, GpuParticleRenderMode mode) key = (track, mode);
            if (MaterialCache.TryGetValue(key, out Material cached))
            {
                return cached;
            }

            Material material = new Material(recipe.Material);
            Shader? shader = ResolveShader(clip, mode, isTrail);
            if (shader != null)
            {
                material.shader = shader;
            }

            MaterialCache[key] = material;
            return material;
        }

        private static Shader? ResolveShader(GpuParticleClip clip, GpuParticleRenderMode mode, bool isTrail)
        {
            GpuParticleRuntimeResources? resources = clip.RuntimeResources;

            if (isTrail)
            {
                Shader? fromResources = resources?.TrailShader;
                return fromResources != null ? fromResources : Shader.Find("GpuParticle/Trail");
            }

            Shader? resourcesShader = mode switch
            {
                GpuParticleRenderMode.Billboard => resources?.BillboardShader,
                GpuParticleRenderMode.StretchedBillboard => resources?.StretchShader,
                GpuParticleRenderMode.Mesh => resources?.MeshShader,
                _ => null,
            };

            if (resourcesShader != null)
            {
                return resourcesShader;
            }

            return mode switch
            {
                GpuParticleRenderMode.Billboard => Shader.Find("GpuParticle/Billboard"),
                GpuParticleRenderMode.StretchedBillboard => Shader.Find("GpuParticle/Stretch"),
                GpuParticleRenderMode.Mesh => Shader.Find("GpuParticle/Mesh"),
                _ => null,
            };
        }

        private static void SetAlignmentKeyword(Material material, GpuParticleAlignment alignment)
        {
            material.DisableKeyword("ALIGNMENT_VIEW");
            material.DisableKeyword("ALIGNMENT_FACING");
            material.DisableKeyword("ALIGNMENT_WORLD");
            material.DisableKeyword("ALIGNMENT_LOCAL");

            string keyword = alignment switch
            {
                GpuParticleAlignment.View => "ALIGNMENT_VIEW",
                GpuParticleAlignment.Facing => "ALIGNMENT_FACING",
                GpuParticleAlignment.World => "ALIGNMENT_WORLD",
                GpuParticleAlignment.Local => "ALIGNMENT_LOCAL",
                _ => "ALIGNMENT_VIEW",
            };
            material.EnableKeyword(keyword);
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
