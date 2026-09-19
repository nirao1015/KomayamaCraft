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
        private const string SortingLayer = "WorldOverlay";
        private const int SortBase = 40;

        [SerializeField] private TMP_FontAsset font;

        private Transform overlayRoot;
        private Transform materialsRoot;
        private Transform productionRoot;
        private Transform fuelRoot;
        private Image progressFill;
        private Image productIcon;
        private TextMeshProUGUI productCountText;
        private Image fuelFill;
        private GameObject fuelObject;

        private readonly List<MaterialSlotView> materialSlots = new();
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
                materialSlots.Clear();
                progressFill = null;
                productIcon = null;
                productCountText = null;
                fuelFill = null;
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

            if (usesFuel && fuelFill != null)
            {
                int fuel = facility.FuelAmount;
                int cap = Mathf.Max(1, facility.FuelCapacity);
                fuelFill.fillAmount = Mathf.Clamp01(fuel / (float)cap);
                fuelFill.color = fuelFill.fillAmount > 0.25f
                    ? new Color(1f, 0.85f, 0.15f, 1f)
                    : new Color(1f, 0.3f, 0.2f, 1f);
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

            int needed = recipe != null ? recipe.Inputs.Count : 0;
            int signature = needed;
            if (recipe != null)
            {
                for (int i = 0; i < recipe.Inputs.Count; i++)
                {
                    ItemAmount req = recipe.Inputs[i];
                    if (req.Item == null)
                    {
                        continue;
                    }

                    signature = signature * 31 + facility.GetInputAmount(req.Item);
                    signature = signature * 31 + req.Amount;
                    signature = signature * 31 + req.Item.GetInstanceID();
                }
            }

            bool rebuild = materialSlots.Count != needed || signature != lastMaterialSignature;
            lastMaterialSignature = signature;
            if (rebuild)
            {
                for (int i = materialsRoot.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(materialsRoot.GetChild(i).gameObject);
                }

                materialSlots.Clear();
                EnsureMaterialSlotCount(needed);
                LayoutMaterialSlots(needed);
            }

            for (int i = 0; i < materialSlots.Count; i++)
            {
                MaterialSlotView slot = materialSlots[i];
                if (slot == null || slot.Root == null)
                {
                    continue;
                }

                bool show = recipe != null && i < recipe.Inputs.Count && recipe.Inputs[i].Item != null;
                slot.Root.SetActive(show);
                if (!show)
                {
                    continue;
                }

                ItemAmount req = recipe.Inputs[i];
                int have = facility.GetInputAmount(req.Item);
                int need = Mathf.Max(1, req.Amount);
                if (rebuild && slot.Icon != null)
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

            materialSlots.Clear();
            progressFill = null;
            productIcon = null;
            productCountText = null;
            fuelFill = null;
            fuelObject = null;
            materialsRoot = null;
            productionRoot = null;
            fuelRoot = null;
            lastRecipe = null;

            // 旧 SpriteRenderer 版のみ除去。FacilityWorldHud は下で作り直す。
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "ProductOverlay" || child.name == "FuelOverlay")
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            Transform existingHud = transform.Find("FacilityWorldHud");
            if (existingHud != null)
            {
                DestroyImmediate(existingHud.gameObject);
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

            // 右：燃料メーター
            fuelObject = fuelRoot.gameObject;
            Image fuelFrame = CreateUiImage(fuelRoot, "FuelFrame", whiteSprite);
            SetRect(fuelFrame.rectTransform, Vector2.zero, new Vector2(22f, 70f));
            fuelFrame.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            fuelFill = CreateUiImage(fuelRoot, "FuelFill", whiteSprite);
            SetRect(fuelFill.rectTransform, Vector2.zero, new Vector2(16f, 62f));
            fuelFill.type = Image.Type.Filled;
            fuelFill.fillMethod = Image.FillMethod.Vertical;
            fuelFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fuelFill.fillAmount = 0f;
            fuelFill.color = new Color(1f, 0.85f, 0.15f, 1f);

            Image bolt = CreateUiImage(fuelRoot, "Bolt", whiteSprite);
            SetRect(bolt.rectTransform, Vector2.zero, new Vector2(10f, 16f));
            bolt.color = new Color(1f, 1f, 0.4f, 1f);
        }

        private void EnsureMaterialSlotCount(int count)
        {
            if (materialsRoot == null)
            {
                return;
            }

            while (materialSlots.Count < count)
            {
                int index = materialSlots.Count;
                GameObject slotGo = CreateChild(materialsRoot, $"Mat_{index}");
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

                materialSlots.Add(new MaterialSlotView
                {
                    Root = slotGo,
                    Icon = icon,
                    Count = countText
                });
            }
        }

        private void LayoutMaterialSlots(int count)
        {
            if (count <= 0)
            {
                return;
            }

            const float spacing = 58f;
            float startX = -0.5f * (count - 1) * spacing;
            for (int i = 0; i < count; i++)
            {
                if (i >= materialSlots.Count || materialSlots[i].Root == null)
                {
                    continue;
                }

                RectTransform rt = materialSlots[i].Root.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(startX + i * spacing, 0f);
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

            if (fuelRoot != null)
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
