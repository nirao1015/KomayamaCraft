using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ItemWorkplaceMock : MonoBehaviour, IWorkplaceTarget
{
    [Header("View")]
    [SerializeField] private Image targetImage;

    [Header("Color Sequence")]
    [SerializeField] private Color dropStartColor = Color.red;
    [SerializeField] private Color dropMiddleColor = Color.green;
    [SerializeField] private Color dropEndColor = Color.white;
    [SerializeField] private float startToMiddleSeconds = 0.18f;
    [SerializeField] private float middleToEndSeconds = 0.22f;

    private Coroutine colorRoutine;

    private void Awake()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }
    }

    public bool CanAcceptItem(DraggableItemController item)
    {
        return isActiveAndEnabled && item != null;
    }

    public void OnItemDropped(DraggableItemController item)
    {
        if (!CanAcceptItem(item) || targetImage == null)
        {
            return;
        }

        if (colorRoutine != null)
        {
            StopCoroutine(colorRoutine);
        }

        colorRoutine = StartCoroutine(PlayDropColorSequence());
    }

    private IEnumerator PlayDropColorSequence()
    {
        targetImage.color = dropStartColor;
        yield return LerpColor(dropStartColor, dropMiddleColor, Mathf.Max(0.01f, startToMiddleSeconds));
        yield return LerpColor(dropMiddleColor, dropEndColor, Mathf.Max(0.01f, middleToEndSeconds));
        colorRoutine = null;
    }

    private IEnumerator LerpColor(Color from, Color to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Game02.GameManager.GameplayDelta;
            float t = Mathf.Clamp01(elapsed / duration);
            targetImage.color = Color.LerpUnclamped(from, to, t);
            yield return null;
        }

        targetImage.color = to;
    }
}
