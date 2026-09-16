using UnityEngine;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaDroppedItem : MonoBehaviour
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField] private KCItemSettings itemSettings;
        [SerializeField, Min(0.01f)] private float overlapRadius = 0.4f;
        [SerializeField] private Collider2D pickupCollider;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private KomayamaDropArea owner;
        private string instanceId;
        private Vector2 dropCenter;
        private Vector2 animationStart;
        private float animationDuration;
        private float animationArcHeight;
        private float animationElapsed;
        private bool isDropAnimating;
        private Vector2 pushStart;
        private float pushDuration;
        private float pushElapsed;
        private bool isPushAnimating;

        public ItemDefinition Item => item;
        public int Amount => amount;
        public string InstanceId => instanceId;
        public float OverlapRadius =>
            itemSettings != null ? itemSettings.PushRadius : overlapRadius;
        public Vector2 DropCenter => dropCenter;
        public bool IsDropAnimating => isDropAnimating;
        public bool IsMoving => isDropAnimating || isPushAnimating;

        private void Awake()
        {
            if (pickupCollider == null)
            {
                pickupCollider = GetComponent<Collider2D>();
            }

            dropCenter = transform.position;
            ApplyPresentation();
        }

        public void SetItemSettings(KCItemSettings settings)
        {
            if (settings != null)
            {
                itemSettings = settings;
            }

            ApplyPresentation();
        }

        private void Update()
        {
            if (isDropAnimating)
            {
                UpdateDropAnimation();
                return;
            }

            if (isPushAnimating)
            {
                UpdatePushAnimation();
            }
        }

        private void UpdateDropAnimation()
        {
            animationElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(animationElapsed / animationDuration);
            Vector2 position = Vector2.Lerp(animationStart, dropCenter, t);
            position.y += 4f * animationArcHeight * t * (1f - t);
            transform.position = position;

            if (t < 1f)
            {
                return;
            }

            isDropAnimating = false;
            transform.position = dropCenter;
            RefreshColliderState();
        }

        private void UpdatePushAnimation()
        {
            pushElapsed += Time.deltaTime;
            float linearT = Mathf.Clamp01(pushElapsed / pushDuration);
            transform.position = Vector2.LerpUnclamped(
                pushStart,
                dropCenter,
                EaseInOutBack(linearT));
            if (linearT < 1f)
            {
                return;
            }

            isPushAnimating = false;
            transform.position = dropCenter;
            RefreshColliderState();
        }

        private static float EaseInOutBack(float t)
        {
            const float Overshoot = 1.70158f;
            const float Scaled = Overshoot * 1.525f;
            if (t < 0.5f)
            {
                float early = 2f * t;
                return early * early * ((Scaled + 1f) * early - Scaled) * 0.5f;
            }

            float late = 2f * t - 2f;
            return (late * late * ((Scaled + 1f) * late + Scaled) + 2f) * 0.5f;
        }

        public void Initialize(
            ItemDefinition definition,
            int quantity,
            KomayamaDropArea dropArea)
        {
            item = definition;
            amount = Mathf.Max(1, quantity);
            owner = dropArea;
            instanceId = GameDataId.CreateInstanceId();
            name = definition != null
                ? $"Dropped_{definition.DefinitionId}"
                : "DroppedItem";
            ApplyPresentation();
        }

        public void SetDropCenter(Vector2 center)
        {
            dropCenter = center;
            if (!IsMoving)
            {
                transform.position = center;
            }
        }

        public void MoveDropCenter(Vector2 center, float duration)
        {
            if (Vector2.Distance(dropCenter, center) <= 0.0001f)
            {
                return;
            }

            dropCenter = center;
            if (isDropAnimating)
            {
                return;
            }

            float distance = Vector2.Distance(transform.position, center);
            pushStart = transform.position;
            pushDuration = Mathf.Max(0.01f, duration) * Mathf.Lerp(0.75f, 1.35f, Mathf.Clamp01(distance / 2.5f));
            pushElapsed = 0f;
            isPushAnimating = true;
            RefreshColliderState();
        }

        public void PlayDropAnimation(
            Vector2 sourcePosition,
            float duration,
            float arcHeight)
        {
            animationStart = sourcePosition;
            animationDuration = Mathf.Max(0.01f, duration);
            animationArcHeight = Mathf.Max(0f, arcHeight);
            animationElapsed = 0f;
            isDropAnimating = true;
            transform.position = sourcePosition;
            if (pickupCollider != null)
            {
                RefreshColliderState();
            }
        }

        public void RestoreIdentity(string savedInstanceId, Vector2 center)
        {
            if (!string.IsNullOrEmpty(savedInstanceId))
            {
                instanceId = savedInstanceId;
            }

            dropCenter = center;
            transform.position = center;
            isDropAnimating = false;
            isPushAnimating = false;
            RefreshColliderState();
        }

        public bool TryTakeOne(KomayamaHandInventory hand)
        {
            if (IsMoving ||
                hand == null ||
                item == null ||
                !hand.TryAdd(item))
            {
                return false;
            }

            ConsumeOne();
            return true;
        }

        public bool TryExtractOne(out ItemDefinition extracted)
        {
            extracted = null;
            if (IsMoving || item == null || amount <= 0)
            {
                return false;
            }

            extracted = item;
            ConsumeOne();
            return true;
        }

        private void ConsumeOne()
        {
            amount--;
            if (amount <= 0)
            {
                owner?.Unregister(this);
                Destroy(gameObject);
            }
        }

        private void RefreshColliderState()
        {
            if (pickupCollider != null)
            {
                pickupCollider.enabled = !IsMoving;
            }
        }

        public void ApplyPresentation()
        {
            transform.localScale = Vector3.one;
            if (spriteRenderer == null)
            {
                Transform visual = transform.Find("見た目");
                spriteRenderer = visual != null
                    ? visual.GetComponent<SpriteRenderer>()
                    : GetComponentInChildren<SpriteRenderer>();
            }

            if (pickupCollider == null)
            {
                pickupCollider = GetComponent<Collider2D>();
            }

            if (item != null && item.Icon != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = item.Icon;
            }

            Vector2 visualSize = itemSettings != null
                ? itemSettings.VisualSize
                : new Vector2(1f, 1f);
            ApplyVisualSize(visualSize);
            ApplyPickupRadius(
                itemSettings != null ? itemSettings.PickupRadius : 0.6f);
            RefreshColliderState();
        }

        private void ApplyVisualSize(Vector2 visualSize)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            spriteRenderer.color = Color.white;
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            spriteRenderer.sortingLayerName = "WorldDrop";
            spriteRenderer.sortingOrder = 10;
            if (spriteRenderer.transform == transform)
            {
                return;
            }

            Vector2 native = spriteRenderer.sprite.bounds.size;
            float scale = Mathf.Min(
                visualSize.x / Mathf.Max(0.0001f, native.x),
                visualSize.y / Mathf.Max(0.0001f, native.y));
            spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void ApplyPickupRadius(float radius)
        {
            if (pickupCollider is CircleCollider2D circle)
            {
                circle.radius = radius;
                circle.offset = Vector2.zero;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyPresentation();
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? (Vector3)dropCenter : transform.position;
            float pickup = itemSettings != null ? itemSettings.PickupRadius : 0.6f;
            float push = OverlapRadius;
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireSphere(center, pickup);
            Gizmos.color = new Color(1f, 0.45f, 0.15f, 0.9f);
            Gizmos.DrawWireSphere(center, push);
            if (itemSettings != null)
            {
                Vector2 visual = itemSettings.VisualSize;
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
                Gizmos.DrawWireCube(center, new Vector3(visual.x, visual.y, 0f));
            }
        }
#endif
    }
}
