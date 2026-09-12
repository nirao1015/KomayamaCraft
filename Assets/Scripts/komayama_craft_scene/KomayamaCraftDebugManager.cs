using UnityEngine;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaCraftDebugManager : MonoBehaviour
    {
        private const string NoDropPaintLayerName = "DropBlocker";
        private const string NoBuildPaintLayerName = "PlacementBlocker";

        [Header("本番")]
        [SerializeField, InspectorName("本番リリース用"), Tooltip(
            "ON のとき、下の開発用チェックはすべて無効になる。")]
        private bool productionReleaseBuild;

        [Header("開発・デバッグ（本番リリース用が ON のときは無効）")]
        [SerializeField, InspectorName("デバッグ表示を出す"), Tooltip(
            "ON のときデバッグオーバーレイを表示する。")]
        private bool showDebugOverlay = true;

        [SerializeField, InspectorName("ドロップ禁止ペイントを表示"), Tooltip(
            "ON のとき、アイテムを置けない場所のペイントをカメラに映す。")]
        private bool showNoDropPaint = true;

        [SerializeField, InspectorName("設置禁止ペイントを表示"), Tooltip(
            "ON のとき、施設を置けない場所のペイントをカメラに映す。")]
        private bool showNoBuildPaint = true;

        [SerializeField, InspectorName("ドロップ詳細ログ"), Tooltip(
            "ON のとき、ドロップ処理の詳細を Console に出す。")]
        private bool verboseDropLogging;

        [SerializeField, InspectorName("長押しで超速連打"), Tooltip(
            "ON のとき、左クリック長押しの回収・採集の連打間隔を極端に短くする。右クリックのドロップ間隔は変えない。")]
        private bool rapidHoldDrop;

        [Header("参照")]
        [SerializeField, Tooltip("デバッグオーバーレイのルート。")]
        private GameObject debugOverlay;

        [SerializeField, Tooltip("ペイント表示の切り替えに使うゲームカメラ。")]
        private Camera gameplayCamera;

        public bool VerboseDropLogging =>
            !productionReleaseBuild && verboseDropLogging;

        public bool RapidHoldDrop =>
            !productionReleaseBuild && rapidHoldDrop;

        private void Awake()
        {
            ApplyDebugVisibility();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                ApplyDebugVisibility();
            }
        }

        private void ApplyDebugVisibility()
        {
            if (debugOverlay != null)
            {
                debugOverlay.SetActive(
                    !productionReleaseBuild && showDebugOverlay);
            }

            if (gameplayCamera == null)
            {
                return;
            }

            ApplyLayerVisibility(NoDropPaintLayerName, showNoDropPaint);
            ApplyLayerVisibility(NoBuildPaintLayerName, showNoBuildPaint);
        }

        private void ApplyLayerVisibility(string layerName, bool show)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0 || gameplayCamera == null)
            {
                return;
            }

            if (!productionReleaseBuild && show)
            {
                gameplayCamera.cullingMask |= 1 << layer;
            }
            else
            {
                gameplayCamera.cullingMask &= ~(1 << layer);
            }
        }
    }
}
