using UnityEngine;

namespace Game02
{
    /// <summary>
    /// game02 のゲームクリアパネルに付与。ボタンは Inspector の OnClick で配線する（実行時バインドしない）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameClearedPanelController : MonoBehaviour
    {
        public static GameClearedPanelController EnsureSceneController()
        {
            GameObject panel = GameObject.Find("PanelCanvas/GameClearedPanel");
            if (panel == null)
            {
                panel = FindInactivePanel();
                if (panel == null)
                {
                    return null;
                }
            }

            GameClearedPanelController controller = panel.GetComponent<GameClearedPanelController>();
            if (controller == null)
            {
                controller = panel.AddComponent<GameClearedPanelController>();
            }

            return controller;
        }

        private static GameObject FindInactivePanel()
        {
            Transform[] all = Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != "GameClearedPanel")
                {
                    continue;
                }

                Transform parent = t.parent;
                if (parent != null && parent.name == "PanelCanvas")
                {
                    return t.gameObject;
                }
            }

            return null;
        }
    }
}
