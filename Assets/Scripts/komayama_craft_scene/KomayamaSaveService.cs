using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class KomayamaSaveService : MonoBehaviour
    {
        [SerializeField] private KomayamaHandInventory hand;
        [SerializeField] private KomayamaDropArea dropArea;
        [SerializeField] private KomayamaBuildController buildController;
        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private Transform droppedItemRoot;
        [SerializeField] private bool saveOnF5 = true;
        [SerializeField] private bool loadOnF9 = true;
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private bool autosaveOnQuit = true;
        [SerializeField, Min(10f)] private float autosaveSeconds = 120f;

        private float nextAutosaveAt;

        public bool DidLoadExistingSave { get; private set; }

        public static string SaveDirectory => KomayamaSaveSlots.SaveDirectory;

        public static string SaveFilePath =>
            KomayamaSaveSlots.SaveFilePath(KomayamaSaveSlots.ActiveSlot);

        public static string BackupFilePath =>
            KomayamaSaveSlots.BackupFilePath(KomayamaSaveSlots.ActiveSlot);

        public static string TempFilePath =>
            KomayamaSaveSlots.TempFilePath(KomayamaSaveSlots.ActiveSlot);

        private void Start()
        {
            nextAutosaveAt = Time.unscaledTime + autosaveSeconds;
            KomayamaSaveSlots.MigrateLegacyIfNeeded();
            bool startNew = KomayamaBootRequest.StartNewGame;
            bool continueFromTitle = KomayamaBootRequest.ContinueFromTitle;
            int selectedSlot = KomayamaBootRequest.SelectedSlot > 0
                ? KomayamaBootRequest.SelectedSlot
                : KomayamaSaveSlots.LastPlayedSlot;
            if (KomayamaSaveSlots.TryConsumeBootRequest(out bool bootNew, out int bootSlot))
            {
                startNew = bootNew;
                continueFromTitle = !bootNew;
                selectedSlot = bootSlot;
            }

            KomayamaBootRequest.ConsumeBootFlags();
            if (startNew)
            {
                KomayamaSaveSlots.PrepareNewGame(selectedSlot);
                DidLoadExistingSave = false;
                hud?.ShowMessage($"新規ゲームを開始した（スロット{KomayamaSaveSlots.ActiveSlot}）");
                return;
            }

            KomayamaSaveSlots.SetActiveSlot(selectedSlot);
            if ((loadOnStart || continueFromTitle) &&
                (File.Exists(SaveFilePath) || File.Exists(BackupFilePath)))
            {
                if (TryLoadFrom(SaveFilePath))
                {
                    DidLoadExistingSave = true;
                    hud?.ShowMessage($"ロードしました（スロット{KomayamaSaveSlots.ActiveSlot}）");
                }
                else if (TryLoadFrom(BackupFilePath))
                {
                    DidLoadExistingSave = true;
                    hud?.ShowMessage($"バックアップからロードしました（スロット{KomayamaSaveSlots.ActiveSlot}）");
                }
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (saveOnF5 && keyboard.f5Key.wasPressedThisFrame)
                {
                    TrySave();
                }

                if (loadOnF9 && keyboard.f9Key.wasPressedThisFrame)
                {
                    TryLoad();
                }
            }

            if (autosaveSeconds > 0f && Time.unscaledTime >= nextAutosaveAt)
            {
                nextAutosaveAt = Time.unscaledTime + autosaveSeconds;
                TrySaveSilent();
            }
        }

        private void OnApplicationQuit()
        {
            if (autosaveOnQuit)
            {
                TrySaveSilent();
            }
        }

        public bool TrySave()
        {
            if (!IsCraftSaveAllowed())
            {
                hud?.ShowMessage("まだセーブできない");
                return false;
            }

            bool saved = TrySaveSilent();
            hud?.ShowMessage(saved ? "セーブしました" : "セーブに失敗しました");
            return saved;
        }

        public bool TrySaveSilent()
        {
            if (!IsCraftSaveAllowed())
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(SaveDirectory);
                KomayamaCraftSaveData data = Capture();
                data.version = KomayamaCraftSaveData.CurrentVersion;
                data.buildVersion = Application.version;
                data.updatedAtUtc = DateTime.UtcNow.ToString("o");
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(TempFilePath, json);
                if (File.Exists(SaveFilePath))
                {
                    File.Copy(SaveFilePath, BackupFilePath, true);
                }

                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                }

                File.Move(TempFilePath, SaveFilePath);
                KomayamaSaveSlots.RememberLastPlayedSlot(KomayamaSaveSlots.ActiveSlot);
                TryCaptureSlotThumbnail(KomayamaSaveSlots.ActiveSlot);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[KomayamaCraft] Save failed: {exception.Message}");
                return false;
            }
        }

        private static bool IsCraftSaveAllowed()
        {
            KomayamaQuestController quest = KomayamaQuestController.Instance;
            return quest == null || quest.AreBuildAndSettingsUnlocked;
        }

        private static void TryCaptureSlotThumbnail(int slot)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(SaveDirectory);
                int w = Mathf.Clamp(cam.pixelWidth / 2, 160, 640);
                int h = Mathf.Clamp(cam.pixelHeight / 2, 90, 360);
                if (w < 16 || h < 16)
                {
                    return;
                }

                RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
                RenderTexture previous = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = previous;

                RenderTexture prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply(false, false);
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);

                byte[] png = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);
                File.WriteAllBytes(KomayamaSaveSlots.ThumbnailFilePath(slot), png);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[KomayamaCraft] Slot thumbnail capture failed: {exception.Message}");
            }
        }

        public bool TryLoad()
        {
            if (TryLoadFrom(SaveFilePath))
            {
                hud?.ShowMessage("ロードしました");
                return true;
            }

            if (TryLoadFrom(BackupFilePath))
            {
                hud?.ShowMessage("バックアップからロードしました");
                return true;
            }

            hud?.ShowMessage("セーブデータを読めませんでした");
            return false;
        }

        private bool TryLoadFrom(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                KomayamaCraftSaveData data =
                    JsonUtility.FromJson<KomayamaCraftSaveData>(File.ReadAllText(path));
                if (data == null)
                {
                    return false;
                }

                data.EnsureCollections();
                Apply(data);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[KomayamaCraft] Load failed: {exception.Message}");
                return false;
            }
        }

        public KomayamaCraftSaveData Capture()
        {
            var data = new KomayamaCraftSaveData();
            data.EnsureCollections();
            data.gameplayElapsedSeconds = Time.time;
            if (hand != null)
            {
                data.handInventory.capacity = hand.Capacity;
                if (!hand.IsEmpty)
                {
                    hand.CaptureStacks(data.handInventory.items);
                }
            }

            KomayamaDroppedItem[] dropped = FindObjectsByType<KomayamaDroppedItem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < dropped.Length; i++)
            {
                KomayamaDroppedItem item = dropped[i];
                if (item == null || item.Item == null)
                {
                    continue;
                }

                data.groundItems.Add(new GroundItemSaveDto
                {
                    itemDefinitionId = item.Item.DefinitionId,
                    instanceId = item.InstanceId,
                    position = new Float2SaveDto(item.DropCenter.x, item.DropCenter.y),
                    amount = item.Amount
                });
            }

            KomayamaProcessingFacility[] processors = FindObjectsByType<KomayamaProcessingFacility>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < processors.Length; i++)
            {
                if (processors[i] == null)
                {
                    continue;
                }

                var dto = new FacilitySaveDto();
                processors[i].CaptureSave(dto);
                data.facilities.Add(dto);
            }

            KomayamaStorageFacility[] storages = FindObjectsByType<KomayamaStorageFacility>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < storages.Length; i++)
            {
                if (storages[i] == null)
                {
                    continue;
                }

                var dto = new FacilitySaveDto();
                storages[i].CaptureSave(dto);
                data.facilities.Add(dto);
            }

            KomayamaGhost[] ghosts = FindObjectsByType<KomayamaGhost>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < ghosts.Length; i++)
            {
                if (ghosts[i] == null)
                {
                    continue;
                }

                var dto = new GhostSaveDto();
                ghosts[i].CaptureSave(dto);
                data.ghosts.Add(dto);
            }

            KomayamaProgressService progress = FindFirstObjectByType<KomayamaProgressService>();
            progress?.CaptureSave(data);
            FindFirstObjectByType<KomayamaShip>()?.CaptureSave(data.ship);
            FindFirstObjectByType<KCMouseFoxFollower>()?.CaptureSave(data.foxFollower);
            FindFirstObjectByType<KomayamaCraftCameraController>()?.CaptureSave(data.cameraView);
            FindFirstObjectByType<KomayamaQuestController>()?.CaptureSave(data);
            return data;
        }

        public void Apply(KomayamaCraftSaveData data)
        {
            if (hand != null)
            {
                hand.Restore(data.handInventory.items);
            }

            KomayamaDroppedItem[] existingDrops = FindObjectsByType<KomayamaDroppedItem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < existingDrops.Length; i++)
            {
                if (existingDrops[i] != null)
                {
                    Destroy(existingDrops[i].gameObject);
                }
            }

            for (int i = 0; i < data.groundItems.Count; i++)
            {
                GroundItemSaveDto ground = data.groundItems[i];
                if (!GameDataCatalogs.KomayamaItems.TryGet(ground.itemDefinitionId, out ItemDefinition item))
                {
                    continue;
                }

                Vector2 position = new(ground.position.x, ground.position.y);
                if (dropArea != null)
                {
                    dropArea.TrySpawnForLoad(
                        item,
                        Mathf.Max(1, ground.amount),
                        position,
                        ground.instanceId,
                        out _);
                }
            }

            ApplyFacilities(data);
            ApplyGhosts(data);
            FindFirstObjectByType<KomayamaProgressService>()?.ApplySave(data);
            FindFirstObjectByType<KomayamaShip>()?.ApplySave(data.ship);
            FindFirstObjectByType<KCMouseFoxFollower>()?.ApplySave(data.foxFollower);
            FindFirstObjectByType<KomayamaCraftCameraController>()?.ApplySave(data.cameraView);
            FindFirstObjectByType<KomayamaQuestController>()?.ApplySave(data);
        }

        private void ApplyFacilities(KomayamaCraftSaveData data)
        {
            KomayamaProcessingFacility[] processors = FindObjectsByType<KomayamaProcessingFacility>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < processors.Length; i++)
            {
                if (processors[i] != null)
                {
                    Destroy(processors[i].gameObject);
                }
            }

            KomayamaStorageFacility[] storages = FindObjectsByType<KomayamaStorageFacility>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < storages.Length; i++)
            {
                if (storages[i] != null)
                {
                    Destroy(storages[i].gameObject);
                }
            }

            if (buildController == null)
            {
                return;
            }

            for (int i = 0; i < data.facilities.Count; i++)
            {
                FacilitySaveDto dto = data.facilities[i];
                if (!GameDataCatalogs.KomayamaFacilities.TryGet(
                        dto.facilityDefinitionId,
                        out FacilityDefinition definition))
                {
                    continue;
                }

                Vector2 position = new(dto.position.x, dto.position.y);
                GameObject created = buildController.SpawnFacility(definition, position, dto.instanceId);
                if (created == null)
                {
                    continue;
                }

                if (created.TryGetComponent(out KomayamaProcessingFacility processing))
                {
                    processing.ApplySave(dto);
                }

                if (created.TryGetComponent(out KomayamaStorageFacility storage))
                {
                    storage.ApplySave(dto);
                }
            }
        }

        private void ApplyGhosts(KomayamaCraftSaveData data)
        {
            KomayamaGhost[] existing = FindObjectsByType<KomayamaGhost>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null)
                {
                    Destroy(existing[i].gameObject);
                }
            }

            if (buildController == null || data.ghosts == null)
            {
                return;
            }

            for (int i = 0; i < data.ghosts.Count; i++)
            {
                GhostSaveDto dto = data.ghosts[i];
                if (!GameDataCatalogs.KomayamaFacilities.TryGet(
                        dto.facilityDefinitionId,
                        out FacilityDefinition definition))
                {
                    continue;
                }

                Vector2 position = new(dto.position.x, dto.position.y);
                GameObject created = buildController.SpawnFacility(definition, position, dto.instanceId);
                if (created != null && created.TryGetComponent(out KomayamaGhost ghost))
                {
                    ghost.ApplySave(dto);
                }
            }
        }
    }
}
