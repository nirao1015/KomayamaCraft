using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Canvas/TimeUIBackground の幅で残りクリア時間を表示する。仕様: spec/game_clear_time_ui.md
/// </summary>
[DisallowMultipleComponent]
public class TimeUIBackgroundClearTimerController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField, Tooltip("未設定時はこの GameObject の RectTransform")]
    private RectTransform barRect;
    [SerializeField, Tooltip("残量表示に使う Image。")]
    private Image barImage;
    [SerializeField, Tooltip("右端フェザー付き表示に使う Material テンプレート。ビルドでは必ず割り当てるか rightFadeShader を指定すること。")]
    private Material rightFadeMaterialTemplate;
    [SerializeField, Tooltip("Material 未設定時に使用。シーン参照でビルドにシェーダーを含める。")]
    private Shader rightFadeShader;
    [SerializeField, Tooltip("右端フェザー幅（0-1, UV基準）。0 で境界がシャープになります。")]
    private float rightEdgeFeather01 = 0.04f;
    [SerializeField, Tooltip("見た目補正: 消え始めを早める秒数。正の値で早く、負の値で遅く開始します。")]
    private float visualStartOffsetSeconds;
    [SerializeField, Tooltip("見た目補正: 全消えタイミングを遅らせる秒数。正の値で遅く、負の値で早く終了します。")]
    private float visualEndOffsetSeconds;

    [Header("References")]
    [SerializeField, Tooltip("未設定時は実行時に検索")]
    private Game01Manager gameManager;

    private Material runtimeBarMaterial;
    private bool useWidthFallback;
    private float fullBarWidthPixels;
    private bool warnedMaterialSetup;
    private static readonly int ProgressProp = Shader.PropertyToID("_Progress");
    private static readonly int FeatherProp = Shader.PropertyToID("_Feather");

    private void Awake()
    {
        if (barRect == null)
        {
            barRect = GetComponent<RectTransform>();
        }

        if (barImage == null)
        {
            barImage = GetComponent<Image>();
        }

        if (barRect != null)
        {
            fullBarWidthPixels = Mathf.Max(0f, barRect.sizeDelta.x);
        }

        SetupFadeMaterial();
    }

    private void OnEnable()
    {
        warnedMaterialSetup = false;
        SetupFadeMaterial();
    }

    private void Update()
    {
        ResolveReferences();
        if (barRect == null || barImage == null || gameManager == null)
        {
            return;
        }

        float limit = gameManager.ClearTimeLimitSeconds;
        if (limit <= 0f)
        {
            SetBarFill01(1f);
            return;
        }

        if (!gameManager.HasGameplayControlStarted)
        {
            SetBarFill01(1f);
            return;
        }

        float elapsed = gameManager.GameplayElapsedSinceControlSeconds;
        float compensatedElapsed = Mathf.Max(0f, elapsed + visualStartOffsetSeconds);
        float compensatedDuration = Mathf.Max(0.01f, limit + visualStartOffsetSeconds + visualEndOffsetSeconds);
        float t = Mathf.Clamp01(compensatedElapsed / compensatedDuration);
        SetBarFill01(1f - t);
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
        {
            gameManager = Game01Manager.Instance;
        }
    }

    private void SetupFadeMaterial()
    {
        if (barImage == null)
        {
            return;
        }

        if (runtimeBarMaterial != null)
        {
            useWidthFallback = false;
            return;
        }

        Material template = rightFadeMaterialTemplate;
        if (template == null)
        {
            Shader shader = rightFadeShader != null ? rightFadeShader : Shader.Find("UI/RightEdgeFadeFill");
            if (shader != null)
            {
                template = new Material(shader);
            }
        }

        if (template == null)
        {
            useWidthFallback = true;
            return;
        }

        runtimeBarMaterial = new Material(template);
        runtimeBarMaterial.name = $"{template.name} (Runtime)";
        barImage.material = runtimeBarMaterial;
        runtimeBarMaterial.SetFloat(FeatherProp, Mathf.Clamp01(rightEdgeFeather01));
        useWidthFallback = false;
    }

    private void SetBarFill01(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);

        if (runtimeBarMaterial == null)
        {
            SetupFadeMaterial();
        }

        if (runtimeBarMaterial != null)
        {
            runtimeBarMaterial.SetFloat(ProgressProp, normalized);
            runtimeBarMaterial.SetFloat(FeatherProp, Mathf.Clamp01(rightEdgeFeather01));
            return;
        }

        ApplyWidthFallbackFill(normalized);

        if (!warnedMaterialSetup)
        {
            Debug.LogWarning(
                "[TimeUIBackgroundClearTimer] Right edge fade material is unavailable; using width/fill fallback. " +
                "Assign rightFadeMaterialTemplate or rightFadeShader for feathered edges.");
            warnedMaterialSetup = true;
        }
    }

    private void ApplyWidthFallbackFill(float normalized)
    {
        if (barImage != null && barImage.type == Image.Type.Filled)
        {
            barImage.fillAmount = normalized;
        }

        if (barRect == null || fullBarWidthPixels <= 0f)
        {
            return;
        }

        Vector2 size = barRect.sizeDelta;
        size.x = fullBarWidthPixels * normalized;
        barRect.sizeDelta = size;
    }

    private void OnDestroy()
    {
        if (runtimeBarMaterial != null)
        {
            Destroy(runtimeBarMaterial);
            runtimeBarMaterial = null;
        }
    }
}
