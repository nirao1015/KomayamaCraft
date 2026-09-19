using System;
using UnityEngine;

namespace KomayamaCraft
{
    public enum KomayamaCraftSeCue
    {
        /// <summary>資源発生点の採集成功。</summary>
        Gather = 0,
        /// <summary>右クリックで地面へドロップしたとき。</summary>
        Drop = 1,
        /// <summary>地面ドロップを拾ったとき／保管から取り出したとき。</summary>
        Pickup = 2,
        /// <summary>施設・仮組・保管などへの投入成功（ゴミ箱以外）。</summary>
        Deposit = 3,
        /// <summary>加工完了で成果物が出たとき。</summary>
        ProcessingComplete = 4,
        /// <summary>操作できない／失敗したとき。</summary>
        Invalid = 5,
        /// <summary>納入ゴミ箱への捨て成功。通常の Drop は鳴らさない。</summary>
        TrashDeposit = 6,
        /// <summary>宇宙船修理完了の喜びモーション開始時（クリップ未設定なら無音）。</summary>
        ShipRepairCompleteJoy = 7
    }

    [Serializable]
    public struct KomayamaCraftSeEntry
    {
        [Tooltip("どの操作で鳴らすか（enum）")]
        public KomayamaCraftSeCue cue;

        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume;

        [Tooltip("自分用メモ：この行の音が何の操作か")]
        [TextArea(1, 3)]
        public string description;
    }

    [DisallowMultipleComponent]
    public sealed class KomayamaCraftSeManager : MonoBehaviour
    {
        [SerializeField] private AudioSource playbackSource;

        [SerializeField]
        [Tooltip("cue ごとに clip を割り当て。description に「いつ鳴るか」を書いておく")]
        private KomayamaCraftSeEntry[] entries =
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

#if UNITY_EDITOR
        [ContextMenu("Fill Default Descriptions")]
        private void FillDefaultDescriptions()
        {
            for (int i = 0; i < entries.Length; i++)
            {
                KomayamaCraftSeEntry entry = entries[i];
                if (!string.IsNullOrWhiteSpace(entry.description))
                {
                    continue;
                }

                entry.description = GetDefaultDescription(entry.cue);
                entries[i] = entry;
            }
        }

        private static string GetDefaultDescription(KomayamaCraftSeCue cue)
        {
            switch (cue)
            {
                case KomayamaCraftSeCue.Gather:
                    return "採集成功（資源発生点を左クリック／長押し）";
                case KomayamaCraftSeCue.Drop:
                    return "右クリックで地面へドロップしたとき（ゴミ箱納入では鳴らさない）";
                case KomayamaCraftSeCue.Pickup:
                    return "地面ドロップ拾い／保管からの取り出し";
                case KomayamaCraftSeCue.Deposit:
                    return "施設・仮組・保管・NPC納品口などへの投入成功（納入ゴミ箱以外）";
                case KomayamaCraftSeCue.ProcessingComplete:
                    return "加工完了で成果物が出たとき";
                case KomayamaCraftSeCue.Invalid:
                    return "操作できない／失敗したとき";
                case KomayamaCraftSeCue.TrashDeposit:
                    return "納入ゴミ箱へ捨てたとき。クリック／長押し連続でもホールド中は1回だけ";
                case KomayamaCraftSeCue.ShipRepairCompleteJoy:
                    return "宇宙船修理完了の喜びモーション開始時";
                default:
                    return string.Empty;
            }
        }
#endif
    }
}
