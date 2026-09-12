using System;
using System.Collections.Generic;
using Game02;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class Game03LevelUpManager : MonoBehaviour
{
    [Serializable]
    private struct UpgradeVisual
    {
        public Game03UpgradeType type;
        public Sprite sprite;
        public string displayName;
        [TextArea(2, 5)] public string description;
    }

    [Serializable]
    private struct UpgradeChoiceTextRefs
    {
        public TMP_Text ugLvText;
        public TMP_Text ugNameText;
        public TMP_Text ugDescText;
    }

    [Serializable]
    private struct UpgradeStatusLevelTextRef
    {
        public Game03UpgradeType type;
        public TMP_Text currentUgLvText;
    }

    [Serializable]
    private struct EquipWeaponViewRef
    {
        [Tooltip("0=Main, 1..4=SubUnit")]
        public int slotIndex;
        public RectTransform hitRect;
        public Image iconImage;
        [Tooltip("そのスロットに装備している武器の総合UGレベル（合計回数）")]
        public TMP_Text totalLevelText;
        [Tooltip("選択中表示する枠（任意）。シーン開始時は非表示にしておく。")]
        public GameObject selectedFrame;
        [Tooltip("sub weapon slots only. Main should stay visible.")]
        public bool hideWhenUnequipped;
    }

    [Header("References")]
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03StatusManager statusManager;
    [SerializeField] private Game03WeaponManager weaponManager;
    [SerializeField] private Game03SeManager seManager;
    [SerializeField, Tooltip("UG リロール無制限などのデバッグ参照。未設定のときは通常のリロール回数のみ。")]
    private Game03DebugManager game03DebugManager;

    [Header("LvUp Panel")]
    [SerializeField] private RectTransform lvUpPanelRoot;
    [SerializeField] private Image ugTargetAcceptedItemView;
    [SerializeField] private TMP_Text ugTargetLotteryTotalLevelText;
    [SerializeField] private Image ugChoice01AcceptedItemView;
    [SerializeField] private Image ugChoice02AcceptedItemView;
    [SerializeField] private Image ugChoice03AcceptedItemView;
    [SerializeField] private RectTransform ugChoice01HitRect;
    [SerializeField] private RectTransform ugChoice02HitRect;
    [SerializeField] private RectTransform ugChoice03HitRect;
    [SerializeField] private RectTransform ugRerollHitRect;
    [Tooltip("任意。残り回数表示用（未設定なら更新しない）。")]
    [SerializeField] private TMP_Text ugRerollRemainingText;
    [SerializeField] private GameObject buzzPaper;
    [SerializeField] private UpgradeChoiceTextRefs ugChoice01Texts;
    [SerializeField] private UpgradeChoiceTextRefs ugChoice02Texts;
    [SerializeField] private UpgradeChoiceTextRefs ugChoice03Texts;
    [SerializeField] private UpgradeStatusLevelTextRef[] ugStatusLevelTexts = Array.Empty<UpgradeStatusLevelTextRef>();
    [SerializeField] private EquipWeaponViewRef equipWeaponMainView;
    [SerializeField] private EquipWeaponViewRef equipWeapon01View;
    [SerializeField] private EquipWeaponViewRef equipWeapon02View;
    [SerializeField] private EquipWeaponViewRef equipWeapon03View;
    [SerializeField] private EquipWeaponViewRef equipWeapon04View;

    [Header("抽選・表示")]
    [SerializeField] private UpgradeVisual[] upgradeVisuals = Array.Empty<UpgradeVisual>();

    private readonly List<Game03WeaponManager.UpgradeWeaponCandidate> candidates = new List<Game03WeaponManager.UpgradeWeaponCandidate>(8);
    private readonly List<Game03UpgradeType> generatedChoices = new List<Game03UpgradeType>(3);
    private int selectedWeaponNumber;
    private int statusPreviewWeaponNumber;
    private int previousTargetWeaponNumber;
    private int selectedEquipSlotIndex = -1;
    private bool panelOpen;

    /// <summary>LvUp パネルが閉じた直後（UnitUG キュー消化など）。</summary>
    public event Action LevelUpPanelClosed;

    /// <summary>
    /// UG パネルが階層上で見えているときのみ true。<c>panelOpen</c> だけだと親 Canvas 非表示時に
    /// <see cref="Game03PauseMenuController"/> の同期で PausePanel が常に隠れる取り違えになる。
    /// </summary>
    public bool IsLevelUpPanelOpen =>
        panelOpen && lvUpPanelRoot != null && lvUpPanelRoot.gameObject.activeInHierarchy;

    /// <summary>ゲームオーバー開始時に LVup を閉じる（ポーズ状態は GameOver 側に任せる）。</summary>
    public void DismissForGameOver()
    {
        if (!panelOpen)
        {
            return;
        }

        panelOpen = false;
        SetPanelVisible(false);
    }

    private bool EffectiveUgRerollUnlimited =>
        game03DebugManager != null && game03DebugManager.EffectiveDebugInfiniteUgReroll;

    private void Awake()
    {
        HideAllEquipWeaponSelectedFrames();
        SetPanelVisible(false);
        RefreshEquipWeaponViews();
        RefreshUgRerollUi();
    }

    private void OnEnable()
    {
        if (statusManager != null)
        {
            statusManager.LevelChanged += OnLevelChanged;
        }
    }

    private void OnDisable()
    {
        if (statusManager != null)
        {
            statusManager.LevelChanged -= OnLevelChanged;
        }
    }

    private void Update()
    {
        RefreshEquipWeaponViews();

        if (panelOpen)
        {
            RefreshUgRerollUi();
        }

        if (!panelOpen || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        if (IsHit(ugChoice01HitRect, mousePos))
        {
            TrySelectUpgrade(0);
        }
        else if (IsHit(ugChoice02HitRect, mousePos))
        {
            TrySelectUpgrade(1);
        }
        else if (IsHit(ugChoice03HitRect, mousePos))
        {
            TrySelectUpgrade(2);
        }
        else if (TryHitUgReroll(mousePos))
        {
            return;
        }
        else if (TryHandleEquipWeaponPreviewClick(mousePos))
        {
            return;
        }
    }

    private void OnLevelChanged(int _)
    {
        if (panelOpen || game03Manager == null || weaponManager == null)
        {
            return;
        }

        OpenLevelUpPanel();
    }

    /// <summary>
    /// デバッグ導線: 経験値ルートを介さずにLvUp演出を強制開始する。
    /// </summary>
    public void DebugStartLevelUpPresentation()
    {
        if (panelOpen || game03Manager == null || weaponManager == null)
        {
            return;
        }

        OpenLevelUpPanel();
    }

    /// <summary>
    /// Pod 仲間加入演出後（操作再開直後）の特別 LVup。初期ターゲットは加入武器固定。UG 適用・集計は通常 LVup と同じ。
    /// </summary>
    public void OpenPodJoinLevelUpPanel(int weaponNumber)
    {
        if (panelOpen || weaponNumber <= 0 || game03Manager == null || weaponManager == null)
        {
            return;
        }

        weaponManager.GetUpgradeableWeaponCandidates(candidates);
        if (!ContainsWeaponCandidate(weaponNumber))
        {
            return;
        }

        OpenLevelUpPanelWithInitialTarget(weaponNumber);
    }

    /// <summary>
    /// 将来の「UG対象武器を手動選択するUI」から呼ぶ想定。
    /// 対象武器に応じて CurrentUGLV 群を更新する。
    /// </summary>
    public void PreviewWeaponUpgradeStatus(int weaponNumber)
    {
        if (weaponNumber <= 0 || weaponManager == null)
        {
            return;
        }

        statusPreviewWeaponNumber = weaponNumber;
        RefreshCurrentUpgradeLevelTexts(weaponNumber);
        if (panelOpen)
        {
            int slot = FindEquippedSlotIndexForWeaponNumber(weaponNumber);
            if (slot >= 0)
            {
                selectedEquipSlotIndex = slot;
                RefreshEquipWeaponSelectedFrameVisual();
            }
        }
    }

    private void OpenLevelUpPanel()
    {
        weaponManager.GetUpgradeableWeaponCandidates(candidates);
        if (candidates.Count == 0)
        {
            return;
        }

        int targetWeapon = DrawTargetWeaponNumber(candidates);
        if (targetWeapon <= 0)
        {
            return;
        }

        OpenLevelUpPanelWithInitialTarget(targetWeapon);
    }

    private void OpenLevelUpPanelWithInitialTarget(int weaponNumber)
    {
        if (weaponNumber <= 0)
        {
            return;
        }

        selectedWeaponNumber = weaponNumber;
        previousTargetWeaponNumber = selectedWeaponNumber;
        statusPreviewWeaponNumber = selectedWeaponNumber;
        GenerateChoices(selectedWeaponNumber);
        if (generatedChoices.Count == 0)
        {
            return;
        }

        selectedEquipSlotIndex = FindEquippedSlotIndexForWeaponNumber(selectedWeaponNumber);
        RefreshEquipWeaponViews();
        RefreshCurrentUpgradeLevelTexts(selectedWeaponNumber);
        ApplyChoiceVisuals();
        game03Manager.SetPaused(true, suppressGameplayPausePanelWhilePaused: true);
        SetPanelVisible(true);
        panelOpen = true;
        RefreshUgTargetLotteryTotalLevelText();
        RefreshUgRerollUi();
        RefreshEquipWeaponSelectedFrameVisual();
    }

    private bool ContainsWeaponCandidate(int weaponNumber)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].WeaponNumber == weaponNumber)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryHitUgReroll(Vector2 mousePos)
    {
        if (ugRerollHitRect == null || statusManager == null)
        {
            return false;
        }

        if (!ugRerollHitRect.gameObject.activeInHierarchy
            || (!EffectiveUgRerollUnlimited && statusManager.UgRerollChargesRemaining <= 0))
        {
            return false;
        }

        if (!IsHit(ugRerollHitRect, mousePos))
        {
            return false;
        }

        TryRerollUgTargetWeapon();
        return true;
    }

    private void TryRerollUgTargetWeapon()
    {
        if (!panelOpen || weaponManager == null || statusManager == null)
        {
            return;
        }

        if (!EffectiveUgRerollUnlimited && statusManager.UgRerollChargesRemaining <= 0)
        {
            return;
        }

        weaponManager.GetUpgradeableWeaponCandidates(candidates);
        if (candidates.Count == 0)
        {
            return;
        }

        int backupPrevTarget = previousTargetWeaponNumber;
        int backupSelected = selectedWeaponNumber;
        previousTargetWeaponNumber = selectedWeaponNumber;
        int nextWeapon = DrawTargetWeaponNumber(candidates);
        if (nextWeapon <= 0)
        {
            previousTargetWeaponNumber = backupPrevTarget;
            selectedWeaponNumber = backupSelected;
            return;
        }

        GenerateChoices(nextWeapon);
        if (generatedChoices.Count == 0)
        {
            previousTargetWeaponNumber = backupPrevTarget;
            selectedWeaponNumber = backupSelected;
            return;
        }

        if (!EffectiveUgRerollUnlimited && !statusManager.TryConsumeUgRerollCharge())
        {
            previousTargetWeaponNumber = backupPrevTarget;
            selectedWeaponNumber = backupSelected;
            GenerateChoices(selectedWeaponNumber);
            return;
        }

        selectedWeaponNumber = nextWeapon;
        previousTargetWeaponNumber = selectedWeaponNumber;
        statusPreviewWeaponNumber = selectedWeaponNumber;
        selectedEquipSlotIndex = FindEquippedSlotIndexForWeaponNumber(selectedWeaponNumber);
        RefreshCurrentUpgradeLevelTexts(selectedWeaponNumber);
        ApplyChoiceVisuals();
        RefreshUgTargetLotteryTotalLevelText();
        RefreshUgRerollUi();
        RefreshEquipWeaponSelectedFrameVisual();
    }

    private void RefreshUgRerollUi()
    {
        if (statusManager == null)
        {
            return;
        }

        int remaining = statusManager.UgRerollChargesRemaining;
        bool show = panelOpen && (EffectiveUgRerollUnlimited || remaining > 0);
        if (ugRerollHitRect != null && ugRerollHitRect.gameObject.activeSelf != show)
        {
            ugRerollHitRect.gameObject.SetActive(show);
        }

        if (ugRerollRemainingText != null)
        {
            if (!show)
            {
                ugRerollRemainingText.text = string.Empty;
            }
            else if (EffectiveUgRerollUnlimited)
            {
                ugRerollRemainingText.text = "\u221E";
            }
            else
            {
                ugRerollRemainingText.text = remaining.ToString();
            }
        }
    }

    private int DrawTargetWeaponNumber(List<Game03WeaponManager.UpgradeWeaponCandidate> list)
    {
        if (list.Count == 1)
        {
            return list[0].WeaponNumber;
        }

        List<int> selectable = new List<int>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            int n = list[i].WeaponNumber;
            if (n != previousTargetWeaponNumber)
            {
                selectable.Add(n);
            }
        }

        if (selectable.Count == 0)
        {
            for (int i = 0; i < list.Count; i++)
            {
                selectable.Add(list[i].WeaponNumber);
            }
        }

        return selectable[UnityEngine.Random.Range(0, selectable.Count)];
    }

    /// <summary>
    /// 仕様: spec/game03/武器ごとのUG.txt 抽選プール方式。
    /// 通常枠の各 UG を3枚・アンコモンを2枚・レアを1枚ずつ「同一プール」に入れ、チケットを同確率で最大3回抽選（選んだUGは重複しない）。
    /// </summary>
    private void GenerateChoices(int weaponNumber)
    {
        generatedChoices.Clear();
        HashSet<Game03UpgradeType> used = new HashSet<Game03UpgradeType>();
        List<Game03UpgradeType> bag = Game03WeaponUpgradePools.BuildCombinedUgChoiceBag(weaponNumber);
        var eligibleScratch = new List<Game03UpgradeType>(bag.Count);

        for (int pick = 0; pick < 3; pick++)
        {
            eligibleScratch.Clear();
            for (int i = 0; i < bag.Count; i++)
            {
                Game03UpgradeType t = bag[i];
                if (!used.Contains(t))
                {
                    eligibleScratch.Add(t);
                }
            }

            if (eligibleScratch.Count == 0)
            {
                break;
            }

            Game03UpgradeType chosen = eligibleScratch[UnityEngine.Random.Range(0, eligibleScratch.Count)];
            generatedChoices.Add(chosen);
            used.Add(chosen);
        }
    }

    private static bool IsHit(RectTransform rect, Vector2 screenPos)
    {
        if (rect == null)
        {
            return false;
        }

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, cam);
    }

    private void TrySelectUpgrade(int index)
    {
        if (index < 0 || index >= generatedChoices.Count || statusManager == null || weaponManager == null)
        {
            return;
        }

        Game03UpgradeType type = generatedChoices[index];
        if (!weaponManager.ApplyLevelUpUpgrade(selectedWeaponNumber, type))
        {
            return;
        }

        statusManager.RegisterWeaponUpgrade($"Weapon{selectedWeaponNumber:00}");
        seManager?.PlayByCue(Game03SeCue.LvUpPanelSelect);

        panelOpen = false;
        RefreshUgTargetLotteryTotalLevelText();
        RefreshUgRerollUi();
        SetPanelVisible(false);
        game03Manager?.SetPaused(false);
        LevelUpPanelClosed?.Invoke();
    }

    private void ApplyChoiceVisuals()
    {
        Sprite targetSprite = null;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].WeaponNumber == selectedWeaponNumber)
            {
                targetSprite = candidates[i].WeaponSprite;
                break;
            }
        }

        SetImageSprite(ugTargetAcceptedItemView, targetSprite);
        SetImageSprite(ugChoice01AcceptedItemView, generatedChoices.Count > 0 ? GetUpgradeSprite(generatedChoices[0]) : targetSprite);
        SetImageSprite(ugChoice02AcceptedItemView, generatedChoices.Count > 1 ? GetUpgradeSprite(generatedChoices[1]) : targetSprite);
        SetImageSprite(ugChoice03AcceptedItemView, generatedChoices.Count > 2 ? GetUpgradeSprite(generatedChoices[2]) : targetSprite);
        ApplyChoiceText(ugChoice01Texts, generatedChoices.Count > 0 ? generatedChoices[0] : (Game03UpgradeType?)null);
        ApplyChoiceText(ugChoice02Texts, generatedChoices.Count > 1 ? generatedChoices[1] : (Game03UpgradeType?)null);
        ApplyChoiceText(ugChoice03Texts, generatedChoices.Count > 2 ? generatedChoices[2] : (Game03UpgradeType?)null);
    }

    private bool TryHandleEquipWeaponPreviewClick(Vector2 mousePos)
    {
        return TryHandleEquipWeaponPreviewClick(equipWeaponMainView, mousePos)
               || TryHandleEquipWeaponPreviewClick(equipWeapon01View, mousePos)
               || TryHandleEquipWeaponPreviewClick(equipWeapon02View, mousePos)
               || TryHandleEquipWeaponPreviewClick(equipWeapon03View, mousePos)
               || TryHandleEquipWeaponPreviewClick(equipWeapon04View, mousePos);
    }

    private bool TryHandleEquipWeaponPreviewClick(EquipWeaponViewRef view, Vector2 mousePos)
    {
        if (view.hitRect == null || !view.hitRect.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!IsHit(view.hitRect, mousePos) || weaponManager == null)
        {
            return false;
        }

        if (!weaponManager.TryGetEquippedWeaponForSlot(view.slotIndex, out int weaponNumber, out _))
        {
            return false;
        }

        PreviewWeaponUpgradeStatus(weaponNumber);
        selectedEquipSlotIndex = view.slotIndex;
        RefreshEquipWeaponSelectedFrameVisual();
        return true;
    }

    private void SetPanelVisible(bool visible)
    {
        if (!visible)
        {
            selectedEquipSlotIndex = -1;
            HideAllEquipWeaponSelectedFrames();
        }

        if (lvUpPanelRoot != null)
        {
            lvUpPanelRoot.gameObject.SetActive(visible);
            if (visible)
            {
                lvUpPanelRoot.SetAsLastSibling();
            }
        }

        if (buzzPaper != null)
        {
            if (visible)
            {
                buzzPaper.SetActive(true);
                BuzzPaperConfettiController confetti = buzzPaper.GetComponent<BuzzPaperConfettiController>();
                if (confetti != null)
                {
                    // Keep emitting while LvUp panel is open.
                    confetti.PlayForSecondsWithoutBuzzPaperSe(999f);
                }
            }
            else
            {
                BuzzPaperConfettiController confetti = buzzPaper.GetComponent<BuzzPaperConfettiController>();
                if (confetti != null)
                {
                    confetti.StopEmittingNewPieces();
                }

                buzzPaper.SetActive(false);
            }
        }

        if (visible)
        {
            seManager?.PlayByCue(Game03SeCue.LvUpPanelOpen);
        }
    }

    private void SetImageSprite(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        if (sprite != null)
        {
            image.sprite = sprite;
            image.enabled = true;
        }
    }

    private Sprite GetUpgradeSprite(Game03UpgradeType type)
    {
        for (int i = 0; i < upgradeVisuals.Length; i++)
        {
            if (upgradeVisuals[i].type == type)
            {
                return upgradeVisuals[i].sprite;
            }
        }

        return null;
    }

    private void RefreshCurrentUpgradeLevelTexts(int weaponNumber)
    {
        if (weaponManager == null)
        {
            return;
        }

        for (int i = 0; i < ugStatusLevelTexts.Length; i++)
        {
            TMP_Text levelText = ugStatusLevelTexts[i].currentUgLvText;
            if (levelText == null)
            {
                continue;
            }

            Game03UpgradeType ut = ugStatusLevelTexts[i].type;
            bool eligible = Game03WeaponManager.IsUpgradeTypeInWeaponLotteryPool(weaponNumber, ut);
            if (!eligible)
            {
                levelText.text = weaponManager.NotLotteryEligibleUpgradeLevelDisplay;
                continue;
            }

            int lv = weaponManager.GetUpgradeCountForWeaponType(weaponNumber, ut);
            levelText.text = lv.ToString();
        }
    }

    private void RefreshEquipWeaponViews()
    {
        RefreshEquipWeaponView(equipWeaponMainView);
        RefreshEquipWeaponView(equipWeapon01View);
        RefreshEquipWeaponView(equipWeapon02View);
        RefreshEquipWeaponView(equipWeapon03View);
        RefreshEquipWeaponView(equipWeapon04View);
        if (panelOpen)
        {
            RefreshEquipWeaponSelectedFrameVisual();
        }
    }

    private void RefreshEquipWeaponView(EquipWeaponViewRef view)
    {
        if (view.hitRect == null)
        {
            return;
        }

        Sprite sprite = null;
        int weaponNumber = 0;
        bool isEquipped = weaponManager != null && weaponManager.TryGetEquippedWeaponForSlot(view.slotIndex, out weaponNumber, out sprite);
        bool shouldShow = !view.hideWhenUnequipped || isEquipped;
        if (view.hitRect.gameObject.activeSelf != shouldShow)
        {
            view.hitRect.gameObject.SetActive(shouldShow);
        }

        if (view.iconImage != null && sprite != null)
        {
            view.iconImage.sprite = sprite;
            view.iconImage.enabled = true;
        }
        ApplySpriteToHoverOverlayIfPresent(view.hitRect, sprite);

        if (view.totalLevelText != null && weaponManager != null)
        {
            view.totalLevelText.text = isEquipped && weaponNumber > 0
                ? weaponManager.GetTotalUpgradeCountForWeapon(weaponNumber).ToString()
                : "0";
        }
    }

    private void RefreshUgTargetLotteryTotalLevelText()
    {
        if (ugTargetLotteryTotalLevelText == null || weaponManager == null)
        {
            return;
        }

        if (!panelOpen || selectedWeaponNumber <= 0)
        {
            ugTargetLotteryTotalLevelText.text = string.Empty;
            return;
        }

        ugTargetLotteryTotalLevelText.text = weaponManager.GetTotalUpgradeCountForWeapon(selectedWeaponNumber).ToString();
    }

    private int FindEquippedSlotIndexForWeaponNumber(int weaponNumber)
    {
        if (weaponManager == null || weaponNumber <= 0)
        {
            return -1;
        }

        for (int slot = 0; slot <= 4; slot++)
        {
            if (weaponManager.TryGetEquippedWeaponForSlot(slot, out int n, out _) && n == weaponNumber)
            {
                return slot;
            }
        }

        return -1;
    }

    private void HideAllEquipWeaponSelectedFrames()
    {
        SetSelectedFrameActive(equipWeaponMainView, false);
        SetSelectedFrameActive(equipWeapon01View, false);
        SetSelectedFrameActive(equipWeapon02View, false);
        SetSelectedFrameActive(equipWeapon03View, false);
        SetSelectedFrameActive(equipWeapon04View, false);
    }

    private static void SetSelectedFrameActive(EquipWeaponViewRef view, bool active)
    {
        if (view.selectedFrame != null && view.selectedFrame.activeSelf != active)
        {
            view.selectedFrame.SetActive(active);
        }
    }

    private void RefreshEquipWeaponSelectedFrameVisual()
    {
        SetSelectedFrameActive(equipWeaponMainView, panelOpen && selectedEquipSlotIndex == equipWeaponMainView.slotIndex);
        SetSelectedFrameActive(equipWeapon01View, panelOpen && selectedEquipSlotIndex == equipWeapon01View.slotIndex);
        SetSelectedFrameActive(equipWeapon02View, panelOpen && selectedEquipSlotIndex == equipWeapon02View.slotIndex);
        SetSelectedFrameActive(equipWeapon03View, panelOpen && selectedEquipSlotIndex == equipWeapon03View.slotIndex);
        SetSelectedFrameActive(equipWeapon04View, panelOpen && selectedEquipSlotIndex == equipWeapon04View.slotIndex);
    }

    private static void ApplySpriteToHoverOverlayIfPresent(RectTransform hitRect, Sprite sprite)
    {
        if (hitRect == null || sprite == null)
        {
            return;
        }

        Image[] images = hitRect.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            if (image.name.IndexOf("HoverOverlay", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                image.sprite = sprite;
                image.enabled = true;
            }
        }
    }

    private void ApplyChoiceText(UpgradeChoiceTextRefs refs, Game03UpgradeType? type)
    {
        if (!type.HasValue)
        {
            SetChoiceTexts(refs, string.Empty, string.Empty, string.Empty);
            return;
        }

        Game03UpgradeType upgradeType = type.Value;
        int currentLv = weaponManager != null
            ? weaponManager.GetUpgradeCountForWeaponType(selectedWeaponNumber, upgradeType)
            : 0;

        string name = GetUpgradeDisplayName(upgradeType);
        string desc = GetUpgradeDescription(upgradeType);
        SetChoiceTexts(refs, currentLv.ToString(), name, desc);
    }

    private static void SetChoiceTexts(UpgradeChoiceTextRefs refs, string lv, string name, string desc)
    {
        if (refs.ugLvText != null)
        {
            refs.ugLvText.text = lv;
        }

        if (refs.ugNameText != null)
        {
            refs.ugNameText.text = name;
        }

        if (refs.ugDescText != null)
        {
            refs.ugDescText.text = desc;
        }
    }

    private string GetUpgradeDisplayName(Game03UpgradeType type)
    {
        for (int i = 0; i < upgradeVisuals.Length; i++)
        {
            if (upgradeVisuals[i].type == type)
            {
                return upgradeVisuals[i].displayName;
            }
        }

        return type.ToString();
    }

    private string GetUpgradeDescription(Game03UpgradeType type)
    {
        for (int i = 0; i < upgradeVisuals.Length; i++)
        {
            if (upgradeVisuals[i].type == type)
            {
                return upgradeVisuals[i].description;
            }
        }

        return string.Empty;
    }

}
