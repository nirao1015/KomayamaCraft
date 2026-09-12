using UnityEngine;

namespace Game02
{
    /// <summary>
    /// SidePanelView 配下。配信関連アップグレード回数の参照用ミラー（画面表示しない想定）。
    /// </summary>
    public sealed class SidePanelStreamingStats : MonoBehaviour
    {
        [SerializeField] private int streamingRelatedUpgradeMirror;

        public int StreamingRelatedUpgradeMirror => streamingRelatedUpgradeMirror;

        public void SyncFromUpgradesManager()
        {
            if (UpgradesManager.Instance == null)
            {
                return;
            }

            streamingRelatedUpgradeMirror = UpgradesManager.Instance.GetStreamingRelatedUpgradeTotalCount();
        }
    }
}
