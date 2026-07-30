using UnityEngine;

namespace GpuParticle.Editor
{
    public sealed class GpuParticleBakerSettings : ScriptableObject
    {
        public const string DefaultOutputRoot = "Assets/GpuParticleGenerated";

        [SerializeField] private string outputRoot = DefaultOutputRoot;
        [SerializeField] private float sampleRate = 120f;
        [SerializeField] private float maxDuration = 8f;
        [SerializeField] private int seedVariantCount = 4;
        [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 0f, -10f);
        [SerializeField] private Vector3 cameraEuler = Vector3.zero;
        [SerializeField] private float cameraFieldOfView = 60f;
        [SerializeField] private int imageWidth = 512;
        [SerializeField] private int imageHeight = 512;

        public string OutputRoot
        {
            get => string.IsNullOrWhiteSpace(outputRoot) ? DefaultOutputRoot : outputRoot;
            set => outputRoot = string.IsNullOrWhiteSpace(value) ? DefaultOutputRoot : value;
        }

        public float SampleRate
        {
            get => Mathf.Max(60f, sampleRate);
            set => sampleRate = Mathf.Max(60f, value);
        }

        public float MaxDuration
        {
            get => Mathf.Max(0.25f, maxDuration);
            set => maxDuration = Mathf.Max(0.25f, value);
        }

        public int SeedVariantCount
        {
            get => Mathf.Max(1, seedVariantCount);
            set => seedVariantCount = Mathf.Max(1, value);
        }

        public Vector3 CameraPosition
        {
            get => cameraPosition;
            set => cameraPosition = value;
        }

        public Vector3 CameraEuler
        {
            get => cameraEuler;
            set => cameraEuler = value;
        }

        public float CameraFieldOfView
        {
            get => Mathf.Clamp(cameraFieldOfView, 1f, 179f);
            set => cameraFieldOfView = Mathf.Clamp(value, 1f, 179f);
        }

        public int ImageWidth
        {
            get => Mathf.Max(32, imageWidth);
            set => imageWidth = Mathf.Max(32, value);
        }

        public int ImageHeight
        {
            get => Mathf.Max(32, imageHeight);
            set => imageHeight = Mathf.Max(32, value);
        }
    }
}
