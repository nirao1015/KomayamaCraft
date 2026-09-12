namespace Game02
{
    /// <summary>
    /// 序盤の動画編集所要時間を短縮する特別措置（UG・編集者性能とは別枠）。
    /// 1 回目は <see cref="GameManager.TryConsumeFirstEditWorkSeconds"/> の固定秒。2〜6 回目のみ最終秒数に乗算。7 回目以降は適用しない。
    /// </summary>
    public static class Game02EarlyEditDuration
    {
        /// <summary>
        /// 生涯の編集仕事開始回数（1 始まり）。2〜6 のときだけ <paramref name="durationMultiplier"/> を返す。
        /// </summary>
        public static bool TryGetSessionDurationMultiplier(int lifetimeEditWorkSessionIndex1Based, out float durationMultiplier)
        {
            durationMultiplier = 1f;
            switch (lifetimeEditWorkSessionIndex1Based)
            {
                case 2:
                    durationMultiplier = 0.15f;
                    return true;
                case 3:
                    durationMultiplier = 0.30f;
                    return true;
                case 4:
                    durationMultiplier = 0.45f;
                    return true;
                case 5:
                    durationMultiplier = 0.60f;
                    return true;
                case 6:
                    durationMultiplier = 0.75f;
                    return true;
                default:
                    return false;
            }
        }
    }
}
