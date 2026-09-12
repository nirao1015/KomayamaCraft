using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class Game03PodManager : MonoBehaviour
{
    private enum SkipTapInput
    {
        None,
        MouseLeft,
        W,
        A,
        S,
        D,
        Space
    }

    [Header("References")]
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03BgmManager game03BgmManager;
    [SerializeField] private Game03WeaponManager game03WeaponManager;
    [SerializeField] private Game03LevelUpManager game03LevelUpManager;
    [SerializeField] private Game03UnitManager game03UnitManager;
    [SerializeField] private Game03SeManager game03SeManager;
    [SerializeField, Tooltip("Game03DebugManager の POD 降下スキップ参照用。未設定時は Awake でシーン検索。")]
    private Game03DebugManager game03DebugManager;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private GameObject enemyCanvasRoot;
    [SerializeField] private GameObject itemCanvasRoot;
    [SerializeField] private GameObject weaponCanvasRoot;
    [SerializeField] private GameObject speedViewIcon;
    [FormerlySerializedAs("panelCanvasRoot")]
    [SerializeField] private RectTransform panelOjRoot;
    [SerializeField] private RectTransform itemPodCanvasRoot;
    [SerializeField, Tooltip("StageStart / FallPod 演出中は非表示にする ItemNavi ルート（PanelCanvas 直下の ItemNavi など）。")]
    private GameObject itemNaviRoot;

    [Header("Fall Pod Panel")]
    [SerializeField] private RectTransform fallPodPanel;
    [SerializeField] private RectTransform fallPodOj;
    [SerializeField] private RectTransform podGroundOj;
    [SerializeField] private Image podImage;
    [SerializeField] private Image podFireImage;
    [SerializeField] private Image podC2Image;
    [SerializeField] private RectTransform podShadowRect;
    [SerializeField] private Image podC1Image;
    [SerializeField] private Image podGroundImage;
    [SerializeField] private RectTransform podUnitRect;
    [SerializeField] private Image podUnitImage;
    [SerializeField] private RectTransform podUnitShadowRect;
    [SerializeField] private Image podUnitShadowImage;
    [SerializeField] private RectTransform fallPodWaitOj;

    [Header("Stage Start Pod")]
    [SerializeField] private PodSequenceVisuals stageStartVisuals;

    [Header("Unit Slots")]
    [SerializeField] private RectTransform mainUnitOj;
    [SerializeField] private RectTransform unit01Oj;
    [SerializeField] private RectTransform unit02Oj;
    [SerializeField] private RectTransform unit03Oj;
    [SerializeField] private RectTransform unit04Oj;

    [Header("Animation Timings")]
    [SerializeField, Min(0.01f)] private float normalFallSeconds = 5f;
    [SerializeField, Min(0.01f)] private float fastFallSeconds = 0.2f;
    [SerializeField, Min(0f)] private float landingSmokeSeconds = 0.5f;
    [SerializeField, Min(0f)] private float unitAppearDelaySeconds = 2.2f;
    [SerializeField, Min(0.01f)] private float revealFadeSeconds = 0.4f;
    [SerializeField, Min(0.01f)] private float firstWalkSeconds = 0.8f;
    [SerializeField, Min(0.01f)] private float secondWalkSeconds = 0.8f;
    [SerializeField, Min(0f)] private float walkPauseSeconds = 0.8f;
    [SerializeField, Min(0.01f)] private float unitMoveSeconds = 0.6f;

    [Header("Animation Distances")]
    [SerializeField] private float startY = 560f;
    [SerializeField] private float nearLandingY = 140f;
    [SerializeField] private float landingY = 0f;
    [SerializeField] private float c2SwayAmplitude = 12f;
    [SerializeField] private float c2SwayFrequency = 8f;
    [SerializeField, Min(0f)] private float landingShiftByUnitY = 120f;
    [SerializeField] private float podLandingYOffset = 0f;
    [SerializeField] private float firstWalkOffsetY = -26f;
    [SerializeField] private float secondWalkOffsetY = -52f;
    [SerializeField, Min(1f)] private float sequenceFastForwardMultiplier = 3f;

    [Header("Camera Sequence")]
    [SerializeField, Tooltip("ON のときポッド演出中にカメラズームを行う。OFF のときズームしない。")]
    private bool enableCameraZoom = true;
    [SerializeField, Min(0.01f)] private float zoomedOrthographicSize = 4.2f;

    [Header("Shadow Scale")]
    [SerializeField] private Vector3 shadowStartScale = new Vector3(0.4f, 0.4f, 1f);
    [SerializeField] private Vector3 shadowEndScale = new Vector3(1f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float podUnitShadowMaxAlpha = 180f / 255f;

    [Header("Pseudo Zoom")]
    [SerializeField, Min(0f), Tooltip("降下演出中、MainUnitOj のみ他ユニットよりさらに拡大する倍率。1.2 なら 20% 上乗せ。")]
    private float mainUnitZoomExtraScale = 1.2f;

    [Header("Skip Input")]
    [SerializeField, Min(0.05f), Tooltip("同一キー/同一クリックをダブル入力として扱う最大間隔（秒）。")]
    private float skipDoubleTapIntervalSeconds = 0.28f;
    [SerializeField, Min(0.01f), Tooltip("スキップ成立時にズーム/位置を戻す短いトランジション秒数。")]
    private float skipReturnTransitionSeconds = 0.12f;

    [Header("Pod Remnant")]
    [SerializeField, Min(0.1f), Tooltip("MainUnitOj からこの画面倍率以上離れたら Pod 残像を削除する。")]
    private float podRemnantRemoveDistanceScreens = 1.5f;
    [SerializeField, Min(0.05f), Tooltip("Pod 残像の距離チェック間隔（秒）。")]
    private float podRemnantCheckIntervalSeconds = 0.2f;

    private bool isSequenceRunning;
    private bool wasPausedBeforeSequence;
    private bool wasEnemyCanvasActive;
    private bool wasItemCanvasActive;
    private bool wasWeaponCanvasActive;
    private Vector2 recruitmentPodGroundDesignAnchored;
    private Vector2 stageStartPodGroundDesignAnchored;
    private bool hasRecruitmentPodGroundDesignAnchored;
    private bool hasStageStartPodGroundDesignAnchored;
    private Vector2 podC2BaseAnchoredPosition;
    private bool hasPodC2BaseAnchoredPosition;
    private float podC2InitialAlpha = 1f;
    private bool hasPodC2InitialAlpha;
    private bool hasCameraCache;
    private Vector3 cachedCameraPosition;
    private float cachedCameraOrthoSize;
    private Vector3 lockedZoomCameraPosition;
    private float lockedZoomCameraOrthoSize;
    private Vector3 cachedFallPodPanelScale = Vector3.one;
    private bool hasFallPodPanelScaleCache;
    private Vector2 cachedFallPodPanelAnchoredPosition = Vector2.zero;
    private bool hasFallPodPanelAnchoredPositionCache;
    private readonly List<GameObject> hiddenPanelCanvasChildren = new List<GameObject>();
    private readonly RectTransform[] cachedUnitSlotRects = new RectTransform[5];
    private readonly Vector3[] cachedUnitSlotScales = new Vector3[5];
    private bool hasUnitSlotScaleCache;
    private int cachedMainUnitSiblingIndex = -1;
    private bool hasCachedMainUnitSiblingIndex;
    private bool skipSequenceRequested;
    private bool skipEndVisualApplied;
    private RectTransform activeSkipTargetSlot;
    private Vector2 cachedSkipTargetPanelPosition;
    private bool hasCachedSkipTargetPanelPosition;
    private SkipTapInput lastSkipTapInput = SkipTapInput.None;
    private float lastSkipTapTime = -999f;
    private RectTransform podRemnantRoot;
    private Coroutine podRemnantWatchCoroutine;

    private PodSequenceVisuals recruitmentVisuals;
    private PodSequenceVisuals activeSeq;
    private RectTransform cachedZoomPanelRoot;
    private float activeSequencePodGroundPivotX;
    private bool hasCachedItemNaviActiveDuringPod;
    private bool wasItemNaviActiveDuringPod;

    private void Awake()
    {
        if (game03DebugManager == null)
        {
            game03DebugManager = FindAnyObjectByType<Game03DebugManager>(FindObjectsInactive.Include);
        }

        if (game03LevelUpManager == null)
        {
            game03LevelUpManager = FindAnyObjectByType<Game03LevelUpManager>(FindObjectsInactive.Include);
        }

        if (fallPodPanel != null)
        {
            fallPodPanel.gameObject.SetActive(false);
            if (!hasFallPodPanelScaleCache)
            {
                cachedFallPodPanelScale = fallPodPanel.localScale;
                hasFallPodPanelScaleCache = true;
            }

            if (!hasFallPodPanelAnchoredPositionCache)
            {
                cachedFallPodPanelAnchoredPosition = fallPodPanel.anchoredPosition;
                hasFallPodPanelAnchoredPositionCache = true;
            }
        }

        CacheUnitSlotScaleIfNeeded();

        recruitmentVisuals = new PodSequenceVisuals
        {
            panelRoot = fallPodPanel,
            fallPodOj = fallPodOj,
            podGroundOj = podGroundOj,
            podImage = podImage,
            podFireImage = podFireImage,
            podC2Image = podC2Image,
            podShadowRect = podShadowRect,
            podC1Image = podC1Image,
            podGroundImage = podGroundImage,
            podUnitRect = podUnitRect,
            podUnitImage = podUnitImage,
            podUnitShadowRect = podUnitShadowRect,
            podUnitShadowImage = podUnitShadowImage,
            fallPodWaitOj = fallPodWaitOj
        };

        CachePodGroundDesignAnchored(recruitmentVisuals.podGroundOj, ref recruitmentPodGroundDesignAnchored, ref hasRecruitmentPodGroundDesignAnchored);
        CachePodGroundDesignAnchored(stageStartVisuals.podGroundOj, ref stageStartPodGroundDesignAnchored, ref hasStageStartPodGroundDesignAnchored);
    }

    private readonly HashSet<int> deployedPodTypesScratch = new HashSet<int>();

    /// <summary>デバッグ: 未装着の仲間 POD（02〜07）を通常抽選で開始する。</summary>
    public bool DebugStartRandomEligiblePod()
    {
        RectTransform[] subSlots = { unit01Oj, unit02Oj, unit03Oj, unit04Oj };
        Game03PodTypeSelection.CollectDeployedSubPodTypes(game03WeaponManager, subSlots, deployedPodTypesScratch);
        if (!Game03PodTypeSelection.TryDrawEligiblePodType(deployedPodTypesScratch, out int podType))
        {
            Debug.LogWarning("[Game03PodManager] No eligible pod type (all 02-07 deployed or missing refs).", this);
            return false;
        }

        return StartPodSequence(podType);
    }

    public bool StartPodSequence(int podType)
    {
        if (isSequenceRunning)
        {
            return false;
        }

        if (!TryResolvePodSprites(podType, out Sprite podSprite, out Sprite podUnitSprite))
        {
            return false;
        }

        if (!TryGetFirstEmptyUnitSlot(out RectTransform targetSlot, out int slotIndex))
        {
            return false;
        }

        StartCoroutine(PlaySequenceRoutine(podType, podSprite, podUnitSprite, targetSlot, slotIndex));
        return true;
    }

    /// <summary>
    /// シーン入室時の StageStartPanel 降下演出。仲間登録・武器付与・フロント画像差し替えは行わない。
    /// </summary>
    public bool TryBeginStageStartSequence()
    {
        if (isSequenceRunning)
        {
            return false;
        }

        if (!stageStartVisuals.IsValidForSequence())
        {
            return false;
        }

        if (mainUnitOj == null && game03UnitManager != null)
        {
            mainUnitOj = game03UnitManager.MainUnitRect;
        }

        if (mainUnitOj == null)
        {
            return false;
        }

        StartCoroutine(PlayStageStartSequenceRoutine());
        return true;
    }

    private IEnumerator PlaySequenceRoutine(int podType, Sprite podSprite, Sprite podUnitSprite, RectTransform targetSlot, int slotIndex)
    {
        activeSeq = recruitmentVisuals;
        isSequenceRunning = true;
        skipSequenceRequested = false;
        lastSkipTapInput = SkipTapInput.None;
        lastSkipTapTime = -999f;
        SetSpeedViewIconVisible(false);
        wasPausedBeforeSequence = game03Manager != null && game03Manager.IsPaused;

        if (game03Manager != null)
        {
            game03Manager.BeginCutscene();
            game03Manager.SetPaused(true, suppressGameplayPausePanelWhilePaused: true);
        }

        CacheCameraState();

        if (game03BgmManager != null)
        {
            game03BgmManager.StopBgm();
        }

        CacheAndHideGameplayCanvases();
        CacheAndHideItemNaviForPodSequence();
        ApplyMainUnitFrontSiblingDuringSequence();

        if (!PrepareSequenceVisuals(podSprite, podUnitSprite))
        {
            RestoreItemNaviAfterPodSequence();
            if (game03Manager != null)
            {
                game03Manager.EndCutscene(true);
                if (!wasPausedBeforeSequence)
                {
                    game03Manager.ResumeGame();
                }
            }

            RestoreGameplayCanvases();
            RestoreMainUnitSiblingOrder();
            CleanupSequenceState();
            yield break;
        }

        BeginSequenceSkipCache(targetSlot);

        // デバッグ: 降下〜装着アニメを即終了（GetSequenceDeltaTime のスキップ経路と同等）。
        if (game03DebugManager != null && game03DebugManager.EffectiveSkipPodDescentSequence)
        {
            skipSequenceRequested = true;
        }

        yield return RunNormalFall();
        yield return RunFastFallAndLanding();
        yield return RunUnitDeploy(targetSlot);

        targetSlot.gameObject.SetActive(true);
        ApplyUnitFrontSprite(targetSlot, podUnitSprite);
        int joinedPodWeaponNumber = 0;
        if (game03WeaponManager != null)
        {
            joinedPodWeaponNumber = Mathf.Clamp(podType, 1, 7);
            game03WeaponManager.AssignSubUnitWeaponBySlotIndex(slotIndex, joinedPodWeaponNumber);
            NotifyGame03CompanionWeaponAchievements(joinedPodWeaponNumber);
        }

        if (skipSequenceRequested)
        {
            // Keep unit handoff instant, then only smooth-return camera/pseudo zoom.
            yield return RunSkipReturnTransition();
        }

        CreatePodRemnantInItemCanvas();

        if (game03Manager != null)
        {
            game03Manager.EndCutscene(true);
            if (!wasPausedBeforeSequence)
            {
                game03Manager.ResumeGame();
            }
        }

        if (activeSeq.panelRoot != null)
        {
            activeSeq.panelRoot.gameObject.SetActive(false);
        }

        RestoreItemNaviAfterPodSequence();
        RestoreGameplayCanvases();
        RestoreMainUnitSiblingOrder();
        RestoreCameraImmediate();
        CleanupSequenceState();

        if (joinedPodWeaponNumber > 0 && game03LevelUpManager != null)
        {
            game03LevelUpManager.OpenPodJoinLevelUpPanel(joinedPodWeaponNumber);
        }
    }

    private IEnumerator PlayStageStartSequenceRoutine()
    {
        activeSeq = stageStartVisuals;
        isSequenceRunning = true;
        skipSequenceRequested = false;
        lastSkipTapInput = SkipTapInput.None;
        lastSkipTapTime = -999f;
        SetSpeedViewIconVisible(false);
        wasPausedBeforeSequence = game03Manager != null && game03Manager.IsPaused;

        RectTransform[] ceremonyHideRects =
        {
            mainUnitOj,
            unit01Oj,
            unit02Oj,
            unit03Oj,
            unit04Oj
        };
        bool[] ceremonyWasActive = new bool[ceremonyHideRects.Length];
        for (int i = 0; i < ceremonyHideRects.Length; i++)
        {
            RectTransform rt = ceremonyHideRects[i];
            ceremonyWasActive[i] = rt != null && rt.gameObject.activeSelf;
            if (rt != null)
            {
                rt.gameObject.SetActive(false);
            }
        }

        if (game03Manager != null)
        {
            game03Manager.BeginCutscene();
            game03Manager.SetPaused(true, suppressGameplayPausePanelWhilePaused: true);
        }

        CacheCameraState();

        if (game03BgmManager != null)
        {
            game03BgmManager.StopBgm();
        }

        CacheAndHideGameplayCanvases();
        CacheAndHideItemNaviForPodSequence();
        ApplyMainUnitFrontSiblingDuringSequence();

        if (!PrepareStageStartVisuals())
        {
            for (int i = 0; i < ceremonyHideRects.Length; i++)
            {
                RectTransform rt = ceremonyHideRects[i];
                if (rt != null)
                {
                    rt.gameObject.SetActive(ceremonyWasActive[i]);
                }
            }

            if (game03Manager != null)
            {
                game03Manager.EndCutscene(true);
                if (!wasPausedBeforeSequence)
                {
                    game03Manager.ResumeGame();
                }
            }

            RestoreItemNaviAfterPodSequence();
            RestoreGameplayCanvases();
            RestoreMainUnitSiblingOrder();
            CleanupSequenceState();
            yield break;
        }

        if (mainUnitOj == null && game03UnitManager != null)
        {
            mainUnitOj = game03UnitManager.MainUnitRect;
        }

        BeginSequenceSkipCache(mainUnitOj);

        if (game03DebugManager != null && game03DebugManager.EffectiveSkipPodDescentSequence)
        {
            skipSequenceRequested = true;
        }

        yield return RunNormalFall();
        yield return RunFastFallAndLanding();
        yield return RunUnitDeploy(mainUnitOj, stageStartThreeAxisWalk: true);

        if (skipSequenceRequested)
        {
            yield return RunSkipReturnTransition();
        }

        CreatePodRemnantInItemCanvas();

        for (int i = 0; i < ceremonyHideRects.Length; i++)
        {
            RectTransform rt = ceremonyHideRects[i];
            if (rt != null)
            {
                rt.gameObject.SetActive(ceremonyWasActive[i]);
            }
        }

        if (game03Manager != null)
        {
            game03Manager.EndCutscene(true);
            if (!wasPausedBeforeSequence)
            {
                game03Manager.ResumeGame();
            }
        }

        if (activeSeq.panelRoot != null)
        {
            activeSeq.panelRoot.gameObject.SetActive(false);
        }

        NotifyGame03StageStartAchievements();
        RestoreItemNaviAfterPodSequence();
        RestoreGameplayCanvases();
        RestoreMainUnitSiblingOrder();
        RestoreCameraImmediate();
        CleanupSequenceState();
    }

    private static void NotifyGame03StageStartAchievements()
    {
        SteamAchievementController.TryUnlock(SteamAchievementIds.Game03_01);
        Game03MetaProgressController meta = Game03MetaProgressController.Instance;
        if (meta != null && meta.HasAnyUpgradeLevels)
        {
            SteamAchievementController.TryUnlock(SteamAchievementIds.Game03_04);
        }
    }

    private static void NotifyGame03CompanionWeaponAchievements(int weaponNumber)
    {
        if (weaponNumber == 2)
        {
            SteamAchievementController.TryUnlock(SteamAchievementIds.Game03_03);
        }

        Game03MetaProgressController meta = Game03MetaProgressController.Instance;
        if (meta != null)
        {
            meta.TryRecordCompanionWeaponAcquired(weaponNumber);
            meta.TryUnlockAllCompanionWeaponsAchievementIfEligible();
        }
    }

    private void CacheAndHideItemNaviForPodSequence()
    {
        if (itemNaviRoot == null)
        {
            return;
        }

        hasCachedItemNaviActiveDuringPod = true;
        wasItemNaviActiveDuringPod = itemNaviRoot.activeSelf;
        itemNaviRoot.SetActive(false);
    }

    private void RestoreItemNaviAfterPodSequence()
    {
        if (!hasCachedItemNaviActiveDuringPod || itemNaviRoot == null)
        {
            return;
        }

        itemNaviRoot.SetActive(wasItemNaviActiveDuringPod);
        hasCachedItemNaviActiveDuringPod = false;
    }

    private bool PrepareSequenceVisuals(Sprite podSprite, Sprite podUnitSprite)
    {
        if (!activeSeq.IsValidForSequence())
        {
            return false;
        }

        activeSeq.podImage.sprite = podSprite;
        activeSeq.podUnitImage.sprite = podUnitSprite;

        float centerX = ResolvePodGroundCenterX();
        float groundLandingY = GetAdjustedGroundY();
        activeSequencePodGroundPivotX = centerX;
        hasPodC2BaseAnchoredPosition = false;
        hasPodC2InitialAlpha = false;

        EnsureZoomPanelCache(activeSeq.panelRoot);

        activeSeq.panelRoot.gameObject.SetActive(true);
        activeSeq.panelRoot.localScale = cachedFallPodPanelScale;
        activeSeq.panelRoot.anchoredPosition = cachedFallPodPanelAnchoredPosition;
        activeSeq.fallPodOj.anchoredPosition = new Vector2(centerX, startY);
        activeSeq.podShadowRect.localScale = shadowStartScale;
        ApplyLandingAnchorPositions(centerX, groundLandingY);

        SetImageEnabled(activeSeq.podFireImage, true);
        SetImageEnabled(activeSeq.podC2Image, true);
        SetImageEnabled(activeSeq.podGroundImage, false);
        EnsureImageObjectActive(activeSeq.podC1Image);
        SetImageEnabled(activeSeq.podC1Image, false);
        SetImageAlpha(activeSeq.podC1Image, 0f);
        if (activeSeq.podC2Image != null && !hasPodC2BaseAnchoredPosition)
        {
            podC2BaseAnchoredPosition = activeSeq.podC2Image.rectTransform.anchoredPosition;
            hasPodC2BaseAnchoredPosition = true;
        }

        if (activeSeq.podC2Image != null && !hasPodC2InitialAlpha)
        {
            podC2InitialAlpha = activeSeq.podC2Image.color.a;
            hasPodC2InitialAlpha = true;
        }

        SetImageAlpha(activeSeq.podC2Image, hasPodC2InitialAlpha ? podC2InitialAlpha : 1f);
        SetImageEnabled(activeSeq.podUnitImage, false);
        SetImageEnabled(activeSeq.podUnitShadowImage, false);
        return true;
    }

    private bool PrepareStageStartVisuals()
    {
        if (!activeSeq.IsValidForSequence())
        {
            return false;
        }

        float centerX = ResolvePodGroundCenterX();
        float groundLandingY = GetAdjustedGroundY();
        activeSequencePodGroundPivotX = centerX;
        hasPodC2BaseAnchoredPosition = false;
        hasPodC2InitialAlpha = false;

        EnsureZoomPanelCache(activeSeq.panelRoot);

        activeSeq.panelRoot.gameObject.SetActive(true);
        activeSeq.panelRoot.localScale = cachedFallPodPanelScale;
        activeSeq.panelRoot.anchoredPosition = cachedFallPodPanelAnchoredPosition;
        activeSeq.fallPodOj.anchoredPosition = new Vector2(centerX, startY);
        activeSeq.podShadowRect.localScale = shadowStartScale;
        ApplyLandingAnchorPositions(centerX, groundLandingY);

        SetImageEnabled(activeSeq.podFireImage, true);
        SetImageEnabled(activeSeq.podC2Image, true);
        SetImageEnabled(activeSeq.podGroundImage, false);
        EnsureImageObjectActive(activeSeq.podC1Image);
        SetImageEnabled(activeSeq.podC1Image, false);
        SetImageAlpha(activeSeq.podC1Image, 0f);
        if (activeSeq.podC2Image != null && !hasPodC2BaseAnchoredPosition)
        {
            podC2BaseAnchoredPosition = activeSeq.podC2Image.rectTransform.anchoredPosition;
            hasPodC2BaseAnchoredPosition = true;
        }

        if (activeSeq.podC2Image != null && !hasPodC2InitialAlpha)
        {
            podC2InitialAlpha = activeSeq.podC2Image.color.a;
            hasPodC2InitialAlpha = true;
        }

        SetImageAlpha(activeSeq.podC2Image, hasPodC2InitialAlpha ? podC2InitialAlpha : 1f);
        SetImageEnabled(activeSeq.podUnitImage, false);
        SetImageEnabled(activeSeq.podUnitShadowImage, false);
        return true;
    }

    private void EnsureZoomPanelCache(RectTransform panel)
    {
        if (panel == null)
        {
            return;
        }

        if (cachedZoomPanelRoot != panel || !hasFallPodPanelScaleCache || !hasFallPodPanelAnchoredPositionCache)
        {
            cachedFallPodPanelScale = panel.localScale;
            cachedFallPodPanelAnchoredPosition = panel.anchoredPosition;
            hasFallPodPanelScaleCache = true;
            hasFallPodPanelAnchoredPositionCache = true;
            cachedZoomPanelRoot = panel;
        }
    }

    private IEnumerator RunNormalFall()
    {
        Game03SeManager se = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();
        se?.TryPlayCutscenePodDescentIntroSe();

        float t = 0f;
        float centerX = ResolvePodGroundCenterX();
        float adjustedLandingY = ResolvePodLandingCenterY();
        Vector2 from = new Vector2(centerX, startY);
        Vector2 to = new Vector2(centerX, nearLandingY + (adjustedLandingY - landingY));
        while (t < normalFallSeconds)
        {
            t += GetSequenceDeltaTime();
            float p = Mathf.Clamp01(t / Mathf.Max(0.01f, normalFallSeconds));
            float eased = 1f - Mathf.Pow(1f - p, 2f);
            activeSeq.fallPodOj.anchoredPosition = Vector2.Lerp(from, to, eased);
            activeSeq.podShadowRect.localScale = Vector3.Lerp(shadowStartScale, shadowEndScale, p);
            UpdatePodC2Visual(p, t);
            UpdateCameraDuringFall(p);
            yield return null;
        }

        activeSeq.fallPodOj.anchoredPosition = to;
        activeSeq.podShadowRect.localScale = shadowEndScale;
        LockCurrentCameraStateAsZoom();

        if (se != null)
        {
            yield return se.FadeOutCutscenePodDescentIntroAndWait(se.PodDescentIntroFadeOutSeconds);
        }
    }

    private IEnumerator RunFastFallAndLanding()
    {
        SetImageEnabled(activeSeq.podFireImage, false);
        SetImageEnabled(activeSeq.podC2Image, false);
        float t = 0f;
        Vector2 from = activeSeq.fallPodOj.anchoredPosition;
        Vector2 to = new Vector2(ResolvePodGroundCenterX(), ResolvePodLandingCenterY());
        while (t < fastFallSeconds)
        {
            t += GetSequenceDeltaTime();
            float p = Mathf.Clamp01(t / Mathf.Max(0.01f, fastFallSeconds));
            float eased = 1f - (1f - p) * (1f - p);
            activeSeq.fallPodOj.anchoredPosition = Vector2.Lerp(from, to, eased);
            KeepCameraLockedZoom();
            yield return null;
        }

        activeSeq.fallPodOj.anchoredPosition = to;
        ApplyLandingAnchorPositions(ResolvePodGroundCenterX(), GetAdjustedGroundY());
        SetImageEnabled(activeSeq.podGroundImage, true);

        Game03SeManager seGround = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();
        seGround?.PlayCutscenePodFastFallGroundImpactSe();

        yield return RunPodC1Impact();
        yield return WaitUnscaled(landingSmokeSeconds);
    }

    private IEnumerator RunPodC1Impact()
    {
        if (activeSeq.podC1Image == null)
        {
            yield break;
        }

        EnsureImageObjectActive(activeSeq.podC1Image);
        SetImageEnabled(activeSeq.podC1Image, true);
        SetImageAlpha(activeSeq.podC1Image, 0f);
        SyncPodC1ToUnit();
        float duration = Mathf.Max(0.08f, landingSmokeSeconds * 0.4f);
        float t = 0f;
        while (t < duration)
        {
            t += GetSequenceDeltaTime();
            float p = Mathf.Clamp01(t / duration);
            // Keep alpha at 0 here; fade-in starts later during unitAppearDelay.
            SetImageAlpha(activeSeq.podC1Image, 0f);
            SyncPodC1ToUnit();
            float shakeX = Mathf.Sin(t * 30f) * 10f * (1f - p);
            if (activeSeq.podUnitRect == null || !activeSeq.podC1Image.rectTransform.IsChildOf(activeSeq.podUnitRect))
            {
                activeSeq.podC1Image.rectTransform.anchoredPosition += new Vector2(shakeX, 0f);
            }
            yield return null;
        }

        SyncPodC1ToUnit();
        SetImageAlpha(activeSeq.podC1Image, 0f);
    }

    private IEnumerator RunUnitDeploy(RectTransform targetSlot, bool stageStartThreeAxisWalk = false)
    {
        yield return WaitUnitAppearDelayWithPodC1FadeIn();
        yield return RunSmokeToUnitReveal();

        Vector2 start = activeSeq.podUnitRect.anchoredPosition;
        Game03SeManager seWalk = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();

        if (stageStartThreeAxisWalk)
        {
            float d = ResolveStageStartWalkStepDistance();
            float forwardSign = Mathf.Sign(firstWalkOffsetY);
            if (Mathf.Abs(forwardSign) < 0.001f)
            {
                forwardSign = -1f;
            }

            Vector2 firstStop = start + new Vector2(0f, forwardSign * d);
            Vector2 secondStop = firstStop + new Vector2(d, 0f);
            Vector2 thirdStop = secondStop + new Vector2(-d, 0f);

            seWalk?.PlayCutscenePodFirstWalkStartSe();
            yield return RunUnitMove(start, firstStop, firstWalkSeconds, false, true);
            yield return WaitUnscaled(walkPauseSeconds);

            seWalk?.PlayCutscenePodSecondWalkStartSe();
            yield return RunUnitMove(firstStop, secondStop, secondWalkSeconds, false);
            yield return WaitUnscaled(walkPauseSeconds);

            seWalk?.PlayCutscenePodSecondWalkStartSe();
            yield return RunUnitMove(secondStop, thirdStop, secondWalkSeconds, false);
            yield return WaitUnscaled(walkPauseSeconds);

            yield return RunUnitDashToSlot(thirdStop, targetSlot, unitMoveSeconds);
        }
        else
        {
            Vector2 firstStop = start + new Vector2(0f, firstWalkOffsetY);
            Vector2 secondStop = start + new Vector2(0f, secondWalkOffsetY);

            seWalk?.PlayCutscenePodFirstWalkStartSe();
            yield return RunUnitMove(start, firstStop, firstWalkSeconds, false, true);
            yield return WaitUnscaled(walkPauseSeconds);

            seWalk?.PlayCutscenePodSecondWalkStartSe();
            yield return RunUnitMove(firstStop, secondStop, secondWalkSeconds, false);
            yield return WaitUnscaled(walkPauseSeconds);
            yield return RunUnitDashToSlot(secondStop, targetSlot, unitMoveSeconds);
        }

        SetImageEnabled(activeSeq.podUnitImage, false);
        SetImageEnabled(activeSeq.podUnitShadowImage, false);
    }

    /// <summary>
    /// StageStart 3歩行（前・右・左）の 1 歩あたりの移動量。従来 1 歩目の |firstWalkOffsetY| に合わせる。
    /// </summary>
    private float ResolveStageStartWalkStepDistance()
    {
        float d = Mathf.Abs(firstWalkOffsetY);
        if (d > 0.0001f)
        {
            return d;
        }

        d = Mathf.Abs(secondWalkOffsetY - firstWalkOffsetY);
        if (d > 0.0001f)
        {
            return d;
        }

        return 26f;
    }

    private IEnumerator WaitUnitAppearDelayWithPodC1FadeIn()
    {
        float total = Mathf.Max(0f, unitAppearDelaySeconds);
        if (total <= 0f)
        {
            SetImageAlpha(activeSeq.podC1Image, 1f);
            Game03SeManager seC1 = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();
            seC1?.PlayCutscenePodC1ReachFullAlphaSe();
            yield break;
        }

        EnsureImageObjectActive(activeSeq.podC1Image);
        SetImageEnabled(activeSeq.podC1Image, true);
        float startAlpha = activeSeq.podC1Image != null ? activeSeq.podC1Image.color.a : 0f;
        float fadeDuration = Mathf.Max(0.0001f, total * 0.8f);
        float timer = 0f;
        while (timer < total)
        {
            timer += GetSequenceDeltaTime();
            float p = Mathf.Clamp01(timer / fadeDuration);
            SetImageAlpha(activeSeq.podC1Image, Mathf.Lerp(startAlpha, 1f, p));
            SyncPodC1ToUnit();
            yield return null;
        }

        SetImageAlpha(activeSeq.podC1Image, 1f);
        SyncPodC1ToUnit();

        Game03SeManager seC1Done = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();
        seC1Done?.PlayCutscenePodC1ReachFullAlphaSe();
    }

    private IEnumerator RunSmokeToUnitReveal()
    {
        SetImageEnabled(activeSeq.podUnitImage, true);
        SetImageEnabled(activeSeq.podUnitShadowImage, true);
        SetImageAlpha(activeSeq.podUnitImage, 0f);
        SetImageAlpha(activeSeq.podUnitShadowImage, 0f);
        EnsureImageObjectActive(activeSeq.podC1Image);
        SetImageEnabled(activeSeq.podC1Image, true);

        float t = 0f;
        float duration = Mathf.Max(0.05f, revealFadeSeconds);
        while (t < duration)
        {
            t += GetSequenceDeltaTime();
            float p = Mathf.Clamp01(t / duration);
            SetImageAlpha(activeSeq.podUnitImage, p);
            SetImageAlpha(activeSeq.podUnitShadowImage, p * Mathf.Clamp01(podUnitShadowMaxAlpha));
            SetImageAlpha(activeSeq.podC1Image, 1f);
            SyncPodC1ToUnit();
            yield return null;
        }

        SetImageAlpha(activeSeq.podUnitImage, 1f);
        SetImageAlpha(activeSeq.podUnitShadowImage, Mathf.Clamp01(podUnitShadowMaxAlpha));
        SetImageAlpha(activeSeq.podC1Image, 1f);
        KeepCameraLockedZoom();
    }

    private IEnumerator RunUnitMove(Vector2 start, Vector2 end, float seconds, bool useDashEasing, bool fadeOutPodC1 = false)
    {
        Vector2 shadowStart = activeSeq.podUnitShadowRect != null ? activeSeq.podUnitShadowRect.anchoredPosition : start;
        Vector2 shadowEnd = end;
        bool shouldMoveShadowRectDirectly =
            activeSeq.podUnitShadowRect != null
            && (activeSeq.podUnitRect == null || !activeSeq.podUnitShadowRect.IsChildOf(activeSeq.podUnitRect));
        float smokeStartAlpha = activeSeq.podC1Image != null ? activeSeq.podC1Image.color.a : 0f;
        float t = 0f;
        float duration = Mathf.Max(0.01f, seconds);
        while (t < duration)
        {
            t += GetSequenceDeltaTime();
            float p = Mathf.Clamp01(t / duration);
            float eased = useDashEasing ? (1f - Mathf.Pow(1f - p, 3f)) : p;
            activeSeq.podUnitRect.anchoredPosition = Vector2.Lerp(start, end, eased);
            if (shouldMoveShadowRectDirectly)
            {
                activeSeq.podUnitShadowRect.anchoredPosition = Vector2.Lerp(shadowStart, shadowEnd, eased);
            }

            if (fadeOutPodC1 && activeSeq.podC1Image != null)
            {
                SetImageAlpha(activeSeq.podC1Image, Mathf.Lerp(smokeStartAlpha, 0f, p));
                SyncPodC1ToUnit();
            }

            if (useDashEasing)
            {
                RestoreCameraByProgress(p);
            }
            else
            {
                KeepCameraLockedZoom();
            }
            yield return null;
        }

        activeSeq.podUnitRect.anchoredPosition = end;
        if (shouldMoveShadowRectDirectly)
        {
            activeSeq.podUnitShadowRect.anchoredPosition = shadowEnd;
        }

        if (fadeOutPodC1 && activeSeq.podC1Image != null)
        {
            SetImageAlpha(activeSeq.podC1Image, 0f);
            SetImageEnabled(activeSeq.podC1Image, false);
        }

    }

    private IEnumerator RunUnitDashToSlot(Vector2 start, RectTransform targetSlot, float seconds)
    {
        Vector2 shadowStart = activeSeq.podUnitShadowRect != null ? activeSeq.podUnitShadowRect.anchoredPosition : start;
        bool shouldMoveShadowRectDirectly =
            activeSeq.podUnitShadowRect != null
            && (activeSeq.podUnitRect == null || !activeSeq.podUnitShadowRect.IsChildOf(activeSeq.podUnitRect));
        float t = 0f;
        float duration = Mathf.Max(0.01f, seconds);
        while (t < duration)
        {
            t += GetSequenceDeltaTime();
            float p = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            Vector2 dynamicEnd = ConvertPointBetweenRects(targetSlot, activeSeq.panelRoot, Vector2.zero);
            activeSeq.podUnitRect.anchoredPosition = Vector2.Lerp(start, dynamicEnd, eased);
            if (shouldMoveShadowRectDirectly)
            {
                activeSeq.podUnitShadowRect.anchoredPosition = Vector2.Lerp(shadowStart, dynamicEnd, eased);
            }

            RestoreCameraByProgress(p);
            yield return null;
        }

        Vector2 finalEnd = ConvertPointBetweenRects(targetSlot, activeSeq.panelRoot, Vector2.zero);
        activeSeq.podUnitRect.anchoredPosition = finalEnd;
        if (shouldMoveShadowRectDirectly)
        {
            activeSeq.podUnitShadowRect.anchoredPosition = finalEnd;
        }
    }

    private void UpdatePodC2Visual(float progress, float elapsed)
    {
        if (activeSeq.podC2Image == null)
        {
            return;
        }

        RectTransform rt = activeSeq.podC2Image.rectTransform;
        Vector2 basePos = hasPodC2BaseAnchoredPosition ? podC2BaseAnchoredPosition : rt.anchoredPosition;
        float sway = Mathf.Sin(elapsed * c2SwayFrequency) * c2SwayAmplitude;
        rt.anchoredPosition = new Vector2(basePos.x + sway, basePos.y);
    }

    private bool TryResolvePodSprites(int podType, out Sprite podSprite, out Sprite podUnitSprite)
    {
        podSprite = null;
        podUnitSprite = null;
        if (fallPodWaitOj == null)
        {
            return false;
        }

        Transform podRoot = fallPodWaitOj.Find($"Pod0{Mathf.Clamp(podType, 1, 7)}");
        if (podRoot == null)
        {
            return false;
        }

        Image podImg = podRoot.GetComponent<Image>();
        Transform unitTf = podRoot.Find("PodUnit");
        Image unitImg = unitTf != null ? unitTf.GetComponent<Image>() : null;
        if (podImg == null || unitImg == null || podImg.sprite == null || unitImg.sprite == null)
        {
            return false;
        }

        podSprite = podImg.sprite;
        podUnitSprite = unitImg.sprite;
        return true;
    }

    private bool TryGetFirstEmptyUnitSlot(out RectTransform slot, out int slotIndex)
    {
        slot = null;
        slotIndex = -1;
        RectTransform[] slots = { unit01Oj, unit02Oj, unit03Oj, unit04Oj };
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                continue;
            }

            if (!slots[i].gameObject.activeSelf)
            {
                slot = slots[i];
                slotIndex = i + 1;
                return true;
            }
        }

        return false;
    }

    private static Vector2 ConvertPointBetweenRects(RectTransform from, RectTransform to, Vector2 localPoint)
    {
        Vector3 world = from.TransformPoint(localPoint);
        return to.InverseTransformPoint(world);
    }

    private static void ApplyUnitFrontSprite(RectTransform unitSlot, Sprite sprite)
    {
        if (unitSlot == null || sprite == null)
        {
            return;
        }

        Transform front = unitSlot.Find("MainUnitFrontImg");
        if (front == null)
        {
            return;
        }

        Image image = front.GetComponent<Image>();
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
    }

    private static void SetImageEnabled(Image image, bool enabled)
    {
        if (image != null)
        {
            image.enabled = enabled;
        }
    }

    private static void EnsureImageObjectActive(Image image)
    {
        if (image != null && image.gameObject != null && !image.gameObject.activeSelf)
        {
            image.gameObject.SetActive(true);
        }
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color c = image.color;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
    }

    private void SyncPodC1ToUnit()
    {
        if (activeSeq.podC1Image == null || activeSeq.podUnitRect == null)
        {
            return;
        }

        RectTransform c1Rect = activeSeq.podC1Image.rectTransform;
        if (activeSeq.podUnitRect != null && c1Rect.IsChildOf(activeSeq.podUnitRect))
        {
            // Child visuals keep their inspector local offset.
            return;
        }

        c1Rect.anchoredPosition = activeSeq.podUnitRect.anchoredPosition;
    }

    private IEnumerator WaitUnscaled(float seconds)
    {
        float timer = 0f;
        while (timer < seconds)
        {
            timer += GetSequenceDeltaTime();
            yield return null;
        }
    }

    private float GetSequenceDeltaTime()
    {
        UpdateSkipRequestFromInput();
        if (skipSequenceRequested)
        {
            TryApplySkipEndVisualState();
            SetSpeedViewIconVisible(false);
            return 999f;
        }

        bool isFastForwardPressed = IsFastForwardInputPressed();
        SetSpeedViewIconVisible(isFastForwardPressed);
        float dt = Time.unscaledDeltaTime;
        float multiplier = isFastForwardPressed ? Mathf.Max(1f, sequenceFastForwardMultiplier) : 1f;
        return dt * multiplier;
    }

    private void UpdateSkipRequestFromInput()
    {
        if (skipSequenceRequested)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            skipSequenceRequested = true;
            return;
        }

        SkipTapInput tap = GetSkipTapPressedThisFrame();
        if (tap == SkipTapInput.None)
        {
            return;
        }

        float now = Time.unscaledTime;
        float interval = Mathf.Max(0.05f, skipDoubleTapIntervalSeconds);
        if (tap == lastSkipTapInput && now - lastSkipTapTime <= interval)
        {
            skipSequenceRequested = true;
        }

        lastSkipTapInput = tap;
        lastSkipTapTime = now;
    }

    private static SkipTapInput GetSkipTapPressedThisFrame()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return SkipTapInput.MouseLeft;
        }

        if (Keyboard.current == null)
        {
            return SkipTapInput.None;
        }

        if (Keyboard.current.wKey.wasPressedThisFrame) return SkipTapInput.W;
        if (Keyboard.current.aKey.wasPressedThisFrame) return SkipTapInput.A;
        if (Keyboard.current.sKey.wasPressedThisFrame) return SkipTapInput.S;
        if (Keyboard.current.dKey.wasPressedThisFrame) return SkipTapInput.D;
        if (Keyboard.current.spaceKey.wasPressedThisFrame) return SkipTapInput.Space;
        return SkipTapInput.None;
    }

    private IEnumerator RunSkipReturnTransition()
    {
        float duration = Mathf.Max(0.01f, skipReturnTransitionSeconds);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            RestoreCameraByProgress(p);
            yield return null;
        }

        RestoreCameraByProgress(1f);
    }

    private static bool IsFastForwardInputPressed()
    {
        bool keyboardPressed = false;
        if (Keyboard.current != null)
        {
            keyboardPressed = Keyboard.current.wKey.isPressed
                              || Keyboard.current.aKey.isPressed
                              || Keyboard.current.sKey.isPressed
                              || Keyboard.current.dKey.isPressed
                              || Keyboard.current.spaceKey.isPressed;
        }

        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
        return keyboardPressed || mousePressed;
    }

    private void CacheCameraState()
    {
        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main;
        }

        if (gameplayCamera == null || !gameplayCamera.orthographic)
        {
            hasCameraCache = false;
            return;
        }

        hasCameraCache = true;
        cachedCameraPosition = gameplayCamera.transform.position;
        cachedCameraOrthoSize = gameplayCamera.orthographicSize;
        lockedZoomCameraPosition = cachedCameraPosition;
        lockedZoomCameraOrthoSize = cachedCameraOrthoSize;
    }

    private void UpdateCameraDuringFall(float progress)
    {
        if (!hasCameraCache || gameplayCamera == null)
        {
            return;
        }

        if (!enableCameraZoom)
        {
            gameplayCamera.orthographicSize = cachedCameraOrthoSize;
            gameplayCamera.transform.position = cachedCameraPosition;
            SyncPodVisualScaleWithCameraZoom(cachedCameraOrthoSize);
            return;
        }

        float p = Mathf.Clamp01(progress);
        gameplayCamera.orthographicSize = Mathf.Lerp(cachedCameraOrthoSize, zoomedOrthographicSize, p);
        gameplayCamera.transform.position = cachedCameraPosition;
        SyncPodVisualScaleWithCameraZoom(gameplayCamera.orthographicSize);
        ApplyPseudoZoomPanelOffset(p);
    }

    private void LockCurrentCameraStateAsZoom()
    {
        if (!hasCameraCache || gameplayCamera == null)
        {
            return;
        }

        lockedZoomCameraPosition = cachedCameraPosition;
        lockedZoomCameraOrthoSize = enableCameraZoom ? gameplayCamera.orthographicSize : cachedCameraOrthoSize;
    }

    private void KeepCameraLockedZoom()
    {
        if (!hasCameraCache || gameplayCamera == null)
        {
            return;
        }

        if (!enableCameraZoom)
        {
            gameplayCamera.transform.position = cachedCameraPosition;
            gameplayCamera.orthographicSize = cachedCameraOrthoSize;
            SyncPodVisualScaleWithCameraZoom(cachedCameraOrthoSize);
            return;
        }

        gameplayCamera.transform.position = lockedZoomCameraPosition;
        gameplayCamera.orthographicSize = lockedZoomCameraOrthoSize;
        SyncPodVisualScaleWithCameraZoom(gameplayCamera.orthographicSize);
        ApplyPseudoZoomPanelOffset(1f);
    }

    private void RestoreCameraByProgress(float progress)
    {
        if (!hasCameraCache || gameplayCamera == null)
        {
            return;
        }

        if (!enableCameraZoom)
        {
            gameplayCamera.transform.position = cachedCameraPosition;
            gameplayCamera.orthographicSize = cachedCameraOrthoSize;
            SyncPodVisualScaleWithCameraZoom(cachedCameraOrthoSize);
            return;
        }

        float p = Mathf.Clamp01(progress);
        gameplayCamera.transform.position = cachedCameraPosition;
        gameplayCamera.orthographicSize = Mathf.Lerp(lockedZoomCameraOrthoSize, cachedCameraOrthoSize, p);
        SyncPodVisualScaleWithCameraZoom(gameplayCamera.orthographicSize);
        ApplyPseudoZoomPanelOffset(1f - p);
    }

    private void RestoreCameraImmediate()
    {
        if (!hasCameraCache || gameplayCamera == null)
        {
            return;
        }

        gameplayCamera.transform.position = cachedCameraPosition;
        gameplayCamera.orthographicSize = cachedCameraOrthoSize;
        RectTransform zoomPanel = cachedZoomPanelRoot != null ? cachedZoomPanelRoot : fallPodPanel;
        if (zoomPanel != null)
        {
            zoomPanel.localScale = cachedFallPodPanelScale;
            if (hasFallPodPanelAnchoredPositionCache)
            {
                zoomPanel.anchoredPosition = cachedFallPodPanelAnchoredPosition;
            }
        }
        ApplyUnitSlotPseudoZoomTransform(1f);
        hasCameraCache = false;
    }

    private void SyncPodVisualScaleWithCameraZoom(float currentOrthoSize)
    {
        RectTransform zoomPanel = cachedZoomPanelRoot != null ? cachedZoomPanelRoot : fallPodPanel;
        if (zoomPanel == null || !hasFallPodPanelScaleCache)
        {
            return;
        }

        if (!enableCameraZoom)
        {
            zoomPanel.localScale = cachedFallPodPanelScale;
            ApplyUnitSlotPseudoZoomTransform(1f);
            return;
        }

        float safeCurrent = Mathf.Max(0.001f, currentOrthoSize);
        float safeBase = Mathf.Max(0.001f, cachedCameraOrthoSize);
        float scale = safeBase / safeCurrent;
        zoomPanel.localScale = cachedFallPodPanelScale * scale;
        ApplyUnitSlotPseudoZoomTransform(scale);
    }

    private void ApplyPseudoZoomPanelOffset(float weight)
    {
        RectTransform zoomPanel = cachedZoomPanelRoot != null ? cachedZoomPanelRoot : fallPodPanel;
        if (zoomPanel == null || !hasFallPodPanelAnchoredPositionCache)
        {
            return;
        }

        float baseScaleX = Mathf.Abs(cachedFallPodPanelScale.x) > 0.0001f ? cachedFallPodPanelScale.x : 1f;
        float currentScaleX = zoomPanel.localScale.x / baseScaleX;
        float pivotX = activeSequencePodGroundPivotX;
        // User requested: subtract PodGroundOj initial X as zoom offset basis.
        zoomPanel.anchoredPosition = cachedFallPodPanelAnchoredPosition + new Vector2(
            pivotX * (1f - currentScaleX),
            0f);
    }

    private void CacheUnitSlotScaleIfNeeded()
    {
        if (hasUnitSlotScaleCache)
        {
            return;
        }

        if (mainUnitOj == null && game03UnitManager != null)
        {
            mainUnitOj = game03UnitManager.MainUnitRect;
        }

        RectTransform[] slots = { mainUnitOj, unit01Oj, unit02Oj, unit03Oj, unit04Oj };
        for (int i = 0; i < slots.Length; i++)
        {
            cachedUnitSlotRects[i] = slots[i];
            cachedUnitSlotScales[i] = slots[i] != null ? slots[i].localScale : Vector3.one;
        }

        hasUnitSlotScaleCache = true;
    }

    private void ApplyUnitSlotPseudoZoomTransform(float scale)
    {
        CacheUnitSlotScaleIfNeeded();
        float s = Mathf.Max(0.0001f, scale);
        for (int i = 0; i < cachedUnitSlotRects.Length; i++)
        {
            RectTransform slot = cachedUnitSlotRects[i];
            if (slot == null)
            {
                continue;
            }

            float slotScaleMultiplier = (mainUnitOj != null && slot == mainUnitOj)
                ? Mathf.Max(0f, mainUnitZoomExtraScale)
                : 1f;
            slot.localScale = cachedUnitSlotScales[i] * (s * slotScaleMultiplier);
        }
    }

    private void CleanupSequenceState()
    {
        SetSpeedViewIconVisible(false);
        isSequenceRunning = false;
        cachedZoomPanelRoot = null;
        skipEndVisualApplied = false;
        activeSkipTargetSlot = null;
        hasCachedSkipTargetPanelPosition = false;
        RestoreActiveSequencePodGroundDesignAnchored();
    }

    private void BeginSequenceSkipCache(RectTransform targetSlot)
    {
        activeSkipTargetSlot = targetSlot;
        skipEndVisualApplied = false;
        hasCachedSkipTargetPanelPosition = false;

        if (targetSlot != null && activeSeq.panelRoot != null)
        {
            cachedSkipTargetPanelPosition = ConvertPointBetweenRects(targetSlot, activeSeq.panelRoot, Vector2.zero);
            hasCachedSkipTargetPanelPosition = true;
        }
    }

    private void TryApplySkipEndVisualState()
    {
        if (skipEndVisualApplied || activeSeq.panelRoot == null || !activeSeq.IsValidForSequence())
        {
            return;
        }

        skipEndVisualApplied = true;

        float centerX = ResolvePodGroundCenterX();
        float groundLandingY = GetAdjustedGroundY();
        float podLandingY = ResolvePodLandingCenterY();

        activeSeq.fallPodOj.anchoredPosition = new Vector2(centerX, podLandingY);
        activeSeq.podShadowRect.localScale = shadowEndScale;
        ApplyLandingAnchorPositions(centerX, groundLandingY);

        SetImageEnabled(activeSeq.podFireImage, false);
        SetImageEnabled(activeSeq.podC2Image, false);
        SetImageEnabled(activeSeq.podGroundImage, true);

        EnsureImageObjectActive(activeSeq.podC1Image);
        SetImageEnabled(activeSeq.podC1Image, true);
        SetImageAlpha(activeSeq.podC1Image, 1f);
        SyncPodC1ToUnit();

        SetImageEnabled(activeSeq.podUnitImage, true);
        SetImageEnabled(activeSeq.podUnitShadowImage, true);
        SetImageAlpha(activeSeq.podUnitImage, 1f);
        SetImageAlpha(activeSeq.podUnitShadowImage, Mathf.Clamp01(podUnitShadowMaxAlpha));

        if (!hasCachedSkipTargetPanelPosition
            && activeSkipTargetSlot != null
            && activeSeq.panelRoot != null)
        {
            cachedSkipTargetPanelPosition = ConvertPointBetweenRects(activeSkipTargetSlot, activeSeq.panelRoot, Vector2.zero);
            hasCachedSkipTargetPanelPosition = true;
        }

        if (hasCachedSkipTargetPanelPosition && activeSeq.podUnitRect != null)
        {
            activeSeq.podUnitRect.anchoredPosition = cachedSkipTargetPanelPosition;
            if (activeSeq.podUnitShadowRect != null
                && (activeSeq.podUnitRect == null || !activeSeq.podUnitShadowRect.IsChildOf(activeSeq.podUnitRect)))
            {
                activeSeq.podUnitShadowRect.anchoredPosition = cachedSkipTargetPanelPosition;
            }
        }

        if (hasCameraCache && gameplayCamera != null)
        {
            if (enableCameraZoom)
            {
                gameplayCamera.orthographicSize = zoomedOrthographicSize;
                gameplayCamera.transform.position = cachedCameraPosition;
                SyncPodVisualScaleWithCameraZoom(gameplayCamera.orthographicSize);
                ApplyPseudoZoomPanelOffset(1f);
            }

            LockCurrentCameraStateAsZoom();
        }
    }

    private void SetSpeedViewIconVisible(bool visible)
    {
        if (speedViewIcon == null)
        {
            return;
        }

        if (speedViewIcon.activeSelf != visible)
        {
            speedViewIcon.SetActive(visible);
        }
    }

    private void CreatePodRemnantInItemCanvas()
    {
        if (itemPodCanvasRoot == null)
        {
            return;
        }

        ClearPodRemnant();

        GameObject rootObject = new GameObject("PodRemnantRuntime", typeof(RectTransform));
        rootObject.layer = itemPodCanvasRoot.gameObject.layer;
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(itemPodCanvasRoot, false);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;
        root.anchoredPosition = Vector2.zero;
        root.localScale = Vector3.one;

        CloneImageToCanvas(
            activeSeq.podShadowRect != null ? activeSeq.podShadowRect.GetComponent<Image>() : null,
            root,
            "PodShadow");
        CloneImageToCanvas(activeSeq.podGroundImage, root, "PodGround");
        CloneImageToCanvas(activeSeq.podImage, root, "Pod");

        podRemnantRoot = root;
        podRemnantWatchCoroutine = StartCoroutine(WatchAndRemovePodRemnant());
    }

    private void CloneImageToCanvas(Image source, RectTransform targetParent, string name)
    {
        if (source == null || targetParent == null || source.sprite == null || !source.enabled)
        {
            return;
        }

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = targetParent.gameObject.layer;
        RectTransform dstRect = go.GetComponent<RectTransform>();
        dstRect.SetParent(targetParent, false);

        RectTransform srcRect = source.rectTransform;
        dstRect.anchorMin = srcRect.anchorMin;
        dstRect.anchorMax = srcRect.anchorMax;
        dstRect.pivot = srcRect.pivot;
        dstRect.sizeDelta = srcRect.sizeDelta;
        dstRect.localScale = srcRect.localScale;
        dstRect.rotation = srcRect.rotation;
        dstRect.position = srcRect.position;

        Image dstImage = go.GetComponent<Image>();
        dstImage.sprite = source.sprite;
        dstImage.color = source.color;
        dstImage.material = source.material;
        dstImage.type = source.type;
        dstImage.preserveAspect = source.preserveAspect;
        dstImage.raycastTarget = false;
    }

    private IEnumerator WatchAndRemovePodRemnant()
    {
        float interval = Mathf.Max(0.05f, podRemnantCheckIntervalSeconds);
        float checkTimer = 0f;
        while (podRemnantRoot != null)
        {
            UpdatePodRemnantFollowBackground();
            checkTimer += Time.unscaledDeltaTime;
            if (checkTimer >= interval && ShouldRemovePodRemnant())
            {
                ClearPodRemnant();
                yield break;
            }

            if (checkTimer >= interval)
            {
                checkTimer = 0f;
            }

            yield return null;
        }
    }

    private bool ShouldRemovePodRemnant()
    {
        if (podRemnantRoot == null)
        {
            return false;
        }

        if (mainUnitOj == null && game03UnitManager != null)
        {
            mainUnitOj = game03UnitManager.MainUnitRect;
        }

        if (mainUnitOj == null)
        {
            return false;
        }

        Vector2 remnantScreen = RectTransformUtility.WorldToScreenPoint(null, podRemnantRoot.position);
        Vector2 mainScreen = RectTransformUtility.WorldToScreenPoint(null, mainUnitOj.position);
        float distance = Vector2.Distance(remnantScreen, mainScreen);
        float threshold = Mathf.Max(Screen.width, Screen.height) * Mathf.Max(0.1f, podRemnantRemoveDistanceScreens);
        return distance >= threshold;
    }

    private void ClearPodRemnant()
    {
        if (podRemnantWatchCoroutine != null)
        {
            StopCoroutine(podRemnantWatchCoroutine);
            podRemnantWatchCoroutine = null;
        }

        if (podRemnantRoot != null)
        {
            Destroy(podRemnantRoot.gameObject);
            podRemnantRoot = null;
        }
    }

    private void UpdatePodRemnantFollowBackground()
    {
        if (podRemnantRoot == null || itemPodCanvasRoot == null || game03UnitManager == null)
        {
            return;
        }

        Vector2 worldDelta = game03UnitManager.LastAppliedFieldDeltaWorld;
        if (worldDelta.sqrMagnitude <= 0.0000001f)
        {
            return;
        }

        if (game03UnitManager.TryConvertWorldDeltaToLocalOnRect(itemPodCanvasRoot, worldDelta, out Vector2 localDelta))
        {
            podRemnantRoot.anchoredPosition += localDelta;
        }
    }

    private float GetAdjustedGroundY()
    {
        float baseY = ResolvePodGroundBaseY();
        if (game03UnitManager == null)
        {
            return baseY;
        }

        float minY = game03UnitManager.MinFieldRootY;
        float maxY = game03UnitManager.MaxFieldRootY;
        float range = maxY - minY;
        if (range <= 0.0001f)
        {
            return baseY;
        }

        float normalized = Mathf.InverseLerp(minY, maxY, game03UnitManager.CurrentFieldRootY);
        if (normalized >= 0.8f)
        {
            return baseY + landingShiftByUnitY;
        }

        if (normalized <= 0.2f)
        {
            return baseY - landingShiftByUnitY;
        }

        return baseY;
    }

    private float ResolvePodGroundCenterX()
    {
        if (activeSeq.podGroundOj != null)
        {
            return activeSeq.podGroundOj.anchoredPosition.x;
        }

        if (activeSeq.podShadowRect != null)
        {
            return activeSeq.podShadowRect.anchoredPosition.x;
        }

        return 0f;
    }

    private float ResolvePodGroundCenterY()
    {
        if (activeSeq.podGroundOj != null)
        {
            return activeSeq.podGroundOj.anchoredPosition.y;
        }

        if (activeSeq.podShadowRect != null)
        {
            return activeSeq.podShadowRect.anchoredPosition.y;
        }

        return landingY;
    }

    private float ResolvePodLandingCenterY()
    {
        return GetAdjustedGroundY() + podLandingYOffset;
    }

    private void ApplyLandingAnchorPositions(float centerX, float groundLandingY)
    {
        if (activeSeq.podGroundOj != null)
        {
            activeSeq.podGroundOj.anchoredPosition = new Vector2(centerX, groundLandingY);
        }

        if (activeSeq.podUnitRect != null)
        {
            // PodUnitOj follows cliff-adjusted ground landing position.
            activeSeq.podUnitRect.anchoredPosition = new Vector2(centerX, groundLandingY);
        }

        if (activeSeq.podUnitShadowRect != null)
        {
            if (activeSeq.podUnitRect == null || !activeSeq.podUnitShadowRect.IsChildOf(activeSeq.podUnitRect))
            {
                activeSeq.podUnitShadowRect.anchoredPosition = new Vector2(centerX, groundLandingY);
            }
        }
    }

    private static void CachePodGroundDesignAnchored(
        RectTransform podGround,
        ref Vector2 designAnchored,
        ref bool hasDesignAnchored)
    {
        if (podGround == null)
        {
            hasDesignAnchored = false;
            return;
        }

        designAnchored = podGround.anchoredPosition;
        hasDesignAnchored = true;
    }

    private bool TryGetActivePodGroundDesignAnchored(out Vector2 designAnchored)
    {
        if (activeSeq.podGroundOj != null
            && podGroundOj != null
            && activeSeq.podGroundOj == podGroundOj
            && hasRecruitmentPodGroundDesignAnchored)
        {
            designAnchored = recruitmentPodGroundDesignAnchored;
            return true;
        }

        if (activeSeq.podGroundOj != null
            && stageStartVisuals.podGroundOj != null
            && activeSeq.podGroundOj == stageStartVisuals.podGroundOj
            && hasStageStartPodGroundDesignAnchored)
        {
            designAnchored = stageStartPodGroundDesignAnchored;
            return true;
        }

        designAnchored = default;
        return false;
    }

    private void RestoreActiveSequencePodGroundDesignAnchored()
    {
        if (!TryGetActivePodGroundDesignAnchored(out Vector2 designAnchored) || activeSeq.podGroundOj == null)
        {
            return;
        }

        activeSeq.podGroundOj.anchoredPosition = designAnchored;
    }

    private float ResolvePodGroundBaseY()
    {
        if (TryGetActivePodGroundDesignAnchored(out Vector2 designAnchored))
        {
            return designAnchored.y;
        }

        return landingY;
    }

    private void CacheAndHideGameplayCanvases()
    {
        wasEnemyCanvasActive = enemyCanvasRoot != null && enemyCanvasRoot.activeSelf;
        wasItemCanvasActive = itemCanvasRoot != null && itemCanvasRoot.activeSelf;
        wasWeaponCanvasActive = weaponCanvasRoot != null && weaponCanvasRoot.activeSelf;
        CacheAndHidePanelCanvasChildrenExceptKeepVisible(activeSeq.panelRoot != null ? activeSeq.panelRoot.gameObject : null);

        if (enemyCanvasRoot != null)
        {
            enemyCanvasRoot.SetActive(false);
        }

        if (itemCanvasRoot != null)
        {
            itemCanvasRoot.SetActive(false);
        }

        if (weaponCanvasRoot != null)
        {
            weaponCanvasRoot.SetActive(false);
        }
    }

    private void RestoreGameplayCanvases()
    {
        if (enemyCanvasRoot != null)
        {
            enemyCanvasRoot.SetActive(wasEnemyCanvasActive);
        }

        if (itemCanvasRoot != null)
        {
            itemCanvasRoot.SetActive(wasItemCanvasActive);
        }

        if (weaponCanvasRoot != null)
        {
            weaponCanvasRoot.SetActive(wasWeaponCanvasActive);
        }

        RestorePanelCanvasChildren();
    }

    private void CacheAndHidePanelCanvasChildrenExceptKeepVisible(GameObject keepVisibleOverride)
    {
        hiddenPanelCanvasChildren.Clear();
        if (panelOjRoot == null)
        {
            return;
        }

        GameObject keepVisible = keepVisibleOverride;
        for (int i = 0; i < panelOjRoot.childCount; i++)
        {
            Transform child = panelOjRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            GameObject go = child.gameObject;
            if (go == keepVisible || !go.activeSelf)
            {
                continue;
            }

            hiddenPanelCanvasChildren.Add(go);
            go.SetActive(false);
        }
    }

    private void RestorePanelCanvasChildren()
    {
        for (int i = 0; i < hiddenPanelCanvasChildren.Count; i++)
        {
            GameObject go = hiddenPanelCanvasChildren[i];
            if (go != null)
            {
                go.SetActive(true);
            }
        }

        hiddenPanelCanvasChildren.Clear();
    }

    private void ApplyMainUnitFrontSiblingDuringSequence()
    {
        if (mainUnitOj == null && game03UnitManager != null)
        {
            mainUnitOj = game03UnitManager.MainUnitRect;
        }

        if (mainUnitOj == null || mainUnitOj.parent == null)
        {
            return;
        }

        if (!hasCachedMainUnitSiblingIndex)
        {
            cachedMainUnitSiblingIndex = mainUnitOj.GetSiblingIndex();
            hasCachedMainUnitSiblingIndex = true;
        }

        mainUnitOj.SetAsLastSibling();
    }

    private void RestoreMainUnitSiblingOrder()
    {
        if (!hasCachedMainUnitSiblingIndex || mainUnitOj == null || mainUnitOj.parent == null)
        {
            return;
        }

        int maxIndex = Mathf.Max(0, mainUnitOj.parent.childCount - 1);
        int restoredIndex = Mathf.Clamp(cachedMainUnitSiblingIndex, 0, maxIndex);
        mainUnitOj.SetSiblingIndex(restoredIndex);
        hasCachedMainUnitSiblingIndex = false;
        cachedMainUnitSiblingIndex = -1;
    }
}
