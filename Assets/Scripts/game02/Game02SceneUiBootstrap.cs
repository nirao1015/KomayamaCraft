using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// game02_scene 用の最小UIセットアップ。
/// FieldCanvas / PanelCanvas / HoverHit を参照し、ホバー雛形を自動生成・接続する。
/// WorkUpgradeEd* はサイドパネル向けに Hover2HitArea のみを用意する（旧 HoverHit は除去）。
/// </summary>
[ExecuteAlways]
public class Game02SceneUiBootstrap : MonoBehaviour
{
    private static bool s_loggedHoverTargetsBoundOnceThisPlaySession;
    private static bool s_hoverBoundLogCoroutineScheduled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetHoverTargetsBoundLogSession()
    {
        s_loggedHoverTargetsBoundOnceThisPlaySession = false;
        s_hoverBoundLogCoroutineScheduled = false;
    }

    [Header("Scene Refs")]
    [SerializeField] private Game02UISceneReferences sceneReferences;
    [SerializeField] private string fieldCanvasName = "FieldCanvas";
    [SerializeField] private string panelCanvasName = "PanelCanvas";
    [SerializeField] private string hoverHitObjectPath = "Hover/HoverHit";
    [SerializeField] private bool setupInEditMode = true;
    [SerializeField] private bool autoBindAllContentProviders = true;
    [SerializeField] private bool includeInactiveTargets = true;
    [SerializeField] private Transform[] extraHoverSearchRoots;
    [SerializeField] private bool autoEnsureWorkUpgradeEdHoverAreas = true;

    [Header("HoverHit Default Hover Text")]
    [SerializeField] private string defaultHoverTitle = "HoverHit";
    [SerializeField] [TextArea(2, 5)] private string defaultHoverDescription = "Hover text.";

    [Header("Tooltip")]
    [SerializeField] private Vector2 tooltipAnchoredPosition = new Vector2(320f, -160f);
    [SerializeField] private Vector2 tooltipPivot = new Vector2(0f, 1f);
    [SerializeField] private float tooltipShowDelaySeconds = 0.25f;

    [Header("Hover Cursor")]
    [SerializeField] private bool applyHoverCursorSettingsFromBootstrap = false;
    [SerializeField] private bool enableHoverCursorSwap = false;
    [SerializeField] private Texture2D hoverCursorTexture;
    [SerializeField] private Vector2 hoverCursorHotspot = Vector2.zero;
    [SerializeField] private CursorMode hoverCursorMode = CursorMode.Auto;
    [SerializeField] private bool useOverlayCursorFallback = true;

    [Header("Collider Fallback")]
    [SerializeField] private bool autoAddHoverCollider = true;
    [SerializeField] private bool autoAddPhysicsRaycasterToMainCamera = true;
    private bool isApplyingSetup;

    private void Awake()
    {
        RunSetup();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            return;
        }

        if (setupInEditMode)
        {
            RunSetup();
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying || !setupInEditMode)
        {
            return;
        }

