using UnityEngine;

/// <summary>
/// ホバー表示内容を供給する基本コンポーネント。
/// 呼び出し元オブジェクトに付けて文言を個別設定できる。
/// </summary>
public class Game02HoverContentProvider : MonoBehaviour
{
    [Header("Hover Text")]
    [SerializeField] private string title = "";
    [SerializeField] [TextArea(2, 6)] private string description = "";

    public virtual string GetTitle()
    {
        return title;
    }

    public virtual string GetDescription()
    {
        return description;
    }

    public virtual void SetContent(string newTitle, string newDescription)
    {
        title = newTitle ?? string.Empty;
        description = newDescription ?? string.Empty;
    }
}
