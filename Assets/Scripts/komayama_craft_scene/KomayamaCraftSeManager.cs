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
        ShipRepairCompleteJoy = 7,
        /// <summary>メインメニュー開閉（menu設定／バツ／Esc）。</summary>
        MainMenuToggle = 8,
        /// <summary>メインメニュー「設定」押下。</summary>
        MainMenuSettings = 9,
        /// <summary>メインメニュー「セーブしてタイトル」押下。</summary>
        MainMenuSaveTitle = 10,
        /// <summary>メインメニュー「セーブしてデスクトップ」押下〜終了待ち。</summary>
        MainMenuSaveQuit = 11,
        /// <summary>タイトルへ戻る遷移開始（フェード前）。</summary>
        TransitionToTitle = 12
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
            float unused;
            return Play(cue, out unused);
        }

        /// <summary>再生に成功したらクリップ長（秒）を返す。未設定時は false。</summary>
        public bool Play(KomayamaCraftSeCue cue, out float playedClipLengthSeconds)
        {
            playedClipLengthSeconds = 0f;
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
                playedClipLengthSeconds = Mathf.Max(0f, entries[i].clip.length);
                return true;
            }

            return false;
        }

        public static bool TryPlay(KomayamaCraftSeManager manager, KomayamaCraftSeCue cue)
        {
            float unused;
            return TryPlay(manager, cue, out unused);
        }

        public static bool TryPlay(
            KomayamaCraftSeManager manager,
            KomayamaCraftSeCue cue,
            out float playedClipLengthSeconds)
        {
            playedClipLengthSeconds = 0f;
            if (manager == null)
            {
                return false;
            }

            return manager.Play(cue, out playedClipLengthSeconds);
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
                case KomayamaCraftSeCue.MainMenuToggle:
                    return "メインメニュー開閉（menu設定／バツ／Esc）";
                case KomayamaCraftSeCue.MainMenuSettings:
                    return "メインメニュー「設定」押下";
                case KomayamaCraftSeCue.MainMenuSaveTitle:
                    return "メインメニュー「セーブしてタイトル」";
                case KomayamaCraftSeCue.MainMenuSaveQuit:
                    return "メインメニュー「セーブしてデスクトップ」〜終了待ち";
                case KomayamaCraftSeCue.TransitionToTitle:
                    return "タイトルへ戻る遷移開始（フェード前）";
                default:
                    return string.Empty;
            }
        }
#endif
    }
}
