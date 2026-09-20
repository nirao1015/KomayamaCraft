using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KomayamaCraft
{
    /// <summary>
    /// World 配下の描画帯を Sorting Layer に揃える。
    /// 画面 UI Canvas（Hud / System / Debug）は <see cref="KomayamaScreenCanvasBands"/> 側。
    /// マテリアルの一括差し替えは「不足時のみ・都度トリガ」。毎フレーム全上書きはしない。
    /// 仕様：spec/KomayamaCraft_描画バンド整理_詳細仕様.md §2.3〜§2.4
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class KomayamaWorldLayerDrawOrder : MonoBehaviour
    {
        [Serializable]
        public sealed class SpriteLayerRule
        {
            [Tooltip("World からの相対パス（例: WorldLayers/Layer_Sea）")]
            public string rootPath = string.Empty;

            public string sortingLayerName = "Default";
        }

        [SerializeField]
        private SpriteLayerRule[] spriteRules =
        {
            new SpriteLayerRule { rootPath = "WorldLayers/Layer_Sea", sortingLayerName = "WorldSea" },
            new SpriteLayerRule { rootPath = "WorldLayers/Layer_Continent", sortingLayerName = "WorldContinent" },
            new SpriteLayerRule { rootPath = "WorldLayers/Layer_Objects", sortingLayerName = "WorldObject" },
            new SpriteLayerRule { rootPath = "WorldLayers/Layer_Npc", sortingLayerName = "WorldNpc" },
            new SpriteLayerRule { rootPath = "WorldLayers/Layer_Effects", sortingLayerName = "WorldEffect" },
            new SpriteLayerRule { rootPath = "Layer_Facilities", sortingLayerName = "WorldFacility" },
            new SpriteLayerRule { rootPath = "Layer_Drops", sortingLayerName = "WorldDrop" },
            new SpriteLayerRule { rootPath = "Layer_Mouse", sortingLayerName = "WorldMouse" },
            new SpriteLayerRule { rootPath = "Layer_WorldOverlay", sortingLayerName = "WorldOverlay" },
            new SpriteLayerRule { rootPath = "DropRestrictionGrid", sortingLayerName = "WorldOverlay" },
            new SpriteLayerRule { rootPath = "BuildRestrictionGrid", sortingLayerName = "WorldOverlay" },
            new SpriteLayerRule { rootPath = "BuildPreview", sortingLayerName = "WorldOverlay" },
        };

        [SerializeField]
        private bool applyInEditMode = true;

        [Header("Layer_Objects")]
        [SerializeField]
        [Tooltip("Layer_Objects 配下の SpriteRenderer に既定で付けるアウトライン材")]
        private Material layerObjectsOutlineMaterial;

        [Header("適用タイミング")]
        [SerializeField, Tooltip("Play 中、Sorting Layer のずれだけ定期補正する間隔（秒）。0 で毎フレーム。")]
        private float sortingRepairIntervalSeconds = 0.25f;

        [SerializeField, Tooltip("ON のときだけ Play 中にマテリアル補正も定期実行する。不足分（Lit→Unlit/Outline）の穴埋め用。既定 ON・Preserve 対象は触らない。")]
        private bool repairMaterialsOnInterval = true;

        private static Material cachedUnlitSpriteMaterial;
        private static Material cachedOutlineSpriteMaterial;
        private float sortingRepairElapsed;
        private bool materialsDirty = true;

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
                EditorApplication.hierarchyChanged += OnEditorHierarchyChanged;
            }
#endif
            materialsDirty = true;
            Apply(applyMaterials: true);
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
#endif
        }

#if UNITY_EDITOR
        private void OnEditorHierarchyChanged()
        {
            if (Application.isPlaying || !applyInEditMode)
            {
                return;
            }

            materialsDirty = true;
            Apply(applyMaterials: true);
        }
