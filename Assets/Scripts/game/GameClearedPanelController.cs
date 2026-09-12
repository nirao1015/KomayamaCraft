using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// game_stage_scene のステージクリア UI（オーバーレイ、テキスト演出）を扱う。
/// </summary>
public class GameClearedPanelController : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private Sprite overlaySprite;
    [SerializeField] private float overlayAlpha = 0.5f;
    [SerializeField] private Image grayOverlayImage;

    [Header("Text")]
    [SerializeField] private TMP_FontAsset uiFont;
    [SerializeField] private TextMeshProUGUI gameClearedText;

    [Header("Animation")]
    [SerializeField] private float moveDuration = 0.8f;
    [SerializeField] private float bounceAmplitude = 12f;
    [SerializeField] private float bouncePeriod = 0.5f;

    private RectTransform textRect;
    private Tween bounceTween;
    private Vector2 textCenter;

    private void Awake()
    {
        if (grayOverlayImage == null)
        {
            Transform t = transform.Find("GrayOverlay");
            if (t != null)
            {
                grayOverlayImage = t.GetComponent<Image>();
            }
        }

        if (gameClearedText == null)
        {
            Transform t = transform.Find("GameClearedText");
            if (t != null)
            {
                gameClearedText = t.GetComponent<TextMeshProUGUI>();
            }
        }

        if (grayOverlayImage != null)
        {
            if (overlaySprite != null)
            {
                grayOverlayImage.sprite = overlaySprite;
            }

            Color c = grayOverlayImage.color;
            c.a = overlayAlpha;
            grayOverlayImage.color = c;
        }

        if (gameClearedText != null)
        {
            if (uiFont != null)
            {
                gameClearedText.font = uiFont;
            }

            textRect = gameClearedText.rectTransform;
            textCenter = textRect.anchoredPosition;
        }
    }

    private void OnDestroy()
    {
        bounceTween?.Kill();
    }

    public void ShowStageCleared(string stageName)
    {
        gameObject.SetActive(true);
        if (gameClearedText != null)
        {
            gameClearedText.text = $"{stageName} クリア";
        }

        bounceTween?.Kill();
        RunIntroAnimation();
    }

    private void RunIntroAnimation()
    {
        if (textRect == null)
        {
            return;
        }

        Canvas root = textRect.GetComponentInParent<Canvas>();
        float canvasWidth = root != null ? Mathf.Max(400f, root.pixelRect.width) : 800f;
        float offLeftX = textCenter.x - canvasWidth * 0.55f - 200f;
        textRect.anchoredPosition = new Vector2(offLeftX, textCenter.y);

        textRect
            .DOAnchorPos(textCenter, Mathf.Max(0.05f, moveDuration))
            .SetEase(Ease.OutCubic)
            .OnComplete(StartBounceLoop);
    }

    private void StartBounceLoop()
    {
        if (textRect == null)
        {
            return;
        }

        float half = Mathf.Max(0.05f, bouncePeriod * 0.5f);
        bounceTween = textRect
            .DOAnchorPosY(textCenter.y + bounceAmplitude, half)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }
}
