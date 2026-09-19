using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// オートプレイ用の疑似入力ブリッジ。捏造完了ではなく、既存進行経路へ差し込む。
    /// </summary>
    public static class KomayamaCraftAutoPlayInput
    {
        public static bool IsActive { get; private set; }

        public static bool HoldW { get; set; }
        public static bool HoldA { get; set; }
        public static bool HoldS { get; set; }
        public static bool HoldD { get; set; }

        private static bool dialogueAdvancePending;
        private static Vector2? forcedWorldPointer;

        public static void BeginSession()
        {
            IsActive = true;
            ClearEphemeral();
            HoldW = HoldA = HoldS = HoldD = false;
        }

        public static void EndSession()
        {
            IsActive = false;
            ClearEphemeral();
            HoldW = HoldA = HoldS = HoldD = false;
        }

        public static void ClearEphemeral()
        {
            dialogueAdvancePending = false;
            forcedWorldPointer = null;
        }

        public static void RequestDialogueAdvance()
        {
            if (!IsActive)
            {
                return;
            }

            dialogueAdvancePending = true;
        }

        public static bool ConsumeDialogueAdvance()
        {
            if (!dialogueAdvancePending)
            {
                return false;
            }

            dialogueAdvancePending = false;
            return true;
        }

        public static void SetForcedWorldPointer(Vector2 world)
        {
            forcedWorldPointer = world;
        }

        public static void ClearForcedWorldPointer()
        {
            forcedWorldPointer = null;
        }

        public static bool TryGetForcedWorldPointer(out Vector2 world)
        {
            if (forcedWorldPointer.HasValue)
            {
                world = forcedWorldPointer.Value;
                return true;
            }

            world = default;
            return false;
        }
    }
}
