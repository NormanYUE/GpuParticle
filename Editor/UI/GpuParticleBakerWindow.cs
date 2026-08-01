using System.Collections.Generic;
using GpuParticle.Runtime;
using UnityEditor;
using UnityEngine;

namespace GpuParticle.Editor
{
    public sealed class GpuParticleBakerWindow : EditorWindow
    {
        private readonly List<GameObject> prefabs = new List<GameObject>();
        private Vector2 scroll;
        private GpuParticleBakerSettings settings = null!;
        private readonly List<GpuParticleValidationResult> results = new List<GpuParticleValidationResult>();

        public static void Open()
        {
            GetWindow<GpuParticleBakerWindow>("GPU Particle Baker").Show();
        }

        private void OnEnable()
        {
            settings = GpuParticleProjectSettings.LoadOrCreate();
        }

        private void OnGUI()
        {
            if (settings == null)
            {
                settings = GpuParticleProjectSettings.LoadOrCreate();
            }

            DrawSettings();
            EditorGUILayout.Space(8f);
            DrawPrefabList();
            EditorGUILayout.Space(8f);
            DrawActions();
            EditorGUILayout.Space(8f);
            DrawResults();
        }

        private void DrawSettings()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
                SerializedObject serialized = new SerializedObject(settings);
                serialized.Update();
                EditorGUILayout.PropertyField(serialized.FindProperty("outputRoot"));
                EditorGUILayout.PropertyField(serialized.FindProperty("sampleRate"));
                EditorGUILayout.PropertyField(serialized.FindProperty("maxDuration"));
                EditorGUILayout.PropertyField(serialized.FindProperty("seedVariantCount"));
                if (serialized.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private void DrawPrefabList()
        {
            Rect dropRect = GUILayoutUtility.GetRect(0f, 48f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drag Prefabs or Folders Here");
            HandleDragAndDrop(dropRect);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(100f));
            for (int i = 0; i < prefabs.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    prefabs[i] = (GameObject)EditorGUILayout.ObjectField(prefabs[i], typeof(GameObject), false);
                    if (GUILayout.Button("Remove", GUILayout.Width(80f)))
                    {
                        prefabs.RemoveAt(i);
                        i--;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("分析并烘焙", GUILayout.Height(32f)))
                {
                    BakeAll();
                }

                if (GUILayout.Button("Validate", GUILayout.Height(32f)))
                {
                    ValidateAll();
                }

                if (GUILayout.Button("Clear", GUILayout.Height(32f), GUILayout.Width(80f)))
                {
                    prefabs.Clear();
                    results.Clear();
                }
            }
        }

        private void DrawResults()
        {
            if (results.Count == 0)
            {
                return;
            }

            int gpu = 0;
            int native = 0;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].IsGpuReady)
                {
                    gpu++;
                }
                else
                {
                    native++;
                }
            }

            EditorGUILayout.LabelField($"GPU 可用: {gpu}    保留原生: {native}", EditorStyles.boldLabel);
            for (int i = 0; i < results.Count; i++)
            {
                GpuParticleValidationResult result = results[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(result.PrefabPath, GUILayout.MinWidth(220f));
                    EditorGUILayout.LabelField(result.IsGpuReady ? "GPU 可用" : "保留原生", GUILayout.Width(80f));
                    if (!result.IsGpuReady)
                    {
                        EditorGUILayout.LabelField(result.Failure.Code.ToString(), GUILayout.Width(180f));
                        string detail = FormatFailureDetail(result);
                        if (!string.IsNullOrEmpty(detail))
                        {
                            EditorGUILayout.LabelField(detail, GUILayout.MinWidth(280f));
                        }
                    }
                }
            }
        }

        private static string FormatFailureDetail(GpuParticleValidationResult result)
        {
            if (!result.Failure.IsFailure)
            {
                return string.Empty;
            }

            string context = result.Failure.Context;
            string message = result.Failure.Message;
            if (string.IsNullOrEmpty(context))
            {
                return message;
            }

            if (string.IsNullOrEmpty(message))
            {
                return context;
            }

            return $"{context}: {message}";
        }

        private void BakeAll()
        {
            results.Clear();
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] != null)
                {
                    results.Add(GpuParticleBakePipeline.BakePrefabGroup(prefabs[i], settings));
                }
            }
        }

        private void ValidateAll()
        {
            results.Clear();
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] != null)
                {
                    results.Add(GpuParticleBakePipeline.ValidatePrefab(prefabs[i]));
                }
            }
        }

        private void HandleDragAndDrop(Rect dropRect)
        {
            Event evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
                {
                    AddObject(DragAndDrop.objectReferences[i]);
                }

                evt.Use();
            }
        }

        private void AddObject(Object obj)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(path))
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
                for (int i = 0; i < guids.Length; i++)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    AddPrefab(prefab);
                }
            }
            else
            {
                AddPrefab(obj as GameObject);
            }
        }

        private void AddPrefab(GameObject? prefab)
        {
            if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
            {
                return;
            }

            if (!prefabs.Contains(prefab))
            {
                prefabs.Add(prefab);
            }
        }
    }
}
