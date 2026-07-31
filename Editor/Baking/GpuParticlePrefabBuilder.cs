using GpuParticle.Runtime;
using UnityEditor;
using UnityEngine;

namespace GpuParticle.Editor.Baking
{
    public static class GpuParticlePrefabBuilder
    {
        public static GameObject Build(
            string prefabPath,
            Mesh mesh,
            Material material,
            GpuParticleClip clip)
        {
            mesh.bounds = clip.LocalBounds;

            var go = new GameObject(clip.name + "_VAT");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            var vat = go.AddComponent<GpuParticleVatRenderer>();
            SerializedObject so = new SerializedObject(vat);
            so.FindProperty("clip").objectReferenceValue = clip;
            so.FindProperty("loop").boolValue = true;
            so.FindProperty("timeScale").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            if (prefab != null)
            {
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            Object.DestroyImmediate(go);
            return prefab!;
        }
    }
}
