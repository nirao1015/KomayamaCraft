using UnityEngine;

/// <summary>
/// カーゴ／消費系フィールドアイテムの種別。取得時の効果分岐に使用する。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03FieldConsumableKindBinding : MonoBehaviour
{
    [SerializeField] private Game03CargoItemKind kind;

    public Game03CargoItemKind Kind => kind;

    public void Configure(Game03CargoItemKind cargoKind)
    {
        kind = cargoKind;
    }
}
