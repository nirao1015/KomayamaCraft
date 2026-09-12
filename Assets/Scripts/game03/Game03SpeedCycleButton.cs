using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <c>DebugPanel/ButtonSpeed</c> と同一 GameObject に付与。クリックで <see cref="Game03Manager.GameTimeScalePreset"/> を順に切り替える。
/// <c>ButtonSpeed/DebugText (TMP)</c> に現在倍率を表示する。本番（<see cref="Game03DebugManager.ProductionReleaseBuild"/>）では操作無効。
/// </summary>
[DefaultExecutionOrder(60)]
[AddComponentMenu("Game03/Speed Cycle Button (Debug)")]
public sealed class Game03SpeedCycleButton : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03DebugManager game03DebugManager;
    [SerializeField, Tooltip("ButtonSpeed/DebugText (TMP)。未設定時は子から TextMeshProUGUI を検索する。")]
    private TMP_Text debugText;

    [Header("任意")]
    [SerializeField, Tooltip("未設定時はこのオブジェクトの Button")]
    private Button button;

    private void Reset()
    {
        CacheRefs();
    }

    private void Awake()
    {
        CacheRefs();
#if UNITY_EDITOR
        if (button == null)
        {
            Debug.LogError("[Game03SpeedCycleButton] Button が必要です。ButtonSpeed と同一 GameObject に UnityEngine.UI.Button があるか確認してください。", this);
        }
#endif
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

        if (debugText == null)
        {
            debugText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void OnEnable()
    {
        if (debugText != null)
        {
            debugText.raycastTarget = false;
        }

        RefreshLabelAndInteractable();
    }

    private void Start()
    {
        RefreshLabelAndInteractable();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClickCycleSpeed);
        }
    }

    private void Update()
    {
        if (button == null)
        {
            return;
        }

        bool allow = game03DebugManager == null || !game03DebugManager.ProductionReleaseBuild;
        bool wantInteractable = allow && game03Manager != null;
        if (button.interactable != wantInteractable)
        {
            button.interactable = wantInteractable;
        }
    }

    private void LateUpdate()
    {
        SyncDebugTextLabel();
    }

    private void SyncDebugTextLabel()
    {
        if (debugText == null || game03Manager == null)
        {
            return;
        }

        string s = Game03Manager.FormatTimeScalePresetLabel(game03Manager.CurrentGameTimeScalePreset);
        if (debugText.text != s)
        {
            debugText.text = s;
        }
    }

    private void OnClickCycleSpeed()
    {
        if (game03Manager == null)
        {
            return;
        }

        if (game03DebugManager != null && game03DebugManager.ProductionReleaseBuild)
        {
            return;
        }

        game03Manager.CycleGameTimeScalePreset();
        RefreshLabelAndInteractable();
    }

    private void RefreshLabelAndInteractable()
    {
        if (debugText != null)
        {
            if (game03Manager != null)
            {
                debugText.text = Game03Manager.FormatTimeScalePresetLabel(game03Manager.CurrentGameTimeScalePreset);
            }
            else
            {
                debugText.text = "—";
            }
        }

        if (button != null)
        {
            bool allow = game03DebugManager == null || !game03DebugManager.ProductionReleaseBuild;
            button.interactable = allow && game03Manager != null;
        }
    }
}
