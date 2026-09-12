using UnityEngine;

namespace KomayamaCraft
{
    public static class KomayamaBootRequest
    {
#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnableEditorPlayWhenUnfocused()
        {
            Application.runInBackground = true;
        }
#endif

        public static bool StartNewGame;
        public static bool ContinueFromTitle;
        public static int SelectedSlot;

        public static void RequestNewGame(int slot)
        {
            StartNewGame = true;
            ContinueFromTitle = false;
            SelectedSlot = KomayamaSaveSlots.ClampSlot(slot);
            KomayamaSaveSlots.WriteBootRequest(true, SelectedSlot);
        }

        public static void RequestContinue(int slot)
        {
            StartNewGame = false;
            ContinueFromTitle = true;
            SelectedSlot = KomayamaSaveSlots.ClampSlot(slot);
            KomayamaSaveSlots.WriteBootRequest(false, SelectedSlot);
        }

        public static void ConsumeBootFlags()
        {
            StartNewGame = false;
            ContinueFromTitle = false;
            SelectedSlot = 0;
        }
    }
}
