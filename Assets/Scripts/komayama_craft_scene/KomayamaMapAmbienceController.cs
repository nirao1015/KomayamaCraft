using System.Collections.Generic;
using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// スキップ可能な地図演出・低優先カーソル演出の予算付きスケジューラ。
    /// 地図チャンネルとアニメチャンネルは別リストで管理する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaMapAmbienceController : MonoBehaviour
    {
        [Header("ティック")]
        [SerializeField, Min(0.5f)] private float tickIntervalSeconds = 5f;

        [Header("予算")]
        [SerializeField, Min(1)] private int maxConcurrentCost = 8;
        [SerializeField, Min(1)] private int maxStartsPerTick = 2;

        [Header("地図チャンネル")]
        [SerializeField] private List<KomayamaMapAmbienceMapChannel> mapChannels = new();

        [Header("アニメ（低優先カーソル等）チャンネル")]
        [SerializeField] private List<KomayamaMapAmbienceAnimChannel> animChannels = new();

        private float tickElapsed;
        private int usedCost;

        public int UsedCost => usedCost;

        private void Start()
        {
            // 雲の出現自体は待たない。スケジューラ起動時点で Ready。
            KomayamaCraftBootReady.NotifyAmbienceReady();
        }

        public bool TryReserveCost(int cost)
        {
            if (cost <= 0)
            {
                return true;
            }

            if (usedCost + cost > maxConcurrentCost)
            {
                return false;
            }

            usedCost += cost;
            return true;
        }

        public void ReleaseCost(int cost)
        {
            if (cost <= 0)
            {
                return;
            }

            usedCost = Mathf.Max(0, usedCost - cost);
        }

        private void Update()
        {
            if (KomayamaGameClock.ResolveIsPaused())
            {
                return;
            }

            tickElapsed += Time.deltaTime;
            if (tickElapsed < tickIntervalSeconds)
            {
                return;
            }

            tickElapsed = 0f;
            TickMapChannels();
            TickAnimChannels();
        }

        private void TickMapChannels()
        {
            int starts = 0;
            for (int i = 0; i < mapChannels.Count; i++)
            {
                KomayamaMapAmbienceMapChannel channel = mapChannels[i];
                if (channel == null || !channel.isActiveAndEnabled)
                {
                    continue;
                }

                channel.OnAmbienceTick(tickIntervalSeconds);

                if (starts >= maxStartsPerTick)
                {
                    continue;
                }

                if (!channel.WantsStart)
                {
                    continue;
                }

                if (!TryReserveCost(channel.Cost))
                {
                    // タイマ据え置き：WantsStart のまま次ティックで再挑戦
                    continue;
                }

                if (channel.TryStartAmbience())
                {
                    starts++;
                }
                else
                {
                    ReleaseCost(channel.Cost);
                }
            }
        }

        private void TickAnimChannels()
        {
            int starts = 0;
            for (int i = 0; i < animChannels.Count; i++)
            {
                KomayamaMapAmbienceAnimChannel channel = animChannels[i];
                if (channel == null || !channel.isActiveAndEnabled)
                {
                    continue;
                }

                channel.OnAmbienceTick(tickIntervalSeconds);

                if (starts >= maxStartsPerTick)
                {
                    continue;
                }

                if (!channel.WantsStart)
                {
                    continue;
                }

                if (!TryReserveCost(channel.Cost))
                {
                    continue;
                }

                if (channel.TryStartAmbience())
                {
                    starts++;
                }
                else
                {
                    ReleaseCost(channel.Cost);
                }
            }
        }
    }
}
