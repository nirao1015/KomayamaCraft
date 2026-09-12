using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class UIMosaicEffect : MonoBehaviour
{
    [Header("Mosaic")]
    [SerializeField] private bool enableMosaic = true;
    [SerializeField] private int mosaicPixelSize = 8;
    [SerializeField] private Shader mosaicShaderAsset;
    [SerializeField] private string mosaicShaderName = "UI/Game02/ItemStreamMosaic";

    private Image image;
    private Material runtimeMosaicMaterial;
    private bool hasLoggedMosaicShaderMissing;

    public bool EnableMosaic => enableMosaic;
    public int MosaicPixelSize => Mathf.Max(1, mosaicPixelSize);

    private void Awake()
    {
        image = GetComponent<Image>();
        ApplyMosaicStateIfConfigured();
    }

    private void OnEnable()
    {
        ApplyMosaicStateIfConfigured();
    }

    private void OnDestroy()
    {
        ReleaseRuntimeMosaicMaterial();
    }

    public void SetMosaicEnabled(bool enabled)
    {
        if (enableMosaic == enabled)
        {
            return;
        }

        enableMosaic = enabled;
        ApplyMosaicStateIfConfigured();
    }

    public void SetMosaicPixelSize(int pixelSize)
    {
        mosaicPixelSize = Mathf.Max(1, pixelSize);
        ApplyMosaicStateIfConfigured();
    }

    public void SetMosaicShaderName(string shaderName)
    {
        if (string.Equals(mosaicShaderName, shaderName, System.StringComparison.Ordinal))
        {
            return;
        }

        mosaicShaderName = string.IsNullOrEmpty(shaderName) ? "UI/Game02/ItemStreamMosaic" : shaderName;
        hasLoggedMosaicShaderMissing = false;
        ReleaseRuntimeMosaicMaterial();
        ApplyMosaicStateIfConfigured();
    }

    public void ApplyMosaicStateIfConfigured()
    {
        image = image != null ? image : GetComponent<Image>();
        if (image == null)
        {
            return;
        }

        if (!enableMosaic)
        {
            if (image.material == runtimeMosaicMaterial)
            {
                image.material = null;
            }

            ReleaseRuntimeMosaicMaterial();
            return;
        }

        Shader shader = mosaicShaderAsset != null
            ? mosaicShaderAsset
            : Shader.Find(mosaicShaderName);
        if (shader == null)
        {
            if (!hasLoggedMosaicShaderMissing)
            {
                Debug.LogWarning($"[UIMosaicEffect] Mosaic shader not found: {mosaicShaderName}");
                hasLoggedMosaicShaderMissing = true;
            }

            return;
        }

        hasLoggedMosaicShaderMissing = false;
        if (runtimeMosaicMaterial == null)
        {
            runtimeMosaicMaterial = new Material(shader);
            runtimeMosaicMaterial.name = $"{name}_MosaicRuntimeMat";
        }

        runtimeMosaicMaterial.SetFloat("_EnableMosaic", 1f);
        runtimeMosaicMaterial.SetFloat("_PixelSize", Mathf.Max(1, mosaicPixelSize));
        image.material = runtimeMosaicMaterial;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (mosaicShaderAsset == null)
        {
            mosaicShaderAsset = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/game02/ItemStreamMosaicUI.shader");
        }
    }
#endif

    private void ReleaseRuntimeMosaicMaterial()
    {
        if (runtimeMosaicMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeMosaicMaterial);
        }
        else
        {
            DestroyImmediate(runtimeMosaicMaterial);
        }

        runtimeMosaicMaterial = null;
    }
}
