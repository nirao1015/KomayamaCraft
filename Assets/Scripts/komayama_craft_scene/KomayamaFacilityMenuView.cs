using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// 施設メニュー（レシピ選択／素材・生産品）。仮UI。SystemCanvas 配下に置く。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaFacilityMenuView : MonoBehaviour
    {
        public const int MaxIngredientDisplay = 6;

        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform recipePanel;
        [SerializeField] private RectTransform statusPanel;
        [SerializeField] private Transform recipeGrid;
        [SerializeField] private TMP_Text recipeDetailTitle;
        [SerializeField] private TMP_Text recipeDetailBody;
        [SerializeField] private Image recipeDetailIcon;
        [Tooltip("ホバー中レシピの必要素材表示（IngredientsSlot1〜6）。")]
        [SerializeField] private RectTransform ingredientOj;
        [Tooltip("ホバー時見た目テンプレート。画像差し替えはここの Image を変えれば反映。")]
        [SerializeField] private RectTransform recipeSlotSelectedTemplate;
        [Tooltip("未開放見た目テンプレート。Icon 画像はテンプレート側をそのまま使う。")]
        [SerializeField] private RectTransform recipeSlotDisabledTemplate;
        [SerializeField] private TMP_Text statusTitle;
        [SerializeField] private TMP_Text statusBody;
        [SerializeField] private TMP_Text statusMessage;
        [SerializeField] private Image statusProductIcon;
        [SerializeField] private Image statusProgressFill;
        [SerializeField] private Slider statusProgress;
        [SerializeField] private Button closeRecipeButton;
        [SerializeField] private Button closeStatusButton;
        [SerializeField] private Button changeRecipeButton;
        [SerializeField] private Button ejectButton;
        [SerializeField] private TMP_FontAsset menuFont;
        [Tooltip("使用素材 1〜6 個用の見た目ルート。位置はコードで動かさない（Inspector で調整）。")]
        [SerializeField] private RectTransform[] ingredientLayouts = new RectTransform[MaxIngredientDisplay];

        [Header("進捗画面（facility_menu_*）")]
        [SerializeField] private Sprite statusPanelSprite;
        [SerializeField] private Sprite statusBorderSprite;
        [SerializeField] private Sprite progressBackgroundSprite;
        [SerializeField] private Sprite progressBarSprite;
        [SerializeField] private Sprite progressFrameSprite;
        [SerializeField] private Sprite craftArrowSprite;
        [SerializeField] private Sprite closeButtonSprite;
        [SerializeField] private Sprite changeRecipeButtonSprite;
        [SerializeField] private Sprite materialSlotFrameSprite;
        [SerializeField] private Sprite outputSlotFrameSprite;
        [SerializeField] private KomayamaCraftSeManager seManager;

        private KomayamaProcessingFacility current;
        private RecipeDefinition previewRecipe;
        [SerializeField] private RectTransform contentRoot;
        private RectTransform craftRowRoot;
        private RectTransform craftOutputSlot;
        private Image craftArrowImage;
        private TMP_Text craftArrowText;
        private Image statusProgressBackground;
        private Image statusProgressFrame;
        private Image statusRecipeProgressBorder;
        [SerializeField] private TMP_Text statusTimeText;
        private IngredientSlotView[][] ingredientSlotsByCount;
        private IngredientSlotView[] recipeIngredientSlots;
        private readonly List<RecipeSlotBinding> recipeSlotBindings = new();
        private static Sprite uiWhiteSprite;
        private static Texture2D uiWhiteTexture;

        private sealed class IngredientSlotView
        {
            public Image Icon;
            public TMP_Text Count;
        }

        private sealed class RecipeSlotBinding
        {
            public Button Button;
            public Image Background;
            public Image Icon;
            public Image SelectedOverlay;
            public Image DisabledOverlay;
            public RecipeDefinition Recipe;
            public bool Unlocked;
            public Sprite NormalBgSprite;
            public Color NormalBgColor;
            public bool NormalBgPreserveAspect;
            public Image.Type NormalBgType;
            public bool Hovered;
        }

        private sealed class RecipeSlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public KomayamaFacilityMenuView Owner;
            public RecipeSlotBinding Binding;

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (Owner != null && Binding != null)
                {
                    Owner.OnRecipeSlotPointerEnter(Binding);
                }
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                if (Owner != null && Binding != null)
                {
                    Owner.OnRecipeSlotPointerExit(Binding);
                }
            }
        }

        public bool IsOpen =>
            gameObject.activeInHierarchy &&
            contentRoot != null &&
            contentRoot.gameObject.activeSelf &&
            current != null;

        public KomayamaProcessingFacility CurrentFacility => current;

        public bool TrySelectRecipeByDefinitionId(string recipeDefinitionId)
        {
            if (!IsOpen || current == null || string.IsNullOrEmpty(recipeDefinitionId))
            {
                return false;
            }

            if (current.Definition == null)
            {
                return false;
            }

            IReadOnlyList<RecipeDefinition> recipes = current.Definition.SupportedRecipes;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDefinition recipe = recipes[i];
                if (recipe == null ||
                    !string.Equals(
                        recipe.DefinitionId,
                        recipeDefinitionId,
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                OnRecipeClicked(recipe);
                return current.Recipe == recipe;
            }

            return false;
        }

        private void Awake()
        {
            EnsureUi();
            ApplyStatusPanelArt();
            WireButtonListeners();
            HideAll();
        }

        private void Start()
        {
            // エディタで Content を開いたまま保存しても、開始時は必ず閉じる。
            HideAll();
        }

        private void OnDestroy()
        {
            if (current != null)
            {
                current.StateChanged -= OnFacilityStateChanged;
            }
        }

        private void Update()
        {
            if (!IsOpen || current == null || statusPanel == null ||
                !statusPanel.gameObject.activeSelf)
            {
                return;
            }

            RefreshStatusTexts();
        }

        public void Open(KomayamaProcessingFacility facility)
        {
            if (facility == null)
            {
                return;
            }

            // 親 GO を誤って非表示にしてもクリックで開けるようにする（閉じる対象は Content）。
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureUi();
            BindFacility(facility);
            if (contentRoot != null)
            {
                contentRoot.gameObject.SetActive(true);
            }

            transform.SetAsLastSibling();
            if (!facility.HasSelectedRecipe)
            {
                ShowRecipePanel();
            }
            else
            {
                ShowStatusPanel();
            }
        }

        public void Close()
        {
            KomayamaCraftSeManager.TryPlay(seManager, KomayamaCraftSeCue.MainMenuToggle);
            HideAll();
            BindFacility(null);
        }

        private void WireButtonListeners()
        {
            WireCloseButton(closeRecipeButton);
            WireCloseButton(closeStatusButton);

            if (changeRecipeButton != null)
            {
                changeRecipeButton.onClick.RemoveListener(OpenRecipeSelectForCurrent);
                changeRecipeButton.onClick.AddListener(OpenRecipeSelectForCurrent);
            }

            if (ejectButton != null)
            {
                ejectButton.onClick.RemoveListener(OnEject);
                ejectButton.onClick.AddListener(OnEject);
            }
        }

        private void WireCloseButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(Close);
            button.onClick.AddListener(Close);
        }

        private void OpenRecipeSelectForCurrent()
        {
            if (current == null)
            {
                return;
            }

            KomayamaCraftSeManager.TryPlay(seManager, KomayamaCraftSeCue.MainMenuToggle);
            ShowRecipePanel();
        }

        private void OnEject()
        {
            if (current == null)
            {
                return;
            }

            current.EjectContents();
            // 排出後はレシピ未選択 → レシピ選択画面へ。
            ShowRecipePanel();
        }

        private void BindFacility(KomayamaProcessingFacility facility)
        {
            if (current != null)
            {
                current.StateChanged -= OnFacilityStateChanged;
            }

            current = facility;
            if (current != null)
            {
                current.StateChanged += OnFacilityStateChanged;
            }
        }

        private void OnFacilityStateChanged()
        {
            if (!IsOpen)
            {
                return;
            }

            if (statusPanel != null && statusPanel.gameObject.activeSelf)
            {
                RefreshStatusTexts();
            }
        }

        private void ShowRecipePanel()
        {
            if (recipePanel != null)
            {
                recipePanel.gameObject.SetActive(true);
            }

            if (statusPanel != null)
            {
                statusPanel.gameObject.SetActive(false);
            }

            EnsureSharedChromeActive();
            RebuildRecipeGrid();
        }

        private void ShowStatusPanel()
        {
            if (recipePanel != null)
            {
                recipePanel.gameObject.SetActive(false);
            }

            if (statusPanel != null)
            {
                statusPanel.gameObject.SetActive(true);
            }

            EnsureSharedChromeActive();
            ApplyStatusPanelArt();
            RefreshStatusTexts();
        }

        private void EnsureSharedChromeActive()
        {
            // StatusPanel から Content へ移した閉じる／タイトルを両パネルで共用。
            if (closeStatusButton != null)
            {
                closeStatusButton.gameObject.SetActive(true);
            }

            if (closeRecipeButton != null)
            {
                closeRecipeButton.gameObject.SetActive(true);
            }

            if (statusTitle != null)
            {
                statusTitle.gameObject.SetActive(true);
            }
        }

        private void HideAll()
        {
            ResolveContentRoot();
            if (contentRoot != null)
            {
                contentRoot.gameObject.SetActive(false);
            }

            if (recipePanel != null)
            {
                recipePanel.gameObject.SetActive(false);
            }

            if (statusPanel != null)
            {
                statusPanel.gameObject.SetActive(false);
            }
        }

        private void ResolveContentRoot()
        {
            if (contentRoot != null)
            {
                return;
            }

            Transform found = transform.Find("Content");
            if (found != null)
            {
                contentRoot = found as RectTransform;
            }
        }

        private void ResolveStatusTimeText()
        {
            if (statusTimeText != null)
            {
                return;
            }

            if (statusPanel == null)
            {
                return;
            }

            Transform found = statusPanel.Find("StatusTime");
            if (found != null)
            {
                statusTimeText = found.GetComponent<TMP_Text>();
            }
        }

        private void RebuildRecipeGrid()
        {
            ClearRecipeSlotBindings();
            ResolveRecipeSlotTemplates();
            if (current == null || current.Definition == null || recipeGrid == null)
            {
                EnsureRecipeIngredientSlots();
                PreviewRecipe(null);
                return;
            }

            HideRecipeSlotTemplates();

            IReadOnlyList<RecipeDefinition> recipes = current.Definition.SupportedRecipes;
            KomayamaProgressService progress = KomayamaProgressService.Instance;
            var unlockedList = new List<RecipeDefinition>();
            var lockedList = new List<RecipeDefinition>();
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDefinition recipe = recipes[i];
                if (recipe == null)
                {
                    continue;
                }

                bool unlocked = progress == null || progress.CanUseRecipe(recipe);
                if (unlocked)
                {
                    unlockedList.Add(recipe);
                }
                else
                {
                    lockedList.Add(recipe);
                }
            }

            // 生産可能（解放済み）を左上から優先し、続けて未解放。余り枠は非表示。
            var ordered = new List<(RecipeDefinition recipe, bool unlocked)>(
                unlockedList.Count + lockedList.Count);
            for (int i = 0; i < unlockedList.Count; i++)
            {
                ordered.Add((unlockedList[i], true));
            }

            for (int i = 0; i < lockedList.Count; i++)
            {
                ordered.Add((lockedList[i], false));
            }

            previewRecipe = unlockedList.Count > 0
                ? unlockedList[0]
                : (ordered.Count > 0 ? ordered[0].recipe : null);

            // RecipeGrid の見た目・子の生成はしない。既存スロットへデータだけ接続する。
            int slotIndex = 0;
            for (int i = 0; i < recipeGrid.childCount; i++)
            {
                Transform child = recipeGrid.GetChild(i);
                if (!IsBindableRecipeSlot(child))
                {
                    continue;
                }

                Button button = child.GetComponent<Button>();
                if (button == null)
                {
                    continue;
                }

                if (slotIndex < ordered.Count)
                {
                    BindRecipeSlot(button, ordered[slotIndex].recipe, ordered[slotIndex].unlocked);
                    child.gameObject.SetActive(true);
                }
                else
                {
                    // 将来もレシピが無い枠はスロット自体を出さない。
                    ClearRecipeSlotVisual(button);
                    child.gameObject.SetActive(false);
                }

                slotIndex++;
            }

            EnsureRecipeIngredientSlots();
            PreviewRecipe(previewRecipe);
        }

        private void ResolveRecipeSlotTemplates()
        {
            if (recipeGrid == null)
            {
                return;
            }

            if (recipeSlotSelectedTemplate == null)
            {
                Transform found = recipeGrid.Find("RecipeSlotSelected");
                if (found != null)
                {
                    recipeSlotSelectedTemplate = found as RectTransform;
                }
            }

            if (recipeSlotDisabledTemplate == null)
            {
                Transform found = recipeGrid.Find("RecipeSlotDisabled");
                if (found != null)
                {
                    recipeSlotDisabledTemplate = found as RectTransform;
                }
            }
        }

        private void HideRecipeSlotTemplates()
        {
            if (recipeSlotSelectedTemplate != null)
            {
                EnsureIgnoreLayout(recipeSlotSelectedTemplate);
                recipeSlotSelectedTemplate.gameObject.SetActive(false);
            }

            if (recipeSlotDisabledTemplate != null)
            {
                EnsureIgnoreLayout(recipeSlotDisabledTemplate);
                recipeSlotDisabledTemplate.gameObject.SetActive(false);
            }
        }

        private static void EnsureIgnoreLayout(Component target)
        {
            if (target == null)
            {
                return;
            }

            LayoutElement layout = target.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = target.gameObject.AddComponent<LayoutElement>();
            }

            layout.ignoreLayout = true;
        }

        private static bool IsBindableRecipeSlot(Transform child)
        {
            if (child == null)
            {
                return false;
            }

            string name = child.name;
            if (name == "SlotBackground" ||
                name == "RecipeSlotSelected" ||
                name == "RecipeSlotDisabled")
            {
                return false;
            }

            return child.GetComponent<Button>() != null;
        }

        private void ClearRecipeSlotBindings()
        {
            for (int i = 0; i < recipeSlotBindings.Count; i++)
            {
                RecipeSlotBinding binding = recipeSlotBindings[i];
                if (binding == null || binding.Button == null)
                {
                    continue;
                }

                binding.Button.onClick.RemoveAllListeners();
                RecipeSlotHover hover = binding.Button.GetComponent<RecipeSlotHover>();
                if (hover != null)
                {
                    hover.Owner = null;
                    hover.Binding = null;
                }
            }

            recipeSlotBindings.Clear();
        }

        private static void ClearRecipeSlotVisual(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            RecipeSlotHover hover = button.GetComponent<RecipeSlotHover>();
            if (hover != null)
            {
                hover.Owner = null;
                hover.Binding = null;
            }

            Transform selected = button.transform.Find("Selected");
            if (selected != null)
            {
                selected.gameObject.SetActive(false);
            }

            Transform disabled = button.transform.Find("Disebled");
            if (disabled == null)
            {
                disabled = button.transform.Find("Disabled");
            }

            if (disabled != null)
            {
                disabled.gameObject.SetActive(false);
            }
        }

        private void BindRecipeSlot(Button button, RecipeDefinition recipe, bool unlocked)
        {
            if (button == null)
            {
                return;
            }

            ResolveRecipeSlotTemplates();

            Image background = button.targetGraphic as Image;
            if (background == null)
            {
                background = button.GetComponent<Image>();
            }

            Transform iconTf = button.transform.Find("Icon");
            Image icon = iconTf != null ? iconTf.GetComponent<Image>() : null;
            Image selectedOverlay = EnsureSlotOverlay(
                button.transform,
                "Selected",
                recipeSlotSelectedTemplate);
            Image disabledOverlay = EnsureSlotOverlay(
                button.transform,
                ResolveDisabledOverlayName(),
                recipeSlotDisabledTemplate);

            var binding = new RecipeSlotBinding
            {
                Button = button,
                Background = background,
                Icon = icon,
                SelectedOverlay = selectedOverlay,
                DisabledOverlay = disabledOverlay,
                Recipe = recipe,
                Unlocked = unlocked && recipe != null,
                Hovered = false,
                NormalBgSprite = background != null ? background.sprite : null,
                NormalBgColor = background != null ? background.color : Color.white,
                NormalBgPreserveAspect = background != null && background.preserveAspect,
                NormalBgType = background != null ? background.type : Image.Type.Simple
            };

            button.onClick.RemoveAllListeners();
            RecipeSlotHover hover = button.GetComponent<RecipeSlotHover>();
            if (hover == null)
            {
                hover = button.gameObject.AddComponent<RecipeSlotHover>();
            }

            hover.Owner = this;
            hover.Binding = binding;

            if (binding.Unlocked)
            {
                button.interactable = true;
                RecipeDefinition captured = recipe;
                button.onClick.AddListener(() => OnRecipeClicked(captured));
            }
            else
            {
                // 未開放は選択不可。ホバーでの詳細プレビューのみ可。
                button.interactable = true;
                button.onClick.RemoveAllListeners();
            }

            ApplyRecipeSlotVisual(binding);
            recipeSlotBindings.Add(binding);
        }

        private string ResolveDisabledOverlayName()
        {
            if (recipeSlotDisabledTemplate != null)
            {
                if (recipeSlotDisabledTemplate.Find("Disebled") != null)
                {
                    return "Disebled";
                }

                if (recipeSlotDisabledTemplate.Find("Disabled") != null)
                {
                    return "Disabled";
                }
            }

            return "Disebled";
        }

        private static Image EnsureSlotOverlay(
            Transform slotRoot,
            string overlayName,
            RectTransform templateRoot)
        {
            if (slotRoot == null || string.IsNullOrEmpty(overlayName))
            {
                return null;
            }

            Transform existing = slotRoot.Find(overlayName);
            if (existing == null && overlayName == "Disebled")
            {
                existing = slotRoot.Find("Disabled");
            }
            else if (existing == null && overlayName == "Disabled")
            {
                existing = slotRoot.Find("Disebled");
            }

            Image overlayImage;
            if (existing != null)
            {
                overlayImage = existing.GetComponent<Image>();
                if (overlayImage == null)
                {
                    overlayImage = existing.gameObject.AddComponent<Image>();
                }
            }
            else
            {
                Transform templateOverlay = null;
                if (templateRoot != null)
                {
                    templateOverlay = templateRoot.Find(overlayName);
                    if (templateOverlay == null && overlayName == "Disebled")
                    {
                        templateOverlay = templateRoot.Find("Disabled");
                    }
                }

                GameObject overlayGo = new GameObject(
                    overlayName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                overlayGo.transform.SetParent(slotRoot, false);
                // Icon より手前（上）に出す。
                overlayGo.transform.SetAsLastSibling();
                RectTransform overlayRt = overlayGo.GetComponent<RectTransform>();
                if (templateOverlay != null)
                {
                    RectTransform src = templateOverlay as RectTransform;
                    overlayRt.anchorMin = src.anchorMin;
                    overlayRt.anchorMax = src.anchorMax;
                    overlayRt.pivot = src.pivot;
                    overlayRt.anchoredPosition = src.anchoredPosition;
                    overlayRt.sizeDelta = src.sizeDelta;
                    overlayRt.localScale = src.localScale;
                }
                else
                {
                    overlayRt.anchorMin = Vector2.zero;
                    overlayRt.anchorMax = Vector2.one;
                    overlayRt.offsetMin = Vector2.zero;
                    overlayRt.offsetMax = Vector2.zero;
                }

                overlayImage = overlayGo.GetComponent<Image>();
                existing = overlayGo.transform;
            }

            overlayImage.raycastTarget = false;
            // Icon の手前に重ねる。
            if (slotRoot.Find("Icon") != null)
            {
                existing.SetAsLastSibling();
            }

            return overlayImage;
        }

        private void OnRecipeSlotPointerEnter(RecipeSlotBinding binding)
        {
            if (binding == null || binding.Recipe == null)
            {
                return;
            }

            binding.Hovered = true;
            ApplyRecipeSlotVisual(binding);
            PreviewRecipe(binding.Recipe);
        }

        private void OnRecipeSlotPointerExit(RecipeSlotBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            binding.Hovered = false;
            ApplyRecipeSlotVisual(binding);
        }

        private void ApplyRecipeSlotVisual(RecipeSlotBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            ResolveRecipeSlotTemplates();

            if (binding.Recipe == null)
            {
                ApplyBackgroundFromCachedNormal(binding);
                SetOverlayActive(binding.SelectedOverlay, false);
                SetOverlayActive(binding.DisabledOverlay, false);
                if (binding.Icon != null)
                {
                    binding.Icon.enabled = false;
                }

                return;
            }

            if (binding.Hovered)
            {
                // ホバー中: Selected オーバーレイ。Icon はレシピ画像。
                ApplyBackgroundFromCachedNormal(binding);
                SyncOverlayFromTemplate(
                    binding.SelectedOverlay,
                    recipeSlotSelectedTemplate,
                    "Selected");
                SetOverlayActive(binding.SelectedOverlay, true);
                SetOverlayActive(binding.DisabledOverlay, false);
                ApplyRecipeIcon(binding.Icon, binding.Recipe);
                return;
            }

            if (!binding.Unlocked)
            {
                // 未開放: Disabled オーバーレイ。Icon はテンプレート画像をそのまま使用。
                ApplyBackgroundFromCachedNormal(binding);
                SyncOverlayFromTemplate(
                    binding.DisabledOverlay,
                    recipeSlotDisabledTemplate,
                    ResolveDisabledOverlayName());
                SetOverlayActive(binding.SelectedOverlay, false);
                SetOverlayActive(binding.DisabledOverlay, true);
                ApplyIconFromTemplate(binding.Icon, recipeSlotDisabledTemplate);
                return;
            }

            ApplyBackgroundFromCachedNormal(binding);
            SetOverlayActive(binding.SelectedOverlay, false);
            SetOverlayActive(binding.DisabledOverlay, false);
            ApplyRecipeIcon(binding.Icon, binding.Recipe);
        }

        private static void SetOverlayActive(Image overlay, bool active)
        {
            if (overlay == null)
            {
                return;
            }

            overlay.gameObject.SetActive(active);
            overlay.enabled = active;
        }

        private static void SyncOverlayFromTemplate(
            Image target,
            RectTransform templateRoot,
            string overlayName)
        {
            if (target == null || templateRoot == null || string.IsNullOrEmpty(overlayName))
            {
                return;
            }

            Transform templateOverlay = templateRoot.Find(overlayName);
            if (templateOverlay == null && overlayName == "Disebled")
            {
                templateOverlay = templateRoot.Find("Disabled");
            }
            else if (templateOverlay == null && overlayName == "Disabled")
            {
                templateOverlay = templateRoot.Find("Disebled");
            }

            Image templateImage = templateOverlay != null
                ? templateOverlay.GetComponent<Image>()
                : null;
            if (templateImage == null)
            {
                return;
            }

            // テンプレートの現行画像を参照（差し替え後も次回同期で反映）。テンプレートは書き換えない。
            target.sprite = templateImage.sprite;
            target.color = templateImage.color;
            target.preserveAspect = templateImage.preserveAspect;
            target.type = templateImage.type;
        }

        private static void ApplyBackgroundFromCachedNormal(RecipeSlotBinding binding)
        {
            if (binding == null || binding.Background == null)
            {
                return;
            }

            binding.Background.sprite = binding.NormalBgSprite;
            binding.Background.color = binding.NormalBgColor;
            binding.Background.preserveAspect = binding.NormalBgPreserveAspect;
            binding.Background.type = binding.NormalBgType;
        }

        private static void ApplyIconFromTemplate(Image target, RectTransform templateRoot)
        {
            if (target == null || templateRoot == null)
            {
                return;
            }

            Transform iconTf = templateRoot.Find("Icon");
            Image templateIcon = iconTf != null ? iconTf.GetComponent<Image>() : null;
            if (templateIcon == null)
            {
                return;
            }

            target.enabled = templateIcon.enabled;
            target.sprite = templateIcon.sprite;
            target.color = templateIcon.color;
            target.preserveAspect = templateIcon.preserveAspect;
            target.type = templateIcon.type;
        }

        private static void ApplyRecipeIcon(Image target, RecipeDefinition recipe)
        {
            if (target == null)
            {
                return;
            }

            Sprite recipeIcon = null;
            if (recipe != null &&
                recipe.Outputs.Count > 0 &&
                recipe.Outputs[0].Item != null)
            {
                recipeIcon = recipe.Outputs[0].Item.Icon;
            }

            if (recipeIcon != null)
            {
                target.enabled = true;
                target.sprite = recipeIcon;
                target.color = Color.white;
                target.preserveAspect = true;
            }
            else
            {
                target.enabled = false;
            }
        }

        public void PreviewRecipe(RecipeDefinition recipe)
        {
            previewRecipe = recipe;
            RefreshRecipeDetail(recipe);
            RefreshRecipeIngredients(recipe);
        }

        private void OnRecipeClicked(RecipeDefinition recipe)
        {
            PreviewRecipe(recipe);
            if (current == null)
            {
                return;
            }

            if (!current.TrySelectRecipe(recipe, out string reason))
            {
                if (statusMessage != null)
                {
                    statusMessage.text = reason;
                }

                return;
            }

            KomayamaCraftSeManager.TryPlay(seManager, KomayamaCraftSeCue.MainMenuToggle);
            ShowStatusPanel();
        }

        private void RefreshRecipeDetail(RecipeDefinition recipe)
        {
            ItemDefinition product = null;
            if (recipe != null && recipe.Outputs.Count > 0)
            {
                product = recipe.Outputs[0].Item;
            }

            if (recipeDetailTitle != null)
            {
                recipeDetailTitle.text = recipe != null
                    ? recipe.DisplayName
                    : string.Empty;
            }

            if (recipeDetailBody != null)
            {
                // 製品ごとの詳細説明表示領域。未記入なら空。
                if (product != null && !string.IsNullOrWhiteSpace(product.Description))
                {
                    recipeDetailBody.text = product.Description;
                }
                else
                {
                    recipeDetailBody.text = string.Empty;
                }
            }

            if (recipeDetailIcon != null)
            {
                if (product != null && product.Icon != null)
                {
                    recipeDetailIcon.sprite = product.Icon;
                    recipeDetailIcon.color = Color.white;
                    recipeDetailIcon.enabled = true;
                }
                else
                {
                    recipeDetailIcon.sprite = GetUiWhiteSprite();
                    recipeDetailIcon.color = new Color(1f, 1f, 1f, 0.2f);
                    recipeDetailIcon.enabled = true;
                }
            }
        }

        private void EnsureRecipeIngredientSlots()
        {
            if (ingredientOj == null && recipePanel != null)
            {
                Transform found = recipePanel.Find("IngredientOj");
                if (found != null)
                {
                    ingredientOj = found as RectTransform;
                }
            }

            if (ingredientOj == null)
            {
                recipeIngredientSlots = null;
                return;
            }

            if (recipeIngredientSlots != null && recipeIngredientSlots.Length == MaxIngredientDisplay)
            {
                return;
            }

            recipeIngredientSlots = new IngredientSlotView[MaxIngredientDisplay];
            for (int i = 0; i < MaxIngredientDisplay; i++)
            {
                Transform slotTf = ingredientOj.Find($"IngredientsSlot{i + 1}");
                var view = new IngredientSlotView();
                if (slotTf != null)
                {
                    Transform iconTf = slotTf.Find("IngredientsIcon");
                    if (iconTf != null)
                    {
                        view.Icon = iconTf.GetComponent<Image>();
                    }

                    Transform countTf = slotTf.Find("IngredientsCount");
                    if (countTf != null)
                    {
                        view.Count = countTf.GetComponent<TMP_Text>();
                    }
                }

                recipeIngredientSlots[i] = view;
            }
        }

        private void RefreshRecipeIngredients(RecipeDefinition recipe)
        {
            EnsureRecipeIngredientSlots();
            if (recipeIngredientSlots == null)
            {
                return;
            }

            List<ItemAmount> inputs = CollectDisplayInputs(recipe);
            for (int i = 0; i < recipeIngredientSlots.Length; i++)
            {
                IngredientSlotView slot = recipeIngredientSlots[i];
                if (slot == null)
                {
                    continue;
                }

                bool active = i < inputs.Count;
                Transform slotRoot = null;
                if (slot.Icon != null)
                {
                    slotRoot = slot.Icon.transform.parent;
                }

                if (slotRoot != null)
                {
                    slotRoot.gameObject.SetActive(active);
                }

                if (!active)
                {
                    if (slot.Count != null)
                    {
                        slot.Count.text = string.Empty;
                    }

                    continue;
                }

                ItemAmount req = inputs[i];
                if (slot.Icon != null)
                {
                    slot.Icon.enabled = true;
                    if (req.Item != null && req.Item.Icon != null)
                    {
                        slot.Icon.sprite = req.Item.Icon;
                        slot.Icon.color = Color.white;
                    }
                    else
                    {
                        slot.Icon.sprite = GetUiWhiteSprite();
                        slot.Icon.color = new Color(0.85f, 0.7f, 0.3f, 1f);
                    }
                }

                if (slot.Count != null)
                {
                    slot.Count.text = $"x{Mathf.Max(1, req.Amount)}";
                }
            }
        }

        private void RefreshStatusTexts()
        {
            if (current == null || statusPanel == null)
            {
                return;
            }

            RecipeDefinition recipe = current.Recipe;

            if (statusMessage != null)
            {
                statusMessage.text = current.DescribeStatusMessage();
            }

            RebuildCraftRow(recipe);

            ResolveStatusTimeText();

            float progress = current.State == KomayamaFacilityState.Processing
                ? current.Progress01
                : 0f;
            if (statusProgressFill != null)
            {
                statusProgressFill.type = Image.Type.Filled;
                statusProgressFill.fillMethod = Image.FillMethod.Horizontal;
                statusProgressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                statusProgressFill.fillAmount = progress;
                if (progressBarSprite == null)
                {
                    statusProgressFill.color = current.State == KomayamaFacilityState.Processing
                        ? new Color(0.95f, 0.4f, 0.2f, 1f)
                        : new Color(0.35f, 0.28f, 0.2f, 1f);
                }
                else
                {
                    float a = current.State == KomayamaFacilityState.Processing ? 1f : 0.35f;
                    statusProgressFill.color = new Color(1f, 1f, 1f, a);
                }
            }

            if (statusProgress != null)
            {
                statusProgress.gameObject.SetActive(false);
            }

            if (statusTimeText != null)
            {
                if (recipe == null)
                {
                    statusTimeText.text = string.Empty;
                }
                else if (current.State == KomayamaFacilityState.Processing)
                {
                    statusTimeText.text =
                        $"{current.RemainingSeconds:0.00} / {recipe.ProcessingSeconds:0.##} 秒";
                }
                else
                {
                    // 待機・素材不足・燃料不足など、非稼働はすべて「- / 必要秒」
                    statusTimeText.text = $"- / {recipe.ProcessingSeconds:0.##} 秒";
                }
            }

            if (statusProductIcon != null)
            {
                statusProductIcon.gameObject.SetActive(false);
            }

            if (statusBody != null)
            {
                statusBody.gameObject.SetActive(false);
            }
        }

        private void RebuildCraftRow(RecipeDefinition recipe)
        {
            EnsureCraftRowScaffold();
            if (craftRowRoot == null)
            {
                return;
            }

            if (recipe == null)
            {
                SetIngredientLayoutActive(0);
                if (craftOutputSlot != null)
                {
                    craftOutputSlot.gameObject.SetActive(false);
                }

                if (craftArrowImage != null)
                {
                    craftArrowImage.gameObject.SetActive(false);
                }

                if (craftArrowText != null)
                {
                    craftArrowText.gameObject.SetActive(false);
                }

                return;
            }

            // 燃料は使用素材一覧に入れない。最大6枠・固定レイアウトを切替表示する。
            List<ItemAmount> inputs = CollectDisplayInputs(recipe);
            int count = Mathf.Clamp(inputs.Count, 0, MaxIngredientDisplay);
            SetIngredientLayoutActive(count);
            if (count > 0 &&
                ingredientSlotsByCount != null &&
                ingredientSlotsByCount[count - 1] != null)
            {
                IngredientSlotView[] slots = ingredientSlotsByCount[count - 1];
                for (int i = 0; i < slots.Length; i++)
                {
                    ItemAmount req = inputs[i];
                    int have = current != null ? current.GetInputAmount(req.Item) : 0;
                    ApplyIngredientSlot(slots[i], req.Item, have, req.Amount, outputOnly: false);
                }
            }

            if (craftArrowImage != null)
            {
                craftArrowImage.gameObject.SetActive(true);
            }
            else if (craftArrowText != null)
            {
                craftArrowText.gameObject.SetActive(true);
            }

            ItemDefinition output = recipe.Outputs.Count > 0 ? recipe.Outputs[0].Item : null;
            int outAmount = recipe.Outputs.Count > 0 ? recipe.Outputs[0].Amount : 1;
            if (craftOutputSlot != null)
            {
                craftOutputSlot.gameObject.SetActive(true);
                IngredientSlotView outView = ResolveSlotView(craftOutputSlot);
                ApplyIngredientSlot(outView, output, outAmount, outAmount, outputOnly: true);
            }
        }

        private static List<ItemAmount> CollectDisplayInputs(RecipeDefinition recipe)
        {
            var list = new List<ItemAmount>(MaxIngredientDisplay);
            if (recipe == null)
            {
                return list;
            }

            for (int i = 0; i < recipe.Inputs.Count && list.Count < MaxIngredientDisplay; i++)
            {
                ItemAmount req = recipe.Inputs[i];
                if (req.Item == null)
                {
                    continue;
                }

                list.Add(req);
            }

            return list;
        }

        private void EnsureCraftRowScaffold()
        {
            if (statusPanel == null)
            {
                return;
            }

            if (craftRowRoot == null)
            {
                Transform existing = statusPanel.Find("CraftRow");
                if (existing != null)
                {
                    craftRowRoot = existing as RectTransform;
                }
                else
                {
                    GameObject row = new GameObject("CraftRow", typeof(RectTransform));
                    row.transform.SetParent(statusPanel, false);
                    craftRowRoot = row.GetComponent<RectTransform>();
                    Place(craftRowRoot, 0f, 25f, 600f, 140f);
                }
            }

            EnsureIngredientLayouts();

            if (craftArrowImage == null && craftArrowText == null)
            {
                Transform arrowTf = craftRowRoot.Find("Arrow");
                if (arrowTf != null)
                {
                    craftArrowImage = arrowTf.GetComponent<Image>();
                    craftArrowText = arrowTf.GetComponent<TMP_Text>();
                }
                else if (craftArrowSprite != null)
                {
                    craftArrowImage = CreateImage(craftRowRoot, "Arrow", Color.white);
                    Place(craftArrowImage.rectTransform, 90f, 20f, 50f, 50f);
                }
                else
                {
                    craftArrowText = CreateText(craftRowRoot, "→", 42f, TextAlignmentOptions.Center);
                    craftArrowText.gameObject.name = "Arrow";
                    Place(craftArrowText.rectTransform, 90f, 20f, 50f, 50f);
                }
            }

            if (craftOutputSlot == null)
            {
                Transform outTf = craftRowRoot.Find("OutputSlot");
                if (outTf != null)
                {
                    craftOutputSlot = outTf as RectTransform;
                }
                else
                {
                    craftOutputSlot = CreateMaterialSlotRoot(
                        craftRowRoot,
                        "OutputSlot",
                        new Vector2(170f, 10f),
                        outputSlotFrameSprite != null ? outputSlotFrameSprite : materialSlotFrameSprite);
                }
            }
        }

        private void EnsureIngredientLayouts()
        {
            if (craftRowRoot == null)
            {
                return;
            }

            if (ingredientLayouts == null || ingredientLayouts.Length != MaxIngredientDisplay)
            {
                ingredientLayouts = new RectTransform[MaxIngredientDisplay];
            }

            ingredientSlotsByCount = new IngredientSlotView[MaxIngredientDisplay][];

            for (int count = 1; count <= MaxIngredientDisplay; count++)
            {
                int layoutIndex = count - 1;
                RectTransform layout = ingredientLayouts[layoutIndex];
                if (layout == null)
                {
                    Transform found = craftRowRoot.Find($"Layout_{count}");
                    if (found != null)
                    {
                        layout = found as RectTransform;
                    }
                    else
                    {
                        layout = CreateDefaultIngredientLayout(craftRowRoot, count);
                    }

                    ingredientLayouts[layoutIndex] = layout;
                }

                ingredientSlotsByCount[layoutIndex] = CacheIngredientSlots(layout, count);
                layout.gameObject.SetActive(false);
            }
        }

        private RectTransform CreateDefaultIngredientLayout(Transform parent, int count)
        {
            GameObject layoutGo = new GameObject($"Layout_{count}", typeof(RectTransform));
            layoutGo.transform.SetParent(parent, false);
            RectTransform layout = layoutGo.GetComponent<RectTransform>();
            StretchFull(layout);

            // 初期配置のみ。以降は位置をコードで動かさない（Inspector で調整可能）。
            const float slotW = 88f;
            const float gap = 12f;
            const float plusW = 28f;
            float unit = slotW + gap + plusW;
            float total = count * slotW + (count - 1) * (gap + plusW);
            float startX = -total * 0.5f + slotW * 0.5f - 80f;

            for (int i = 0; i < count; i++)
            {
                float x = startX + i * unit;
                CreateMaterialSlotRoot(layout, $"Slot_{i}", new Vector2(x, 10f), materialSlotFrameSprite);
                if (i < count - 1)
                {
                    TMP_Text plus = CreateText(layout, "+", 36f, TextAlignmentOptions.Center);
                    plus.gameObject.name = $"Plus_{i}";
                    Place(plus.rectTransform, x + slotW * 0.5f + gap * 0.5f + plusW * 0.5f, 20f, plusW, 40f);
                }
            }

            return layout;
        }

        private static IngredientSlotView[] CacheIngredientSlots(RectTransform layout, int count)
        {
            var slots = new IngredientSlotView[count];
            if (layout == null)
            {
                return slots;
            }

            for (int i = 0; i < count; i++)
            {
                Transform slotTf = layout.Find($"Slot_{i}");
                if (slotTf == null)
                {
                    continue;
                }

                slots[i] = ResolveSlotView(slotTf);
            }

            return slots;
        }

        private static IngredientSlotView ResolveSlotView(Transform slotRoot)
        {
            var view = new IngredientSlotView();
            if (slotRoot == null)
            {
                return view;
            }

            Transform iconTf = slotRoot.Find("Icon");
            if (iconTf != null)
            {
                view.Icon = iconTf.GetComponent<Image>();
            }

            Transform countTf = slotRoot.Find("Count");
            if (countTf != null)
            {
                view.Count = countTf.GetComponent<TMP_Text>();
            }

            return view;
        }

        private void SetIngredientLayoutActive(int count)
        {
            if (ingredientLayouts == null)
            {
                return;
            }

            for (int i = 0; i < ingredientLayouts.Length; i++)
            {
                if (ingredientLayouts[i] == null)
                {
                    continue;
                }

                ingredientLayouts[i].gameObject.SetActive(count > 0 && i == count - 1);
            }
        }

        private static void ApplyIngredientSlot(
            IngredientSlotView slot,
            ItemDefinition item,
            int have,
            int need,
            bool outputOnly)
        {
            if (slot == null)
            {
                return;
            }

            if (slot.Icon != null)
            {
                if (item != null && item.Icon != null)
                {
                    slot.Icon.sprite = item.Icon;
                    slot.Icon.color = Color.white;
                }
                else
                {
                    slot.Icon.sprite = GetUiWhiteSprite();
                    slot.Icon.color = new Color(0.85f, 0.7f, 0.3f, 1f);
                }
            }

            if (slot.Count != null)
            {
                need = Mathf.Max(1, need);
                slot.Count.text = outputOnly ? $"x{need}" : $"{have}/{need}";
                slot.Count.color = outputOnly || have >= need
                    ? new Color(0.6f, 1f, 0.6f, 1f)
                    : new Color(1f, 0.55f, 0.45f, 1f);
            }
        }

        private RectTransform CreateMaterialSlotRoot(
            Transform parent,
            string name,
            Vector2 pos,
            Sprite frameSprite = null)
        {
            Color fallback = frameSprite != null
                ? Color.white
                : new Color(0.22f, 0.15f, 0.1f, 0.95f);
            GameObject slot = CreatePanel(parent, name, fallback);
            RectTransform slotRt = slot.GetComponent<RectTransform>();
            Place(slotRt, pos.x, pos.y, 88f, 110f);
            ApplySpriteOrKeep(slot.GetComponent<Image>(), frameSprite, fallback);

            Image icon = CreateImage(slot.transform, "Icon", Color.white);
            icon.gameObject.name = "Icon";
            Place(icon.rectTransform, 0f, 18f, 64f, 64f);

            TMP_Text count = CreateText(slot.transform, "0/0", 22f, TextAlignmentOptions.Center);
            count.gameObject.name = "Count";
            Place(count.rectTransform, 0f, -40f, 84f, 28f);
            return slotRt;
        }

        private void ApplyStatusPanelArt()
        {
            ApplyPanelBackgroundArt(statusPanel);
            ApplyPanelBackgroundArt(recipePanel);

            if (statusPanel == null)
            {
                return;
            }

            EnsureProgressArtScaffold();
            ApplySpriteOrKeep(statusProgressBackground, progressBackgroundSprite, Color.white, preserveAspect: false);
            if (statusProgressFill != null && progressBarSprite != null)
            {
                statusProgressFill.sprite = progressBarSprite;
                statusProgressFill.type = Image.Type.Filled;
                statusProgressFill.fillMethod = Image.FillMethod.Horizontal;
                statusProgressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                statusProgressFill.color = Color.white;
                statusProgressFill.preserveAspect = false;
            }

            ApplySpriteOrKeep(statusProgressFrame, progressFrameSprite, Color.white, preserveAspect: false);
            ApplySpriteOrKeep(statusRecipeProgressBorder, statusBorderSprite, Color.white, preserveAspect: false);

            if (craftArrowSprite != null)
            {
                EnsureCraftRowScaffold();
                if (craftRowRoot != null)
                {
                    Transform arrowTf = craftRowRoot.Find("Arrow");
                    if (arrowTf != null)
                    {
                        craftArrowImage = arrowTf.GetComponent<Image>();
                        if (craftArrowImage == null)
                        {
                            Transform iconTf = arrowTf.Find("Icon");
                            if (iconTf == null)
                            {
                                craftArrowImage = CreateImage(arrowTf, "Icon", Color.white);
                                StretchFull(craftArrowImage.rectTransform);
                                craftArrowImage.raycastTarget = false;
                            }
                            else
                            {
                                craftArrowImage = iconTf.GetComponent<Image>();
                            }

                            TMP_Text legacyArrow = arrowTf.GetComponent<TMP_Text>();
                            if (legacyArrow != null)
                            {
                                legacyArrow.enabled = false;
                                legacyArrow.text = string.Empty;
                            }
                        }

                        craftArrowText = null;
                    }
                }

                ApplySpriteOrKeep(craftArrowImage, craftArrowSprite, Color.white);
            }

            ApplyIconButtonArt(closeStatusButton, closeButtonSprite);
            if (closeRecipeButton != null && closeRecipeButton != closeStatusButton)
            {
                ApplyIconButtonArt(closeRecipeButton, closeButtonSprite);
            }

            // ChangeRecipe / Eject の位置・文言・見た目は Inspector 調整を維持する。
            ApplyMaterialSlotFrames();
        }

        private void ApplyPanelBackgroundArt(RectTransform panel)
        {
            if (panel == null || statusPanelSprite == null)
            {
                return;
            }

            Image panelImage = panel.GetComponent<Image>();
            if (panelImage == null)
            {
                return;
            }

            // preserveAspect など Inspector 設定は維持する。
            panelImage.sprite = statusPanelSprite;
            panelImage.color = Color.white;
            panelImage.type = Image.Type.Simple;
        }

        private void EnsureProgressArtScaffold()
        {
            if (statusPanel == null)
            {
                return;
            }

            if (statusProgressFill == null)
            {
                Transform fillTf = statusPanel.Find("ProgressBg/ProgressFill");
                if (fillTf == null)
                {
                    fillTf = statusPanel.Find("ProgressFill");
                }

                if (fillTf != null)
                {
                    statusProgressFill = fillTf.GetComponent<Image>();
                }
            }

            if (statusProgressBackground == null)
            {
                Transform bgTf = statusPanel.Find("ProgressBg");
                if (bgTf != null)
                {
                    statusProgressBackground = bgTf.GetComponent<Image>();
                }
                else if (statusProgressFill != null)
                {
                    statusProgressBackground = statusProgressFill.transform.parent != null
                        ? statusProgressFill.transform.parent.GetComponent<Image>()
                        : null;
                }
            }

            if (statusProgressFrame == null)
            {
                Transform frameTf = statusPanel.Find("ProgressFrame");
                if (frameTf == null && statusProgressBackground != null)
                {
                    frameTf = statusProgressBackground.transform.Find("ProgressFrame");
                }

                if (frameTf != null)
                {
                    statusProgressFrame = frameTf.GetComponent<Image>();
                }
                else if (progressFrameSprite != null && statusProgressBackground != null)
                {
                    statusProgressFrame = CreateImage(
                        statusProgressBackground.transform,
                        "ProgressFrame",
                        Color.white);
                    StretchFull(statusProgressFrame.rectTransform);
                    statusProgressFrame.raycastTarget = false;
                    statusProgressFrame.transform.SetAsLastSibling();
                }
            }

            if (statusRecipeProgressBorder == null)
            {
                Transform borderTf = statusPanel.Find("RecipeProgressBorder");
                if (borderTf != null)
                {
                    statusRecipeProgressBorder = borderTf.GetComponent<Image>();
                }
                else if (statusBorderSprite != null)
                {
                    statusRecipeProgressBorder = CreateImage(
                        statusPanel,
                        "RecipeProgressBorder",
                        Color.white);
                    Place(statusRecipeProgressBorder.rectTransform, 0f, -95f, 560f, 12f);
                    statusRecipeProgressBorder.raycastTarget = false;
                }
            }
        }

        private void ApplyMaterialSlotFrames()
        {
            if (craftRowRoot == null)
            {
                EnsureCraftRowScaffold();
            }

            if (craftRowRoot == null)
            {
                return;
            }

            if (ingredientLayouts != null)
            {
                for (int i = 0; i < ingredientLayouts.Length; i++)
                {
                    RectTransform layout = ingredientLayouts[i];
                    if (layout == null)
                    {
                        continue;
                    }

                    for (int s = 0; s < layout.childCount; s++)
                    {
                        Transform child = layout.GetChild(s);
                        if (child == null || !child.name.StartsWith("Slot_"))
                        {
                            continue;
                        }

                        ApplySpriteOrKeep(
                            child.GetComponent<Image>(),
                            materialSlotFrameSprite,
                            Color.white);
                    }
                }
            }

            if (craftOutputSlot != null)
            {
                Sprite outSprite = outputSlotFrameSprite != null
                    ? outputSlotFrameSprite
                    : materialSlotFrameSprite;
                ApplySpriteOrKeep(craftOutputSlot.GetComponent<Image>(), outSprite, Color.white);
            }
        }

        private static void ApplyIconButtonArt(Button button, Sprite sprite)
        {
            if (button == null || sprite == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            ApplySpriteOrKeep(image, sprite, Color.white);
            // 子 Text は Inspector 側の文言・表示を維持する。
        }

        private static void ApplySpriteOrKeep(Image image, Sprite sprite, Color whenSprite, bool preserveAspect = true)
        {
            if (image == null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.color = whenSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = preserveAspect;
        }

        private void EnsureUi()
        {
            ResolveContentRoot();
            ResolveStatusTimeText();

            if (recipePanel != null &&
                statusPanel != null &&
                contentRoot != null &&
                statusProgressFill != null)
            {
                return;
            }

            if (contentRoot != null && statusProgressFill == null)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(transform.GetChild(i).gameObject);
                }

                contentRoot = null;
                recipePanel = null;
                statusPanel = null;
                recipeGrid = null;
                craftRowRoot = null;
                craftOutputSlot = null;
                craftArrowImage = null;
                craftArrowText = null;
                statusProgressBackground = null;
                statusProgressFrame = null;
                statusRecipeProgressBorder = null;
                ingredientSlotsByCount = null;
                if (ingredientLayouts != null)
                {
                    for (int i = 0; i < ingredientLayouts.Length; i++)
                    {
                        ingredientLayouts[i] = null;
                    }
                }
            }

            if (root == null)
            {
                root = GetComponent<RectTransform>();
            }

            if (contentRoot == null)
            {
                GameObject content = new GameObject("Content", typeof(RectTransform));
                content.transform.SetParent(transform, false);
                contentRoot = content.GetComponent<RectTransform>();
                StretchFull(contentRoot);
            }

            Transform parent = contentRoot;
            GameObject dim = CreatePanel(parent, "Dim", new Color(0f, 0f, 0f, 0.45f));
            StretchFull(dim.GetComponent<RectTransform>());
            Button dimButton = dim.GetComponent<Button>();
            if (dimButton == null)
            {
                dimButton = dim.AddComponent<Button>();
            }

            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(Close);

            recipePanel = CreatePanel(parent, "RecipePanel", new Color(0.36f, 0.24f, 0.14f, 0.98f))
                .GetComponent<RectTransform>();
            recipePanel.sizeDelta = new Vector2(760f, 480f);
            Center(recipePanel);

            statusPanel = CreatePanel(parent, "StatusPanel", new Color(0.36f, 0.24f, 0.14f, 0.98f))
                .GetComponent<RectTransform>();
            statusPanel.sizeDelta = new Vector2(720f, 460f);
            Center(statusPanel);

            GameObject gridGo = new GameObject("RecipeGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(recipePanel, false);
            recipeGrid = gridGo.transform;
            RectTransform gridRt = gridGo.GetComponent<RectTransform>();
            Place(gridRt, -170f, -20f, 300f, 300f);
            GridLayoutGroup grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(88f, 88f);
            grid.spacing = new Vector2(10f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            recipeDetailTitle = CreateText(recipePanel, "詳細", 32f, TextAlignmentOptions.Left, "RecipeDetailTitle");
            Place(recipeDetailTitle.rectTransform, 180f, 150f, 280f, 40f);
            recipeDetailIcon = CreateImage(recipePanel, "RecipeDetailIcon", Color.white);
            Place(recipeDetailIcon.rectTransform, 180f, 40f, 96f, 96f);
            recipeDetailBody = CreateText(recipePanel, string.Empty, 24f, TextAlignmentOptions.TopLeft, "RecipeDetailBody");
            Place(recipeDetailBody.rectTransform, 180f, -120f, 280f, 200f);

            // 閉じる／タイトルは Content 直下で両パネル共用。
            closeStatusButton = CreateButton(contentRoot, "CloseStatus", "×", new Vector2(320f, 190f));
            closeRecipeButton = closeStatusButton;
            changeRecipeButton = CreateButton(statusPanel, "ChangeRecipe", "レシピ変更", new Vector2(-220f, 190f), 160f);
            ejectButton = CreateButton(statusPanel, "Eject", "排出", new Vector2(40f, 190f), 120f);

            statusTitle = CreateText(contentRoot, "生産", 36f, TextAlignmentOptions.Center, "MenuTitle");
            Place(statusTitle.rectTransform, 0f, 140f, 400f, 44f);

            statusProductIcon = CreateImage(statusPanel, "Product", Color.white);
            Place(statusProductIcon.rectTransform, 0f, 40f, 72f, 72f);
            statusProductIcon.gameObject.SetActive(false);

            statusBody = CreateText(statusPanel, string.Empty, 24f, TextAlignmentOptions.TopLeft, "StatusBody");
            Place(statusBody.rectTransform, -240f, -40f, 200f, 120f);
            statusBody.gameObject.SetActive(false);

            statusMessage = CreateText(statusPanel, string.Empty, 28f, TextAlignmentOptions.Center, "StatusMessage");
            Place(statusMessage.rectTransform, 0f, -70f, 520f, 40f);

            statusTimeText = CreateText(statusPanel, string.Empty, 24f, TextAlignmentOptions.Center, "StatusTime");
            Place(statusTimeText.rectTransform, 0f, -110f, 400f, 32f);

            GameObject progressBg = CreatePanel(statusPanel, "ProgressBg", new Color(0.12f, 0.08f, 0.05f, 0.95f));
            Place(progressBg.GetComponent<RectTransform>(), 0f, -160f, 520f, 32f);
            statusProgressBackground = progressBg.GetComponent<Image>();
            statusProgressFill = CreateImage(progressBg.transform, "ProgressFill", new Color(0.95f, 0.4f, 0.2f, 1f));
            StretchFull(statusProgressFill.rectTransform);
            statusProgressFill.type = Image.Type.Filled;
            statusProgressFill.fillMethod = Image.FillMethod.Horizontal;
            statusProgressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            statusProgressFill.fillAmount = 0f;
            statusProgressFrame = CreateImage(progressBg.transform, "ProgressFrame", Color.white);
            StretchFull(statusProgressFrame.rectTransform);
            statusProgressFrame.raycastTarget = false;
            statusRecipeProgressBorder = CreateImage(statusPanel, "RecipeProgressBorder", Color.white);
            Place(statusRecipeProgressBorder.rectTransform, 0f, -95f, 560f, 12f);
            statusRecipeProgressBorder.raycastTarget = false;

            // リスナーは Awake の WireButtonListeners で接続する。
            ApplyStatusPanelArt();
        }

        private static Sprite GetUiWhiteSprite()
        {
            if (uiWhiteSprite != null)
            {
                return uiWhiteSprite;
            }

            uiWhiteTexture = Texture2D.whiteTexture;
            uiWhiteSprite = Sprite.Create(
                uiWhiteTexture,
                new Rect(0f, 0f, uiWhiteTexture.width, uiWhiteTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            uiWhiteSprite.name = "FacilityMenuWhite";
            return uiWhiteSprite;
        }

        private static void ApplyUiSprite(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = GetUiWhiteSprite();
            image.type = Image.Type.Simple;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            ApplyUiSprite(image);
            image.color = color;
            return go;
        }

        private Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            ApplyUiSprite(image);
            image.color = color;
            image.preserveAspect = true;
            return image;
        }

        private TMP_Text CreateText(
            Transform parent,
            string value,
            float size,
            TextAlignmentOptions align,
            string objectName = "Text")
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            TMP_FontAsset font = menuFont;
            if (font == null)
            {
                font = TMP_Settings.defaultFontAsset;
            }

            if (font != null)
            {
                text.font = font;
                if (font.material != null)
                {
                    text.fontSharedMaterial = font.material;
                }
            }

            return text;
        }

        private Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPos,
            float width = 64f)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 48f);
            rt.anchoredPosition = anchoredPos;
            Image image = go.GetComponent<Image>();
            ApplyUiSprite(image);
            image.color = new Color(0.55f, 0.38f, 0.2f, 1f);
            Button button = go.GetComponent<Button>();
            TMP_Text text = CreateText(go.transform, label, 24f, TextAlignmentOptions.Center, name + "Label");
            StretchFull(text.rectTransform);
            return button;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
