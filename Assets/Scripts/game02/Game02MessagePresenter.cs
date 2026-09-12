using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game02
{
    [DisallowMultipleComponent]
    public sealed class Game02MessagePresenter : MonoBehaviour
    {
        private static Game02MessagePresenter activePresenter;

        [Header("Refs")]
        [Tooltip("メッセージ本文を表示する TMP。未設定時は MsgText (TMP) を自動探索します。")]
        [SerializeField] private TMP_Text msgText;

        [Header("Animation")]
        [Tooltip("画面外上部の開始位置に移動する余白ピクセル。")]
        [SerializeField] private float startOffsetTopPixels = 24f;
        [Tooltip("画面外上部から元位置へ移動する時間（秒）。")]
        [SerializeField] private float moveInSeconds = 0.28f;
        [Tooltip("表示後に待機する秒数（ShowMessage の引数未指定時）。")]
        [SerializeField] private float defaultHoldSeconds = 1.8f;
        [Tooltip("フェードアウト時間（秒）。")]
        [SerializeField] private float fadeOutSeconds = 0.35f;

        private RectTransform selfRect;
        private RectTransform parentRect;
        private CanvasGroup canvasGroup;
        private Vector2 originalAnchoredPos;
        private bool hasOriginalAnchoredPos;
        private Coroutine queueRoutine;
        private readonly Queue<QueuedMessage> pendingMessages = new Queue<QueuedMessage>();

        private readonly struct QueuedMessage
        {
            public readonly string Message;
            public readonly float HoldSeconds;
            public readonly bool PlayGenericMessageSound;

            public QueuedMessage(string message, float holdSeconds, bool playGenericMessageSound)
            {
                Message = message;
                HoldSeconds = holdSeconds;
                PlayGenericMessageSound = playGenericMessageSound;
            }
        }

        public static Game02MessagePresenter EnsureSceneController()
        {
            GameObject msgOb = GameObject.Find("PanelCanvas/MsgOb");
            if (msgOb == null)
            {
                msgOb = FindMsgObUnderPanelCanvas();
            }

            if (msgOb == null)
            {
                return null;
            }

            Game02MessagePresenter presenter = msgOb.GetComponent<Game02MessagePresenter>();
            if (presenter == null)
            {
                presenter = msgOb.AddComponent<Game02MessagePresenter>();
            }

            return presenter;
        }

        private void Awake()
        {
            if (activePresenter != null && activePresenter != this)
            {
                Destroy(this);
                return;
            }

            activePresenter = this;
        }

        private void OnDestroy()
        {
            if (activePresenter == this)
            {
                activePresenter = null;
            }
        }

        /// <param name="playGenericMessageSound">
        /// true のとき <see cref="Game02SeCue.GenericMessage"/> を鳴らす。
        /// <see cref="Game02MsgManager"/> 経由の表示では SE をここでは鳴らさず false にする。
        /// </param>
        public void ShowMessage(string message, float holdSeconds = -1f, bool playGenericMessageSound = false)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            CacheRefsIfNeeded();
            if (selfRect == null || msgText == null)
            {
                return;
            }

            pendingMessages.Enqueue(new QueuedMessage(message, holdSeconds, playGenericMessageSound));
            if (queueRoutine == null)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }
                queueRoutine = StartCoroutine(CoProcessMessageQueue());
            }
        }

        private IEnumerator CoProcessMessageQueue()
        {
            while (pendingMessages.Count > 0)
            {
                QueuedMessage next = pendingMessages.Dequeue();
                yield return CoShowSingleMessage(next.Message, next.HoldSeconds, next.PlayGenericMessageSound);
            }

            queueRoutine = null;
        }

        private IEnumerator CoShowSingleMessage(string message, float holdSeconds, bool playGenericMessageSound)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            CacheRefsIfNeeded();
            msgText.text = message;
            if (playGenericMessageSound)
            {
                Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.GenericMessage);
            }

            if (!hasOriginalAnchoredPos)
            {
                originalAnchoredPos = selfRect.anchoredPosition;
                hasOriginalAnchoredPos = true;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            selfRect.anchoredPosition = ResolveOffscreenTopPosition();

            float moveDuration = Mathf.Max(0.01f, moveInSeconds);
            float elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
                float t = Mathf.Clamp01(elapsed / moveDuration);
                selfRect.anchoredPosition = Vector2.LerpUnclamped(ResolveOffscreenTopPosition(), originalAnchoredPos, t);
                yield return null;
            }
            selfRect.anchoredPosition = originalAnchoredPos;

            float waitSeconds = holdSeconds >= 0f ? holdSeconds : defaultHoldSeconds;
            if (waitSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(waitSeconds);
            }

            float fadeDuration = Mathf.Max(0.01f, fadeOutSeconds);
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f - t;
                }
                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private Vector2 ResolveOffscreenTopPosition()
        {
            float topY = originalAnchoredPos.y;
            if (parentRect != null)
            {
                float parentHalf = Mathf.Abs(parentRect.rect.height) * 0.5f;
                float selfHalf = Mathf.Abs(selfRect.rect.height) * 0.5f;
                topY = parentHalf + selfHalf + Mathf.Max(0f, startOffsetTopPixels);
            }
            else
            {
                topY = originalAnchoredPos.y + Mathf.Abs(selfRect.rect.height) + Mathf.Max(0f, startOffsetTopPixels);
            }

            return new Vector2(originalAnchoredPos.x, topY);
        }

        private void CacheRefsIfNeeded()
        {
            if (selfRect == null)
            {
                selfRect = transform as RectTransform;
            }

            if (parentRect == null && selfRect != null)
            {
                parentRect = selfRect.parent as RectTransform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (msgText == null)
            {
                Transform t = transform.Find("MsgText (TMP)");
                if (t != null)
                {
                    msgText = t.GetComponent<TMP_Text>();
                }

                if (msgText == null)
                {
                    TMP_Text[] all = GetComponentsInChildren<TMP_Text>(true);
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i] != null && all[i].name == "MsgText (TMP)")
                        {
                            msgText = all[i];
                            break;
                        }
                    }
                }
            }

            if (!hasOriginalAnchoredPos && selfRect != null)
            {
                originalAnchoredPos = selfRect.anchoredPosition;
                hasOriginalAnchoredPos = true;
            }
        }

        private static GameObject FindMsgObUnderPanelCanvas()
        {
            Transform[] all = Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != "MsgOb")
                {
                    continue;
                }

                Transform p = t.parent;
                if (p != null && p.name == "PanelCanvas")
                {
                    return t.gameObject;
                }
            }

            return null;
        }
    }
}
