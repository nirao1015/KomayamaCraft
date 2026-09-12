using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public sealed class Game03DebugManager : MonoBehaviour
{
    [Header("本番リリース用")]
    [SerializeField, Tooltip("ON のときはデバッグ設定を無効化し、DebugPanel を常に非表示にする。")]
    private bool productionReleaseBuild;

    [Header("開発・デバッグ（本番リリース用が ON のとき無効）")]
    [SerializeField, Tooltip("ON のとき DebugPanel を表示する。")]
    private bool enableDebugConsole;

    [SerializeField, Tooltip("ON のときプレイヤー無敵（ダメージを受けず、ライフ0によるゲームオーバーなし）。本番ビルド（productionReleaseBuild）は無効。")]
    private bool debugInvincibleNoGameOver;

    [SerializeField, Tooltip("ON のとき敵の新規出現を行わない（本番ビルドでは無効）。")]
    private bool debugNoEnemySpawns;

    [SerializeField, Tooltip("ON のとき POD 降下〜ユニット装着までの演出を即スキップ（ダブルタップスキップ相当）。本番（productionReleaseBuild）では無効。")]
    private bool debugSkipPodDescentSequence;

    [SerializeField, Tooltip("ON のとき LvUp パネルで UG 対象武器のリロールを回数消費なしで無制限。本番（productionReleaseBuild）では無効。")]
    private bool debugInfiniteUgReroll;

    [SerializeField, Min(0f), Tooltip("生存クリアまでの秒数の開発用上書き。0 未満は 0 扱い。0 のとき Game03GameClearPresentation の survivalClearGameplaySeconds をそのまま使う。1 以上でその秒数を閾値にする。本番（productionReleaseBuild）では無効。")]
    private float debugSurvivalClearGameplaySeconds;

    [SerializeField, Tooltip("敵出現フェーズのデバッグ開始。空または 0 のとき通常（フェーズ1先頭）。2 や 02 なら EnemySpawnPhase02.json を読み、ゲーム内経過秒をそのフェーズ先頭に合わせる。本番（productionReleaseBuild）では無視（常に空扱い）。")]
    private string debugEnemySpawnStartPhase;

    [SerializeField, Tooltip("ON のときメインユニットと敵が接触した敵を武器で倒したのと同じ扱いにする（湧き確認用）。本番（productionReleaseBuild）では無効。")]
    private bool debugKillEnemyOnPlayerContact;

    [Header("参照（未設定時のみ名前検索で補完）")]
    [SerializeField, Tooltip("PanelCanvas 配下の DebugPanel。未設定なら PanelCanvas/PanelObject/DebugPanel を探索。")]
    private GameObject debugPanelRoot;
    [SerializeField, Tooltip("本番時にゲーム内倍率を x1.0 に固定するための Game03Manager。未設定で本番 ON のときのみ自動検索する。")]
    private Game03Manager game03Manager;
    [SerializeField, Tooltip("Pod デバッグ演出の実行先。未設定ならシーン上の Game03PodManager を探索。")]
    private Game03PodManager game03PodManager;
    [SerializeField, Tooltip("後方フォールオフ表示用。未設定ならシーン検索。")]
    private Game03EnemyManager game03EnemyManager;
    [SerializeField, Tooltip("DebugPanel 配下。後方フォールオフの進行ロック表示（未設定なら更新しない）。")]
    private TMP_Text rearFalloffScrollDebugText;

    private Game03CombatBalanceLogger combatBalanceLogger;

    [Header("経験値ドロップデバッグ")]
    [SerializeField, Tooltip("DebugPanel/ButtonExp。クリックで低経験値アイテムをばらまく。")]
    private Button buttonExp;
    [SerializeField, Tooltip("DebugPanel/ButtonLvUp。クリックでLvUp演出を強制開始する。")]
    private Button buttonLvUp;
    [SerializeField, Tooltip("DebugPanel/ButtonOver。クリックでゲームオーバー演出を開始する。本番（productionReleaseBuild）では無効。")]
    private Button buttonOver;
    [SerializeField, Tooltip("DebugPanel/ButtonUnitUG。クリックで UnitUG 演出を開始する。")]
    private Button buttonUnitUg;
    [SerializeField, Tooltip("DebugPanel/ButtonBossItem。クリックで BossUG 演出を開始する。")]
    private Button buttonBossItem;
    [SerializeField, Tooltip("DebugPanel/ButtonPod。クリックで通常抽選の仲間 POD 演出を開始する。")]
    private Button buttonPod;
    [SerializeField, Tooltip("DebugPanel/ButtonTargetPod。クリックで debugTargetPodType の POD 演出を開始する。")]
    private Button buttonTargetPod;
    [SerializeField, Range(2, 7), Tooltip("ButtonTargetPod 用。3 なら Weapon03（POD03）の演出。")]
    private int debugTargetPodType = 3;
    [SerializeField, Tooltip("DebugPanel/ButtonUGAttack。装備中全武器の攻撃力 UG を 1 段階。")]
    private Button buttonUgAttack;
    [SerializeField, Tooltip("DebugPanel/ButtonUGCD。装備中全武器のクールダウン UG を 1 段階。")]
    private Button buttonUgCd;
    [SerializeField, Tooltip("DebugPanel/ButtonUGArea。装備中全武器の攻撃範囲 UG を 1 段階。")]
    private Button buttonUgArea;
    [SerializeField, Tooltip("DebugPanel/ButtonUGKU。装備中全武器のノックバック UG を 1 段階。")]
    private Button buttonUgKu;
    [SerializeField, Tooltip("LvUp UG 適用先。未設定ならシーン検索。")]
    private Game03WeaponManager game03WeaponManager;
    [SerializeField, Tooltip("UnitUG 抽選・演出。未設定ならシーン検索。")]
    private Game03UnitUgManager unitUgManager;
    [SerializeField, Tooltip("BossUG 抽選・演出。未設定ならシーン検索。")]
    private Game03BossUgManager bossUgManager;
    [SerializeField, Tooltip("ゲームオーバー演出。未設定ならシーン検索。")]
    private Game03GameOverPresentation gameOverPresentation;
    [SerializeField] private Game03ExperienceFieldController experienceFieldController;
    [SerializeField] private Game03LevelUpManager levelUpManager;
    [SerializeField, Min(1)] private int debugExpSpawnCount = 20;
    [SerializeField] private float debugExpOffsetRightPx = 300f;
    [SerializeField, Min(0f)] private float debugExpSpawnRadiusPx = 120f;

    private bool EffectiveProductionReleaseBuild =>
        TitleDebugManager.ResolveProductionReleaseBuild(productionReleaseBuild);

    /// <summary>
    /// 外部参照用。実効値は本番フラグが優先される。
    /// </summary>
    public bool EffectiveEnableDebugConsole => !EffectiveProductionReleaseBuild && enableDebugConsole;

    /// <summary>本番リリースビルド用。ON のときデバッグ速度変更などが無効。</summary>
    public bool ProductionReleaseBuild => EffectiveProductionReleaseBuild;

    /// <summary>
    /// 無敵デバッグ。本番では必ず false（<see cref="productionReleaseBuild"/> が ON のとき無効）。
    /// </summary>
    public bool EffectiveDebugInvincible => !EffectiveProductionReleaseBuild && debugInvincibleNoGameOver;

    /// <summary>
    /// 敵非出現デバッグ。本番では必ず false。
    /// </summary>
    public bool EffectiveNoEnemySpawns => !EffectiveProductionReleaseBuild && debugNoEnemySpawns;

    /// <summary>
    /// POD 降下演出の即スキップ。本番では必ず false。
    /// </summary>
    public bool EffectiveSkipPodDescentSequence => !EffectiveProductionReleaseBuild && debugSkipPodDescentSequence;

    /// <summary>
    /// UG リロール無制限デバッグ。本番では必ず false。
    /// </summary>
    public bool EffectiveDebugInfiniteUgReroll => !EffectiveProductionReleaseBuild && debugInfiniteUgReroll;

    /// <summary>
    /// 生存クリアまでの実効秒数。本番では常に <paramref name="configuredSeconds"/>。
    /// 開発時は <see cref="debugSurvivalClearGameplaySeconds"/> が 1 未満なら <paramref name="configuredSeconds"/>、1 以上ならデバッグ値。
    /// </summary>
    public float GetEffectiveSurvivalClearGameplaySeconds(float configuredSeconds)
    {
        if (EffectiveProductionReleaseBuild)
        {
            return configuredSeconds;
        }

        float overrideSeconds = Mathf.Max(0f, debugSurvivalClearGameplaySeconds);
        if (overrideSeconds < 1f)
        {
            return configuredSeconds;
        }

        return overrideSeconds;
    }

    /// <summary>
    /// 本番では必ず 1。開発時のみ <see cref="debugEnemySpawnStartPhase"/> を解釈（1〜99、ファイルが無ければロード失敗ログ）。
    /// </summary>
    public int GetEffectiveEnemySpawnStartPhaseNumber()
    {
        if (EffectiveProductionReleaseBuild)
        {
            return 1;
        }

        if (!TryParseEnemySpawnPhaseToken(debugEnemySpawnStartPhase, out int phase))
        {
            return 1;
        }

        return Mathf.Clamp(phase, 1, 99);
    }

    /// <summary>
    /// 本番では必ず false。接触で敵を撃破扱いにするデバッグ。
    /// </summary>
    public bool EffectiveKillEnemyOnPlayerContact => !EffectiveProductionReleaseBuild && debugKillEnemyOnPlayerContact;

    private static bool TryParseEnemySpawnPhaseToken(string raw, out int phase)
    {
        phase = 1;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string t = raw.Trim();
        if (t == "0")
        {
            return false;
        }

        int start = 0;
        while (start < t.Length && t[start] == '0')
        {
            start++;
        }

        if (start >= t.Length)
        {
            return false;
        }

        if (!int.TryParse(t.Substring(start), NumberStyles.Integer, CultureInfo.InvariantCulture, out phase))
        {
            return false;
        }

        return phase > 0;
    }

    private void Awake()
    {
        if (EffectiveProductionReleaseBuild)
        {
            EnsureGame03ManagerForProductionClamp();
            ApplyProductionGameplayTimeClamp();
        }
        else
        {
            combatBalanceLogger = GetComponent<Game03CombatBalanceLogger>();
            if (combatBalanceLogger == null)
            {
                combatBalanceLogger = gameObject.AddComponent<Game03CombatBalanceLogger>();
            }
        }
    }

    private void EnsureGame03ManagerForProductionClamp()
    {
        if (game03Manager != null)
        {
            return;
        }

        game03Manager = FindAnyObjectByType<Game03Manager>(FindObjectsInactive.Include);
    }

    private void ApplyProductionGameplayTimeClamp()
    {
        if (game03Manager == null)
        {
            return;
        }

        game03Manager.ApplyProductionGameplayTimeClamp();
    }

    private void Start()
    {
        ApplyDebugPanelVisibility();
    }

    private void OnEnable()
    {
        RegisterDebugPanelButtonListeners();
    }

    private void OnDisable()
    {
        UnregisterDebugPanelButtonListeners();
    }

    private void OnValidate()
    {
        if (EffectiveProductionReleaseBuild)
        {
            debugNoEnemySpawns = false;
            debugSkipPodDescentSequence = false;
            debugInfiniteUgReroll = false;
            debugSurvivalClearGameplaySeconds = 0f;
            debugEnemySpawnStartPhase = string.Empty;
            debugKillEnemyOnPlayerContact = false;
            EnsureGame03ManagerForProductionClamp();
            ApplyProductionGameplayTimeClamp();
        }

        if (!Application.isPlaying)
        {
            return;
        }

        ApplyDebugPanelVisibility();
        RegisterDebugPanelButtonListeners();
    }

    private void LateUpdate()
    {
        // 本番フラグ ON のときは、外部から active 変更されても毎フレーム非表示を維持する。
        if (EffectiveProductionReleaseBuild)
        {
            ApplyDebugPanelVisibility();
            EnsureGame03ManagerForProductionClamp();
            ApplyProductionGameplayTimeClamp();
        }
    }

    private void Update()
    {
        if (!EffectiveEnableDebugConsole)
        {
            if (rearFalloffScrollDebugText != null)
            {
                rearFalloffScrollDebugText.text = string.Empty;
            }

            return;
        }

        UpdateRearFalloffDebugLabel();

        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            Game03PodManager podManager = ResolvePodManager();
            if (podManager != null)
            {
                // Matches ButtonPod debug behavior.
                podManager.DebugStartRandomEligiblePod();
            }
        }

        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
        {
            OnDebugSpawnExperienceCluster();
        }
    }

    private void RegisterDebugPanelButtonListeners()
    {
        UnregisterDebugPanelButtonListeners();
        if (EffectiveProductionReleaseBuild)
        {
            return;
        }

        ResolveOptionalDebugPanelButtonRefs();

        if (buttonExp != null)
        {
            buttonExp.onClick.AddListener(OnDebugSpawnExperienceCluster);
        }

        if (buttonLvUp != null)
        {
            buttonLvUp.onClick.AddListener(OnDebugStartLevelUp);
        }

        if (buttonOver != null)
        {
            buttonOver.onClick.AddListener(OnDebugStartGameOver);
        }

        if (buttonUnitUg != null)
        {
            buttonUnitUg.onClick.AddListener(OnDebugStartUnitUg);
        }

        if (buttonBossItem != null)
        {
            buttonBossItem.onClick.AddListener(OnDebugStartBossUg);
        }

        if (buttonPod != null)
        {
            buttonPod.onClick.AddListener(OnDebugStartRandomPod);
        }

        if (buttonTargetPod != null)
        {
            buttonTargetPod.onClick.AddListener(OnDebugStartTargetPod);
        }

        if (buttonUgAttack != null)
        {
            buttonUgAttack.onClick.AddListener(OnDebugUgAttack);
        }

        if (buttonUgCd != null)
        {
            buttonUgCd.onClick.AddListener(OnDebugUgCd);
        }

        if (buttonUgArea != null)
        {
            buttonUgArea.onClick.AddListener(OnDebugUgArea);
        }

        if (buttonUgKu != null)
        {
            buttonUgKu.onClick.AddListener(OnDebugUgKu);
        }
    }

    private void UnregisterDebugPanelButtonListeners()
    {
        if (buttonExp != null)
        {
            buttonExp.onClick.RemoveListener(OnDebugSpawnExperienceCluster);
        }

        if (buttonLvUp != null)
        {
            buttonLvUp.onClick.RemoveListener(OnDebugStartLevelUp);
        }

        if (buttonOver != null)
        {
            buttonOver.onClick.RemoveListener(OnDebugStartGameOver);
        }

        if (buttonUnitUg != null)
        {
            buttonUnitUg.onClick.RemoveListener(OnDebugStartUnitUg);
        }

        if (buttonBossItem != null)
        {
            buttonBossItem.onClick.RemoveListener(OnDebugStartBossUg);
        }

        if (buttonPod != null)
        {
            buttonPod.onClick.RemoveListener(OnDebugStartRandomPod);
        }

        if (buttonTargetPod != null)
        {
            buttonTargetPod.onClick.RemoveListener(OnDebugStartTargetPod);
        }

        if (buttonUgAttack != null)
        {
            buttonUgAttack.onClick.RemoveListener(OnDebugUgAttack);
        }

        if (buttonUgCd != null)
        {
            buttonUgCd.onClick.RemoveListener(OnDebugUgCd);
        }

        if (buttonUgArea != null)
        {
            buttonUgArea.onClick.RemoveListener(OnDebugUgArea);
        }

        if (buttonUgKu != null)
        {
            buttonUgKu.onClick.RemoveListener(OnDebugUgKu);
        }
    }

    /// <summary>DebugPanel ボタン経由のデバッグ操作が許可されるか（本番では常に false）。</summary>
    private bool CanInvokeDebugPanelActions()
    {
        return !EffectiveProductionReleaseBuild && EffectiveEnableDebugConsole;
    }

    private void OnDebugSpawnExperienceCluster()
    {
        if (!CanInvokeDebugPanelActions() || experienceFieldController == null)
        {
            return;
        }

        experienceFieldController.DebugSpawnLowTierPickupsCluster(debugExpSpawnCount, debugExpOffsetRightPx, debugExpSpawnRadiusPx);
    }

    private void OnDebugStartLevelUp()
    {
        if (!CanInvokeDebugPanelActions() || levelUpManager == null)
        {
            return;
        }

        levelUpManager.DebugStartLevelUpPresentation();
    }

    private void OnDebugStartUnitUg()
    {
        if (!CanInvokeDebugPanelActions())
        {
            return;
        }

        Game03UnitUgManager manager = ResolveUnitUgManager();
        if (manager == null)
        {
            Debug.LogWarning("[Game03DebugManager] Game03UnitUgManager が見つかりません。ButtonUnitUG は無効です。", this);
            return;
        }

        if (!manager.TryRequestPresentation(Game03UnitUgRequestSource.Debug))
        {
            Debug.LogWarning("[Game03DebugManager] UnitUG の開始に失敗しました（候補なし・演出中・ラン終了など）。", this);
        }
    }

    private void OnDebugStartBossUg()
    {
        if (!CanInvokeDebugPanelActions())
        {
            return;
        }

        Game03BossUgManager manager = ResolveBossUgManager();
        if (manager == null)
        {
            Debug.LogWarning("[Game03DebugManager] Game03BossUgManager が見つかりません。ButtonBossItem は無効です。", this);
            return;
        }

        if (!manager.TryRequestPresentation(Game03BossUgRequestSource.Debug))
        {
            Debug.LogWarning("[Game03DebugManager] BossUG の開始に失敗しました（候補なし・上限・演出中・ラン終了など）。", this);
        }
    }

    private void ResolveOptionalDebugPanelButtonRefs()
    {
        if (debugPanelRoot == null)
        {
            return;
        }

        TryResolveDebugButton("ButtonUnitUG", ref buttonUnitUg);
        TryResolveDebugButton("ButtonBossItem", ref buttonBossItem);
        TryResolveDebugButton("ButtonPod", ref buttonPod);
        TryResolveDebugButton("ButtonTargetPod", ref buttonTargetPod);
        TryResolveDebugButton("ButtonUGAttack", ref buttonUgAttack);
        TryResolveDebugButton("ButtonUGCD", ref buttonUgCd);
        TryResolveDebugButton("ButtonUGArea", ref buttonUgArea);
        TryResolveDebugButton("ButtonUGKU", ref buttonUgKu);
    }

    private void TryResolveDebugButton(string childName, ref Button target)
    {
        if (target != null)
        {
            return;
        }

        Transform found = debugPanelRoot.transform.Find(childName);
        if (found != null)
        {
            target = found.GetComponent<Button>();
        }
    }

    private void OnDebugStartRandomPod()
    {
        if (!CanInvokeDebugPanelActions())
        {
            return;
        }

        Game03PodManager podManager = ResolvePodManager();
        if (podManager == null)
        {
            Debug.LogWarning("[Game03DebugManager] Game03PodManager が見つかりません。ButtonPod は無効です。", this);
            return;
        }

        if (!podManager.DebugStartRandomEligiblePod())
        {
            Debug.LogWarning("[Game03DebugManager] ランダム POD の開始に失敗しました。", this);
        }
    }

    private void OnDebugStartTargetPod()
    {
        if (!CanInvokeDebugPanelActions())
        {
            return;
        }

        Game03PodManager podManager = ResolvePodManager();
        if (podManager == null)
        {
            Debug.LogWarning("[Game03DebugManager] Game03PodManager が見つかりません。ButtonTargetPod は無効です。", this);
            return;
        }

        int podType = Mathf.Clamp(debugTargetPodType, 2, 7);
        if (!podManager.StartPodSequence(podType))
        {
            Debug.LogWarning("[Game03DebugManager] POD" + podType.ToString(CultureInfo.InvariantCulture) + " の開始に失敗しました。", this);
        }
    }

    private void OnDebugUgAttack() => TryDebugApplyUgToAllEquipped(Game03UpgradeType.AttackPowerUp);

    private void OnDebugUgCd() => TryDebugApplyUgToAllEquipped(Game03UpgradeType.CooldownReduction);

    private void OnDebugUgArea() => TryDebugApplyUgToAllEquipped(Game03UpgradeType.AttackRangeUp);

    private void OnDebugUgKu() => TryDebugApplyUgToAllEquipped(Game03UpgradeType.KnockbackUp);

    private void TryDebugApplyUgToAllEquipped(Game03UpgradeType upgradeType)
    {
        if (!CanInvokeDebugPanelActions())
        {
            return;
        }

        Game03WeaponManager weapons = ResolveWeaponManager();
        if (weapons == null)
        {
            Debug.LogWarning("[Game03DebugManager] Game03WeaponManager が見つかりません。", this);
            return;
        }

        int applied = weapons.TryApplyLevelUpUpgradeToAllEquippedWeapons(upgradeType);
        if (applied <= 0)
        {
            Debug.LogWarning("[Game03DebugManager] UG を適用できませんでした: " + upgradeType, this);
        }
    }

    private Game03WeaponManager ResolveWeaponManager()
    {
        if (game03WeaponManager != null)
        {
            return game03WeaponManager;
        }

        game03WeaponManager = FindAnyObjectByType<Game03WeaponManager>(FindObjectsInactive.Include);
        return game03WeaponManager;
    }

    private Game03UnitUgManager ResolveUnitUgManager()
    {
        if (unitUgManager != null)
        {
            return unitUgManager;
        }

        unitUgManager = FindAnyObjectByType<Game03UnitUgManager>(FindObjectsInactive.Include);
        return unitUgManager;
    }

    private Game03BossUgManager ResolveBossUgManager()
    {
        if (bossUgManager != null)
        {
            return bossUgManager;
        }

        bossUgManager = FindAnyObjectByType<Game03BossUgManager>(FindObjectsInactive.Include);
        return bossUgManager;
    }

    private void OnDebugStartGameOver()
    {
        if (!CanInvokeDebugPanelActions())
        {
            return;
        }

        Game03GameOverPresentation presentation = ResolveGameOverPresentation();
        if (presentation == null)
        {
            Debug.LogWarning("[Game03DebugManager] Game03GameOverPresentation が見つかりません。ButtonOver は無効です。", this);
            return;
        }

        presentation.NotifyPlayerDefeated();
    }

    private Game03GameOverPresentation ResolveGameOverPresentation()
    {
        if (gameOverPresentation != null)
        {
            return gameOverPresentation;
        }

        gameOverPresentation = FindAnyObjectByType<Game03GameOverPresentation>(FindObjectsInactive.Include);
        return gameOverPresentation;
    }

    private void ApplyDebugPanelVisibility()
    {
        GameObject panel = ResolveDebugPanel();
        if (panel == null)
        {
            return;
        }

        bool wantActive = EffectiveEnableDebugConsole;
        if (panel.activeSelf != wantActive)
        {
            panel.SetActive(wantActive);
        }
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

    private Game03PodManager ResolvePodManager()
    {
        if (game03PodManager != null)
        {
            return game03PodManager;
        }

        game03PodManager = FindAnyObjectByType<Game03PodManager>(FindObjectsInactive.Include);
        return game03PodManager;
    }

    private void UpdateRearFalloffDebugLabel()
    {
        if (rearFalloffScrollDebugText == null)
        {
            return;
        }

        if (game03EnemyManager == null)
        {
            game03EnemyManager = FindAnyObjectByType<Game03EnemyManager>(FindObjectsInactive.Include);
        }

        rearFalloffScrollDebugText.text = game03EnemyManager != null
            ? game03EnemyManager.RearFalloffScrollDebugLabel
            : "進行:—";
    }
}
