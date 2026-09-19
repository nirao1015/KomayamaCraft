using UnityEngine;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaCraftDebugManager : MonoBehaviour
    {
        private const string NoDropPaintLayerName = "DropBlocker";
        private const string NoBuildPaintLayerName = "PlacementBlocker";

        /// <summary>デバッグ倍速の段階。自動化プレイテストからは変更しない。</summary>
        private static readonly float[] GameSpeedMultipliers = { 0.5f, 1f, 3f, 5f, 10f };

        [Header("本番")]
        [SerializeField, InspectorName("本番リリース用"), Tooltip(
            "ON のとき、下の開発用チェックはすべて無効になる。")]
        private bool productionReleaseBuild;

        [Header("開発・デバッグ（本番リリース用が ON のときは無効）")]
        [SerializeField, InspectorName("デバッグ表示を出す"), Tooltip(
            "ON のときデバッグオーバーレイを表示する。")]
        private bool showDebugOverlay = true;

        [SerializeField, InspectorName("ドロップ禁止ペイントを表示"), Tooltip(
            "ON のとき、アイテムを置けない場所のペイントをカメラに映す。")]
        private bool showNoDropPaint = true;

        [SerializeField, InspectorName("設置禁止ペイントを表示"), Tooltip(
            "ON のとき、施設を置けない場所のペイントをカメラに映す。")]
        private bool showNoBuildPaint = true;

        [SerializeField, InspectorName("ドロップ詳細ログ"), Tooltip(
            "ON のとき、ドロップ処理の詳細を Console に出す。")]
        private bool verboseDropLogging;

        [SerializeField, InspectorName("OP演出しない"), Tooltip(
            "ON のとき新規ゲームでも OP（目覚め＋会話）をスキップする。本番リリース用が ON のときは無効（常に OP あり）。")]
        private bool skipOpeningPresentation;

        [SerializeField, InspectorName("長押しで超速連打"), Tooltip(
            "ON のとき、左クリック長押しの回収・採集の連打間隔を極端に短くする。右クリックのドロップ間隔は変えない。")]
        private bool rapidHoldDrop;

        public enum DebugQuestStartPoint
        {
            None = 0,
            SituationSurvey = 1,
            RainLeak = 2
        }

        [SerializeField, HideInInspector, Tooltip("旧フィールド。KomayamaQuestDebugController へ移行済み。")]
        private DebugQuestStartPoint debugQuestStart = DebugQuestStartPoint.None;

        [SerializeField, InspectorName("クエストデバッグ"), Tooltip(
            "開始シナリオはこちらのコンポーネントで選ぶ。未設定なら子／同オブジェクトを検索。")]
        private KomayamaQuestDebugController questDebugController;

        [SerializeField, InspectorName("チュートリアル自動プレイ"), Tooltip(
            "ON のとき Play 開始で OP→初期納品まで自動プレイする。本番リリース用が ON のときは無効。OPスキップより優先して OP を通す。")]
        private bool startTutorialAutoPlay;

        [Header("ゲーム倍速（デバッグ）")]
        [SerializeField, Range(0, 4), InspectorName("初期倍速段階")]
        [Tooltip("0=0.5倍 / 1=等速 / 2=3倍 / 3=5倍 / 4=10倍。本番リリース用が ON のときは常に等速。自動化プレイテストからは変更しない。Time.timeScale 経由で生産・演出・アニメ等すべてに効く。")]
        private int initialGameSpeedStepIndex = 1;

        [Header("カメラ移動範囲（デバッグ）")]
        [SerializeField, Range(0, 4), InspectorName("地域開放（カメラ）")]
        [Tooltip(
            "0=海・全域 / 1=初期 / 2=1段階開放 / 3=2段階開放 / 4=3段階開放。\n" +
            "本番リリース用が ON のときは無効（進行段階のみ）。クエスト連動は未実装。")]
        private int cameraAreaUnlockDebug = 1;

        [Header("参照")]
        [SerializeField, Tooltip("ゲーム時間（倍速・一時停止）の正本。未設定時はシーン検索。")]
        private KomayamaGameClock gameClock;

        [SerializeField, Tooltip("デバッグ Canvas（Guide/State/Hand/Overlay 等）。未設定時は debugOverlay のみ切替。")]
        private GameObject debugCanvas;

        [SerializeField, Tooltip("デバッグオーバーレイのルート（互換用）。")]
        private GameObject debugOverlay;

        [SerializeField, Tooltip("ペイント表示の切り替えに使うゲームカメラ。")]
        private Camera gameplayCamera;

        [SerializeField, Tooltip("カメラ移動範囲のデバッグ上書き先。")]
        private KomayamaCraftCameraController cameraController;

        private int gameSpeedStepIndex = 1;

        public bool ProductionReleaseBuild => productionReleaseBuild;

        public bool VerboseDropLogging =>
            !productionReleaseBuild && verboseDropLogging;

        public bool RapidHoldDrop =>
            !productionReleaseBuild && rapidHoldDrop;

        /// <summary>デバッグで OP を飛ばすとき true。本番リリース用 ON では常に false。</summary>
        public bool SkipOpeningPresentation =>
            !productionReleaseBuild && skipOpeningPresentation;

        /// <summary>デバッグ用の開始クエスト。本番リリース用 ON では常に None。</summary>
        [System.Obsolete("KomayamaQuestDebugController.ActiveScenario を使う")]
        public DebugQuestStartPoint DebugQuestStart =>
            MapLegacyStartPoint(QuestDebugScenario);

        /// <summary>クエストデバッグの開始シナリオ。本番 ON では None。</summary>
        public KomayamaQuestDebugController.Scenario QuestDebugScenario
        {
            get
            {
                if (productionReleaseBuild)
                {
                    return KomayamaQuestDebugController.Scenario.None;
                }

                EnsureQuestDebugController();
                return questDebugController != null
                    ? questDebugController.ActiveScenario
                    : MapFromLegacy(debugQuestStart);
            }
        }

        public KomayamaQuestDebugController QuestDebugController
        {
            get
            {
                EnsureQuestDebugController();
                return questDebugController;
            }
        }

        /// <summary>チュートリアル自動プレイ開始。本番リリース用 ON では常に false。</summary>
        public bool StartTutorialAutoPlay =>
            !productionReleaseBuild && startTutorialAutoPlay;

        public int GameSpeedStepIndex =>
            Mathf.Clamp(gameSpeedStepIndex, 0, GameSpeedMultipliers.Length - 1);

        public float GameSpeedMultiplier =>
            productionReleaseBuild
                ? 1f
                : GameSpeedMultipliers[GameSpeedStepIndex];

        public bool IsGamePaused =>
            gameClock != null && gameClock.IsPaused;

        public static string FormatGameSpeedLabel(int stepIndex)
        {
            int clamped = Mathf.Clamp(stepIndex, 0, GameSpeedMultipliers.Length - 1);
            float scale = GameSpeedMultipliers[clamped];
            if (Mathf.Approximately(scale, 0.5f))
            {
                return "x0.5";
            }

            if (Mathf.Approximately(scale, 1f))
            {
                return "x1";
            }

            return "x" + Mathf.RoundToInt(scale);
        }

        public void CycleGameSpeed()
        {
            if (productionReleaseBuild)
            {
                return;
            }

            gameSpeedStepIndex = (GameSpeedStepIndex + 1) % GameSpeedMultipliers.Length;
            ApplyGameSpeed();
        }

        public void SetGameSpeedStepIndex(int stepIndex)
        {
            if (productionReleaseBuild)
            {
                return;
            }

            gameSpeedStepIndex = Mathf.Clamp(stepIndex, 0, GameSpeedMultipliers.Length - 1);
            ApplyGameSpeed();
        }

        /// <summary>デバッグ用。ゲーム時間の一時停止をトグルする。</summary>
        public void ToggleGamePause()
        {
            if (productionReleaseBuild)
            {
                return;
            }

            EnsureGameClock();
            gameClock?.TogglePaused();
        }

        public void SetGamePaused(bool paused)
        {
            if (productionReleaseBuild)
            {
                return;
            }

            EnsureGameClock();
            gameClock?.SetPaused(paused);
        }

        private void Awake()
        {
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<KomayamaCraftCameraController>();
            }

            EnsureQuestDebugController();
            EnsureGameClock();
            gameSpeedStepIndex = Mathf.Clamp(
                initialGameSpeedStepIndex,
                0,
                GameSpeedMultipliers.Length - 1);
            ApplyDebugVisibility();
            ApplyCameraAreaDebug();
            ApplyGameSpeed();
        }

        private void EnsureQuestDebugController()
        {
            if (questDebugController != null)
            {
                return;
            }

            questDebugController = GetComponent<KomayamaQuestDebugController>();
            if (questDebugController == null)
            {
                questDebugController =
                    GetComponentInChildren<KomayamaQuestDebugController>(true);
            }
        }

        private static DebugQuestStartPoint MapLegacyStartPoint(
            KomayamaQuestDebugController.Scenario scenario)
        {
            switch (scenario)
            {
                case KomayamaQuestDebugController.Scenario.SituationSurveyIntro:
                    return DebugQuestStartPoint.SituationSurvey;
                case KomayamaQuestDebugController.Scenario.RainLeakIntro:
                case KomayamaQuestDebugController.Scenario.RainLeakPreDelivery:
                    return DebugQuestStartPoint.RainLeak;
                default:
                    return DebugQuestStartPoint.None;
            }
        }

        private static KomayamaQuestDebugController.Scenario MapFromLegacy(
            DebugQuestStartPoint point)
        {
            switch (point)
            {
                case DebugQuestStartPoint.SituationSurvey:
                    return KomayamaQuestDebugController.Scenario.SituationSurveyIntro;
                case DebugQuestStartPoint.RainLeak:
                    return KomayamaQuestDebugController.Scenario.RainLeakIntro;
                default:
                    return KomayamaQuestDebugController.Scenario.None;
            }
        }

        private void OnValidate()
        {
            cameraAreaUnlockDebug = Mathf.Clamp(cameraAreaUnlockDebug, 0, 4);
            initialGameSpeedStepIndex = Mathf.Clamp(
                initialGameSpeedStepIndex,
                0,
                GameSpeedMultipliers.Length - 1);
            EnsureQuestDebugController();
            if (Application.isPlaying)
            {
                ApplyDebugVisibility();
                ApplyCameraAreaDebug();
                if (!productionReleaseBuild)
                {
                    gameSpeedStepIndex = initialGameSpeedStepIndex;
                }

                ApplyGameSpeed();
            }
        }

        private void EnsureGameClock()
        {
            if (gameClock != null)
            {
                return;
            }

            gameClock = FindFirstObjectByType<KomayamaGameClock>(FindObjectsInactive.Include);
            if (gameClock != null)
            {
                return;
            }

            gameClock = gameObject.AddComponent<KomayamaGameClock>();
        }

        private void ApplyDebugVisibility()
        {
            bool showDebug = !productionReleaseBuild && showDebugOverlay;
            if (debugCanvas != null)
            {
                debugCanvas.SetActive(showDebug);
            }
            else if (debugOverlay != null)
            {
                debugOverlay.SetActive(showDebug);
            }

            if (gameplayCamera == null)
            {
                return;
            }

            ApplyLayerVisibility(NoDropPaintLayerName, showNoDropPaint);
            ApplyLayerVisibility(NoBuildPaintLayerName, showNoBuildPaint);
        }

        private void ApplyCameraAreaDebug()
        {
            if (cameraController == null)
            {
                return;
            }

            if (productionReleaseBuild)
            {
                cameraController.SetDebugAreaOverride(null);
                return;
            }

            cameraController.SetDebugAreaOverride(cameraAreaUnlockDebug);
        }

        private void ApplyGameSpeed()
        {
            EnsureGameClock();
            if (gameClock == null)
            {
                return;
            }

            gameClock.SetSpeedMultiplier(GameSpeedMultiplier);
        }

        private void ApplyLayerVisibility(string layerName, bool show)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0 || gameplayCamera == null)
            {
                return;
            }

            if (!productionReleaseBuild && show)
            {
                gameplayCamera.cullingMask |= 1 << layer;
            }
            else
            {
                gameplayCamera.cullingMask &= ~(1 << layer);
            }
        }
    }
}