#endif

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            // 毎フレーム全 Sprite の材を潰さない。Sorting ずれだけ間欠補正。
            float interval = Mathf.Max(0f, sortingRepairIntervalSeconds);
            sortingRepairElapsed += Time.unscaledDeltaTime;
            if (interval <= 0f || sortingRepairElapsed >= interval || materialsDirty)
            {
                sortingRepairElapsed = 0f;
                bool doMaterials = materialsDirty || repairMaterialsOnInterval;
                materialsDirty = false;
                Apply(applyMaterials: doMaterials);
            }
        }

        /// <summary>
        /// ランタイムで Sprite を追加したあと呼ぶ。Sorting ＋不足マテリアル補正を一度だけ行う。
        /// </summary>
        public void RequestApplyMaterials()
        {
            materialsDirty = true;
            if (isActiveAndEnabled && Application.isPlaying)
            {
                materialsDirty = false;
                Apply(applyMaterials: true);
            }
        }

        /// <summary>
        /// シーン内の DrawOrder へ材補正を依頼（スポーン直後用）。
        /// </summary>
        public static void RequestApplyMaterialsInScene()
        {
            KomayamaWorldLayerDrawOrder drawOrder =
                FindFirstObjectByType<KomayamaWorldLayerDrawOrder>();
            if (drawOrder != null)
            {
                drawOrder.RequestApplyMaterials();
            }
        }

        [ContextMenu("Apply Draw Order Now")]
        public void Apply()
        {
            Apply(applyMaterials: true);
        }

        public void Apply(bool applyMaterials)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (!Application.isPlaying && !applyInEditMode)
            {
                return;
            }

            if (spriteRules == null)
            {
                return;
            }

            for (int i = 0; i < spriteRules.Length; i++)
            {
                ApplySpriteRule(spriteRules[i], applyMaterials);
            }
        }

        private void ApplySpriteRule(SpriteLayerRule rule, bool applyMaterials)
        {
            if (rule == null || string.IsNullOrEmpty(rule.rootPath) || string.IsNullOrEmpty(rule.sortingLayerName))
            {
                return;
            }

            if (!SortingLayerExists(rule.sortingLayerName))
            {
                return;
            }

            Transform root = transform.Find(rule.rootPath);
            if (root == null)
            {
                return;
            }

            bool isLayerObjects = IsLayerObjectsPath(rule.rootPath);
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                bool changed = false;
                if (renderer.sortingLayerName != rule.sortingLayerName)
                {
                    renderer.sortingLayerName = rule.sortingLayerName;
                    changed = true;
                }

                if (applyMaterials && !ShouldPreserveSpriteMaterial(renderer))
                {
                    // Layer_Objects はアウトライン材を既定。他帯は Unlit（Lit だと真っ黒になるため）。
                    if (isLayerObjects)
                    {
                        if (ApplyLayerObjectsOutlineMaterial(renderer))
                        {
                            changed = true;
                        }
                    }
                    else if (ApplyUnlitSpriteMaterial(renderer))
                    {
                        changed = true;
                    }
                }

                if (changed)
                {
                    MarkDirty(renderer);
                }
            }
        }

        private static bool IsLayerObjectsPath(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                return false;
            }

            return rootPath.IndexOf("Layer_Objects", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 専用材・演出オーバーレイは Outline／Unlit 強制の対象外。
        /// </summary>
        private static bool ShouldPreserveSpriteMaterial(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return true;
            }

            if (renderer.GetComponent<KomayamaPreserveSpriteMaterial>() != null)
            {
                return true;
            }

            string objectName = renderer.gameObject.name;
            if (objectName == "CocoonOverlay" ||
                objectName == "CocoonGlowInner" ||
                objectName == "CocoonGlowOuter" ||
                objectName == "CocoonWhiteFlashOverlay" ||
                objectName == "ThumpOverlay" ||
                objectName == "FoxJoyAccessory")
            {
                return true;
            }

            Material current = renderer.sharedMaterial;
            if (current != null && current.shader != null)
            {
                string shaderName = current.shader.name;
                if (shaderName.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    shaderName.IndexOf("WhiteFlash", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ApplyLayerObjectsOutlineMaterial(SpriteRenderer renderer)
        {
            Material outline = ResolveLayerObjectsOutlineMaterial();
            if (outline == null)
            {
                return ApplyUnlitSpriteMaterial(renderer);
            }

            if (renderer.sharedMaterial == outline)
            {
                return false;
            }

            Material current = renderer.sharedMaterial;
            if (current != null &&
                current.shader != null &&
                IsOutlineShaderName(current.shader.name) &&
                current != cachedUnlitSpriteMaterial)
            {
                // 既に同系アウトライン（別インスタンス含む）なら維持
                return false;
            }

            // 既に意図した Unlit 系（加算・専用）は上で Preserve。ここは不足分のみ Outline。
            renderer.sharedMaterial = outline;
            return true;
        }

        private Material ResolveLayerObjectsOutlineMaterial()
        {
            if (layerObjectsOutlineMaterial != null)
            {
                return layerObjectsOutlineMaterial;
            }

            if (cachedOutlineSpriteMaterial != null)
            {
                return cachedOutlineSpriteMaterial;
            }

#if UNITY_EDITOR
            cachedOutlineSpriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Materials/KomayamaCraft/SpriteOutline.mat");
            if (cachedOutlineSpriteMaterial != null)
            {
                return cachedOutlineSpriteMaterial;
            }
#endif

            Shader shader = Shader.Find("KomayamaCraft/Sprite-Unlit-Outline");
            if (shader == null)
            {
                return null;
            }

            cachedOutlineSpriteMaterial = new Material(shader)
            {
                name = "KC_LayerObjectsOutline (Runtime)"
            };
            return cachedOutlineSpriteMaterial;
        }

        private static bool IsOutlineShaderName(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
            {
                return false;
            }

            return shaderName.IndexOf("Sprite-Unlit-Outline", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   shaderName.IndexOf("SpriteOutline", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ApplyUnlitSpriteMaterial(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            if (cachedUnlitSpriteMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader != null)
                {
                    cachedUnlitSpriteMaterial = new Material(shader)
                    {
                        name = "KC_WorldSpriteUnlit (Runtime)"
                    };
                }
            }

            if (cachedUnlitSpriteMaterial == null)
            {
                return false;
            }

            if (renderer.sharedMaterial == cachedUnlitSpriteMaterial)
            {
                return false;
            }

            // 既に Unlit 系／アウトライン用なら触らない（再上書き禁止）
            Material current = renderer.sharedMaterial;
            if (current != null &&
                current.shader != null)
            {
                string shaderName = current.shader.name;
                if (shaderName.IndexOf("Unlit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    IsOutlineShaderName(shaderName))
                {
                    return false;
                }
            }

            renderer.sharedMaterial = cachedUnlitSpriteMaterial;
            return true;
        }

        private static bool SortingLayerExists(string name)
        {
            SortingLayer[] layers = SortingLayer.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private static void MarkDirty(UnityEngine.Object target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && target != null)
            {
                EditorUtility.SetDirty(target);
            }
#else
            _ = target;
#endif
        }
    }
}
