using System.Collections;
using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// デバッグ専用のクエスト開始シナリオ。
    /// <see cref="KomayamaCraftDebugManager"/> 配下に置き、今後シナリオを増やしていく。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaQuestDebugController : MonoBehaviour
    {
        public enum Scenario
        {
            [Tooltip("通常進行（デバッグ開始なし）")]
            None = 0,

            [Tooltip("現状把握クエスト導入から")]
            SituationSurveyIntro = 1,

            [Tooltip("雨漏りクエスト導入から（現状把握完了扱い）")]
            RainLeakIntro = 2,

            [Tooltip("雨漏り納品直前：鱗鉄板3枚が地面、納品待ち")]
            RainLeakPreDelivery = 3
        }

        [Header("開始シナリオ")]
        [SerializeField, InspectorName("開始シナリオ"), Tooltip(
            "Play 時にこの状態から始める。本番リリース用が ON のときは常に None。")]
        private Scenario startScenario = Scenario.None;

        [Header("参照")]
        [SerializeField] private KomayamaCraftDebugManager debugManager;
        [SerializeField] private KomayamaQuestController questController;
        [SerializeField] private KomayamaDropArea dropArea;
        [SerializeField] private ItemDefinition ironScalePlateItem;
        [SerializeField] private Transform plateSpawnCenter;
        [SerializeField] private KomayamaShipVisual shipVisual;

        [Header("RainLeakPreDelivery")]
        [SerializeField, Min(1)] private int plateSpawnCount = 3;
        [SerializeField, Min(0.1f)] private float plateSpawnRadius = 1.2f;

        /// <summary>本番 OFF 時の有効シナリオ。本番 ON なら常に None。</summary>
        public Scenario ActiveScenario
        {
            get
            {
                if (debugManager != null && debugManager.ProductionReleaseBuild)
                {
                    return Scenario.None;
                }

                return startScenario;
            }
        }

        public bool HasActiveScenario => ActiveScenario != Scenario.None;

        /// <summary>
        /// クエスト側から呼ばれる。適用したら true（通常の OP 後進行はしない）。
        /// </summary>
        public bool TryBootstrap()
        {
            Scenario scenario = ActiveScenario;
            if (scenario == Scenario.None || questController == null)
            {
                return false;
            }

            switch (scenario)
            {
                case Scenario.SituationSurveyIntro:
                    return questController.DebugBootstrapSituationSurveyIntro();
                case Scenario.RainLeakIntro:
                    return questController.DebugBootstrapRainLeakIntro();
                case Scenario.RainLeakPreDelivery:
                    if (!questController.DebugBootstrapRainLeakPreDelivery())
                    {
                        return false;
                    }

                    StartCoroutine(SpawnPlatesNextFrame());
                    return true;
                default:
                    return false;
            }
        }

        private IEnumerator SpawnPlatesNextFrame()
        {
            // セーブ復元のドロップ生成とぶつからないよう1フレ待つ
            yield return null;
            SpawnIronScalePlatesOnGround();
            if (shipVisual != null)
            {
                shipVisual.SetStage(KomayamaShipVisual.StageInitial, force: true);
            }
        }

        private void SpawnIronScalePlatesOnGround()
        {
            if (dropArea == null || ironScalePlateItem == null)
            {
                Debug.LogWarning(
                    "[QuestDebug] RainLeakPreDelivery: DropArea または鱗鉄板 Item が未設定",
                    this);
                return;
            }

            // 納入ゴミ箱直下は NoDropPaint で拒否されやすいので、少し手前（上）の空き地に散らす
            Vector2 center = plateSpawnCenter != null
                ? (Vector2)plateSpawnCenter.position + new Vector2(0f, 2.2f)
                : Vector2.zero;
            int count = Mathf.Max(1, plateSpawnCount);
            float radius = Mathf.Max(0.1f, plateSpawnRadius);
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                float angle = (Mathf.PI * 2f * i) / count + Mathf.PI * 0.25f;
                Vector2 pos = center + new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)) * radius;

                // デバッグ開始用: 禁止ペイントを無視して強制配置
                if (dropArea.TrySpawnForLoad(ironScalePlateItem, 1, pos, null, out _))
                {
                    spawned++;
                    continue;
                }

                if (dropArea.TrySpawnNear(ironScalePlateItem, 1, pos, out _))
                {
                    spawned++;
                }
            }

            if (spawned < count)
            {
                Debug.LogWarning(
                    $"[QuestDebug] 鱗鉄板の地面生成が不足: {spawned}/{count} @ {center}",
                    this);
            }
            else
            {
                Debug.Log(
                    $"[QuestDebug] 鱗鉄板を {spawned} 枚生成 @ {center}（納入ゴミ箱のやや上）",
                    this);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (debugManager == null)
            {
                debugManager = GetComponentInParent<KomayamaCraftDebugManager>();
                if (debugManager == null)
                {
                    debugManager = GetComponent<KomayamaCraftDebugManager>();
                }
            }
        }
#endif
    }
}
