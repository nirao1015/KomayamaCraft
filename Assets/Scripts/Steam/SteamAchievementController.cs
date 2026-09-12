using System.Collections;
using UnityEngine;
#if STEAMWORKS_NET
using System.Collections.Generic;
using Steamworks;
#endif

/// <summary>
/// Steam 実績の取得・解除申請（DontDestroyOnLoad）。正本: spec/クラウドsave実績仕様書.txt
/// </summary>
[DisallowMultipleComponent]
public sealed class SteamAchievementController : MonoBehaviour
{
    private const int FetchRetryMax = 3;
    private const int UnlockRetryMax = 2;

#if STEAMWORKS_NET
    private enum AchievementState
    {
        Unknown,
        Locked,
        Unlocked
    }
#endif

    public static SteamAchievementController Instance { get; private set; }

    [SerializeField] private bool debugLog;

#if STEAMWORKS_NET
    private readonly Dictionary<string, AchievementState> states = new Dictionary<string, AchievementState>(32);
    private readonly HashSet<string> unlockRequestedThisSession = new HashSet<string>(32);

    private bool fetchCompleted;
    private bool statsReceived;
    private Callback<UserStatsReceived_t> userStatsReceivedCallback;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
#if STEAMWORKS_NET
        InitializeStatesUnknown();
#endif
    }

    private void OnEnable()
    {
#if STEAMWORKS_NET
        if (userStatsReceivedCallback == null)
        {
            userStatsReceivedCallback = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);
        }
#endif
    }

    private void Start()
    {
#if STEAMWORKS_NET
        StartCoroutine(FetchAchievementStatesRoutine());
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void TryUnlock(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId))
        {
            return;
        }

        SteamSessionFileLogger.LogAchievement(achievementId, "TRIGGER", "unlock requested");

        SteamAchievementController controller = Instance;
        if (controller == null)
        {
            SteamSessionFileLogger.LogAchievement(achievementId, "SKIP", "SteamAchievementController not in scene");
            return;
        }

#if STEAMWORKS_NET
        controller.StartCoroutine(controller.TryUnlockRoutine(achievementId));
#else
        SteamSessionFileLogger.LogAchievement(achievementId, "SKIP", "STEAMWORKS_NET not defined");
#endif
    }

#if STEAMWORKS_NET
    private void InitializeStatesUnknown()
    {
        states.Clear();
        for (int i = 0; i < SteamAchievementIds.All.Length; i++)
        {
            states[SteamAchievementIds.All[i]] = AchievementState.Unknown;
        }
    }

    private static bool IsSteamReady()
    {
        return SteamApiLifecycleController.Instance != null
            && SteamApiLifecycleController.Instance.IsSteamAvailable;
    }

    private IEnumerator FetchAchievementStatesRoutine()
    {
        const float waitSteamTimeout = 8f;
        float waited = 0f;
        while (!IsSteamReady() && waited < waitSteamTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!IsSteamReady())
        {
            fetchCompleted = true;
            if (debugLog)
            {
                Debug.LogWarning("[SteamAchievementController] Steam unavailable; achievements disabled this session.");
            }

            yield break;
        }

        // Steamworks SDK 1.61+ では RequestCurrentStats が削除され、クライアントが事前同期する。
        const float waitCallbackSeconds = 8f;
        float elapsed = 0f;
        while (!statsReceived && elapsed < waitCallbackSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!statsReceived && TryRefreshAllAchievementStatesFromSteam())
        {
            statsReceived = true;
        }

        fetchCompleted = true;
        if (debugLog)
        {
            Debug.Log($"[SteamAchievementController] Fetch complete. statsReceived={statsReceived}");
        }
    }

    private void OnUserStatsReceived(UserStatsReceived_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            if (debugLog)
            {
                Debug.LogWarning($"[SteamAchievementController] UserStatsReceived failed: {callback.m_eResult}");
            }

            return;
        }

        statsReceived = true;
        RefreshAllAchievementStatesFromSteam();
    }

    private bool TryRefreshAllAchievementStatesFromSteam()
    {
        bool anyRead = false;
        for (int i = 0; i < SteamAchievementIds.All.Length; i++)
        {
            string id = SteamAchievementIds.All[i];
            AchievementState state = ReadAchievementStateFromSteam(id);
            if (state != AchievementState.Unknown)
            {
                anyRead = true;
            }

            states[id] = state;
        }

        return anyRead;
    }

    private void RefreshAllAchievementStatesFromSteam()
    {
        TryRefreshAllAchievementStatesFromSteam();
    }

    private AchievementState ReadAchievementStateFromSteam(string achievementId)
    {
        for (int attempt = 1; attempt <= FetchRetryMax; attempt++)
        {
            if (SteamUserStats.GetAchievement(achievementId, out bool achieved))
            {
                return achieved ? AchievementState.Unlocked : AchievementState.Locked;
            }

            if (debugLog)
            {
                Debug.LogWarning($"[SteamAchievementController] GetAchievement failed: {achievementId} attempt={attempt}");
            }
        }

        return AchievementState.Unknown;
    }

    private IEnumerator TryUnlockRoutine(string achievementId)
    {
        const float waitFetchTimeout = 6f;
        float waited = 0f;
        while (!fetchCompleted && waited < waitFetchTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!IsSteamReady())
        {
            SteamSessionFileLogger.LogAchievement(achievementId, "SKIP", "Steam API unavailable");
            yield break;
        }

        if (unlockRequestedThisSession.Contains(achievementId))
        {
            SteamSessionFileLogger.LogAchievement(achievementId, "SKIP", "already requested this session");
            yield break;
        }

        if (states.TryGetValue(achievementId, out AchievementState known) && known == AchievementState.Unlocked)
        {
            SteamSessionFileLogger.LogAchievement(
                achievementId,
                "SKIP",
                "already unlocked (cached before request)");
            yield break;
        }

        unlockRequestedThisSession.Add(achievementId);

        bool unlocked = false;
        for (int attempt = 1; attempt <= UnlockRetryMax && !unlocked; attempt++)
        {
            if (SteamUserStats.SetAchievement(achievementId))
            {
                if (SteamUserStats.StoreStats())
                {
                    unlocked = true;
                    states[achievementId] = AchievementState.Unlocked;
                    SteamSessionFileLogger.LogAchievement(
                        achievementId,
                        "UNLOCKED",
                        $"SetAchievement+StoreStats ok attempt={attempt}");
                    if (debugLog)
                    {
                        Debug.Log($"[SteamAchievementController] Unlocked {achievementId}");
                    }
                }
                else
                {
                    SteamSessionFileLogger.LogAchievement(
                        achievementId,
                        "FAILED",
                        $"StoreStats failed attempt={attempt}");
                    if (debugLog)
                    {
                        Debug.LogWarning($"[SteamAchievementController] StoreStats failed: {achievementId} attempt={attempt}");
                    }
                }
            }
            else
            {
                SteamSessionFileLogger.LogAchievement(
                    achievementId,
                    "FAILED",
                    $"SetAchievement failed attempt={attempt}");
                if (debugLog)
                {
                    Debug.LogWarning($"[SteamAchievementController] SetAchievement failed: {achievementId} attempt={attempt}");
                }
            }

            if (!unlocked && attempt < UnlockRetryMax)
            {
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }
    }
#endif
}
