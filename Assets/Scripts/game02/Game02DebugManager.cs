using UnityEngine;

namespace Game02
{
    /// <summary>
    /// game02 のデバッグ・開発用フラグをまとめる。
    /// 「本番リリース用」が ON のとき、開発向けチェックは OFF として扱う（ゲーム開始演出の有効化は例外で本番では常に ON）。
    /// シーン開始時、実効設定で異星人スロット（ParsonObject / SidePanelParsonObject 直下）を非活性化できる。
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class Game02DebugManager : MonoBehaviour
    {
        [Header("本番リリース用")]
        [SerializeField, Tooltip(
            "ON のとき、下の「開発・デバッグ」のチェックがすべて ON でも OFF として扱う（誤設定・デバッグの混入防止）。")]
        private bool productionReleaseBuild;

        [Header("ゲーム開始演出（本番では常に ON / 開発時のみ次項を反映）")]
        [SerializeField, Tooltip(
            "新規開始で StageStartOverlay 演出を行うか。**本番リリース用が ON のときはこの値に関わらず必ず演出 ON**。" +
            " OFF にできるのは開発ビルド（本番フラグ OFF）のみ。")]
        private bool enableStageStartOverlaySequence = true;

        [Header("開発・デバッグ（UI初期表示 / 本番リリース用が ON のとき無効）")]
        [SerializeField, Tooltip("ON のとき開始時に DebugPanel を active にする。OFF のとき非 active。")]
        private bool debugPanelActiveAtStart;

        [Header("開発・デバッグ（仕事場 InvalidView ログ / 本番リリース用が ON のとき無効）")]
        [SerializeField, Tooltip(
            "ON のとき、`WorkStreaming` / `WorkEditor` / `WorkMovieSlotController` の InvalidView 更新で Debug.Log を出す。")]
        private bool debugLogInvalidViewRefresh;

        [Header("開発・デバッグ（セーブ後演出・クリア表示初期状態 / 本番リリース用が ON のとき無効）")]
        [SerializeField, Tooltip(
            "（セーブ読込で開始したときのみ）ON なら ApplySaveState で開始演出が無効になっていても StageStartOverlay を強制表示し、開始演出を頭から再生してデバッグする。")]
        private bool debugForceStageStartOverlayAfterSaveLoad;
        [SerializeField, Tooltip("ON のとき開始時に GameClearedPanel を active にする。OFF のとき非 active。")]
        private bool gameClearedPanelActiveAtStart;

        [SerializeField, Tooltip(
            "ON のときシーン開始後にゲームクリア演出を強制開始する（人気閾値・演出済みセーブフラグは無視）。**本番リリース用が ON のときは無効。**")]
        private bool debugForceGameClearCeremonyAtStart;

        [SerializeField, Tooltip(
            "異星人出現抑制無効化。ON のとき、シーン開始時に ParsonObject / SidePanelParsonObject 直下の対象子を非活性化しない。" +
            " OFF のとき非活性化する。**本番リリース用が ON のときはこのチェックに関わらず常に非活性化する。**")]
        private bool disableAlienAppearanceSuppression;

        [Header("参照（未設定時のみ名前検索で補完）")]
        [SerializeField, Tooltip("PanelCanvas 配下の DebugPanel。未設定なら PanelCanvas/PanelObject/DebugPanel を探索。")]
        private GameObject debugPanelRoot;
        [SerializeField, Tooltip("GameClearedPanel。未設定なら PanelCanvas/GameClearedPanel を探索。")]
        private GameObject gameClearedPanelRoot;
        [SerializeField, Tooltip("PanelCanvas/ParsonObject。異星人 UI 非活性化の親（直下の Parson* のみ対象）。未設定時のみパス探索で補完。")]
        private Transform panelParsonObjectRoot;
        [SerializeField, Tooltip(
            "SidePanelView/SidePanelParsonObject。未設定時のみ PanelCanvas/.../SidePanelParsonObject を探索して補完。")]
        private Transform sidePanelParsonObjectRoot;

        private bool EffectiveProductionReleaseBuild =>
            TitleDebugManager.ResolveProductionReleaseBuild(productionReleaseBuild);

        /// <summary>
        /// セーブ読込開始時に <see cref="GameManager"/> が参照。本番リリース用が ON のときは常に false。
        /// </summary>
        internal bool ComputeEffectiveForceStageStartOverlayAfterSaveLoad()
        {
            if (EffectiveProductionReleaseBuild)
            {
                return false;
            }

            return debugForceStageStartOverlayAfterSaveLoad;
        }

        /// <summary>
        /// 開始時にゲームクリア演出を強制するか。本番リリース用が ON のときは常に false。
        /// </summary>
        internal bool ComputeEffectiveDebugForceGameClearCeremonyAtStart()
        {
            if (EffectiveProductionReleaseBuild)
            {
                return false;
            }

            return debugForceGameClearCeremonyAtStart;
        }

        /// <summary>
        /// 新規開始で <see cref="StageStartOverlay"/> 演出を行うか。**本番リリース用が ON のときは常に true**（必ず演出）。
        /// </summary>
        internal bool ComputeEffectiveEnableStageStartOverlaySequence()
        {
            if (EffectiveProductionReleaseBuild)
            {
                return true;
            }

            return enableStageStartOverlaySequence;
        }

        /// <summary>
        /// InvalidView 更新時の詳細ログを出すか。本番リリース用が ON のときは常に false。
        /// </summary>
        internal bool ComputeEffectiveDebugLogInvalidViewRefresh()
        {
            if (EffectiveProductionReleaseBuild)
            {
                return false;
            }

            return debugLogInvalidViewRefresh;
        }

        /// <summary>
        /// <see cref="WorkStreaming"/> 等から参照。シーンに <see cref="Game02DebugManager"/> が無いときは false。
        /// </summary>
        public static bool ShouldLogInvalidViewRefresh()
        {
            Game02DebugManager dm = FindAnyObjectByType<Game02DebugManager>(FindObjectsInactive.Include);
            if (dm == null)
            {
                return false;
            }

            return dm.ComputeEffectiveDebugLogInvalidViewRefresh();
        }

        private bool EffectiveDebugPanelActiveAtStart =>
            !EffectiveProductionReleaseBuild && debugPanelActiveAtStart;

        private bool EffectiveGameClearedPanelActiveAtStart =>
            !EffectiveProductionReleaseBuild && gameClearedPanelActiveAtStart;

        private void Start()
        {
            ApplyAlienParsonChildrenInactiveAtStart();
            ApplyDebugPanelAtStart();
            ApplyGameClearedPanelAtStart();
            ApplyDebugForceGameClearCeremonyAtStart();
        }

        /// <summary>
        /// シーン開始時に異星人スロット直下を非活性にするか。<see cref="productionReleaseBuild"/> が ON のときは常に true。
        /// </summary>
        private bool ShouldApplyAlienAppearanceSuppressionAtStart()
        {
            return EffectiveProductionReleaseBuild || !disableAlienAppearanceSuppression;
        }

        private void ApplyAlienParsonChildrenInactiveAtStart()
        {
            if (!ShouldApplyAlienAppearanceSuppressionAtStart())
            {
                return;
            }

            Transform mainRoot = ResolvePanelParsonObjectRoot();
            if (mainRoot != null)
            {
                SetInactiveDirectChildrenIfNamePrefix(mainRoot, "Parson");
            }

            Transform sideRoot = ResolveSidePanelParsonObjectRoot();
            if (sideRoot != null)
            {
                SetInactiveDirectChildrenIfNamePrefix(sideRoot, "SideParson");
            }

            PersonEffectManager.NotifyParsonSlotHierarchyChanged();
        }

        private static void SetInactiveDirectChildrenIfNamePrefix(Transform root, string namePrefix)
        {
            if (root == null || string.IsNullOrEmpty(namePrefix))
            {
                return;
            }

            int count = root.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                string n = child.name;
                if (string.IsNullOrEmpty(n) || !n.StartsWith(namePrefix))
                {
                    continue;
                }

                GameObject go = child.gameObject;
                if (go.activeSelf)
                {
                    go.SetActive(false);
                }
            }
        }

