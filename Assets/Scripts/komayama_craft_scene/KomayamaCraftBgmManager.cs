using System.Collections;
using UnityEngine;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaCraftBgmManager : MonoBehaviour
    {
        [SerializeField] private AudioSource playbackSource;
        [SerializeField] private AudioClip gameplayClip;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool loop = true;
        [SerializeField, Min(0.05f), InspectorName("フェードアウト秒")]
        private float defaultFadeOutSeconds = 0.8f;
        [SerializeField, Min(0.05f), InspectorName("再開フェードイン秒")]
        private float defaultFadeInSeconds = 0.8f;

        private Coroutine fadeRoutine;
        private float baseVolume = 1f;

        private void Awake()
        {
            if (playbackSource == null)
            {
                playbackSource = GetComponent<AudioSource>();
            }

            if (playbackSource != null)
            {
                playbackSource.playOnAwake = false;
                playbackSource.loop = loop;
            }
        }

        private void Start()
        {
            if (playOnStart)
            {
                PlayGameplay();
            }
        }

        private void Update()
        {
            if (fadeRoutine == null)
            {
                ApplyVolume();
            }
        }

        public void PlayGameplay()
        {
            if (playbackSource == null || gameplayClip == null)
            {
                return;
            }

            StopFadeRoutine();
            playbackSource.clip = gameplayClip;
            playbackSource.loop = loop;
            ApplyVolume();
            playbackSource.Play();
        }

        /// <summary>ゲーム BGM をフェードアウトして停止する。</summary>
        public IEnumerator FadeOutAndStopRoutine(float seconds = -1f)
        {
            if (playbackSource == null || !playbackSource.isPlaying)
            {
                yield break;
            }

            float dur = seconds > 0f ? seconds : defaultFadeOutSeconds;
            StopFadeRoutine();
            bool done = false;
            fadeRoutine = StartCoroutine(FadeVolumeRoutine(
                playbackSource.volume,
                0f,
                dur,
                () =>
                {
                    playbackSource.Stop();
                    playbackSource.volume = 0f;
                    done = true;
                }));
            while (!done)
            {
                yield return null;
            }

            fadeRoutine = null;
        }

        /// <summary>ゲーム BGM を再開し、フェードインする。</summary>
        public IEnumerator ResumeGameplayRoutine(float seconds = -1f)
        {
            if (playbackSource == null || gameplayClip == null)
            {
                yield break;
            }

            float dur = seconds > 0f ? seconds : defaultFadeInSeconds;
            StopFadeRoutine();
            if (!playbackSource.isPlaying || playbackSource.clip != gameplayClip)
            {
                playbackSource.clip = gameplayClip;
                playbackSource.loop = loop;
                playbackSource.volume = 0f;
                playbackSource.Play();
            }

            float target = ResolveTargetVolume();
            bool done = false;
            fadeRoutine = StartCoroutine(FadeVolumeRoutine(
                playbackSource.volume,
                target,
                dur,
                () => done = true));
            while (!done)
            {
                yield return null;
            }

            fadeRoutine = null;
            ApplyVolume();
        }

        private IEnumerator FadeVolumeRoutine(
            float from,
            float to,
            float seconds,
            System.Action onComplete)
        {
            float dur = Mathf.Max(0.05f, seconds);
            float elapsed = 0f;
            playbackSource.volume = from;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                playbackSource.volume = Mathf.Lerp(from, to, t);
                yield return null;
            }

            playbackSource.volume = to;
            onComplete?.Invoke();
        }

        private void StopFadeRoutine()
        {
            if (fadeRoutine == null)
            {
                return;
            }

            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        private void ApplyVolume()
        {
            if (playbackSource == null)
            {
                return;
            }

            playbackSource.volume = ResolveTargetVolume();
        }

        private float ResolveTargetVolume()
        {
            baseVolume = SoundSettingsManager.Instance != null
                ? SoundSettingsManager.Instance.GetBgmGain01()
                : 1f;
            return Mathf.Clamp01(baseVolume);
        }
    }
}
