using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>地図アンビエント用チャンネルの共通口。</summary>
    public abstract class KomayamaMapAmbienceMapChannel : MonoBehaviour
    {
        [SerializeField, Min(1)] private int cost = 1;
        [SerializeField, Min(0)] private int priority;

        public int Cost => Mathf.Max(1, cost);
        public int Priority => priority;
        public abstract bool WantsStart { get; }

        public abstract void OnAmbienceTick(float tickIntervalSeconds);

        public abstract bool TryStartAmbience();
    }
}
