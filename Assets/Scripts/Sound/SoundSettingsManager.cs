using System;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class SoundSettingsManager : MonoBehaviour
{
    private const string SaveFolderName = "saveData";
    private const string PlayerDataFileName = "playerData.json";
    private const int PlayerDataVersion = 1;
    private const string ObsoleteLegacyVolumeFileName = "sound_volume.json";
    private const int DefaultVolumeWhenNoSave = 10;

    [Serializable]
    private struct PersistedPlayerData
    {
        public int version;
        public string updatedAtUtc;
        public string buildVersion;
        public int master;
        public int bgm;
        public int se;
        public bool game01Cleared;
        public bool game02Cleared;
        public bool game03Cleared;
        public bool debugUnlockAllGames;
    }

    [Header("Volume (0-20)")]
    [SerializeField] private int masterVolume = DefaultVolumeWhenNoSave;
    [SerializeField] private int seVolume = DefaultVolumeWhenNoSave;
    [SerializeField] private int bgmVolume = DefaultVolumeWhenNoSave;
    [Header("Global Base Multiplier")]
    [SerializeField] private float seBaseMultiplier = 1f;
    [SerializeField] private float bgmBaseMultiplier = 1f;

    private bool game01Cleared;
    private bool game02Cleared;
    private bool game03Cleared;
    private bool debugUnlockAllGames;
    private string recordedBuildVersion = string.Empty;
    private bool applicationHasFocus = true;
    private bool applicationPausedForPlatform;

    private static SoundSettingsManager instance;

    public static SoundSettingsManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<SoundSettingsManager>();
                if (instance == null)
                {
                    GameObject managerObject = new GameObject("SoundSettingsManager");
                    instance = managerObject.AddComponent<SoundSettingsManager>();
                }
            }

            return instance;
        }
    }

    private static string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, SaveFolderName);
    private static string PlayerDataFilePath => Path.Combine(SaveDirectoryPath, PlayerDataFileName);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ClampAll();
        DeleteObsoleteLegacyVolumeFileIfPresent();
        LoadPersistedVolumesIfAny();
        applicationHasFocus = Application.isFocused;
        RefreshApplicationAudioOutput();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (instance != this)
        {
            return;
        }

        applicationHasFocus = hasFocus;
        RefreshApplicationAudioOutput();
    }

    public bool IsApplicationAudioOutputAllowed =>
        applicationHasFocus && !applicationPausedForPlatform;

    private void OnApplicationQuit()
    {
        if (instance == this)
        {
            SavePersistedVolumes();
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (instance != this)
        {
            return;
        }

        applicationPausedForPlatform = pause;
        RefreshApplicationAudioOutput();

        if (pause)
        {
            SavePersistedVolumes();
        }
    }

    private void RefreshApplicationAudioOutput()
    {
        AudioListener.pause = !IsApplicationAudioOutputAllowed;
    }

    public void SetMasterVolume(int value)
    {
        masterVolume = ClampVolume(value);
        SavePersistedVolumes();
    }

    public void SetSeVolume(int value)
    {
        seVolume = ClampVolume(value);
        SavePersistedVolumes();
    }

    public void SetBgmVolume(int value)
    {
        bgmVolume = ClampVolume(value);
        SavePersistedVolumes();
    }

    public int GetMasterVolume()
    {
        return masterVolume;
    }

    public int GetSeVolume()
    {
        return seVolume;
    }

    public int GetBgmVolume()
    {
        return bgmVolume;
    }

    public float GetSeGain01()
    {
        if (!IsApplicationAudioOutputAllowed)
        {
            return 0f;
        }

        return To01(masterVolume) * To01(seVolume) * Mathf.Max(0f, seBaseMultiplier);
    }

    public float GetBgmGain01()
    {
        if (!IsApplicationAudioOutputAllowed)
        {
            return 0f;
        }

        return To01(masterVolume) * To01(bgmVolume) * Mathf.Max(0f, bgmBaseMultiplier);
    }

    public bool GetDebugUnlockAllGames()
    {
        return debugUnlockAllGames;
    }

    public void SetDebugUnlockAllGames(bool enabled)
    {
        if (debugUnlockAllGames == enabled)
        {
            return;
        }

        debugUnlockAllGames = enabled;
        SavePersistedVolumes();
    }

    /// <summary>最後に <c>title_scene</c> で遊んだときのビルド Version。未記録なら空文字。</summary>
    public string GetRecordedBuildVersion()
    {
        return recordedBuildVersion ?? string.Empty;
    }

    /// <summary>
    /// <c>title_scene</c> でセーブ読込後に呼ぶ。現在のビルド Version と
    /// <see cref="recordedBuildVersion"/> が異なるとき（未記録含む）に上書き保存する。
    /// </summary>
    public void UpdateRecordedBuildVersionOnTitleEntry()
    {
        string current = GameBuildVersion.GetCurrentApplicationVersion();
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        if (string.Equals(recordedBuildVersion, current, StringComparison.Ordinal))
        {
            return;
        }

        recordedBuildVersion = current;
        SavePersistedVolumes();
    }

    public bool IsGame02UnlockedOnTitle()
    {
        return game01Cleared;
    }

    public bool IsGame03UnlockedOnTitle()
    {
        return game02Cleared;
    }

    public void MarkGame01Cleared()
    {
        if (game01Cleared)
        {
            return;
        }

        game01Cleared = true;
        SavePersistedVolumes();
    }

    public void MarkGame02Cleared()
    {
        if (game02Cleared)
        {
            return;
        }

        game02Cleared = true;
        SavePersistedVolumes();
    }

    public void MarkGame03Cleared()
    {
        if (game03Cleared)
        {
            return;
        }

        game03Cleared = true;
        SavePersistedVolumes();
    }

    private static int ClampVolume(int value)
    {
        return Mathf.Clamp(value, 0, 20);
    }

    private static float To01(int value)
    {
        return Mathf.Clamp(value, 0, 20) / 20f;
    }

    private void ClampAll()
    {
        masterVolume = ClampVolume(masterVolume);
        seVolume = ClampVolume(seVolume);
        bgmVolume = ClampVolume(bgmVolume);
        seBaseMultiplier = Mathf.Max(0f, seBaseMultiplier);
        bgmBaseMultiplier = Mathf.Max(0f, bgmBaseMultiplier);
    }

    private void ApplyDefaultVolumesWhenNoSave()
    {
        masterVolume = DefaultVolumeWhenNoSave;
        bgmVolume = DefaultVolumeWhenNoSave;
        seVolume = DefaultVolumeWhenNoSave;
        ClampAll();
    }

    /// <summary>
    /// <c>saveData/playerData.json</c> のみ読む。無いときは <see cref="DefaultVolumeWhenNoSave"/> を各チャンネルに適用する。
    /// </summary>
    private void LoadPersistedVolumesIfAny()
    {
        if (!File.Exists(PlayerDataFilePath))
        {
            ApplyDefaultVolumesWhenNoSave();
            return;
        }

        try
        {
            string json = File.ReadAllText(PlayerDataFilePath);
            if (TryReadPlayerData(json, out PersistedPlayerData playerData))
            {
                ApplyPlayerData(playerData);
            }
        }
        catch (IOException)
        {
        }
        catch (Exception)
        {
        }
    }

    private void SavePersistedVolumes()
    {
        try
        {
            PersistedPlayerData dto = BuildPersistedPlayerData();

            if (!Directory.Exists(SaveDirectoryPath))
            {
                Directory.CreateDirectory(SaveDirectoryPath);
            }

            string path = PlayerDataFilePath;
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(dto, true));
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);

            SteamSessionFileLogger.LogSaveWrite(
                PlayerDataFileName,
                path,
                SteamSessionFileLogger.SummarizePlayerDataSave(
                    dto.version,
                    dto.updatedAtUtc,
                    dto.buildVersion,
                    dto.master,
                    dto.bgm,
                    dto.se,
                    dto.game01Cleared,
                    dto.game02Cleared,
                    dto.game03Cleared,
                    dto.debugUnlockAllGames));
        }
        catch (IOException)
        {
        }
        catch (Exception)
        {
        }
    }

    private static void DeleteObsoleteLegacyVolumeFileIfPresent()
    {
        try
        {
            string legacyPath = Path.Combine(Application.persistentDataPath, ObsoleteLegacyVolumeFileName);
            if (File.Exists(legacyPath))
            {
                File.Delete(legacyPath);
            }
        }
        catch (IOException)
        {
        }
        catch (Exception)
        {
        }
    }

    private static bool TryReadPlayerData(string json, out PersistedPlayerData data)
    {
        data = default;
        PersistedPlayerData parsed = JsonUtility.FromJson<PersistedPlayerData>(json);
        if (parsed.version <= 0)
        {
            return false;
        }

        data = parsed;
        return true;
    }

    private void ApplyPlayerData(PersistedPlayerData data)
    {
        masterVolume = ClampVolume(data.master);
        bgmVolume = ClampVolume(data.bgm);
        seVolume = ClampVolume(data.se);
        game01Cleared = data.game01Cleared;
        game02Cleared = data.game02Cleared;
        game03Cleared = data.game03Cleared;
        debugUnlockAllGames = data.debugUnlockAllGames;
        recordedBuildVersion = data.buildVersion ?? string.Empty;
    }

    private PersistedPlayerData BuildPersistedPlayerData()
    {
        return new PersistedPlayerData
        {
            version = PlayerDataVersion,
            updatedAtUtc = DateTime.UtcNow.ToString("o"),
            buildVersion = recordedBuildVersion ?? string.Empty,
            master = masterVolume,
            bgm = bgmVolume,
            se = seVolume,
            game01Cleared = game01Cleared,
            game02Cleared = game02Cleared,
            game03Cleared = game03Cleared,
            debugUnlockAllGames = debugUnlockAllGames
        };
    }
}
