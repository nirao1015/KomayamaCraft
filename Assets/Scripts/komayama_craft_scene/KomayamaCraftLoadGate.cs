using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// タイトル→クラフト遷移中にゲーム時間を止めるゲート。
    /// <see cref="KomayamaGameClock"/> が Awake しても、解放まで timeScale=0 を維持する。
    /// </summary>
    public static class KomayamaCraftLoadGate
    {
        public static bool HoldGameTime { get; private set; }

        public static void BeginHold()
        {
            HoldGameTime = true;
            Time.timeScale = 0f;
        }

        public static void ReleaseHold()
        {
            HoldGameTime = false;
            KomayamaGameClock clock = KomayamaGameClock.Instance;
            if (clock != null)
            {
                clock.ApplyToUnity();
                return;
            }

            Time.timeScale = 1f;
        }
    }
}
