public static class SceneTransitionContext
{
    public static string StageName { get; set; } = "stage_01";
    public static string DestinationSceneName { get; set; }

    /// <summary>
    /// 会話 CSV（<c>Assets/GameData/Dialogue/{scene}-{stage}.csv</c> → DialogueScriptCatalog）の前半に使うシーン名。
    /// 未設定時は <see cref="DestinationSceneName"/> と同じ扱い（従来どおり）。
    /// game03 クリア→dialogue→menu03 のとき <c>DestinationSceneName</c> は <c>menu03_scene</c> のまま、
    /// CSV だけ <c>game03_scene-ed.csv</c> にしたい場合に <c>game03_scene</c> を設定する。
    /// </summary>
    public static string DialogueStreamingSceneNameOverride { get; set; }
}
