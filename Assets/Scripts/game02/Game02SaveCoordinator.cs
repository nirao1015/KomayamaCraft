using System.Collections.Generic;
using UnityEngine;

namespace Game02
{
    public sealed class Game02SaveCoordinator : MonoBehaviour
    {
        public static bool DidLoadSaveThisSession { get; private set; }

        [SerializeField] private bool enableAutoSave = true;
        [SerializeField] private float autoSaveIntervalSeconds = 10f;
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private bool saveOnPauseAndQuit = true;
        [SerializeField] private bool debugLog;

        private Game02SaveService saveService;
        private float autoSaveElapsed;
        private bool hasLoadedOnce;

        public static Game02SaveCoordinator EnsureSceneController()
        {
            Game02SaveCoordinator existing = FindObjectOfType<Game02SaveCoordinator>(true);
            if (existing != null)
            {
                return existing;
            }

            GameObject go = new GameObject("Game02SaveCoordinator");
            return go.AddComponent<Game02SaveCoordinator>();
        }

        private void Awake()
        {
            saveService = new Game02SaveService();
            DidLoadSaveThisSession = false;
            hasLoadedOnce = true;

            Game02StartMode startMode = Game02StartMode.LoadSaveIfAvailable;
            Game02StartContext.TryConsumeNextStartMode(out startMode);

            if (startMode == Game02StartMode.StartFreshAndDiscardSave)
            {
                bool deleted = saveService.TryDelete();
                if (debugLog)
                {
                    Debug.Log($"[Game02SaveCoordinator] Fresh start requested. Save delete {(deleted ? "succeeded" : "failed")}.");
                }

                Game02SceneLifecycleLog.SceneBootSaveResolution(
                    DidLoadSaveThisSession,
                    $"startMode={startMode} loadOnStart={loadOnStart} saveDeletedAttempt={(deleted ? "yes" : "no")}");
                return;
            }

            if (loadOnStart)
            {
                // Start より前にロードして、開始演出分岐や初期UI反映に間に合わせる。
                TryLoadAndApply();
            }

            Game02SceneLifecycleLog.SceneBootSaveResolution(
                DidLoadSaveThisSession,
                $"startMode={startMode} loadOnStart={loadOnStart}");
        }

        private void OnEnable()
        {
            WorkEditor.WorkCompleted += OnWorkEditorCompletedForProgress;
            WorkStreaming.WorkCompleted += OnWorkStreamingCompletedForProgress;
            WorkMovieUploadController.MovieUploadAccepted += OnMovieUploadAcceptedForProgress;
        }

        private void OnDisable()
        {
            WorkEditor.WorkCompleted -= OnWorkEditorCompletedForProgress;
            WorkStreaming.WorkCompleted -= OnWorkStreamingCompletedForProgress;
            WorkMovieUploadController.MovieUploadAccepted -= OnMovieUploadAcceptedForProgress;
        }

        private void OnWorkEditorCompletedForProgress()
        {
            Game02AlienProgressTracker.EnsureExists()?.NotifyWorkEditorComplete();
            OnMeaningfulProgress();
        }

        private void OnWorkStreamingCompletedForProgress()
        {
            Game02AlienProgressTracker.EnsureExists()?.NotifyWorkStreamingComplete();
            OnMeaningfulProgress();
        }

        private void OnMovieUploadAcceptedForProgress(int slotIndex)
        {
            Game02AlienProgressTracker.EnsureExists()?.NotifyWorkMovieAccepted(slotIndex);
            OnMeaningfulProgress();
        }

        private void Start()
        {
        }

        private void Update()
        {
            if (!enableAutoSave || !hasLoadedOnce)
            {
                return;
            }

            autoSaveElapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
            if (autoSaveElapsed < Mathf.Max(1f, autoSaveIntervalSeconds))
            {
                return;
            }

            autoSaveElapsed = 0f;
            SaveNow();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && saveOnPauseAndQuit)
            {
                SaveNow();
            }
        }

        private void OnApplicationQuit()
        {
            if (saveOnPauseAndQuit)
            {
                SaveNow();
            }
        }

