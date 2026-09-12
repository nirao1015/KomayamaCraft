using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Attach to a UI Button to load a scene with FadeManager.
/// This component is self-contained and works in any scene.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class ButtonFadeSceneLoader : MonoBehaviour
{
    private const string Game02SceneName = "game02_scene";

    [SerializeField] private string targetSceneName = "title_scene";
    [SerializeField] private float fadeOutSeconds = 1f;
    [SerializeField] private float fadeInSeconds = 0.2f;
    [SerializeField] private float clickDelaySeconds = 0f;
    [SerializeField] private bool disableButtonDuringTransition = true;
    [Header("Game02 Start Option")]
    [SerializeField] private bool setGame02StartModeOnClick;
    [SerializeField] private Game02.Game02StartMode game02StartMode = Game02.Game02StartMode.LoadSaveIfAvailable;

    private Button cachedButton;
    private bool isTransitioning;

    private void Awake()
    {
        cachedButton = GetComponent<Button>();
        if (cachedButton != null)
        {
            cachedButton.onClick.RemoveListener(OnClickLoadScene);
            cachedButton.onClick.AddListener(OnClickLoadScene);
        }
    }

    private void OnDestroy()
    {
        if (cachedButton != null)
        {
            cachedButton.onClick.RemoveListener(OnClickLoadScene);
        }
    }

    private void OnClickLoadScene()
    {
        if (isTransitioning)
        {
            return;
        }

        string sceneName = string.IsNullOrWhiteSpace(targetSceneName) ? string.Empty : targetSceneName.Trim();
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[ButtonFadeSceneLoader] targetSceneName is empty.");
            return;
        }

        if (setGame02StartModeOnClick && string.Equals(sceneName, Game02SceneName, System.StringComparison.Ordinal))
        {
            Game02.Game02StartContext.SetNextStartMode(game02StartMode);
        }

        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    private System.Collections.IEnumerator LoadSceneCoroutine(string sceneName)
    {
        isTransitioning = true;

        if (disableButtonDuringTransition && cachedButton != null)
        {
            cachedButton.interactable = false;
        }

        float wait = Mathf.Max(0f, clickDelaySeconds);
        if (wait > 0f)
        {
            yield return new WaitForSeconds(wait);
        }

        FadeManager fadeManager = EnsureFadeManager();
        if (fadeManager != null)
        {
            fadeManager.LoadScene(sceneName, Mathf.Max(0.01f, fadeOutSeconds), Mathf.Max(0.01f, fadeInSeconds));
            yield break;
        }

        SceneManager.LoadScene(sceneName);
    }

    private static FadeManager EnsureFadeManager()
    {
        FadeManager fadeManager = FindAnyObjectByType<FadeManager>();
        if (fadeManager == null)
        {
            GameObject fadeManagerObject = new GameObject("FadeManager");
            fadeManager = fadeManagerObject.AddComponent<FadeManager>();
            fadeManager.DebugMode = false;
        }
        else
        {
            fadeManager.DebugMode = false;
        }

        return fadeManager;
    }
}
