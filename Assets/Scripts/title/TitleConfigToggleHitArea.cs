using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 設定トグル行の当たり判定。文言・余白クリックでも紐づく Toggle を切り替える。
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleConfigToggleHitArea : MonoBehaviour
{
    [SerializeField] private Toggle targetToggle;
    [SerializeField] private Button hitButton;

    private void Awake()
    {
        if (hitButton == null)
        {
            hitButton = GetComponent<Button>();
        }

        if (hitButton != null)
        {
            hitButton.onClick.AddListener(ToggleTarget);
        }
    }

    private void OnDestroy()
    {
        if (hitButton != null)
        {
            hitButton.onClick.RemoveListener(ToggleTarget);
        }
    }

    private void ToggleTarget()
    {
        if (targetToggle == null)
        {
            return;
        }

        targetToggle.isOn = !targetToggle.isOn;
    }
}