        private Transform ResolvePanelParsonObjectRoot()
        {
            if (panelParsonObjectRoot != null)
            {
                return panelParsonObjectRoot;
            }

            GameObject found = GameObject.Find("PanelCanvas/ParsonObject");
            if (found != null)
            {
                panelParsonObjectRoot = found.transform;
                return panelParsonObjectRoot;
            }

            return null;
        }

        private Transform ResolveSidePanelParsonObjectRoot()
        {
            if (sidePanelParsonObjectRoot != null)
            {
                return sidePanelParsonObjectRoot;
            }

            GameObject found =
                GameObject.Find("PanelCanvas/PanelObject/SidePanelObject/SidePanelView/SidePanelParsonObject");
            if (found != null)
            {
                sidePanelParsonObjectRoot = found.transform;
                return sidePanelParsonObjectRoot;
            }

            return null;
        }

        private void ApplyDebugPanelAtStart()
        {
            GameObject panel = ResolveDebugPanel();
            if (panel == null)
            {
                return;
            }

            bool want = EffectiveDebugPanelActiveAtStart;
            if (panel.activeSelf != want)
            {
                panel.SetActive(want);
            }
        }

        private void ApplyGameClearedPanelAtStart()
        {
            GameObject panel = ResolveGameClearedPanel();
            if (panel == null)
            {
                return;
            }

            bool want = EffectiveGameClearedPanelActiveAtStart;
            if (panel.activeSelf != want)
            {
                panel.SetActive(want);
            }
        }

        private void ApplyDebugForceGameClearCeremonyAtStart()
        {
            if (!ComputeEffectiveDebugForceGameClearCeremonyAtStart())
            {
                return;
            }

            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning("[Game02DebugManager] GameManager が無いためクリア演出強制をスキップしました。");
                return;
            }

            gm.DebugForceStartGameClearCeremony();
        }

        private GameObject ResolveDebugPanel()
        {
            if (debugPanelRoot != null)
            {
                return debugPanelRoot;
            }

            GameObject found = GameObject.Find("PanelCanvas/PanelObject/DebugPanel");
            if (found != null)
            {
                debugPanelRoot = found;
                return debugPanelRoot;
            }

            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name == "DebugPanel")
                {
                    debugPanelRoot = t.gameObject;
                    return debugPanelRoot;
                }
            }

            return null;
        }

        private GameObject ResolveGameClearedPanel()
        {
            if (gameClearedPanelRoot != null)
            {
                return gameClearedPanelRoot;
            }

            GameObject found = GameObject.Find("PanelCanvas/GameClearedPanel");
            if (found != null)
            {
                gameClearedPanelRoot = found;
                return gameClearedPanelRoot;
            }

            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != "GameClearedPanel")
                {
                    continue;
                }

                Transform parent = t.parent;
                if (parent != null && parent.name == "PanelCanvas")
                {
                    gameClearedPanelRoot = t.gameObject;
                    return gameClearedPanelRoot;
                }
            }

            return null;
        }
    }
}
