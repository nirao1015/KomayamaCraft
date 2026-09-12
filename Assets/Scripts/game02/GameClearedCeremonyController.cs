using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game02
{
    /// <summary>
    /// <c>GameClearedPanel</c> 上のクリア演出シーケンス。仕様: <c>spec/game02/game_cleared_ceremony_spec.md</c>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameClearedCeremonyController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TMP_Text clearedPopularText;
        [SerializeField] private GameObject stampImageRoot;
        [SerializeField] private RectTransform floatCommentsParent;
        [SerializeField, Tooltip("未設定時は実行時に TMP のみの子を生成します。")]
        private GameObject floatCommentPrefab;
        [SerializeField] private GameObject gameReturnButtonRoot;
        [SerializeField] private GameObject gameClearButtonRoot;
        [SerializeField] private GameObject gameClearedChrObjectRoot;

        [Header("Timing")]
        [SerializeField, Tooltip("人気カウントアップ秒（unscaled）。")]
        private float clearCountUpSeconds = 2.4f;
        [SerializeField] private AnimationCurve countUpEase = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField, Tooltip("カウントアップ終了後、スタンプを表示するまでの待機秒（unscaled）。")]
        private float stampDisplayWaitSeconds;
        [SerializeField, Tooltip("スタンプ後にボタンを出すまでの待機秒（unscaled）。")]
        private float stampPostWaitSeconds = 1.8f;
        [SerializeField] private float confettiEmitMaxSeconds = 120f;

        [Header("Floating comments (Phase E)")]
        [SerializeField] private float floatSpawnIntervalMin = 0.35f;
        [SerializeField] private float floatSpawnIntervalMax = 1.05f;
        [SerializeField, Tooltip("横移動速度のベース（anchored px / 秒・unscaled）。")]
        private float floatSpeedBasePixelsPerSecond = 280f;
        [SerializeField, Tooltip("文字数あたりの速度加算（px/s）。")]
        private float floatSpeedPerCharacter = 12f;
        [SerializeField] private float floatMinAnchoredY = -220f;
        [SerializeField] private float floatMaxAnchoredY = 220f;
        [SerializeField] private int maxConcurrentFloatComments = 12;
        [SerializeField] private List<string> commentCandidates = new List<string>
        {
            "ちょっと待ってこれリアルタイムで見てるのやばすぎないか今この瞬間に100万突破したってことだよね本当にすごい",
            "最初から見てた勢なんだけどここまで来るとは正直思ってなかった本当に努力の塊って感じで感動してる",
            "これマジでやばいって普通に考えて100万人って相当だよねしかもここまで一気に来るとか異常すぎる",
            "いやこれ伝説だろこの瞬間に立ち会えたことを誇りに思うレベルでやばい",
            "ここまで積み上げてきたもの全部が今この瞬間に爆発した感じがして鳥肌が止まらない",
            "ずっと見てきたから分かるけどこの人ほんとに地道にやってきたからこの結果は当然とも言える",
            "いやちょっと待って今の流れ何が起きたの一気に数字跳ねすぎて頭追いついてないんだけど",
            "このまま終わるのもったいなさすぎるからまだ続けてほしい正直ここからが本番だと思ってる",
            "100万ってひとつのゴールではあるけどこの人の場合まだ上があるのが怖い",
            "こんな瞬間に立ち会えるなんて思ってなかったし普通に人生の思い出に残るレベル",
            "正直ここまで来るとは思ってなかったけどやっぱり結果出す人は違うなって思った",
            "いやもう語彙力なくなるレベルでやばいしか言えない本当におめでとう",
            "ちょっと今スクショ撮った一生の記念にするレベルでやばい瞬間",
            "これが本物の配信者ってやつかって感じで納得しかない",
            "この瞬間を共有できてる視聴者全員勝ち組だと思う",
            "うおおおおお",
            "100万きたあああ",
            "おめでとう！！",
            "神配信者",
            "ついにここまで",
            "伝説始まった",
            "これはすごい",
            "泣いた",
            "ガチで尊敬",
            "えぐい",
            "見届けたぞ",
            "最高かよ",
            "お祝いだああ",
            "やばすぎる",
            "これは歴史",
            "きたあああああ",
            "強すぎる",
            "配信の王",
            "ここまで来たか",
            "感動した",
            "ずっと見てた",
            "神回だった",
            "レジェンド誕生",
            "これは伝説",
            "すげええええ",
            "おめでとーーー",
            "最高の瞬間",
            "ここが頂点か",
            "まだいける",
            "すごすぎる",
            "視聴者代表して拍手",
            "やりきったな",
            "これは記録",
            "神すぎるんだが",
            "うますぎる",
            "やばい泣ける",
            "ガチでやばい",
            "つよい",
            "これが実力",
            "圧倒的だった",
            "配信の化け物",
            "ついていきます",
            "これは神",
            "完全勝利",
            "まだ続けてくれ",
            "神展開",
            "覇者すぎる",
            "世界取ったな",
            "ありがとう",
            "伝説を見た",
            "冷蔵庫のプリン全部あげるから続けて",
            "今この瞬間だけ地球止まってる説ある",
            "うちの猫も拍手してる",
            "これ親に見せるわ",
            "近所の人に教えてくる",
            "さっきまで寝てたのに完全に覚醒した",
            "これ見てないやつ損してるぞ",
            "俺が育てた（違う）",
            "電車乗り過ごしたけど許す",
            "これもう国宝でいいだろ",
            "画面の向こうで正座してる",
            "ちょっとコンビニで赤飯買ってくる",
            "心の中で鐘が鳴ってる",
            "いま空が少し光った気がした",
            "これ歴史の教科書に載るやつ",
            "もう一周して冷静になれない",
            "近くの鳩がざわついてる",
            "WiFiも祝福してる",
            "ｶﾐﾀﾞﾜ",
            "ﾊﾞｸﾊﾂｼﾀ",
            "ｺﾚﾊ ｶﾐﾃﾞｽ",
            "ﾜﾀｼ ﾄﾃﾓ ｳﾚｼｲ",
            "ｵﾒﾃﾞﾄｳ ｽｺﾞｲ",
            "ﾜﾀｼ ｺﾚ ｽｷ",
            "ﾄﾃﾓ ﾂﾖｲ ﾋﾄ",
            "ﾜﾀｼ ﾋﾞｯｸﾘ ｼﾀ",
            "ｺﾚ ﾔﾊﾞｲ ｺﾄ",
            "ﾜﾀｼ ﾐﾀ ｲﾏ",
            "ｽｺﾞｲ ﾎﾝﾄ ｽｺﾞｲ",
            "ﾜﾀｼ ｺｺ ｲﾙ",
            "ﾄﾃﾓ ｲｲ ｼｺﾞﾄ",
            "ｺﾚ ﾃﾞﾝｾﾂ ﾅﾙ",
            "ﾜﾀｼ ﾅｸ ｶﾓ",
            "ｺﾚ ﾎﾝﾄ ﾖｲ",
            "ﾜﾀｼ ﾀﾉｼｲ ｲﾏ",
            "ｽｺﾞｲ ｽｺﾞｲ ﾓｳｲｯｶｲ",
            "ﾜﾀｼ ﾘｶｲ ｼﾀ",
            "ｺﾚ ｲﾁﾊﾞﾝ ｲｲ",
            "ﾜﾀｼ ｱﾅﾀ ｽｷ",
            "ｺﾚ ﾏﾀ ﾐﾙ",
            "⊗∴⌁∵⊙",
            "∵⌘⊕",
            "⌬∴⊗∵⌁⊙",
            "∴⊙∵⌁",
            "⟁∵⌘⊗∴⊕⌁",
            "⊕∴",
            "∵⌬⊙∴⊗",
            "⌘∴⌁⊙∵⊗",
            "∴⊗",
            "⊙∵⌁⊕∴⌬",
            "⌁∵",
            "⊗⌬∴⊙∵⌘",
            "∵⊕∴⌁⊗⊙",
            "⌘⊙",
            "∴⌁⊕⌬∵⊗⌁",
            "⊗∵⌁",
        };

        [Header("Audio (count-up loop)")]
        [SerializeField, Tooltip("GameClearCountUpLoop を鳴らす用。未設定ならこの GameObject に AudioSource を追加します。")]
        private AudioSource countUpLoopAudioSource;

        private const long FallbackCeremonyStartPopularity = 100L;
        private const long FallbackCeremonyEndPopularity = 1_000_000L;
        private const float FloatCommentScreenMargin = 120f;

        private GameManager boundGameManager;
        private Coroutine ceremonyRoutine;
        private Coroutine floatSpawnRoutine;
        private Coroutine confettiCapRoutine;
        private bool cleanupDone;
        private bool celebrationPhaseActive;
        private int activeFloatCommentCount;
        private BuzzPaperConfettiController cachedConfetti;

        private static GameClearedCeremonyController TryFindController()
        {
            return FindAnyObjectByType<GameClearedCeremonyController>(FindObjectsInactive.Include);
        }

        /// <summary>Return / GameClear 遷移開始時に演出ループを止める。</summary>
        public static void TryNotifyEndingCleanupStatic()
        {
            TryFindController()?.NotifyEndingCleanup();
        }

        public void BeginCeremony(GameManager gameManager)
        {
            boundGameManager = gameManager;
            cleanupDone = false;
            celebrationPhaseActive = false;
            if (ceremonyRoutine != null)
            {
                StopCoroutine(ceremonyRoutine);
                ceremonyRoutine = null;
            }

            StopFloatSpawnRoutine();
            StopConfettiCapRoutine();
            StopCountUpLoopAudio();
            ceremonyRoutine = StartCoroutine(CoCeremonySequence());
        }

        public void NotifyEndingCleanup()
        {
            if (cleanupDone)
            {
                return;
            }

            cleanupDone = true;
            celebrationPhaseActive = false;
            StopFloatSpawnRoutine();
            StopConfettiCapRoutine();
            cachedConfetti?.StopEmittingNewPieces();
            cachedConfetti = null;
            StopCountUpLoopAudio();
            Game02BgmManager.TryGet()?.ResumeGameplayBgmAfterClearCeremony();
        }

        private IEnumerator CoCeremonySequence()
        {
            // Phase A（パネル・ポーズ・セーブフラグは GameManager 側で済んでいる想定）
            Game02BgmManager.TryGet()?.StopBgm();
            SetUiButtonsVisible(false);
            ClearFloatChildren();
            if (stampImageRoot != null)
            {
                stampImageRoot.SetActive(false);
            }

            GameManager gm = boundGameManager != null ? boundGameManager : GameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning("[GameClearedCeremony] GameManager missing; skipping ceremony.");
                SetUiButtonsVisible(true);
                ceremonyRoutine = null;
                yield break;
            }

            long startPop = gm.InitialPopularity;
            long endPop = gm.GameClearPopularityThreshold;
            // 演出は常に「初期人気 → クリア閾値」の順でカウントアップ。整合しないときは固定レンジ。
            if (startPop >= endPop)
            {
                startPop = FallbackCeremonyStartPopularity;
                endPop = FallbackCeremonyEndPopularity;
            }

            // Phase B
            ApplyPopularDisplay(startPop);
            StartCountUpLoopAudio();
            float dur = Mathf.Max(0.05f, clearCountUpSeconds);
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
                float u = Mathf.Clamp01(elapsed / dur);
                float eased = countUpEase != null && countUpEase.length > 0 ? countUpEase.Evaluate(u) : u;
                double t = Mathf.Clamp01(eased);
                double sd = startPop;
                double ed = endPop;
                long shown = (long)System.Math.Round(sd + (ed - sd) * t);
                if (shown > endPop)
                {
                    shown = endPop;
                }

                ApplyPopularDisplay(shown);
                yield return null;
            }

            ApplyPopularDisplay(endPop);
            StopCountUpLoopAudio();
            Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.GameClearReach);
            yield return WaitUnscaled(Mathf.Max(0f, stampDisplayWaitSeconds));

            // Phase C
            if (stampImageRoot != null)
            {
                stampImageRoot.SetActive(true);
            }

            Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.GameClearStamp);
            yield return WaitUnscaled(stampPostWaitSeconds);
            SetUiButtonsVisible(true);

            // Phase D + E
            celebrationPhaseActive = true;
            Game02BgmManager.TryGet()?.PlayClearCeremonyBgm();
            cachedConfetti = BuzzPaperConfettiController.EnsureSceneController();
            cachedConfetti?.PlayForSecondsWithoutBuzzPaperSe(confettiEmitMaxSeconds);
            confettiCapRoutine = StartCoroutine(CoConfettiEmitCap(confettiEmitMaxSeconds));
            floatSpawnRoutine = StartCoroutine(CoFloatSpawnLoop());

            ceremonyRoutine = null;
        }

        private IEnumerator CoConfettiEmitCap(float seconds)
        {
            float wait = Mathf.Max(0f, seconds);
            if (wait <= 0f)
            {
                yield break;
            }

            yield return WaitUnscaled(wait);
            cachedConfetti?.StopEmittingNewPieces();
            confettiCapRoutine = null;
        }

        private IEnumerator CoFloatSpawnLoop()
        {
            List<string> pool = BuildCommentPool();
            while (celebrationPhaseActive && !cleanupDone && pool.Count > 0)
            {
                if (activeFloatCommentCount < Mathf.Max(1, maxConcurrentFloatComments))
                {
                    string msg = pool[Random.Range(0, pool.Count)];
                    SpawnOneFloatComment(msg);
                }

                float gap = Random.Range(
                    Mathf.Min(floatSpawnIntervalMin, floatSpawnIntervalMax),
                    Mathf.Max(floatSpawnIntervalMin, floatSpawnIntervalMax));
                yield return WaitUnscaled(Mathf.Max(0.05f, gap));
            }

            floatSpawnRoutine = null;
        }

        private List<string> BuildCommentPool()
        {
            List<string> pool = new List<string>();
            if (commentCandidates == null)
            {
                return pool;
            }

            for (int i = 0; i < commentCandidates.Count; i++)
            {
                string s = commentCandidates[i];
                if (!string.IsNullOrWhiteSpace(s))
                {
                    pool.Add(s.Trim());
                }
            }

            return pool;
        }

        private void SpawnOneFloatComment(string message)
        {
            if (floatCommentsParent == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            RectTransform rt = InstantiateFloatCommentRect(message);
            if (rt == null)
            {
                return;
            }

            activeFloatCommentCount++;
            Canvas rootCanvas = floatCommentsParent.GetComponentInParent<Canvas>();
            float y = Random.Range(
                Mathf.Min(floatMinAnchoredY, floatMaxAnchoredY),
                Mathf.Max(floatMinAnchoredY, floatMaxAnchoredY));

            GetHorizontalExtentsInCommentParent(floatCommentsParent, rootCanvas, out float leftX, out float rightX);

            rt.anchoredPosition = new Vector2(0f, y);
            RefreshFloatCommentLayout(rt, message);

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(floatCommentsParent, rt);
            float pastRight = rightX + FloatCommentScreenMargin;
            rt.anchoredPosition = new Vector2(pastRight - bounds.min.x, y);

            float speed = Mathf.Max(10f, floatSpeedBasePixelsPerSecond + message.Length * floatSpeedPerCharacter);
            float exitPastLeft = leftX - FloatCommentScreenMargin;
            StartCoroutine(CoMoveFloatComment(rt, speed, exitPastLeft, floatCommentsParent));
        }

        /// <summary>
        /// キャンバス（なければ親の rect）の左右端を <paramref name="commentParent"/> のローカル X で返す。
        /// </summary>
        private static void GetHorizontalExtentsInCommentParent(
            RectTransform commentParent,
            Canvas canvas,
            out float leftX,
            out float rightX)
        {
            RectTransform canvasRt = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRt != null)
            {
                Vector3[] corners = new Vector3[4];
                canvasRt.GetWorldCorners(corners);
                leftX = float.PositiveInfinity;
                rightX = float.NegativeInfinity;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 local = commentParent.InverseTransformPoint(corners[i]);
                    leftX = Mathf.Min(leftX, local.x);
                    rightX = Mathf.Max(rightX, local.x);
                }

                return;
            }

            Rect pr = commentParent.rect;
            leftX = pr.xMin;
            rightX = pr.xMax;
        }

        private static void RefreshFloatCommentLayout(RectTransform rt, string message)
        {
            TextMeshProUGUI tmp = rt.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                tmp.text = message;
                tmp.ForceMeshUpdate(true);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private RectTransform InstantiateFloatCommentRect(string message)
        {
            GameObject go;
            if (floatCommentPrefab != null)
            {
                go = Instantiate(floatCommentPrefab, floatCommentsParent, false);
            }
            else
            {
                go = new GameObject("GameClearedFloatTextRuntime", typeof(RectTransform));
                go.transform.SetParent(floatCommentsParent, false);
                TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.raycastTarget = false;
                tmp.fontSize = 32f;
                tmp.text = message;
                if (clearedPopularText != null && clearedPopularText.font != null)
                {
                    tmp.font = clearedPopularText.font;
                }
            }

            TextMeshProUGUI text = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.text = message;
                text.raycastTarget = false;
            }

            RectTransform rt = go.transform as RectTransform;
            if (rt == null)
            {
                Destroy(go);
                return null;
            }

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        private IEnumerator CoMoveFloatComment(
            RectTransform rt,
            float speedPixelsPerSecond,
            float exitPastLeftX,
            RectTransform parent)
        {
            if (rt == null || parent == null)
            {
                yield break;
            }

            while (rt != null && celebrationPhaseActive && !cleanupDone)
            {
                Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, rt);
                if (b.max.x < exitPastLeftX)
                {
                    break;
                }

                Vector2 p = rt.anchoredPosition;
                p.x -= speedPixelsPerSecond * Mathf.Max(0f, Time.unscaledDeltaTime);
                rt.anchoredPosition = p;
                yield return null;
            }

            if (rt != null)
            {
                Destroy(rt.gameObject);
            }

            activeFloatCommentCount = Mathf.Max(0, activeFloatCommentCount - 1);
        }

        private void ApplyPopularDisplay(long value)
        {
            if (clearedPopularText == null)
            {
                return;
            }

            clearedPopularText.text = value.ToString("N0");
        }

        private void SetUiButtonsVisible(bool visible)
        {
            if (gameReturnButtonRoot != null)
            {
                gameReturnButtonRoot.SetActive(visible);
            }

            if (gameClearButtonRoot != null)
            {
                gameClearButtonRoot.SetActive(visible);
            }

            if (gameClearedChrObjectRoot != null)
            {
                gameClearedChrObjectRoot.SetActive(visible);
            }
        }

        private void ClearFloatChildren()
        {
            if (floatCommentsParent == null)
            {
                return;
            }

            for (int i = floatCommentsParent.childCount - 1; i >= 0; i--)
            {
                Transform c = floatCommentsParent.GetChild(i);
                if (c != null)
                {
                    Destroy(c.gameObject);
                }
            }

            activeFloatCommentCount = 0;
        }

        private void StartCountUpLoopAudio()
        {
            EnsureCountUpLoopAudioSource();
            Game02SeManager se = Game02SeManager.TryGet();
            if (countUpLoopAudioSource == null || se == null || !se.TryGetClip(Game02SeCue.GameClearCountUpLoop, out AudioClip clip) ||
                clip == null)
            {
                return;
            }

            countUpLoopAudioSource.clip = clip;
            countUpLoopAudioSource.loop = true;
            float gain = SoundSettingsManager.Instance != null ? Mathf.Clamp01(SoundSettingsManager.Instance.GetSeGain01()) : 1f;
            countUpLoopAudioSource.volume = gain;
            countUpLoopAudioSource.Play();
        }

        private void StopCountUpLoopAudio()
        {
            if (countUpLoopAudioSource == null)
            {
                return;
            }

            countUpLoopAudioSource.Stop();
            countUpLoopAudioSource.clip = null;
        }

        private void EnsureCountUpLoopAudioSource()
        {
            if (countUpLoopAudioSource != null)
            {
                return;
            }

            countUpLoopAudioSource = gameObject.AddComponent<AudioSource>();
            countUpLoopAudioSource.playOnAwake = false;
            countUpLoopAudioSource.loop = true;
        }

        private void StopFloatSpawnRoutine()
        {
            if (floatSpawnRoutine != null)
            {
                StopCoroutine(floatSpawnRoutine);
                floatSpawnRoutine = null;
            }
        }

        private void StopConfettiCapRoutine()
        {
            if (confettiCapRoutine != null)
            {
                StopCoroutine(confettiCapRoutine);
                confettiCapRoutine = null;
            }
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                remaining -= Mathf.Max(0f, Time.unscaledDeltaTime);
                yield return null;
            }
        }

        private void OnDisable()
        {
            celebrationPhaseActive = false;
            StopFloatSpawnRoutine();
            StopConfettiCapRoutine();
            StopCountUpLoopAudio();
        }
    }
}
