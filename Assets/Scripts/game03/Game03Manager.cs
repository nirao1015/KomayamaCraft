using UnityEngine;
using TMPro;

/// <summary>
/// game03 のポーズ・タイムスケール・経過時間など。ObstaclesRoot は既定で非アクティブのため、
/// <see cref="Game03FieldItemCoordinator"/>（-120）より前に有効化する必要がある。
/// </summary>
[DefaultExecutionOrder(-250)]
public class Game03Manager : MonoBehaviour
{
    public enum GameTimeScalePreset
    {
        X0_5,
        X0_8,
        X0_9,
        X1_0,
        X1_5,
        X2_0,
        X3_0,
        X5_0
    }

    [Header("References")]
    [SerializeField] private Game03BgmManager game03BgmManager;
    [SerializeField] private Game03SeManager game03SeManager;
    [SerializeField, Tooltip("未設定時はシーン直下の ObstaclesRoot を名前で検索（非アクティブ含む）。非表示なら Awake で表示にする。")]
    private GameObject obstaclesRoot;

    [Header("Runtime State")]
    [SerializeField] private bool isPaused;
    [SerializeField] private bool suppressGameplayPausePanelWhilePaused;
    [SerializeField] private bool isCutsceneActive;
    [SerializeField] private GameTimeScalePreset gameTimeScalePreset = GameTimeScalePreset.X1_0;
    [SerializeField, Tooltip("ON の間 GameplayElapsedSeconds を増やさない（開始演出などゲームタイマー開始前用）。既定OFFで従来どおり進む。")]
    private bool suppressGameplayElapsedAccumulation;

    [Header("FPS")]
    [SerializeField, Min(0.05f)] private float fpsSampleInterval = 0.1f;
    [SerializeField] private TMP_Text fpsUiText;
    [SerializeField, Min(0.1f)] private float fpsUiRefreshInterval = 0.5f;

    private float gameplayDeltaTime;
    private float cutsceneDeltaTime;
    private float gameplayElapsedSeconds;
    private float gameplayTimeScale = 1f;
    private float fpsSampleElapsed;
    private int fpsSampleFrames;
    private int currentFpsRounded;
    private float fpsUiRefreshElapsed;
    private int lastShownFps = int.MinValue;
    private bool hasFpsSample;
    private AudioClip preCutsceneBgmClip;
    private bool hasCutsceneBgmOverride;

    public bool IsPaused => isPaused;
    public bool SuppressGameplayPausePanelWhilePaused => suppressGameplayPausePanelWhilePaused;
    public bool ShouldShowGameplayPausePanel => isPaused && !suppressGameplayPausePanelWhilePaused;
    public bool IsCutsceneActive => isCutsceneActive;
    public bool CanRunGameplay => !isPaused && !isCutsceneActive;
    public bool CanPlayGameplaySe => CanRunGameplay;
    public bool CanPlayCutsceneSe => isCutsceneActive;
    public float GameplayTimeScale => gameplayTimeScale;
    public GameTimeScalePreset CurrentGameTimeScalePreset => gameTimeScalePreset;
    public float GameplayDeltaTime => gameplayDeltaTime;
    public float CutsceneDeltaTime => cutsceneDeltaTime;
    /// <summary>
    /// 一時停止・カットシーン中は進まないゲーム内経過時間（秒）。game02 の GameplayElapsedSeconds と同様にタイムスケール反映。
    /// </summary>
    public float GameplayElapsedSeconds => gameplayElapsedSeconds;
    public int CurrentFpsRounded => currentFpsRounded;
    public bool SuppressGameplayElapsedAccumulation => suppressGameplayElapsedAccumulation;

    /// <summary>
    /// デバッグ専用。敵出現フェーズ先頭に合わせるため <see cref="GameplayElapsedSeconds"/> を上書きする（本番では呼ばない想定）。
    /// </summary>
    public void DebugSeedGameplayElapsedSeconds(float secondsFromGameStart)
    {
        gameplayElapsedSeconds = Mathf.Max(0f, secondsFromGameStart);
    }

