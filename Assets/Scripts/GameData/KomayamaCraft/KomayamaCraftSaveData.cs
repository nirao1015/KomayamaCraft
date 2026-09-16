using System;
using System.Collections.Generic;

/// <summary>
/// 新作KomayamaCraftのJsonUtility用ルートDTO。
/// ファイルI/O、定義解決、実行時モデルへの適用は担当しない。
/// </summary>
[Serializable]
public sealed class KomayamaCraftSaveData
{
    public const int CurrentVersion = 5;

    public int version = CurrentVersion;
    public string buildVersion = string.Empty;
    public string updatedAtUtc = string.Empty;

    public int currentTier = 1;
    public float gameplayElapsedSeconds;
    public HandInventorySaveDto handInventory = new HandInventorySaveDto();
    public List<GroundItemSaveDto> groundItems = new List<GroundItemSaveDto>();
    public List<FacilitySaveDto> facilities = new List<FacilitySaveDto>();
    public List<ResourceNodeSaveDto> resourceNodes = new List<ResourceNodeSaveDto>();
    public List<UnlockStateRecord> unlockStates = new List<UnlockStateRecord>();
    public List<DeliveryProgressSaveDto> deliveryProgress =
        new List<DeliveryProgressSaveDto>();
    public List<GhostSaveDto> ghosts = new List<GhostSaveDto>();
    public ShipProgressSaveDto ship = new ShipProgressSaveDto();
    public ProgressSaveDto progress = new ProgressSaveDto();
    public FoxFollowerSaveDto foxFollower = new FoxFollowerSaveDto();
    public CameraViewSaveDto cameraView = new CameraViewSaveDto();

    /// <summary>
    /// 欠損フィールドを含むJSONの復元後に、コレクションを安全な空状態へ補完する。
    /// 未知IDの除去や定義解決は行わない。
    /// </summary>
    public void EnsureCollections()
    {
        if (handInventory == null)
        {
            handInventory = new HandInventorySaveDto();
        }

        if (groundItems == null)
        {
            groundItems = new List<GroundItemSaveDto>();
        }

        if (facilities == null)
        {
            facilities = new List<FacilitySaveDto>();
        }

        if (resourceNodes == null)
        {
            resourceNodes = new List<ResourceNodeSaveDto>();
        }

        if (unlockStates == null)
        {
            unlockStates = new List<UnlockStateRecord>();
        }

        if (deliveryProgress == null)
        {
            deliveryProgress = new List<DeliveryProgressSaveDto>();
        }

        if (ghosts == null)
        {
            ghosts = new List<GhostSaveDto>();
        }

        if (ship == null)
        {
            ship = new ShipProgressSaveDto();
        }

        if (progress == null)
        {
            progress = new ProgressSaveDto();
        }

        if (foxFollower == null)
        {
            foxFollower = new FoxFollowerSaveDto();
        }

        if (cameraView == null)
        {
            cameraView = new CameraViewSaveDto();
        }

        ship.EnsureCollections();
        progress.EnsureCollections();

        handInventory.EnsureCollections();
        for (int i = 0; i < facilities.Count; i++)
        {
            facilities[i]?.EnsureCollections();
        }

        for (int i = 0; i < deliveryProgress.Count; i++)
        {
            deliveryProgress[i]?.EnsureCollections();
        }
    }
}

/// <summary>
/// UnityEngine.Vector2を保存DTOへ持ち込まないための2成分数値。
/// </summary>
[Serializable]
public struct Float2SaveDto
{
    public float x;
    public float y;

    public Float2SaveDto(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
}

[Serializable]
public sealed class ItemStackSaveDto
{
    public string itemDefinitionId = string.Empty;
    public int amount;
}

[Serializable]
public sealed class HandInventorySaveDto
{
    public int capacity;
    public List<ItemStackSaveDto> items = new List<ItemStackSaveDto>();

    public void EnsureCollections()
    {
        if (items == null)
        {
            items = new List<ItemStackSaveDto>();
        }
    }
}

[Serializable]
public sealed class GroundItemSaveDto
{
    public string itemDefinitionId = string.Empty;
    public string instanceId = string.Empty;
    public Float2SaveDto position;
    public int amount;
}

public enum FacilityProcessingState
{
    Idle = 0,
    WaitingForInput = 1,
    WaitingForFuel = 2,
    Processing = 3,
    WaitingForOutput = 4,
    Paused = 5
}

[Serializable]
public sealed class FacilitySaveDto
{
    public string facilityDefinitionId = string.Empty;
    public string instanceId = string.Empty;
    public Float2SaveDto position;
    public float rotationDegrees;

