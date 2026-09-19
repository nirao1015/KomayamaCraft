using UnityEngine;

namespace KomayamaCraft
{
    public enum NpcMarkerKind
    {
        None = 0,
        MainQuest = 1,
        SubQuest = 2,
        Delivery = 3,
    }

    /// <summary>
    /// ワールドNPC。頭上マーカー枠と左クリック会話を持つ。
    /// 納品は同オブジェクトの <see cref="KomayamaDepositBin"/> ＋右クリック。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class KomayamaNpc : MonoBehaviour
    {
        private const string FrameChildName = "MarkerFrame";
        private const string IconChildName = "MarkerIcon";

        [Header("会話")]
        [SerializeField] private string dialogueStageKey = "craft_npc_unit05_default";
        [SerializeField] private string standingSpriteKey = "unit05";
        [SerializeField] private KomayamaCraftDialogueOverlay dialogueOverlay;

        [Header("頭上マーカー")]
        [SerializeField] private SpriteRenderer markerFrameRenderer;
        [SerializeField] private SpriteRenderer markerIconRenderer;
        [SerializeField] private Sprite mainQuestIcon;
        [SerializeField] private Sprite subQuestIcon;
        [SerializeField] private Sprite deliveryIcon;
        [SerializeField] private Sprite frameSprite;
        [SerializeField] private NpcMarkerKind markerKind = NpcMarkerKind.None;
        [SerializeField] private Vector2 markerLocalOffset = new Vector2(0f, 0.15f);
        [SerializeField] private float markerWorldScale = 0.55f;

        private static Sprite runtimeFrameSprite;
        private static Sprite runtimeMainSprite;
        private static Sprite runtimeSubSprite;
        private static Sprite runtimeDeliverySprite;

        public NpcMarkerKind MarkerKind => markerKind;
        public string StandingSpriteKey => standingSpriteKey;
        public string DialogueStageKey => dialogueStageKey;

        private void Awake()
        {
            EnsureMarkerHierarchy();
            RefreshMarkerVisual();
        }

        private void OnEnable()
        {
            EnsureMarkerHierarchy();
            RefreshMarkerVisual();
        }

        private void LateUpdate()
        {
            RepositionMarkerAboveSprite();
        }

        public void SetMarker(NpcMarkerKind kind)
        {
            markerKind = kind;
            RefreshMarkerVisual();
        }

        public void SetDialogueStageKey(string stageKey)
        {
            dialogueStageKey = stageKey ?? string.Empty;
        }

        public bool TryTalk()
        {
            if (string.IsNullOrEmpty(dialogueStageKey))
            {
                return false;
            }

            if (KomayamaCraftDialogueOverlay.Instance != null &&
                KomayamaCraftDialogueOverlay.Instance.IsDialogueActive)
            {
                return false;
            }

            KomayamaCraftDialogueOverlay overlay =
                dialogueOverlay != null
                    ? dialogueOverlay
                    : KomayamaCraftDialogueOverlay.Instance;
            if (overlay == null)
            {
                Debug.LogWarning("[KomayamaNpc] DialogueOverlay is missing.", this);
                return false;
            }

            overlay.Play(
                dialogueStageKey,
                managePause: true,
                restoreCamera: false,
                dimAlpha: 0f);
            return true;
        }

        private void EnsureMarkerHierarchy()
        {
            if (markerFrameRenderer == null)
            {
                Transform frameTf = transform.Find(FrameChildName);
                GameObject frameGo = frameTf != null
                    ? frameTf.gameObject
                    : CreateMarkerChild(FrameChildName);
                markerFrameRenderer = frameGo.GetComponent<SpriteRenderer>();
                if (markerFrameRenderer == null)
                {
                    markerFrameRenderer = frameGo.AddComponent<SpriteRenderer>();
                }
            }

            if (markerIconRenderer == null)
            {
                Transform iconTf = transform.Find(IconChildName);
                if (iconTf == null && markerFrameRenderer != null)
                {
                    iconTf = markerFrameRenderer.transform.Find(IconChildName);
                }

                GameObject iconGo;
                if (iconTf != null)
                {
                    iconGo = iconTf.gameObject;
                }
                else if (markerFrameRenderer != null)
                {
                    iconGo = CreateMarkerChild(IconChildName, markerFrameRenderer.transform);
                }
                else
                {
                    iconGo = CreateMarkerChild(IconChildName);
                }

                markerIconRenderer = iconGo.GetComponent<SpriteRenderer>();
                if (markerIconRenderer == null)
                {
                    markerIconRenderer = iconGo.AddComponent<SpriteRenderer>();
                }
            }

            ApplyMarkerRendererDefaults(markerFrameRenderer, sortingOrder: 40);
            ApplyMarkerRendererDefaults(markerIconRenderer, sortingOrder: 41);

            if (markerFrameRenderer.sprite == null)
            {
                markerFrameRenderer.sprite = frameSprite != null
                    ? frameSprite
                    : GetOrCreateRuntimeFrameSprite();
            }

            if (mainQuestIcon == null)
            {
                mainQuestIcon = GetOrCreateRuntimeMainSprite();
            }

            if (subQuestIcon == null)
            {
                subQuestIcon = GetOrCreateRuntimeSubSprite();
            }

            if (deliveryIcon == null)
            {
                deliveryIcon = GetOrCreateRuntimeDeliverySprite();
            }
        }

        private GameObject CreateMarkerChild(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = gameObject.layer;
            return go;
        }

        private static void ApplyMarkerRendererDefaults(SpriteRenderer renderer, int sortingOrder)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.sortingLayerName = "WorldNpc";
            renderer.sortingOrder = sortingOrder;
            renderer.color = Color.white;
            ApplyUnlit(renderer);
        }

        private static Material cachedUnlit;

