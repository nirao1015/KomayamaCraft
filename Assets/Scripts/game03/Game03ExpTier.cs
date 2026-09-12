using System;
using UnityEngine;

/// <summary>
/// 経験値ドロップの見た目段階（緑・黄・赤・紫の4種）。敵種別などから決まる。
/// </summary>
public enum Game03ExpTier
{
    Green = 0,
    Yellow = 1,
    Red = 2,
    Purple = 3
}

/// <summary>経験値ドロップがユニット中心へホーミングするときの移動の決め方（<see cref="Game03ExperienceFieldController"/> で指定）。</summary>
public enum Game03ExpHomingMoveStyle
{
    [Tooltip("等速: 毎フレーム一定の速さ（px/s）でユニット中心へ進む。")]
    ConstantSpeed = 0,
    [Tooltip("加速: ホーミング開始から徐々に速くなり、上限速度で頭打ち（加速度は別パラメータ）。")]
    AccelerateAlongPath = 1,
    [Tooltip("手前減速: ユニット中心付近ほど遅くなり、吸い込まれる感じを出す。")]
    SlowNearCore = 2
}

/// <summary>
/// 1ティア分の見た目。Inspector で差し替え可能。
/// </summary>
[Serializable]
public struct Game03ExpTierPresentation
{
    [Tooltip("経験値 Image のティント色。")]
    public Color color;

    [Tooltip("表示スプライト。未設定（null）のときはプレハブ既定の sprite を使う。")]
    public Sprite sprite;
}
