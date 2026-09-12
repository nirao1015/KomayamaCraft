using System.Collections.Generic;
using UnityEngine;

namespace KomayamaCraft
{
    public enum KomayamaFacilityState
    {
        WaitingForInput,
        WaitingForFuel,
        Processing,
        WaitingForOutput
    }

    [DisallowMultipleComponent]
    public sealed class KomayamaProcessingFacility : MonoBehaviour
    {
        [SerializeField] private FacilityDefinition definition;
        [SerializeField] private RecipeDefinition recipe;
        [SerializeField] private string instanceId;
        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private KomayamaCraftSeManager seManager;

        private readonly List<ItemDefinition> inputItems = new();
        private readonly List<int> inputAmounts = new();
        private readonly List<ItemDefinition> outputItems = new();
        private readonly List<int> outputAmounts = new();
        private ItemDefinition fuelItem;
        private int fuelAmount;
        private float progressSeconds;
        private KomayamaFacilityState state = KomayamaFacilityState.WaitingForInput;

        public FacilityDefinition Definition => definition;
        public RecipeDefinition Recipe => recipe;
        public string InstanceId => instanceId;
        public int InputAmount => Total(inputAmounts);
        public int OutputAmount => Total(outputAmounts);
        public int FuelAmount => fuelAmount;
        public string FuelItemName =>
            fuelItem != null ? fuelItem.DisplayName : "燃料なし";
        public string RecipeName => recipe != null ? recipe.DisplayName : "未選択";
        public string InputItemName => DescribeStacks(inputItems, inputAmounts, "材料なし");
        public string OutputItemName => DescribeStacks(outputItems, outputAmounts, "完成品なし");
        public float Progress01 => recipe != null && recipe.ProcessingSeconds > 0f
            ? Mathf.Clamp01(progressSeconds / recipe.ProcessingSeconds)
            : 0f;
        public KomayamaFacilityState State => state;

