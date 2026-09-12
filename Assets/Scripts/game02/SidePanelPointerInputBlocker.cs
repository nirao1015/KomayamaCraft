using UnityEngine;

namespace Game02
{
    /// <summary>
    /// サイドパネル<strong>背景</strong>専用。子に付け、<see cref="IWorkplaceTarget"/>（例: WorkStreaming）より
    /// 手前に置かないこと。開いている間、背景の外側へレイが抜けるのを防ぐ。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SidePanelPointerInputBlocker : MonoBehaviour
    {
        public static bool IsBlockingRaycastHit(GameObject hitObject)
        {
            if (hitObject == null)
            {
                return false;
            }

            SidePanelPointerInputBlocker blocker = hitObject.GetComponentInParent<SidePanelPointerInputBlocker>();
            return blocker != null && blocker.isActiveAndEnabled;
        }
    }
}
