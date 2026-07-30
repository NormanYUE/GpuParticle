using System;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GpuParticle.Editor
{
    internal sealed class GpuParticlePreviewScene : IDisposable
    {
        private readonly Scene scene;
        private readonly Camera camera;
        private bool disposed;

        public GpuParticlePreviewScene(GpuParticleBakerSettings settings)
        {
            scene = EditorSceneManager.NewPreviewScene();
            GameObject cameraObject = new GameObject("GpuParticle Bake Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = settings.CameraPosition;
            camera.transform.rotation = Quaternion.Euler(settings.CameraEuler);
            camera.fieldOfView = settings.CameraFieldOfView;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;
            camera.enabled = false;

            GameObject lightObject = new GameObject("GpuParticle Bake Light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        public Scene Scene => scene;
        public Camera Camera => camera;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (scene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
