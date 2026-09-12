using UnityEngine;

/// <summary>
/// menu03 の DebugPanel 表示とメタ進行の開発用上書き。
/// game03 の <see cref="Game03DebugManager"/> と同様、本番フラグ ON ではデバッグを無効化する。
/// </summary>
[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
public sealed class Menu03DebugManager : MonoBehaviour
{
    [Header("本番リリース用")]
    [SerializeField, Tooltip("ON のときデバッグ UI・上書き・ButtonMony は無効。セーブの可否には介入しない。")]
    private bool productionReleaseBuild;

    [Header("開発・デバッグ（本番では無効）")]
    [SerializeField, Tooltip("ON のとき DebugPanel を表示する。")]
    private bool enableDebugConsole;

    [SerializeField, Tooltip("PanelCanvas 配下の DebugPanel ルート。")]
    private GameObject debugPanelRoot;

    [SerializeField, Tooltip("メタ上書き後にセーブファイルへ書き出す（開発時のみ利用）。")]
    private bool persistMetaAfterDebugOverride;

    [Header("メタ上書き（各項目: override が OFF のときは変更しない）")]
    [SerializeField] private bool overrideMetaCurrency;
    [SerializeField] private long debugMetaCurrency;
    [SerializeField] private bool overrideAttackPowerLevel;
    [SerializeField] private int debugAttackPowerLevel;
    [SerializeField] private bool overrideAutoHealLevel;
    [SerializeField] private int debugAutoHealLevel;
    [SerializeField] private bool overrideAttackCountLevel;
    [SerializeField] private int debugAttackCountLevel;
    [SerializeField] private bool overrideSwiftnessLevel;
    [SerializeField] private int debugSwiftnessLevel;
    [SerializeField] private bool overrideRerollLevel;
    [SerializeField] private int debugRerollLevel;
    [SerializeField] private bool overrideCooldownReductionLevel;
    [SerializeField] private int debugCooldownReductionLevel;
    [SerializeField] private bool overrideTotalSpentOnUpgrades;
    [SerializeField] private long debugTotalSpentOnUpgrades;

    [SerializeField, Tooltip("デバッグ加算後に UG 表示を更新する場合に指定。")]
    private Menu03UgShopController ugShopController;

    private bool EffectiveProductionReleaseBuild =>
        TitleDebugManager.ResolveProductionReleaseBuild(productionReleaseBuild);

    /// <summary>外部参照用。実効値は本番フラグが優先される。</summary>
    public bool EffectiveEnableDebugConsole => !EffectiveProductionReleaseBuild && enableDebugConsole;

    private void Start()
    {
        ApplyDevelopmentMetaOverridesFromInspector();
        ApplyDebugPanelVisibility();
    }

    /// <summary>
    /// シーン遷移直前などに呼ぶ。本番フラグ ON または上書き項目がすべて OFF のときは何もしない。
    /// </summary>
    public void ApplyDevelopmentMetaOverridesFromInspector()
    {
        ApplyMetaOverridesFromInspectorIfDev();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ApplyDebugPanelVisibility();
    }

    private void LateUpdate()
    {
        if (EffectiveProductionReleaseBuild)
        {
            ApplyDebugPanelVisibility();
        }
    }

    /// <summary>
    /// ButtonMony から呼ぶ。本番では明示的に無効（処理しない）。
    /// </summary>
    public void OnClickDebugGrantMoney()
    {
        if (EffectiveProductionReleaseBuild)
        {
            return;
        }

        Game03MetaProgressController meta = Game03MetaProgressController.Instance;
        if (meta == null)
        {
            return;
        }

        meta.AddMetaCurrencyAndPersist(1000);
        ugShopController?.RefreshDisplay();
    }

    private void ApplyMetaOverridesFromInspectorIfDev()
    {
        if (EffectiveProductionReleaseBuild)
        {
            return;
        }

        bool any = overrideMetaCurrency || overrideAttackPowerLevel || overrideAutoHealLevel || overrideAttackCountLevel
                   || overrideSwiftnessLevel || overrideRerollLevel || overrideCooldownReductionLevel
                   || overrideTotalSpentOnUpgrades;
        if (!any)
        {
            return;
        }

        Game03MetaProgressController meta = Game03MetaProgressController.Instance;
        if (meta == null)
        {
            return;
        }

        meta.ApplyMenuDebugOverrides(
            overrideMetaCurrency ? debugMetaCurrency : (long?)null,
            overrideAttackPowerLevel ? debugAttackPowerLevel : (int?)null,
            overrideAutoHealLevel ? debugAutoHealLevel : (int?)null,
            overrideAttackCountLevel ? debugAttackCountLevel : (int?)null,
            overrideSwiftnessLevel ? debugSwiftnessLevel : (int?)null,
            overrideRerollLevel ? debugRerollLevel : (int?)null,
            overrideCooldownReductionLevel ? debugCooldownReductionLevel : (int?)null,
            overrideTotalSpentOnUpgrades ? debugTotalSpentOnUpgrades : (long?)null);

        if (persistMetaAfterDebugOverride)
        {
            meta.PersistCurrentToDisk();
        }

        ugShopController?.RefreshDisplay();
    }

    private void ApplyDebugPanelVisibility()
    {
        if (debugPanelRoot == null)
        {
            return;
        }

        bool wantActive = EffectiveEnableDebugConsole;
        if (debugPanelRoot.activeSelf != wantActive)
        {
            debugPanelRoot.SetActive(wantActive);
        }
    }
}
