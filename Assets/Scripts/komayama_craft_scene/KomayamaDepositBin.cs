using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 右クリックで手持ち1個を納入して消すゴミ箱。
    /// 配置は click 生産施設と同じく、Layer_Objects 直下・NativeLife・見た目枠＝反応範囲。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class KomayamaDepositBin : MonoBehaviour
    {
        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private KomayamaCraftSeManager seManager;

        public bool TryDepositOne(KomayamaHandInventory hand, out string failureReason)
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
                seManager?.Play(KomayamaCraftSeCue.Invalid);
                return false;
            }

            ItemDefinition item = hand.Item;
            if (item == null || !hand.TryRemoveOne(item, out _))
            {
                failureReason = "納入できるアイテムがありません";
                return false;
            }

            seManager?.Play(KomayamaCraftSeCue.Deposit);
            return true;
        }
    }
}
