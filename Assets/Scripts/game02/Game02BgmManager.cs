using System.Collections;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// game02 シーンの BGM（通常 / バズ切替）を一元管理する。Clip・AudioSource は Inspector で指定。未設定は無音。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Game02BgmManager : MonoBehaviour
    {
        private enum BgmMode
        {
            Normal = 0,
            Buzz = 1,
            ClearCeremony = 2
        }

        private static Game02BgmManager instance;

        [Header("BGM 再生先")]
        [SerializeField, Tooltip("BGM を鳴らす AudioSource。未設定時は同じ GameObject の AudioSource を使います（無ければ無音）。")]
        private AudioSource bgmPlaybackAudioSource;

        [Header("BGM クリップ")]
        [SerializeField, Tooltip("通常プレイ中の BGM。")]
        private AudioClip normalBgmClip;
        [SerializeField, Tooltip("バズ状態の BGM。")]
        private AudioClip buzzBgmClip;
        [SerializeField, Tooltip("ゲームクリア演出用 BGM（ループ）。未設定なら無音。")]
        private AudioClip clearCeremonyBgmClip;

        [Header("再生設定")]
        [SerializeField, Tooltip("開始時に通常 BGM を再生するか。")]
        private bool playNormalBgmOnStart = true;
        [SerializeField, Tooltip("ループ再生するか。")]
        private bool loop = true;

        [Header("バズ切替フェード")]
        [SerializeField, Tooltip("通常からバズへ切り替える際のフェードアウト秒。")]
        private float buzzFadeOutSeconds = 0.2f;
        [SerializeField, Tooltip("バズから通常へ戻る際のフェードアウト秒。")]
        private float normalFadeOutSeconds = 0.4f;
        [SerializeField, Tooltip("通常 BGM 再開時のフェードイン秒。")]
        private float normalFadeInSeconds = 0.4f;

        private AudioClip currentClip;
        private Coroutine transitionRoutine;
        private int activeBuzzRequestCount;
        private BgmMode currentMode = BgmMode.Normal;

        /// <summary>
        /// バズ BGM の参照カウントが残っている間に真。
        /// WorkMovie のバズ演出サイクル終了・FIFO 追い出し等で <see cref="RequestBuzzModeEnd"/> が呼ばれると減る。
        /// Parson01 のバズ／通常画像切替もこれに合わせる。
        /// </summary>
        public bool HasActiveBuzzBgmRequests => activeBuzzRequestCount > 0;

        public static Game02BgmManager TryGet()
        {
            if (instance != null)
            {
                return instance;
            }

            return FindObjectOfType<Game02BgmManager>(true);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            ResolveBgmAudioSource();
            PrepareSource(bgmPlaybackAudioSource);
            ApplyBgmGain();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Start()
        {
            if (playNormalBgmOnStart)
            {
                PlayNormalBgm();
            }
        }

        private void Update()
        {
            ApplyBgmGain();
        }

        public void PlayNormalBgm()
        {
            currentMode = BgmMode.Normal;
            PlayClip(normalBgmClip);
        }

        public void PlayBuzzBgm()
        {
            currentMode = BgmMode.Buzz;
            PlayClip(buzzBgmClip);
        }

        public void StopBgm()
        {
            if (bgmPlaybackAudioSource == null)
            {
                return;
            }

            bgmPlaybackAudioSource.Stop();
            currentClip = null;
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }
        }

        /// <summary>ゲームクリア演出フェーズ D。フェード中コルーチンを中断してクリア用ループを再生。</summary>
        public void PlayClearCeremonyBgm()
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            ResolveBgmAudioSource();
            if (bgmPlaybackAudioSource == null || clearCeremonyBgmClip == null)
            {
                return;
            }

            currentMode = BgmMode.ClearCeremony;
            currentClip = clearCeremonyBgmClip;
            bgmPlaybackAudioSource.clip = clearCeremonyBgmClip;
            bgmPlaybackAudioSource.loop = true;
            ApplyBgmGain();
            bgmPlaybackAudioSource.Play();
        }

        /// <summary>クリアパネルを閉じたあと。バズ参照カウントに応じて通常／バズ BGM を再生。</summary>
        public void ResumeGameplayBgmAfterClearCeremony()
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            StopBgm();
            if (HasActiveBuzzBgmRequests)
            {
                PlayBuzzBgm();
            }
            else
            {
                PlayNormalBgm();
            }
        }

        public void RequestBuzzModeStart()
        {
            if (!CanProcessRequests())
            {
                return;
            }

            activeBuzzRequestCount = Mathf.Max(0, activeBuzzRequestCount + 1);
            StartBuzzTransitionIfNeeded();
        }

        public void RequestBuzzModeEnd()
        {
            if (!CanProcessRequests())
            {
                return;
            }

            activeBuzzRequestCount = Mathf.Max(0, activeBuzzRequestCount - 1);
            if (activeBuzzRequestCount <= 0)
            {
                StartNormalTransitionIfNeeded();
            }
        }

        private void PlayClip(AudioClip clip)
        {
            ResolveBgmAudioSource();
            if (bgmPlaybackAudioSource == null || clip == null)
            {
                return;
            }

            if (currentClip == clip && bgmPlaybackAudioSource.isPlaying)
            {
                ApplyBgmGain();
                return;
            }

            currentClip = clip;
            bgmPlaybackAudioSource.clip = clip;
            bgmPlaybackAudioSource.loop = loop;
            ApplyBgmGain();
            bgmPlaybackAudioSource.Play();
        }

        private void StartBuzzTransitionIfNeeded()
        {
            if (!CanRunCoroutine())
            {
                return;
            }

            if (currentMode == BgmMode.Buzz && transitionRoutine == null)
            {
                return;
            }

            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            transitionRoutine = StartCoroutine(CoTransitionToBuzz());
        }

        private void StartNormalTransitionIfNeeded()
        {
            if (!CanRunCoroutine())
            {
                return;
            }

            if (currentMode == BgmMode.Normal && transitionRoutine == null)
            {
                return;
            }

            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            transitionRoutine = StartCoroutine(CoTransitionToNormal());
        }

        private IEnumerator CoTransitionToBuzz()
        {
            ResolveBgmAudioSource();
            yield return FadeOutCurrent(Mathf.Max(0f, buzzFadeOutSeconds));
            PlayBuzzBgm();
            transitionRoutine = null;
        }

        private IEnumerator CoTransitionToNormal()
        {
            ResolveBgmAudioSource();
            yield return FadeOutCurrent(Mathf.Max(0f, normalFadeOutSeconds));

            if (normalBgmClip == null || bgmPlaybackAudioSource == null)
            {
                currentMode = BgmMode.Normal;
                transitionRoutine = null;
                yield break;
            }

            currentMode = BgmMode.Normal;
            currentClip = normalBgmClip;
            bgmPlaybackAudioSource.clip = normalBgmClip;
            bgmPlaybackAudioSource.loop = loop;
            bgmPlaybackAudioSource.volume = 0f;
            bgmPlaybackAudioSource.Play();
            yield return FadeToTargetGain(Mathf.Max(0f, normalFadeInSeconds));
            transitionRoutine = null;
        }

        private IEnumerator FadeOutCurrent(float seconds)
        {
            if (bgmPlaybackAudioSource == null || !bgmPlaybackAudioSource.isPlaying)
            {
                yield break;
            }

            float start = bgmPlaybackAudioSource.volume;
            if (seconds <= 0f)
            {
                bgmPlaybackAudioSource.volume = 0f;
                bgmPlaybackAudioSource.Stop();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
                float t = Mathf.Clamp01(elapsed / seconds);
                bgmPlaybackAudioSource.volume = Mathf.Lerp(start, 0f, t);
                yield return null;
            }

            bgmPlaybackAudioSource.volume = 0f;
            bgmPlaybackAudioSource.Stop();
        }

        private IEnumerator FadeToTargetGain(float seconds)
        {
            if (bgmPlaybackAudioSource == null)
            {
                yield break;
            }

            float target = ResolveBgmGain();
            if (seconds <= 0f)
            {
                bgmPlaybackAudioSource.volume = target;
                yield break;
            }

            float start = bgmPlaybackAudioSource.volume;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
                float t = Mathf.Clamp01(elapsed / seconds);
                target = ResolveBgmGain();
                bgmPlaybackAudioSource.volume = Mathf.Lerp(start, target, t);
                yield return null;
            }

            bgmPlaybackAudioSource.volume = ResolveBgmGain();
        }

        private void ResolveBgmAudioSource()
        {
            if (bgmPlaybackAudioSource != null)
            {
                return;
            }

            bgmPlaybackAudioSource = GetComponent<AudioSource>();
        }

        private static void PrepareSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.Stop();
            source.clip = null;
            source.time = 0f;
        }

        private void ApplyBgmGain()
        {
            if (bgmPlaybackAudioSource == null)
            {
                return;
            }

            if (transitionRoutine == null)
            {
                bgmPlaybackAudioSource.volume = ResolveBgmGain();
            }
        }

        private static float ResolveBgmGain()
        {
            float volume = 1f;
            if (SoundSettingsManager.Instance != null)
            {
                volume = SoundSettingsManager.Instance.GetBgmGain01();
            }

            return Mathf.Clamp01(volume);
        }

        private bool CanProcessRequests()
        {
            return this != null && gameObject != null && gameObject.activeInHierarchy;
        }

        private bool CanRunCoroutine()
        {
            return CanProcessRequests() && isActiveAndEnabled;
        }
    }
}