        RunSetup();
    }

    [ContextMenu("Setup Hover Objects Now")]
    public void SetupHoverObjectsNow()
    {
        RunSetup();
    }

    private void RunSetup()
    {
        if (isApplyingSetup)
        {
            return;
        }

        isApplyingSetup = true;
        try
        {
            EnsureSceneReferences();
            if (sceneReferences == null || sceneReferences.PanelCanvas == null)
            {
                Debug.LogWarning("[Game02SceneUiBootstrap] PanelCanvas が見つからないためセットアップを中断します。");
                return;
            }

            Game02HoverPresenter presenter = EnsureHoverPresenter(sceneReferences.PanelCanvas);
            presenter.ConfigureTiming(tooltipShowDelaySeconds);
            if (applyHoverCursorSettingsFromBootstrap)
            {
                presenter.ConfigureHoverCursor(
                    enableHoverCursorSwap,
                    hoverCursorTexture,
                    hoverCursorHotspot,
                    hoverCursorMode,
                    useOverlayCursorFallback);
            }
            EnsurePrimaryHoverHitBinding(presenter);
            if (autoEnsureWorkUpgradeEdHoverAreas)
            {
                EnsureWorkUpgradeEdHoverAreas(presenter);
            }
            if (autoBindAllContentProviders)
            {
                EnsureAllHoverTargetsBound(presenter);
            }
        }
        finally
        {
            isApplyingSetup = false;
        }
    }

    private void EnsureSceneReferences()
    {
        if (sceneReferences == null)
        {
            sceneReferences = GetComponent<Game02UISceneReferences>();
        }

        if (sceneReferences == null)
        {
            sceneReferences = gameObject.AddComponent<Game02UISceneReferences>();
        }

        RectTransform fieldCanvas = FindRectTransformByName(fieldCanvasName);
        RectTransform panelCanvas = FindRectTransformByName(panelCanvasName);
        Transform hoverHit = FindHoverHitTarget(fieldCanvas);
        sceneReferences.Configure(fieldCanvas, hoverHit, panelCanvas);
    }

    private RectTransform FindRectTransformByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        GameObject go = GameObject.Find(objectName);
        if (go == null)
        {
            return null;
        }

        return go.GetComponent<RectTransform>();
    }

    private Transform FindHoverHitTarget(RectTransform fieldCanvas)
    {
        if (fieldCanvas == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(hoverHitObjectPath))
        {
            Transform byPath = fieldCanvas.Find(hoverHitObjectPath);
            if (byPath != null)
            {
                return byPath;
            }
        }

        return FindChildRecursive(fieldCanvas, "HoverHit");
    }

    private static Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        if (parent.name == targetName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private Game02HoverPresenter EnsureHoverPresenter(RectTransform panelCanvas)
    {
        Transform systemRoot = panelCanvas.Find("Game02HoverSystem");
        if (systemRoot == null)
        {
            GameObject go = new GameObject("Game02HoverSystem", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(panelCanvas, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            systemRoot = go.transform;
        }

        RectTransform tooltipRoot = EnsureTooltipRoot(systemRoot);
        TMP_Text titleText = EnsureTooltipText(tooltipRoot, "TitleText", 28f, FontStyles.Bold, new Vector2(20f, -16f));
        TMP_Text descriptionText = EnsureTooltipText(tooltipRoot, "DescriptionText", 22f, FontStyles.Normal, new Vector2(20f, -62f));

        Game02HoverPresenter presenter = systemRoot.GetComponent<Game02HoverPresenter>();
        if (presenter == null)
        {
            presenter = systemRoot.gameObject.AddComponent<Game02HoverPresenter>();
        }

        presenter.Configure(panelCanvas, tooltipRoot, titleText, descriptionText);
        return presenter;
    }

    private RectTransform EnsureTooltipRoot(Transform parent)
    {
        Transform existing = parent.Find("HoverTooltipRoot");
        RectTransform rect;
        bool created = false;
        if (existing == null)
        {
            GameObject go = new GameObject("HoverTooltipRoot", typeof(RectTransform), typeof(Image));
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            created = true;
        }
        else
        {
            rect = existing.GetComponent<RectTransform>();
        }

        if (created)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = tooltipPivot;
            rect.anchoredPosition = tooltipAnchoredPosition;
            rect.sizeDelta = new Vector2(240f, 120f);
        }

        Image background = rect.GetComponent<Image>();
        if (background == null)
        {
            background = rect.gameObject.AddComponent<Image>();
        }

        if (created)
        {
            background.color = new Color(0f, 0f, 0f, 0.82f);
        }

        return rect;
    }

    private static TMP_Text EnsureTooltipText(
        RectTransform parent,
        string objectName,
        float fontSize,
        FontStyles fontStyle,
        Vector2 anchoredPosition)
    {
        Transform existing = parent.Find(objectName);
        TextMeshProUGUI text;
        bool created = false;
        if (existing == null)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            text = go.GetComponent<TextMeshProUGUI>();
            created = true;
        }
        else
        {
            text = existing.GetComponent<TextMeshProUGUI>();
        }

        if (created)
        {
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = anchoredPosition;
            textRect.sizeDelta = new Vector2(460f, 120f);

            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = string.Empty;
        }

        return text;
    }

    private void EnsurePrimaryHoverHitBinding(Game02HoverPresenter presenter)
    {
        if (sceneReferences == null || sceneReferences.HoverHitTarget == null)
        {
            Debug.LogWarning("[Game02SceneUiBootstrap] FieldCanvas/Hover/HoverHit が見つからないため、主ホバー呼び出し元を作成できません。");
            return;
        }

        GameObject hoverHit = sceneReferences.HoverHitTarget.gameObject;

        Game02HoverContentProvider provider = hoverHit.GetComponent<Game02HoverContentProvider>();
        if (provider == null)
        {
            provider = hoverHit.AddComponent<Game02HoverContentProvider>();
        }

        if (string.IsNullOrEmpty(provider.GetTitle()) && string.IsNullOrEmpty(provider.GetDescription()))
        {
            provider.SetContent(defaultHoverTitle, defaultHoverDescription);
        }

        Game02HoverTrigger trigger = hoverHit.GetComponent<Game02HoverTrigger>();
        if (trigger == null)
        {
            trigger = hoverHit.AddComponent<Game02HoverTrigger>();
        }

        trigger.ConfigureBinding(presenter, provider);

        if (autoAddHoverCollider)
        {
            EnsureHoverCollider(hoverHit);
        }

        EnsurePhysicsRaycasterIfNeeded(hoverHit);
    }

    private void EnsureAllHoverTargetsBound(Game02HoverPresenter presenter)
    {
        int boundCount = 0;
        boundCount += BindHoverTargetsInRoot(sceneReferences != null ? sceneReferences.FieldCanvas : null, presenter);
        boundCount += BindHoverTargetsInRoot(sceneReferences != null ? sceneReferences.PanelCanvas : null, presenter);

        if (extraHoverSearchRoots != null)
        {
            for (int i = 0; i < extraHoverSearchRoots.Length; i++)
            {
                boundCount += BindHoverTargetsInRoot(extraHoverSearchRoots[i], presenter);
            }
        }

        // ExecuteAlways + エディタ OnValidate で RunSetup が何度も走るため、再生中のみ。かつプレイセッションで最初の1回だけ。
        if (!Application.isPlaying)
        {
            return;
        }

        if (s_loggedHoverTargetsBoundOnceThisPlaySession)
        {
            return;
        }

        if (s_hoverBoundLogCoroutineScheduled)
        {
            return;
        }

        s_hoverBoundLogCoroutineScheduled = true;
        StartCoroutine(CoLogHoverBoundOnceEndOfFrame(boundCount));
    }

    private IEnumerator CoLogHoverBoundOnceEndOfFrame(int boundCount)
    {
        // 同一フレーム内の他コンポーネント Awake（例: SaveCoordinator）より後に出したいため EndOfFrame まで遅延。
        yield return new WaitForEndOfFrame();

        s_loggedHoverTargetsBoundOnceThisPlaySession = true;

        string utc = DateTime.UtcNow.ToString("o");
        Debug.Log($"{Game02.Game02SceneLifecycleLog.Tag} [Game02SceneUiBootstrap] Hover targets bound count={boundCount} utc={utc} unscaledTime={Time.unscaledTime:F3}s realtimeSinceStartup={Time.realtimeSinceStartup:F3}s frame={Time.frameCount}");
    }

    private int BindHoverTargetsInRoot(Transform root, Game02HoverPresenter presenter)
    {
        if (root == null)
        {
            return 0;
        }

        Game02HoverContentProvider[] providers = root.GetComponentsInChildren<Game02HoverContentProvider>(includeInactiveTargets);
        int boundCount = 0;
        for (int i = 0; i < providers.Length; i++)
        {
            Game02HoverContentProvider provider = providers[i];
            if (provider == null)
            {
                continue;
            }

            GameObject go = provider.gameObject;
            Game02HoverTrigger trigger = go.GetComponent<Game02HoverTrigger>();
            if (trigger == null)
            {
                trigger = go.AddComponent<Game02HoverTrigger>();
            }

            trigger.ConfigureBinding(presenter, provider);
            BringHoverHitToFrontIfNeeded(go.transform);
            EnsureParentHoverBridge(go.transform, trigger);
            EnsurePhysicsRaycasterIfNeeded(go);
            boundCount++;
        }

        return boundCount;
    }

    private void EnsureWorkUpgradeEdHoverAreas(Game02HoverPresenter presenter)
    {
        if (presenter == null)
        {
            return;
        }

        string[] targetNames = { "WorkUpgradeEd01", "WorkUpgradeEd02", "WorkUpgradeEd03", "WorkUpgradeEd04", "WorkUpgradeEd05", "WorkUpgradeEd51" };
        for (int i = 0; i < targetNames.Length; i++)
        {
            EnsureHoverAreasForWorkUpgradeEd(targetNames[i], presenter);
        }
    }

    private void EnsureHoverAreasForWorkUpgradeEd(string objectName, Game02HoverPresenter presenter)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return;
        }

        Transform target = FindTransformByName(objectName);
        if (target == null)
        {
            return;
        }

        // サイドパネル奥でもレイキャストできる Hover2HitArea に統一（旧 HoverHit は削除してよい）。
        DestroyLegacyHoverHitUnderWorkUpgradeEd(target);
        EnsureOneHoverArea(target, presenter, "Hover2HitArea", objectName, "アップグレード内容を確認");
    }

    /// <summary>
    /// WorkUpgradeEd* に残っている HoverHit を除去する。ホバーは Hover2HitArea のみで提供する。
    /// </summary>
    private static void DestroyLegacyHoverHitUnderWorkUpgradeEd(Transform workUpgradeEdRoot)
    {
        if (workUpgradeEdRoot == null)
        {
            return;
        }

        Transform hoverHit = workUpgradeEdRoot.Find("HoverHit");
        if (hoverHit == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.DestroyObjectImmediate(hoverHit.gameObject);
            return;
        }
#endif
        UnityEngine.Object.Destroy(hoverHit.gameObject);
    }

    private static Transform FindTransformByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        GameObject go = GameObject.Find(objectName);
        if (go != null)
        {
            return go.transform;
        }

        Transform[] all = UnityEngine.Object.FindObjectsOfType<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && string.Equals(t.name, objectName, System.StringComparison.Ordinal))
            {
                return t;
            }
        }

        return null;
    }

    private static void EnsureOneHoverArea(
        Transform parent,
        Game02HoverPresenter presenter,
        string areaName,
        string defaultTitle,
        string defaultDescription)
    {
        if (parent == null || presenter == null || string.IsNullOrEmpty(areaName))
        {
            return;
        }

        Transform existing = parent.Find(areaName);
        GameObject areaGo;
        RectTransform areaRect;
        if (existing == null)
        {
            areaGo = new GameObject(areaName, typeof(RectTransform), typeof(Image));
            areaRect = areaGo.GetComponent<RectTransform>();
            areaRect.SetParent(parent, false);
            areaRect.anchorMin = new Vector2(0f, 0f);
            areaRect.anchorMax = new Vector2(1f, 1f);
            areaRect.offsetMin = Vector2.zero;
            areaRect.offsetMax = Vector2.zero;
            areaRect.pivot = new Vector2(0.5f, 0.5f);
        }
        else
        {
            areaGo = existing.gameObject;
            areaRect = existing as RectTransform;
            if (areaRect == null)
            {
                return;
            }
        }

        Image image = areaGo.GetComponent<Image>();
        if (image == null)
        {
            image = areaGo.AddComponent<Image>();
        }

        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        Game02HoverContentProvider provider = areaGo.GetComponent<Game02HoverContentProvider>();
        if (provider == null)
        {
            provider = areaGo.AddComponent<Game02HoverContentProvider>();
        }

        if (string.IsNullOrEmpty(provider.GetTitle()) && string.IsNullOrEmpty(provider.GetDescription()))
        {
            provider.SetContent(defaultTitle, defaultDescription);
        }

        Game02HoverTrigger trigger = areaGo.GetComponent<Game02HoverTrigger>();
        if (trigger == null)
        {
            trigger = areaGo.AddComponent<Game02HoverTrigger>();
        }

        trigger.ConfigureBinding(presenter, provider);
        BringHoverHitToFrontIfNeeded(areaGo.transform);
        EnsureParentHoverBridge(areaGo.transform, trigger);
    }

    private void EnsureHoverCollider(GameObject hoverHit)
    {
        if (hoverHit == null)
        {
            return;
        }

        Collider2D existingCollider = hoverHit.GetComponent<Collider2D>();
        if (existingCollider != null)
        {
            return;
        }

        CircleCollider2D collider2D = hoverHit.AddComponent<CircleCollider2D>();
        SpriteRenderer sr = hoverHit.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            Vector3 ext = sr.bounds.extents;
            collider2D.radius = Mathf.Max(0.01f, Mathf.Max(ext.x, ext.y));
            return;
        }

        RectTransform rt = hoverHit.GetComponent<RectTransform>();
        if (rt != null)
        {
            float baseSize = Mathf.Max(0.01f, Mathf.Max(rt.rect.width, rt.rect.height) * 0.5f);
            collider2D.radius = baseSize;
            return;
        }

        collider2D.radius = 0.5f;
    }

    private void EnsurePhysicsRaycasterIfNeeded(GameObject target)
    {
        if (!autoAddPhysicsRaycasterToMainCamera || target == null)
        {
            return;
        }

        // OnMouse 系で動かす場合は不要だが、IPointer 経由でも使えるように補完しておく。
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        if (cam.GetComponent<UnityEngine.EventSystems.Physics2DRaycaster>() == null)
        {
            cam.gameObject.AddComponent<UnityEngine.EventSystems.Physics2DRaycaster>();
        }
    }

    private static void BringHoverHitToFrontIfNeeded(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (!string.Equals(target.name, "HoverHit", System.StringComparison.Ordinal) &&
            !string.Equals(target.name, "Hover2HitArea", System.StringComparison.Ordinal))
        {
            return;
        }

        target.SetAsLastSibling();
    }

    private static void EnsureParentHoverBridge(Transform hoverAreaTransform, Game02HoverTrigger trigger)
    {
        if (hoverAreaTransform == null || trigger == null)
        {
            return;
        }

        if (!string.Equals(hoverAreaTransform.name, "HoverHit", System.StringComparison.Ordinal) &&
            !string.Equals(hoverAreaTransform.name, "Hover2HitArea", System.StringComparison.Ordinal))
        {
            return;
        }

        Transform parent = hoverAreaTransform.parent;
        if (parent == null)
        {
            return;
        }

        Game02HoverPointerBridge bridge = parent.GetComponent<Game02HoverPointerBridge>();
        if (bridge == null)
        {
            bridge = parent.gameObject.AddComponent<Game02HoverPointerBridge>();
        }

        bridge.RegisterTarget(trigger);
    }
}
