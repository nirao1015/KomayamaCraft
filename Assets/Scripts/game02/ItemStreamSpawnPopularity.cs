using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ItemStreamSpawnPopularity : MonoBehaviour
{
    [SerializeField] private TMP_Text popularText;
    [SerializeField] private TMP_Text streamGenreText;
    [Header("Random Sprite (optional)")]
    [SerializeField] private List<Sprite> randomDisplaySprites = new List<Sprite>();
    [Header("Mosaic Effect (ItemStream only)")]
    [SerializeField] private bool enableMosaic = true;
    [SerializeField] private int mosaicPixelSize = 8;
    [SerializeField] private Shader mosaicShaderAsset;
    [SerializeField] private string mosaicShaderName = "UI/Game02/ItemStreamMosaic";

    private long spawnPopularity;
    private string streamGenre = "未設定";
    private bool hasAppliedRandomDisplaySprite;
    private Image cachedItemImage;
    private Material cachedOriginalMaterial;
    private Material runtimeMosaicMaterial;
    private bool hasLoggedMosaicShaderMissing;

    private void Awake()
    {
        ApplyRandomDisplaySpriteIfConfigured();
        ApplyMosaicStateIfConfigured();
    }

    private void OnDestroy()
    {
        ReleaseRuntimeMosaicMaterial();
    }

    public void ApplySpawnPopularity(long popularityAtSpawn)
    {
        spawnPopularity = popularityAtSpawn;
        TMP_Text text = ResolvePopularText();
        if (text != null)
        {
            text.text = popularityAtSpawn.ToString("N0");
        }
    }

    public long GetSpawnPopularity()
    {
        return spawnPopularity;
    }

    public void ApplyStreamGenre(string genre)
    {
        streamGenre = string.IsNullOrEmpty(genre) ? "未設定" : genre;
        TMP_Text text = ResolveStreamGenreText();
        if (text != null)
        {
            text.text = streamGenre;
        }
    }

    public string GetStreamGenre()
    {
        return streamGenre;
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

    private void ApplyRandomDisplaySpriteIfConfigured()
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

    private void ApplyMosaicStateIfConfigured()
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
                Debug.LogWarning($"[ItemStreamSpawnPopularity] Mosaic shader not found: {mosaicShaderName}");
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

        DraggableItemController draggable = GetComponent<DraggableItemController>();
        if (draggable != null && draggable.TryGetDisplaySprite(out _))
        {
            cachedItemImage = GetComponent<Image>();
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

    private TMP_Text ResolvePopularText()
    {
        if (popularText != null)
        {
            return popularText;
        }

        Transform t = transform.Find("PopularText");
        if (t != null)
        {
            return t.GetComponent<TMP_Text>();
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate != null &&
                candidate.gameObject.name.StartsWith("PopularText", StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private TMP_Text ResolveStreamGenreText()
    {
        if (streamGenreText != null)
        {
            return streamGenreText;
        }

        Transform genreRoot = transform.Find("StGenre");
        if (genreRoot != null)
        {
            Transform genreTextTransform = genreRoot.Find("GenreText");
            if (genreTextTransform != null)
            {
                TMP_Text byPath = genreTextTransform.GetComponent<TMP_Text>();
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
            if (candidate != null &&
                candidate.gameObject.name.StartsWith("GenreText", StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }
}
