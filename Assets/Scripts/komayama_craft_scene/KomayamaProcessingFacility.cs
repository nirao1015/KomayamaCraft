using System.Collections.Generic;
using UnityEngine;

namespace KomayamaCraft
{
    public enum KomayamaFacilityState
    {
        Idle,
        WaitingForInput,
        WaitingForFuel,
        Processing
    }

    [DisallowMultipleComponent]
    public sealed class KomayamaProcessingFacility : MonoBehaviour
    {
        [SerializeField] private FacilityDefinition definition;
        [SerializeField] private RecipeDefinition recipe;
        [SerializeField] private string instanceId;
        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private KomayamaCraftSeManager seManager;
        [SerializeField] private KomayamaDropArea dropArea;
        [SerializeField] private KCBuildSettings buildSettings;
        [SerializeField] private KomayamaFacilityWorldOverlay worldOverlay;

        private readonly List<ItemDefinition> inputItems = new();
        private readonly List<int> inputAmounts = new();
        private ItemDefinition fuelItem;
        private int fuelAmount;
        private float progressSeconds;
        private KomayamaFacilityState state = KomayamaFacilityState.Idle;

        public FacilityDefinition Definition => definition;
        public RecipeDefinition Recipe => recipe;
        public string InstanceId => instanceId;
        public int InputAmount => Total(inputAmounts);
        public int FuelAmount => fuelAmount;
        public int FuelCapacity => definition != null ? definition.FuelCapacity : 0;
        public string FuelItemName =>
            fuelItem != null ? fuelItem.DisplayName : "燃料なし";
        public string RecipeName => recipe != null ? recipe.DisplayName : "未選択";
        public string InputItemName => DescribeStacks(inputItems, inputAmounts, "材料なし");
        public float Progress01 => recipe != null && recipe.ProcessingSeconds > 0f
            ? Mathf.Clamp01(progressSeconds / recipe.ProcessingSeconds)
            : 0f;
        public float RemainingSeconds => recipe != null
            ? Mathf.Max(0f, recipe.ProcessingSeconds - progressSeconds)
            : 0f;
        public KomayamaFacilityState State => state;
        public bool HasSelectedRecipe => recipe != null;
        public bool UsesFuel => definition != null && definition.UsesFuel;

        public event System.Action StateChanged;

        private void Awake()
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = GameDataId.CreateInstanceId();
            }

