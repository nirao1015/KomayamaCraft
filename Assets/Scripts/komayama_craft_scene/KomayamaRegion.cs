using UnityEngine;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaRegion : MonoBehaviour
    {
        [SerializeField] private string regionId = "tier2";
        [SerializeField] private KomayamaProgressService progress;
        [SerializeField] private SpriteRenderer marker;
        [SerializeField] private ItemDefinition requiredCraftedItem;
        [SerializeField] private string lockedReason = "未解放の地域です";

        public string RegionId => regionId;

        public bool IsUnlocked
        {
            get
            {
                bool regionOk = progress == null || progress.IsRegionUnlocked(regionId);
                bool craftOk = requiredCraftedItem == null ||
                    (progress != null && progress.HasCrafted(requiredCraftedItem));
                return regionOk && craftOk;
            }
        }

        public string LockReason
        {
            get
            {
                if (requiredCraftedItem != null &&
                    (progress == null || !progress.HasCrafted(requiredCraftedItem)))
                {
                    return $"{requiredCraftedItem.DisplayName}を作るまで入れません";
                }

                return string.IsNullOrEmpty(lockedReason) ? "未解放の地域です" : lockedReason;
            }
        }

        public bool Contains(Vector2 position)
        {
            return TryGetComponent(out Collider2D collider) && collider.OverlapPoint(position);
        }

        public static bool CanInteract(Vector2 position, out string failureReason)
        {
            failureReason = null;
            KomayamaRegion[] regions = FindObjectsByType<KomayamaRegion>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < regions.Length; i++)
            {
                KomayamaRegion region = regions[i];
                if (region != null && region.Contains(position) && !region.IsUnlocked)
                {
                    failureReason = region.LockReason;
                    return false;
                }
            }

            return true;
        }

        private void Update()
        {
            if (marker != null)
            {
                marker.color = IsUnlocked
                    ? new Color(0.3f, 1f, 0.45f, 0.25f)
                    : new Color(1f, 0.25f, 0.25f, 0.25f);
            }
        }
    }
}
