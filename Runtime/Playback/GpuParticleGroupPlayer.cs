using System.Collections.Generic;
using UnityEngine;

namespace GpuParticle.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GpuParticleGroupPlayer : MonoBehaviour
    {
        [SerializeField] private List<GpuParticlePlayer> players = new List<GpuParticlePlayer>();

        public IReadOnlyList<GpuParticlePlayer> Players => players;

        private void OnEnable()
        {
            CollectPlayers();
        }

        public void Play(float timeScale = 1f, bool loop = false, uint seedVariant = uint.MaxValue)
        {
            CollectPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                GpuParticlePlayer player = players[i];
                if (player == null)
                {
                    continue;
                }

                GpuParticlePlayParams parameters = new GpuParticlePlayParams(
                    player.transform.localToWorldMatrix,
                    timeScale,
                    loop,
                    seedVariant);
                player.Play(parameters);
            }
        }

        public void Stop(bool clear = true)
        {
            CollectPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                players[i]?.Stop(clear);
            }
        }

        public void Pause()
        {
            CollectPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                players[i]?.Pause();
            }
        }

        public void Resume()
        {
            CollectPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                players[i]?.Resume();
            }
        }

        public void SetTimeScale(float timeScale)
        {
            CollectPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                players[i]?.SetTimeScale(timeScale);
            }
        }

        private void CollectPlayers()
        {
            players.Clear();
            GetComponentsInChildren<GpuParticlePlayer>(true, players);
            players.Sort(ComparePlayerOrder);
            for (int i = 0; i < players.Count; i++)
            {
                GpuParticlePlayer player = players[i];
                if (player == null)
                {
                    continue;
                }

                Debug.Log(
                    $"[GpuParticle] Group player order {i}: '{player.name}', " +
                    $"layerValue={player.SortingLayerValue}, order={player.SortingOrder}, " +
                    $"queue={player.RenderQueue}, depth={player.HierarchyDepth}");
            }
        }

        private static int ComparePlayerOrder(GpuParticlePlayer left, GpuParticlePlayer right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int compare = left.SortingLayerValue.CompareTo(right.SortingLayerValue);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.SortingOrder.CompareTo(right.SortingOrder);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.RenderQueue.CompareTo(right.RenderQueue);
            if (compare != 0)
            {
                return compare;
            }

            // When all explicit sorting keys are identical, render deeper children first
            // and shallower parents later so that parents naturally occlude children.
            compare = right.HierarchyDepth.CompareTo(left.HierarchyDepth);
            if (compare != 0)
            {
                return compare;
            }

            return string.Compare(left.name, right.name, System.StringComparison.Ordinal);
        }

        private void OnDisable()
        {
            Stop(clear: true);
        }
    }
}