        public bool TryLoadAndApply()
        {
            if (!saveService.TryLoad(out Game02SaveData data))
            {
                return false;
            }

            ApplyAll(data);
            DidLoadSaveThisSession = true;
            if (debugLog)
            {
                Debug.Log("[Game02SaveCoordinator] Load applied.");
            }

            return true;
        }

        public bool SaveNow()
        {
            Game02SaveData data = CaptureAll();
            bool saved = saveService.TrySave(data);
            if (debugLog)
            {
                Debug.Log($"[Game02SaveCoordinator] Save {(saved ? "succeeded" : "failed")}.");
            }

            return saved;
        }

        private void OnMeaningfulProgress()
        {
            if (!enableAutoSave)
            {
                return;
            }

            SaveNow();
            autoSaveElapsed = 0f;
        }

        private static Game02SaveData CaptureAll()
        {
            var data = new Game02SaveData();
            if (GameManager.Instance != null)
            {
                data.gameManagerState = GameManager.Instance.CaptureSaveState();
            }

            if (UpgradesManager.Instance != null)
            {
                data.upgradesState = UpgradesManager.Instance.CaptureSaveState();
            }

            data.worldState = CaptureWorldState();
            Game02AlienProgressTracker tracker = Game02AlienProgressTracker.Instance ?? Game02AlienProgressTracker.EnsureExists();
            data.alienProgress = tracker != null
                ? tracker.CaptureAlienProgressState()
                : new AlienProgressState();
            return data;
        }