        private void Awake()
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = GameDataId.CreateInstanceId();
            }
        }

        public void BindRuntime(KomayamaCraftHud runtimeHud, KomayamaCraftSeManager runtimeSe)
        {
            if (hud == null)
            {
                hud = runtimeHud;
            }

            if (seManager == null)
            {
                seManager = runtimeSe;
            }
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
        }

        public bool TryCycleRecipe(int delta, out string failureReason)
        {
            failureReason = null;
            if (definition == null || definition.SupportedRecipes.Count == 0)
            {
                failureReason = "切り替えられるレシピがありません";
                return false;
            }

            if (state == KomayamaFacilityState.Processing ||
                InputAmount > 0 ||
                OutputAmount > 0)
            {
                failureReason = "材料や完成品がある間はレシピを変えられません";
                return false;
            }

            int current = 0;
            for (int i = 0; i < definition.SupportedRecipes.Count; i++)
            {
                if (definition.SupportedRecipes[i] == recipe)
                {
                    current = i;
                    break;
                }
            }

            int next = current + delta;
            int count = definition.SupportedRecipes.Count;
            next %= count;
            if (next < 0)
            {
                next += count;
            }

            recipe = definition.SupportedRecipes[next];
            state = KomayamaFacilityState.WaitingForInput;
            hud?.ShowMessage($"レシピ: {RecipeName}");
            return true;
        }

        public void EvacuateTo(KomayamaHandInventory hand, KomayamaDropArea dropArea)
        {
            if (state == KomayamaFacilityState.Processing &&
                recipe != null)
            {
                RefundRecipeInputs(hand, dropArea);
                hud?.ShowMessage("加工を中断し、材料を戻しました");
            }

            DumpStacks(outputItems, outputAmounts, hand, dropArea);
            DumpStacks(inputItems, inputAmounts, hand, dropArea);
            if (fuelItem != null && fuelAmount > 0)
            {
                KomayamaFacilityContents.GiveOrDrop(
                    fuelItem,
                    fuelAmount,
                    hand,
                    dropArea,
                    transform.position);
            }

            ClearStacks(inputItems, inputAmounts);
            ClearStacks(outputItems, outputAmounts);
            fuelItem = null;
            fuelAmount = 0;
            progressSeconds = 0f;
            state = KomayamaFacilityState.WaitingForInput;
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
                KomayamaFacilityState.WaitingForOutput => FacilityProcessingState.WaitingForOutput,
                KomayamaFacilityState.WaitingForFuel => FacilityProcessingState.WaitingForFuel,
                _ => FacilityProcessingState.WaitingForInput
            };
            dto.EnsureCollections();
            dto.inputItems.Clear();
            dto.outputItems.Clear();
            dto.fuelItems.Clear();
            CopyStacks(inputItems, inputAmounts, dto.inputItems);
            CopyStacks(outputItems, outputAmounts, dto.outputItems);
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
            ClearStacks(outputItems, outputAmounts);
            fuelItem = null;
            fuelAmount = 0;
            progressSeconds = 0f;
            LoadStacks(dto.inputItems, inputItems, inputAmounts);
            LoadStacks(dto.outputItems, outputItems, outputAmounts);
            if (dto.fuelItems != null &&
                dto.fuelItems.Count > 0 &&
                GameDataCatalogs.KomayamaItems.TryGet(
                    dto.fuelItems[0].itemDefinitionId,
                    out ItemDefinition savedFuel))
            {
                fuelItem = savedFuel;
                fuelAmount = Mathf.Max(0, dto.fuelItems[0].amount);
            }

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
                FacilityProcessingState.WaitingForOutput => KomayamaFacilityState.WaitingForOutput,
                FacilityProcessingState.WaitingForFuel => KomayamaFacilityState.WaitingForFuel,
                _ => KomayamaFacilityState.WaitingForInput
            };
            if (recipe != null && recipe.ProcessingSeconds > 0f)
            {
                progressSeconds = Mathf.Clamp01(dto.processingProgress01) * recipe.ProcessingSeconds;
            }
        }

        private void Update()
        {
            if (recipe == null)
            {
                return;
            }

            if (state == KomayamaFacilityState.WaitingForInput ||
                state == KomayamaFacilityState.WaitingForFuel)
            {
                TryStartProcessing();
                return;
            }

            if (state != KomayamaFacilityState.Processing)
            {
                return;
            }

            progressSeconds += Time.deltaTime;
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

            ItemDefinition incoming = hand.FindFirst(NeedsMoreRecipeInput)
                ?? hand.FindFirst(IsAcceptedFuel);
            if (incoming == null)
            {
                failureReason = "このレシピには投入できる素材がありません";
                return false;
            }

            bool needsMoreInput = NeedsMoreRecipeInput(incoming);
            if (needsMoreInput)
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
                return true;
            }

            if (IsAcceptedFuel(incoming))
            {
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
                return true;
            }

            failureReason = recipe != null
                ? $"このレシピには{incoming.DisplayName}を投入できません"
                : "投入できる素材がありません";
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

            if (fuelItem != null && fuelItem != item)
            {
                return false;
            }

            fuelItem = item;
            fuelAmount++;
            TryStartProcessing();
            return true;
        }

        public bool TryCollectOne(KomayamaHandInventory hand)
        {
            return TryCollectOne(hand, out _);
        }

        public bool TryCollectOne(KomayamaHandInventory hand, out string failureReason)
        {
            failureReason = null;
            if (hand == null || OutputAmount <= 0)
            {
                failureReason = "回収できる完成品がありません";
                return false;
            }

            ItemDefinition outputItem = outputItems[0];
            if (!hand.TryAdd(outputItem))
            {
                failureReason = hand.GetAddFailureReason(outputItem);
                return false;
            }

            outputAmounts[0]--;
            if (outputAmounts[0] <= 0)
            {
                outputItems.RemoveAt(0);
                outputAmounts.RemoveAt(0);
            }

            if (OutputAmount <= 0)
            {
                state = KomayamaFacilityState.WaitingForInput;
                TryStartProcessing();
            }

            seManager?.Play(KomayamaCraftSeCue.Pickup);
            return true;
        }

        private void TryStartProcessing()
        {
            if (recipe == null ||
                state == KomayamaFacilityState.Processing ||
                OutputAmount > 0)
            {
                return;
            }

            if (!HasAllInputs())
            {
                state = KomayamaFacilityState.WaitingForInput;
                return;
            }

            if (recipe.UsesFuel)
            {
                if (fuelAmount <= 0 || fuelItem == null)
                {
                    state = KomayamaFacilityState.WaitingForFuel;
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
            state = KomayamaFacilityState.Processing;
        }

        private void CompleteProcessing()
        {
            progressSeconds = 0f;
            if (recipe.OutputMode == RecipeOutputMode.WeightedSingle)
            {
                if (recipe.TrySelectWeightedOutput(Random.Range(0, int.MaxValue), out RecipeOutput selected) &&
                    selected.Item != null)
                {
                    AddStack(outputItems, outputAmounts, selected.Item, selected.Amount);
                }
            }
            else
            {
                for (int i = 0; i < recipe.Outputs.Count; i++)
                {
                    RecipeOutput output = recipe.Outputs[i];
                    if (output.Item != null && output.Amount > 0)
                    {
                        AddStack(outputItems, outputAmounts, output.Item, output.Amount);
                    }
                }
            }

            state = KomayamaFacilityState.WaitingForOutput;
            seManager?.Play(KomayamaCraftSeCue.ProcessingComplete);
            hud?.ShowMessage($"{recipe.DisplayName}が完了しました");
            for (int i = 0; i < outputItems.Count; i++)
            {
                KomayamaProgressService.Instance?.NotifyCrafted(outputItems[i]);
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

        private void RefundRecipeInputs(KomayamaHandInventory hand, KomayamaDropArea dropArea)
        {
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                ItemAmount required = recipe.Inputs[i];
                KomayamaFacilityContents.GiveOrDrop(
                    required.Item,
                    required.Amount,
                    hand,
                    dropArea,
                    transform.position);
            }
        }

        private bool IsRecipeInput(ItemDefinition item)
        {
            return RequiredInputAmount(item) > 0;
        }

        private bool NeedsMoreRecipeInput(ItemDefinition item)
        {
            int required = RequiredInputAmount(item);
            return required > 0 &&
                   GetAmount(inputItems, inputAmounts, item) < required;
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

        private void DumpStacks(
            List<ItemDefinition> items,
            List<int> amounts,
            KomayamaHandInventory hand,
            KomayamaDropArea dropArea)
        {
            for (int i = 0; i < items.Count; i++)
            {
                KomayamaFacilityContents.GiveOrDrop(
                    items[i],
                    amounts[i],
                    hand,
                    dropArea,
                    transform.position);
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
