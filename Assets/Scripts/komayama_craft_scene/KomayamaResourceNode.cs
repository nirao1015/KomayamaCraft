using System.Collections;
using UnityEngine;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaResourceNode : MonoBehaviour
    {
        [SerializeField] private ResourceNodeDefinition definition;
        [SerializeField] private string instanceId;
        [SerializeField] private KomayamaDropArea dropArea;
        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private KomayamaCraftSeManager seManager;

        public ResourceNodeDefinition Definition => definition;
        public string InstanceId => instanceId;
        public float Progress01 => 0f;
        public bool IsGathering => false;
        public bool IsCoolingDown => false;

        private void Awake()
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = GameDataId.CreateInstanceId();
            }
        }

        public void RestoreIdentity(string savedInstanceId)
        {
            if (!string.IsNullOrEmpty(savedInstanceId))
            {
                instanceId = savedInstanceId;
            }
        }

        public bool TryGather()
        {
            if (definition == null || dropArea == null)
            {
                return false;
            }

            if (!KomayamaRegion.CanInteract(transform.position, out string regionReason))
            {
                hud?.ShowMessage(regionReason);
                seManager?.Play(KomayamaCraftSeCue.Invalid);
                return false;
            }

            bool allSpawned = true;
            for (int i = 0; i < definition.Yields.Count; i++)
            {
                ItemAmount yield = definition.Yields[i];
                for (int amountIndex = 0; amountIndex < yield.Amount; amountIndex++)
                {
                    if (!dropArea.TrySpawnNear(
                            yield.Item,
                            1,
                            transform,
                            out _))
                    {
                        allSpawned = false;
                    }
                }
            }

            if (allSpawned)
            {
                seManager?.Play(KomayamaCraftSeCue.Gather);
                StartCoroutine(PlayGatherReaction());
                KomayamaProgressService.Instance?.NotifyGathered();
            }
            else
            {
                seManager?.Play(KomayamaCraftSeCue.Invalid);
                hud?.ShowMessage("置ける場所がないため、素材が消滅しました");
            }

            return allSpawned;
        }

        private IEnumerator PlayGatherReaction()
        {
            Vector3 originalScale = transform.localScale;
            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            Color originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            transform.localScale = originalScale * 1.12f;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(originalColor, Color.white, 0.55f);
            }

            yield return new WaitForSeconds(0.08f);
            transform.localScale = originalScale;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }
    }
}
