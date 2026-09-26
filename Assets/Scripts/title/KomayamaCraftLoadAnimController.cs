using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LoadCanvas のロード中ループ演出。
/// ImageString: 左→右グラデーション（2秒ループ）。
/// Image (1)(2)(3): 2秒ごとに1枚だけ表示を切り替え。
/// timeScale=0 中でも動くよう unscaled 時間を使う。
/// </summary>
[DisallowMultipleComponent]
public sealed class KomayamaCraftLoadAnimController : MonoBehaviour
{
    public static KomayamaCraftLoadAnimController Instance { get; private set; }

    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int FeatherId = Shader.PropertyToID("_Feather");

    [Header("参照")]
    [SerializeField] private Image imageString;
    [SerializeField] private GameObject[] cycleImages;

    [Header("タイミング")]
    [SerializeField, Min(0.1f)] private float loopSeconds = 2f;
    [SerializeField, Range(0.01f, 0.5f)] private float gradientFeather = 0.18f;

    [Header("シェーダー")]
    [SerializeField] private Shader gradientSweepShader;

    private Material imageStringMaterial;
    private bool playing;
    private float stringElapsed;
    private float cycleElapsed;
    private int cycleIndex;

    public bool IsPlaying => playing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureImageStringMaterial();
        ApplyCycleVisual(0);
        SetStringProgress(0f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (imageStringMaterial != null)
        {
            Destroy(imageStringMaterial);
            imageStringMaterial = null;
        }
    }

    private void Update()
    {
        if (!playing)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float period = Mathf.Max(0.1f, loopSeconds);

        stringElapsed += dt;
        while (stringElapsed >= period)
        {
            stringElapsed -= period;
        }

        SetStringProgress(stringElapsed / period);

        cycleElapsed += dt;
        if (cycleElapsed >= period)
        {
            cycleElapsed -= period;
            cycleIndex++;
            if (cycleImages == null || cycleImages.Length == 0)
            {
                cycleIndex = 0;
            }
            else
            {
                cycleIndex %= cycleImages.Length;
            }

            ApplyCycleVisual(cycleIndex);
        }
    }

    public void Play()
    {
        EnsureImageStringMaterial();
        playing = true;
        stringElapsed = 0f;
        cycleElapsed = 0f;
        cycleIndex = 0;
        ApplyCycleVisual(0);
        SetStringProgress(0f);
    }

    public void Stop()
    {
        playing = false;
        SetStringProgress(0f);
        ApplyCycleVisual(0);
    }

    private void EnsureImageStringMaterial()
    {
        if (imageString == null)
        {
            return;
        }

        if (imageStringMaterial != null)
        {
            return;
        }

        Shader shader = gradientSweepShader;
        if (shader == null)
        {
            shader = Shader.Find("UI/KomayamaLoadGradientSweep");
        }

        if (shader == null)
        {
            Debug.LogWarning("[KomayamaCraftLoadAnimController] gradient shader missing");
            return;
        }

        imageStringMaterial = new Material(shader)
        {
            name = "KomayamaLoadGradientSweep (Instance)",
            hideFlags = HideFlags.HideAndDontSave
        };
        imageStringMaterial.SetFloat(FeatherId, gradientFeather);
        imageString.material = imageStringMaterial;
    }

    private void SetStringProgress(float progress01)
    {
        if (imageStringMaterial == null)
        {
            return;
        }

        imageStringMaterial.SetFloat(ProgressId, Mathf.Clamp01(progress01));
        imageStringMaterial.SetFloat(FeatherId, gradientFeather);
    }

    private void ApplyCycleVisual(int index)
    {
        if (cycleImages == null || cycleImages.Length == 0)
        {
            return;
        }

        for (int i = 0; i < cycleImages.Length; i++)
        {
            GameObject go = cycleImages[i];
            if (go == null)
            {
                continue;
            }

            go.SetActive(i == index);
        }
    }
}
