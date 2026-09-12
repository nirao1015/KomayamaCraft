using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game02
{
    /// <summary>猫ボタン用 SE 1 行（<see cref="Game02SeManager"/> のリスト要素）。</summary>
    [System.Serializable]
    public struct CatMeowSeEntry
    {
        public AudioClip clip;

        [Tooltip("抽選重み。0 以下は抽選から除外。")]
        public int weight;
    }
    public enum Game02SeCue
    {
        PurchaseSuccess = 0,
        PurchaseFailure = 1,
        WorkStreamingAccepted = 2,
        WorkEditorAccepted = 3,
        TrashDrop = 4,
        BuzzPaper = 5,
        ItemBounce = 6,
        ItemGrab = 7,
        WorkEditorExtractGrab = 8,
        ItemInvalidDropRelease = 9,
        SidePanelOpen = 10,
        SidePanelClose = 11,
        PauseOn = 12,
        PauseOff = 13,
        WorkStreamingComplete = 14,
        WorkEditorComplete = 15,
        WorkUpgradeComplete = 16,
        WorkUpgradeAccepted = 17,
        WorkMovieAccepted = 18,
        WorkMovieFifoEjected = 19,
        WorkEditorAutoSearchReserved = 20,
        WorkEditorAutoCarryDropAccepted = 21,
        /// <summary>人気マイルストーン（旧 MovieRateTierStage1 と同値）。</summary>
        PopularityMilestone = 22,

        MovieRateTierStage1 = 22,
        MovieRateTierStage2 = 23,
        MovieRateTierGoal = 24,

        /// <summary>汎用メッセージ SE（旧 CommonMessage と同値）。</summary>
        GenericMessage = 25,

        CommonMessage = 25,
        MenuPanelOpen = 26,
        MenuPanelClose = 27,
        GameClearedPanelReturn = 28,
        StageStartStamp = 29,
        StageStartCharaAppear = 30,
        StageStartCharaMove = 31,
        GameClearReach = 32,
        GameClearStamp = 33,
        GameClearCountUpLoop = 34
    }

    /// <summary>
    /// game02 シーンの SE を一元管理する。Clip・AudioSource は Inspector。未設定は無音（エラーにしない）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Game02SeManager : MonoBehaviour
    {
        private static Game02SeManager instance;

        [Header("SE 再生先")]
        [SerializeField, Tooltip("SE の再生に使う AudioSource。未設定時は同じ GameObject の AudioSource を使います（無ければ無音）。")]
        private AudioSource defaultSeAudioSource;

        [Header("購入")]
        [SerializeField, Tooltip("購入成功時（WorkUpgrade 購入成立など）。")]
        private AudioClip purchaseSuccessSeClip;
        [SerializeField, Tooltip("購入失敗時（残高不足など）。")]
        private AudioClip purchaseFailureSeClip;

        [Header("仕事・編集")]
        [SerializeField, Tooltip("WorkStreaming が ItemMailChara を受け入れたとき。")]
        private AudioClip workStreamingAcceptedSeClip;
        [SerializeField, Tooltip("WorkEditor が編集者アイテムを受け入れたとき。")]
        private AudioClip workEditorAcceptedSeClip;
        [SerializeField, Tooltip("TrashDropTarget に捨てたとき。")]
        private AudioClip trashDropSeClip;
        [SerializeField, Tooltip("BuzzPaper 紙吹雪演出。")]
        private AudioClip buzzPaperSeClip;
        [SerializeField, Tooltip("非受理ドロップでアイテムが跳ね返るとき。")]
        private AudioClip itemBounceSeClip;
        [SerializeField, Tooltip("通常アイテムを掴んだとき。")]
        private AudioClip itemGrabSeClip;
        [SerializeField, Tooltip("WorkEditor から編集者を取り出して掴んだとき。")]
        private AudioClip workEditorExtractGrabSeClip;
        [SerializeField, Tooltip("無効ドロップでアイテムが戻るとき。")]
        private AudioClip itemInvalidDropReleaseSeClip;
        [SerializeField, Tooltip("サイドパネルを開いたとき。")]
        private AudioClip sidePanelOpenSeClip;
        [SerializeField, Tooltip("サイドパネルを閉じたとき。")]
        private AudioClip sidePanelCloseSeClip;
        [SerializeField, Tooltip("一時停止にしたとき。")]
        private AudioClip pauseOnSeClip;
        [SerializeField, Tooltip("一時停止から再開したとき。")]
        private AudioClip pauseOffSeClip;
        [SerializeField, Tooltip("ButtonMenu でゲームメニューパネルを開いたとき。")]
        private AudioClip menuPanelOpenSeClip;
        [SerializeField, Tooltip("GameReturnButton でゲームメニューパネルを閉じたとき。")]
        private AudioClip menuPanelCloseSeClip;
        [SerializeField, Tooltip("GameClearedPanel の GameReturnButton でパネルを閉じたとき。未設定なら無音。")]
        private AudioClip gameClearedPanelReturnSeClip;

        [Header("ゲーム開始演出（StageStartOverlay）")]
        [SerializeField, Tooltip("開始演出 SE1：OpStampImage 表示と同時に鳴らす。未設定なら無音。")]
        private AudioClip stageStartStampSeClip;
        [SerializeField, Tooltip("開始演出 SE2：ItemMailCharaImage 表示と同時に鳴らす。未設定なら無音。")]
        private AudioClip stageStartCharaAppearSeClip;
        [SerializeField, Tooltip("開始演出 SE3：ItemMailCharaImage の移動開始と同時に鳴らす。未設定なら無音。")]
        private AudioClip stageStartCharaMoveSeClip;

        [Header("ゲームクリア演出")]
        [SerializeField, Tooltip("クリアの人気カウントアップ終了時（ピタ止め時）。未設定なら無音。")]
        private AudioClip gameClearReachSeClip;
        [SerializeField, Tooltip("スタンプ表示時。未設定なら無音。")]
        private AudioClip gameClearStampSeClip;
        [SerializeField, Tooltip("カウントアップ中ループ SE（任意）。未設定なら無音。")]
        private AudioClip gameClearCountUpLoopSeClip;

        [SerializeField, Tooltip("WorkStreaming 完了時。")]
        private AudioClip workStreamingCompleteSeClip;
        [SerializeField, Tooltip("WorkEditor 完了時。")]
        private AudioClip workEditorCompleteSeClip;
        [SerializeField, Tooltip("アップグレード仕事完了時。")]
        private AudioClip workUpgradeCompleteSeClip;
        [SerializeField, Tooltip("アップグレード仕事場に ItemMailChara が入ったとき。")]
        private AudioClip workUpgradeAcceptedSeClip;
        [SerializeField, Tooltip("WorkMovie が動画を受け入れたとき。")]
        private AudioClip workMovieAcceptedSeClip;
        [SerializeField, Tooltip("WorkMovie FIFO で右端が押し出されたとき。")]
        private AudioClip workMovieFifoEjectedSeClip;
        [SerializeField, Tooltip("WorkEditor 自動捜索で予約確保に成功したとき。")]
        private AudioClip workEditorAutoSearchReservedSeClip;
        [SerializeField, Tooltip("WorkEditor 自動搬送の受け入れ成功時。")]
        private AudioClip workEditorAutoCarryDropAcceptedSeClip;

        [Header("画面遷移")]
        [SerializeField, Tooltip("Game02MenuReturnButton や GameClearButton で別シーンへ遷移を開始するときに鳴らす SE。未設定なら無音。")]
        private AudioClip transitionStartSeClip;

        [Header("メッセージ・人気マイルストーン SE")]
        [SerializeField, Tooltip("人気マイルストーン表示時（旧 Stage1/Stage2 を統合）。")]
        private AudioClip popularityMilestoneSeClip;
        [SerializeField, Tooltip("汎用メッセージ表示時（旧 Goal/Common を統合）。")]
        private AudioClip genericMessageSeClip;

        [Header("猫の鳴き声（CatButton）")]
        [SerializeField, Tooltip("重み付き抽選。clip が null または weight が 0 以下の行は無視。")]
        private List<CatMeowSeEntry> catMeowSeEntries = new List<CatMeowSeEntry>();

        [Header("デバッグ")]
        [SerializeField, Tooltip("ContextMenu の全 SE テストで次の SE まで待つ秒数。")]
        private float testPlayIntervalSeconds = 0.2f;

        private Coroutine testPlayAllCoroutine;

        private float lastCatMeowUnscaledTime = float.NegativeInfinity;

        public static Game02SeManager TryGet()
        {
            if (instance != null)
            {
                return instance;
            }

            return FindObjectOfType<Game02SeManager>(true);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (defaultSeAudioSource == null)
            {
                defaultSeAudioSource = GetComponent<AudioSource>();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public bool TryGetClip(Game02SeCue cue, out AudioClip clip)
        {
            clip = cue switch
            {
                Game02SeCue.PurchaseSuccess => purchaseSuccessSeClip,
                Game02SeCue.PurchaseFailure => purchaseFailureSeClip,
                Game02SeCue.WorkStreamingAccepted => workStreamingAcceptedSeClip,
                Game02SeCue.WorkEditorAccepted => workEditorAcceptedSeClip,
                Game02SeCue.TrashDrop => trashDropSeClip,
                Game02SeCue.BuzzPaper => buzzPaperSeClip,
                Game02SeCue.ItemBounce => itemBounceSeClip,
                Game02SeCue.ItemGrab => itemGrabSeClip,
                Game02SeCue.WorkEditorExtractGrab => workEditorExtractGrabSeClip,
                Game02SeCue.ItemInvalidDropRelease => itemInvalidDropReleaseSeClip,
                Game02SeCue.SidePanelOpen => sidePanelOpenSeClip,
                Game02SeCue.SidePanelClose => sidePanelCloseSeClip,
                Game02SeCue.PauseOn => pauseOnSeClip,
                Game02SeCue.PauseOff => pauseOffSeClip,
                Game02SeCue.MenuPanelOpen => menuPanelOpenSeClip,
                Game02SeCue.MenuPanelClose => menuPanelCloseSeClip,
                Game02SeCue.GameClearedPanelReturn => gameClearedPanelReturnSeClip,
                Game02SeCue.StageStartStamp => stageStartStampSeClip,
                Game02SeCue.StageStartCharaAppear => stageStartCharaAppearSeClip,
                Game02SeCue.StageStartCharaMove => stageStartCharaMoveSeClip,
                Game02SeCue.GameClearReach => gameClearReachSeClip,
                Game02SeCue.GameClearStamp => gameClearStampSeClip,
                Game02SeCue.GameClearCountUpLoop => gameClearCountUpLoopSeClip,
                Game02SeCue.WorkStreamingComplete => workStreamingCompleteSeClip,
                Game02SeCue.WorkEditorComplete => workEditorCompleteSeClip,
                Game02SeCue.WorkUpgradeComplete => workUpgradeCompleteSeClip,
                Game02SeCue.WorkUpgradeAccepted => workUpgradeAcceptedSeClip,
                Game02SeCue.WorkMovieAccepted => workMovieAcceptedSeClip,
                Game02SeCue.WorkMovieFifoEjected => workMovieFifoEjectedSeClip,
                Game02SeCue.WorkEditorAutoSearchReserved => workEditorAutoSearchReservedSeClip,
                Game02SeCue.WorkEditorAutoCarryDropAccepted => workEditorAutoCarryDropAcceptedSeClip,
                // MovieRateTierStage1 は PopularityMilestone と同値のため同一アームで処理
                Game02SeCue.PopularityMilestone => popularityMilestoneSeClip,
                Game02SeCue.MovieRateTierStage2 => popularityMilestoneSeClip,
                Game02SeCue.MovieRateTierGoal => genericMessageSeClip,
                // CommonMessage は GenericMessage と同値のため同一アームで処理
                Game02SeCue.GenericMessage => genericMessageSeClip,
                _ => null
            };
            return clip != null;
        }

        public bool PlayByCue(Game02SeCue cue, float baseVolume = 1f)
        {
            if (!TryGetClip(cue, out AudioClip clip))
            {
                return false;
            }

            return PlayOneShot(clip, baseVolume);
        }

        /// <summary>ゲーム遷移ボタン（Game02MenuReturnButton / GameClearButton）押下でシーン遷移を開始するときに鳴らす。</summary>
        public bool PlayTransitionStartSe(float baseVolume = 1f)
        {
            return PlayOneShot(transitionStartSeClip, baseVolume);
        }

        public bool PlayOneShot(AudioClip clip, float baseVolume = 1f)
        {
            if (clip == null)
            {
                return false;
            }

            AudioSource source = defaultSeAudioSource != null
                ? defaultSeAudioSource
                : GetComponent<AudioSource>();
            if (source == null)
            {
                return false;
            }

            float seGain = SoundSettingsManager.Instance != null
                ? Mathf.Max(0f, SoundSettingsManager.Instance.GetSeGain01())
                : 1f;

            float finalVolume = Mathf.Clamp01(Mathf.Max(0f, baseVolume) * seGain);
            if (finalVolume <= 0f)
            {
                return false;
            }

            source.PlayOneShot(clip, finalVolume);
            return true;
        }

        /// <summary>
        /// 猫ボタン用 SE。unscaled 経過が <paramref name="seIntervalSeconds"/> 未満なら再生しない。
        /// 間隔 0 以下は毎回抽選のみ行う。
        /// </summary>
        public bool TryPlayCatMeowIfIntervalAllows(float seIntervalSeconds)
        {
            if (seIntervalSeconds > 0f &&
                Time.unscaledTime - lastCatMeowUnscaledTime < seIntervalSeconds)
            {
                return false;
            }

            if (!TryPickCatMeowClip(out AudioClip clip))
            {
                return false;
            }

            if (!PlayOneShot(clip))
            {
                return false;
            }

            lastCatMeowUnscaledTime = Time.unscaledTime;
            return true;
        }

        private bool TryPickCatMeowClip(out AudioClip clip)
        {
            clip = null;
            if (catMeowSeEntries == null || catMeowSeEntries.Count == 0)
            {
                return false;
            }

            int total = 0;
            for (int i = 0; i < catMeowSeEntries.Count; i++)
            {
                CatMeowSeEntry e = catMeowSeEntries[i];
                if (e.clip != null && e.weight > 0)
                {
                    total += e.weight;
                }
            }

            if (total <= 0)
            {
                return false;
            }

            int x = Random.Range(0, total);
            int cum = 0;
            for (int i = 0; i < catMeowSeEntries.Count; i++)
            {
                CatMeowSeEntry e = catMeowSeEntries[i];
                if (e.clip == null || e.weight <= 0)
                {
                    continue;
                }

                cum += e.weight;
                if (x < cum)
                {
                    clip = e.clip;
                    return true;
                }
            }

            return false;
        }

        [ContextMenu("SE Test/購入成功")]
        private void TestPlayPurchaseSuccess()
        {
            PlayByCue(Game02SeCue.PurchaseSuccess);
        }

        [ContextMenu("SE Test/購入失敗")]
        private void TestPlayPurchaseFailure()
        {
            PlayByCue(Game02SeCue.PurchaseFailure);
        }

        [ContextMenu("SE Test/画面遷移開始")]
        private void TestPlayTransitionStart()
        {
            PlayTransitionStartSe();
        }

        [ContextMenu("SE Test/全SEを順番に再生")]
        private void TestPlayAll()
        {
            if (!Application.isPlaying)
            {
                PlayByCue(Game02SeCue.PurchaseSuccess);
                PlayByCue(Game02SeCue.PurchaseFailure);
                PlayByCue(Game02SeCue.WorkStreamingAccepted);
                PlayByCue(Game02SeCue.WorkEditorAccepted);
                PlayByCue(Game02SeCue.TrashDrop);
                PlayByCue(Game02SeCue.BuzzPaper);
                PlayByCue(Game02SeCue.ItemBounce);
                return;
            }

            if (testPlayAllCoroutine != null)
            {
                StopCoroutine(testPlayAllCoroutine);
            }

            testPlayAllCoroutine = StartCoroutine(CoTestPlayAll());
        }

        private IEnumerator CoTestPlayAll()
        {
            Game02SeCue[] cues =
            {
                Game02SeCue.PurchaseSuccess,
                Game02SeCue.PurchaseFailure,
                Game02SeCue.WorkStreamingAccepted,
                Game02SeCue.WorkEditorAccepted,
                Game02SeCue.TrashDrop,
                Game02SeCue.BuzzPaper,
                Game02SeCue.ItemBounce,
                Game02SeCue.ItemGrab,
                Game02SeCue.WorkEditorExtractGrab,
                Game02SeCue.ItemInvalidDropRelease,
                Game02SeCue.SidePanelOpen,
                Game02SeCue.SidePanelClose,
                Game02SeCue.PauseOn,
                Game02SeCue.PauseOff,
                Game02SeCue.MenuPanelOpen,
                Game02SeCue.MenuPanelClose,
                Game02SeCue.GameClearedPanelReturn,
                Game02SeCue.StageStartStamp,
                Game02SeCue.StageStartCharaAppear,
                Game02SeCue.StageStartCharaMove,
                Game02SeCue.WorkStreamingComplete,
                Game02SeCue.WorkEditorComplete,
                Game02SeCue.WorkUpgradeComplete,
                Game02SeCue.WorkUpgradeAccepted,
                Game02SeCue.WorkMovieAccepted,
                Game02SeCue.WorkMovieFifoEjected,
                Game02SeCue.WorkEditorAutoSearchReserved,
                Game02SeCue.WorkEditorAutoCarryDropAccepted,
                Game02SeCue.PopularityMilestone,
                Game02SeCue.GenericMessage
            };

            float waitSeconds = Mathf.Max(0f, testPlayIntervalSeconds);
            for (int i = 0; i < cues.Length; i++)
            {
                PlayByCue(cues[i]);
                if (waitSeconds > 0f && i < cues.Length - 1)
                {
                    yield return new WaitForSeconds(waitSeconds);
                }
            }

            if (waitSeconds > 0f)
            {
                yield return new WaitForSeconds(waitSeconds);
            }

            PlayTransitionStartSe();

            testPlayAllCoroutine = null;
        }
    }
}
