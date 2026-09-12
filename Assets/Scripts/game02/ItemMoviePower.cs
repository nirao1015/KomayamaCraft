using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ItemMoviePower : MonoBehaviour
{
    [SerializeField] private TMP_Text moviePowerText;
    [SerializeField] private TMP_Text movieNameText;
    [Header("Random Sprite (optional)")]
    [SerializeField] private List<Sprite> randomDisplaySprites = new List<Sprite>();
    [Header("Mosaic Effect (ItemMovie only)")]
    [SerializeField] private bool enableMosaic = true;
    [SerializeField] private int mosaicPixelSize = 8;
    [SerializeField] private Shader mosaicShaderAsset;
    [SerializeField] private string mosaicShaderName = "UI/Game02/ItemStreamMosaic";
    [Header("Trend Upgrade Contribution (debug/read model)")]
    [SerializeField] private int upgradeBuzzValue;
    [SerializeField] private int upgradeBuzzFactor;
    [SerializeField] private long buzzGainValue;

    private long moviePower;
    private string movieName = MovieNameCatalog.DefaultMovieName;
    private bool hasAppliedRandomDisplaySprite;
    private Image cachedItemImage;
    private Material cachedOriginalMaterial;
    private Material runtimeMosaicMaterial;
    private bool hasLoggedMosaicShaderMissing;

    public long MoviePower => moviePower;
    public long FinalPopularity => moviePower;
    public long BuzzGainValue => buzzGainValue;
    public string MovieName => movieName;

    private void Awake()
    {
        ApplyRandomDisplaySpriteIfConfigured();
        ApplyMosaicStateIfConfigured();
    }

    private void OnDestroy()
    {
        ReleaseRuntimeMosaicMaterial();
    }

    public void ApplyMoviePower(long value)
    {
        moviePower = value;
        ApplyMoviePowerText(value);
    }

    public void InitializeMovieStats(long finalPopularity, long buzzGain)
    {
        InitializeMovieStats(finalPopularity, buzzGain, MovieNameCatalog.DefaultMovieName);
    }

    public void InitializeMovieStats(long finalPopularity, long buzzGain, string resolvedMovieName)
    {
        moviePower = finalPopularity;
        buzzGainValue = System.Math.Max(0L, buzzGain);
        movieName = SanitizeMovieName(resolvedMovieName);
        ApplyMoviePowerText(moviePower);
        ApplyMovieNameText(movieName);
    }

    public long GetFinalPopularity()
    {
        return moviePower;
    }

    public long GetBuzzGainValue()
    {
        return buzzGainValue;
    }

    public string GetMovieName()
    {
        return movieName;
    }

    public int UpgradeBuzzValue => upgradeBuzzValue;
    public int UpgradeBuzzFactor => upgradeBuzzFactor;

    public void ApplyTrendUpgradeContribution(int buzzValue, int buzzFactor)
    {
        upgradeBuzzValue = Mathf.Max(0, buzzValue);
        upgradeBuzzFactor = Mathf.Max(0, buzzFactor);
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

    public void ApplyRandomDisplaySpriteIfConfigured()
    {
        if (hasAppliedRandomDisplaySprite)
        {
            return;
        }

        hasAppliedRandomDisplaySprite = true;
        if (randomDisplaySprites == null || randomDisplaySprites.Count <= 0)
        {
            return;
        }

        int index = UnityEngine.Random.Range(0, randomDisplaySprites.Count);
        Sprite selected = randomDisplaySprites[index];
        if (selected == null)
        {
            return;
        }

        DraggableItemController draggable = GetComponent<DraggableItemController>();
        if (draggable != null)
        {
            draggable.ApplyDisplaySprite(selected);
            return;
        }

        Image image = GetComponent<Image>();
        if (image != null)
        {
            image.sprite = selected;
        }
    }

    public void ApplyMosaicStateIfConfigured()
    {
        Image image = ResolveItemImage();
        if (image == null)
        {
            return;
        }

        if (!enableMosaic)
        {
            if (image.material == runtimeMosaicMaterial)
            {
                image.material = cachedOriginalMaterial;
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
                Debug.LogWarning($"[ItemMoviePower] Mosaic shader not found: {mosaicShaderName}");
                hasLoggedMosaicShaderMissing = true;
            }
            return;
        }

        if (runtimeMosaicMaterial == null)
        {
            cachedOriginalMaterial = image.material;
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

    private Image ResolveItemImage()
    {
        if (cachedItemImage != null)
        {
            return cachedItemImage;
        }

        cachedItemImage = GetComponent<Image>();
        return cachedItemImage;
    }

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

    private void ApplyMoviePowerText(long value)
    {
        TMP_Text text = ResolveMoviePowerText();
        if (text != null)
        {
            text.text = value.ToString("N0");
        }
    }

    private void ApplyMovieNameText(string value)
    {
        TMP_Text text = ResolveMovieNameText();
        if (text != null)
        {
            text.text = value;
        }
    }

    private TMP_Text ResolveMoviePowerText()
    {
        if (moviePowerText != null)
        {
            return moviePowerText;
        }

        Transform t = transform.Find("MoviePowerText");
        if (t != null)
        {
            return t.GetComponent<TMP_Text>();
        }

        t = transform.Find("Text (TMP)");
        if (t != null)
        {
            return t.GetComponent<TMP_Text>();
        }

        return null;
    }

    private TMP_Text ResolveMovieNameText()
    {
        if (movieNameText != null)
        {
            return movieNameText;
        }

        Transform genreRoot = transform.Find("MovieGenre");
        if (genreRoot != null)
        {
            Transform movieTextTransform = genreRoot.Find("MovieText");
            if (movieTextTransform != null)
            {
                TMP_Text byPath = movieTextTransform.GetComponent<TMP_Text>();
                if (byPath != null)
                {
                    return byPath;
                }
            }
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate != null && candidate.gameObject.name.StartsWith("MovieText", System.StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string SanitizeMovieName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return MovieNameCatalog.DefaultMovieName;
        }

        string sanitized = value.Replace("\r", string.Empty).Replace("\n", string.Empty);
        return string.IsNullOrEmpty(sanitized) ? MovieNameCatalog.DefaultMovieName : sanitized;
    }
}
