using System.Collections;
using UnityEngine;

/// <summary>
/// 爆弾アイテム取得時: <c>BombEffectImage</c> を一度表示し、<see cref="slideDurationSeconds"/> かけて
/// <c>localScale</c> を 0 から演出開始時点の値まで増やし、終了後は非表示にしてスケールを元に戻す。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03BombItemEffectPlayer : MonoBehaviour
{
    [SerializeField, Tooltip("爆弾取得時に表示する BombEffectImage（RectTransform）。")]
    private RectTransform bombEffectImage;
    [SerializeField, Min(0.02f), Tooltip("表示時、localScale を 0 から現在値まで伸ばす時間（秒・unscaled）。")]
    private float slideDurationSeconds = 0.4f;

    private Coroutine routine;

    public void PlayIfConfigured()
    {
        if (bombEffectImage == null)
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        routine = StartCoroutine(ScaleUpRoutine());
    }

    private IEnumerator ScaleUpRoutine()
    {
        RectTransform rt = bombEffectImage;
        Vector3 targetScale = rt.localScale;
        float dur = Mathf.Max(0.02f, slideDurationSeconds);

        rt.gameObject.SetActive(true);
        rt.localScale = Vector3.zero;

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            rt.localScale = Vector3.Lerp(Vector3.zero, targetScale, u);
            yield return null;
        }

        rt.localScale = targetScale;
        rt.gameObject.SetActive(false);
        routine = null;
    }
}
