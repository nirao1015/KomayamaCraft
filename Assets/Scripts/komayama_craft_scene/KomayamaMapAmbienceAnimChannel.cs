using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>低優先カーソル／狐リアクション等のアニメ用チャンネル共通口（地図とコード分離）。</summary>
    public abstract class KomayamaMapAmbienceAnimChannel : MonoBehaviour
    {
        [SerializeField, Min(0)] private int cost = 1;
        [SerializeField, Min(0)] private int priority;

        public int Cost => Mathf.Max(0, cost);
        public int Priority => priority;
        public abstract bool WantsStart { get; }

        public abstract void OnAmbienceTick(float tickIntervalSeconds);

        public abstract bool TryStartAmbience();
    }
}
