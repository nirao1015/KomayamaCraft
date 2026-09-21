using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// メニューアイコン配下の透明ヒット（プレハブ複製用）。
    /// Presenter は未設定なら <see cref="KomayamaMenuHoverGuidePresenter.Instance"/> を使う。
    /// 文言・フォント・サイズだけインスタンスごとに変える。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    [RequireComponent(typeof(Image))]
    public sealed class KomayamaMenuHoverGuideHit : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler
    {
        [Header("表示内容（インスタンスごとに設定）")]
        [SerializeField] private string title = "タイトル";
        [SerializeField, TextArea(2, 4)] private string guide = "ガイド文を入力";
        [SerializeField] private TMP_FontAsset font;
        [SerializeField, Min(1f)] private float titleFontSize = 22f;
        [SerializeField, Min(1f)] private float guideFontSize = 18f;

        [Header("参照（空で可）")]
        [SerializeField] private KomayamaMenuHoverGuidePresenter presenter;
        [SerializeField] private Button forwardClickTo;

        private bool pointerInside;

        public string Title => title;
        public string Guide => guide;
        public TMP_FontAsset Font => font;
        public float TitleFontSize => Mathf.Max(1f, titleFontSize);
        public float GuideFontSize => Mathf.Max(1f, guideFontSize);

        private void Awake()
        {
            EnsureHitImage();
            if (forwardClickTo == null)
            {
                forwardClickTo = GetComponentInParent<Button>();
            }
        }

        private void OnDisable()
        {
            if (pointerInside)
            {
                pointerInside = false;
                ResolvePresenter()?.RequestHide(this);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            ResolvePresenter()?.RequestShow(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            ResolvePresenter()?.RequestHide(this);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Forward(eventData, ExecuteEvents.pointerDownHandler);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Forward(eventData, ExecuteEvents.pointerUpHandler);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Forward(eventData, ExecuteEvents.pointerClickHandler);
        }

        /// <summary>エディタ／セットアップ用。文言だけ差し替える。</summary>
        public void ConfigureContent(string newTitle, string newGuide, TMP_FontAsset newFont = null)
        {
            title = newTitle ?? string.Empty;
            guide = newGuide ?? string.Empty;
            if (newFont != null)
            {
                font = newFont;
            }
        }

        private void EnsureHitImage()
        {
            Image image = GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.raycastTarget = true;
            Color c = image.color;
            c.a = 0f;
            image.color = c;
        }

        private void Forward<T>(PointerEventData eventData, ExecuteEvents.EventFunction<T> functor)
            where T : IEventSystemHandler
        {
            if (forwardClickTo == null)
            {
                forwardClickTo = GetComponentInParent<Button>();
            }

            if (forwardClickTo == null)
            {
                return;
            }

            ExecuteEvents.Execute(forwardClickTo.gameObject, eventData, functor);
        }

        private KomayamaMenuHoverGuidePresenter ResolvePresenter()
        {
            if (presenter != null)
            {
                return presenter;
            }

            return KomayamaMenuHoverGuidePresenter.Instance;
        }
    }
}
