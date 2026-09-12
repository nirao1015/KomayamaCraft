using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Game02
{
    public sealed class WorkUpgradeEd02Controller : WorkUpgradeEdBuyControllerBase
    {
        [Header("DirectionView04 Sprite By Ed02 Level")]
        [SerializeField] private List<Sprite> directionView04Sprites = new List<Sprite>();

        public static void EnsureSceneWorkUpgradeEd02Exists()
        {
            GameObject go = GameObject.Find("PanelCanvas/PanelObject/SidePanelObject/SidePanelView/WorkUpgradeEd02");
            if (go == null)
            {
                go = FindInactiveObjectByName("WorkUpgradeEd02");
            }

            if (go != null && go.GetComponent<WorkUpgradeEd02Controller>() == null)
            {
                go.AddComponent<WorkUpgradeEd02Controller>();
            }
        }

        protected override void ConfigureControllerDefaults()
        {
            basePrice = 14000L;
            priceMultiplier = 1.75d;
            purchaseLimit = 10;
        }

        protected override int GetPurchasedCount()
        {
            return UpgradesManager.Instance != null ? UpgradesManager.Instance.GetWorkUpgradeEd02PurchaseCount() : 0;
        }

        protected override bool ApplyPurchase()
        {
            if (UpgradesManager.Instance == null || !UpgradesManager.Instance.ApplyEd02Purchase())
            {
                return false;
            }

            ApplyDirectionView04SpriteByCurrentLevel();
            return true;
        }

        protected override void RunInitialStatusCheckOnce()
        {
            ApplyDirectionView04SpriteByCurrentLevel();
        }

        private void ApplyDirectionView04SpriteByCurrentLevel()
        {
            int level = UpgradesManager.Instance != null ? UpgradesManager.Instance.GetWorkUpgradeEd02PurchaseCount() : 0;
            Sprite sprite = ResolveSpriteForLevel(level);
            if (sprite == null)
            {
                return;
            }

            ApplySpriteToWorkEditorDirectionView04("WorkEditor_1", sprite);
            ApplySpriteToWorkEditorDirectionView04("WorkEditor_2", sprite);
        }

        private Sprite ResolveSpriteForLevel(int level)
        {
            if (directionView04Sprites == null || directionView04Sprites.Count == 0)
            {
                return null;
            }

            int clamped = Mathf.Clamp(level, 0, directionView04Sprites.Count - 1);
            return directionView04Sprites[clamped];
        }

        private static void ApplySpriteToWorkEditorDirectionView04(string workEditorName, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            GameObject workEditor = FindNamedObject(workEditorName);
            if (workEditor == null)
            {
                return;
            }

            Transform[] all = workEditor.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || !string.Equals(t.name, "DirectionView04", System.StringComparison.Ordinal))
                {
                    continue;
                }

                DirectionView04Controller direction = t.GetComponent<DirectionView04Controller>();
                if (direction != null)
                {
                    direction.SetDisplaySprite(sprite);
                }

                Image image = t.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = sprite;
                    image.preserveAspect = true;
                }

                break;
            }
        }

        private static GameObject FindNamedObject(string objectName)
        {
            GameObject go = GameObject.Find($"UnitCanvas/{objectName}");
            if (go != null)
            {
                return go;
            }

            Transform[] all = Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && string.Equals(t.name, objectName, System.StringComparison.Ordinal))
                {
                    return t.gameObject;
                }
            }

            return null;
        }
    }
}
