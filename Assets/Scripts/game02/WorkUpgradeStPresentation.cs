using UnityEngine;
using UnityEngine.UI;

namespace Game02
{
    /// <summary>
    /// WorkUpgradeSt01〜04 共通の着任ユニット表示復元。
    /// </summary>
    internal static class WorkUpgradeStPresentation
    {
        public static void ResolveAcceptedItemViewReference(
            Transform workplaceRoot,
            ref Image acceptedItemView,
            ref AcceptedItemViewBounceController acceptedItemViewBounce)
        {
            if (workplaceRoot == null)
            {
                return;
            }

            if (acceptedItemView == null)
            {
                Transform acceptedRoot = workplaceRoot.Find("AcceptedItemView");
                if (acceptedRoot != null)
                {
                    acceptedItemView = acceptedRoot.GetComponent<Image>();
                }
            }

            Transform invalidViewRoot = workplaceRoot.Find("InvalidView");
            if (acceptedItemView == null || invalidViewRoot == null)
            {
                return;
            }

            Transform viewTransform = acceptedItemView.transform;
            if (viewTransform != invalidViewRoot && !viewTransform.IsChildOf(invalidViewRoot))
            {
                return;
            }

            Transform acceptedRootResolved = workplaceRoot.Find("AcceptedItemView");
            if (acceptedRootResolved == null)
            {
                return;
            }

            Image img = acceptedRootResolved.GetComponent<Image>();
            if (img == null)
            {
                return;
            }

            acceptedItemView = img;
            if (acceptedItemViewBounce == null)
            {
                acceptedItemViewBounce = acceptedRootResolved.GetComponent<AcceptedItemViewBounceController>();
            }
        }

        public static bool TryApplyAcceptedSprite(Image acceptedItemView, Sprite acceptedSprite, ref string lastAcceptedSpriteName)
        {
            if (acceptedItemView == null || acceptedSprite == null)
            {
                return false;
            }

            lastAcceptedSpriteName = acceptedSprite.name;
            acceptedItemView.sprite = acceptedSprite;
            acceptedItemView.enabled = true;
            return true;
        }

        public static void RestoreSpriteOnAcceptedItemView(Image acceptedItemView, ref string lastAcceptedSpriteName)
        {
            if (acceptedItemView == null)
            {
                return;
            }

            if (acceptedItemView.sprite == null && !string.IsNullOrEmpty(lastAcceptedSpriteName))
            {
                acceptedItemView.sprite = Game02SpriteResolver.ResolveByName(lastAcceptedSpriteName);
            }

            if (acceptedItemView.sprite != null)
            {
                lastAcceptedSpriteName = acceptedItemView.sprite.name;
                acceptedItemView.enabled = true;
            }
        }

        public static string ResolveCaptureSpriteName(Image acceptedItemView, string lastAcceptedSpriteName)
        {
            if (!string.IsNullOrEmpty(lastAcceptedSpriteName))
            {
                return lastAcceptedSpriteName;
            }

            return acceptedItemView != null && acceptedItemView.sprite != null ? acceptedItemView.sprite.name : string.Empty;
        }
    }
}
