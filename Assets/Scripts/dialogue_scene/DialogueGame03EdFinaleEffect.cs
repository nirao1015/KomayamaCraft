using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DialogueScene
{
    public enum EdFinaleTitleRevealStyle
    {
        Spiral,
        Mosaic,
        Fade,
    }

    /// <summary>
    /// game03 ED 会話の専用フィナーレ（黒幕＋背景下退＋ dialog_title フェードイン）。CSV <c>fx_game03_ed_finale</c> 用。引数なし固定演出。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueGame03EdFinaleEffect : MonoBehaviour
    {
        private const string TitleRevealShaderName = "UI/Dialogue/EdFinaleTitleReveal";

        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int SpiralTurnsId = Shader.PropertyToID("_SpiralTurns");
        private static readonly int MosaicBlocksId = Shader.PropertyToID("_MosaicBlocks");
        private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");

        [Header("タイミング（固定演出）")]
        [SerializeField] private float backgroundExitDurationSeconds = 2.8f;
        [SerializeField] private float backgroundSlidePixels = 1100f;
        [SerializeField] private float titleFadeInDurationSeconds = 1.2f;
        [SerializeField] private float inputUnlockDelaySeconds = 1f;

        [Header("タイトル表示")]
        [SerializeField] private Vector2 titleAnchoredPosition = Vector2.zero;
        [SerializeField] private Vector2 titleSize = new Vector2(1400f, 400f);

        [Header("タイトル出現演出")]
        [SerializeField] private EdFinaleTitleRevealStyle titleRevealStyle = EdFinaleTitleRevealStyle.Spiral;
        [SerializeField] private float spiralTurns = 2.5f;
        [SerializeField] private int mosaicBlockCount = 18;
        [SerializeField] private float revealEdgeSoftness = 0.07f;
        [SerializeField] private float titlePopStartScale = 0.9f;

        private Image _blackOverlay;
        private Image _titleImage;
        private Material _titleRevealMaterial;
        private bool _loggedMissingRevealShader;

        public IEnumerator PlayFinale(Image backgroundImage, Sprite titleSprite, GameObject[] hideDuringFinale)
        {
            if (backgroundImage == null)
            {
                Debug.LogWarning("[DialogueGame03EdFinale] backgroundImage is null.");
                yield break;
            }

            if (titleSprite == null)
            {
                Debug.LogWarning("[DialogueGame03EdFinale] dialog_title sprite is null.");
                yield break;
            }

            RectTransform canvasRt = backgroundImage.rectTransform.parent as RectTransform;
            if (canvasRt == null)
            {
                Debug.LogWarning("[DialogueGame03EdFinale] Background has no canvas parent.");
                yield break;
            }

            bool[] wasActiveBeforeHide = CaptureActiveStates(hideDuringFinale);
            SetActiveStates(hideDuringFinale, false);
            EnsureBlackOverlay(canvasRt, backgroundImage.rectTransform);
            EnsureTitleImage(canvasRt, titleSprite);

            RectTransform bgRt = backgroundImage.rectTransform;
            bgRt.DOKill();
            Color bgColor = backgroundImage.color;
            Vector2 startMin = bgRt.offsetMin;
            Vector2 startMax = bgRt.offsetMax;
            float slide = Mathf.Max(1f, backgroundSlidePixels);
            float exitDuration = Mathf.Max(0.05f, backgroundExitDurationSeconds);

            Tween slideTween = DOTween.To(
                    () => 0f,
                    v =>
                    {
                        bgRt.offsetMin = new Vector2(startMin.x, startMin.y - v);
                        bgRt.offsetMax = new Vector2(startMax.x, startMax.y - v);
                    },
                    slide,
                    exitDuration)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);

            Tween fadeTween = backgroundImage
                .DOFade(0f, exitDuration)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);

            yield return DOTween.Sequence()
                .Join(slideTween)
                .Join(fadeTween)
                .WaitForCompletion();

            backgroundImage.gameObject.SetActive(false);
            backgroundImage.color = bgColor;

            float titleFade = Mathf.Max(0.05f, titleFadeInDurationSeconds);
            yield return AnimateTitleReveal(titleFade);

            float wait = Mathf.Max(0f, inputUnlockDelaySeconds);
            if (wait > 0f)
            {
                float elapsed = 0f;
                while (elapsed < wait)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            RestoreActiveStates(hideDuringFinale, wasActiveBeforeHide);
        }

        private void OnDestroy()
        {
            ReleaseTitleRevealMaterial();
        }

        private IEnumerator AnimateTitleReveal(float duration)
        {
            _titleImage.gameObject.SetActive(true);
            _titleImage.color = Color.white;

            RectTransform titleRt = _titleImage.rectTransform;
            titleRt.DOKill();

            if (titleRevealStyle == EdFinaleTitleRevealStyle.Fade || !TryApplyTitleRevealMaterial())
            {
                Color titleColor = _titleImage.color;
                titleColor.a = 0f;
                _titleImage.color = titleColor;
                yield return _titleImage
                    .DOFade(1f, duration)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .WaitForCompletion();
                yield break;
            }

            SetReveal(0f);
            float startScale = Mathf.Clamp(titlePopStartScale, 0.5f, 1f);
            titleRt.localScale = Vector3.one * startScale;

            Tween revealTween = DOTween
                .To(() => 0f, SetReveal, 1f, duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
            Tween scaleTween = titleRt
                .DOScale(1f, duration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);

            yield return DOTween.Sequence()
                .Join(revealTween)
                .Join(scaleTween)
                .WaitForCompletion();

            SetReveal(1f);
        }

        private bool TryApplyTitleRevealMaterial()
        {
            Shader shader = Shader.Find(TitleRevealShaderName);
            if (shader == null)
            {
                if (!_loggedMissingRevealShader)
                {
                    Debug.LogWarning($"[DialogueGame03EdFinale] Shader not found: {TitleRevealShaderName}. Using fade.");
                    _loggedMissingRevealShader = true;
                }

                return false;
            }

            if (_titleRevealMaterial == null)
            {
                _titleRevealMaterial = new Material(shader);
                _titleRevealMaterial.name = "EdFinaleTitleReveal_Runtime";
            }

            _titleRevealMaterial.SetFloat(ModeId, titleRevealStyle == EdFinaleTitleRevealStyle.Mosaic ? 1f : 0f);
            _titleRevealMaterial.SetFloat(SpiralTurnsId, Mathf.Max(0.5f, spiralTurns));
            _titleRevealMaterial.SetFloat(MosaicBlocksId, Mathf.Max(4f, mosaicBlockCount));
            _titleRevealMaterial.SetFloat(EdgeSoftnessId, Mathf.Clamp(revealEdgeSoftness, 0.01f, 0.25f));
            _titleImage.material = _titleRevealMaterial;
            return true;
        }

        private void SetReveal(float value)
        {
            if (_titleRevealMaterial != null)
            {
                _titleRevealMaterial.SetFloat(RevealId, Mathf.Clamp01(value));
            }
        }

        private void ReleaseTitleRevealMaterial()
        {
            if (_titleRevealMaterial == null)
            {
                return;
            }

            if (_titleImage != null && _titleImage.material == _titleRevealMaterial)
            {
                _titleImage.material = null;
            }

            if (Application.isPlaying)
            {
                Destroy(_titleRevealMaterial);
            }
            else
            {
                DestroyImmediate(_titleRevealMaterial);
            }

            _titleRevealMaterial = null;
        }

        private void EnsureBlackOverlay(RectTransform canvasRt, RectTransform backgroundRt)
        {
            if (_blackOverlay != null)
            {
                _blackOverlay.gameObject.SetActive(true);
                _blackOverlay.rectTransform.SetSiblingIndex(backgroundRt.GetSiblingIndex());
                return;
            }

            _blackOverlay = CreateFullScreenImage(canvasRt, "EdFinaleBlackOverlay", Color.black);
            _blackOverlay.rectTransform.SetSiblingIndex(backgroundRt.GetSiblingIndex());
        }

        private void EnsureTitleImage(RectTransform canvasRt, Sprite titleSprite)
        {
            if (_titleImage == null)
            {
                _titleImage = CreateFullScreenImage(canvasRt, "EdFinaleTitle", Color.white);
                RectTransform rt = _titleImage.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = titleSize;
                rt.anchoredPosition = titleAnchoredPosition;
                _titleImage.preserveAspect = true;
                _titleImage.gameObject.SetActive(false);
            }

            _titleImage.sprite = titleSprite;
        }

        private static Image CreateFullScreenImage(RectTransform parent, string name, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static bool[] CaptureActiveStates(GameObject[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                return Array.Empty<bool>();
            }

            var states = new bool[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                GameObject go = targets[i];
                states[i] = go != null && go.activeSelf;
            }

            return states;
        }

        private static void SetActiveStates(GameObject[] targets, bool active)
        {
            if (targets == null)
            {
                return;
            }

            foreach (GameObject go in targets)
            {
                if (go != null)
                {
                    go.SetActive(active);
                }
            }
        }

        private static void RestoreActiveStates(GameObject[] targets, bool[] wasActiveBeforeHide)
        {
            if (targets == null || wasActiveBeforeHide == null)
            {
                return;
            }

            int count = Mathf.Min(targets.Length, wasActiveBeforeHide.Length);
            for (int i = 0; i < count; i++)
            {
                GameObject go = targets[i];
                if (go != null && wasActiveBeforeHide[i])
                {
                    go.SetActive(true);
                }
            }
        }
    }
}
