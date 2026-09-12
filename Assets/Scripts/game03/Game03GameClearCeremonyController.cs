using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// game03: タイムクリア時の <c>GameClearedPanel</c> 演出（レーザー → 船 → 着陸 → 乗車 → ユニット退場 → 発射 SE と同タイミングで船が左上へ → 入力待ちまで）。
/// 仕様: <c>spec/game03/ゲームクリア演出.txt</c>
/// 早送り・遷移入力は <c>Game03PodManager</c>（FallPodPanel 降下）と同系統。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03GameClearCeremonyController : MonoBehaviour
{
    private enum CeremonyFastTapInput
    {
        None,
        MouseLeft,
        W,
        A,
        S,
        D,
        Space
    }

    [SerializeField] private GameObject gameClearedPanelRoot;
    [SerializeField] private Game03WeaponManager weaponManager;
    [SerializeField] private Game03BgmManager bgmManager;
    [SerializeField] private Game03SeManager seManager;
    [SerializeField, Tooltip("クリア後 dialogue_scene へ。ResultPanel 未接続時のフォールバック。")]
    private Game03TransitionManager game03TransitionManager;
    [SerializeField, Tooltip("GameClearedPanel 確定後に ResultPanel を開く。未設定なら Find。")]
    private Game03GameOverPresentation runEndPresentation;

    [SerializeField] private RectTransform imageLaser1;
    [SerializeField] private RectTransform imageLaser2;
    [SerializeField] private RectTransform imageLaser3;

    [SerializeField] private RectTransform imageShip01;
    [SerializeField] private RectTransform imageShip02;
    [SerializeField] private RectTransform imageShip03;
    [SerializeField] private RectTransform imageShip04;
    [SerializeField] private RectTransform imageShip05;
    [SerializeField] private RectTransform imageShip06;
    [SerializeField] private RectTransform imageShip07;

    [SerializeField] private GameObject enemyCanvasRoot;
    [SerializeField] private GameObject weaponCanvasRoot;
    [SerializeField] private GameObject itemCanvasRoot;
    [SerializeField] private GameObject itemNaviRoot;
    [SerializeField] private GameObject panelOjRoot;

    [Header("UnitOj（乗車演出）")]
    [SerializeField] private RectTransform mainUnitOjRect;
    [SerializeField] private RectTransform unit01OjRect;
    [SerializeField] private RectTransform unit02OjRect;
    [SerializeField] private RectTransform unit03OjRect;
    [SerializeField] private RectTransform unit04OjRect;

    [Header("テキスト（発射以降）")]
    [SerializeField] private TextMeshProUGUI gameClearedTextMesh;
    [SerializeField] private TextMeshProUGUI returnTextMesh;

    [Header("タイミング（秒・unscaled）")]
    [SerializeField, Min(0f)] private float laserIntroWaitSeconds = 1f;
    [SerializeField, Min(0.01f)] private float laserStrikeDurationSeconds = 0.4f;
    [SerializeField, Min(0f)] private float laserBetweenSeconds = 1.2f;
    [SerializeField, Min(0.01f)] private float shipCrossDurationSeconds = 0.8f;
    [SerializeField, Min(0f)] private float shipLandingWaitSeconds = 1f;
    [SerializeField, Min(0.01f)] private float shipLandingMoveDurationSeconds = 3f;
    [SerializeField, Min(0f)] private float shipBoardingWaitSeconds = 1f;
    [SerializeField, Min(0.01f)] private float unitDropDurationSeconds = 0.45f;
    [SerializeField, Min(0f)] private float unitBoardingDropPixels = 220f;
    [SerializeField, Min(0.01f)] private float unitTogetherLayoutDurationSeconds = 0.55f;
    [SerializeField, Min(0.01f)] private float unitBoardingSlideSpeedPixelsPerSecond = 900f;
    [SerializeField, Tooltip("乗車後の第1スライド: メイン／サブ複製の anchoredPosition.x がこの値以下になるまで、複製だけを左へ動かす（宇宙船は動かさない）。")]
    private float unitBoardingExitAnchoredX = -2200f;
    [SerializeField, Tooltip("ユニット複製をさらに左へ滑らせ、この anchoredPosition.x より左へ出たらクローンを破棄し、原物は非表示のままにする。")]
    private float unitBoardHideAnchoredX = -3600f;
    [SerializeField, Min(0f)] private float shipLaunchWaitSeconds = 1f;
    [SerializeField, Min(0f), Tooltip("ユニット複製が消えたあと、宇宙船が左上（横切り目標）へ戻るアニメの長さ（秒）。0 のときは shipCrossDurationSeconds と同じ。")]
    private float shipReturnAfterUnitsHideDurationSeconds;
    [SerializeField, Min(0f)] private float returnTextBlinkPeriodSeconds = 3f;

    [Header("早送り・遷移入力（FallPod / Game03PodManager と同系統）")]
    [SerializeField, Min(1f), Tooltip("左クリック／WASD／スペースを押し続けている間の演出経過速度倍率。")]
    private float clearCeremonyFastForwardMultiplier = 3f;
    [SerializeField, Tooltip("`Game03PodManager` の `speedViewIcon` と同じ GameObject でよい。早送り中のみ表示（`ReturnText` 表示後の遷移待ちでは非表示）。")]
    private GameObject speedViewIcon;

    [Header("乗車後のユニット並び（親 Rect の anchoredPosition）")]
    [SerializeField] private Vector2 unitBoardingMainAnchored = new Vector2(-48f, -320f);
    [SerializeField] private Vector2 unitBoardingSubAnchored = new Vector2(48f, -320f);

    [Header("レーザー待機位置（ホーム＋この Y オフセット = 画面上側の待機）")]
    [SerializeField] private float laserStandbyOffsetY = 1400f;

    [Header("船の目標 anchoredPosition（UI ピクセル）")]
    [SerializeField] private Vector2 shipCrossTargetAnchoredPosition;
    [SerializeField] private Vector2 shipLandingTargetAnchoredPosition;

    private readonly Vector2[] laserHomeAnchored = new Vector2[3];
    private bool laserHomesCaptured;
    private bool ceremonyRunning;
    private GameObject boardingUnitCloneMainGo;
    private GameObject boardingUnitCloneSubGo;
    private RectTransform boardingSubUnitSourceRect;
    private bool suppressBoardingOriginalUnitRestore;
    /// <summary><c>ReturnText</c> 表示後の遷移待ちでは早送り・3倍速アイコンを使わない。</summary>
    private bool clearCeremonyReturnPromptPhaseActive;

    private void Awake()
    {
        SetEndPhaseTextsVisible(false);
        SetCeremonySpeedViewIconVisible(false);
    }

    private void LateUpdate()
    {
        if (!ceremonyRunning)
        {
            SetCeremonySpeedViewIconVisible(false);
            return;
        }

        if (clearCeremonyReturnPromptPhaseActive)
        {
            SetCeremonySpeedViewIconVisible(false);
            return;
        }

        SetCeremonySpeedViewIconVisible(IsClearCeremonyFastForwardHeld());
    }

    private void SetCeremonySpeedViewIconVisible(bool visible)
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

    private void OnDestroy()
    {
        ClearBoardingUnitCeremonyVisuals(!suppressBoardingOriginalUnitRestore);
    }

    /// <summary>生存クリアから呼ぶ。パネルはまだ非表示でもよい（内部で準備後に表示）。</summary>
    public void BeginCeremony()
    {
        if (ceremonyRunning)
        {
            return;
        }

        ceremonyRunning = true;
        suppressBoardingOriginalUnitRestore = false;
        clearCeremonyReturnPromptPhaseActive = false;
        SetCeremonySpeedViewIconVisible(false);
        StartCoroutine(CeremonyRoutine());
    }

    private float GetClearCeremonyUnscaledDeltaTime()
    {
        if (clearCeremonyReturnPromptPhaseActive)
        {
            return Time.unscaledDeltaTime;
        }

        return Time.unscaledDeltaTime * (IsClearCeremonyFastForwardHeld() ? Mathf.Max(1f, clearCeremonyFastForwardMultiplier) : 1f);
    }

    /// <summary><c>Game03PodManager.IsFastForwardInputPressed</c> と同じキー種別。</summary>
    private static bool IsClearCeremonyFastForwardHeld()
    {
#if ENABLE_INPUT_SYSTEM
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
#elif ENABLE_LEGACY_INPUT_MANAGER
        bool keyboardPressed = Input.GetKey(KeyCode.W)
                            || Input.GetKey(KeyCode.A)
                            || Input.GetKey(KeyCode.S)
                            || Input.GetKey(KeyCode.D)
                            || Input.GetKey(KeyCode.Space);
        return keyboardPressed || Input.GetMouseButton(0);
#else
        return false;
#endif
    }

    private static CeremonyFastTapInput GetClearEndTapPressedThisFrameFromNewInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return CeremonyFastTapInput.MouseLeft;
        }

        if (Keyboard.current == null)
        {
            return CeremonyFastTapInput.None;
        }

        if (Keyboard.current.wKey.wasPressedThisFrame)
        {
            return CeremonyFastTapInput.W;
        }

        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            return CeremonyFastTapInput.A;
        }

        if (Keyboard.current.sKey.wasPressedThisFrame)
        {
            return CeremonyFastTapInput.S;
        }

        if (Keyboard.current.dKey.wasPressedThisFrame)
        {
            return CeremonyFastTapInput.D;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return CeremonyFastTapInput.Space;
        }
