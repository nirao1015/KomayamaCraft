using UnityEngine;

namespace Game02
{
    /// <summary>
    /// WorkMovie_1..5 の有効/休眠を制御する。
    /// </summary>
    public sealed class WorkMovieSlotController : MonoBehaviour
    {
        [SerializeField] private bool startDormant;
        [SerializeField] private int slotIndex;
        [SerializeField] private GameObject invalidView;
        [SerializeField] private bool isWorkplaceAvailable;

        public int SlotIndex => slotIndex;
        public bool IsWorkplaceAvailable => isWorkplaceAvailable;

        public static void EnsureSceneWorkMovieSlotsExist()
        {
            EnsureOne("UnitCanvas/WorkMovie_1", 1);
            EnsureOne("UnitCanvas/WorkMovie_2", 2);
            EnsureOne("UnitCanvas/WorkMovie_3", 3);
            EnsureOne("UnitCanvas/WorkMovie_4", 4);
            EnsureOne("UnitCanvas/WorkMovie_5", 5);
            EnsureUploadControllersOnAllSlots();
        }

        public static void UnlockBySlotIndex(int targetSlotIndex)
        {
            if (targetSlotIndex <= 0)
            {
                return;
            }

            WorkMovieSlotController[] slots = Object.FindObjectsOfType<WorkMovieSlotController>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                WorkMovieSlotController slot = slots[i];
                if (slot != null && slot.slotIndex == targetSlotIndex)
                {
                    slot.Unlock();
                    return;
                }
            }

            GameObject go = FindInactiveObjectByName($"WorkMovie_{targetSlotIndex}");
            if (go == null)
            {
                return;
            }

            WorkMovieSlotController added = go.GetComponent<WorkMovieSlotController>();
            if (added == null)
            {
                added = go.AddComponent<WorkMovieSlotController>();
            }

            if (added != null)
            {
                added.slotIndex = targetSlotIndex;
                added.Unlock();
            }
        }

        private static void EnsureOne(string objectPath, int index)
        {
            GameObject go = GameObject.Find(objectPath);
            if (go == null)
            {
                go = FindInactiveObjectByName($"WorkMovie_{index}");
            }

            if (go == null)
            {
                return;
            }

            WorkMovieSlotController existing = go.GetComponent<WorkMovieSlotController>();
            if (existing == null)
            {
                existing = go.AddComponent<WorkMovieSlotController>();
            }

            if (existing != null)
            {
                existing.slotIndex = index;
                existing.ResolveInvalidViewIfNeeded();
                bool defaultAvailable = index <= 2;
                bool initializedAvailability = existing.isWorkplaceAvailable || !existing.startDormant;
                bool shouldBeAvailable = initializedAvailability ? existing.isWorkplaceAvailable : defaultAvailable;
                existing.SetAvailable(shouldBeAvailable, false);
            }
        }

        private static GameObject FindInactiveObjectByName(string objectName)
        {
            Transform[] all = Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name == objectName)
                {
                    return t.gameObject;
                }
            }

            return null;
        }

        private static void EnsureUploadControllersOnAllSlots()
        {
            for (int i = 1; i <= 5; i++)
            {
                GameObject slot = GameObject.Find($"UnitCanvas/WorkMovie_{i}");
                if (slot == null)
                {
                    slot = FindInactiveObjectByName($"WorkMovie_{i}");
                }

                if (slot == null)
                {
                    continue;
                }

                if (slot.GetComponent<WorkMovieUploadController>() == null)
                {
                    slot.AddComponent<WorkMovieUploadController>();
                }
            }
        }

        private void Awake()
        {
            if (slotIndex <= 0)
            {
                slotIndex = ParseSlotIndexFromName(gameObject.name);
            }

            if (slotIndex == 1 || slotIndex == 2)
            {
                startDormant = false;
            }
            else if (slotIndex >= 3)
            {
                bool keepCurrentUnlockedState = isWorkplaceAvailable && !startDormant;
                if (!keepCurrentUnlockedState)
                {
                    bool unlockedByUpgrade = UpgradesManager.Instance != null &&
                        UpgradesManager.Instance.GetMaxUploadSlotCount() >= slotIndex;
                    startDormant = !unlockedByUpgrade;
                }
            }

            ResolveInvalidViewIfNeeded();
            SetAvailable(!startDormant, false);
        }

        public void Unlock()
        {
            SetAvailable(true);
        }

        public void SetAvailable(bool available)
        {
            SetAvailable(available, true);
        }

        public void SetAvailable(bool available, bool activateObject)
        {
            startDormant = !available;
            isWorkplaceAvailable = available;
            if (activateObject && available && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // 仕様: WorkMovie_4/5 は未購入時は仕事場自体を非表示。
            if (slotIndex >= 4 && !available && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            RefreshInvalidView();
        }

        public void DisableInvalidViewByUpgrade()
        {
            SetAvailable(true);
        }

        // 再ロック仕様を導入する場合にここへ実処理を追加する。
        public void SetUnavailableReserved()
        {
        }

        public void RefreshInvalidView()
        {
            if (invalidView != null)
            {
                invalidView.SetActive(!isWorkplaceAvailable);
            }

            if (Game02DebugManager.ShouldLogInvalidViewRefresh())
            {
                Debug.Log($"[WorkMovieSlotController] InvalidView active={!isWorkplaceAvailable} available={isWorkplaceAvailable} object={name}");
            }
        }

        private static int ParseSlotIndexFromName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return 0;
            }

            const string prefix = "WorkMovie_";
            if (!objectName.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return 0;
            }

            string suffix = objectName.Substring(prefix.Length);
            return int.TryParse(suffix, out int parsed) ? parsed : 0;
        }

        private void ResolveInvalidViewIfNeeded()
        {
            if (invalidView != null)
            {
                return;
            }

            Transform t = transform.Find("InvalidView");
            if (t != null)
            {
                invalidView = t.gameObject;
            }
        }

        public WorkMovieSlotState CaptureSaveState()
        {
            return new WorkMovieSlotState
            {
                slotIndex = slotIndex,
                isWorkplaceAvailable = isWorkplaceAvailable
            };
        }

        public void ApplySaveState(WorkMovieSlotState state)
        {
            if (state == null || state.slotIndex != slotIndex)
            {
                return;
            }

            SetAvailable(state.isWorkplaceAvailable, true);
        }
    }
}
