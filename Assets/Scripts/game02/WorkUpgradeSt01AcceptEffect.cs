using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game02
{
    /// <summary>
    /// WorkStreamingAcceptEffect と同等の着任演出（work_upgrade_st01_spec.md）。
    /// </summary>
    public sealed class WorkUpgradeSt01AcceptEffect : MonoBehaviour
    {
        [Header("Arrival Scale")]
        [SerializeField] private float initialArrivalScaleMultiplier = 1.2f;
        [SerializeField] private float arrivalShrinkDurationSeconds = 0.35f;

        public IEnumerator Play(Sprite acceptedSprite, Image acceptedItemView)
        {
            if (acceptedSprite == null || acceptedItemView == null)
            {
                yield break;
            }

            RectTransform rect = acceptedItemView.rectTransform;
            Vector3 originalScale = rect.localScale;
            float multiplier = Mathf.Max(0.01f, initialArrivalScaleMultiplier);
            float duration = Mathf.Max(0f, arrivalShrinkDurationSeconds);

            acceptedItemView.sprite = acceptedSprite;
            acceptedItemView.enabled = true;
            rect.localScale = originalScale * multiplier;

            if (duration <= 0f)
            {
                rect.localScale = originalScale;
                yield break;
            }

            float elapsed = 0f;
            Vector3 fromScale = originalScale * multiplier;
            while (elapsed < duration)
            {
                if (GameManager.Instance != null && GameManager.Instance.IsPaused)
                {
                    yield return null;
                    continue;
                }

                elapsed += GameManager.GameplayDelta;
                float t = Mathf.Clamp01(elapsed / duration);
                rect.localScale = Vector3.LerpUnclamped(fromScale, originalScale, t);
                yield return null;
            }

            rect.localScale = originalScale;
        }
    }
}
