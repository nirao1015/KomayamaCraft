using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class Game01EffectManager : HoverOverlayEffectManagerBase
{
    [Serializable]
    private sealed class ScoreAddUIBinding
    {
        [Tooltip("演出対象の AddUI。")]
        public TextMeshProUGUI targetUI;
        [Tooltip("加算表示の総表示時間（秒）。")]
        public float totalDisplaySeconds = 0.8f;
        [Tooltip("上方向の移動量（px）。0 なら移動しません。")]
        public float moveUpPixels = 20f;
        [Tooltip("右方向の移動量（px）。0 なら移動しません。")]
        public float moveRightPixels = 12f;
        [Tooltip("フェードイン時間（秒）。")]
        public float fadeInSeconds = 0.15f;
        [Tooltip("フェードアウト時間（秒）。")]
        public float fadeOutSeconds = 0.3f;
        [Tooltip("true のとき静止時間を秒指定で使用します。false のとき総表示時間から自動計算します。")]
        public bool useExplicitHoldSeconds;
        [Tooltip("静止時間（秒）。useExplicitHoldSeconds が true のときに使用。")]
        public float holdSeconds = 0.2f;

        [NonSerialized] public Coroutine activeCoroutine;
        [NonSerialized] public Color baseColor = Color.white;
        [NonSerialized] public bool hasBaseColor;
        [NonSerialized] public Vector2 startAnchoredPosition;
        [NonSerialized] public bool hasStartAnchoredPosition;
    }

    [Header("Score Add UI Effects")]
    [SerializeField] private ScoreAddUIBinding[] scoreAddUiBindings = Array.Empty<ScoreAddUIBinding>();

    protected override void Awake()
    {
        base.Awake();
        ClearAllScoreAddUI();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ClearAllScoreAddUI();
    }

    protected override void OnDestroy()
    {
        StopAllScoreAddCoroutines();
        base.OnDestroy();
    }

    public bool PlayScoreAddUI(TextMeshProUGUI targetUI, int addScore)
    {
        if (targetUI == null || addScore <= 0)
        {
            return false;
        }

        ScoreAddUIBinding binding = FindBinding(targetUI);
        if (binding == null)
        {
            return false;
        }

        if (binding.activeCoroutine != null)
        {
            StopCoroutine(binding.activeCoroutine);
            binding.activeCoroutine = null;
        }

        if (!targetUI.gameObject.activeSelf)
        {
            targetUI.gameObject.SetActive(true);
        }

        targetUI.text = $"+{addScore.ToString("#,0", CultureInfo.InvariantCulture)}";
        SetScoreAddUIAlpha(binding, 0f);
        SetScoreAddUIAnchoredPosition(binding, 0f);
        binding.activeCoroutine = StartCoroutine(PlayScoreAddUIAnimationCoroutine(binding));
        return true;
    }

    public bool PlayScoreAddUI(GameObject targetObject, int addScore)
    {
        if (targetObject == null)
        {
            return false;
        }

        TextMeshProUGUI targetUI = targetObject.GetComponent<TextMeshProUGUI>();
        return PlayScoreAddUI(targetUI, addScore);
    }

    private ScoreAddUIBinding FindBinding(TextMeshProUGUI targetUI)
    {
        if (scoreAddUiBindings == null)
        {
            return null;
        }

        for (int i = 0; i < scoreAddUiBindings.Length; i++)
        {
            ScoreAddUIBinding binding = scoreAddUiBindings[i];
            if (binding != null && binding.targetUI == targetUI)
            {
                return binding;
            }
        }

        return null;
    }

    private void ClearAllScoreAddUI()
    {
        if (scoreAddUiBindings == null)
        {
            return;
        }

        for (int i = 0; i < scoreAddUiBindings.Length; i++)
        {
            ScoreAddUIBinding binding = scoreAddUiBindings[i];
            if (binding == null)
            {
                continue;
            }

            if (binding.activeCoroutine != null)
            {
                StopCoroutine(binding.activeCoroutine);
                binding.activeCoroutine = null;
            }

            TextMeshProUGUI targetUI = binding.targetUI;
            if (targetUI == null)
            {
                continue;
            }

            targetUI.text = string.Empty;
            SetScoreAddUIAlpha(binding, 0f);
            SetScoreAddUIAnchoredPosition(binding, 0f);
            if (targetUI.gameObject.activeSelf)
            {
                targetUI.gameObject.SetActive(false);
            }
        }
    }

    private void StopAllScoreAddCoroutines()
    {
        if (scoreAddUiBindings == null)
        {
            return;
        }

        for (int i = 0; i < scoreAddUiBindings.Length; i++)
        {
            ScoreAddUIBinding binding = scoreAddUiBindings[i];
            if (binding == null || binding.activeCoroutine == null)
            {
                continue;
            }

            StopCoroutine(binding.activeCoroutine);
            binding.activeCoroutine = null;
        }
    }

    private IEnumerator PlayScoreAddUIAnimationCoroutine(ScoreAddUIBinding binding)
    {
        TextMeshProUGUI targetUI = binding.targetUI;
        if (targetUI == null)
        {
            binding.activeCoroutine = null;
            yield break;
        }

        float fadeIn = Mathf.Max(0f, binding.fadeInSeconds);
        float fadeOut = Mathf.Max(0f, binding.fadeOutSeconds);
        float hold;
        if (binding.useExplicitHoldSeconds)
        {
            hold = Mathf.Max(0f, binding.holdSeconds);
        }
        else
        {
            float total = Mathf.Max(0.01f, binding.totalDisplaySeconds);
            total = Mathf.Max(total, fadeIn + fadeOut);
            hold = Mathf.Max(0f, total - fadeIn - fadeOut);
        }

        float elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.deltaTime;
            float t = fadeIn > 0f ? Mathf.Clamp01(elapsed / fadeIn) : 1f;
            SetScoreAddUIAlpha(binding, t);
            SetScoreAddUIAnchoredPosition(binding, 0f);
            yield return null;
        }

        SetScoreAddUIAlpha(binding, 1f);
        SetScoreAddUIAnchoredPosition(binding, 0f);

        elapsed = 0f;
        while (elapsed < hold)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < fadeOut)
        {
            elapsed += Time.deltaTime;
            float t = fadeOut > 0f ? Mathf.Clamp01(elapsed / fadeOut) : 1f;
            SetScoreAddUIAlpha(binding, 1f - t);
            SetScoreAddUIAnchoredPosition(binding, t);
            yield return null;
        }

        targetUI.text = string.Empty;
        SetScoreAddUIAlpha(binding, 0f);
        SetScoreAddUIAnchoredPosition(binding, 0f);
        if (targetUI.gameObject.activeSelf)
        {
            targetUI.gameObject.SetActive(false);
        }

        binding.activeCoroutine = null;
    }

    private static void SetScoreAddUIAlpha(ScoreAddUIBinding binding, float alpha)
    {
        TextMeshProUGUI targetUI = binding.targetUI;
        if (targetUI == null)
        {
            return;
        }

        if (!binding.hasBaseColor)
        {
            binding.baseColor = targetUI.color;
            binding.hasBaseColor = true;
        }

        Color color = binding.baseColor;
        color.a = Mathf.Clamp01(alpha);
        targetUI.color = color;
    }

    private static void SetScoreAddUIAnchoredPosition(ScoreAddUIBinding binding, float normalized)
    {
        TextMeshProUGUI targetUI = binding.targetUI;
        if (targetUI == null)
        {
            return;
        }

        RectTransform rect = targetUI.rectTransform;
        if (!binding.hasStartAnchoredPosition)
        {
            binding.startAnchoredPosition = rect.anchoredPosition;
            binding.hasStartAnchoredPosition = true;
        }

        float t = Mathf.Clamp01(normalized);
        float x = binding.startAnchoredPosition.x + (binding.moveRightPixels * t);
        float y = binding.startAnchoredPosition.y + (binding.moveUpPixels * t);
        rect.anchoredPosition = new Vector2(x, y);
    }
}
