namespace DialogueScene
{
    /// <summary>
    /// CSV の type 列に対応する種別（<see cref="DialogueRowType.Unknown"/> は出力に含めない）。
    /// </summary>
    public enum DialogueRowType
    {
        Unknown = 0,
        Line,
        Bg,
        Stand,
        Se,
        Bgm,
        BgmStop,
        FxShake,
        FxShakeStart,
        FxShakeStop,
        FxGame03EdFinale,
        End
    }
}
