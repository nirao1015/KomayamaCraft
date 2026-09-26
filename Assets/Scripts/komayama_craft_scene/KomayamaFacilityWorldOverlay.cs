using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// 施設ワールド上の常時表示。
    /// 上：選択レシピ素材（投入/必要をアイコン重ね）／中央：円形残り時間＋出来予定×個／右：燃料。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaFacilityWorldOverlay : MonoBehaviour
    {
        public const int MaxIngredientDisplay = 6;

        private const string SortingLayer = "WorldOverlay";
        private const int SortBase = 40;

        [SerializeField] private TMP_FontAsset font;
        [Tooltip("使用素材 1〜6 個用レイアウト。位置はコードで動かさない。")]
        [SerializeField] private RectTransform[] ingredientLayouts = new RectTransform[MaxIngredientDisplay];

        [Header("燃料インジケーター")]
        [Tooltip("枠スプライト。未設定時は白四角。")]
        [SerializeField] private Sprite fuelFrameSprite;
        [Tooltip("残量バー。Fill Amount だけ実行時に更新する。")]
        [SerializeField] private Sprite fuelBarSprite;
        [Tooltip("ピクト。残量0で消灯。位置・大きさは Hierarchy で調整。")]
        [SerializeField] private Sprite fuelPictSprite;
        [Tooltip("背景。位置・大きさは Hierarchy で調整。")]
        [SerializeField] private Sprite fuelBackgroundSprite;
        [Tooltip("初回生成時の高さのみ。既存 Fuel 子の Rect はコードで上書きしない。")]
        [SerializeField, Min(1f)] private float fuelMeterHeight = 96f;
        [Tooltip("ON: 毎フレーム足跡右へ自動配置。OFF: Fuel の位置は Inspector／プレハブのまま（推奨）。")]
        [SerializeField] private bool autoPlaceFuelByFootprint;

        private Transform overlayRoot;
        private Transform materialsRoot;
        private Transform productionRoot;
        private Transform fuelRoot;
        private Image progressFill;
        private Image productIcon;
        private TextMeshProUGUI productCountText;
        private Image fuelBackground;
        private Image fuelFill;
        private Image fuelFrame;
        private Image fuelPict;
        private GameObject fuelObject;

        private MaterialSlotView[][] ingredientSlotsByCount;
        private Sprite whiteSprite;
        private Texture2D whiteTexture;
        private Sprite circleSprite;
        private Texture2D circleTexture;
        private Sprite ringSprite;
        private Texture2D ringTexture;
        private float cachedHalfW = 0.5f;
        private float cachedHalfH = 0.5f;
        private FacilityDefinition cachedDefinition;
        private RecipeDefinition lastRecipe;
        private int lastMaterialSignature = int.MinValue;
        private int lastPlannedOutput = int.MinValue;
        private int lastActiveLayoutCount = -1;

        private sealed class MaterialSlotView
        {
            public GameObject Root;
            public Image Icon;
            public TextMeshProUGUI Count;
        }

        private void Awake()
        {
            if (font == null)
            {
                font = TMP_Settings.defaultFontAsset;
            }

            EnsureVisuals();
        }

        public void Refresh(KomayamaProcessingFacility facility)
        {
            if (facility == null)
            {
                return;
            }

            if (overlayRoot == null)
            {
                ingredientSlotsByCount = null;
                lastActiveLayoutCount = -1;
                lastMaterialSignature = int.MinValue;
                progressFill = null;
                productIcon = null;
                productCountText = null;
                fuelBackground = null;
                fuelFill = null;
                fuelFrame = null;
                fuelPict = null;
                fuelObject = null;
                materialsRoot = null;
                productionRoot = null;
                fuelRoot = null;
            }

            EnsureVisuals();
            if (overlayRoot == null)
            {
                return;
            }

            if (materialsRoot == null)
            {
                Transform found = overlayRoot.Find("Materials");
                materialsRoot = found;
            }

            if (productionRoot == null)
            {
                Transform found = overlayRoot.Find("Production");
                productionRoot = found;
            }

            if (fuelRoot == null)
            {
                Transform found = overlayRoot.Find("Fuel");
                fuelRoot = found;
                fuelObject = found != null ? found.gameObject : null;
            }

            BindFuelImages();
            // 子が無いときだけ初期生成。既にある見た目・位置は触らない。
            if (fuelRoot != null && fuelRoot.Find("FuelBk") == null)
            {
                BuildFuelMeterVisuals();
            }

            if (materialsRoot == null || productionRoot == null)
            {
                return;
            }

            if (progressFill == null && productionRoot != null)
            {
                Transform fill = productionRoot.Find("ProgressFill");
                if (fill != null)
                {
                    progressFill = fill.GetComponent<Image>();
                }

                Transform icon = productionRoot.Find("ProductIcon");
                if (icon != null)
                {
                    productIcon = icon.GetComponent<Image>();
                }

                Transform count = productionRoot.Find("ProductCount");
                if (count != null)
                {
                    productCountText = count.GetComponent<TextMeshProUGUI>();
                }
            }

            if (fuelFill == null && fuelRoot != null)
            {
                Transform fill = fuelRoot.Find("FuelFill");
                if (fill != null)
                {
                    fuelFill = fill.GetComponent<Image>();
                }
            }

            CacheFootprint(facility);
            ApplyCounterScaleAndLayout(facility);

            bool hasRecipe = facility.HasSelectedRecipe;
            if (materialsRoot != null)
            {
                materialsRoot.gameObject.SetActive(hasRecipe);
            }

            if (productionRoot != null)
            {
                productionRoot.gameObject.SetActive(hasRecipe);
            }

            bool usesFuel = facility.UsesFuel;
            if (fuelObject != null)
            {
                fuelObject.SetActive(usesFuel);
            }

            if (usesFuel)
            {
                RefreshFuelMeter(facility);
            }

            if (!hasRecipe)
            {
                return;
            }

            RecipeDefinition recipe = facility.Recipe;
            RefreshMaterialSlots(facility, recipe);
            RefreshProduction(facility, recipe);
        }

        private void RefreshMaterialSlots(
            KomayamaProcessingFacility facility,
            RecipeDefinition recipe)
        {
            if (materialsRoot == null)
            {
                return;
            }

            EnsureIngredientLayouts();

            // 燃料は使用素材一覧に含めない。最大6種・固定レイアウト切替。
            var inputs = new List<ItemAmount>(MaxIngredientDisplay);
            if (recipe != null)
            {
                for (int i = 0; i < recipe.Inputs.Count && inputs.Count < MaxIngredientDisplay; i++)
                {
                    ItemAmount req = recipe.Inputs[i];
                    if (req.Item == null)
                    {
                        continue;
                    }

                    inputs.Add(req);
                }
            }

            int count = inputs.Count;
            int signature = count;
            for (int i = 0; i < inputs.Count; i++)
            {
                ItemAmount req = inputs[i];
                signature = signature * 31 + facility.GetInputAmount(req.Item);
                signature = signature * 31 + req.Amount;
                signature = signature * 31 + req.Item.GetInstanceID();
            }

            if (signature == lastMaterialSignature && count == lastActiveLayoutCount)
            {
                // 在庫変化が無いときはスキップ。signature に have を含めているのでここに来るのは同一内容。
                return;
            }

            // 在庫だけ変わった場合も Count 更新が必要なので signature 不一致で続行。
            lastMaterialSignature = signature;
            lastActiveLayoutCount = count;

            SetIngredientLayoutActive(count);
            if (count <= 0 ||
                ingredientSlotsByCount == null ||
                ingredientSlotsByCount[count - 1] == null)
            {
                return;
            }

            MaterialSlotView[] slots = ingredientSlotsByCount[count - 1];
            for (int i = 0; i < slots.Length; i++)
            {
                MaterialSlotView slot = slots[i];
                if (slot == null || slot.Root == null)
                {
                    continue;
                }

                ItemAmount req = inputs[i];
                int have = facility.GetInputAmount(req.Item);
                int need = Mathf.Max(1, req.Amount);
                if (slot.Icon != null)
                {
                    if (req.Item.Icon != null)
                    {
                        slot.Icon.sprite = req.Item.Icon;
                        slot.Icon.color = Color.white;
                    }
                    else
                    {
                        slot.Icon.sprite = whiteSprite;
                        slot.Icon.color = new Color(0.9f, 0.75f, 0.3f, 1f);
                    }
                }

                if (slot.Count != null)
                {
                    slot.Count.text = $"{have}/{need}";
                    slot.Count.color = have >= need
                        ? new Color(0.75f, 1f, 0.75f, 1f)
                        : Color.white;
                }
            }
        }

        private void EnsureIngredientLayouts()
        {
            if (materialsRoot == null)
            {
                return;
            }

            if (ingredientLayouts == null || ingredientLayouts.Length != MaxIngredientDisplay)
            {
                ingredientLayouts = new RectTransform[MaxIngredientDisplay];
            }

            if (ingredientSlotsByCount == null ||
                ingredientSlotsByCount.Length != MaxIngredientDisplay)
            {
                ingredientSlotsByCount = new MaterialSlotView[MaxIngredientDisplay][];
            }

            EnsureSprites();

            for (int count = 1; count <= MaxIngredientDisplay; count++)
            {
                int layoutIndex = count - 1;
                if (ingredientLayouts[layoutIndex] == null)
                {
                    Transform found = materialsRoot.Find($"Layout_{count}");
                    if (found != null)
                    {
                        ingredientLayouts[layoutIndex] = found as RectTransform;
                    }
                    else
                    {
                        ingredientLayouts[layoutIndex] = CreateDefaultIngredientLayout(count);
                    }
                }

                if (ingredientSlotsByCount[layoutIndex] == null)
                {
                    ingredientSlotsByCount[layoutIndex] =
                        CacheWorldIngredientSlots(ingredientLayouts[layoutIndex], count);
                }

                if (ingredientLayouts[layoutIndex] != null)
                {
                    ingredientLayouts[layoutIndex].gameObject.SetActive(false);
                }
            }
        }

        private RectTransform CreateDefaultIngredientLayout(int count)
        {
            GameObject layoutGo = CreateChild(materialsRoot, $"Layout_{count}");
            RectTransform layout = layoutGo.GetComponent<RectTransform>();
            layout.anchorMin = new Vector2(0.5f, 0.5f);
            layout.anchorMax = new Vector2(0.5f, 0.5f);
            layout.pivot = new Vector2(0.5f, 0.5f);
            layout.sizeDelta = new Vector2(360f, 60f);
            layout.anchoredPosition = Vector2.zero;

            // 初期配置のみ。以降は位置をコードで動かさない。
            const float spacing = 58f;
            float startX = -0.5f * (count - 1) * spacing;
            for (int i = 0; i < count; i++)
            {
                GameObject slotGo = CreateChild(layout, $"Slot_{i}");
                RectTransform slotRt = slotGo.GetComponent<RectTransform>();
                slotRt.anchoredPosition = new Vector2(startX + i * spacing, 0f);
                slotRt.sizeDelta = new Vector2(52f, 52f);

                Image frame = CreateUiImage(slotGo.transform, "Frame", whiteSprite);
                SetRect(frame.rectTransform, Vector2.zero, new Vector2(52f, 52f));
                frame.color = new Color(0.12f, 0.18f, 0.28f, 0.92f);

                Image icon = CreateUiImage(slotGo.transform, "Icon", whiteSprite);
                SetRect(icon.rectTransform, Vector2.zero, new Vector2(40f, 40f));
                icon.preserveAspect = true;

                TextMeshProUGUI countText = CreateTmp(slotGo.transform, "Count", 16f);
                SetRect(countText.rectTransform, new Vector2(0f, -8f), new Vector2(52f, 24f));
                countText.alignment = TextAlignmentOptions.Center;
                countText.fontStyle = FontStyles.Bold;
                countText.outlineWidth = 0.22f;
                countText.outlineColor = new Color32(0, 0, 0, 230);
            }

            return layout;
        }

        private static MaterialSlotView[] CacheWorldIngredientSlots(RectTransform layout, int count)
        {
            var slots = new MaterialSlotView[count];
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

                Transform iconTf = slotTf.Find("Icon");
                Transform countTf = slotTf.Find("Count");
                slots[i] = new MaterialSlotView
                {
                    Root = slotTf.gameObject,
                    Icon = iconTf != null ? iconTf.GetComponent<Image>() : null,
                    Count = countTf != null ? countTf.GetComponent<TextMeshProUGUI>() : null
                };
            }

            return slots;
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

        private void RefreshProduction(
            KomayamaProcessingFacility facility,
            RecipeDefinition recipe)
        {
            if (recipe != lastRecipe)
            {
                Sprite sprite = ResolveProductSprite(recipe);
                if (productIcon != null)
                {
                    productIcon.sprite = sprite != null ? sprite : whiteSprite;
                    productIcon.color = sprite != null
                        ? Color.white
                        : new Color(0.95f, 0.75f, 0.25f, 1f);
                }

                lastRecipe = recipe;
            }

            bool processing = facility.State == KomayamaFacilityState.Processing;
            float progress = processing ? facility.Progress01 : 0f;
            // 残り時間＝未完了分を赤で示す（減っていく）
            if (progressFill != null)
            {
                progressFill.fillAmount = processing ? Mathf.Clamp01(1f - progress) : 0f;
                progressFill.gameObject.SetActive(processing || facility.HasSelectedRecipe);
                progressFill.color = new Color(0.95f, 0.22f, 0.18f, 1f);
            }

            int planned = ComputePlannedOutputCount(facility, recipe);
            if (productCountText != null)
            {
                productCountText.text = planned > 0 ? $"×{planned}" : "×0";
                productCountText.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// いまの投入在庫（＋生産中の1回）から見込める成果個数。
        /// </summary>
        private static int ComputePlannedOutputCount(
            KomayamaProcessingFacility facility,
            RecipeDefinition recipe)
        {
            if (recipe == null || recipe.Inputs.Count == 0)
            {
                return 0;
            }

            int craftsFromStock = int.MaxValue;
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                ItemAmount req = recipe.Inputs[i];
                if (req.Item == null || req.Amount <= 0)
                {
                    continue;
                }

                int have = facility.GetInputAmount(req.Item);
                craftsFromStock = Mathf.Min(craftsFromStock, have / req.Amount);
            }

            if (craftsFromStock == int.MaxValue)
            {
                craftsFromStock = 0;
            }

            if (recipe.UsesFuel)
            {
                craftsFromStock = Mathf.Min(craftsFromStock, facility.FuelAmount);
            }

            if (facility.State == KomayamaFacilityState.Processing)
            {
                craftsFromStock += 1;
            }

            int perCraft = 0;
            if (recipe.OutputMode == RecipeOutputMode.WeightedSingle)
            {
                perCraft = recipe.Outputs.Count > 0 ? Mathf.Max(1, recipe.Outputs[0].Amount) : 1;
            }
            else
            {
                for (int i = 0; i < recipe.Outputs.Count; i++)
                {
                    if (recipe.Outputs[i].Item != null)
                    {
                        perCraft += Mathf.Max(0, recipe.Outputs[i].Amount);
                    }
                }

                if (perCraft <= 0)
                {
                    perCraft = 1;
                }
            }

            return Mathf.Max(0, craftsFromStock) * perCraft;
        }

        private void EnsureVisuals()
        {
            EnsureSprites();
            if (overlayRoot != null)
            {
                return;
            }

            ingredientSlotsByCount = null;
            lastActiveLayoutCount = -1;
            lastMaterialSignature = int.MinValue;
            progressFill = null;
            productIcon = null;
            productCountText = null;
            fuelBackground = null;
            fuelFill = null;
            fuelFrame = null;
            fuelPict = null;
            fuelObject = null;
            materialsRoot = null;
            productionRoot = null;
            fuelRoot = null;
            lastRecipe = null;

            // 旧 SpriteRenderer 版のみ除去。
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "ProductOverlay" || child.name == "FuelOverlay")
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            // プレハブに置いた FacilityWorldHud は再利用（燃料の位置・見た目を後から触れるため）。
            Transform existingHud = transform.Find("FacilityWorldHud");
            if (existingHud != null)
            {
                overlayRoot = existingHud;
                materialsRoot = existingHud.Find("Materials");
                productionRoot = existingHud.Find("Production");
                fuelRoot = existingHud.Find("Fuel");
                fuelObject = fuelRoot != null ? fuelRoot.gameObject : null;
                BindFuelImages();
                if (fuelRoot != null && fuelRoot.Find("FuelBk") == null)
                {
                    BuildFuelMeterVisuals();
                }

                ApplyFuelSpritesToExisting();
                return;
            }

            GameObject rootGo = new GameObject("FacilityWorldHud");
            rootGo.transform.SetParent(transform, false);

            Canvas canvas = rootGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerName = SortingLayer;
            canvas.sortingOrder = SortBase;
            rootGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 32f;
            rootGo.AddComponent<GraphicRaycaster>().enabled = false;

            // Canvas 追加で Transform→RectTransform に差し替わるため、この後で保持する
            overlayRoot = rootGo.transform;
            RectTransform rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(220f, 220f);
            rootRt.localScale = Vector3.one * 0.01f;

            materialsRoot = CreateChild(rootGo.transform, "Materials").transform;
            productionRoot = CreateChild(rootGo.transform, "Production").transform;
            fuelRoot = CreateChild(rootGo.transform, "Fuel").transform;
            // 初回のみ既定位置。以降は autoPlaceFuelByFootprint が OFF なら触らない。
            float rightWu = cachedHalfW > 0.01f ? cachedHalfW + 0.55f : 1.05f;
            fuelRoot.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(rightWu * 100f, 0f);

            // 中央：円形リング残り時間＋成果アイコン＋予定個数
            Image progressBack = CreateUiImage(productionRoot, "ProgressBack", ringSprite);
            SetRect(progressBack.rectTransform, Vector2.zero, new Vector2(78f, 78f));
            progressBack.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            progressFill = CreateUiImage(productionRoot, "ProgressFill", ringSprite);
            SetRect(progressFill.rectTransform, Vector2.zero, new Vector2(78f, 78f));
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Radial360;
            progressFill.fillOrigin = (int)Image.Origin360.Top;
            progressFill.fillClockwise = true;
            progressFill.fillAmount = 0f;
            progressFill.color = new Color(0.95f, 0.22f, 0.18f, 1f);

            productIcon = CreateUiImage(productionRoot, "ProductIcon", whiteSprite);
            SetRect(productIcon.rectTransform, Vector2.zero, new Vector2(40f, 40f));
            productIcon.preserveAspect = true;

            productCountText = CreateTmp(productionRoot, "ProductCount", 18f);
            SetRect(productCountText.rectTransform, new Vector2(24f, -20f), new Vector2(52f, 26f));
            productCountText.alignment = TextAlignmentOptions.Center;
            productCountText.text = "×0";
            productCountText.fontStyle = FontStyles.Bold;
            productCountText.outlineWidth = 0.2f;
            productCountText.outlineColor = new Color32(0, 0, 0, 220);

            // 右：燃料メーター（背景・バー・枠・ピクト）
            fuelObject = fuelRoot.gameObject;
            BuildFuelMeterVisuals();
        }

        private void BindFuelImages()
        {
            if (fuelRoot == null)
            {
                return;
            }

            if (fuelBackground == null)
            {
                Transform t = fuelRoot.Find("FuelBk");
                if (t != null)
                {
                    fuelBackground = t.GetComponent<Image>();
                }
            }

            if (fuelFill == null)
            {
                Transform t = fuelRoot.Find("FuelFill");
                if (t != null)
                {
                    fuelFill = t.GetComponent<Image>();
                }
            }

            if (fuelFrame == null)
            {
                Transform t = fuelRoot.Find("FuelFrame");
                if (t != null)
                {
                    fuelFrame = t.GetComponent<Image>();
                }
            }

            if (fuelPict == null)
            {
                Transform t = fuelRoot.Find("FuelPict");
                if (t != null)
                {
                    fuelPict = t.GetComponent<Image>();
                }
            }
        }

        private void BuildFuelMeterVisuals()
        {
            if (fuelRoot == null)
            {
                return;
            }

            // 既に本構成がある場合は位置・サイズを保持（後からユーザーが調整する想定）。
            if (fuelRoot.Find("FuelBk") != null)
            {
                BindFuelImages();
                ApplyFuelSpritesToExisting();
                return;
            }

            fuelBackground = null;
            fuelFill = null;
            fuelFrame = null;
            fuelPict = null;

            Vector2 frameSize = ResolveFuelMeterSize();
            Vector2 barSize = ResolveFuelBarSize(frameSize);
            Vector2 pictSize = ResolveFuelPictSize(frameSize);

            Sprite bkSprite = fuelBackgroundSprite != null ? fuelBackgroundSprite : whiteSprite;
            Sprite barSprite = fuelBarSprite != null ? fuelBarSprite : whiteSprite;
            Sprite frameSprite = fuelFrameSprite != null ? fuelFrameSprite : whiteSprite;
            Sprite pictSprite = fuelPictSprite != null ? fuelPictSprite : whiteSprite;

            fuelBackground = CreateUiImage(fuelRoot, "FuelBk", bkSprite);
            SetRect(fuelBackground.rectTransform, Vector2.zero, frameSize);
            fuelBackground.preserveAspect = true;
            fuelBackground.color = Color.white;
            fuelBackground.raycastTarget = false;

            fuelFill = CreateUiImage(fuelRoot, "FuelFill", barSprite);
            SetRect(fuelFill.rectTransform, Vector2.zero, barSize);
            fuelFill.preserveAspect = true;
            fuelFill.type = Image.Type.Filled;
            fuelFill.fillMethod = Image.FillMethod.Vertical;
            fuelFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fuelFill.fillAmount = 0f;
            fuelFill.color = Color.white;
            fuelFill.raycastTarget = false;

            fuelFrame = CreateUiImage(fuelRoot, "FuelFrame", frameSprite);
            SetRect(fuelFrame.rectTransform, Vector2.zero, frameSize);
            fuelFrame.preserveAspect = true;
            fuelFrame.color = Color.white;
            fuelFrame.raycastTarget = false;

            fuelPict = CreateUiImage(fuelRoot, "FuelPict", pictSprite);
            SetRect(fuelPict.rectTransform, new Vector2(0f, frameSize.y * 0.28f), pictSize);
            fuelPict.preserveAspect = true;
            fuelPict.color = Color.white;
            fuelPict.enabled = false;
            fuelPict.raycastTarget = false;
        }

        private void ApplyFuelSpritesToExisting()
        {
            if (fuelBackground != null && fuelBackgroundSprite != null)
            {
                fuelBackground.sprite = fuelBackgroundSprite;
            }

            if (fuelFill != null && fuelBarSprite != null)
            {
                fuelFill.sprite = fuelBarSprite;
                fuelFill.type = Image.Type.Filled;
                fuelFill.fillMethod = Image.FillMethod.Vertical;
                fuelFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            }

            if (fuelFrame != null && fuelFrameSprite != null)
            {
                fuelFrame.sprite = fuelFrameSprite;
            }

            if (fuelPict != null && fuelPictSprite != null)
            {
                fuelPict.sprite = fuelPictSprite;
            }
        }

        private Vector2 ResolveFuelMeterSize()
        {
            float height = Mathf.Max(1f, fuelMeterHeight);
            Sprite refSprite = fuelFrameSprite != null
                ? fuelFrameSprite
                : fuelBackgroundSprite;
            if (refSprite != null && refSprite.rect.height > 0.01f)
            {
                float aspect = refSprite.rect.width / refSprite.rect.height;
                return new Vector2(height * aspect, height);
            }

            return new Vector2(height * 0.56f, height);
        }

        private Vector2 ResolveFuelBarSize(Vector2 frameSize)
        {
            if (fuelBarSprite == null || fuelBarSprite.rect.height < 0.01f)
            {
                return new Vector2(frameSize.x * 0.72f, frameSize.y * 0.88f);
            }

            float barAspect = fuelBarSprite.rect.width / fuelBarSprite.rect.height;
            // 枠内に収まるよう高さ基準で合わせる
            float barHeight = frameSize.y * 0.9f;
            float barWidth = barHeight * barAspect;
            if (barWidth > frameSize.x * 0.9f)
            {
                barWidth = frameSize.x * 0.9f;
                barHeight = barWidth / barAspect;
            }

            return new Vector2(barWidth, barHeight);
        }

        private Vector2 ResolveFuelPictSize(Vector2 frameSize)
        {
            float side = frameSize.x * 0.55f;
            return new Vector2(side, side);
        }

        private void RefreshFuelMeter(KomayamaProcessingFacility facility)
        {
            BindFuelImages();
            int fuel = Mathf.Max(0, facility.FuelAmount);
            int cap = Mathf.Max(1, facility.FuelCapacity);
            float ratio = Mathf.Clamp01(fuel / (float)cap);

            if (fuelFill != null)
            {
                // 使用のたび（残量／容量）の割合でバーを減らす
                fuelFill.fillAmount = ratio;
                fuelFill.color = Color.white;
            }

            if (fuelPict != null)
            {
                // 空のとき消灯
                bool lit = fuel > 0;
                fuelPict.enabled = lit;
                fuelPict.color = lit ? Color.white : new Color(1f, 1f, 1f, 0f);
            }

            if (fuelBackground != null)
            {
                fuelBackground.color = Color.white;
            }

            if (fuelFrame != null)
            {
                fuelFrame.color = Color.white;
            }
        }

        private void ApplyCounterScaleAndLayout(KomayamaProcessingFacility facility)
        {
            if (overlayRoot == null)
            {
                return;
            }

            Vector3 lossy = transform.lossyScale;
            float sx = Mathf.Abs(lossy.x) > 0.001f ? 1f / lossy.x : 1f;
            float sy = Mathf.Abs(lossy.y) > 0.001f ? 1f / lossy.y : 1f;
            // World Space Canvas：100px ≒ 1wu。親の施設スケールは打ち消す。
            overlayRoot.localScale = new Vector3(sx * 0.01f, sy * 0.01f, 1f);
            overlayRoot.localPosition = Vector3.zero;

            if (materialsRoot != null)
            {
                float topWu = cachedHalfH + 0.55f;
                materialsRoot.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(0f, topWu * 100f);
            }

            if (productionRoot != null)
            {
                productionRoot.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            }

            if (fuelRoot != null && autoPlaceFuelByFootprint)
            {
                float rightWu = cachedHalfW + 0.55f;
                fuelRoot.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(rightWu * 100f, 0f);
            }
        }

        private void CacheFootprint(KomayamaProcessingFacility facility)
        {
            FacilityDefinition def = facility.Definition;
            if (def == null || def == cachedDefinition)
            {
                return;
            }

            cachedDefinition = def;
            float block = 0.5f;
            KCBuildSettings settings = FindAnyObjectByType<KCBuildSettings>();
            if (settings != null)
            {
                block = settings.BlockSize;
            }

            cachedHalfW = def.FootprintWidthBlocks * block * 0.5f;
            cachedHalfH = def.FootprintHeightBlocks * block * 0.5f;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private Image CreateUiImage(Transform parent, string name, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private TextMeshProUGUI CreateTmp(Transform parent, string name, float size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = Color.white;
            text.raycastTarget = false;
            if (font != null)
            {
                text.font = font;
                if (font.material != null)
                {
                    text.fontSharedMaterial = font.material;
                }
            }
            else if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return text;
        }

        private static void SetRect(RectTransform rt, Vector2 anchored, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = size;
        }

        private static Sprite ResolveProductSprite(RecipeDefinition recipe)
        {
            if (recipe == null || recipe.Outputs.Count == 0)
            {
                return null;
            }

            ItemDefinition item = recipe.Outputs[0].Item;
            return item != null ? item.Icon : null;
        }

        private void EnsureSprites()
        {
            if (whiteSprite == null)
            {
                whiteTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[64];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color.white;
                }

                whiteTexture.SetPixels(pixels);
                whiteTexture.Apply();
                whiteTexture.filterMode = FilterMode.Point;
                whiteTexture.hideFlags = HideFlags.HideAndDontSave;
                whiteSprite = Sprite.Create(
                    whiteTexture,
                    new Rect(0f, 0f, 8f, 8f),
                    new Vector2(0.5f, 0.5f),
                    8f);
            }

            if (circleSprite == null)
            {
                const int size = 64;
                circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[size * size];
                float r = (size - 2) * 0.5f;
                Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), c);
                        pixels[y * size + x] = d <= r ? Color.white : Color.clear;
                    }
                }

                circleTexture.SetPixels(pixels);
                circleTexture.Apply();
                circleTexture.filterMode = FilterMode.Bilinear;
                circleTexture.hideFlags = HideFlags.HideAndDontSave;
                circleSprite = Sprite.Create(
                    circleTexture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    size);
            }

            if (ringSprite == null)
            {
                const int size = 64;
                ringTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[size * size];
                float outer = (size - 2) * 0.5f;
                float inner = outer - 8f;
                Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), c);
                        pixels[y * size + x] = d <= outer && d >= inner ? Color.white : Color.clear;
                    }
                }

                ringTexture.SetPixels(pixels);
                ringTexture.Apply();
                ringTexture.filterMode = FilterMode.Bilinear;
                ringTexture.hideFlags = HideFlags.HideAndDontSave;
                ringSprite = Sprite.Create(
                    ringTexture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    size);
            }
        }
    }
}
