using System.Collections;
using KomayamaCraft;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// タイトルの LoadCanvas 用。ゲームシーンへ非同期ロードし、完了まで表示を維持する。
/// </summary>
[DisallowMultipleComponent]
public sealed class KomayamaCraftSceneLoader : MonoBehaviour
{
    public static KomayamaCraftSceneLoader Instance { get; private set; }

    [SerializeField] private GameObject loadCanvasRoot;
    [SerializeField] private Canvas loadCanvas;
    [SerializeField, Min(0)] private int loadingSortOrder = 9999;
    [SerializeField, Min(0)] private int framesToWaitAfterSceneLoaded = 2;

    private bool loading;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (loadCanvasRoot == null)
        {
            loadCanvasRoot = gameObject;
        }

        if (loadCanvas == null)
        {
            loadCanvas = loadCanvasRoot.GetComponent<Canvas>();
        }
    }

    private void Start()
    {
        // ロード中でなければ初期非表示。Awake で消すと BeginLoad の有効化と競合する。
        if (!loading && loadCanvasRoot != null)
        {
            loadCanvasRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void LoadCraftScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[KomayamaCraftSceneLoader] sceneName is empty");
            return;
        }

        KomayamaCraftSceneLoader loader = Instance;
        if (loader == null)
        {
            loader = FindFirstObjectByType<KomayamaCraftSceneLoader>(FindObjectsInactive.Include);
        }

        if (loader == null)
        {
            Debug.LogWarning("[KomayamaCraftSceneLoader] missing — fallback to sync LoadScene");
            SceneManager.LoadScene(sceneName.Trim());
            return;
        }

        loader.BeginLoad(sceneName.Trim());
    }

    public void BeginLoad(string sceneName)
    {
        if (loading)
        {
            return;
        }

        loading = true;
        if (loadCanvasRoot == null)
        {
            loadCanvasRoot = gameObject;
        }

        // 非アクティブでは StartCoroutine できないので、先に有効化する。
        if (!loadCanvasRoot.activeSelf)
        {
            loadCanvasRoot.SetActive(true);
        }

        StartCoroutine(CoLoad(sceneName));
    }

    private IEnumerator CoLoad(string sceneName)
    {
        KomayamaCraftLoadGate.BeginHold();

        if (loadCanvas == null)
        {
            loadCanvas = loadCanvasRoot.GetComponent<Canvas>();
        }

        // シーンを跨ぐので Overlay に切り替え（Camera 依存を切る）
        if (loadCanvas != null)
        {
            loadCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            loadCanvas.overrideSorting = true;
            loadCanvas.sortingOrder = loadingSortOrder;
        }

        DontDestroyOnLoad(loadCanvasRoot);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[KomayamaCraftSceneLoader] LoadSceneAsync failed: {sceneName}");
            FinishLoading(destroyRoot: true);
            yield break;
        }

        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
        {
            yield return null;
        }

        op.allowSceneActivation = true;
        while (!op.isDone)
        {
            yield return null;
        }

        // Awake/Start（セーブ適用など）が終わるのを待つ
        int wait = Mathf.Max(0, framesToWaitAfterSceneLoaded);
        for (int i = 0; i < wait; i++)
        {
            yield return null;
        }

        FinishLoading(destroyRoot: true);
    }

    private void FinishLoading(bool destroyRoot)
    {
        loading = false;
        KomayamaCraftLoadGate.ReleaseHold();

        if (loadCanvasRoot != null)
        {
            loadCanvasRoot.SetActive(false);
        }

        if (destroyRoot && loadCanvasRoot != null)
        {
            if (Instance == this)
            {
                Instance = null;
            }

            Destroy(loadCanvasRoot);
        }
    }
}
