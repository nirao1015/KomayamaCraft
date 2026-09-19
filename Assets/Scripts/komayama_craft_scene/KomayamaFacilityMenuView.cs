using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// 施設メニュー（レシピ選択／素材・生産品）。仮UI。SystemCanvas 配下に置く。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaFacilityMenuView : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform recipePanel;
        [SerializeField] private RectTransform statusPanel;
        [SerializeField] private Transform recipeGrid;
        [SerializeField] private TMP_Text recipeDetailTitle;
        [SerializeField] private TMP_Text recipeDetailBody;
        [SerializeField] private Image recipeDetailIcon;
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

        private KomayamaProcessingFacility current;
        private readonly List<Button> recipeButtons = new();
        private RecipeDefinition previewRecipe;
        private RectTransform contentRoot;
        private RectTransform craftRowRoot;
        private TMP_Text statusTimeText;
        private static Sprite uiWhiteSprite;
        private static Texture2D uiWhiteTexture;

        public bool IsOpen =>
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
            HideAll();
            BindFacility(null);
        }

        private void OpenRecipeSelectForCurrent()
        {
            if (current == null)
            {
                return;
            }

            ShowRecipePanel();
        }

        private void OnEject()
        {
            if (current == null)
            {
                return;
            }

            current.EjectContents();
            RefreshStatusTexts();
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

            RefreshStatusTexts();
        }

        private void HideAll()
        {
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

        private void RebuildRecipeGrid()
        {
            for (int i = 0; i < recipeButtons.Count; i++)
            {
                if (recipeButtons[i] != null)
                {
                    Destroy(recipeButtons[i].gameObject);
                }
            }

            recipeButtons.Clear();
            if (current == null || current.Definition == null || recipeGrid == null)
            {
                return;
            }

            IReadOnlyList<RecipeDefinition> recipes = current.Definition.SupportedRecipes;
            previewRecipe = recipes.Count > 0 ? recipes[0] : null;
            for (int i = 0; i < 9; i++)
            {
                RecipeDefinition recipe = i < recipes.Count ? recipes[i] : null;
                Button button = CreateRecipeSlot(recipe, i);
                recipeButtons.Add(button);
            }

            RefreshRecipeDetail(previewRecipe);
        }

        private Button CreateRecipeSlot(RecipeDefinition recipe, int index)
        {
            GameObject slot = new GameObject(
                $"RecipeSlot_{index}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            slot.transform.SetParent(recipeGrid, false);
            RectTransform rt = slot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(88f, 88f);
            Image bg = slot.GetComponent<Image>();
            ApplyUiSprite(bg);
            bg.color = recipe != null
                ? new Color(0.45f, 0.32f, 0.18f, 0.95f)
                : new Color(0.25f, 0.2f, 0.15f, 0.85f);
            Button button = slot.GetComponent<Button>();

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(slot.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.15f, 0.15f);
            iconRt.anchorMax = new Vector2(0.85f, 0.85f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            Image icon = iconGo.GetComponent<Image>();
            if (recipe != null && recipe.Outputs.Count > 0 && recipe.Outputs[0].Item != null)
            {
                icon.sprite = recipe.Outputs[0].Item.Icon;
                icon.color = Color.white;
                icon.preserveAspect = true;
            }
            else
            {
                ApplyUiSprite(icon);
                icon.color = new Color(0.6f, 0.55f, 0.45f, 1f);
                TMP_Text q = CreateText(slot.transform, "?", 36f, TextAlignmentOptions.Center);
                RectTransform qRt = q.rectTransform;
                qRt.anchorMin = Vector2.zero;
                qRt.anchorMax = Vector2.one;
                qRt.offsetMin = Vector2.zero;
                qRt.offsetMax = Vector2.zero;
            }

            if (recipe != null)
            {
                RecipeDefinition captured = recipe;
                button.onClick.AddListener(() => OnRecipeClicked(captured));
            }
            else
            {
                button.interactable = false;
            }

            return button;
        }

        private void OnRecipeClicked(RecipeDefinition recipe)
        {
            previewRecipe = recipe;
            RefreshRecipeDetail(recipe);
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

            ShowStatusPanel();
        }

        private void RefreshRecipeDetail(RecipeDefinition recipe)
        {
            if (recipeDetailTitle != null)
            {
                recipeDetailTitle.text = recipe != null ? recipe.DisplayName : "？";
            }

            if (recipeDetailBody != null)
            {
                if (recipe == null)
                {
                    recipeDetailBody.text = "未解放のレシピです。";
                }
                else
                {
                    string inputs = string.Empty;
                    for (int i = 0; i < recipe.Inputs.Count; i++)
                    {
                        ItemAmount req = recipe.Inputs[i];
                        if (req.Item == null)
                        {
                            continue;
                        }

                        if (inputs.Length > 0)
                        {
                            inputs += "\n";
                        }

                        inputs += $"{req.Item.DisplayName} x{req.Amount}";
                    }

                    recipeDetailBody.text =
                        $"{recipe.ProcessingSeconds:0.##}秒\n必要素材:\n{inputs}";
                }
            }

            if (recipeDetailIcon != null)
            {
                recipeDetailIcon.sprite = recipe != null &&
                                         recipe.Outputs.Count > 0 &&
                                         recipe.Outputs[0].Item != null
                    ? recipe.Outputs[0].Item.Icon
                    : GetUiWhiteSprite();
                recipeDetailIcon.enabled = true;
                recipeDetailIcon.color = recipeDetailIcon.sprite != null
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.2f);
            }
        }

        private void RefreshStatusTexts()
        {
            if (current == null || statusPanel == null)
            {
                return;
            }

            RecipeDefinition recipe = current.Recipe;
            if (statusTitle != null)
            {
                statusTitle.text = recipe != null ? recipe.DisplayName : "未選択";
            }

            if (statusMessage != null)
            {
                statusMessage.text = current.DescribeStatusMessage();
            }

            RebuildCraftRow(recipe);

            float progress = current.State == KomayamaFacilityState.Processing
                ? current.Progress01
                : 0f;
            if (statusProgressFill != null)
            {
                statusProgressFill.type = Image.Type.Filled;
                statusProgressFill.fillMethod = Image.FillMethod.Horizontal;
                statusProgressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                statusProgressFill.fillAmount = progress;
                statusProgressFill.color = current.State == KomayamaFacilityState.Processing
                    ? new Color(0.95f, 0.4f, 0.2f, 1f)
                    : new Color(0.35f, 0.28f, 0.2f, 1f);
            }

            if (statusProgress != null)
            {
                statusProgress.gameObject.SetActive(false);
            }

            if (statusTimeText != null)
            {
                statusTimeText.text = recipe != null
                    ? $"{current.RemainingSeconds:0.00} / {recipe.ProcessingSeconds:0.##} 秒"
                    : string.Empty;
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
            if (craftRowRoot == null)
            {
                GameObject row = new GameObject("CraftRow", typeof(RectTransform));
                row.transform.SetParent(statusPanel, false);
                craftRowRoot = row.GetComponent<RectTransform>();
                Place(craftRowRoot, 0f, 25f, 600f, 140f);
            }

            for (int i = craftRowRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(craftRowRoot.GetChild(i).gameObject);
            }

            if (recipe == null)
            {
                return;
            }

            float x = -230f;
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                ItemAmount req = recipe.Inputs[i];
                if (req.Item == null)
                {
                    continue;
                }

                int have = current.GetInputAmount(req.Item);
                CreateMaterialSlot(craftRowRoot, req.Item, have, req.Amount, new Vector2(x, 10f));
                x += 100f;
                if (i < recipe.Inputs.Count - 1)
                {
                    TMP_Text plus = CreateText(craftRowRoot, "+", 36f, TextAlignmentOptions.Center);
                    Place(plus.rectTransform, x - 50f, 20f, 40f, 40f);
                }
            }

            if (current.UsesFuel && current.Definition != null &&
                current.Definition.AcceptedFuelItems.Count > 0)
            {
                ItemDefinition fuelIconItem = current.Definition.AcceptedFuelItems[0];
                TMP_Text plus = CreateText(craftRowRoot, "+", 36f, TextAlignmentOptions.Center);
                Place(plus.rectTransform, x - 10f, 20f, 40f, 40f);
                CreateMaterialSlot(
                    craftRowRoot,
                    fuelIconItem,
                    current.FuelAmount,
                    1,
                    new Vector2(x + 40f, 10f),
                    "燃料");
            }

            TMP_Text arrow = CreateText(craftRowRoot, "→", 42f, TextAlignmentOptions.Center);
            Place(arrow.rectTransform, 90f, 20f, 50f, 50f);

            ItemDefinition output = recipe.Outputs.Count > 0 ? recipe.Outputs[0].Item : null;
            int outAmount = recipe.Outputs.Count > 0 ? recipe.Outputs[0].Amount : 1;
            CreateMaterialSlot(craftRowRoot, output, outAmount, outAmount, new Vector2(170f, 10f), null, true);
        }

        private void CreateMaterialSlot(
            Transform parent,
            ItemDefinition item,
            int have,
            int need,
            Vector2 pos,
            string labelOverride = null,
            bool outputOnly = false)
        {
            GameObject slot = CreatePanel(parent, "MatSlot", new Color(0.22f, 0.15f, 0.1f, 0.95f));
            RectTransform slotRt = slot.GetComponent<RectTransform>();
            Place(slotRt, pos.x, pos.y, 88f, 110f);

            Image icon = CreateImage(slot.transform, "Icon", Color.white);
            Place(icon.rectTransform, 0f, 18f, 64f, 64f);
            if (item != null && item.Icon != null)
            {
                icon.sprite = item.Icon;
                icon.color = Color.white;
            }
            else
            {
                icon.color = new Color(0.85f, 0.7f, 0.3f, 1f);
            }

            string countText = outputOnly ? $"x{need}" : $"{have}/{need}";
            Color countColor = outputOnly || have >= need
                ? new Color(0.6f, 1f, 0.6f, 1f)
                : new Color(1f, 0.55f, 0.45f, 1f);
            TMP_Text count = CreateText(slot.transform, countText, 22f, TextAlignmentOptions.Center);
            count.color = countColor;
            Place(count.rectTransform, 0f, -40f, 84f, 28f);

            if (!string.IsNullOrEmpty(labelOverride))
            {
                TMP_Text label = CreateText(slot.transform, labelOverride, 16f, TextAlignmentOptions.Center);
                Place(label.rectTransform, 0f, 48f, 84f, 22f);
            }
        }

        private void EnsureUi()
        {
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

            TMP_Text recipeHeader = CreateText(recipePanel, "メニュー", 40f, TextAlignmentOptions.Center);
            Place(recipeHeader.rectTransform, 0f, 200f, 400f, 48f);

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

            recipeDetailTitle = CreateText(recipePanel, "詳細", 32f, TextAlignmentOptions.Left);
            Place(recipeDetailTitle.rectTransform, 180f, 150f, 280f, 40f);
            recipeDetailIcon = CreateImage(recipePanel, "DetailIcon", Color.white);
            Place(recipeDetailIcon.rectTransform, 180f, 40f, 96f, 96f);
            recipeDetailBody = CreateText(recipePanel, string.Empty, 24f, TextAlignmentOptions.TopLeft);
            Place(recipeDetailBody.rectTransform, 180f, -120f, 280f, 200f);

            closeRecipeButton = CreateButton(recipePanel, "CloseRecipe", "×", new Vector2(340f, 200f));
            closeStatusButton = CreateButton(statusPanel, "CloseStatus", "×", new Vector2(320f, 190f));
            changeRecipeButton = CreateButton(statusPanel, "ChangeRecipe", "レシピ変更", new Vector2(-220f, 190f), 160f);
            ejectButton = CreateButton(statusPanel, "Eject", "排出", new Vector2(40f, 190f), 120f);

            statusTitle = CreateText(statusPanel, "生産", 36f, TextAlignmentOptions.Center);
            Place(statusTitle.rectTransform, 0f, 140f, 400f, 44f);

            statusProductIcon = CreateImage(statusPanel, "Product", Color.white);
            Place(statusProductIcon.rectTransform, 0f, 40f, 72f, 72f);
            statusProductIcon.gameObject.SetActive(false);

            statusBody = CreateText(statusPanel, string.Empty, 24f, TextAlignmentOptions.TopLeft);
            Place(statusBody.rectTransform, -240f, -40f, 200f, 120f);
            statusBody.gameObject.SetActive(false);

            statusMessage = CreateText(statusPanel, string.Empty, 28f, TextAlignmentOptions.Center);
            Place(statusMessage.rectTransform, 0f, -70f, 520f, 40f);

            statusTimeText = CreateText(statusPanel, string.Empty, 24f, TextAlignmentOptions.Center);
            Place(statusTimeText.rectTransform, 0f, -110f, 400f, 32f);

            GameObject progressBg = CreatePanel(statusPanel, "ProgressBg", new Color(0.12f, 0.08f, 0.05f, 0.95f));
            Place(progressBg.GetComponent<RectTransform>(), 0f, -160f, 520f, 32f);
            statusProgressFill = CreateImage(progressBg.transform, "ProgressFill", new Color(0.95f, 0.4f, 0.2f, 1f));
            StretchFull(statusProgressFill.rectTransform);
            statusProgressFill.type = Image.Type.Filled;
            statusProgressFill.fillMethod = Image.FillMethod.Horizontal;
            statusProgressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            statusProgressFill.fillAmount = 0f;

            closeRecipeButton.onClick.AddListener(Close);
            closeStatusButton.onClick.AddListener(Close);
            changeRecipeButton.onClick.AddListener(OpenRecipeSelectForCurrent);
            ejectButton.onClick.AddListener(OnEject);
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
            TextAlignmentOptions align)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
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
            TMP_Text text = CreateText(go.transform, label, 24f, TextAlignmentOptions.Center);
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
