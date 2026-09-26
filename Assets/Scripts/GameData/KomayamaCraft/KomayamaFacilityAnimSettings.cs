using UnityEngine;

/// <summary>
/// 設備稼働スプライトアニメの共通タイミング。
/// このアセットの1か所を変えれば、参照する全設備の待機時間が変わる。
/// </summary>
[CreateAssetMenu(
    fileName = "FacilityAnimSettings",
    menuName = "KomayamaCraft/Game Data/Facility Anim Settings")]
public sealed class KomayamaFacilityAnimSettings : ScriptableObject
{
    [SerializeField, Min(0.02f)]
    [Tooltip("稼働アニメの1コマあたりの待機秒数。全設備共通。")]
    private float secondsPerFrame = 0.12f;

    public float SecondsPerFrame => Mathf.Max(0.02f, secondsPerFrame);
}
