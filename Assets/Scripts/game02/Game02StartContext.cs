namespace Game02
{
    public enum Game02StartMode
    {
        LoadSaveIfAvailable = 0,
        StartFreshAndDiscardSave = 1
    }

    public static class Game02StartContext
    {
        private static bool hasNextStartMode;
        private static Game02StartMode nextStartMode = Game02StartMode.LoadSaveIfAvailable;

        public static void SetNextStartMode(Game02StartMode mode)
        {
            nextStartMode = mode;
            hasNextStartMode = true;
        }

        public static bool TryConsumeNextStartMode(out Game02StartMode mode)
        {
            if (!hasNextStartMode)
            {
                mode = Game02StartMode.LoadSaveIfAvailable;
                return false;
            }

            mode = nextStartMode;
            hasNextStartMode = false;
            nextStartMode = Game02StartMode.LoadSaveIfAvailable;
            return true;
        }
    }
}
