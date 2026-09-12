using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game02
{
    /// <summary>
    /// <c>DebugPanel/ButtonDebugMsg</c> と同一 GameObject に付与。クリックで <c>ViewMsg</c> の文字列を <see cref="Game02MessagePresenter"/>（MsgOb）へ表示する。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class Game02DebugMsgButton : MonoBehaviour
    {
        [SerializeField] private Button button;

        [SerializeField, Tooltip("表示する文言の元。未設定時は子オブジェクト ViewMsg の TMP を使用する。")]
        private TMP_Text viewMsg;

        [SerializeField, Tooltip("ShowMessage の保持秒。-1 で Presenter の既定値。")]
        private float holdSeconds = -1f;

        private void Reset()
        {
            button = GetComponent<Button>();
            TryBindViewMsgFromChildren();
        }

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            TryBindViewMsgFromChildren();

            if (button != null)
            {
                button.onClick.RemoveListener(OnClickShowMessageFromViewMsg);
                button.onClick.AddListener(OnClickShowMessageFromViewMsg);
            }
        }

        private void OnValidate()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (viewMsg == null)
            {
                TryBindViewMsgFromChildren();
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClickShowMessageFromViewMsg);
            }
        }

        /// <summary>Inspector の On Click からも呼べるように public。</summary>
        public void OnClickShowMessageFromViewMsg()
        {
            TryBindViewMsgFromChildren();
            string raw = viewMsg != null ? viewMsg.text : string.Empty;
            string message = raw != null ? raw.Trim() : string.Empty;
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            Game02MsgManager msgManager = Game02MsgManager.TryGet();
            if (msgManager != null)
            {
                msgManager.PresentDebugAdHoc(message, holdSeconds);
                return;
            }

            Game02MessagePresenter presenter = Game02MessagePresenter.EnsureSceneController();
            if (presenter == null)
            {
                return;
            }

            presenter.ShowMessage(message, holdSeconds, playGenericMessageSound: true);
        }

        private void TryBindViewMsgFromChildren()
        {
            if (viewMsg != null)
            {
                return;
            }

            Transform child = transform.Find("ViewMsg");
            if (child != null)
            {
                viewMsg = child.GetComponent<TMP_Text>();
                return;
            }

            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == "ViewMsg")
                {
                    viewMsg = texts[i];
                    return;
                }
            }
        }
    }
}
