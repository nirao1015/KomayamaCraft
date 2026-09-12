using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game02
{
    public class FieldView : MonoBehaviour
    {
        public enum HoleLayoutPreset
        {
            None = 0,
            // 画像（上半分）に対して「配信/編集/動画」っぽい位置へ穴を当てる簡易プリセット
            Game02_TopHalf_DispatchEditVideoApprox = 1
        }

        [Serializable]
        public struct HoleRectNormalized
        {
            public float x; // left (0..1)
            public float y; // bottom (0..1)
            public float w; // width (0..1)
            public float h; // height (0..1)
        }

        [Serializable]
        public struct HoleRectInspector
        {
            [Min(0f)] public float xPixels; // left from FieldImg sprite left edge (pixels)
            [Min(0f)] public float yPixels; // top from FieldImg sprite top edge (pixels)

            [Min(0f)] public float sidePixels;   // square side length in FieldImg sprite pixels
            [Min(0f)] public float widthPixels;  // rect width in FieldImg sprite pixels
            [Min(0f)] public float heightPixels; // rect height in FieldImg sprite pixels
        }

        [Header("Refs")]
        [SerializeField] private SpriteRenderer fieldImgRenderer;

        [Header("Hole Layout")]
        [SerializeField] private bool autoApplyOnAwake = true;
        [SerializeField] private bool useInspectorHoles = true;
        [SerializeField] private List<HoleRectInspector> initialHoles = new List<HoleRectInspector>(0);
        [SerializeField] private List<HoleRectInspector> ed05AdditionalHoles = new List<HoleRectInspector>(0);

        [SerializeField] private HoleLayoutPreset preset = HoleLayoutPreset.Game02_TopHalf_DispatchEditVideoApprox;
        [SerializeField] private bool applyPresetIfNoInspectorHoles = true;

        [Header("Shader")]
        [SerializeField] private bool autoAssignHoleShader = true;
        [SerializeField] private Shader holeShaderAsset;
        [SerializeField] private string holeShaderName = "Game02/FieldHoleOverlaySprite";

        [Header("FieldImg Size (Pixels)")]
        [SerializeField] private bool usePixelSizedFieldImg = false;
        [SerializeField] private bool keepAspectWhenPixelSizing = false;
        [Min(0f)] [SerializeField] private float fieldImgWidthPixels = 0f;
        [Min(0f)] [SerializeField] private float fieldImgHeightPixels = 0f;

        private MaterialPropertyBlock mpb;

        private static readonly int HoleCountId = Shader.PropertyToID("_HoleCount");
        private static readonly int Hole0Id = Shader.PropertyToID("_Hole0");
        private static readonly int Hole1Id = Shader.PropertyToID("_Hole1");
        private static readonly int Hole2Id = Shader.PropertyToID("_Hole2");
        private static readonly int Hole3Id = Shader.PropertyToID("_Hole3");
        private static readonly int Hole4Id = Shader.PropertyToID("_Hole4");
        private static readonly int Hole5Id = Shader.PropertyToID("_Hole5");
        private static readonly int Hole6Id = Shader.PropertyToID("_Hole6");
        private static readonly int Hole7Id = Shader.PropertyToID("_Hole7");
        private static readonly int Hole8Id = Shader.PropertyToID("_Hole8");
        private static readonly int Hole9Id = Shader.PropertyToID("_Hole9");

        private static readonly int[] HoleIds =
        {
            Hole0Id, Hole1Id, Hole2Id, Hole3Id, Hole4Id,
            Hole5Id, Hole6Id, Hole7Id, Hole8Id, Hole9Id
        };

        private const int MaxHoles = 10;

        private void Awake()
        {
            if (mpb == null)
            {
                mpb = new MaterialPropertyBlock();
            }

            if (fieldImgRenderer == null)
            {
                fieldImgRenderer = GetComponent<SpriteRenderer>();
            }

            ApplyPixelSizedFieldImgIfNeeded();
            EnsureHoleShaderIfNeeded();

            if (!autoApplyOnAwake)
            {
                return;
            }

            if (useInspectorHoles && initialHoles != null && initialHoles.Count > 0)
            {
                ApplyInspectorHoles(initialHoles);
            }
            else if (applyPresetIfNoInspectorHoles && preset != HoleLayoutPreset.None)
            {
                ApplyPreset(preset);
            }

            int purchasedEd05Count = UpgradesManager.Instance != null
                ? UpgradesManager.Instance.GetWorkUpgradeEd05PurchaseCount()
                : 0;
            ApplyEd05AdditionalHolesByPurchaseCount(purchasedEd05Count);
        }

        public void ClearHoles()
        {
            EnsureReady();
            mpb.SetFloat(HoleCountId, 0f);
            fieldImgRenderer.SetPropertyBlock(mpb);
        }

        public void SetSingleHole(HoleRectNormalized hole)
        {
            EnsureReady();
            mpb.SetFloat(HoleCountId, 1f);
            mpb.SetVector(Hole0Id, new Vector4(hole.x, hole.y, hole.w, hole.h));
            fieldImgRenderer.SetPropertyBlock(mpb);
        }

        private void ApplyInspectorHoles(IReadOnlyList<HoleRectInspector> holes)
        {
            EnsureReady();
            if (fieldImgRenderer == null)
            {
                return;
            }

            Sprite sprite = fieldImgRenderer.sprite;
            if (sprite == null)
            {
                Debug.LogError("[FieldView] FieldImg Sprite が null のため穴変換できません。");
                return;
            }

            float spriteW = sprite.rect.width;
            float spriteH = sprite.rect.height;
            if (spriteW <= 0f || spriteH <= 0f)
            {
                Debug.LogError($"[FieldView] FieldImg Sprite.rect invalid. w={spriteW}, h={spriteH}");
                return;
            }

            List<HoleRectNormalized> converted = ConvertInspectorHolesToNormalized(holes, spriteW, spriteH, MaxHoles);
            if (converted.Count <= 0)
            {
                ClearHoles();
                return;
            }

            SetHoles(converted);
        }

        public void ApplyEd05AdditionalHolesByPurchaseCount(int purchasedCount)
        {
            EnsureReady();
            if (fieldImgRenderer == null)
            {
                return;
            }

            Sprite sprite = fieldImgRenderer.sprite;
            if (sprite == null)
            {
                return;
            }

            float spriteW = sprite.rect.width;
            float spriteH = sprite.rect.height;
            if (spriteW <= 0f || spriteH <= 0f)
            {
                return;
            }

            List<HoleRectNormalized> baseHoles = ResolveBaseHolesNormalized(spriteW, spriteH);
            int addCount = Mathf.Clamp(purchasedCount, 0, ed05AdditionalHoles != null ? ed05AdditionalHoles.Count : 0);
            List<HoleRectNormalized> addHoles = ConvertInspectorHolesToNormalized(ed05AdditionalHoles, spriteW, spriteH, addCount);

            List<HoleRectNormalized> merged = new List<HoleRectNormalized>(Mathf.Min(MaxHoles, baseHoles.Count + addHoles.Count));
            for (int i = 0; i < baseHoles.Count && merged.Count < MaxHoles; i++)
            {
                merged.Add(baseHoles[i]);
            }

            for (int i = 0; i < addHoles.Count && merged.Count < MaxHoles; i++)
            {
                merged.Add(addHoles[i]);
            }

            SetHoles(merged);
        }

        public static void RefreshAllByCurrentUpgrades()
        {
            FieldView[] views = FindObjectsOfType<FieldView>(true);
            int purchasedEd05Count = UpgradesManager.Instance != null
                ? UpgradesManager.Instance.GetWorkUpgradeEd05PurchaseCount()
                : 0;
            for (int i = 0; i < views.Length; i++)
            {
                FieldView view = views[i];
                if (view != null)
                {
                    view.ApplyEd05AdditionalHolesByPurchaseCount(purchasedEd05Count);
                }
            }
        }

        public void SetHoles(IReadOnlyList<HoleRectNormalized> holes)
        {
            EnsureReady();
            if (holes == null || holes.Count <= 0)
            {
                ClearHoles();
                return;
            }

            int count = Mathf.Clamp(holes.Count, 0, MaxHoles);
            mpb.SetFloat(HoleCountId, (float)count);

            for (int i = 0; i < count; i++)
            {
                HoleRectNormalized h = holes[i];
                mpb.SetVector(HoleIds[i], new Vector4(h.x, h.y, h.w, h.h));
            }

            fieldImgRenderer.SetPropertyBlock(mpb);
        }

        private void ApplyPreset(HoleLayoutPreset targetPreset)
        {
            EnsureReady();
            List<HoleRectNormalized> holes = BuildPresetHoles(targetPreset);
            if (holes.Count <= 0)
            {
                ClearHoles();
                return;
            }

            SetHoles(holes);
        }

        private void EnsureHoleShaderIfNeeded()
        {
            if (!autoAssignHoleShader)
            {
                return;
            }

            if (fieldImgRenderer == null)
            {
                return;
            }

            Shader holeShader = holeShaderAsset != null
                ? holeShaderAsset
                : Shader.Find(holeShaderName);
            if (holeShader == null)
            {
                Debug.LogError($"[FieldView] hole shader not found. name={holeShaderName}");
                return;
            }

            // 穴あけ用シェーダーに差し替える（既に差し替え済みなら何もしない）
            if (fieldImgRenderer.sharedMaterial == null || fieldImgRenderer.sharedMaterial.shader != holeShader)
            {
                Material runtimeMat = new Material(holeShader);
                runtimeMat.name = "[FieldHoleOverlaySprite] Runtime";
                fieldImgRenderer.sharedMaterial = runtimeMat;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (holeShaderAsset == null)
            {
                holeShaderAsset = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/game02/FieldHoleOverlaySprite.shader");
            }
        }
#endif

        private void ApplyPixelSizedFieldImgIfNeeded()
        {
            if (!usePixelSizedFieldImg || fieldImgRenderer == null)
            {
                return;
            }

            Sprite sprite = fieldImgRenderer.sprite;
            if (sprite == null)
            {
                return;
            }

            float baseWidth = sprite.rect.width / Mathf.Max(1e-6f, sprite.pixelsPerUnit);
            float baseHeight = sprite.rect.height / Mathf.Max(1e-6f, sprite.pixelsPerUnit);
            if (baseWidth <= 0f || baseHeight <= 0f)
            {
                return;
            }

            Vector3 localScale = fieldImgRenderer.transform.localScale;
            bool hasWidth = fieldImgWidthPixels > 0f;
            bool hasHeight = fieldImgHeightPixels > 0f;
            if (!hasWidth && !hasHeight)
            {
                return;
            }

            if (keepAspectWhenPixelSizing)
            {
                float uniformScale = localScale.x;
                if (hasWidth)
                {
                    uniformScale = fieldImgWidthPixels / baseWidth;
                }
                else if (hasHeight)
                {
                    uniformScale = fieldImgHeightPixels / baseHeight;
                }

                localScale.x = uniformScale;
                localScale.y = uniformScale;
            }
            else
            {
                if (hasWidth)
                {
                    localScale.x = fieldImgWidthPixels / baseWidth;
                }

                if (hasHeight)
                {
                    localScale.y = fieldImgHeightPixels / baseHeight;
                }
            }

            fieldImgRenderer.transform.localScale = localScale;
        }

        private void EnsureReady()
        {
            if (fieldImgRenderer != null)
            {
                return;
            }

            fieldImgRenderer = GetComponent<SpriteRenderer>();
            if (fieldImgRenderer == null)
            {
                Debug.LogError("[FieldView] SpriteRenderer (FieldImg) is missing.");
                return;
            }

            if (mpb == null)
            {
                mpb = new MaterialPropertyBlock();
            }
        }

        private List<HoleRectNormalized> ResolveBaseHolesNormalized(float spriteW, float spriteH)
        {
            if (useInspectorHoles && initialHoles != null && initialHoles.Count > 0)
            {
                return ConvertInspectorHolesToNormalized(initialHoles, spriteW, spriteH, MaxHoles);
            }

            if (applyPresetIfNoInspectorHoles && preset != HoleLayoutPreset.None)
            {
                return BuildPresetHoles(preset);
            }

            return new List<HoleRectNormalized>(0);
        }

        private static List<HoleRectNormalized> ConvertInspectorHolesToNormalized(
            IReadOnlyList<HoleRectInspector> holes,
            float spriteW,
            float spriteH,
            int maxCount)
        {
            int maxInput = Mathf.Clamp(holes != null ? holes.Count : 0, 0, Mathf.Clamp(maxCount, 0, MaxHoles));
            List<HoleRectNormalized> converted = new List<HoleRectNormalized>(maxInput);
            for (int i = 0; i < maxInput; i++)
            {
                HoleRectInspector h0 = holes[i];
                float widthPx = h0.widthPixels;
                float heightPx = h0.heightPixels;
                bool hasRect = widthPx > 0f && heightPx > 0f;
                bool hasSide = !hasRect && h0.sidePixels > 0f;
                if (!hasRect && !hasSide)
                {
                    continue;
                }

                if (!hasRect)
                {
                    widthPx = h0.sidePixels;
                    heightPx = h0.sidePixels;
                }

                float uvW = Mathf.Clamp01(widthPx / spriteW);
                float uvH = Mathf.Clamp01(heightPx / spriteH);
                if (uvW <= 0f || uvH <= 0f)
                {
                    continue;
                }

                float leftMax = Mathf.Max(0f, 1f - uvW);
                float topMax = Mathf.Max(0f, 1f - uvH);
                float left01 = h0.xPixels / spriteW;
                float top01 = h0.yPixels / spriteH;
                float left = Mathf.Clamp(left01, 0f, leftMax);
                float top = Mathf.Clamp(top01, 0f, topMax);
                float bottom = Mathf.Clamp01(1f - top - uvH);
                converted.Add(new HoleRectNormalized { x = left, y = bottom, w = uvW, h = uvH });
            }

            return converted;
        }

        private static List<HoleRectNormalized> BuildPresetHoles(HoleLayoutPreset targetPreset)
        {
            switch (targetPreset)
            {
                case HoleLayoutPreset.Game02_TopHalf_DispatchEditVideoApprox:
                    return new List<HoleRectNormalized>
                    {
                        new HoleRectNormalized { x = 0.05f, y = 0.70f, w = 0.26f, h = 0.14f },
                        new HoleRectNormalized { x = 0.34f, y = 0.70f, w = 0.26f, h = 0.14f },
                        new HoleRectNormalized { x = 0.63f, y = 0.70f, w = 0.30f, h = 0.14f },
                        new HoleRectNormalized { x = 0.05f, y = 0.52f, w = 0.15f, h = 0.14f },
                        new HoleRectNormalized { x = 0.23f, y = 0.52f, w = 0.15f, h = 0.14f },
                        new HoleRectNormalized { x = 0.41f, y = 0.52f, w = 0.15f, h = 0.14f },
                        new HoleRectNormalized { x = 0.59f, y = 0.52f, w = 0.15f, h = 0.14f },
                        new HoleRectNormalized { x = 0.77f, y = 0.52f, w = 0.15f, h = 0.14f },
                    };
                default:
                    return new List<HoleRectNormalized>(0);
            }
        }
    }
}

