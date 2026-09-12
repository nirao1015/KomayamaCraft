using UnityEngine;
using UnityEngine.Tilemaps;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaBuildController : MonoBehaviour
    {
        [SerializeField] private KomayamaHandInventory hand;
        [SerializeField] private KomayamaDropArea dropArea;
        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private KomayamaCraftSeManager seManager;
        [SerializeField] private Collider2D placementSurface;
        [SerializeField] private Tilemap noBuildPaint;
        [SerializeField] private SpriteRenderer previewRenderer;
        [SerializeField] private Transform facilityRoot;
        [SerializeField] private LayerMask blockedLayers;
        [SerializeField] private FacilityDefinition[] buildableFacilities;
        [SerializeField] private float gridSnap = 0.5f;

        private KomayamaInputMode mode = KomayamaInputMode.Field;
        private int selectedIndex;
        private KomayamaProcessingFacility movingProcessing;
        private KomayamaStorageFacility movingStorage;
        private KomayamaGhost movingGhost;

        public KomayamaInputMode Mode => mode;
        public FacilityDefinition SelectedFacility =>
            buildableFacilities != null &&
            selectedIndex >= 0 &&
            selectedIndex < buildableFacilities.Length
                ? buildableFacilities[selectedIndex]
                : null;

        public bool IsMovingFacility =>
            movingProcessing != null || movingStorage != null || movingGhost != null;

        public string ModeLabel
        {
            get
            {
                if (mode == KomayamaInputMode.Build)
                {
                    FacilityDefinition selected = SelectedFacility;
                    return selected != null
                        ? $"建設: {selected.DisplayName}{DescribeCost(selected)}"
                        : "建設";
                }

                if (mode == KomayamaInputMode.Edit)
                {
                    return IsMovingFacility ? "移設先をクリック" : "編集: 左で移設 右で撤去";
                }

                return "通常";
            }
        }

        public void SetMode(KomayamaInputMode nextMode)
        {
            if (mode == KomayamaInputMode.Edit && nextMode != KomayamaInputMode.Edit)
            {
                CancelMove();
            }

            mode = nextMode;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, buildableFacilities.Length - 1));
            UpdatePreview(Vector2.zero, false);
        }

        public void SelectBuildIndex(int index)
        {
            if (buildableFacilities == null || buildableFacilities.Length == 0)
            {
                return;
            }

            selectedIndex = Mathf.Clamp(index, 0, buildableFacilities.Length - 1);
            if (mode != KomayamaInputMode.Build)
            {
                SetMode(KomayamaInputMode.Build);
            }
        }

        public void UpdatePreview(Vector2 worldPosition, bool visible)
        {
            if (previewRenderer == null)
            {
                return;
            }

            if (!visible || mode == KomayamaInputMode.Field)
            {
                previewRenderer.enabled = false;
                return;
            }

            Vector2 snapped = Snap(worldPosition);
            previewRenderer.enabled = true;
            previewRenderer.transform.position = snapped;
            bool allowed = mode == KomayamaInputMode.Edit && !IsMovingFacility
                ? true
                : TryGetPlacementFailure(snapped, GetMovingFootprint(), out _) == false;
            previewRenderer.color = allowed
                ? new Color(0.35f, 1f, 0.45f, 0.45f)
                : new Color(1f, 0.25f, 0.25f, 0.45f);
            FacilityDefinition selected = SelectedFacility;
            if (selected != null && selected.Prefab != null &&
                selected.Prefab.TryGetComponent(out SpriteRenderer prefabRenderer))
            {
                previewRenderer.sprite = prefabRenderer.sprite;
                previewRenderer.transform.localScale = selected.Prefab.transform.localScale;
            }
        }

        public bool TryBuildAt(Vector2 worldPosition, out string failureReason)
        {
            failureReason = null;
            FacilityDefinition selected = SelectedFacility;
            if (selected == null || selected.Prefab == null)
            {
                failureReason = "建設する設備がありません";
                return false;
            }

            Vector2 snapped = Snap(worldPosition);
            KomayamaProgressService progress = KomayamaProgressService.Instance;
            if (progress != null && !progress.CanBuild(selected))
            {
                failureReason = "設計図が未解放です";
                return false;
            }

            if (TryGetPlacementFailure(snapped, selected.Prefab, out failureReason))
            {
                return false;
            }

            if (!TryPayCost(selected, out failureReason))
            {
                return false;
            }

            GameObject created = Instantiate(
                selected.Prefab,
                snapped,
                Quaternion.identity,
                facilityRoot);
            created.name = selected.DisplayName;
            BindCreatedFacility(created, selected, null);
            seManager?.Play(KomayamaCraftSeCue.Deposit);
            hud?.ShowMessage($"{selected.DisplayName}を建設しました");
            return true;
        }

        public bool TryBeginMove(GameObject target, out string failureReason)
        {
            failureReason = null;
            CancelMove();
            movingProcessing = target.GetComponentInParent<KomayamaProcessingFacility>();
            movingStorage = target.GetComponentInParent<KomayamaStorageFacility>();
            movingGhost = target.GetComponentInParent<KomayamaGhost>();
            if (movingProcessing == null && movingStorage == null && movingGhost == null)
            {
                failureReason = "移設できる設備がありません";
                return false;
            }

            SetHidden(GetMovingObject(), true);
            return true;
        }

        public bool TryFinishMove(Vector2 worldPosition, out string failureReason)
        {
            failureReason = null;
            GameObject moving = GetMovingObject();
            if (moving == null)
            {
                failureReason = "移設中の設備がありません";
                return false;
            }

            Vector2 snapped = Snap(worldPosition);
            if (TryGetPlacementFailure(snapped, moving, out failureReason))
            {
                return false;
            }

            moving.transform.position = snapped;
            SetHidden(moving, false);
            movingProcessing = null;
            movingStorage = null;
            movingGhost = null;
            seManager?.Play(KomayamaCraftSeCue.Deposit);
            return true;
        }

        public bool TryRemove(GameObject target, out string failureReason)
        {
            failureReason = null;
            KomayamaProcessingFacility processing =
                target.GetComponentInParent<KomayamaProcessingFacility>();
            KomayamaStorageFacility storage =
                target.GetComponentInParent<KomayamaStorageFacility>();
            KomayamaGhost ghost = target.GetComponentInParent<KomayamaGhost>();
            FacilityDefinition definition = processing != null
                ? processing.Definition
                : storage != null ? storage.Definition
                : ghost != null ? ghost.Definition : null;
            if (definition == null)
            {
                failureReason = "撤去できる設備がありません";
                return false;
            }

            processing?.EvacuateTo(hand, dropArea);
            storage?.EvacuateTo(hand, dropArea);
            RefundCost(definition, target.transform.position);
            Destroy(
                processing != null ? processing.gameObject
                : storage != null ? storage.gameObject
                : ghost.gameObject);
            seManager?.Play(KomayamaCraftSeCue.Drop);
            hud?.ShowMessage($"{definition.DisplayName}を撤去しました");
            return true;
        }

        public void CancelMove()
        {
            SetHidden(GetMovingObject(), false);
            movingProcessing = null;
            movingStorage = null;
            movingGhost = null;
        }

        public GameObject SpawnFacility(
            FacilityDefinition definition,
            Vector2 position,
            string instanceId)
        {
            if (definition == null || definition.Prefab == null)
            {
                return null;
            }

            GameObject created = Instantiate(
                definition.Prefab,
                position,
                Quaternion.identity,
                facilityRoot);
            created.name = definition.DisplayName;
            BindCreatedFacility(created, definition, instanceId);
            return created;
        }

        private void BindCreatedFacility(
            GameObject created,
            FacilityDefinition definition,
            string instanceId)
        {
            if (created.TryGetComponent(out KomayamaProcessingFacility processing))
            {
                RecipeDefinition recipe = definition.SupportedRecipes.Count > 0
                    ? definition.SupportedRecipes[0]
                    : processing.Recipe;
                processing.Configure(definition, recipe, instanceId);
                processing.BindRuntime(hud, seManager);
            }

            if (created.TryGetComponent(out KomayamaStorageFacility storage))
            {
                storage.Configure(definition, instanceId);
                storage.BindRuntime(hud, seManager);
            }

            if (created.TryGetComponent(out KomayamaGhost ghost))
            {
                ghost.Configure(definition, instanceId);
                ghost.BindRuntime(hud);
            }
        }

        private bool TryPayCost(FacilityDefinition definition, out string failureReason)
        {
            failureReason = null;
            if (definition.ConstructionCost.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < definition.ConstructionCost.Count; i++)
            {
                ItemAmount cost = definition.ConstructionCost[i];
                if (hand == null || !hand.CanConsume(cost.Item, cost.Amount))
                {
                    failureReason = cost.Item != null
                        ? $"{cost.Item.DisplayName}が{cost.Amount}個必要です"
                        : "建設コストが足りません";
                    return false;
                }
            }

            for (int i = 0; i < definition.ConstructionCost.Count; i++)
            {
                ItemAmount cost = definition.ConstructionCost[i];
                if (!hand.TryConsume(cost.Item, cost.Amount))
                {
                    failureReason = "建設コストが足りません";
                    return false;
                }
            }

            return true;
        }

        private void RefundCost(FacilityDefinition definition, Vector2 position)
        {
            for (int i = 0; i < definition.ConstructionCost.Count; i++)
            {
                ItemAmount cost = definition.ConstructionCost[i];
                KomayamaFacilityContents.GiveOrDrop(
                    cost.Item,
                    cost.Amount,
                    hand,
                    dropArea,
                    position);
            }
        }

        private bool TryGetPlacementFailure(
            Vector2 position,
            GameObject footprintSource,
            out string failureReason)
        {
            failureReason = null;
            if (placementSurface == null || !placementSurface.OverlapPoint(position))
            {
                failureReason = "建設できる地面の外です";
                return true;
            }

            if (!KomayamaRegion.CanInteract(position, out failureReason))
            {
                return true;
            }

            Bounds bounds = GetFootprintBounds(footprintSource, position);
            if (IsBlockedByNoBuildPaint(bounds))
            {
                failureReason = "建設できない場所です";
                return true;
            }

            Collider2D hit = Physics2D.OverlapBox(
                bounds.center,
                bounds.size * 0.9f,
                0f,
                blockedLayers);
            GameObject moving = GetMovingObject();
            if (hit != null && (moving == null || !hit.transform.IsChildOf(moving.transform) && hit.gameObject != moving))
            {
                failureReason = "他の物と重なっています";
                return true;
            }

            return false;
        }

        private bool IsBlockedByNoBuildPaint(Bounds bounds)
        {
            if (noBuildPaint == null)
            {
                return false;
            }

            Vector3 cellSize = noBuildPaint.layoutGrid != null
                ? noBuildPaint.layoutGrid.cellSize
                : noBuildPaint.cellSize;
            Vector3Int minCell = noBuildPaint.WorldToCell(bounds.min);
            Vector3Int maxCell = noBuildPaint.WorldToCell(bounds.max);
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    if (noBuildPaint.GetTile(new Vector3Int(x, y, minCell.z)) != null)
                    {
                        return true;
                    }
                }
            }

            _ = cellSize;
            return false;
        }

        private static Bounds GetFootprintBounds(GameObject source, Vector2 position)
        {
            if (source != null && source.TryGetComponent(out Collider2D collider))
            {
                Bounds bounds = collider.bounds;
                bounds.center = new Vector3(position.x, position.y, bounds.center.z);
                return bounds;
            }

            return new Bounds(position, Vector3.one);
        }

        private GameObject GetMovingFootprint()
        {
            return GetMovingObject() != null
                ? GetMovingObject()
                : SelectedFacility != null ? SelectedFacility.Prefab : null;
        }

        private GameObject GetMovingObject()
        {
            if (movingProcessing != null)
            {
                return movingProcessing.gameObject;
            }

            if (movingStorage != null)
            {
                return movingStorage.gameObject;
            }

            return movingGhost != null ? movingGhost.gameObject : null;
        }

        private static void SetHidden(GameObject target, bool hidden)
        {
            if (target != null)
            {
                target.SetActive(!hidden);
            }
        }

        private Vector2 Snap(Vector2 worldPosition)
        {
            float snap = Mathf.Max(0.01f, gridSnap);
            return new Vector2(
                Mathf.Round(worldPosition.x / snap) * snap,
                Mathf.Round(worldPosition.y / snap) * snap);
        }

        private static string DescribeCost(FacilityDefinition definition)
        {
            if (definition == null ||
                definition.ConstructionCost == null ||
                definition.ConstructionCost.Count == 0)
            {
                return string.Empty;
            }

            ItemAmount cost = definition.ConstructionCost[0];
            if (cost.Item == null || cost.Amount <= 0)
            {
                return string.Empty;
            }

            return $" ({cost.Item.DisplayName}×{cost.Amount})";
        }
    }
}
