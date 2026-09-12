using UnityEngine;

// 既存プレハブ参照互換のため残す。新規は DraggableItemController を使用する。
public sealed class ItemMailCharaController : DraggableItemController
{
    private void Reset()
    {
        SetItemType(ItemType.ItemMailChara);
    }
}
