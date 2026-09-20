using TMPro;
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
        [SerializeField] private KCBuildSettings buildSettings;
        [SerializeField] private Collider2D placementSurface;
        [SerializeField] private Tilemap noBuildPaint;
        [SerializeField] private SpriteRenderer previewRenderer;
        [SerializeField] private Transform facilityRoot;
        [SerializeField] private LayerMask blockedLayers;
        [SerializeField] private FacilityDefinition[] buildableFacilities;
        [SerializeField, Tooltip("buildSettings が無いときのフォールバック")]
        private float gridSnapFallback = 0.5f;
        [SerializeField, Min(1), Tooltip("設置上限（仮組含む）。現状は選択時チェック用の設計値。")]
        private int maxPlacedFacilities = 100;

        private KomayamaInputMode mode = KomayamaInputMode.Field;
        private int selectedIndex;
        private KomayamaProcessingFacility movingProcessing;
        private KomayamaStorageFacility movingStorage;
        private KomayamaGhost movingGhost;
        private bool forbidCursorActive;

        public KomayamaInputMode Mode => mode;
        public FacilityDefinition SelectedFacility =>
            buildableFacilities != null &&
            selectedIndex >= 0 &&
            selectedIndex < buildableFacilities.Length
                ? buildableFacilities[selectedIndex]
                : null;

        public bool IsMovingFacility =>
            movingProcessing != null || movingStorage != null || movingGhost != null;

        public float BlockSize =>
            buildSettings != null ? buildSettings.BlockSize : Mathf.Max(0.01f, gridSnapFallback);

        public int MaxPlacedFacilities => Mathf.Max(1, maxPlacedFacilities);

        /// <summary>
        /// Layer_Facilities 配下の仮組・加工・保管・Ghost を数える（上限100設計の集計）。
        /// </summary>
        public int CountPlacedFacilities()
        {
            Transform root = facilityRoot != null ? facilityRoot : null;
            int count = 0;
            KomayamaProvisionalFacility[] provisional =
                FindObjectsByType<KomayamaProvisionalFacility>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            KomayamaProcessingFacility[] processors =
                FindObjectsByType<KomayamaProcessingFacility>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            KomayamaStorageFacility[] storages =
                FindObjectsByType<KomayamaStorageFacility>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            KomayamaGhost[] ghosts =
                FindObjectsByType<KomayamaGhost>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            count += CountUnderRoot(provisional, root);
            count += CountUnderRoot(processors, root);
            count += CountUnderRoot(storages, root);
            count += CountUnderRoot(ghosts, root);
            return count;
        }

        private static int CountUnderRoot<T>(T[] items, Transform root) where T : Component
        {
            if (items == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null)
                {
                    continue;
                }

                if (root != null && !items[i].transform.IsChildOf(root))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

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

        private void OnDisable()
        {
            ClearForbidCursor();
        }

        public void SetMode(KomayamaInputMode nextMode)
        {
            if (mode == KomayamaInputMode.Edit && nextMode != KomayamaInputMode.Edit)
            {
                CancelMove();
            }

            mode = nextMode;
            selectedIndex = Mathf.Clamp(
                selectedIndex,
                0,
                Mathf.Max(0, (buildableFacilities != null ? buildableFacilities.Length : 1) - 1));
            if (mode == KomayamaInputMode.Field)
            {
                ClearForbidCursor();
            }

            UpdatePreview(Vector2.zero, false);
        }

        public void SelectBuildIndex(int index)
        {
            if (buildableFacilities == null || buildableFacilities.Length == 0)
            {
                return;
            }

            if (CountPlacedFacilities() >= MaxPlacedFacilities)
            {
                hud?.ShowMessage($"設置上限（{MaxPlacedFacilities}）に達しています");
                seManager?.Play(KomayamaCraftSeCue.Invalid);
                return;
            }

            selectedIndex = Mathf.Clamp(index, 0, buildableFacilities.Length - 1);
            if (mode != KomayamaInputMode.Build)
            {
                SetMode(KomayamaInputMode.Build);
            }

            UpdatePreview(Vector2.zero, false);
        }

        public bool TrySelectFacilityByDefinitionId(string definitionId)
        {
            if (buildableFacilities == null || string.IsNullOrEmpty(definitionId))
            {
                return false;
            }

            for (int i = 0; i < buildableFacilities.Length; i++)
            {
                FacilityDefinition facility = buildableFacilities[i];
                if (facility != null &&
                    string.Equals(
                        facility.DefinitionId,
                        definitionId,
                        System.StringComparison.Ordinal))
                {
                    SelectBuildIndex(i);
                    return true;
                }
            }

            return false;
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
                ClearForbidCursor();
                return;
            }

            // 非アクティブだと描画されない。ソートは地形(WorldContinent)より手前へ。
            if (!previewRenderer.gameObject.activeSelf)
            {
                previewRenderer.gameObject.SetActive(true);
            }

            EnsureFacilitySorting(previewRenderer);

            Vector2 snapped = Snap(worldPosition);
            previewRenderer.enabled = true;
            previewRenderer.transform.position = snapped;
            bool allowed = mode == KomayamaInputMode.Edit && !IsMovingFacility
                || TryGetPlacementFailure(snapped, SelectedFacility, GetMovingFootprint(), out _) == false;

            Color valid = buildSettings != null
                ? buildSettings.BlueprintValidColor
                : new Color(0.25f, 0.75f, 1f, 0.45f);
            Color invalid = buildSettings != null
                ? buildSettings.BlueprintInvalidColor
                : new Color(1f, 0.2f, 0.2f, 0.45f);
            previewRenderer.color = allowed ? valid : invalid;
            SetForbidCursor(!allowed && mode == KomayamaInputMode.Build);

            FacilityDefinition selected = SelectedFacility;
            Sprite facilitySprite = ResolveFacilitySprite(selected);
            previewRenderer.sprite = facilitySprite != null
                ? facilitySprite
                : GetOrCreatePlaceholderSprite();
            if (selected != null)
            {
                FitPreviewToFootprint(selected);
            }
        }

        /// <summary>
        /// 仮組を置く。建設費はここでは消費しない。
        /// </summary>
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

            if (TryGetPlacementFailure(snapped, selected, selected.Prefab, out failureReason))
            {
                return false;
            }

            GameObject provisionalGo = CreateProvisional(selected, snapped);
            if (provisionalGo == null)
            {
                failureReason = "仮組を作成できません";
                return false;
            }

            seManager?.Play(KomayamaCraftSeCue.Deposit);
            hud?.ShowMessage($"{selected.DisplayName}を仮組しました");
            SetMode(KomayamaInputMode.Field);
            ClearForbidCursor();
            KomayamaProvisionalFacility provisional =
                provisionalGo.GetComponent<KomayamaProvisionalFacility>();
            if (provisional != null)
            {
                ProvisionalFacilityPlaced?.Invoke(provisional);
            }

            return true;
        }

        /// <summary>仮組の新規設置に成功したとき。</summary>
        public static event System.Action<KomayamaProvisionalFacility> ProvisionalFacilityPlaced;

        public GameObject SpawnCompletedFacility(FacilityDefinition definition, Vector2 position)
        {
            return SpawnFacility(definition, position, null);
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
            FacilityDefinition definition = movingProcessing != null
                ? movingProcessing.Definition
                : movingStorage != null ? movingStorage.Definition
                : movingGhost != null ? movingGhost.Definition : null;
            if (TryGetPlacementFailure(snapped, definition, moving, out failureReason))
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
            KomayamaProvisionalFacility provisional =
                target.GetComponentInParent<KomayamaProvisionalFacility>();
            if (provisional != null)
            {
                Destroy(provisional.gameObject);
                seManager?.Play(KomayamaCraftSeCue.Drop);
                hud?.ShowMessage("仮組を撤去しました");
                return true;
            }

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
            if (created.TryGetComponent(out SpriteRenderer createdRenderer))
            {
                EnsureFacilitySorting(createdRenderer);
                createdRenderer.color = Color.white;
                // 仮組と同じ施設スプライト・同じ占有サイズに揃える
                Sprite facilitySprite = ResolveFacilitySprite(definition);
                if (facilitySprite != null)
                {
                    createdRenderer.sprite = facilitySprite;
                }

                created.transform.localScale = Vector3.one;
                FitSpriteToFootprint(createdRenderer, definition);
                if (created.TryGetComponent(out BoxCollider2D createdBox))
                {
                    // Transform スケール後も、当たり判定を見た目サイズに合わせる
                    MatchColliderToRenderedSprite(createdBox, createdRenderer);
                }
            }

            BindCreatedFacility(created, definition, instanceId);
            KomayamaWorldLayerDrawOrder.RequestApplyMaterialsInScene();
            return created;
        }

        private GameObject CreateProvisional(FacilityDefinition definition, Vector2 position)
        {
            GameObject root = new GameObject($"仮組_{definition.DisplayName}");
            root.layer = LayerMask.NameToLayer("Facility") >= 0
                ? LayerMask.NameToLayer("Facility")
                : 0;
            root.transform.SetParent(facilityRoot, false);
            root.transform.position = position;

            var box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true;

            var blueprint = new GameObject("Blueprint");
            blueprint.transform.SetParent(root.transform, false);
            var sr = blueprint.AddComponent<SpriteRenderer>();
            EnsureFacilitySorting(sr);
            Sprite facilitySprite = ResolveFacilitySprite(definition);
            sr.sprite = facilitySprite != null
                ? facilitySprite
                : GetOrCreatePlaceholderSprite();

            Color valid = buildSettings != null
                ? buildSettings.BlueprintValidColor
                : new Color(0.25f, 0.75f, 1f, 0.45f);
            sr.color = valid;

            var costHud = new GameObject("CostHud");
            costHud.transform.SetParent(root.transform, false);
            float hudY = definition.FootprintHeightBlocks * BlockSize * 0.5f + 0.35f;
            costHud.transform.localPosition = new Vector3(0f, hudY, 0f);

            var frameGo = new GameObject("Frame");
            frameGo.transform.SetParent(costHud.transform, false);
            var frameSr = frameGo.AddComponent<SpriteRenderer>();
            frameSr.sprite = CreateSolidSprite(32, new Color(0.15f, 0.35f, 0.55f, 0.9f));
            frameSr.sortingLayerName = "WorldOverlay";
            frameSr.sortingOrder = 20;
            ApplyUnlitSpriteMaterial(frameSr);
            frameGo.transform.localScale = new Vector3(0.55f, 0.55f, 1f);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(costHud.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            var iconSr = iconGo.AddComponent<SpriteRenderer>();
            iconSr.sortingLayerName = "WorldOverlay";
            iconSr.sortingOrder = 21;
            ApplyUnlitSpriteMaterial(iconSr);
            // ドロップ用アイコンは巨大なことがある。コスト表示は常に小さくする。
            iconGo.transform.localScale = Vector3.one;

            var textGo = new GameObject("Count");
            textGo.transform.SetParent(costHud.transform, false);
            textGo.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 3f;
            tmp.color = Color.white;
            tmp.text = "0/5";
            var tmpRt = textGo.GetComponent<RectTransform>();
            if (tmpRt != null)
            {
                tmpRt.sizeDelta = new Vector2(1.2f, 0.4f);
            }

            var tmpRenderer = textGo.GetComponent<MeshRenderer>();
            if (tmpRenderer != null)
            {
                tmpRenderer.sortingLayerName = "WorldOverlay";
                tmpRenderer.sortingOrder = 22;
            }

            var provisional = root.AddComponent<KomayamaProvisionalFacility>();
            provisional.BindVisuals(sr, box, frameSr, iconSr, tmp, costHud.transform);
            provisional.SetFootprintBlocks(
                BlockSize,
                definition.FootprintWidthBlocks,
                definition.FootprintHeightBlocks);
            FitSpriteToFootprint(sr, definition);
            // 見た目と同じ当たり判定（root にコライダー、子にスプライト）
            MatchColliderToRenderedSprite(box, sr);
            provisional.Configure(definition, this, hud, seManager, valid);
            return root;
        }

        /// <summary>
        /// BoxCollider2D のローカル size/offset を、描画中スプライトの見た目に一致させる。
        /// Transform.localScale で引き伸ばした後も、ワールド上の当たり＝スプライト枠になる。
        /// </summary>
        private static void MatchColliderToRenderedSprite(BoxCollider2D box, SpriteRenderer spriteRenderer)
        {
            if (box == null || spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            Transform boxTransform = box.transform;
            Transform spriteTransform = spriteRenderer.transform;

            // スプライトのワールド AABB を、コライダー側ローカルへ戻す
            Bounds world = spriteRenderer.bounds;
            Vector3 localCenter = boxTransform.InverseTransformPoint(world.center);
            Vector3 localMin = boxTransform.InverseTransformPoint(world.min);
            Vector3 localMax = boxTransform.InverseTransformPoint(world.max);
            Vector2 localSize = new Vector2(
                Mathf.Abs(localMax.x - localMin.x),
                Mathf.Abs(localMax.y - localMin.y));

            // 同一 Transform 上なら sprite.bounds.size（ローカル）で足りるが、
            // 子にスプライトがある仮組でも正しく合わせる
            if (spriteTransform == boxTransform)
            {
                Vector2 native = spriteRenderer.sprite.bounds.size;
                box.size = native;
                box.offset = spriteRenderer.sprite.bounds.center;
            }
            else
            {
                box.size = localSize;
                box.offset = new Vector2(localCenter.x, localCenter.y);
            }
        }

        private void FitPreviewToFootprint(FacilityDefinition definition)
        {
            FitSpriteToFootprint(previewRenderer, definition);
        }

        private void FitSpriteToFootprint(SpriteRenderer renderer, FacilityDefinition definition)
        {
            if (renderer == null || renderer.sprite == null || definition == null)
            {
                return;
            }

            Vector2 native = renderer.sprite.bounds.size;
            float targetW = definition.FootprintWidthBlocks * BlockSize;
            float targetH = definition.FootprintHeightBlocks * BlockSize;
            // 占有矩形いっぱいに合わせる（正方形画像でも横10×縦8の見た目になる）
            float scaleX = targetW / Mathf.Max(0.0001f, native.x);
            float scaleY = targetH / Mathf.Max(0.0001f, native.y);
            renderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        private static Sprite ResolveFacilitySprite(FacilityDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            // ワールド見た目の正本はプレハブ。UI用 Icon と取り違えない。
            if (definition.Prefab != null)
            {
                SpriteRenderer prefabRenderer =
                    definition.Prefab.GetComponentInChildren<SpriteRenderer>(true);
                if (prefabRenderer != null && prefabRenderer.sprite != null)
                {
                    return prefabRenderer.sprite;
                }
            }

            return definition.Icon;
        }

        private static void EnsureFacilitySorting(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.sortingLayerName = "WorldFacility";
            if (renderer.sortingOrder < 20)
            {
                renderer.sortingOrder = 20;
            }

            ApplyUnlitSpriteMaterial(renderer);
        }

        private static Material cachedUnlitSpriteMaterial;

        private static void ApplyUnlitSpriteMaterial(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            if (cachedUnlitSpriteMaterial == null)
            {
                // Lit + Light2Dの対象外ソートだと真っ黒になるため Unlit を使う
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader != null)
                {
                    cachedUnlitSpriteMaterial = new Material(shader)
                    {
                        name = "KC_SpriteUnlit (Runtime)"
                    };
                }
            }

            if (cachedUnlitSpriteMaterial != null)
            {
                renderer.sharedMaterial = cachedUnlitSpriteMaterial;
            }
        }

        private Sprite placeholderSprite;

        private Sprite GetOrCreatePlaceholderSprite()
        {
            if (placeholderSprite != null)
            {
                return placeholderSprite;
            }

            const int Size = 64;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            Color fill = new Color(0.35f, 0.7f, 0.95f, 1f);
            Color edge = new Color(0.1f, 0.35f, 0.55f, 1f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool border = x < 3 || y < 3 || x >= Size - 3 || y >= Size - 3;
                    bool anvil =
                        (y >= 18 && y <= 28 && x >= 12 && x <= 52) ||
                        (y >= 28 && y <= 48 && x >= 28 && x <= 36);
                    tex.SetPixel(x, y, border || anvil ? edge : fill);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Point;
            placeholderSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, Size, Size),
                new Vector2(0.5f, 0.5f),
                64f);
            placeholderSprite.name = "FacilityPlaceholder";
            return placeholderSprite;
        }

        private void BindCreatedFacility(
            GameObject created,
            FacilityDefinition definition,
            string instanceId)
        {
            if (created.TryGetComponent(out KomayamaProcessingFacility processing))
            {
                RecipeDefinition recipe = null;
                processing.Configure(definition, recipe, instanceId);
                processing.BindRuntime(hud, seManager, dropArea, buildSettings);
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
            FacilityDefinition definition,
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

            Bounds bounds = GetFootprintBounds(definition, footprintSource, position);
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
            if (hit != null &&
                (moving == null ||
                 (!hit.transform.IsChildOf(moving.transform) && hit.gameObject != moving)))
            {
                if (hit.GetComponentInParent<KomayamaProvisionalFacility>() != null ||
                    hit.GetComponentInParent<KomayamaProcessingFacility>() != null ||
                    hit.GetComponentInParent<KomayamaStorageFacility>() != null ||
                    hit.GetComponentInParent<KomayamaGhost>() != null)
                {
                    failureReason = "他の物と重なっています";
                    return true;
                }
            }

            return false;
        }

        private bool IsBlockedByNoBuildPaint(Bounds bounds)
        {
            if (noBuildPaint == null)
            {
                return false;
            }

            // タイルは z=0 に置かれている。WorldToCell の z を使うと取りこぼす。
            Vector3Int minCell = noBuildPaint.WorldToCell(bounds.min + new Vector3(0.001f, 0.001f, 0f));
            Vector3Int maxCell = noBuildPaint.WorldToCell(bounds.max - new Vector3(0.001f, 0.001f, 0f));
            if (maxCell.x < minCell.x)
            {
                int swapX = minCell.x;
                minCell.x = maxCell.x;
                maxCell.x = swapX;
            }

            if (maxCell.y < minCell.y)
            {
                int swapY = minCell.y;
                minCell.y = maxCell.y;
                maxCell.y = swapY;
            }

            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    if (noBuildPaint.GetTile(new Vector3Int(x, y, 0)) != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Bounds GetFootprintBounds(
            FacilityDefinition definition,
            GameObject footprintSource,
            Vector2 position)
        {
            if (definition != null)
            {
                float w = definition.FootprintWidthBlocks * BlockSize;
                float h = definition.FootprintHeightBlocks * BlockSize;
                return new Bounds(position, new Vector3(w, h, 1f));
            }

            if (footprintSource != null && footprintSource.TryGetComponent(out Collider2D collider))
            {
                Bounds bounds = collider.bounds;
                bounds.center = new Vector3(position.x, position.y, bounds.center.z);
                return bounds;
            }

            return new Bounds(position, Vector3.one * BlockSize);
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
            float snap = BlockSize;
            return new Vector2(
                Mathf.Round(worldPosition.x / snap) * snap,
                Mathf.Round(worldPosition.y / snap) * snap);
        }

        private void SetForbidCursor(bool forbidden)
        {
            if (forbidden == forbidCursorActive)
            {
                return;
            }

            forbidCursorActive = forbidden;
            if (!forbidden)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                return;
            }

            Texture2D tex = buildSettings != null ? buildSettings.ForbidCursorTexture : null;
            if (tex == null)
            {
                tex = CreateForbidCursorTexture();
            }

            Vector2 hot = buildSettings != null
                ? buildSettings.ForbidCursorHotspot
                : new Vector2(16f, 16f);
            Cursor.SetCursor(tex, hot, CursorMode.Auto);
        }

        private void ClearForbidCursor()
        {
            if (!forbidCursorActive)
            {
                return;
            }

            forbidCursorActive = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private static Texture2D CreateForbidCursorTexture()
        {
            const int Size = 32;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color red = new Color(1f, 0.15f, 0.15f, 1f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float cx = x - 15.5f;
                    float cy = y - 15.5f;
                    float r = Mathf.Sqrt(cx * cx + cy * cy);
                    bool ring = r > 10f && r < 14f;
                    bool slash = Mathf.Abs(cx + cy) < 2.2f && r < 12f;
                    tex.SetPixel(x, y, ring || slash ? red : clear);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return tex;
        }

        private static Sprite CreateSolidSprite(int size, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
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
