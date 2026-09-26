using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 見た目調整で無効化した制限グリッドを、Play 開始時に強制アクティブにする。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class KomayamaRestrictionGridBoot : MonoBehaviour
    {
        [SerializeField] private GameObject dropRestrictionGrid;
        [SerializeField] private GameObject buildRestrictionGrid;

        private void Awake()
        {
            ActivateHierarchy(dropRestrictionGrid);
            ActivateHierarchy(buildRestrictionGrid);
        }

        private static void ActivateHierarchy(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            if (!root.activeSelf)
            {
                root.SetActive(true);
            }

            Transform t = root.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                GameObject child = t.GetChild(i).gameObject;
                if (!child.activeSelf)
                {
                    child.SetActive(true);
                }
            }
        }
    }
}
