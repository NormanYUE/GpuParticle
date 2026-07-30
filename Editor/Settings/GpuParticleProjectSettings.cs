using UnityEditor;
using UnityEngine;

namespace GpuParticle.Editor
{
    internal static class GpuParticleProjectSettings
    {
        private const string SettingsAssetPath = "Assets/GpuParticleGenerated/GpuParticleBakerSettings.asset";

        public static GpuParticleBakerSettings LoadOrCreate()
        {
            GpuParticleBakerSettings settings = AssetDatabase.LoadAssetAtPath<GpuParticleBakerSettings>(SettingsAssetPath);
            if (settings != null)
            {
                return settings;
            }

            EnsureFolder("Assets/GpuParticleGenerated");
            settings = ScriptableObject.CreateInstance<GpuParticleBakerSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public static void EnsureFolder(string assetFolder)
        {
            string normalized = assetFolder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
