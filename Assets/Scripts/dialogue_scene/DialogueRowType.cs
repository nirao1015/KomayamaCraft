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
        /// <summary>Craft オーバーレイ用: 暗幕 α（mode = 0〜1）。</summary>
        Dim,
        /// <summary>Craft オーバーレイ用: カメラ移動（text = x,y / mode = 秒 / audio_key = size）。</summary>
        Camera,
        End
    }
}
