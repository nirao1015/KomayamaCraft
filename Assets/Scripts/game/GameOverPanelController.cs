using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// game_stage_scene のゲームオーバー UI（オーバーレイ、演出、BGM、遷移）を扱います。
/// </summary>
public class GameOverPanelController : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private Sprite overlaySprite;
    [SerializeField] private float overlayAlpha = 0.5f;
    [SerializeField] private Image grayOverlayImage;

    [Header("Text")]
    [SerializeField] private TMP_FontAsset uiFont;
    [SerializeField] private TextMeshProUGUI gameOverText;

    [Header("参照: 管理オブジェクト")]
    [SerializeField] private Game01BgmManager game01BgmManager;
    [SerializeField] private Game01TransitionManager game01TransitionManager;

    [Header("Animation")]
    [SerializeField] private float gameOverMoveDuration = 0.8f;
    [SerializeField] private float gameOverBounceAmplitude = 14f;
    [SerializeField] private float gameOverBouncePeriod = 0.45f;

    private RectTransform gameOverTextRect;
    private Tween bounceTween;
    private Vector2 gameOverTextCenterAnchored;

    private void Awake()
    {
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

        if (gameOverText != null)
        {
            if (uiFont != null)
            {
                gameOverText.font = uiFont;
            }

            gameOverTextRect = gameOverText.rectTransform;
            gameOverTextCenterAnchored = gameOverTextRect.anchoredPosition;
        }

    }

    private void OnDestroy()
    {
        bounceTween?.Kill();
    }

    public void OnPlayerGameOver()
    {
        if (game01TransitionManager != null)
        {
            game01TransitionManager.ShowGameOverPanel();
        }
        else
        {
            gameObject.SetActive(true);
        }

        game01BgmManager?.StopStageBgm();
        game01BgmManager?.PlayGameOverBgm();

        bounceTween?.Kill();
        RunGameOverIntroAnimation();

    }

    private void RunGameOverIntroAnimation()
    {
        if (gameOverTextRect == null)
        {
            return;
        }

        Canvas root = gameOverTextRect.GetComponentInParent<Canvas>();
        float canvasWidth = 800f;
        if (root != null)
        {
            canvasWidth = Mathf.Max(400f, root.pixelRect.width);
        }

        float offLeftX = gameOverTextCenterAnchored.x - canvasWidth * 0.55f - 200f;

        gameOverTextRect.anchoredPosition = new Vector2(offLeftX, gameOverTextCenterAnchored.y);

        float moveDur = Mathf.Max(0.05f, gameOverMoveDuration);
        gameOverTextRect
            .DOAnchorPos(gameOverTextCenterAnchored, moveDur)
            .SetEase(Ease.OutCubic)
            .OnComplete(StartBounceLoop);
    }

    private void StartBounceLoop()
    {
        if (gameOverTextRect == null)
        {
            return;
        }

        float half = Mathf.Max(0.05f, gameOverBouncePeriod * 0.5f);
        bounceTween = gameOverTextRect
            .DOAnchorPosY(gameOverTextCenterAnchored.y + gameOverBounceAmplitude, half)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

}
