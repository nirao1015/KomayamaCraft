using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace KomayamaCraft
{
    /// <summary>
    /// 右クリックで手持ち1個を納入して消すゴミ箱。
    /// 配置は click 生産施設と同じく、Layer_Objects 直下・NativeLife・見た目枠＝反応範囲。
    /// closed/open スプライトが揃っているときだけ、ホバー差し替えと震え演出を行う
    /// （DeliveryNpc など見た目なし納入先には付けない）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class KomayamaDepositBin : MonoBehaviour
    {
        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private KomayamaCraftSeManager seManager;

        [Header("Trash View（任意・納入ゴミ箱用）")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private Sprite openSprite;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Collider2D hitCollider;

        [Header("Shake Animation（game02 TrashDropTarget と同値）")]
        [SerializeField] private float jumpUpPixels = 16f;
        [SerializeField] private float shakeDurationSeconds = 0.22f;
        [SerializeField] private float shakeAngleDegrees = 10f;
        [SerializeField] private int shakeCount = 3;

        private Transform visual;
        private Vector3 visualBaseLocalPosition;
        private Quaternion visualBaseLocalRotation;
        private bool hasVisualBase;
        private bool pointerHover;
        private Coroutine shakeRoutine;

        /// <summary>1個納入して消したとき。</summary>
        public static event System.Action<KomayamaDepositBin, ItemDefinition> ItemDeposited;

        private bool HasTrashView =>
            spriteRenderer != null && closedSprite != null && openSprite != null;

        private void Awake()
        {
            CacheVisualBase();
            if (HasTrashView)
            {
                SetOpenState(false);
            }
        }

        private void OnDisable()
        {
            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                shakeRoutine = null;
            }

            pointerHover = false;
            if (HasTrashView && hasVisualBase && visual != null)
            {
                visual.localPosition = visualBaseLocalPosition;
                visual.localRotation = visualBaseLocalRotation;
                SetOpenState(false);
            }
        }

        private void Update()
        {
            if (!HasTrashView || shakeRoutine != null)
            {
                return;
            }

            bool hover = IsPointerOverBin();
            if (hover == pointerHover)
            {
                return;
            }

            pointerHover = hover;
            SetOpenState(pointerHover);
        }

        public bool TryDepositOne(KomayamaHandInventory hand, out string failureReason)
        {
            return TryDepositOne(hand, out failureReason, playSe: true);
        }

        /// <param name="playSe">
        /// false のとき SE を鳴らさない（長押し連続納入で2回目以降など）。
        /// </param>
        public bool TryDepositOne(
            KomayamaHandInventory hand,
            out string failureReason,
            bool playSe)
        {
            failureReason = null;
            if (hand == null || hand.IsEmpty)
            {
                failureReason = "納入できるアイテムがありません";
                return false;
            }

            if (!KomayamaRegion.CanInteract(transform.position, out string regionReason))
            {
                failureReason = regionReason;
                hud?.ShowMessage(regionReason);
                if (playSe)
                {
                    seManager?.Play(KomayamaCraftSeCue.Invalid);
                }

                return false;
            }

            ItemDefinition item = hand.Item;
            if (item == null || !hand.TryRemoveOne(item, out _))
            {
                failureReason = "納入できるアイテムがありません";
                return false;
            }

            if (playSe)
            {
                // 納入ゴミ箱は専用音。通常の Drop／施設 Deposit は使わない。
                seManager?.Play(
                    HasTrashView
                        ? KomayamaCraftSeCue.TrashDeposit
                        : KomayamaCraftSeCue.Deposit);
            }

            if (HasTrashView)
            {
                PlayTrashShake();
            }

            ItemDeposited?.Invoke(this, item);
            return true;
        }

        /// <summary>ホバー開閉・震え・ゴミ箱専用 SE を使う納入ゴミ箱か。</summary>
        public bool UsesTrashDepositSe => HasTrashView;

        private void PlayTrashShake()
        {
            CacheVisualBase();
            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
            }

            shakeRoutine = StartCoroutine(CoPlayShake());
        }

        private IEnumerator CoPlayShake()
        {
            SetOpenState(true);
            if (!hasVisualBase || visual == null)
            {
                yield return null;
                SetOpenState(pointerHover);
                shakeRoutine = null;
                yield break;
            }

            float jumpWorld = ResolveJumpWorld();
            visual.localPosition = visualBaseLocalPosition + Vector3.up * jumpWorld;
            float duration = Mathf.Max(0.01f, shakeDurationSeconds);
            int cycles = Mathf.Max(1, shakeCount);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float wave = Mathf.Sin(t * cycles * Mathf.PI * 2f);
                visual.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    wave * Mathf.Max(0f, shakeAngleDegrees));
                yield return null;
            }

            visual.localRotation = visualBaseLocalRotation;
            visual.localPosition = visualBaseLocalPosition;
            SetOpenState(pointerHover);
            shakeRoutine = null;
        }

        private float ResolveJumpWorld()
        {
            float ppu = 100f;
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                ppu = Mathf.Max(1f, spriteRenderer.sprite.pixelsPerUnit);
            }

            return Mathf.Max(0f, jumpUpPixels) / ppu;
        }

        private void SetOpenState(bool open)
        {
            if (!HasTrashView)
            {
                return;
            }

            spriteRenderer.sprite = open ? openSprite : closedSprite;
        }

        private void CacheVisualBase()
        {
            if (spriteRenderer == null)
            {
                hasVisualBase = false;
                visual = null;
                return;
            }

            visual = spriteRenderer.transform;
            if (!hasVisualBase)
            {
                visualBaseLocalPosition = visual.localPosition;
                visualBaseLocalRotation = visual.localRotation;
                hasVisualBase = true;
            }
        }

        private bool IsPointerOverBin()
        {
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }

            if (!TryGetPointerWorld(out Vector2 world))
            {
                return false;
            }

            Collider2D col = hitCollider != null
                ? hitCollider
                : GetComponent<Collider2D>();
            return col != null && col.OverlapPoint(world);
        }

        private bool TryGetPointerWorld(out Vector2 world)
        {
            world = default;
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null)
            {
                return false;
            }

            Vector2 screen;
            if (Mouse.current != null)
            {
                screen = Mouse.current.position.ReadValue();
            }
            else if (Touchscreen.current != null &&
                     Touchscreen.current.primaryTouch.press.isPressed)
            {
                screen = Touchscreen.current.primaryTouch.position.ReadValue();
            }
            else
            {
                return false;
            }

            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            world = w;
            return true;
        }
    }
}
