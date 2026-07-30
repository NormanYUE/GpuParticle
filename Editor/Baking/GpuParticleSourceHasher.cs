using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GpuParticle.Editor
{
    public static class GpuParticleSourceHasher
    {
        public static string ComputePrefabHash(GameObject prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            string guid = AssetDatabase.AssetPathToGUID(path);
            string[] dependencies = AssetDatabase.GetDependencies(path, true);

            using SHA256 sha = SHA256.Create();
            Append(sha, guid);
            for (int i = 0; i < dependencies.Length; i++)
            {
                string dependency = dependencies[i];
                if (dependency.Contains("/GpuParticleGenerated/"))
                {
                    continue;
                }

                Append(sha, dependency);
                AppendFileContent(sha, dependency);
            }

            sha.TransformFinalBlock(System.Array.Empty<byte>(), 0, 0);
            return ToHex(sha.Hash!);
        }

        public static string ComputeFileContentHashForTests(string[] paths)
        {
            using SHA256 sha = SHA256.Create();
            for (int i = 0; i < paths.Length; i++)
            {
                Append(sha, paths[i]);
                AppendFileContent(sha, paths[i]);
            }

            sha.TransformFinalBlock(System.Array.Empty<byte>(), 0, 0);
            return ToHex(sha.Hash!);
        }

        private static void AppendFileContent(HashAlgorithm hash, string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                Append(hash, string.Empty);
                return;
            }

            byte[] bytes = System.IO.File.ReadAllBytes(path);
            hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
            byte[] delimiter = { 0xFF };
            hash.TransformBlock(delimiter, 0, delimiter.Length, null, 0);
        }

        public static string ComputeFingerprint(GameObject prefab, GpuParticleBakerSettings settings)
        {
            using SHA256 sha = SHA256.Create();
            Append(sha, ComputePrefabHash(prefab));
            Append(sha, settings.SampleRate.ToString("R"));
            Append(sha, settings.MaxDuration.ToString("R"));
            Append(sha, settings.SeedVariantCount.ToString());
            Append(sha, settings.CameraPosition.ToString("R"));
            Append(sha, settings.CameraEuler.ToString("R"));
            Append(sha, settings.CameraFieldOfView.ToString("R"));
            Append(sha, Application.unityVersion);
            sha.TransformFinalBlock(System.Array.Empty<byte>(), 0, 0);
            return ToHex(sha.Hash!);
        }

        private static void Append(HashAlgorithm hash, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
            byte[] delimiter = { 0 };
            hash.TransformBlock(delimiter, 0, delimiter.Length, null, 0);
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