#endif
        return CeremonyFastTapInput.None;
    }

    private static CeremonyFastTapInput GetClearEndTapPressedThisFrameFromLegacy()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            return CeremonyFastTapInput.MouseLeft;
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            return CeremonyFastTapInput.W;
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            return CeremonyFastTapInput.A;
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            return CeremonyFastTapInput.S;
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            return CeremonyFastTapInput.D;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            return CeremonyFastTapInput.Space;
        }
#endif
        return CeremonyFastTapInput.None;
    }

    private CeremonyFastTapInput GetClearEndTapPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        CeremonyFastTapInput t = GetClearEndTapPressedThisFrameFromNewInput();
        if (t != CeremonyFastTapInput.None)
        {
            return t;
        }

        return CeremonyFastTapInput.None;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return GetClearEndTapPressedThisFrameFromLegacy();
#else
        return CeremonyFastTapInput.None;
#endif
    }

    private bool TryEscapeImmediateTransitionPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    private bool TryGetClearEndTransitionConfirmed()
    {
        if (TryEscapeImmediateTransitionPressed())
        {
            return true;
        }

        return GetClearEndTapPressedThisFrame() != CeremonyFastTapInput.None;
    }

    private float GetShipReturnAfterUnitsHideDuration()
    {
        return shipReturnAfterUnitsHideDurationSeconds > 0f
            ? Mathf.Max(0.01f, shipReturnAfterUnitsHideDurationSeconds)
            : shipCrossDurationSeconds;
    }

    private IEnumerator CoWaitClearCeremonyRealtime(float seconds)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += GetClearCeremonyUnscaledDeltaTime();
            yield return null;
        }
    }

    private IEnumerator CoAnimateAnchoredPosition(RectTransform rt, Vector2 from, Vector2 to, float duration)
    {
        if (rt == null || duration <= 0f)
        {
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += GetClearCeremonyUnscaledDeltaTime();
            float u = Mathf.Clamp01(t / duration);
            rt.anchoredPosition = Vector2.Lerp(from, to, u);
            yield return null;
        }

        rt.anchoredPosition = to;
    }

    private void CaptureLaserHomesIfNeeded()
    {
        if (laserHomesCaptured)
        {
            return;
        }

        RectTransform[] lasers = { imageLaser1, imageLaser2, imageLaser3 };
        for (int i = 0; i < lasers.Length; i++)
        {
            laserHomeAnchored[i] = lasers[i] != null ? lasers[i].anchoredPosition : Vector2.zero;
        }

        laserHomesCaptured = true;
    }

    /// <summary>レーザー着弾演出のため、<c>ImageLaser1～3</c> が非表示なら表示する。</summary>
    private void EnsureLaserImagesVisibleForStrikeSequence()
    {
        RectTransform[] lasers = { imageLaser1, imageLaser2, imageLaser3 };
        for (int i = 0; i < lasers.Length; i++)
        {
            RectTransform rt = lasers[i];
            if (rt == null)
            {
                continue;
            }

            if (!rt.gameObject.activeSelf)
            {
                rt.gameObject.SetActive(true);
            }
        }
    }

    private IEnumerator CeremonyRoutine()
    {
        if (gameClearedPanelRoot != null)
        {
            gameClearedPanelRoot.SetActive(true);
        }

        SteamAchievementController.TryUnlock(SteamAchievementIds.Game03_02);
        SoundSettingsManager.Instance?.MarkGame03Cleared();

        EnsureLaserImagesVisibleForStrikeSequence();
        CaptureLaserHomesIfNeeded();
        PrepareLasersOffScreen();
        int mvpWeapon = ResolveMvpWeaponNumber();
        if (bgmManager != null)
        {
            bgmManager.StopBgm();
        }

        SetEndPhaseTextsVisible(false);

        yield return CoWaitClearCeremonyRealtime(laserIntroWaitSeconds);
        RectTransform[] lasers = { imageLaser1, imageLaser2, imageLaser3 };
        for (int i = 0; i < lasers.Length; i++)
        {
            RectTransform rt = lasers[i];
            if (rt == null)
            {
                continue;
            }

            seManager?.PlayGameClearLaserSe();
            yield return CoAnimateAnchoredPosition(rt, rt.anchoredPosition, laserHomeAnchored[i], laserStrikeDurationSeconds);
            yield return CoWaitClearCeremonyRealtime(laserBetweenSeconds);
        }

        RectTransform mvpShip = GetShipRectForWeaponNumber(mvpWeapon);
        PrepareShipsForMvp(mvpShip);
        if (mvpShip == null)
        {
            ceremonyRunning = false;
            yield break;
        }

        Vector2 shipStart = mvpShip.anchoredPosition;
        seManager?.PlayGameClearShipCrossSe();
        float crossHalf = shipCrossDurationSeconds * 0.5f;
        bool hidGameplayUi = false;
        float crossT = 0f;
        while (crossT < shipCrossDurationSeconds)
        {
            crossT += GetClearCeremonyUnscaledDeltaTime();
            float u = Mathf.Clamp01(crossT / shipCrossDurationSeconds);
            mvpShip.anchoredPosition = Vector2.Lerp(shipStart, shipCrossTargetAnchoredPosition, u);
            if (!hidGameplayUi && crossT >= crossHalf)
            {
                hidGameplayUi = true;
                SetGameplayUiRootsActive(false);
            }

            yield return null;
        }

        mvpShip.anchoredPosition = shipCrossTargetAnchoredPosition;
        yield return CoWaitClearCeremonyRealtime(shipLandingWaitSeconds);
        Vector3 shipEulerBeforeLandFlip = mvpShip.localEulerAngles;
        Vector3 shipScaleBeforeLand = mvpShip.localScale;
        mvpShip.localEulerAngles = new Vector3(shipEulerBeforeLandFlip.x, 180f, shipEulerBeforeLandFlip.z);
        mvpShip.localScale = Vector3.one;
        Vector2 landStart = mvpShip.anchoredPosition;
        float landT = 0f;
        while (landT < shipLandingMoveDurationSeconds)
        {
            landT += GetClearCeremonyUnscaledDeltaTime();
            float u = Mathf.Clamp01(landT / shipLandingMoveDurationSeconds);
            mvpShip.anchoredPosition = Vector2.Lerp(landStart, shipLandingTargetAnchoredPosition, u);
            yield return null;
        }

        mvpShip.anchoredPosition = shipLandingTargetAnchoredPosition;
        yield return BoardingLaunchAndInputRoutine(mvpWeapon, mvpShip, shipEulerBeforeLandFlip, shipScaleBeforeLand);
        ceremonyRunning = false;
    }

    private IEnumerator BoardingLaunchAndInputRoutine(int mvpWeapon, RectTransform mvpShip, Vector3 shipEulerForTakeoffReturn, Vector3 shipScaleForTakeoffReturn)
    {
        boardingSubUnitSourceRect = null;
        try
        {
            yield return CoWaitClearCeremonyRealtime(shipBoardingWaitSeconds);

            RectTransform mainSrc = mainUnitOjRect;
            RectTransform subSrc = null;
            if (mvpWeapon != 1)
            {
                TryGetSubUnitRectForEquippedWeapon(mvpWeapon, out subSrc);
            }

            RectTransform mainAnim = CreateBoardingUnitVisualClone(mainSrc, "MainUnitOj_ClearBoardingClone", ref boardingUnitCloneMainGo);
            RectTransform subAnim = null;
            if (subSrc != null)
            {
                subAnim = CreateBoardingUnitVisualClone(subSrc, "UnitSub_ClearBoardingClone", ref boardingUnitCloneSubGo);
                if (subAnim != null)
                {
                    boardingSubUnitSourceRect = subSrc;
                }
            }

            Vector2 mainStart = mainAnim != null ? mainAnim.anchoredPosition : Vector2.zero;
            Vector2 subStart = subAnim != null ? subAnim.anchoredPosition : Vector2.zero;
            Vector2 mainDropTarget = mainStart + new Vector2(0f, -unitBoardingDropPixels);
            Vector2 subDropTarget = subAnim != null ? subStart + new Vector2(0f, -unitBoardingDropPixels) : Vector2.zero;

            if (mainAnim != null)
            {
                yield return CoAnimateAnchoredPosition(mainAnim, mainStart, mainDropTarget, unitDropDurationSeconds);
            }

            if (subAnim != null)
            {
                yield return CoAnimateAnchoredPosition(subAnim, subStart, subDropTarget, unitDropDurationSeconds);
            }

            if (mainAnim != null && subAnim != null)
            {
                yield return CoAnimateAnchoredPosition(mainAnim, mainDropTarget, unitBoardingMainAnchored, unitTogetherLayoutDurationSeconds);
                yield return CoAnimateAnchoredPosition(subAnim, subDropTarget, unitBoardingSubAnchored, unitTogetherLayoutDurationSeconds);
            }
            else if (mainAnim != null)
            {
                yield return CoAnimateAnchoredPosition(mainAnim, mainDropTarget, unitBoardingMainAnchored, unitTogetherLayoutDurationSeconds);
            }

            while (AnyUnitRectNeedsMoreSlideLeft(mainAnim, subAnim, unitBoardingExitAnchoredX))
            {
                float dx = -unitBoardingSlideSpeedPixelsPerSecond * GetClearCeremonyUnscaledDeltaTime();
                if (mainAnim != null)
                {
                    mainAnim.anchoredPosition += new Vector2(dx, 0f);
                }

                if (subAnim != null)
                {
                    subAnim.anchoredPosition += new Vector2(dx, 0f);
                }

                yield return null;
            }

            while (AnyUnitRectNeedsMoreSlideLeft(mainAnim, subAnim, unitBoardHideAnchoredX))
            {
                float dt = GetClearCeremonyUnscaledDeltaTime();
                float dx = -unitBoardingSlideSpeedPixelsPerSecond * dt;
                if (mainAnim != null)
                {
                    mainAnim.anchoredPosition += new Vector2(dx, 0f);
                }

                if (subAnim != null)
                {
                    subAnim.anchoredPosition += new Vector2(dx, 0f);
                }

                yield return null;
            }

            suppressBoardingOriginalUnitRestore = true;
            ClearBoardingUnitCeremonyVisuals(restoreOriginalBoardingUnits: false);

            seManager?.PlayGameClearLaunchSe();

            if (mvpShip != null)
            {
                Vector2 shipFlyStart = mvpShip.anchoredPosition;
                Vector3 shipEulerFlyStart = mvpShip.localEulerAngles;
                Vector3 shipScaleFlyStart = mvpShip.localScale;
                float shipFlyDuration = GetShipReturnAfterUnitsHideDuration();
                float shipFlyT = 0f;
                while (shipFlyT < shipFlyDuration)
                {
                    float dt = GetClearCeremonyUnscaledDeltaTime();
                    shipFlyT += dt;
                    float u = Mathf.Clamp01(shipFlyT / shipFlyDuration);
                    mvpShip.anchoredPosition = Vector2.Lerp(shipFlyStart, shipCrossTargetAnchoredPosition, u);
                    mvpShip.localEulerAngles = Vector3.Lerp(shipEulerFlyStart, shipEulerForTakeoffReturn, u);
                    mvpShip.localScale = Vector3.Lerp(shipScaleFlyStart, shipScaleForTakeoffReturn, u);
                    yield return null;
                }

                mvpShip.anchoredPosition = shipCrossTargetAnchoredPosition;
                mvpShip.localEulerAngles = shipEulerForTakeoffReturn;
                mvpShip.localScale = shipScaleForTakeoffReturn;
            }

            yield return CoWaitClearCeremonyRealtime(shipLaunchWaitSeconds);
            if (gameClearedTextMesh != null)
            {
                gameClearedTextMesh.gameObject.SetActive(true);
            }

            bgmManager?.PlayGameClearPresentationBgm();

            yield return CoWaitClearCeremonyRealtime(shipLaunchWaitSeconds);
            if (returnTextMesh != null)
            {
                returnTextMesh.gameObject.SetActive(true);
            }

            clearCeremonyReturnPromptPhaseActive = true;
            SetCeremonySpeedViewIconVisible(false);

            float blinkPeriod = Mathf.Max(0.01f, returnTextBlinkPeriodSeconds);
            while (!TryGetClearEndTransitionConfirmed())
            {
                float a = (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / blinkPeriod)) + 1f) * 0.5f;
                if (returnTextMesh != null)
                {
                    Color c = returnTextMesh.color;
                    c.a = a;
                    returnTextMesh.color = c;
                }

                yield return null;
            }

            ceremonyRunning = false;
            clearCeremonyReturnPromptPhaseActive = false;
            if (gameClearedPanelRoot != null)
            {
                gameClearedPanelRoot.SetActive(false);
            }

            Game03GameOverPresentation presentation = runEndPresentation != null
                ? runEndPresentation
                : FindAnyObjectByType<Game03GameOverPresentation>(FindObjectsInactive.Include);
            if (presentation != null)
            {
                presentation.OpenResultPanelAfterGameClear();
            }
            else
            {
                Game03TransitionManager tm = game03TransitionManager != null
                    ? game03TransitionManager
                    : FindAnyObjectByType<Game03TransitionManager>(FindObjectsInactive.Include);
                tm?.TransitionToDialogueForGame03ClearEnd();
            }
        }
        finally
        {
            ClearBoardingUnitCeremonyVisuals(!suppressBoardingOriginalUnitRestore);
        }
    }

    private void SetEndPhaseTextsVisible(bool visible)
    {
        if (gameClearedTextMesh != null)
        {
            gameClearedTextMesh.gameObject.SetActive(visible);
        }

        if (returnTextMesh != null)
        {
            returnTextMesh.gameObject.SetActive(visible);
            Color c = returnTextMesh.color;
            c.a = visible ? 1f : 0f;
            returnTextMesh.color = c;
        }
    }

    private bool TryGetSubUnitRectForEquippedWeapon(int weaponNumber, out RectTransform rect)
    {
        rect = null;
        if (weaponManager == null)
        {
            return false;
        }

        for (int slot = 1; slot <= 4; slot++)
        {
            if (!weaponManager.TryGetEquippedWeaponForSlot(slot, out int w, out _))
            {
                continue;
            }

            if (w != weaponNumber)
            {
                continue;
            }

            rect = GetUnitSlotRect(slot);
            return rect != null;
        }

        return false;
    }

    private RectTransform GetUnitSlotRect(int slotIndex1To4)
    {
        return slotIndex1To4 switch
        {
            1 => unit01OjRect,
            2 => unit02OjRect,
            3 => unit03OjRect,
            4 => unit04OjRect,
            _ => null
        };
    }

    private static bool AnyUnitRectNeedsMoreSlideLeft(RectTransform mainRt, RectTransform subRt, float hideAnchoredX)
    {
        if (mainRt != null && mainRt.gameObject.activeSelf && mainRt.anchoredPosition.x > hideAnchoredX)
        {
            return true;
        }

        if (subRt != null && subRt.gameObject.activeSelf && subRt.anchoredPosition.x > hideAnchoredX)
        {
            return true;
        }

        return false;
    }

    private RectTransform CreateBoardingUnitVisualClone(RectTransform source, string cloneName, ref GameObject cloneRootOut)
    {
        cloneRootOut = null;
        if (source == null)
        {
            return null;
        }

        int siblingIndex = source.GetSiblingIndex();
        GameObject cloneGo = Instantiate(source.gameObject, source.parent);
        cloneGo.name = cloneName;
        RectTransform cloneRt = cloneGo.GetComponent<RectTransform>();
        if (cloneRt == null)
        {
            Destroy(cloneGo);
            return null;
        }

        cloneRt.anchorMin = source.anchorMin;
        cloneRt.anchorMax = source.anchorMax;
        cloneRt.pivot = source.pivot;
        cloneRt.anchoredPosition = source.anchoredPosition;
        cloneRt.sizeDelta = source.sizeDelta;
        cloneRt.localRotation = source.localRotation;
        cloneRt.localScale = source.localScale;
        cloneRt.offsetMin = source.offsetMin;
        cloneRt.offsetMax = source.offsetMax;
        cloneRt.SetSiblingIndex(siblingIndex);
        source.gameObject.SetActive(false);
        cloneRootOut = cloneGo;
        return cloneRt;
    }

    private void ClearBoardingUnitCeremonyVisuals(bool restoreOriginalBoardingUnits = true)
    {
        if (boardingUnitCloneMainGo != null)
        {
            Destroy(boardingUnitCloneMainGo);
            boardingUnitCloneMainGo = null;
        }

        if (boardingUnitCloneSubGo != null)
        {
            Destroy(boardingUnitCloneSubGo);
            boardingUnitCloneSubGo = null;
        }

        if (restoreOriginalBoardingUnits)
        {
            if (mainUnitOjRect != null)
            {
                mainUnitOjRect.gameObject.SetActive(true);
            }

            if (boardingSubUnitSourceRect != null)
            {
                boardingSubUnitSourceRect.gameObject.SetActive(true);
                boardingSubUnitSourceRect = null;
            }
        }
        else
        {
            boardingSubUnitSourceRect = null;
        }
    }

    private void PrepareLasersOffScreen()
    {
        RectTransform[] lasers = { imageLaser1, imageLaser2, imageLaser3 };
        for (int i = 0; i < lasers.Length; i++)
        {
            RectTransform rt = lasers[i];
            if (rt == null)
            {
                continue;
            }

            Vector2 home = laserHomeAnchored[i];
            rt.anchoredPosition = home + new Vector2(0f, laserStandbyOffsetY);
        }
    }

    /// <summary>武器01はラン開始から装備のため、MVP 戦艦抽選のみ UG 合計をこの分だけ減算して比較する。</summary>
    private const int MvpShipSelectionWeapon01UpgradePenalty = 4;

    private int ResolveMvpWeaponNumber()
    {
        if (weaponManager == null)
        {
            return 1;
        }

        var candidates = new List<int>(8);
        for (int slot = 0; slot <= 4; slot++)
        {
            if (!weaponManager.TryGetEquippedWeaponForSlot(slot, out int w, out _))
            {
                continue;
            }

            if (w >= 1 && w <= 7 && !candidates.Contains(w))
            {
                candidates.Add(w);
            }
        }

        if (candidates.Count == 0)
        {
            return 1;
        }

        int bestTotal = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            int t = GetMvpShipSelectionUpgradeTotal(candidates[i]);
            if (t > bestTotal)
            {
                bestTotal = t;
            }
        }

        var tied = new List<int>(8);
        for (int i = 0; i < candidates.Count; i++)
        {
            int w = candidates[i];
            if (GetMvpShipSelectionUpgradeTotal(w) == bestTotal)
            {
                tied.Add(w);
            }
        }

        if (tied.Count == 0)
        {
            return candidates[0];
        }

        return tied[Random.Range(0, tied.Count)];
    }

    private int GetMvpShipSelectionUpgradeTotal(int weaponNumber)
    {
        int total = weaponManager.GetTotalUpgradeCountForWeapon(weaponNumber);
        if (weaponNumber == 1)
        {
            return Mathf.Max(0, total - MvpShipSelectionWeapon01UpgradePenalty);
        }

        return total;
    }

    private RectTransform GetShipRectForWeaponNumber(int weaponNumber)
    {
        return weaponNumber switch
        {
            1 => imageShip01,
            2 => imageShip02,
            3 => imageShip03,
            4 => imageShip04,
            5 => imageShip05,
            6 => imageShip06,
            7 => imageShip07,
            _ => imageShip01
        };
    }

    private void PrepareShipsForMvp(RectTransform mvpShip)
    {
        RectTransform[] all =
        {
            imageShip01, imageShip02, imageShip03, imageShip04, imageShip05, imageShip06, imageShip07
        };
        for (int i = 0; i < all.Length; i++)
        {
            RectTransform rt = all[i];
            if (rt == null)
            {
                continue;
            }

            bool on = rt == mvpShip;
            rt.gameObject.SetActive(on);
        }
    }

    private void SetGameplayUiRootsActive(bool active)
    {
        if (enemyCanvasRoot != null)
        {
            enemyCanvasRoot.SetActive(active);
        }

        if (weaponCanvasRoot != null)
        {
            weaponCanvasRoot.SetActive(active);
        }

        if (itemCanvasRoot != null)
        {
            itemCanvasRoot.SetActive(active);
        }

        if (itemNaviRoot != null)
        {
            itemNaviRoot.SetActive(active);
        }

        if (panelOjRoot != null)
        {
            panelOjRoot.SetActive(active);
        }

        RectTransform[] lasers = { imageLaser1, imageLaser2, imageLaser3 };
        for (int i = 0; i < lasers.Length; i++)
        {
            RectTransform rt = lasers[i];
            if (rt != null)
            {
                rt.gameObject.SetActive(active);
            }
        }
    }
}
