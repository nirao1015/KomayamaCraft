using System.Collections;
using KomayamaCraft;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// タイトルの LoadCanvas 用。ゲームシーンへ非同期ロードし、
/// Boot Ready 完了後に暗転入場してからゲーム時間を再開する。
/// </summary>
[DisallowMultipleComponent]
public sealed class KomayamaCraftSceneLoader : MonoBehaviour
{
    public static KomayamaCraftSceneLoader Instance { get; private set; }

    [SerializeField] private GameObject loadCanvasRoot;
    [SerializeField] private Canvas loadCanvas;
    [SerializeField, Tooltip("ロード見た目。暗転後に非表示／破棄。未設定なら loadCanvasRoot の子を隠す。")]
    private GameObject loadVisualRoot;
    [SerializeField, Tooltip("入場用黒フェード。LoadCanvas 配下に置き、見た目破棄後も残す。")]
    private Fade entryFade;
    [SerializeField, Tooltip("entryFade 未設定時に生成する FadeCanvas プレハブ（任意）。")]
    private GameObject entryFadePrefab;
    [SerializeField] private KomayamaCraftLoadAnimController loadAnimController;
    [SerializeField, Min(0)] private int loadingSortOrder = 9999;
    [SerializeField, Min(0.01f)] private float fadeToBlackSeconds = 0.35f;
    [SerializeField, Min(0.01f)] private float fadeFromBlackSeconds = 0.5f;
    [SerializeField, Min(0.5f)] private float readyTimeoutSeconds = 15f;

    private bool loading;
    private GameObject entryFadeRoot;

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
        if (TitleDebugManager.IsDebugPreviewLoadAnimationActive)
        {
            BeginDebugPreviewLoadAnimation();
            return;
        }

        // ロード中でなければ初期非表示。Awake で消すと BeginLoad の有効化と競合する。
        if (!loading && loadCanvasRoot != null)
        {
            loadCanvasRoot.SetActive(false);
        }
    }

    /// <summary>
    /// TitleDebugManager のロードアニメプレビュー。遷移はしない。
    /// </summary>
    public void BeginDebugPreviewLoadAnimation()
    {
        if (loadCanvasRoot == null)
        {
            loadCanvasRoot = gameObject;
        }

        if (loadCanvas == null)
        {
            loadCanvas = loadCanvasRoot.GetComponent<Canvas>();
        }

        if (!loadCanvasRoot.activeSelf)
        {
            loadCanvasRoot.SetActive(true);
        }

        if (loadVisualRoot != null && !loadVisualRoot.activeSelf)
        {
            loadVisualRoot.SetActive(true);
        }

        if (entryFade != null)
        {
            entryFade.gameObject.SetActive(false);
        }

        if (loadCanvas != null)
        {
            loadCanvas.overrideSorting = true;
            loadCanvas.sortingOrder = loadingSortOrder;
        }

        ResolveLoadAnim()?.Play();
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

        ResolveLoadAnim()?.Play();
        StartCoroutine(CoLoad(sceneName));
    }

    private KomayamaCraftLoadAnimController ResolveLoadAnim()
    {
        if (loadAnimController != null)
        {
            return loadAnimController;
        }

        loadAnimController = GetComponentInChildren<KomayamaCraftLoadAnimController>(true);
        return loadAnimController;
    }

    private IEnumerator CoLoad(string sceneName)
    {
        KomayamaCraftBootReady.ResetForLoad();
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
        EnsureEntryFade();
        // Fade.Start の Init 待ち
        yield return null;

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

        // シーンに Ambience が無い場合はここで Ready 扱い
        if (!KomayamaCraftBootReady.AmbienceReady &&
            FindFirstObjectByType<KomayamaMapAmbienceController>(FindObjectsInactive.Include) == null)
        {
            KomayamaCraftBootReady.NotifyAmbienceReady();
        }

        yield return CoWaitBootReady();

        // 黒へ暗転（本プロジェクトの FadeIn＝画面を黒くする）
        yield return CoRunFade(toBlack: true);

        HideOrDestroyLoadVisual();

        // 黒からフェードイン（FadeOut＝黒を晴らす）
        yield return CoRunFade(toBlack: false);

        FinishLoading(destroyRoot: true);
    }

    private IEnumerator CoWaitBootReady()
    {
        float deadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, readyTimeoutSeconds);
        while (!KomayamaCraftBootReady.IsAllReady)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Debug.LogWarning(
                    "[KomayamaCraftSceneLoader] BootReady timeout — proceeding. " +
                    KomayamaCraftBootReady.DescribePending());
                yield break;
            }

            yield return null;
        }
    }

    private void EnsureEntryFade()
    {
        if (entryFade != null)
        {
            entryFadeRoot = entryFade.gameObject;
            if (!entryFadeRoot.activeSelf)
            {
                entryFadeRoot.SetActive(true);
            }

            EnsureFadeCanvasFrontMost(entryFadeRoot, loadingSortOrder + 1);
            return;
        }

        if (entryFadePrefab == null)
        {
            return;
        }

        entryFadeRoot = Instantiate(entryFadePrefab);
        entryFadeRoot.name = "CraftEntryFadeCanvas";
        DontDestroyOnLoad(entryFadeRoot);
        EnsureFadeCanvasFrontMost(entryFadeRoot, loadingSortOrder + 1);
        entryFade = entryFadeRoot.GetComponent<Fade>();
        if (entryFade == null)
        {
            entryFade = entryFadeRoot.GetComponentInChildren<Fade>(true);
        }
    }

    private static void EnsureFadeCanvasFrontMost(GameObject fadeObject, int sortingOrder)
    {
        if (fadeObject == null)
        {
            return;
        }

        Canvas canvas = fadeObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = fadeObject.GetComponentInChildren<Canvas>(true);
        }

        if (canvas == null)
        {
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
    }

    private IEnumerator CoRunFade(bool toBlack)
    {
        if (entryFade == null)
        {
            yield break;
        }

        bool done = false;
        float seconds = toBlack
            ? Mathf.Max(0.01f, fadeToBlackSeconds)
            : Mathf.Max(0.01f, fadeFromBlackSeconds);
        if (toBlack)
        {
            entryFade.FadeIn(seconds, () => done = true);
        }
        else
        {
            entryFade.FadeOut(seconds, () => done = true);
        }

        while (!done)
        {
            yield return null;
        }
    }

    private void HideOrDestroyLoadVisual()
    {
        if (loadVisualRoot != null)
        {
            Destroy(loadVisualRoot);
            loadVisualRoot = null;
            return;
        }

        if (loadCanvasRoot == null)
        {
            return;
        }

        Transform root = loadCanvasRoot.transform;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (entryFadeRoot != null &&
                (child == entryFadeRoot.transform ||
                 entryFadeRoot.transform.IsChildOf(child)))
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private void FinishLoading(bool destroyRoot)
    {
        loading = false;
        KomayamaCraftLoadGate.ReleaseHold();

        if (entryFadeRoot != null && entryFadeRoot != loadCanvasRoot)
        {
            Destroy(entryFadeRoot);
            entryFadeRoot = null;
            entryFade = null;
        }

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
