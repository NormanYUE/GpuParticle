using UnityEditor;
using UnityEngine;

namespace GpuParticle.Editor
{
    internal static class GpuParticleSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/GPU Particle", SettingsScope.Project)
            {
                label = "GPU Particle",
                guiHandler = _ =>
                {
                    GpuParticleBakerSettings settings = GpuParticleProjectSettings.LoadOrCreate();
                    SerializedObject serialized = new SerializedObject(settings);
                    serialized.Update();

                    EditorGUILayout.PropertyField(serialized.FindProperty("outputRoot"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("sampleRate"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("maxDuration"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("seedVariantCount"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("cameraPosition"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("cameraEuler"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("cameraFieldOfView"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("imageWidth"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("imageHeight"));

                    if (serialized.ApplyModifiedProperties())
                    {
                        EditorUtility.SetDirty(settings);
                        AssetDatabase.SaveAssets();
                    }
                },
            };
        }
    }
}
