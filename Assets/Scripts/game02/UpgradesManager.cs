using System;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// アップグレードの現在値を保持し、他システムから参照する（仕様: side_panel_upgrade_spec.md）。
    /// </summary>
    public sealed class UpgradesManager : MonoBehaviour
    {
        public static UpgradesManager Instance { get; private set; }

        [Header("編集関連")]
        [Tooltip("ゲーム開始時点の編集枠（購入対象外の基準）。")]
        [SerializeField] private int baseEditorSlotsAtGameStart = 1;

        [Tooltip("買い切りで得た編集枠の加算分（固定増加の累計想定）。")]
        [SerializeField] private int purchasedEditorSlotAdds = 0;

        [Tooltip("WorkUpgradeEd01（編集枠購入数）の購入回数。0/1。")]
        [SerializeField] private int workUpgradeEd01PurchaseCount;
        [Tooltip("WorkUpgradeEd02（編集時間）の購入回数。0..10。")]
        [SerializeField] private int workUpgradeEd02PurchaseCount;
        [Tooltip("WorkUpgradeEd03（編集時バズ値補正）の購入回数。0..5。")]
        [SerializeField] private int workUpgradeEd03PurchaseCount;
        [Tooltip("WorkUpgradeEd04（編集時人気補正）の購入回数。0..5。")]
        [SerializeField] private int workUpgradeEd04PurchaseCount;
        [Tooltip("WorkUpgradeEd05（アップロード動画数）の購入回数。0..3（3〜5枠目まで）。")]
        [SerializeField] private int workUpgradeEd05PurchaseCount;
        [Tooltip("WorkUpgradeEd51（編集自動化）の購入回数。0/1。")]
        [SerializeField] private int workUpgradeEd51PurchaseCount;

        [SerializeField] private float editTimeMultiplier = 1f;
        [SerializeField] private float editBuzzGainMultiplier = 1f;
        [SerializeField] private long editBuzzGainAdditive;
        [SerializeField] private float editPopularityMultiplier = 1f;

        [Header("配信関連")]
        [Tooltip("配信関連の総仕事完了回数 T。St01〜St04 いずれか完了のたび +1（仕様: side_panel_upgrade_spec.md）。")]
        [SerializeField] private int streamingRelatedUpgradeTotalCount;

        [Tooltip("WorkUpgradeSt04 のみの完了回数 d（St04 完了で T と同時に +1）。n = T - d。")]
        [SerializeField] private int streamEnvironmentUpgradeCount;

        [Tooltip("WorkUpgradeSt01 の仕事完了回数（0以上）。トーク力 LV。")]
        [SerializeField] private int talkPowerUpgradeLevel;
        [Tooltip("WorkUpgradeSt02 の仕事完了回数（0以上）。トレンド力 LV。")]
        [SerializeField] private int trendPowerUpgradeLevel;
        [Tooltip("WorkUpgradeSt03 の仕事完了回数（0以上）。SEO力 LV。")]
        [SerializeField] private int seoPowerUpgradeLevel;
        [Tooltip("WorkUpgradeSt04 の仕事完了回数（0以上）。配信環境改善 LV。d と同値運用。")]
        [SerializeField] private int streamEnvironmentUpgradeLevel;

        [Tooltip("SidePanelView の配信関連回数ミラー（任意）。")]
        [SerializeField] private SidePanelStreamingStats sidePanelStreamingStats;

        [Tooltip("ItemStream 人気への乗算（表示用。実体は talkPowerUpgradeLevel）。")]
        [SerializeField] private float talkPower = 1f;

        [Tooltip("バズ発生確率およびバズ時倍率への寄与（仕事場・割合。計算は後続）。")]
        [SerializeField] private float trendPower = 1f;

        [Tooltip("動画の初期視聴者数への割合（仕事場・割合。計算は後続）。")]
        [SerializeField] private float seoPower = 1f;

        [Tooltip("配信に要する時間を減らす方向の割合（仕事場・割合。計算は後続）。")]
        [SerializeField] private float streamEnvironmentImprovement = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (sidePanelStreamingStats == null)
            {
                sidePanelStreamingStats = FindObjectOfType<SidePanelStreamingStats>(true);
            }

            SyncTalkPowerFloatFromLevel();
            SyncTrendPowerFloatFromLevel();
            SyncSeoPowerFloatFromLevel();
            SyncStreamEnvironmentImprovementFromCount();
            SyncEditMultipliersFromEdPurchases();
            sidePanelStreamingStats?.SyncFromUpgradesManager();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public int GetBaseEditorSlotsAtGameStart()
        {
            return baseEditorSlotsAtGameStart;
        }

        public int GetPurchasedEditorSlotAdds()
        {
            return purchasedEditorSlotAdds;
        }

        public int GetWorkUpgradeEd01PurchaseCount()
        {
            return Mathf.Clamp(workUpgradeEd01PurchaseCount, 0, 1);
        }

        public bool IsWorkUpgradeEd01Purchased()
        {
            return GetWorkUpgradeEd01PurchaseCount() >= 1;
        }

        public int GetWorkUpgradeEd02PurchaseCount()
        {
            return Mathf.Clamp(workUpgradeEd02PurchaseCount, 0, 10);
        }

        public int GetWorkUpgradeEd03PurchaseCount()
        {
            return Mathf.Clamp(workUpgradeEd03PurchaseCount, 0, 5);
        }

        public int GetWorkUpgradeEd04PurchaseCount()
        {
            return Mathf.Clamp(workUpgradeEd04PurchaseCount, 0, 5);
        }

        public int GetWorkUpgradeEd05PurchaseCount()
        {
            return Mathf.Clamp(workUpgradeEd05PurchaseCount, 0, 3);
        }

        public int GetWorkUpgradeEd51PurchaseCount()
        {
            return Mathf.Clamp(workUpgradeEd51PurchaseCount, 0, 1);
        }

        public bool IsWorkUpgradeEd51Purchased()
        {
            return GetWorkUpgradeEd51PurchaseCount() >= 1;
        }

        public int GetMaxUploadSlotCount()
        {
            return 2 + GetWorkUpgradeEd05PurchaseCount();
        }

        public bool ApplyEd01Purchase()
        {
            if (GetWorkUpgradeEd01PurchaseCount() >= 1)
            {
                return false;
            }

            workUpgradeEd01PurchaseCount = 1;
            purchasedEditorSlotAdds = Mathf.Max(1, purchasedEditorSlotAdds);
            return true;
        }

        public void MarkWorkUpgradeEd01Purchased()
        {
            ApplyEd01Purchase();
        }

        public bool ApplyEd02Purchase()
        {
            int current = GetWorkUpgradeEd02PurchaseCount();
            if (current >= 10)
            {
                return false;
            }

            workUpgradeEd02PurchaseCount = current + 1;
            SyncEditMultipliersFromEdPurchases();
            return true;
        }

        public bool ApplyEd03Purchase()
        {
            int current = GetWorkUpgradeEd03PurchaseCount();
            if (current >= 5)
            {
                return false;
            }

            workUpgradeEd03PurchaseCount = current + 1;
            SyncEditMultipliersFromEdPurchases();
            return true;
        }

        public bool ApplyEd04Purchase()
        {
            int current = GetWorkUpgradeEd04PurchaseCount();
            if (current >= 5)
            {
                return false;
            }

            workUpgradeEd04PurchaseCount = current + 1;
            SyncEditMultipliersFromEdPurchases();
            return true;
        }

        public bool ApplyEd05Purchase(out int unlockedSlotIndex)
        {
            unlockedSlotIndex = 0;
            int current = GetWorkUpgradeEd05PurchaseCount();
            if (current >= 3)
            {
                return false;
            }

            workUpgradeEd05PurchaseCount = current + 1;
            // 初期有効2枠に対して、購入1回目で3枠目を開放（最大3回で5枠目まで）。
            unlockedSlotIndex = 2 + workUpgradeEd05PurchaseCount;
            return true;
        }

        public bool ApplyEd51Purchase()
        {
            if (GetWorkUpgradeEd51PurchaseCount() >= 1)
            {
                return false;
            }

            workUpgradeEd51PurchaseCount = 1;
            return true;
        }

        public int GetTotalEditorSlotCount()
        {
            return Mathf.Max(0, baseEditorSlotsAtGameStart) + Mathf.Max(0, purchasedEditorSlotAdds);
        }

        public float GetEditTimeMultiplier()
        {
            return editTimeMultiplier;
        }

        public float GetEditBuzzGainMultiplier()
        {
            return editBuzzGainMultiplier;
        }

        public long GetEditBuzzGainAdditive()
        {
            return Math.Max(0L, editBuzzGainAdditive);
        }

        public float GetEditPopularityMultiplier()
        {
            return editPopularityMultiplier;
        }

        private void SyncEditMultipliersFromEdPurchases()
        {
            int ed02 = GetWorkUpgradeEd02PurchaseCount();
            int ed03 = GetWorkUpgradeEd03PurchaseCount();
            int ed04 = GetWorkUpgradeEd04PurchaseCount();

            // Ed02: 10回購入時点で約50%まで短縮。
            double ed02OneStepFactor = Math.Pow(0.5d, 1d / 10d);
            editTimeMultiplier = (float)Math.Pow(ed02OneStepFactor, ed02);

            // Ed03: バズ値増加は 1/2/5回目のみ。バズ係数は毎回微増。
            editBuzzGainMultiplier = 1f + (0.1f * ed03);
            long additive = 0L;
            if (ed03 >= 1)
            {
                additive += 2L;
            }
            if (ed03 >= 2)
            {
                additive += 2L;
            }
            if (ed03 >= 5)
            {
                additive += 2L;
            }
            editBuzzGainAdditive = additive;

            // Ed04: 購入ごとの上昇量は抑えめにする。
            editPopularityMultiplier = 1f + (0.08f * ed04);
        }

        /// <summary>配信関連の総アップグレード完了回数 T（式の n = T - d の T）。</summary>
        public int GetStreamingRelatedUpgradeTotalCount()
        {
            return streamingRelatedUpgradeTotalCount;
        }

        /// <summary>配信環境改善（St04）の完了回数 d。</summary>
        public int GetStreamEnvironmentUpgradeCount()
        {
            return GetClampedStreamEnvironmentCount();
        }

        /// <summary>配信時間式の増加側回数 n = max(0, T - d)。</summary>
        public int GetStreamingIncreaseUpgradeCountN()
        {
            return Mathf.Max(0, streamingRelatedUpgradeTotalCount - GetStreamEnvironmentUpgradeCount());
        }

        public float GetTalkPower()
        {
            return talkPower;
        }

        /// <summary>St01 完了回数（トーク力 LV）。</summary>
        public int GetTalkPowerUpgradeLevel()
        {
            return Mathf.Max(0, talkPowerUpgradeLevel);
        }

        /// <summary>
        /// St01 仕事完了時: LV と T を更新し、<paramref name="currentPopularity"/> にトーク式を適用した ItemStream 用人気を返す。
        /// </summary>
        public long ApplySt01WorkComplete(long currentPopularity)
        {
            if (talkPowerUpgradeLevel < int.MaxValue)
            {
                talkPowerUpgradeLevel++;
            }

            streamingRelatedUpgradeTotalCount = Mathf.Max(0, streamingRelatedUpgradeTotalCount + 1);
            SyncTalkPowerFloatFromLevel();
            sidePanelStreamingStats?.SyncFromUpgradesManager();
            WorkStreaming.RefreshAllDebugUpgradeAwareWorkSeconds();

            int lv = Mathf.Max(0, talkPowerUpgradeLevel);
            double mult = 1d + 0.02d * lv;
            double prod = (double)currentPopularity * mult;
            if (prod <= 0d)
            {
                return 0L;
            }

            if (prod >= long.MaxValue)
            {
                return long.MaxValue;
            }

            return (long)Math.Floor(prod);
        }

        /// <summary>
        /// 現在のトーク力 LV を使って ItemStream 人気へ倍率を適用する（副作用なし）。
        /// </summary>
        public long ComputeTalkPowerAdjustedPopularity(long basePopularity)
        {
            if (basePopularity <= 0L)
            {
                return 0L;
            }

            int lv = Mathf.Max(0, talkPowerUpgradeLevel);
            double mult = 1d + 0.02d * lv;
            double prod = (double)basePopularity * mult;
            if (prod <= 0d)
            {
                return 0L;
            }

            if (prod >= long.MaxValue)
            {
                return long.MaxValue;
            }

            return (long)Math.Floor(prod);
        }

        private void SyncTalkPowerFloatFromLevel()
        {
            talkPower = 1f + (0.02f * Mathf.Max(0, talkPowerUpgradeLevel));
        }

        private void SyncTrendPowerFloatFromLevel()
        {
            trendPower = 1f + (0.2f * Mathf.Max(0, trendPowerUpgradeLevel));
        }

        private void SyncSeoPowerFloatFromLevel()
        {
            seoPower = 1f + (0.02f * Mathf.Max(0, seoPowerUpgradeLevel));
        }

        private int GetClampedStreamEnvironmentCount()
        {
            return Mathf.Max(0, Mathf.Max(streamEnvironmentUpgradeCount, streamEnvironmentUpgradeLevel));
        }

        private void SyncStreamEnvironmentImprovementFromCount()
        {
            int d = GetClampedStreamEnvironmentCount();
            streamEnvironmentImprovement = (float)Math.Pow(0.88d, d);
        }

        public float GetTrendPower()
        {
            return trendPower;
        }

        /// <summary>St02 完了回数（トレンド力 LV）。</summary>
        public int GetTrendPowerUpgradeLevel()
        {
            return Mathf.Max(0, trendPowerUpgradeLevel);
        }

        /// <summary>編集仕事場へ渡すアップグレードバズ値（St02 は3回目のみ +1）。</summary>
        public int GetTrendUpgradeBuzzValue()
        {
            int lv = Mathf.Max(0, trendPowerUpgradeLevel);
            return lv >= 3 ? 1 : 0;
        }

        /// <summary>編集仕事場へ渡すアップグレードバズ係数（St02 は毎回微増）。</summary>
        public int GetTrendUpgradeBuzzFactor()
        {
            int lv = Mathf.Max(0, trendPowerUpgradeLevel);
            return Mathf.FloorToInt(lv * 0.2f);
        }

        /// <summary>St02 仕事完了時: LV と T を更新する。</summary>
        public void ApplySt02WorkComplete()
        {
            if (trendPowerUpgradeLevel < int.MaxValue)
            {
                trendPowerUpgradeLevel++;
            }

            streamingRelatedUpgradeTotalCount = Mathf.Max(0, streamingRelatedUpgradeTotalCount + 1);
            SyncTrendPowerFloatFromLevel();
            sidePanelStreamingStats?.SyncFromUpgradesManager();
            WorkStreaming.RefreshAllDebugUpgradeAwareWorkSeconds();
        }

        public float GetSeoPower()
        {
            return seoPower;
        }

        /// <summary>St03 完了回数（SEO力 LV）。</summary>
        public int GetSeoPowerUpgradeLevel()
        {
            return Mathf.Max(0, seoPowerUpgradeLevel);
        }

        /// <summary>アップロード時初期視聴者数に掛ける倍率（St03 は1回ごとに+2%）。</summary>
        public float GetSeoInitialViewerMultiplier()
        {
            int lv = Mathf.Max(0, seoPowerUpgradeLevel);
            return 1f + 0.02f * lv;
        }

        /// <summary>St03 仕事完了時: LV と T を更新する。</summary>
        public void ApplySt03WorkComplete()
        {
            if (seoPowerUpgradeLevel < int.MaxValue)
            {
                seoPowerUpgradeLevel++;
            }

            streamingRelatedUpgradeTotalCount = Mathf.Max(0, streamingRelatedUpgradeTotalCount + 1);
            SyncSeoPowerFloatFromLevel();
            sidePanelStreamingStats?.SyncFromUpgradesManager();
            WorkStreaming.RefreshAllDebugUpgradeAwareWorkSeconds();
        }

        public float GetStreamEnvironmentImprovement()
        {
            return streamEnvironmentImprovement;
        }

        /// <summary>St04 完了回数（配信環境改善 LV）。d と同値で扱う。</summary>
        public int GetStreamEnvironmentUpgradeLevel()
        {
            return Mathf.Max(0, Mathf.Max(streamEnvironmentUpgradeLevel, streamEnvironmentUpgradeCount));
        }

        /// <summary>St04 仕事完了時: LV・T・d を同時更新する。</summary>
        public void ApplySt04WorkComplete()
        {
            int current = Mathf.Max(0, streamEnvironmentUpgradeLevel);
            int next = current < int.MaxValue ? current + 1 : int.MaxValue;
            streamEnvironmentUpgradeLevel = next;
            streamEnvironmentUpgradeCount = next;

            streamingRelatedUpgradeTotalCount = Mathf.Max(0, streamingRelatedUpgradeTotalCount + 1);
            SyncStreamEnvironmentImprovementFromCount();
            sidePanelStreamingStats?.SyncFromUpgradesManager();
            WorkStreaming.RefreshAllDebugUpgradeAwareWorkSeconds();
        }

        public UpgradesState CaptureSaveState()
        {
            return new UpgradesState
            {
                purchasedEditorSlotAdds = purchasedEditorSlotAdds,
                workUpgradeEd01PurchaseCount = workUpgradeEd01PurchaseCount,
                workUpgradeEd02PurchaseCount = workUpgradeEd02PurchaseCount,
                workUpgradeEd03PurchaseCount = workUpgradeEd03PurchaseCount,
                workUpgradeEd04PurchaseCount = workUpgradeEd04PurchaseCount,
                workUpgradeEd05PurchaseCount = workUpgradeEd05PurchaseCount,
                workUpgradeEd51PurchaseCount = workUpgradeEd51PurchaseCount,
                streamingRelatedUpgradeTotalCount = streamingRelatedUpgradeTotalCount,
                streamEnvironmentUpgradeCount = streamEnvironmentUpgradeCount,
                talkPowerUpgradeLevel = talkPowerUpgradeLevel,
                trendPowerUpgradeLevel = trendPowerUpgradeLevel,
                seoPowerUpgradeLevel = seoPowerUpgradeLevel,
                streamEnvironmentUpgradeLevel = streamEnvironmentUpgradeLevel
            };
        }

        public void ApplySaveState(UpgradesState state)
        {
            if (state == null)
            {
                return;
            }

            purchasedEditorSlotAdds = Mathf.Max(0, state.purchasedEditorSlotAdds);
            workUpgradeEd01PurchaseCount = Mathf.Clamp(state.workUpgradeEd01PurchaseCount, 0, 1);
            workUpgradeEd02PurchaseCount = Mathf.Clamp(state.workUpgradeEd02PurchaseCount, 0, 10);
            workUpgradeEd03PurchaseCount = Mathf.Clamp(state.workUpgradeEd03PurchaseCount, 0, 5);
            workUpgradeEd04PurchaseCount = Mathf.Clamp(state.workUpgradeEd04PurchaseCount, 0, 5);
                workUpgradeEd05PurchaseCount = Mathf.Clamp(state.workUpgradeEd05PurchaseCount, 0, 3);
            workUpgradeEd51PurchaseCount = Mathf.Clamp(state.workUpgradeEd51PurchaseCount, 0, 1);
            streamingRelatedUpgradeTotalCount = Mathf.Max(0, state.streamingRelatedUpgradeTotalCount);
            streamEnvironmentUpgradeCount = Mathf.Max(0, state.streamEnvironmentUpgradeCount);
            talkPowerUpgradeLevel = Mathf.Max(0, state.talkPowerUpgradeLevel);
            trendPowerUpgradeLevel = Mathf.Max(0, state.trendPowerUpgradeLevel);
            seoPowerUpgradeLevel = Mathf.Max(0, state.seoPowerUpgradeLevel);
            streamEnvironmentUpgradeLevel = Mathf.Max(0, state.streamEnvironmentUpgradeLevel);

            // 補完は「購入済みフラグ -> 実効果」方向だけに限定する。
            // 実効果値だけから購入済みへ昇格させると、未購入データを誤って購入済みにしてしまう。
            if (workUpgradeEd01PurchaseCount > 0 && purchasedEditorSlotAdds <= 0)
            {
                purchasedEditorSlotAdds = 1;
            }

            SyncTalkPowerFloatFromLevel();
            SyncTrendPowerFloatFromLevel();
            SyncSeoPowerFloatFromLevel();
            SyncStreamEnvironmentImprovementFromCount();
            SyncEditMultipliersFromEdPurchases();
            sidePanelStreamingStats?.SyncFromUpgradesManager();
            WorkStreaming.RefreshAllDebugUpgradeAwareWorkSeconds();
            FieldView.RefreshAllByCurrentUpgrades();

            if (IsWorkUpgradeEd01Purchased())
            {
                WorkEditor[] editors = FindObjectsOfType<WorkEditor>(true);
                for (int i = 0; i < editors.Length; i++)
                {
                    WorkEditor editor = editors[i];
                    if (editor != null && editor.name == "WorkEditor_2")
                    {
                        editor.UnlockSecondSlotIfPurchasedEd01();
                        editor.DisableInvalidViewByUpgrade();
                        break;
                    }
                }
            }
        }
    }
}
