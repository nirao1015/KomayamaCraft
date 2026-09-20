using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 付けた SpriteRenderer は <see cref="KomayamaWorldLayerDrawOrder"/> の
    /// マテリアル強制差し替え対象外（Sorting Layer のみ揃える）。
    /// 演出・専用シェーダ用。詳細は spec/KomayamaCraft_描画バンド整理_詳細仕様.md §2.4。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaPreserveSpriteMaterial : MonoBehaviour
    {
    }
}
