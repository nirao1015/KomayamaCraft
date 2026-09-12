using DG.Tweening;
using TMPro;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ステージ開始演出: グレー透過、ステージ名テキスト、プレイヤーの拡大→左中央へ移動＋縮小。
/// </summary>
public class StageStartSequenceController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Game01Manager gameManager;
    [SerializeField] private Game01TransitionManager game01TransitionManager;
    [SerializeField] private Game01PlayerController playerController;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private GameObject stageStartOverlayRoot;
    [SerializeField] private Image stageGrayOverlay;
    [SerializeField] private TextMeshProUGUI stageStartText;

    [Header("タイミング")]
    [SerializeField] private float introDurationSeconds = 3f;
    [SerializeField, Range(0f, 1f)] private float overlayStartAlpha = 0.65f;
    [SerializeField] private float playerStartScaleMultiplier = 2.4f;

    [Header("開始位置（中央円形レンジ）")]
    [SerializeField] private bool useCenterRingStart = true;
    [SerializeField] private Vector2 centerViewport = new Vector2(0.5f, 0.5f);
    [SerializeField] private float ringRadiusMinViewport = 0.18f;
    [SerializeField] private float ringRadiusMaxViewport = 0.32f;

    [Header("開始位置（旧設定）")]
    [SerializeField] private bool useViewportStart = true;
    [SerializeField] private Vector2 startViewport = new Vector2(0.12f, 0.88f);
    [SerializeField] private Vector3 startWorldOffsetFromGoal = new Vector3(0f, 7.5f, 0f);

    private void Awake()
    {
        if (playerController != null)
        {
            if (playerRigidbody == null)
            {
                playerRigidbody = playerController.GetComponent<Rigidbody2D>();
            }

            if (playerTransform == null)
            {
                playerTransform = playerController.transform;
            }
        }

        ResolveOverlayRootReference();
    }

    private void Start()
    {
        if (gameManager == null || !gameManager.WaitForStageIntro)
        {
            enabled = false;
            return;
        }

        // ルートが無効でも開始演出のために必ず有効化する。
        if (stageStartOverlayRoot != null && !stageStartOverlayRoot.activeSelf)
        {
            if (game01TransitionManager != null)
            {
                game01TransitionManager.ShowStageStartOverlay();
            }
            else
            {
                stageStartOverlayRoot.SetActive(true);
            }
        }

        if (playerRigidbody == null || playerTransform == null || stageGrayOverlay == null || stageStartText == null)
        {
            gameManager.NotifyStageIntroComplete();
            enabled = false;
            return;
        }

        RunIntro();
    }

    private void RunIntro()
    {
        Vector2 goalPos = playerRigidbody.position;
        Vector3 goalScale = playerTransform.localScale;

        Vector2 startPos = ComputeStartWorldPosition(goalPos);
        float startScale = Mathf.Max(0.01f, playerStartScaleMultiplier * goalScale.x);

        stageStartText.text = $"{FormatStageLabel(gameManager.CurrentStageName)}\nスタート";
        stageStartText.gameObject.SetActive(true);

        if (stageStartOverlayRoot != null)
        {
            RectTransform overlayRect = stageStartOverlayRoot.transform as RectTransform;
            if (overlayRect != null)
            {
                overlayRect.SetAsLastSibling();
            }
        }

        stageGrayOverlay.transform.SetAsFirstSibling();
        stageStartText.transform.SetAsLastSibling();
        stageGrayOverlay.color = new Color(1f, 1f, 1f, Mathf.Clamp01(overlayStartAlpha));
        stageGrayOverlay.gameObject.SetActive(true);

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        playerRigidbody.linearVelocity = Vector2.zero;
        playerRigidbody.position = startPos;
        playerTransform.position = new Vector3(startPos.x, startPos.y, playerTransform.position.z);
        playerTransform.localScale = Vector3.one * startScale;

        Sequence sequence = DOTween.Sequence();
        // プレイヤー到着タイミングと同時にグレーが完全に消える。
        sequence.Join(stageGrayOverlay.DOFade(0f, introDurationSeconds).SetEase(Ease.OutQuad));
        sequence.Join(
            playerRigidbody
                .DOMove(goalPos, introDurationSeconds)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(UpdateType.Fixed));
        sequence.Join(playerTransform.DOScale(goalScale, introDurationSeconds).SetEase(Ease.InOutQuad));
        sequence.OnComplete(OnIntroFinished);
    }

    private void ResolveOverlayRootReference()
    {
        if (stageStartOverlayRoot != null)
        {
            return;
        }

        if (stageGrayOverlay != null && stageGrayOverlay.transform.parent != null)
        {
            stageStartOverlayRoot = stageGrayOverlay.transform.parent.gameObject;
            return;
        }

        if (stageStartText != null && stageStartText.transform.parent != null)
        {
            stageStartOverlayRoot = stageStartText.transform.parent.gameObject;
        }
    }

    private void OnIntroFinished()
    {
        stageStartText.gameObject.SetActive(false);
        Color c = stageGrayOverlay.color;
        stageGrayOverlay.color = new Color(c.r, c.g, c.b, 0f);
        stageGrayOverlay.gameObject.SetActive(false);

        game01TransitionManager?.HideStageStartOverlay();

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
        }

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        if (gameManager != null)
        {
            gameManager.NotifyStageIntroComplete();
        }
    }

    private static string FormatStageLabel(string rawStageName)
    {
        if (string.IsNullOrWhiteSpace(rawStageName))
        {
            return "STAGE";
        }

        string upper = rawStageName.Trim().ToUpperInvariant();
        var builder = new StringBuilder(upper.Length);
        for (int i = 0; i < upper.Length; i++)
        {
            char c = upper[i];
            builder.Append(c == '_' ? ' ' : c);
        }

        return builder.ToString();
    }

    private Vector2 ComputeStartWorldPosition(Vector2 goalWorld)
    {
        Camera cam = Camera.main;
        if (useCenterRingStart && cam != null)
        {
            float z = Mathf.Abs(cam.transform.position.z - playerTransform.position.z);
            float minR = Mathf.Max(0f, ringRadiusMinViewport);
            float maxR = Mathf.Max(minR, ringRadiusMaxViewport);
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.right;
            }
            else
            {
                dir.Normalize();
            }

            float r = Random.Range(minR, maxR);
            Vector2 vp = centerViewport + (dir * r);
            vp.x = Mathf.Clamp(vp.x, 0.02f, 0.98f);
            vp.y = Mathf.Clamp(vp.y, 0.02f, 0.98f);

            Vector3 w = cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, z));
            return new Vector2(w.x, w.y);
        }

        if (!useViewportStart)
        {
            return goalWorld + (Vector2)startWorldOffsetFromGoal;
        }

        if (cam == null)
        {
            return goalWorld + (Vector2)startWorldOffsetFromGoal;
        }

        float depth = Mathf.Abs(cam.transform.position.z - playerTransform.position.z);
        Vector3 worldPos = cam.ViewportToWorldPoint(new Vector3(startViewport.x, startViewport.y, depth));
        return new Vector2(worldPos.x, worldPos.y);
    }
}
