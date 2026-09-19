using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// ゲーム内時間の正本。<see cref="Time.timeScale"/> を通じて
    /// 生産・ドロップ演出・Ghost 移動・WaitForSeconds 等、scaled 時間を使うものすべてに効く。
    /// イベント中は <see cref="PushPause"/> / <see cref="PopPause"/> で一時停止できる。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class KomayamaGameClock : MonoBehaviour
    {
        public static KomayamaGameClock Instance { get; private set; }

        [SerializeField, Min(0f), Tooltip("一時停止していないときのゲーム倍率（1=等速）。")]
        private float speedMultiplier = 1f;

        private int pauseDepth;
        private bool ownsTimeScale;

        /// <summary>一時停止中か（深さ 1 以上）。</summary>
        public bool IsPaused => pauseDepth > 0;

        /// <summary>一時停止していないときの倍率。</summary>
        public float SpeedMultiplier => Mathf.Max(0f, speedMultiplier);

        /// <summary>実際に Time.timeScale へ入れる値。停止中は 0。</summary>
        public float EffectiveTimeScale => IsPaused ? 0f : SpeedMultiplier;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[KomayamaGameClock] 複数存在します。後勝ちで Instance を更新します。", this);
            }

            Instance = this;
            ApplyToUnity();
        }

        private void OnEnable()
        {
            ApplyToUnity();
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            RestoreUnityDefault();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            RestoreUnityDefault();
        }

        /// <summary>ゲーム倍率を設定する（一時停止中でも保持し、解除後に反映）。</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Max(0f, multiplier);
            ApplyToUnity();
        }

        /// <summary>イベント等でゲーム時間を止める。入れ子可。</summary>
        public void PushPause()
        {
            pauseDepth++;
            ApplyToUnity();
        }

        /// <summary>対応する PushPause を 1 段解除する。</summary>
        public void PopPause()
        {
            if (pauseDepth <= 0)
            {
                return;
            }

            pauseDepth--;
            ApplyToUnity();
        }

        /// <summary>一時停止の ON/OFF（深さを 0 または 1 に揃える）。デバッグ／単純制御用。イベントは Push/Pop を使う。</summary>
        public void SetPaused(bool paused)
        {
            pauseDepth = paused ? 1 : 0;
            ApplyToUnity();
        }

        /// <summary>一時停止をトグル（深さを 0⇔1）。</summary>
        public void TogglePaused()
        {
            SetPaused(!IsPaused);
        }

        public void ApplyToUnity()
        {
            Time.timeScale = EffectiveTimeScale;
            ownsTimeScale = true;
        }

        private void RestoreUnityDefault()
        {
            if (!ownsTimeScale)
            {
                return;
            }

            Time.timeScale = 1f;
            ownsTimeScale = false;
        }

        /// <summary>シーンに無ければ等速・非停止として扱う。</summary>
        public static float ResolveEffectiveTimeScale()
        {
            return Instance != null ? Instance.EffectiveTimeScale : 1f;
        }

        public static bool ResolveIsPaused()
        {
            return Instance != null && Instance.IsPaused;
        }
    }
}
