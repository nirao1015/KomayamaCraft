using System;
using UnityEngine;
using Steamworks;

[DisallowMultipleComponent]
public sealed class SteamApiLifecycleController : MonoBehaviour
{
    public static SteamApiLifecycleController Instance { get; private set; }

    [SerializeField] private bool initializeOnAwake = true;
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool debugLog;

    public bool IsSteamAvailable => isInitialized;

    private bool isInitialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (initializeOnAwake)
        {
            TryInitializeSteam();
        }
    }

    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        SteamAPI.RunCallbacks();
    }

    private void OnApplicationQuit()
    {
        ShutdownSteamIfNeeded();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            ShutdownSteamIfNeeded();
            Instance = null;
        }
    }

    public bool TryInitializeSteam()
    {
        if (isInitialized)
        {
            return true;
        }

        try
        {
            if (!SteamAPI.Init())
            {
                if (debugLog)
                {
                    Debug.LogWarning("[SteamApiLifecycleController] SteamAPI.Init failed.");
                }

                return false;
            }
        }
        catch (Exception ex)
        {
            if (debugLog)
            {
                Debug.LogWarning($"[SteamApiLifecycleController] Steam init exception: {ex.Message}");
            }

            return false;
        }

        isInitialized = true;
        if (debugLog)
        {
            Debug.Log("[SteamApiLifecycleController] SteamAPI initialized.");
        }

        return true;
    }

    private void ShutdownSteamIfNeeded()
    {
        if (!isInitialized)
        {
            return;
        }

        SteamAPI.Shutdown();
        isInitialized = false;
        if (debugLog)
        {
            Debug.Log("[SteamApiLifecycleController] SteamAPI shutdown.");
        }
    }
}
