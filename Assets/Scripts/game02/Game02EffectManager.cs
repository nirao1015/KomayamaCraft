using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Game02
{
    [DisallowMultipleComponent]
    public sealed class Game02EffectManager : HoverOverlayEffectManagerBase
    {
        private static Game02EffectManager instance;

        [Header("販売色")]
        [SerializeField, Tooltip("購入可能時：アップグレード価格 TMP のグラデ（右上・左下・右下）。")]
        private Color salePriceAffordableEdge = new Color(0.22745098f, 0.35686275f, 1f, 1f);
        [SerializeField, Tooltip("購入不可能時：アップグレード価格 TMP のグラデ（右上・左下・右下）。")]
        private Color salePriceUnaffordableEdge = Color.red;
        [SerializeField, Tooltip("購入可否グラデの左上ハイライト（差し込み色）。")]
        private Color salePriceGradientTopLeft = new Color(0.81960785f, 0.81960785f, 0.81960785f, 1f);
        [SerializeField, Tooltip("売り切れ時：価格グラデの左上。")]
        private Color salePriceSoldOutTopLeft = new Color(0.38f, 0.38f, 0.40f, 1f);
        [SerializeField, Tooltip("売り切れ時：価格グラデの他3頂点（黒寄りグレー）。")]
        private Color salePriceSoldOutEdge = new Color(0.28f, 0.28f, 0.30f, 1f);

        [Serializable]
        private sealed class AddUiBinding
        {
            [Tooltip("演出対象の AddUI。")]
            public TMP_Text targetUi;
            [Tooltip("加算表示の総表示時間（秒）。")]
            public float totalDisplaySeconds = 0.8f;
            [Tooltip("上方向の移動量（px）。0 なら移動しません。")]
            public float moveUpPixels = 20f;
            [Tooltip("右方向の移動量（px）。0 なら移動しません。")]
            public float moveRightPixels = 12f;
            [Tooltip("フェードイン時間（秒）。")]
            public float fadeInSeconds = 0.15f;
            [Tooltip("フェードアウト時間（秒）。")]
            public float fadeOutSeconds = 0.3f;
            [Tooltip("true のとき静止時間を秒指定で使用します。false のとき総表示時間から自動計算します。")]
            public bool useExplicitHoldSeconds;
            [Tooltip("静止時間（秒）。useExplicitHoldSeconds が true のときに使用。")]
            public float holdSeconds = 0.2f;

            [NonSerialized] public Coroutine activeCoroutine;
            [NonSerialized] public Color baseColor = Color.white;
            [NonSerialized] public bool hasBaseColor;
            [NonSerialized] public Vector2 startAnchoredPosition;
            [NonSerialized] public bool hasStartAnchoredPosition;
        }

        [Header("Add UI Effects")]
        [SerializeField] private AddUiBinding[] addUiBindings = Array.Empty<AddUiBinding>();

        [Header("ゲーム開始演出設定")]
        [SerializeField, Tooltip("StageStartOverlay 有効化からスタンプ表示までの待機（秒、unscaled）。")]
        private float gameStartStampDelaySeconds = 0.4f;
        [SerializeField, Tooltip("スタンプ表示からキャラ表示までの待機（秒、unscaled）。")]
        private float gameStartCharaAppearDelaySeconds = 0.5f;
        [SerializeField, Tooltip("キャラ表示から移動開始までの待機（秒、unscaled）。")]
        private float gameStartCharaMoveDelaySeconds = 0.4f;
        [SerializeField, Tooltip("ItemMailCharaImage が初期位置から終端位置まで移動するのにかける時間（秒、unscaled）。")]
        private float gameStartCharaMoveDurationSeconds = 1.2f;
        [SerializeField, Tooltip("移動中の回転速度（度/秒）。終端到達後はこの速度から減速して 0 度に合わせる。")]
        private float gameStartCharaSpinDegreesPerSecond = 540f;
        [SerializeField, Tooltip("終端到達後、回転が 0 度に揃うまでの減速時間（秒、unscaled）。")]
        private float gameStartCharaSpinSettleDurationSeconds = 0.45f;

        /// <summary>アップグレード等の価格表示色（購入可能時・グラデ右下側など）。</summary>
        public Color SalePriceAffordableEdge => salePriceAffordableEdge;

        /// <summary>アップグレード等の価格表示色（購入不可能時）。</summary>
        public Color SalePriceUnaffordableEdge => salePriceUnaffordableEdge;

        /// <summary>購入可否グラデの左上差し込み色。</summary>
        public Color SalePriceGradientTopLeft => salePriceGradientTopLeft;

        /// <summary>売り切れ時グラデの左上。</summary>
        public Color SalePriceSoldOutTopLeft => salePriceSoldOutTopLeft;

        /// <summary>売り切れ時グラデの他3頂点。</summary>
        public Color SalePriceSoldOutEdge => salePriceSoldOutEdge;

        /// <summary>開始演出：StageStartOverlay 有効化〜スタンプ表示までの待機（秒）。</summary>
        public float GameStartStampDelaySeconds => Mathf.Max(0f, gameStartStampDelaySeconds);

        /// <summary>開始演出：スタンプ表示〜キャラ表示までの待機（秒）。</summary>
        public float GameStartCharaAppearDelaySeconds => Mathf.Max(0f, gameStartCharaAppearDelaySeconds);

        /// <summary>開始演出：キャラ表示〜移動開始までの待機（秒）。</summary>
        public float GameStartCharaMoveDelaySeconds => Mathf.Max(0f, gameStartCharaMoveDelaySeconds);

        /// <summary>開始演出：ItemMailCharaImage を終端まで動かす時間（秒）。</summary>
        public float GameStartCharaMoveDurationSeconds => Mathf.Max(0.01f, gameStartCharaMoveDurationSeconds);

        /// <summary>開始演出：移動中の回転速度（度/秒）。</summary>
        public float GameStartCharaSpinDegreesPerSecond => gameStartCharaSpinDegreesPerSecond;

        /// <summary>開始演出：終端到達後に回転を 0 度へ収束させる減速時間（秒）。</summary>
        public float GameStartCharaSpinSettleDurationSeconds => Mathf.Max(0f, gameStartCharaSpinSettleDurationSeconds);

        public static Game02EffectManager TryGet()
        {
            if (instance != null)
            {
                return instance;
            }

            return FindObjectOfType<Game02EffectManager>(true);
        }

        protected override void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            base.Awake();
            ClearAllAddUi();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ClearAllAddUi();
        }

        protected override void OnDestroy()
        {
            StopAllAddUiCoroutines();
            if (instance == this)
            {
                instance = null;
            }

            base.OnDestroy();
        }

        public bool PlayAddUi(TMP_Text targetUi, long addValue)
        {
            if (targetUi == null || addValue <= 0L)
            {
                return false;
            }

            AddUiBinding binding = FindBinding(targetUi);
            if (binding == null)
            {
                return false;
            }

            if (binding.activeCoroutine != null)
            {
                StopCoroutine(binding.activeCoroutine);
                binding.activeCoroutine = null;
            }

            if (!targetUi.gameObject.activeSelf)
            {
                targetUi.gameObject.SetActive(true);
            }

            targetUi.text = $"+{addValue.ToString("N0", CultureInfo.InvariantCulture)}";
            SetAddUiAlpha(binding, 0f);
            SetAddUiAnchoredPosition(binding, 0f);
            binding.activeCoroutine = StartCoroutine(PlayAddUiAnimationCoroutine(binding));
            return true;
        }

        public bool ClearAddUi(TMP_Text targetUi)
        {
            AddUiBinding binding = FindBinding(targetUi);
            if (binding == null || binding.targetUi == null)
            {
                return false;
            }

            if (binding.activeCoroutine != null)
            {
                StopCoroutine(binding.activeCoroutine);
                binding.activeCoroutine = null;
            }

            binding.targetUi.text = string.Empty;
            SetAddUiAlpha(binding, 0f);
            SetAddUiAnchoredPosition(binding, 0f);
            if (binding.targetUi.gameObject.activeSelf)
            {
                binding.targetUi.gameObject.SetActive(false);
            }

            return true;
        }

        private AddUiBinding FindBinding(TMP_Text targetUi)
        {
            if (targetUi == null || addUiBindings == null)
            {
                return null;
            }

            for (int i = 0; i < addUiBindings.Length; i++)
            {
                AddUiBinding binding = addUiBindings[i];
                if (binding != null && binding.targetUi == targetUi)
                {
                    return binding;
                }
            }

            return null;
        }

        private void ClearAllAddUi()
        {
            if (addUiBindings == null)
            {
                return;
            }

            for (int i = 0; i < addUiBindings.Length; i++)
            {
                AddUiBinding binding = addUiBindings[i];
                if (binding == null || binding.targetUi == null)
                {
                    continue;
                }

                if (binding.activeCoroutine != null)
                {
                    StopCoroutine(binding.activeCoroutine);
                    binding.activeCoroutine = null;
                }

                binding.targetUi.text = string.Empty;
                SetAddUiAlpha(binding, 0f);
                SetAddUiAnchoredPosition(binding, 0f);
                if (binding.targetUi.gameObject.activeSelf)
                {
                    binding.targetUi.gameObject.SetActive(false);
                }
            }
        }

        private void StopAllAddUiCoroutines()
        {
            if (addUiBindings == null)
            {
                return;
            }

            for (int i = 0; i < addUiBindings.Length; i++)
            {
                AddUiBinding binding = addUiBindings[i];
                if (binding == null || binding.activeCoroutine == null)
                {
                    continue;
                }

                StopCoroutine(binding.activeCoroutine);
                binding.activeCoroutine = null;
            }
        }

        private IEnumerator PlayAddUiAnimationCoroutine(AddUiBinding binding)
        {
            TMP_Text targetUi = binding.targetUi;
            if (targetUi == null)
            {
                binding.activeCoroutine = null;
                yield break;
            }

            float fadeIn = Mathf.Max(0f, binding.fadeInSeconds);
            float fadeOut = Mathf.Max(0f, binding.fadeOutSeconds);
            float hold;
            if (binding.useExplicitHoldSeconds)
            {
                hold = Mathf.Max(0f, binding.holdSeconds);
            }
            else
            {
                float total = Mathf.Max(0.01f, binding.totalDisplaySeconds);
                total = Mathf.Max(total, fadeIn + fadeOut);
                hold = Mathf.Max(0f, total - fadeIn - fadeOut);
            }

            float elapsed = 0f;
            while (elapsed < fadeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = fadeIn > 0f ? Mathf.Clamp01(elapsed / fadeIn) : 1f;
                SetAddUiAlpha(binding, t);
                SetAddUiAnchoredPosition(binding, 0f);
                yield return null;
            }

            SetAddUiAlpha(binding, 1f);
            SetAddUiAnchoredPosition(binding, 0f);

            elapsed = 0f;
            while (elapsed < hold)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < fadeOut)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = fadeOut > 0f ? Mathf.Clamp01(elapsed / fadeOut) : 1f;
                SetAddUiAlpha(binding, 1f - t);
                SetAddUiAnchoredPosition(binding, t);
                yield return null;
            }

            targetUi.text = string.Empty;
            SetAddUiAlpha(binding, 0f);
            SetAddUiAnchoredPosition(binding, 0f);
            if (targetUi.gameObject.activeSelf)
            {
                targetUi.gameObject.SetActive(false);
            }

            binding.activeCoroutine = null;
        }

        private static void SetAddUiAlpha(AddUiBinding binding, float alpha)
        {
            TMP_Text targetUi = binding.targetUi;
            if (targetUi == null)
            {
                return;
            }

            if (!binding.hasBaseColor)
            {
                binding.baseColor = targetUi.color;
                binding.hasBaseColor = true;
            }

            Color color = binding.baseColor;
            color.a = Mathf.Clamp01(alpha);
            targetUi.color = color;
        }

        private static void SetAddUiAnchoredPosition(AddUiBinding binding, float normalized)
        {
            TMP_Text targetUi = binding.targetUi;
            if (targetUi == null)
            {
                return;
            }

            RectTransform rect = targetUi.rectTransform;
            if (!binding.hasStartAnchoredPosition)
            {
                binding.startAnchoredPosition = rect.anchoredPosition;
                binding.hasStartAnchoredPosition = true;
            }

            float t = Mathf.Clamp01(normalized);
            float x = binding.startAnchoredPosition.x + (binding.moveRightPixels * t);
            float y = binding.startAnchoredPosition.y + (binding.moveUpPixels * t);
            rect.anchoredPosition = new Vector2(x, y);
        }
    }
}