        private static WorldState CaptureWorldState()
        {
            var world = new WorldState();
            world.looseItems = ItemStateCapture.CaptureLooseItems();

            // 起動直後や導線差で GameManager の初期化順が前後しても、
            // セーブ時点では WorkEditor 群を必ず捕捉できるようにする。
            WorkEditor.EnsureSceneWorkEditorsExist();

            EditorBuyController[] editorBuys = FindObjectsOfType<EditorBuyController>(true);
            for (int i = 0; i < editorBuys.Length; i++)
            {
                if (editorBuys[i] != null)
                {
                    world.editorBuys.Add(editorBuys[i].CaptureSaveState());
                }
            }

            WorkEditor[] editors = FindObjectsOfType<WorkEditor>(true);
            for (int i = 0; i < editors.Length; i++)
            {
                if (editors[i] != null)
                {
                    world.workEditors.Add(editors[i].CaptureSaveState());
                }
            }

            WorkEditorExt[] editorExts = FindObjectsOfType<WorkEditorExt>(true);
            for (int i = 0; i < editorExts.Length; i++)
            {
                if (editorExts[i] != null)
                {
                    world.workEditorExts.Add(editorExts[i].CaptureSaveState());
                }
            }

            WorkStreaming[] streamings = FindObjectsOfType<WorkStreaming>(true);
            for (int i = 0; i < streamings.Length; i++)
            {
                if (streamings[i] != null)
                {
                    world.workStreamings.Add(streamings[i].CaptureSaveState());
                }
            }

            WorkUpgradeSt01Controller[] st01s = FindObjectsOfType<WorkUpgradeSt01Controller>(true);
            for (int i = 0; i < st01s.Length; i++)
            {
                if (st01s[i] != null)
                {
                    world.workUpgradeWorkplaces.Add(st01s[i].CaptureSaveState());
                }
            }

            WorkUpgradeSt02Controller[] st02s = FindObjectsOfType<WorkUpgradeSt02Controller>(true);
            for (int i = 0; i < st02s.Length; i++)
            {
                if (st02s[i] != null)
                {
                    world.workUpgradeWorkplaces.Add(st02s[i].CaptureSaveState());
                }
            }

            WorkUpgradeSt03Controller[] st03s = FindObjectsOfType<WorkUpgradeSt03Controller>(true);
            for (int i = 0; i < st03s.Length; i++)
            {
                if (st03s[i] != null)
                {
                    world.workUpgradeWorkplaces.Add(st03s[i].CaptureSaveState());
                }
            }

            WorkUpgradeSt04Controller[] st04s = FindObjectsOfType<WorkUpgradeSt04Controller>(true);
            for (int i = 0; i < st04s.Length; i++)
            {
                if (st04s[i] != null)
                {
                    world.workUpgradeWorkplaces.Add(st04s[i].CaptureSaveState());
                }
            }

            WorkMovieSlotController[] slots = FindObjectsOfType<WorkMovieSlotController>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    world.workMovieSlots.Add(slots[i].CaptureSaveState());
                }
            }

            WorkMovieUploadController upload = FindObjectOfType<WorkMovieUploadController>(true);
            if (upload != null)
            {
                world.workMovieUpload = upload.CaptureSaveState();
            }

            return world;
        }

        private static void ApplyAll(Game02SaveData data)
        {
            if (data == null)
            {
                return;
            }

            if (GameManager.Instance != null && data.gameManagerState != null)
            {
                GameManager.Instance.ApplySaveState(data.gameManagerState);
            }

            if (UpgradesManager.Instance != null && data.upgradesState != null)
            {
                UpgradesManager.Instance.ApplySaveState(data.upgradesState);
            }

            ApplyWorldState(data.worldState);
            Game02AlienProgressTracker alienTracker = Game02AlienProgressTracker.EnsureExists();
            if (alienTracker != null)
            {
                alienTracker.ApplyAlienProgressState(data.alienProgress);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.PostLoadRefresh();
            }

            AlienParsonUnlockCoordinator unlockCoordinator =
                FindFirstObjectByType<AlienParsonUnlockCoordinator>(FindObjectsInactive.Include);
            unlockCoordinator?.RefreshAllSlots();

            PersonEffectManager.ScheduleBaselineRefreshAfterLayoutForAll();

            Parson01Controller.RefreshAllAfterSaveApplied();
            ParsonR02Controller.RefreshAllAfterSaveApplied();
            ParsonBPortraitControllerBase.RefreshAllAfterSaveApplied();
        }

        private static void ApplyWorldState(WorldState world)
        {
            if (world == null)
            {
                return;
            }

            // ロード適用時も対象コンポーネントの存在を先に保証する。
            WorkEditor.EnsureSceneWorkEditorsExist();

            // 先に枠開放状態を反映し、後続の WorkMovie 表示側が正しい開放数を参照できるようにする。
            var slotMap = new Dictionary<int, WorkMovieSlotState>();
            for (int i = 0; i < world.workMovieSlots.Count; i++)
            {
                WorkMovieSlotState state = world.workMovieSlots[i];
                if (state != null)
                {
                    slotMap[state.slotIndex] = state;
                }
            }

            WorkMovieSlotController[] slots = FindObjectsOfType<WorkMovieSlotController>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                WorkMovieSlotController slot = slots[i];
                if (slot != null && slotMap.TryGetValue(slot.SlotIndex, out WorkMovieSlotState state))
                {
                    slot.ApplySaveState(state);
                }
            }

            ItemStateCapture.ApplyLooseItems(world.looseItems);

            var buyMap = new Dictionary<string, EditorBuyState>();
            for (int i = 0; i < world.editorBuys.Count; i++)
            {
                EditorBuyState state = world.editorBuys[i];
                if (state != null && !string.IsNullOrEmpty(state.objectName))
                {
                    buyMap[state.objectName] = state;
                }
            }

            EditorBuyController[] editorBuys = FindObjectsOfType<EditorBuyController>(true);
            for (int i = 0; i < editorBuys.Length; i++)
            {
                EditorBuyController buy = editorBuys[i];
                if (buy != null && buyMap.TryGetValue(buy.name, out EditorBuyState state))
                {
                    buy.ApplySaveState(state);
                }
            }

            var editorMap = new Dictionary<string, WorkEditorState>();
            for (int i = 0; i < world.workEditors.Count; i++)
            {
                WorkEditorState state = world.workEditors[i];
                if (state != null && !string.IsNullOrEmpty(state.objectName))
                {
                    editorMap[state.objectName] = state;
                }
            }

            WorkEditor[] editors = FindObjectsOfType<WorkEditor>(true);
            for (int i = 0; i < editors.Length; i++)
            {
                WorkEditor editor = editors[i];
                if (editor != null && editorMap.TryGetValue(editor.name, out WorkEditorState state))
                {
                    editor.ApplySaveState(state);
                }
            }

            List<WorkEditorExtState> extStates = world.workEditorExts ?? new List<WorkEditorExtState>();
            var editorExtMap = new Dictionary<string, WorkEditorExtState>();
            for (int i = 0; i < extStates.Count; i++)
            {
                WorkEditorExtState state = extStates[i];
                if (state != null && !string.IsNullOrEmpty(state.objectName))
                {
                    editorExtMap[state.objectName] = state;
                }
            }

            WorkEditorExt[] editorExts = FindObjectsOfType<WorkEditorExt>(true);
            for (int i = 0; i < editorExts.Length; i++)
            {
                WorkEditorExt editorExt = editorExts[i];
                if (editorExt != null && editorExtMap.TryGetValue(editorExt.name, out WorkEditorExtState state))
                {
                    editorExt.ApplySaveState(state);
                }
            }

            var streamingMap = new Dictionary<string, WorkStreamingState>();
            for (int i = 0; i < world.workStreamings.Count; i++)
            {
                WorkStreamingState state = world.workStreamings[i];
                if (state != null && !string.IsNullOrEmpty(state.objectName))
                {
                    streamingMap[state.objectName] = state;
                }
            }

            WorkStreaming[] streamings = FindObjectsOfType<WorkStreaming>(true);
            for (int i = 0; i < streamings.Length; i++)
            {
                WorkStreaming streaming = streamings[i];
                if (streaming != null && streamingMap.TryGetValue(streaming.name, out WorkStreamingState state))
                {
                    streaming.ApplySaveState(state);
                }
            }

            var upgradeMap = new Dictionary<string, WorkUpgradeWorkplaceState>();
            for (int i = 0; i < world.workUpgradeWorkplaces.Count; i++)
            {
                WorkUpgradeWorkplaceState state = world.workUpgradeWorkplaces[i];
                if (state != null && !string.IsNullOrEmpty(state.objectName))
                {
                    upgradeMap[state.objectName] = state;
                }
            }

            WorkUpgradeSt01Controller[] st01s = FindObjectsOfType<WorkUpgradeSt01Controller>(true);
            for (int i = 0; i < st01s.Length; i++)
            {
                WorkUpgradeSt01Controller c = st01s[i];
                if (c != null && upgradeMap.TryGetValue(c.name, out WorkUpgradeWorkplaceState state))
                {
                    c.ApplySaveState(state);
                }
            }

            WorkUpgradeSt02Controller[] st02s = FindObjectsOfType<WorkUpgradeSt02Controller>(true);
            for (int i = 0; i < st02s.Length; i++)
            {
                WorkUpgradeSt02Controller c = st02s[i];
                if (c != null && upgradeMap.TryGetValue(c.name, out WorkUpgradeWorkplaceState state))
                {
                    c.ApplySaveState(state);
                }
            }

            WorkUpgradeSt03Controller[] st03s = FindObjectsOfType<WorkUpgradeSt03Controller>(true);
            for (int i = 0; i < st03s.Length; i++)
            {
                WorkUpgradeSt03Controller c = st03s[i];
                if (c != null && upgradeMap.TryGetValue(c.name, out WorkUpgradeWorkplaceState state))
                {
                    c.ApplySaveState(state);
                }
            }

            WorkUpgradeSt04Controller[] st04s = FindObjectsOfType<WorkUpgradeSt04Controller>(true);
            for (int i = 0; i < st04s.Length; i++)
            {
                WorkUpgradeSt04Controller c = st04s[i];
                if (c != null && upgradeMap.TryGetValue(c.name, out WorkUpgradeWorkplaceState state))
                {
                    c.ApplySaveState(state);
                }
            }

            WorkMovieUploadController upload = FindObjectOfType<WorkMovieUploadController>(true);
            if (upload != null && world.workMovieUpload != null)
            {
                upload.ApplySaveState(world.workMovieUpload);
            }

            FieldView.RefreshAllByCurrentUpgrades();
        }
    }
}
