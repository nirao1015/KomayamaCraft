using TMPro;
using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 仮組中の施設。建設費を手持ち右クリックで1個ずつ受け取り、揃うと完成する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaProvisionalFacility : MonoBehaviour
    {
        [SerializeField] private FacilityDefinition definition;
        [SerializeField] private SpriteRenderer blueprintRenderer;
        [SerializeField] private BoxCollider2D footprintCollider;
        [SerializeField] private SpriteRenderer costFrameRenderer;
        [SerializeField] private SpriteRenderer costIconRenderer;
        [SerializeField] private TMP_Text costCountText;
        [SerializeField] private Transform costHudRoot;

        private readonly System.Collections.Generic.Dictionary<string, int> delivered =
            new System.Collections.Generic.Dictionary<string, int>();

        private KomayamaBuildController buildController;
        private KomayamaCraftHud hud;
        private KomayamaCraftSeManager seManager;
        private bool completing;

        public FacilityDefinition Definition => definition;

        public void BindVisuals(
            SpriteRenderer blueprint,
            BoxCollider2D box,
            SpriteRenderer frame,
            SpriteRenderer icon,
            TMP_Text count,
            Transform costHud)
        {
            blueprintRenderer = blueprint;
            footprintCollider = box;
            costFrameRenderer = frame;
            costIconRenderer = icon;
            costCountText = count;
            costHudRoot = costHud;
        }

        public void Configure(
            FacilityDefinition facilityDefinition,
            KomayamaBuildController controller,
            KomayamaCraftHud craftHud,
            KomayamaCraftSeManager craftSe,
            Color blueprintColor)
        {
            definition = facilityDefinition;
            buildController = controller;
            hud = craftHud;
            seManager = craftSe;
            delivered.Clear();
            ApplyBlueprintVisual(blueprintColor);
            RefreshCostHud();
        }

        public void ApplyBlueprintVisual(Color color)
        {
            if (blueprintRenderer != null)
            {
                blueprintRenderer.color = color;
            }
        }

        /// <summary>
        /// 建設に使う素材だけ1個受け取る。違う素材は無反応（音も出さない）。
        /// </summary>
        /// <param name="completedConstruction">この1個で完成した場合 true</param>
        public bool TryDepositConstructionMaterial(
            KomayamaHandInventory hand,
            out bool completedConstruction)
        {
            completedConstruction = false;
            if (completing || definition == null || hand == null || hand.IsEmpty)
            {
                return false;
            }

            ItemDefinition needed = FindFirstDeliverableFromHand(hand);
            if (needed == null)
            {
                return false;
            }

            if (!hand.TryRemoveOne(needed, out _))
            {
                return false;
            }

            string id = needed.DefinitionId;
            delivered.TryGetValue(id, out int current);
            delivered[id] = current + 1;
            RefreshCostHud();
            ConstructionMaterialDeposited?.Invoke(this, needed);

            if (IsConstructionComplete())
            {
                completedConstruction = true;
                CompleteConstruction();
            }

            return true;
        }

        /// <summary>建設素材を1個受け取ったとき。</summary>
        public static event System.Action<KomayamaProvisionalFacility, ItemDefinition>
            ConstructionMaterialDeposited;

        /// <summary>
        /// チュートリアル用。建設費の一部を事前に納入済みにする（例: 5必要のうち2）。
        /// </summary>
        public void SeedConstructionDelivery(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0)
            {
                return;
            }

            string id = item.DefinitionId;
            delivered.TryGetValue(id, out int current);
            delivered[id] = current + amount;
            RefreshCostHud();
        }

        /// <summary>互換用。完成フラグが不要な呼び出し向け。</summary>
        public bool TryDepositConstructionMaterial(KomayamaHandInventory hand)
        {
            return TryDepositConstructionMaterial(hand, out _);
        }

        private ItemDefinition FindFirstDeliverableFromHand(KomayamaHandInventory hand)
        {
            for (int i = 0; i < definition.ConstructionCost.Count; i++)
            {
                ItemAmount cost = definition.ConstructionCost[i];
                if (cost.Item == null || cost.Amount <= 0)
                {
                    continue;
                }

                string id = cost.Item.DefinitionId;
                delivered.TryGetValue(id, out int have);
                if (have >= cost.Amount)
                {
                    continue;
                }

                if (hand.CountOf(cost.Item) > 0)
                {
                    return cost.Item;
                }
            }

            return null;
        }

        private bool IsConstructionComplete()
        {
            for (int i = 0; i < definition.ConstructionCost.Count; i++)
            {
                ItemAmount cost = definition.ConstructionCost[i];
                if (cost.Item == null || cost.Amount <= 0)
                {
                    continue;
                }

                delivered.TryGetValue(cost.Item.DefinitionId, out int have);
                if (have < cost.Amount)
                {
                    return false;
                }
            }

            return definition.ConstructionCost.Count > 0;
        }

        private void CompleteConstruction()
        {
            if (completing || buildController == null || definition == null)
            {
                return;
            }

            completing = true;
            Vector2 position = transform.position;
            FacilityDefinition builtDefinition = definition;
            Destroy(gameObject);
            GameObject created = buildController.SpawnCompletedFacility(builtDefinition, position);
            seManager?.Play(KomayamaCraftSeCue.Deposit);
            hud?.ShowMessage($"{builtDefinition.DisplayName}が完成しました");
            KomayamaProcessingFacility processing =
                created != null
                    ? created.GetComponent<KomayamaProcessingFacility>()
                    : null;
            if (processing != null)
            {
                FacilityConstructionCompleted?.Invoke(processing);
            }
        }

        /// <summary>仮組が完成して本施設になったとき。</summary>
        public static event System.Action<KomayamaProcessingFacility> FacilityConstructionCompleted;

        private void RefreshCostHud()
        {
            if (definition == null || definition.ConstructionCost.Count == 0)
            {
                if (costHudRoot != null)
                {
                    costHudRoot.gameObject.SetActive(false);
                }

                return;
            }

            ItemAmount primary = definition.ConstructionCost[0];
            if (primary.Item == null)
            {
                return;
            }

            if (costHudRoot != null)
            {
                costHudRoot.gameObject.SetActive(true);
            }

            if (costIconRenderer != null)
            {
                costIconRenderer.sprite = primary.Item.Icon;
                costIconRenderer.enabled = primary.Item.Icon != null;
                if (primary.Item.Icon != null)
                {
                    FitWorldSpriteSize(costIconRenderer, 0.28f);
                }
            }

            delivered.TryGetValue(primary.Item.DefinitionId, out int have);
            if (costCountText != null)
            {
                costCountText.text = $"{have}/{primary.Amount}";
            }
        }

        public void SetFootprintBlocks(float blockSize, int widthBlocks, int heightBlocks)
        {
            float w = Mathf.Max(1, widthBlocks) * blockSize;
            float h = Mathf.Max(1, heightBlocks) * blockSize;
            if (footprintCollider != null)
            {
                footprintCollider.size = new Vector2(w, h);
                footprintCollider.offset = Vector2.zero;
            }
        }

        private static void FitWorldSpriteSize(SpriteRenderer renderer, float targetMaxEdge)
        {
            if (renderer == null || renderer.sprite == null)
            {
                return;
            }

            Vector2 native = renderer.sprite.bounds.size;
            float longest = Mathf.Max(native.x, native.y, 0.0001f);
            float scale = targetMaxEdge / longest;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
