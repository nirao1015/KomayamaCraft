using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// デバッグ用: ButtonCargo 等から <see cref="Game03EmergencyCargoDropController.DebugRequestCargoDropNow"/> を呼ぶ。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03EmergencyCargoDebugButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Game03EmergencyCargoDropController cargoDropController;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(OnDebugCargoClicked);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnDebugCargoClicked);
        }
    }

    private void OnDebugCargoClicked()
    {
        if (cargoDropController == null)
        {
            Debug.LogWarning("[Game03EmergencyCargoDebugButton] Assign cargoDropController.");
            return;
        }

        cargoDropController.DebugRequestCargoDropNow();
    }
}
