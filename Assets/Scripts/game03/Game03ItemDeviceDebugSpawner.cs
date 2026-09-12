using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// デバッグ用: Button を押して Obstacle 側の配置マーカー位置へフィールドアイテムを生成する。
/// </summary>
public sealed class Game03ItemDeviceDebugSpawner : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Transform obstaclePlaceMarker;
    [SerializeField] private Game03ItemDeviceFieldController fieldController;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(OnDebugSpawnClicked);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnDebugSpawnClicked);
        }
    }

    private void OnDebugSpawnClicked()
    {
        if (fieldController == null || obstaclePlaceMarker == null)
        {
            Debug.LogWarning("[Game03ItemDeviceDebugSpawner] Assign fieldController and obstaclePlaceMarker.");
            return;
        }

        fieldController.TrySpawnAtObstaclePlace(obstaclePlaceMarker, out _);
    }
}
