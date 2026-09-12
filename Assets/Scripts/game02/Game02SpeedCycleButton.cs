using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <c>PanelCanvas/ButtonSpeed</c> と同一の GameObject に付ける。
/// 各速度の見た目は <b>このコンポーネント</b> のスプライト欄だけで設定する（子の Text は Raycast Target をオフ推奨）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class Game02SpeedCycleButton : MonoBehaviour
{
    [Header("速度アイコン（ButtonSpeed 自身・このスクリプトから設定）")]
    [Tooltip("等速 (x1)")]
    [SerializeField]
    private Sprite spriteSpeed1x;

    [Tooltip("3倍速 (x3)")]
    [SerializeField]
    private Sprite spriteSpeed3x;

    [Tooltip("5倍速 (x5)")]
    [SerializeField]
    private Sprite spriteSpeed5x;

    [Tooltip("10倍速 (x10)")]
    [SerializeField]
    private Sprite spriteSpeed10x;

    [Header("初期速度段階")]
    [Tooltip("0 = x1, 1 = x3, 2 = x5, 3 = x10")]
    [SerializeField]
    [Range(0, 3)]
    private int initialSpeedStepIndex;

    [Header("参照（空なら自動）")]
    [Tooltip("未指定時は Button の Target Graphic の Image（通常はこのオブジェクトの Image）")]
    [SerializeField]
    private Image targetImage;

    [Tooltip("未指定時はこのオブジェクトの Button")]
    [SerializeField]
    private Button button;

    private bool hasAppliedInitialSpeed;

    private void Reset()
    {
        CacheRefs();
        if (spriteSpeed1x == null && targetImage != null)
        {
            spriteSpeed1x = targetImage.sprite;
        }
    }

    private void Awake()
    {
        CacheRefs();
        if (button != null)
        {
            button.onClick.RemoveListener(OnClickCycleSpeed);
            button.onClick.AddListener(OnClickCycleSpeed);
        }
    }

    private void OnValidate()
    {
        CacheRefs();
    }

    private void CacheRefs()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (targetImage == null)
        {
            if (button != null && button.targetGraphic is Image tg)
            {
                targetImage = tg;
            }
            else
            {
                targetImage = GetComponent<Image>();
            }
        }
    }

    private void OnEnable()
    {
        RefreshVisual();
    }

    private void Start()
    {
        if (!hasAppliedInitialSpeed && Game02.GameManager.Instance != null)
        {
            Game02.GameManager.Instance.SetGameSpeedStepIndex(initialSpeedStepIndex);
            hasAppliedInitialSpeed = true;
        }

        RefreshVisual();
    }

    private void Update()
    {
        if (button == null || Game02.GameManager.Instance == null)
        {
            return;
        }

        bool interactable = !Game02.GameManager.Instance.HasFatalError &&
                            !Game02.GameManager.Instance.IsPreGameSequenceActive &&
                            !Game02.GameManager.Instance.IsGameCleared;
        if (button.interactable != interactable)
        {
            button.interactable = interactable;
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClickCycleSpeed);
        }
    }

    private void OnClickCycleSpeed()
    {
        if (Game02.GameManager.Instance == null || Game02.GameManager.Instance.HasFatalError)
        {
            return;
        }

        if (Game02.GameManager.Instance.IsPreGameSequenceActive ||
            Game02.GameManager.Instance.IsGameCleared)
        {
            return;
        }

        Game02.GameManager.Instance.CycleGameSpeed();
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (targetImage == null || Game02.GameManager.Instance == null)
        {
            return;
        }

        int step = Game02.GameManager.Instance.GameSpeedStepIndex;
        Sprite next = step switch
        {
            0 => spriteSpeed1x,
            1 => spriteSpeed3x,
            2 => spriteSpeed5x,
            _ => spriteSpeed10x,
        };

        if (next != null)
        {
            targetImage.sprite = next;
        }
    }
}
