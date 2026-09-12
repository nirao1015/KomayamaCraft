using UnityEngine;

/// <summary>
/// menu_scene の StartButton 専用遷移コンテキスト設定。
/// MenuSceneController が存在する場合のみ参照される。
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuStartButtonTransitionSettings : MonoBehaviour
{
    [SerializeField]
    [Tooltip("有効時、StartButton 押下時の遷移先/ステージ名をこのコンポーネント値で上書きします。")]
    private bool useOverrides = true;

    [SerializeField]
    [Tooltip("StartButton 押下時に SceneTransitionContext.DestinationSceneName へ渡すシーン名。空なら MenuSceneController 側設定を使用。")]
    private string destinationSceneName = "game_stage_scene";

    [SerializeField]
    [Tooltip("StartButton 押下時に SceneTransitionContext.StageName へ渡すステージ名。空なら MenuSceneController 側設定を使用。")]
    private string stageName = "stage_01";

    public bool UseOverrides => useOverrides;
    public string DestinationSceneName => destinationSceneName;
    public string StageName => stageName;
}
