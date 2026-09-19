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

        private static Material cachedUnlitSpriteMaterial;
        private static Material cachedOutlineSpriteMaterial;

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
                EditorApplication.hierarchyChanged += OnEditorHierarchyChanged;
            }
#endif
            Apply();
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

            Apply();
        }
#endif

        private void LateUpdate()
        {
            // 編集中に毎フレーム Apply+SetDirty するとシーンの * が消えない
            if (!Application.isPlaying)
            {
                return;
            }

            Apply();
        }

        [ContextMenu("Apply Draw Order Now")]
        public void Apply()
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
                ApplySpriteRule(spriteRules[i]);
            }
        }

        private void ApplySpriteRule(SpriteLayerRule rule)
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

            // 既に Unlit 系／アウトライン用なら触らない
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
