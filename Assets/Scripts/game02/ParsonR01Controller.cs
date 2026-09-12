using UnityEngine;

namespace Game02
{
    /// <summary>
    /// ParsonObject / ParsonR01 専用。サイドパネルを閉じるたびに確率で進行カウンタを増やし、
    /// 閾値以上で代替画像へ切り替える。代替表示中にパネルを閉じると通常画像へ戻す（仕様: spec/game02/parson_r01_side_panel_visual_spec.md）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParsonR01Controller : MonoBehaviour
    {
        [Tooltip("通常表示（ParsonR01Image）。未設定時は直下の名前検索。")]
        [SerializeField] private GameObject parsonR01PrimaryVisualGo;

        [Tooltip("切り替え後（ParsonR01Image_2）。未設定時は直下の名前検索。")]
        [SerializeField] private GameObject parsonR01AlternateVisualGo;

        [SerializeField] private int changeThreshold = 6;

        [SerializeField, Range(0f, 100f)]
        private float incrementOnCloseProbabilityPercent = 80f;

        private int progressCounter;

        private void Awake()
        {
            ResolveRefsIfMissing();
        }

        private void OnEnable()
        {
            progressCounter = 0;
            SidePanelOpenCloseController.SidePanelClosed += OnSidePanelClosed;
            ResolveRefsIfMissing();
            ApplyPrimaryVisual();
        }

        private void OnDisable()
        {
            SidePanelOpenCloseController.SidePanelClosed -= OnSidePanelClosed;
        }

        private void ResolveRefsIfMissing()
        {
            if (parsonR01PrimaryVisualGo == null)
            {
                Transform t = transform.Find("ParsonR01Image");
                if (t != null)
                {
                    parsonR01PrimaryVisualGo = t.gameObject;
                }
            }

            if (parsonR01AlternateVisualGo == null)
            {
                Transform t = transform.Find("ParsonR01Image_2");
                if (t != null)
                {
                    parsonR01AlternateVisualGo = t.gameObject;
                }
            }
        }

        private bool IsAlternateVisualShowing()
        {
            return parsonR01AlternateVisualGo != null && parsonR01AlternateVisualGo.activeSelf;
        }

        private void OnSidePanelClosed()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                return;
            }

            ResolveRefsIfMissing();

            if (parsonR01PrimaryVisualGo == null || parsonR01AlternateVisualGo == null)
            {
                return;
            }

            if (IsAlternateVisualShowing())
            {
                progressCounter = 0;
                ApplyPrimaryVisual();
                return;
            }

            float p = Mathf.Clamp(incrementOnCloseProbabilityPercent * 0.01f, 0f, 1f);
            if (Random.value >= p)
            {
                return;
            }

            progressCounter++;
            int thr = Mathf.Max(0, changeThreshold);
            if (progressCounter >= thr)
            {
                ApplyAlternateVisual();
            }
        }

        private void ApplyPrimaryVisual()
        {
            if (parsonR01PrimaryVisualGo != null)
            {
                parsonR01PrimaryVisualGo.SetActive(true);
            }

            if (parsonR01AlternateVisualGo != null)
            {
                parsonR01AlternateVisualGo.SetActive(false);
            }
        }

        private void ApplyAlternateVisual()
        {
            if (parsonR01PrimaryVisualGo != null)
            {
                parsonR01PrimaryVisualGo.SetActive(false);
            }

            if (parsonR01AlternateVisualGo != null)
            {
                parsonR01AlternateVisualGo.SetActive(true);
            }
        }
    }
}
