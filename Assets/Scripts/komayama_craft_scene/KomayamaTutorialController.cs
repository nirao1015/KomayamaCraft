using UnityEngine;
using UnityEngine.InputSystem;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    public sealed class KomayamaTutorialController : MonoBehaviour
    {
        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private KomayamaHandInventory hand;
        [SerializeField] private bool runOnNewGame = false;
        [SerializeField] private TMPro.TMP_Text guideExtraText;

        private int step;
        private bool skipped;
        private bool completed;
        private bool sessionIsNewGame;

        public bool IsActive => !skipped && !completed && step >= 0;

        private void Awake()
        {
            sessionIsNewGame = KomayamaBootRequest.StartNewGame;
        }

        private void Start()
        {
            KomayamaSaveService save = FindFirstObjectByType<KomayamaSaveService>();
            bool loadedExistingSave = save != null && save.DidLoadExistingSave;
            if (runOnNewGame && loadedExistingSave && !sessionIsNewGame)
            {
                skipped = true;
                return;
            }

            step = 0;
            ShowCurrent();
        }

        private void Update()
        {
            if (skipped || completed)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
            {
                Skip();
                return;
            }

            AdvanceIfReady();
        }

        public void Skip()
        {
            skipped = true;
            completed = true;
            hud?.ShowMessage("案内をスキップした");
            SetExtra("Tで案内をスキップ済み");
        }

        private void AdvanceIfReady()
        {
            bool ready = step switch
            {
                0 => FindObjectsByType<KomayamaDroppedItem>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Length > 0,
                1 => hand != null && !hand.IsEmpty,
                2 => HasProcessingInput(),
                3 => HasProcessingOutput(),
                4 => FindFirstObjectByType<KomayamaBuildController>() != null &&
                     FindFirstObjectByType<KomayamaBuildController>().Mode !=
                     KomayamaInputMode.Field,
                5 => HasWorkingGhost(),
                _ => false
            };

            if (!ready)
            {
                return;
            }

            step++;
            if (step > 5)
            {
                completed = true;
                hud?.ShowMessage("基本操作の案内はここまで");
                SetExtra(string.Empty);
                return;
            }

            ShowCurrent();
        }

        private void ShowCurrent()
        {
            string text = step switch
            {
                0 => "案内1: 発生点を左クリックして素材を地面へ出す",
                1 => "案内2: 地面の素材を左クリックで手に持つ",
                2 => "案内3: 設備の上で右クリックして投入する",
                3 => "案内4: 加工が終わったら左クリックで回収する",
                4 => "案内5: Bで建設、WASDとホイールで視点",
                5 => "案内6: Ghostをクリックして稼働。Tでスキップ可",
                _ => string.Empty
            };
            hud?.ShowMessage(text);
            SetExtra(text + "  （Tでスキップ）");
        }

        private static bool HasProcessingInput()
        {
            KomayamaProcessingFacility[] facilities = FindObjectsByType<KomayamaProcessingFacility>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < facilities.Length; i++)
            {
                if (facilities[i] != null && facilities[i].InputAmount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasProcessingOutput()
        {
            KomayamaProcessingFacility[] facilities = FindObjectsByType<KomayamaProcessingFacility>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < facilities.Length; i++)
            {
                if (facilities[i] != null &&
                    facilities[i].State == KomayamaFacilityState.Processing)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasWorkingGhost()
        {
            KomayamaGhost[] ghosts = FindObjectsByType<KomayamaGhost>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < ghosts.Length; i++)
            {
                if (ghosts[i] != null && ghosts[i].IsEnabled)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetExtra(string text)
        {
            if (guideExtraText != null)
            {
                guideExtraText.text = text;
            }
        }
    }
}