    private void Awake()
    {
        Game03RunSessionState.ResetForNewRun();
        Game03UnitUgManager unitUgManager = FindAnyObjectByType<Game03UnitUgManager>(FindObjectsInactive.Include);
        unitUgManager?.ResetForNewRun();
        Game03BossUgManager bossUgManager = FindAnyObjectByType<Game03BossUgManager>(FindObjectsInactive.Include);
        bossUgManager?.ResetForNewRun();
        EnsureObstaclesRootVisibleIfNeeded();
        ApplyTimeScalePreset(gameTimeScalePreset);
        if (fpsUiText != null)
        {
            fpsUiText.SetText(string.Empty);
        }
    }

    private void EnsureObstaclesRootVisibleIfNeeded()
    {
        const string rootName = "ObstaclesRoot";
        GameObject root = obstaclesRoot;
        if (root == null)
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.parent != null || t.name != rootName)
                {
                    continue;
                }

                root = t.gameObject;
                break;
            }
        }

        if (root != null && !root.activeSelf)
        {
            root.SetActive(true);
        }
    }

    private void Update()
    {
        cutsceneDeltaTime = Time.unscaledDeltaTime;
        gameplayDeltaTime = CanRunGameplay ? cutsceneDeltaTime * gameplayTimeScale : 0f;
        if (!suppressGameplayElapsedAccumulation)
        {
            gameplayElapsedSeconds += gameplayDeltaTime;
        }

        TickFps(cutsceneDeltaTime);
        TickFpsUi(cutsceneDeltaTime);
    }

    public void SetPaused(bool paused, bool suppressGameplayPausePanelWhilePaused = false)
    {
        if (paused)
        {
            if (!isPaused)
            {
                isPaused = true;
                StopAllSeImmediately();
            }

            this.suppressGameplayPausePanelWhilePaused = suppressGameplayPausePanelWhilePaused;
            return;
        }

        if (!isPaused)
        {
            return;
        }

        isPaused = false;
        suppressGameplayPausePanelWhilePaused = false;
    }

    public void PauseGame()
    {
        SetPaused(true);
    }

    public void ResumeGame()
    {
        SetPaused(false);
    }

    public bool SetGameplayTimeScale(float scale)
    {
        if (!IsAllowedTimeScale(scale))
        {
            return false;
        }

        gameplayTimeScale = scale;
        return true;
    }

    public bool SetGameplayTimeScalePreset(GameTimeScalePreset preset)
    {
        gameTimeScalePreset = preset;
        return SetGameplayTimeScale(ConvertPresetToScale(preset));
    }

    /// <summary>
    /// デバッグ用。定義済みプリセットを宣言順に循環する（<see cref="GameTimeScalePreset"/>）。
    /// </summary>
    public void CycleGameTimeScalePreset()
    {
        int n = System.Enum.GetValues(typeof(GameTimeScalePreset)).Length;
        int cur = (int)gameTimeScalePreset;
        int next = (cur + 1) % Mathf.Max(1, n);
        SetGameplayTimeScalePreset((GameTimeScalePreset)next);
    }

    /// <summary>UI 表示用の倍率ラベル（例: x1.0）。</summary>
    public static string FormatTimeScalePresetLabel(GameTimeScalePreset preset)
    {
        return preset switch
        {
            GameTimeScalePreset.X0_5 => "x0.5",
            GameTimeScalePreset.X0_8 => "x0.8",
            GameTimeScalePreset.X0_9 => "x0.9",
            GameTimeScalePreset.X1_0 => "x1.0",
            GameTimeScalePreset.X1_5 => "x1.5",
            GameTimeScalePreset.X2_0 => "x2.0",
            GameTimeScalePreset.X3_0 => "x3.0",
            GameTimeScalePreset.X5_0 => "x5.0",
            _ => "x1.0"
        };
    }

    /// <summary>
    /// 本番リリースビルド時にゲーム内タイムスケールを常に等速へ戻す。
    /// </summary>
    public void ApplyProductionGameplayTimeClamp()
    {
        gameTimeScalePreset = GameTimeScalePreset.X1_0;
        gameplayTimeScale = 1f;
    }

    public void BeginCutscene()
    {
        if (isCutsceneActive)
        {
            return;
        }

        isCutsceneActive = true;
        hasCutsceneBgmOverride = false;
        preCutsceneBgmClip = game03BgmManager != null ? game03BgmManager.CurrentClip : null;
        StopAllSeImmediately();
    }

    public void EndCutscene(bool restorePreCutsceneBgm = true)
    {
        if (!isCutsceneActive)
        {
            return;
        }

        isCutsceneActive = false;
        StopAllSeImmediately();

        if (restorePreCutsceneBgm && game03BgmManager != null)
        {
            if (preCutsceneBgmClip != null)
            {
                game03BgmManager.PlayClip(preCutsceneBgmClip);
            }
            else
            {
                game03BgmManager.PlayMainBgm();
            }
        }

        preCutsceneBgmClip = null;
        hasCutsceneBgmOverride = false;
    }

    public bool OverrideCutsceneBgm(AudioClip clip)
    {
        if (!isCutsceneActive || clip == null)
        {
            return false;
        }

        if (game03BgmManager == null || !game03BgmManager.PlayClip(clip))
        {
            return false;
        }

        hasCutsceneBgmOverride = true;
        return true;
    }

    private void ApplyTimeScalePreset(GameTimeScalePreset preset)
    {
        gameplayTimeScale = ConvertPresetToScale(preset);
    }

    private static float ConvertPresetToScale(GameTimeScalePreset preset)
    {
        return preset switch
        {
            GameTimeScalePreset.X0_5 => 0.5f,
            GameTimeScalePreset.X0_8 => 0.8f,
            GameTimeScalePreset.X0_9 => 0.9f,
            GameTimeScalePreset.X1_0 => 1f,
            GameTimeScalePreset.X1_5 => 1.5f,
            GameTimeScalePreset.X2_0 => 2f,
            GameTimeScalePreset.X3_0 => 3f,
            GameTimeScalePreset.X5_0 => 5f,
            _ => 1f
        };
    }

    private static bool IsAllowedTimeScale(float scale)
    {
        return Mathf.Approximately(scale, 0.5f)
               || Mathf.Approximately(scale, 0.8f)
               || Mathf.Approximately(scale, 0.9f)
               || Mathf.Approximately(scale, 1f)
               || Mathf.Approximately(scale, 1.5f)
               || Mathf.Approximately(scale, 2f)
               || Mathf.Approximately(scale, 3f)
               || Mathf.Approximately(scale, 5f);
    }

    private void TickFps(float unscaledDeltaTime)
    {
        fpsSampleElapsed += unscaledDeltaTime;
        fpsSampleFrames++;
        if (fpsSampleElapsed < Mathf.Max(0.05f, fpsSampleInterval))
        {
            return;
        }

        float fps = fpsSampleFrames / Mathf.Max(0.0001f, fpsSampleElapsed);
        currentFpsRounded = Mathf.RoundToInt(fps);
        hasFpsSample = true;
        fpsSampleElapsed = 0f;
        fpsSampleFrames = 0;
    }

    private void TickFpsUi(float unscaledDeltaTime)
    {
        if (fpsUiText == null || !hasFpsSample)
        {
            return;
        }

        fpsUiRefreshElapsed += unscaledDeltaTime;
        if (fpsUiRefreshElapsed < Mathf.Max(0.1f, fpsUiRefreshInterval))
        {
            return;
        }

        fpsUiRefreshElapsed = 0f;
        if (lastShownFps == currentFpsRounded)
        {
            return;
        }

        lastShownFps = currentFpsRounded;
        fpsUiText.SetText("{0}", currentFpsRounded);
    }

    private void StopAllSeImmediately()
    {
        if (game03SeManager != null)
        {
            game03SeManager.StopAllSeImmediately();
        }
    }

    /// <summary>
    /// ゲーム内経過タイマー（GameplayElapsedSeconds）のカウントを抑止／再開する。
    /// </summary>
    public void SetGameplayElapsedAccumulationSuppressed(bool suppressed)
    {
        suppressGameplayElapsedAccumulation = suppressed;
    }
}
