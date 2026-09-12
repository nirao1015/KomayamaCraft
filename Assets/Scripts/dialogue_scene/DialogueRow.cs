namespace DialogueScene
{
    /// <summary>
    /// 会話台本 CSV の 1 データ行（ヘッダ行は含まない）。
    /// </summary>
    public readonly struct DialogueRow
    {
        public DialogueRow(
            DialogueRowType type,
            string speaker,
            string text,
            string slot,
            string spriteKey,
            string mode,
            string audioKey)
        {
            Type = type;
            Speaker = speaker ?? string.Empty;
            Text = text ?? string.Empty;
            Slot = slot ?? string.Empty;
            SpriteKey = spriteKey ?? string.Empty;
            Mode = mode ?? string.Empty;
            AudioKey = audioKey ?? string.Empty;
        }

        public DialogueRowType Type { get; }
        public string Speaker { get; }
        public string Text { get; }
        public string Slot { get; }
        public string SpriteKey { get; }
        public string Mode { get; }
        public string AudioKey { get; }
    }
}
