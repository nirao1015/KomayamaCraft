using UnityEngine;

/// <summary>
/// メインユニット用 HP バー: HP フルなら UnitHpOj 非表示、未満なら表示し ImageHpCurrent を左端固定で幅のみ変化（右側が削れる）。HP0 で幅 0。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03UnitHpBarController : MonoBehaviour
{
    [SerializeField] private GameObject unitHpOjRoot;
    [SerializeField] private RectTransform imageHpCurrent;
    [SerializeField] private Game03UnitManager unitManager;

    private float fullBarWidth;
    private float fullBarHeight;
    private float barLeftEdgeAnchoredX;
    private float barAnchoredY;
    private bool layoutCaptured;

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (unitManager == null || unitHpOjRoot == null || imageHpCurrent == null)
        {
            return;
        }

        CaptureLayoutOnce();

        int maxLife = unitManager.MaxLife;
        int cur = unitManager.CurrentLife;
        if (cur >= maxLife)
        {
            unitHpOjRoot.SetActive(false);
            return;
        }

        unitHpOjRoot.SetActive(true);
        float ratio = Mathf.Clamp01((float)cur / maxLife);
        float w = fullBarWidth * ratio;
        imageHpCurrent.sizeDelta = new Vector2(w, fullBarHeight);
        imageHpCurrent.anchoredPosition = new Vector2(barLeftEdgeAnchoredX + w * imageHpCurrent.pivot.x, barAnchoredY);
    }

    private void CaptureLayoutOnce()
    {
        if (layoutCaptured || imageHpCurrent == null)
        {
            return;
        }

        fullBarWidth = Mathf.Max(0f, imageHpCurrent.sizeDelta.x);
        fullBarHeight = imageHpCurrent.sizeDelta.y;
        barLeftEdgeAnchoredX = imageHpCurrent.anchoredPosition.x - imageHpCurrent.sizeDelta.x * imageHpCurrent.pivot.x;
        barAnchoredY = imageHpCurrent.anchoredPosition.y;
        layoutCaptured = true;
    }
}
