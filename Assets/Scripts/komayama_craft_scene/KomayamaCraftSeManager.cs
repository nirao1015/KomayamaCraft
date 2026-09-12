using System;
using UnityEngine;

namespace KomayamaCraft
{
    public enum KomayamaCraftSeCue
    {
        Gather,
        Drop,
        Pickup,
        Deposit,
        ProcessingComplete,
        Invalid
    }

    [Serializable]
    public struct KomayamaCraftSeEntry
    {
        public KomayamaCraftSeCue cue;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [DisallowMultipleComponent]
    public sealed class KomayamaCraftSeManager : MonoBehaviour
    {
        [SerializeField] private AudioSource playbackSource;
        [SerializeField] private KomayamaCraftSeEntry[] entries =
            Array.Empty<KomayamaCraftSeEntry>();

        public bool Play(KomayamaCraftSeCue cue)
        {
            if (playbackSource == null)
            {
                playbackSource = GetComponent<AudioSource>();
            }

            if (playbackSource == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].cue != cue || entries[i].clip == null)
                {
                    continue;
                }

                float settingsGain = SoundSettingsManager.Instance != null
                    ? SoundSettingsManager.Instance.GetSeGain01()
                    : 1f;
                float entryGain = entries[i].volume > 0f ? entries[i].volume : 1f;
                playbackSource.PlayOneShot(
                    entries[i].clip,
                    Mathf.Clamp01(settingsGain * entryGain));
                return true;
            }

            return false;
        }
    }
}
