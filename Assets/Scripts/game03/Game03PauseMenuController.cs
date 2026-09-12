using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tab または PanelOj.ButtonStop（pauseEnterButton）で PausePanel をトグル（開いていれば閉じて再開／閉じていれば開いてポーズ）。
/// Tab・再生ボタン（pauseResumeButton / pausePanelButtonPlay）も同様に閉じて再開可能。
/// 開始／解除 SE は <see cref="Game03SeManager"/> の PausePanel 用クリップ（開くはポーズ直後、解除は再開直後）。
/// PausePanel 内の StattusW* 行に装備武器の総合レベル・タイプ別レベルを表示する。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03PauseMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03LevelUpManager levelUpManager;
    [SerializeField] private Game03UnitUgManager unitUgManager;
    [SerializeField] private Game03BossUgManager bossUgManager;
    [SerializeField] private Game03WeaponManager weaponManager;
    [SerializeField] private Game03SeManager game03SeManager;
    [SerializeField] private Game03TransitionManager game03TransitionManager;
    [SerializeField] private RectTransform pausePanelRoot;
    [SerializeField, Tooltip("PanelOj.ButtonStop。PausePanel の開閉＝ゲーム用ポーズのトグル（Tab と同じ）。")]
    private Button pauseEnterButton;
    [SerializeField] private Button pauseResumeButton;
    [SerializeField, Tooltip("PausePanel 内の再生／閉じるボタン（例: ButtonPlay）。pauseResumeButton と別オブジェクトでも可。")]
    private Button pausePanelButtonPlay;
    [SerializeField, Tooltip("PausePanel.ButtonMenu。押下で transitionStartSe ＋フェード後に menu03_scene へ遷移。")]
    private Button pauseMenuButton;

    [Tooltip("StattusWMainOj, StattusW01Oj … StattusW04Oj の RectTransform（この順でスロット 0〜4）")]
    [SerializeField] private RectTransform[] pauseWeaponRowRoots = new RectTransform[5];

    [SerializeField, Tooltip("サブ武器行（スロット1〜4）は未装備時に非表示")]
    private bool hideSubRowsWhenSlotEmpty = true;

    private Game03WeaponStatusRowBinder.ParsedWeaponRow[] parsedRows;
    private bool weaponRowsParsed;

    /// <summary>
    /// Tab／ButtonStop で開いた「ゲームプレイ用ポーズパネル」を表示し続ける意図。
    /// 演出ポーズ（suppress）や LvUp 表示中はクリアされ、パネルも閉じる。
    /// </summary>
    private bool manualGameplayPausePanelOpen;

    private void Awake()
    {
        if (pausePanelRoot != null)
        {
            pausePanelRoot.gameObject.SetActive(false);
        }

        if (pauseEnterButton != null)
        {
            pauseEnterButton.onClick.RemoveListener(OnPauseStopButtonToggleClicked);
            pauseEnterButton.onClick.AddListener(OnPauseStopButtonToggleClicked);
        }

        if (pauseResumeButton != null)
        {
            pauseResumeButton.onClick.RemoveListener(OnPauseResumeClicked);
            pauseResumeButton.onClick.AddListener(OnPauseResumeClicked);
        }

        if (pausePanelButtonPlay != null && pausePanelButtonPlay != pauseResumeButton)
        {
            pausePanelButtonPlay.onClick.RemoveListener(OnPauseResumeClicked);
            pausePanelButtonPlay.onClick.AddListener(OnPauseResumeClicked);
        }

        if (pauseMenuButton != null)
        {
            pauseMenuButton.onClick.RemoveListener(OnPauseMenuButtonClicked);
            pauseMenuButton.onClick.AddListener(OnPauseMenuButtonClicked);
        }

        TryParseWeaponRows();
        manualGameplayPausePanelOpen = false;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleManualPausePanel();
        }
    }

    /// <summary>
    /// 他コンポーネントの Update で PausePanel が非表示にされても、本フレームの最後で意図どおりに揃える。
    /// 毎フレーム SetActive は activeSelf と希望がずれたときだけ行う。
    /// </summary>
    private void LateUpdate()
    {
        ApplyPausePanelVisibility();
        RefreshPauseWeaponRowsIfNeeded();
    }

    private void OnPauseStopButtonToggleClicked()
    {
        ToggleManualPausePanel();
    }

    private void OnPauseResumeClicked()
    {
        LeavePauseMenu();
    }

    private void OnPauseMenuButtonClicked()
    {
        if (!CanOpenPauseMenuActions() || game03TransitionManager == null)
        {
            return;
        }

        if (game03TransitionManager.IsSceneTransitionInProgress)
        {
            return;
        }

        game03TransitionManager.TransitionToMenu03FromPausePanel();
    }

    /// <summary>
    /// Tab／PanelOj.ButtonStop と同じ: PausePanel が開いていれば閉じてポーズ解除、閉じていれば開いてポーズ。
    /// </summary>
    private void ToggleManualPausePanel()
    {
        if (!CanUseManualPause())
        {
            return;
        }

        if (!game03Manager.IsPaused)
        {
            EnterPauseMenu();
            return;
        }

        LeavePauseMenu();
    }

    private bool CanUseManualPause()
    {
        return CanOpenPauseMenuActions();
    }

    private bool CanOpenPauseMenuActions()
    {
        if (game03Manager == null)
        {
            return false;
        }

        if (levelUpManager != null && levelUpManager.IsLevelUpPanelOpen)
        {
            return false;
        }

        if (unitUgManager != null && unitUgManager.IsUnitUgPanelOpen)
        {
            return false;
        }

        if (bossUgManager != null && bossUgManager.IsBossUgPanelOpen)
        {
            return false;
        }

        if (game03TransitionManager != null && game03TransitionManager.IsSceneTransitionInProgress)
        {
            return false;
        }

        return true;
    }

    private void EnterPauseMenu()
    {
        if (game03Manager == null || pausePanelRoot == null)
        {
            return;
        }

        manualGameplayPausePanelOpen = true;
        game03Manager.SetPaused(true, suppressGameplayPausePanelWhilePaused: false);

        Game03SeManager se = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();
        se?.PlayByCue(Game03SeCue.PausePanelOpen);

        pausePanelRoot.gameObject.SetActive(true);
        RefreshPauseWeaponRows();
        RefreshPausePanelHoverOverlays();
        game03TransitionManager?.ConfigureLeaveButtonPressFeedback(pauseMenuButton);
    }

    private static void RefreshPausePanelHoverOverlays()
    {
        HoverOverlayEffectManagerBase[] managers = FindObjectsByType<HoverOverlayEffectManagerBase>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            HoverOverlayEffectManagerBase manager = managers[i];
            if (manager != null)
            {
                manager.RefreshHoverOverlayMaterials();
            }
        }
    }

    private void LeavePauseMenu()
    {
        if (game03Manager == null || pausePanelRoot == null)
        {
            return;
        }

        manualGameplayPausePanelOpen = false;

        if (pausePanelRoot.gameObject.activeSelf)
        {
            pausePanelRoot.gameObject.SetActive(false);
        }

        if (!game03Manager.SuppressGameplayPausePanelWhilePaused)
        {
            Game03SeManager se = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();
            se?.PlayByCue(Game03SeCue.PausePanelResume);

            game03Manager.SetPaused(false);
        }
    }

    /// <summary>
    /// 演出ポーズ（<see cref="Game03Manager.SuppressGameplayPausePanelWhilePaused"/>）や LvUp 表示中はパネルを出さない。
    /// それ以外は「手動で開いた」またはゲームマネージャの通常ポーズ表示条件に従う。
    /// </summary>
    private void ApplyPausePanelVisibility()
    {
        if (pausePanelRoot == null || game03Manager == null)
        {
            return;
        }

        bool lvOpen = levelUpManager != null && levelUpManager.IsLevelUpPanelOpen;
        bool unitUgOpen = unitUgManager != null && unitUgManager.IsUnitUgPanelOpen;
        bool bossUgOpen = bossUgManager != null && bossUgManager.IsBossUgPanelOpen;
        bool presentationBlocking = game03Manager.SuppressGameplayPausePanelWhilePaused || lvOpen || unitUgOpen || bossUgOpen;
        if (presentationBlocking)
        {
            manualGameplayPausePanelOpen = false;
        }

        bool wantVisible = !presentationBlocking &&
            (manualGameplayPausePanelOpen || game03Manager.ShouldShowGameplayPausePanel);

        if (pausePanelRoot.gameObject.activeSelf == wantVisible)
        {
            if (wantVisible && weaponRowsParsed)
            {
                RefreshPauseWeaponRows();
            }

            return;
        }

        pausePanelRoot.gameObject.SetActive(wantVisible);

        if (wantVisible && weaponRowsParsed)
        {
            RefreshPauseWeaponRows();
        }

        RefreshPauseMenuButtonInteractable();
    }

    private void RefreshPauseMenuButtonInteractable()
    {
        if (pauseMenuButton == null)
        {
            return;
        }

        bool canUse = CanOpenPauseMenuActions()
            && pausePanelRoot != null
            && pausePanelRoot.gameObject.activeInHierarchy;
        if (pauseMenuButton.interactable != canUse)
        {
            pauseMenuButton.interactable = canUse;
        }
    }

    private void RefreshPauseWeaponRowsIfNeeded()
    {
        if (pausePanelRoot == null || !pausePanelRoot.gameObject.activeInHierarchy || weaponManager == null || !weaponRowsParsed)
        {
            return;
        }

        RefreshPauseWeaponRows();
    }

    private void RefreshPauseWeaponRows()
    {
        if (!weaponRowsParsed || weaponManager == null || parsedRows == null)
        {
            return;
        }

        Game03WeaponStatusRowBinder.RefreshRows(parsedRows, weaponManager, hideSubRowsWhenSlotEmpty, damageTracker: null);
    }

    private void TryParseWeaponRows()
    {
        parsedRows = Game03WeaponStatusRowBinder.ParseRows(pauseWeaponRowRoots, bindDamageText: false);
        weaponRowsParsed = parsedRows.Length > 0;
    }
}
