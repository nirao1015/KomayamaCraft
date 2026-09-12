using Game02;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <c>PanelCanvas/ButtonStop</c> と同一の GameObject に付ける。
/// <c>Game02.GameManager</c> の一時停止をトグルし、<c>PausePanel</c> の表示だけ同期する（名前検索しない）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class Game02PauseToggleButton : MonoBehaviour
{
    [Tooltip("一時停止中のみ有効。シーンでは非アクティブにしておく。")]
    [SerializeField] private GameObject pausePanel;

    [SerializeField] private Button button;

    [Tooltip("オプション。一時停止へ入る押下のとき、CatButton のスケールをシーン既定（初回 Awake 時）へ戻す。")]
    [SerializeField] private CatButtonController catButtonToResetOnPauseEnter;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.RemoveListener(OnClickTogglePause);
            button.onClick.AddListener(OnClickTogglePause);
        }
    }

    private void OnValidate()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        RefreshPausePanel();
    }

    private void Update()
    {
        RefreshPausePanel();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClickTogglePause);
        }
    }

    private void OnClickTogglePause()
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

        bool willPause = !Game02.GameManager.Instance.IsPaused;
        Game02.GameManager.Instance.SetPaused(willPause);
        if (willPause && catButtonToResetOnPauseEnter != null)
        {
            catButtonToResetOnPauseEnter.ResetScaleToBaseline();
        }

        Game02SeManager.TryGet()?.PlayByCue(
            willPause ? Game02SeCue.PauseOn : Game02SeCue.PauseOff);
        RefreshPausePanel();
    }

    private void RefreshPausePanel()
    {
        if (pausePanel == null)
        {
            return;
        }

        Game02.GameManager gm = Game02.GameManager.Instance;
        bool wantActive = gm != null && gm.ShouldShowPausePanelWhilePaused;
        if (pausePanel.activeSelf != wantActive)
        {
            pausePanel.SetActive(wantActive);
        }
    }
}