    public List<ItemStackSaveDto> inputItems = new List<ItemStackSaveDto>();
    public List<ItemStackSaveDto> outputItems = new List<ItemStackSaveDto>();
    public List<ItemStackSaveDto> fuelItems = new List<ItemStackSaveDto>();
    public float storedFuelEnergy;

    public string selectedRecipeId = string.Empty;
    public FacilityProcessingState processingState;
    public float processingProgress01;

    public void EnsureCollections()
    {
        if (inputItems == null)
        {
            inputItems = new List<ItemStackSaveDto>();
        }

        if (outputItems == null)
        {
            outputItems = new List<ItemStackSaveDto>();
        }

        if (fuelItems == null)
        {
            fuelItems = new List<ItemStackSaveDto>();
        }
    }
}

[Serializable]
public sealed class GhostSaveDto
{
    public string facilityDefinitionId = string.Empty;
    public string instanceId = string.Empty;
    public Float2SaveDto position;
    public bool isEnabled = true;
    public string assignedResourceNodeInstanceId = string.Empty;
    public string carriedItemDefinitionId = string.Empty;
    public int carriedAmount;
    public string stopReason = string.Empty;
}

[Serializable]
public sealed class ShipSlotSaveDto
{
    public string slotId = string.Empty;
    public string requiredItemDefinitionId = string.Empty;
    public bool isInstalled;
}

[Serializable]
public sealed class ShipProgressSaveDto
{
    public List<ShipSlotSaveDto> slots = new List<ShipSlotSaveDto>();

    public void EnsureCollections()
    {
        if (slots == null)
        {
            slots = new List<ShipSlotSaveDto>();
        }
    }
}

[Serializable]
public sealed class ProgressSaveDto
{
    public int experience;
    public int level = 1;
    public int skillPoints;
    public List<string> unlockedSkillIds = new List<string>();
    public List<string> unlockedBlueprintIds = new List<string>();
    public List<string> unlockedRegionIds = new List<string>();
    public List<string> craftedItemIds = new List<string>();
    public bool hasClearedEnding;

    public void EnsureCollections()
    {
        if (unlockedSkillIds == null)
        {
            unlockedSkillIds = new List<string>();
        }

        if (unlockedBlueprintIds == null)
        {
            unlockedBlueprintIds = new List<string>();
        }

        if (unlockedRegionIds == null)
        {
            unlockedRegionIds = new List<string>();
        }

        if (craftedItemIds == null)
        {
            craftedItemIds = new List<string>();
        }
    }
}

[Serializable]
public sealed class ResourceNodeSaveDto
{
    public string resourceNodeDefinitionId = string.Empty;
    public string instanceId = string.Empty;
    public bool isDepleted;
    public float gatheringProgress01;
    public float reharvestRemainingSeconds;
}

[Serializable]
public sealed class FoxFollowerSaveDto
{
    public int displayMode;
    public float screenViewportX = 0.2f;
    public float screenViewportY = 0.5f;
    public float displayHeight = 2.4f;
}

/// <summary>
/// フィールドカメラの位置とズーム（orthographicSize）。
/// orthographicSize が未設定（旧セーブ）のときはロード時に適用しない。
/// </summary>
[Serializable]
public sealed class CameraViewSaveDto
{
    public Float2SaveDto position;
    public float orthographicSize;
}

[Serializable]
public sealed class DeliveredItemProgressSaveDto
{
    public string itemDefinitionId = string.Empty;
    public int deliveredAmount;
}

/// <summary>
/// 納品済み数量、納品条件達成、完了報告を個別に保持する。
/// 所持数やUnlockStateRecordの解放済み状態とは共有しない。
/// </summary>
[Serializable]
public sealed class DeliveryProgressSaveDto
{
    public string progressId = string.Empty;
    public List<DeliveredItemProgressSaveDto> deliveredItems =
        new List<DeliveredItemProgressSaveDto>();
    public bool isDeliveryComplete;
    public bool isCompletionReported;

    public void EnsureCollections()
    {
        if (deliveredItems == null)
        {
            deliveredItems = new List<DeliveredItemProgressSaveDto>();
        }
    }
}
