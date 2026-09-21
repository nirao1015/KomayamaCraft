using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// タイトル入場時のビルド版記録など、タイトル固有の入口処理。
/// 設定音量 UI は <see cref="ConfigVolumeUi"/>（ConfigCanvas プレハブ）側。
/// </summary>
public class TitleSceneController : MonoBehaviour
{
    private void Start()
    {
        SoundSettingsManager.Instance?.UpdateRecordedBuildVersionOnTitleEntry();
    }
}