            EnsureOverlay();
        }

        public void BindRuntime(
            KomayamaCraftHud runtimeHud,
            KomayamaCraftSeManager runtimeSe,
            KomayamaDropArea runtimeDropArea,
            KCBuildSettings runtimeBuildSettings)
        {
            if (hud == null)
            {
                hud = runtimeHud;
            }

            if (seManager == null)
            {
                seManager = runtimeSe;
            }

            if (dropArea == null)
            {
                dropArea = runtimeDropArea;
            }

            if (buildSettings == null)
            {
                buildSettings = runtimeBuildSettings;
            }

            EnsureOverlay();
            NotifyStateChanged();
        }

        public void Configure(
            FacilityDefinition facilityDefinition,
            RecipeDefinition selectedRecipe,
            string savedInstanceId)
        {
            definition = facilityDefinition;
            recipe = selectedRecipe;
            if (!string.IsNullOrEmpty(savedInstanceId))
            {
                instanceId = savedInstanceId;
            }

            if (recipe == null)
            {
                state = KomayamaFacilityState.Idle;
            }

            EnsureOverlay();
            NotifyStateChanged();
        }

        public Vector2 GetDropOrigin()
        {
            float block = buildSettings != null ? buildSettings.BlockSize : 0.5f;
            float offset = buildSettings != null
                ? buildSettings.FacilityDropOffsetBelowFootprint
                : 0.35f;
            float width = definition != null
                ? definition.FootprintWidthBlocks * block
                : block;
            float height = definition != null
                ? definition.FootprintHeightBlocks * block
                : block;
            Vector2 center = transform.position;
            float bottom = center.y - height * 0.5f;
            return new Vector2(center.x, bottom - offset);
        }

        public int GetInputAmount(ItemDefinition item)
        {
            return GetAmount(inputItems, inputAmounts, item);
        }

        public bool TrySelectRecipe(RecipeDefinition selected, out string failureReason)
        {
            failureReason = null;
            if (definition == null || selected == null)
            {
                failureReason = "レシピを選べません";
                return false;
            }

            if (!definition.SupportsRecipe(selected.DefinitionId))
            {
                failureReason = "この設備では使えないレシピです";
                return false;
            }

            if (recipe == selected)
            {
                RecipeSelected?.Invoke(this, selected);
                return true;
            }

            if (recipe != null)
            {
                AbortProcessingKeepFuel();
                DropUnusedInputs();
            }

            recipe = selected;
            progressSeconds = 0f;
            state = KomayamaFacilityState.WaitingForInput;
            hud?.ShowMessage($"レシピ: {RecipeName}");
            TryStartProcessing();
            NotifyStateChanged();
            RecipeSelected?.Invoke(this, selected);
            return true;
        }

        /// <summary>レシピが選ばれたとき。</summary>
        public static event System.Action<KomayamaProcessingFacility, RecipeDefinition>
            RecipeSelected;

        /// <summary>チュートリアル用。受理燃料を指定個数セットする。</summary>
        public void ApplyTutorialFuelCharges(int charges)
        {
            if (definition == null || !definition.UsesFuel || charges <= 0)
            {
                return;
            }

            if (definition.AcceptedFuelItems == null ||
                definition.AcceptedFuelItems.Count == 0 ||
                definition.AcceptedFuelItems[0] == null)
            {
                return;
            }

            fuelItem = definition.AcceptedFuelItems[0];
            fuelAmount = Mathf.Clamp(charges, 0, definition.FuelCapacity);
            NotifyStateChanged();
        }

        public bool TryCycleRecipe(int delta, out string failureReason)
        {
            failureReason = null;
            if (definition == null || definition.SupportedRecipes.Count == 0)
            {
                failureReason = "切り替えられるレシピがありません";
                return false;
            }

            int current = -1;
            for (int i = 0; i < definition.SupportedRecipes.Count; i++)
            {
                if (definition.SupportedRecipes[i] == recipe)
                {
                    current = i;
                    break;
                }
            }

            int count = definition.SupportedRecipes.Count;
            int next = current < 0 ? 0 : current + delta;
            next %= count;
            if (next < 0)
            {
                next += count;
            }

            return TrySelectRecipe(definition.SupportedRecipes[next], out failureReason);
        }

        /// <summary>メニュー「排出」。停止中は素材＋燃料、生産中は素材のみ（進行破棄・燃料残留）。</summary>
        public void EjectContents()
        {
            bool processing = state == KomayamaFacilityState.Processing;
            if (processing)
            {
                AbortProcessingKeepFuel();
            }

            DropUnusedInputs();
            if (!processing)
            {
                DropFuel();
            }

            progressSeconds = 0f;
            if (recipe == null)
            {
                state = KomayamaFacilityState.Idle;
            }
            else
            {
                state = KomayamaFacilityState.WaitingForInput;
                TryStartProcessing();
            }

            NotifyStateChanged();
        }

        /// <summary>解体時。未使用入力は返却、加工中消費分・燃料は消滅。</summary>
        public void EvacuateTo(KomayamaHandInventory hand, KomayamaDropArea evacuateDropArea)
        {
            KomayamaDropArea target = evacuateDropArea != null ? evacuateDropArea : dropArea;
            if (state == KomayamaFacilityState.Processing)
            {
                AbortProcessingKeepFuel();
            }

            DumpStacksToGround(inputItems, inputAmounts, target);
            ClearStacks(inputItems, inputAmounts);
            fuelItem = null;
            fuelAmount = 0;
            progressSeconds = 0f;
            state = recipe == null
                ? KomayamaFacilityState.Idle
                : KomayamaFacilityState.WaitingForInput;
            NotifyStateChanged();
        }

        public void CaptureSave(FacilitySaveDto dto)
        {
            dto.facilityDefinitionId = definition != null ? definition.DefinitionId : string.Empty;
            dto.instanceId = instanceId;
            dto.position = new Float2SaveDto(transform.position.x, transform.position.y);
            dto.rotationDegrees = transform.eulerAngles.z;
            dto.selectedRecipeId = recipe != null ? recipe.DefinitionId : string.Empty;
            dto.processingProgress01 = Progress01;
            dto.processingState = state switch
            {
                KomayamaFacilityState.Processing => FacilityProcessingState.Processing,
                KomayamaFacilityState.WaitingForFuel => FacilityProcessingState.WaitingForFuel,
                KomayamaFacilityState.WaitingForInput => FacilityProcessingState.WaitingForInput,
                _ => FacilityProcessingState.Idle
            };
            dto.EnsureCollections();
            dto.inputItems.Clear();
            dto.outputItems.Clear();
            dto.fuelItems.Clear();
            CopyStacks(inputItems, inputAmounts, dto.inputItems);
            if (fuelItem != null && fuelAmount > 0)
            {
                dto.fuelItems.Add(new ItemStackSaveDto
                {
                    itemDefinitionId = fuelItem.DefinitionId,
                    amount = fuelAmount
                });
            }
        }

        public void ApplySave(FacilitySaveDto dto)
        {
            if (dto == null)
            {
                return;
            }

            instanceId = dto.instanceId;
            ClearStacks(inputItems, inputAmounts);
            fuelItem = null;
            fuelAmount = 0;
            progressSeconds = 0f;
            LoadStacks(dto.inputItems, inputItems, inputAmounts);
            if (dto.fuelItems != null &&
                dto.fuelItems.Count > 0 &&
                GameDataCatalogs.KomayamaItems.TryGet(
                    dto.fuelItems[0].itemDefinitionId,
                    out ItemDefinition savedFuel))
            {
                fuelItem = savedFuel;
                fuelAmount = Mathf.Max(0, dto.fuelItems[0].amount);
            }

            recipe = null;
            if (!string.IsNullOrEmpty(dto.selectedRecipeId) &&
                GameDataCatalogs.KomayamaRecipes.TryGet(
                    dto.selectedRecipeId,
                    out RecipeDefinition savedRecipe))
            {
                recipe = savedRecipe;
            }

            state = dto.processingState switch
            {
                FacilityProcessingState.Processing => KomayamaFacilityState.Processing,
                FacilityProcessingState.WaitingForFuel => KomayamaFacilityState.WaitingForFuel,
                FacilityProcessingState.WaitingForInput => KomayamaFacilityState.WaitingForInput,
                FacilityProcessingState.WaitingForOutput => KomayamaFacilityState.WaitingForInput,
                FacilityProcessingState.Paused => KomayamaFacilityState.Idle,
                _ => recipe == null
                    ? KomayamaFacilityState.Idle
                    : KomayamaFacilityState.WaitingForInput
            };
            if (recipe != null && recipe.ProcessingSeconds > 0f)
            {
                progressSeconds = Mathf.Clamp01(dto.processingProgress01) * recipe.ProcessingSeconds;
            }

            NotifyStateChanged();
        }

        private void Update()
        {
            if (recipe == null)
            {
                if (state != KomayamaFacilityState.Idle)
                {
                    state = KomayamaFacilityState.Idle;
                    NotifyStateChanged();
                }

                return;
            }

            if (state == KomayamaFacilityState.WaitingForInput ||
                state == KomayamaFacilityState.WaitingForFuel ||
                state == KomayamaFacilityState.Idle)
            {
                TryStartProcessing();
                return;
            }

            if (state != KomayamaFacilityState.Processing)
            {
                return;
            }

            progressSeconds += Time.deltaTime;
            worldOverlay?.Refresh(this);
            if (progressSeconds < recipe.ProcessingSeconds)
            {
                return;
            }

            CompleteProcessing();
        }

        public bool TryDepositOne(KomayamaHandInventory hand)
        {
            return TryDepositOne(hand, out _);
        }

        public bool TryDepositOne(KomayamaHandInventory hand, out string failureReason)
        {
            failureReason = null;
            if (hand == null || hand.IsEmpty)
            {
                failureReason = "投入できる素材がありません";
                return false;
            }

            ItemDefinition incoming = hand.FindFirst(
                item => CanAcceptFuel(item) || AcceptsAsRecipeInput(item));
            if (incoming == null)
            {
                failureReason = recipe != null
                    ? "このレシピには投入できる素材がありません"
                    : "投入できる燃料がありません";
                return false;
            }

            if (CanAcceptFuel(incoming) && !AcceptsAsRecipeInput(incoming))
            {
                return TryDepositFuel(hand, incoming, out failureReason);
            }

            if (AcceptsAsRecipeInput(incoming))
            {
                if (definition == null || InputAmount >= definition.InputCapacity)
                {
                    failureReason = "設備の材料置き場が満杯です";
                    return false;
                }

                if (!hand.TryRemoveOne(incoming, out _))
                {
                    failureReason = "投入できる素材がありません";
                    return false;
                }

                AddStack(inputItems, inputAmounts, incoming, 1);
                seManager?.Play(KomayamaCraftSeCue.Deposit);
                TryStartProcessing();
                NotifyStateChanged();
                return true;
            }

            failureReason = "投入できません";
            return false;
        }

        public bool NeedsFuelSupply
        {
            get
            {
                return definition != null &&
                       definition.UsesFuel &&
                       recipe != null &&
                       recipe.UsesFuel &&
                       fuelAmount < definition.FuelCapacity &&
                       (state == KomayamaFacilityState.WaitingForFuel ||
                        HasAllInputs());
            }
        }

        public bool CanAcceptFuel(ItemDefinition item)
        {
            return IsAcceptedFuel(item) &&
                   definition != null &&
                   fuelAmount < definition.FuelCapacity &&
                   (fuelItem == null || fuelItem == item);
        }

        public bool TryAcceptFuel(ItemDefinition item)
        {
            if (!CanAcceptFuel(item))
            {
                return false;
            }

            fuelItem = item;
            fuelAmount++;
            TryStartProcessing();
            NotifyStateChanged();
            return true;
        }

        /// <summary>旧左クリック回収。完成品は常時地面排出のため未使用。</summary>
        public bool TryCollectOne(KomayamaHandInventory hand)
        {
            return TryCollectOne(hand, out _);
        }

        public bool TryCollectOne(KomayamaHandInventory hand, out string failureReason)
        {
            failureReason = "完成品は施設下へ自動排出されます";
            return false;
        }

        public string DescribeStatusMessage()
        {
            if (recipe == null)
            {
                return "レシピ未選択";
            }

            return state switch
            {
                KomayamaFacilityState.Processing => "生産中",
                KomayamaFacilityState.WaitingForFuel => "燃料を入れてください",
                KomayamaFacilityState.WaitingForInput => "素材を入れてください",
                _ => "待機中"
            };
        }

        private bool TryDepositFuel(
            KomayamaHandInventory hand,
            ItemDefinition incoming,
            out string failureReason)
        {
            failureReason = null;
            if (definition == null || fuelAmount >= definition.FuelCapacity)
            {
                failureReason = "燃料置き場が満杯です";
                return false;
            }

            if (fuelItem != null && fuelItem != incoming)
            {
                failureReason = $"燃料は{fuelItem.DisplayName}だけ受け入れます";
                return false;
            }

            if (!hand.TryRemoveOne(incoming, out _))
            {
                failureReason = "投入できる素材がありません";
                return false;
            }

            fuelItem = incoming;
            fuelAmount++;
            seManager?.Play(KomayamaCraftSeCue.Deposit);
            TryStartProcessing();
            NotifyStateChanged();
            return true;
        }

        private void TryStartProcessing()
        {
            if (recipe == null || state == KomayamaFacilityState.Processing)
            {
                return;
            }

            if (!HasAllInputs())
            {
                SetState(KomayamaFacilityState.WaitingForInput);
                return;
            }

            if (recipe.UsesFuel)
            {
                if (fuelAmount <= 0 || fuelItem == null)
                {
                    SetState(KomayamaFacilityState.WaitingForFuel);
                    return;
                }
            }

            ConsumeRecipeInputs();
            if (recipe.UsesFuel)
            {
                fuelAmount--;
                if (fuelAmount <= 0)
                {
                    fuelAmount = 0;
                    fuelItem = null;
                }
            }

            progressSeconds = 0f;
            SetState(KomayamaFacilityState.Processing);
        }

        private void CompleteProcessing()
        {
            progressSeconds = 0f;
            Vector2 origin = GetDropOrigin();
            if (recipe.OutputMode == RecipeOutputMode.WeightedSingle)
            {
                if (recipe.TrySelectWeightedOutput(Random.Range(0, int.MaxValue), out RecipeOutput selected) &&
                    selected.Item != null)
                {
                    KomayamaFacilityContents.DropOnly(
                        selected.Item,
                        selected.Amount,
                        dropArea,
                        origin);
                    KomayamaProgressService.Instance?.NotifyCrafted(selected.Item);
                }
            }
            else
            {
                for (int i = 0; i < recipe.Outputs.Count; i++)
                {
                    RecipeOutput output = recipe.Outputs[i];
                    if (output.Item == null || output.Amount <= 0)
                    {
                        continue;
                    }

                    KomayamaFacilityContents.DropOnly(
                        output.Item,
                        output.Amount,
                        dropArea,
                        origin);
                    KomayamaProgressService.Instance?.NotifyCrafted(output.Item);
                }
            }

            seManager?.Play(KomayamaCraftSeCue.ProcessingComplete);
            hud?.ShowMessage($"{recipe.DisplayName}が完了しました");
            ItemProduced?.Invoke(this, recipe);
            SetState(KomayamaFacilityState.WaitingForInput);
            TryStartProcessing();
        }

        /// <summary>1回の生産が完了したとき（レシピ単位）。</summary>
        public static event System.Action<KomayamaProcessingFacility, RecipeDefinition>
            ItemProduced;

        private void AbortProcessingKeepFuel()
        {
            progressSeconds = 0f;
            if (state == KomayamaFacilityState.Processing)
            {
                state = recipe == null
                    ? KomayamaFacilityState.Idle
                    : KomayamaFacilityState.WaitingForInput;
            }
        }

        private void DropUnusedInputs()
        {
            DumpStacksToGround(inputItems, inputAmounts, dropArea);
            ClearStacks(inputItems, inputAmounts);
        }

        private void DropFuel()
        {
            if (fuelItem != null && fuelAmount > 0)
            {
                KomayamaFacilityContents.DropOnly(
                    fuelItem,
                    fuelAmount,
                    dropArea,
                    GetDropOrigin());
            }

            fuelItem = null;
            fuelAmount = 0;
        }

        private void DumpStacksToGround(
            List<ItemDefinition> items,
            List<int> amounts,
            KomayamaDropArea target)
        {
            Vector2 origin = GetDropOrigin();
            for (int i = 0; i < items.Count; i++)
            {
                KomayamaFacilityContents.DropOnly(items[i], amounts[i], target, origin);
            }
        }

        private bool HasAllInputs()
        {
            if (recipe == null || recipe.Inputs.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                ItemAmount required = recipe.Inputs[i];
                if (required.Item == null ||
                    GetAmount(inputItems, inputAmounts, required.Item) < required.Amount)
                {
                    return false;
                }
            }

            return true;
        }

        private void ConsumeRecipeInputs()
        {
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                ItemAmount required = recipe.Inputs[i];
                RemoveAmount(inputItems, inputAmounts, required.Item, required.Amount);
            }
        }

        private bool AcceptsAsRecipeInput(ItemDefinition item)
        {
            return RequiredInputAmount(item) > 0 &&
                   definition != null &&
                   InputAmount < definition.InputCapacity;
        }

        private int RequiredInputAmount(ItemDefinition item)
        {
            if (recipe == null || item == null)
            {
                return 0;
            }

            int required = 0;
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                if (recipe.Inputs[i].Item == item)
                {
                    required += recipe.Inputs[i].Amount;
                }
            }

            return required;
        }

        private bool IsAcceptedFuel(ItemDefinition item)
        {
            if (definition == null || !definition.UsesFuel || item == null)
            {
                return false;
            }

            for (int i = 0; i < definition.AcceptedFuelItems.Count; i++)
            {
                if (definition.AcceptedFuelItems[i] == item)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureOverlay()
        {
            if (worldOverlay == null)
            {
                worldOverlay = GetComponent<KomayamaFacilityWorldOverlay>();
            }

            worldOverlay?.Refresh(this);
        }

        private void SetState(KomayamaFacilityState next)
        {
            if (state == next)
            {
                worldOverlay?.Refresh(this);
                return;
            }

            state = next;
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            worldOverlay?.Refresh(this);
            StateChanged?.Invoke();
        }

        private static void AddStack(
            List<ItemDefinition> items,
            List<int> amounts,
            ItemDefinition item,
            int amount)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == item)
                {
                    amounts[i] += amount;
                    return;
                }
            }

            items.Add(item);
            amounts.Add(amount);
        }

        private static int GetAmount(
            List<ItemDefinition> items,
            List<int> amounts,
            ItemDefinition item)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == item)
                {
                    return amounts[i];
                }
            }

            return 0;
        }

        private static void RemoveAmount(
            List<ItemDefinition> items,
            List<int> amounts,
            ItemDefinition item,
            int amount)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != item)
                {
                    continue;
                }

                amounts[i] -= amount;
                if (amounts[i] <= 0)
                {
                    items.RemoveAt(i);
                    amounts.RemoveAt(i);
                }

                return;
            }
        }

        private static void ClearStacks(List<ItemDefinition> items, List<int> amounts)
        {
            items.Clear();
            amounts.Clear();
        }

        private static void CopyStacks(
            List<ItemDefinition> items,
            List<int> amounts,
            List<ItemStackSaveDto> destination)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null || amounts[i] <= 0)
                {
                    continue;
                }

                destination.Add(new ItemStackSaveDto
                {
                    itemDefinitionId = items[i].DefinitionId,
                    amount = amounts[i]
                });
            }
        }

        private static void LoadStacks(
            List<ItemStackSaveDto> source,
            List<ItemDefinition> items,
            List<int> amounts)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (!GameDataCatalogs.KomayamaItems.TryGet(
                        source[i].itemDefinitionId,
                        out ItemDefinition item))
                {
                    continue;
                }

                AddStack(items, amounts, item, Mathf.Max(0, source[i].amount));
            }
        }

        private static int Total(List<int> amounts)
        {
            int total = 0;
            for (int i = 0; i < amounts.Count; i++)
            {
                total += amounts[i];
            }

            return total;
        }

        private static string DescribeStacks(
            List<ItemDefinition> items,
            List<int> amounts,
            string emptyLabel)
        {
            if (items.Count == 0)
            {
                return emptyLabel;
            }

            string text = string.Empty;
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    text += " ";
                }

                text += $"{items[i].DisplayName}×{amounts[i]}";
            }

            return text;
        }
    }
}
