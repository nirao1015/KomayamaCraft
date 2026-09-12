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
            ApplyVolume();
        }

        public void PlayGameplay()
        {
            if (playbackSource == null || gameplayClip == null)
            {
                return;
            }

            playbackSource.clip = gameplayClip;
            playbackSource.loop = loop;
            ApplyVolume();
            playbackSource.Play();
        }

        private void ApplyVolume()
        {
            if (playbackSource == null)
            {
                return;
            }

            float volume = SoundSettingsManager.Instance != null
                ? SoundSettingsManager.Instance.GetBgmGain01()
                : 1f;
            playbackSource.volume = Mathf.Clamp01(volume);
        }
    }
}
