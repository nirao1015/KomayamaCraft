using System.Collections.Generic;
using KomayamaCraft;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// kougu 画像の初期表示／クリック差し替え。漂い（TitleKouguDrift）には触れない。
/// 入力は静止した hitGraphic、見た目は targetImage（漂い対象）に分離する。
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleKouguSpritePicker : MonoBehaviour
{
    private const float DefaultWeight = 0.20f;
    private const float NormalWeight = 0.50f;
    // 残り 0.30 が easter

    private static readonly List<RaycastResult> RaycastBuffer = new List<RaycastResult>(16);

    [Header("参照")]
    [SerializeField, Tooltip("見た目用。TitleKouguDrift の移動対象。")]
    private Image targetImage;
    [SerializeField, Tooltip("クリック判定用。親側に置き漂わせない。未設定時は targetImage。")]
    private Graphic hitGraphic;
    [SerializeField, Tooltip("title_kougu-数字.png（デフォルト title_kougu.png は含めない）")]
    private Sprite[] normalVariants;
    [SerializeField, Tooltip("title_kougu-e-数字.png")]
    private Sprite[] easterVariants;

    [Header("クリック")]
    [SerializeField, Min(0f)] private float clickCooldownSeconds = 0.2f;

    private Sprite defaultSprite;
    private float nextClickAllowedUnscaledTime;

    private void Awake()
    {
        ResolveImages();
        CacheDefaultSprite();
        if (hitGraphic != null)
        {
            hitGraphic.raycastTarget = true;
        }

        if (targetImage != null && hitGraphic != null && hitGraphic.gameObject != targetImage.gameObject)
        {
            // 見た目側は入力を取らない（漂いで Click が外れるのを防ぐ）
            targetImage.raycastTarget = false;
        }

        ApplyBySaveRules();
    }

    private void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (!IsPointerOverHitGraphic())
        {
            return;
        }

        TryChangeByClick();
    }

    private void TryChangeByClick()
    {
        float now = Time.unscaledTime;
        if (now < nextClickAllowedUnscaledTime)
        {
            return;
        }

        nextClickAllowedUnscaledTime = now + Mathf.Max(0f, clickCooldownSeconds);
        ApplyBySaveRules();
    }

    private bool IsPointerOverHitGraphic()
    {
        if (hitGraphic == null || !hitGraphic.isActiveAndEnabled || !hitGraphic.raycastTarget)
        {
            return false;
        }

        if (EventSystem.current == null || Mouse.current == null)
        {
            return false;
        }

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        RaycastBuffer.Clear();
        EventSystem.current.RaycastAll(eventData, RaycastBuffer);
        for (int i = 0; i < RaycastBuffer.Count; i++)
        {
            if (RaycastBuffer[i].gameObject == hitGraphic.gameObject)
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveImages()
    {
        if (targetImage == null)
        {
            Transform imageTf = transform.Find("Image");
            if (imageTf != null)
            {
                targetImage = imageTf.GetComponent<Image>();
            }

            if (targetImage == null)
            {
                targetImage = GetComponentInChildren<Image>(true);
            }
        }

        if (hitGraphic == null)
        {
            Transform hitTf = transform.Find("HitArea");
            if (hitTf != null)
            {
                hitGraphic = hitTf.GetComponent<Graphic>();
            }
        }

        if (hitGraphic == null)
        {
            hitGraphic = targetImage;
        }
    }

    private void CacheDefaultSprite()
    {
        if (targetImage != null && targetImage.sprite != null)
        {
            defaultSprite = targetImage.sprite;
        }
    }

    private void ApplyBySaveRules()
    {
        // セーブが1つも無いときは必ずデフォルト（初期実行含む）
        if (!KomayamaSaveSlots.HasAnySave())
        {
            ApplySprite(defaultSprite);
            return;
        }

        ApplyWeightedRandom();
    }

    private void ApplyWeightedRandom()
    {
        if (targetImage == null)
        {
            return;
        }

        float roll = Random.value;
        if (roll < DefaultWeight)
        {
            ApplySprite(defaultSprite);
            return;
        }

        if (roll < DefaultWeight + NormalWeight)
        {
            ApplySprite(PickEven(normalVariants) ?? defaultSprite);
            return;
        }

        ApplySprite(PickEven(easterVariants) ?? defaultSprite);
    }

    private static Sprite PickEven(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
        {
            return null;
        }

        int validCount = 0;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        int pick = Random.Range(0, validCount);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
            {
                continue;
            }

            if (pick == 0)
            {
                return sprites[i];
            }

            pick--;
        }

        return null;
    }

    private void ApplySprite(Sprite sprite)
    {
        if (targetImage == null || sprite == null)
        {
            return;
        }

        targetImage.sprite = sprite;
    }
}
