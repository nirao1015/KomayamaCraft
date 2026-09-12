using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// UnitUG 中の OjMony.GetMonyText を 0 から当該〈中ボス獲得ポイント〉までカウントアップ表示する。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03UnitUgMonyCountUpController : MonoBehaviour
{
    private const string GetMonyTextChildName = "GetMonyText";

    [SerializeField] private TMP_Text getMonyText;
    [SerializeField, Tooltip("0 から当該ポイントまでのカウントアップ秒（unscaled）。")]
    private float countUpSeconds = 2.4f;
    [SerializeField] private AnimationCurve countUpEase = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private Coroutine countUpRoutine;
    private long targetValue;

    private void Awake()
    {
        ResolveGetMonyTextIfNeeded();
    }

    public void BeginCountUp(long target)
    {
        ResolveGetMonyTextIfNeeded();
        targetValue = Math.Max(0L, target);
        StopCountUpRoutine();

        if (getMonyText == null)
        {
            return;
        }

        ApplyDisplay(0L);
        if (targetValue <= 0L)
        {
            return;
        }

        countUpRoutine = StartCoroutine(CountUpRoutine());
    }

    public void SnapToFinal()
    {
        StopCountUpRoutine();
        ApplyDisplay(targetValue);
    }

    public void Cancel()
    {
        StopCountUpRoutine();
    }

    private IEnumerator CountUpRoutine()
    {
        float duration = Mathf.Max(0.05f, countUpSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
            float u = Mathf.Clamp01(elapsed / duration);
            float eased = countUpEase != null && countUpEase.length > 0 ? countUpEase.Evaluate(u) : u;
            double t = Mathf.Clamp01(eased);
            long shown = (long)Math.Round(targetValue * t);
            if (shown > targetValue)
            {
                shown = targetValue;
            }

            ApplyDisplay(shown);
            yield return null;
        }

        ApplyDisplay(targetValue);
        countUpRoutine = null;
    }

    private void ApplyDisplay(long value)
    {
        if (getMonyText == null)
        {
            return;
        }

        getMonyText.text = value.ToString("N0");
    }

    private void ResolveGetMonyTextIfNeeded()
    {
        if (getMonyText != null)
        {
            return;
        }

        Transform child = transform.Find(GetMonyTextChildName);
        if (child != null)
        {
            getMonyText = child.GetComponent<TMP_Text>();
        }
    }

    private void StopCountUpRoutine()
    {
        if (countUpRoutine == null)
        {
            return;
        }

        StopCoroutine(countUpRoutine);
        countUpRoutine = null;
    }
}