        private static void ApplyUnlit(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            if (cachedUnlit == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader != null)
                {
                    cachedUnlit = new Material(shader)
                    {
                        name = "KC_NpcMarkerUnlit (Runtime)"
                    };
                }
            }

            if (cachedUnlit != null)
            {
                renderer.sharedMaterial = cachedUnlit;
            }
        }

        private void RefreshMarkerVisual()
        {
            EnsureMarkerHierarchy();
            if (markerFrameRenderer != null)
            {
                markerFrameRenderer.enabled = true;
                float inv = 1f / Mathf.Max(0.0001f, transform.lossyScale.x);
                markerFrameRenderer.transform.localScale =
                    Vector3.one * (markerWorldScale * inv);
            }

            if (markerIconRenderer == null)
            {
                return;
            }

            Sprite icon = null;
            switch (markerKind)
            {
                case NpcMarkerKind.MainQuest:
                    icon = mainQuestIcon;
                    break;
                case NpcMarkerKind.SubQuest:
                    icon = subQuestIcon;
                    break;
                case NpcMarkerKind.Delivery:
                    icon = deliveryIcon;
                    break;
            }

            markerIconRenderer.sprite = icon;
            markerIconRenderer.enabled = icon != null;
            markerIconRenderer.transform.localPosition = Vector3.zero;
            markerIconRenderer.transform.localScale = Vector3.one * 0.72f;
        }

        private void RepositionMarkerAboveSprite()
        {
            if (markerFrameRenderer == null)
            {
                return;
            }

            SpriteRenderer body = GetComponent<SpriteRenderer>();
            Vector3 local = markerLocalOffset;
            if (body != null && body.sprite != null)
            {
                Bounds b = body.sprite.bounds;
                local = new Vector3(
                    b.center.x + markerLocalOffset.x,
                    b.max.y + markerLocalOffset.y,
                    0f);
            }

            markerFrameRenderer.transform.localPosition = local;
        }

        private static Sprite GetOrCreateRuntimeFrameSprite()
        {
            if (runtimeFrameSprite != null)
            {
                return runtimeFrameSprite;
            }

            runtimeFrameSprite = CreateBoxSprite(48, 48, new Color(0.15f, 0.15f, 0.18f, 0.85f), new Color(1f, 1f, 1f, 0.95f), 3);
            runtimeFrameSprite.name = "NPC_MarkerFrame_Runtime";
            return runtimeFrameSprite;
        }

        private static Sprite GetOrCreateRuntimeMainSprite()
        {
            if (runtimeMainSprite != null)
            {
                return runtimeMainSprite;
            }

            runtimeMainSprite = CreateGlyphSprite(64, 64, "!", new Color(1f, 0.85f, 0.2f, 1f));
            runtimeMainSprite.name = "NPC_MarkerMain_Runtime";
            return runtimeMainSprite;
        }

        private static Sprite GetOrCreateRuntimeSubSprite()
        {
            if (runtimeSubSprite != null)
            {
                return runtimeSubSprite;
            }

            runtimeSubSprite = CreateGlyphSprite(64, 64, "?", new Color(0.45f, 0.85f, 1f, 1f));
            runtimeSubSprite.name = "NPC_MarkerSub_Runtime";
            return runtimeSubSprite;
        }

        private static Sprite GetOrCreateRuntimeDeliverySprite()
        {
            if (runtimeDeliverySprite != null)
            {
                return runtimeDeliverySprite;
            }

            runtimeDeliverySprite = CreateCircleSprite(64, 64, new Color(0.35f, 0.95f, 0.45f, 1f));
            runtimeDeliverySprite.name = "NPC_MarkerDelivery_Runtime";
            return runtimeDeliverySprite;
        }

        private static Sprite CreateBoxSprite(
            int width,
            int height,
            Color fill,
            Color border,
            int borderWidth)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "NPC_MarkerFrameTex"
            };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool edge = x < borderWidth ||
                                y < borderWidth ||
                                x >= width - borderWidth ||
                                y >= height - borderWidth;
                    tex.SetPixel(x, y, edge ? border : fill);
                }
            }

            tex.Apply(false, true);
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                64f);
        }

        private static Sprite CreateCircleSprite(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "NPC_MarkerCircleTex"
            };
            float cx = (width - 1) * 0.5f;
            float cy = (height - 1) * 0.5f;
            float outer = Mathf.Min(cx, cy) - 1f;
            float inner = outer * 0.55f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= outer && d >= inner)
                    {
                        tex.SetPixel(x, y, color);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply(false, true);
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                64f);
        }

        private static Sprite CreateGlyphSprite(int width, int height, string glyph, Color color)
        {
            // テクスチャに簡易な！／？形を描く（フォント依存を避ける）
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "NPC_MarkerGlyphTex_" + glyph
            };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }

            if (glyph == "!")
            {
                FillRect(tex, 28, 14, 8, 34, color);
                FillRect(tex, 28, 6, 8, 6, color);
            }
            else
            {
                // ?
                FillRect(tex, 20, 42, 24, 6, color);
                FillRect(tex, 38, 30, 6, 14, color);
                FillRect(tex, 26, 24, 18, 6, color);
                FillRect(tex, 20, 18, 6, 8, color);
                FillRect(tex, 28, 6, 8, 6, color);
            }

            tex.Apply(false, true);
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                64f);
        }

        private static void FillRect(Texture2D tex, int x, int y, int w, int h, Color color)
        {
            for (int yy = y; yy < y + h; yy++)
            {
                for (int xx = x; xx < x + w; xx++)
                {
                    if (xx >= 0 && yy >= 0 && xx < tex.width && yy < tex.height)
                    {
                        tex.SetPixel(xx, yy, color);
                    }
                }
            }
        }
    }
}
