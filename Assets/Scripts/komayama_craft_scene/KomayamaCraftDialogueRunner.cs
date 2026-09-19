using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DG.Tweening;
using DialogueScene;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KomayamaCraft
{
    /// <summary>
    /// Craft オーバーレイ用 CSV 会話ランナー（シーン遷移なし）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaCraftDialogueRunner : MonoBehaviour
    {
        [Serializable]
        public sealed class NamedSpriteEntry
        {
            public string key;
            public Sprite sprite;
        }

        [Serializable]
        public sealed class NamedAudioEntry
        {
            public string key;
            public AudioClip clip;
        }

        [Header("UI")]
        [SerializeField] private Image dimmerImage;
        [SerializeField] private Image overlayBackgroundImage;
        [SerializeField] private Image standingLeftImage;
        [SerializeField] private Image standingRightImage;
        [SerializeField] private TextMeshProUGUI speakerText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private CanvasGroup advanceCursorCanvasGroup;
        [SerializeField] private Button skipButton;

        [Header("Audio")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource seSource;
        [SerializeField] private AudioClip advanceTextSeClip;
        [SerializeField] private AudioClip skipSeClip;

        [Header("辞書")]
        [SerializeField] private NamedSpriteEntry[] backgroundSprites = Array.Empty<NamedSpriteEntry>();
        [SerializeField] private NamedSpriteEntry[] standingSprites = Array.Empty<NamedSpriteEntry>();
        [SerializeField] private NamedAudioEntry[] bgmClips = Array.Empty<NamedAudioEntry>();
        [SerializeField] private NamedAudioEntry[] seClips = Array.Empty<NamedAudioEntry>();

        [Header("立ち絵")]
        [SerializeField] private Vector2 standingLeftAnchor = new Vector2(0.2f, 0.36f);
        [SerializeField] private Vector2 standingRightAnchor = new Vector2(0.8f, 0.36f);
        [SerializeField] private Vector2 standingDefaultAnchoredPosition = new Vector2(0f, -100f);
        [SerializeField] private Vector2 standingLeftDefaultSize = new Vector2(700f, 780f);
        [SerializeField] private Vector2 standingRightDefaultSize = new Vector2(420f, 780f);
        [SerializeField] private float leanInDurationSeconds = 0.45f;
        [SerializeField] private float leanInOffsetPixels = 900f;

        [Header("カメラ")]
        [SerializeField] private KomayamaCraftCameraController cameraController;

        [Header("進行")]
        [SerializeField] private float typewriterCharsPerSecond = 32f;
        [SerializeField] private float lineAdvanceInputLockSeconds = 0.2f;
        [SerializeField] private float bgmFadeOutSeconds = 1f;

        private readonly Dictionary<string, Sprite> bgLookup = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> standLookup = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> bgmLookup = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> seLookup = new(StringComparer.Ordinal);

        private readonly List<DialogueRow> rows = new();
        private int rowIndex;
        private Coroutine playRoutine;
        private bool advanceInputBlocked;
        private bool skipRequested;
        private Action onFinished;
        private Vector2 leftBaseAnchored;
        private Vector2 rightBaseAnchored;
        private float defaultDimAlpha = 0.45f;

        private void Awake()
        {
            BuildLookups();
            ApplyStandingLayoutDefaults();
            if (skipButton != null)
            {
                skipButton.onClick.RemoveListener(OnSkipClicked);
                skipButton.onClick.AddListener(OnSkipClicked);
            }

            if (overlayBackgroundImage != null)
            {
                overlayBackgroundImage.gameObject.SetActive(false);
            }

            if (advanceCursorCanvasGroup != null)
            {
                advanceCursorCanvasGroup.alpha = 0f;
            }
        }

        public void BeginPlay(string sceneName, string stageKey, float dimAlpha, Action finished)
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            onFinished = finished;
            defaultDimAlpha = Mathf.Clamp01(dimAlpha);
            skipRequested = false;
            advanceInputBlocked = false;
            ApplyDim(defaultDimAlpha);
            HideStanding();
            if (overlayBackgroundImage != null)
            {
                overlayBackgroundImage.gameObject.SetActive(false);
            }

            if (speakerText != null)
            {
                speakerText.text = string.Empty;
            }

            if (bodyText != null)
            {
                bodyText.text = string.Empty;
            }

            if (!TryLoadRows(sceneName, stageKey))
            {
                Debug.LogError(
                    $"[CraftDialogue] CSV missing: {sceneName}-{stageKey}. Run Tools/GameData/Sync All Catalogs.",
                    this);
                FinishPlay();
                return;
            }

            playRoutine = StartCoroutine(RunSequence());
        }

        private bool TryLoadRows(string sceneName, string stageKey)
        {
            rows.Clear();
            DialogueScriptCatalog catalog = GameDataCatalogs.Dialogue;
            if (catalog != null &&
                catalog.TryGetTextAsset(sceneName, stageKey, out TextAsset csv) &&
                csv != null)
            {
                rows.AddRange(DialogueCsvParser.ParseFromText(csv.text));
                return rows.Count > 0;
            }

#if UNITY_EDITOR
            string path = DialogueCsvPaths.GetEditorDialogueCsvPath(sceneName, stageKey);
            if (File.Exists(path))
            {
                rows.AddRange(DialogueCsvParser.ParseFromFilePath(path));
                return rows.Count > 0;
            }
#endif
            return false;
        }

        private IEnumerator RunSequence()
        {
            rowIndex = 0;
            while (rowIndex < rows.Count)
            {
                if (skipRequested)
                {
                    break;
                }

                DialogueRow row = rows[rowIndex];
                switch (row.Type)
                {
                    case DialogueRowType.Line:
                        yield return LineRoutine(row);
                        rowIndex++;
                        break;
                    case DialogueRowType.Stand:
                        yield return StandRoutine(row);
                        rowIndex++;
                        break;
                    case DialogueRowType.BgmStop:
                        yield return FadeOutAndStopBgm(ParseFloatOr(row.Mode, bgmFadeOutSeconds));
                        rowIndex++;
                        break;
                    case DialogueRowType.Camera:
                        yield return CameraRoutine(row);
                        rowIndex++;
                        break;
                    case DialogueRowType.End:
                        yield return FinishSequenceRoutine();
                        yield break;
                    default:
                        ApplyImmediate(row);
                        rowIndex++;
                        yield return null;
                        break;
                }
            }

            yield return FinishSequenceRoutine();
        }

        private IEnumerator FinishSequenceRoutine()
        {
            yield return FadeOutAndStopBgm(bgmFadeOutSeconds);
            FinishPlay();
        }

        private void FinishPlay()
        {
            playRoutine = null;
            Action done = onFinished;
            onFinished = null;
            done?.Invoke();
        }

        private void ApplyImmediate(DialogueRow row)
        {
            switch (row.Type)
            {
                case DialogueRowType.Bg:
                    ApplyOverlayBackground(row);
                    break;
                case DialogueRowType.Dim:
                    ApplyDim(ParseFloatOr(row.Mode, defaultDimAlpha));
                    break;
                case DialogueRowType.Se:
                    PlaySe(row.AudioKey);
                    break;
                case DialogueRowType.Bgm:
                    PlayBgm(row.AudioKey);
                    break;
            }
        }

        private void ApplyOverlayBackground(DialogueRow row)
        {
            if (overlayBackgroundImage == null)
            {
                return;
            }

            string mode = row.Mode != null ? row.Mode.Trim().ToLowerInvariant() : string.Empty;
            if (mode == "hide" || string.IsNullOrWhiteSpace(row.SpriteKey))
            {
                overlayBackgroundImage.gameObject.SetActive(false);
                return;
            }

            if (!bgLookup.TryGetValue(row.SpriteKey, out Sprite sp) || sp == null)
            {
                Debug.LogWarning($"[CraftDialogue] BG key not found: '{row.SpriteKey}'", this);
                return;
            }

            overlayBackgroundImage.sprite = sp;
            overlayBackgroundImage.color = Color.white;
            overlayBackgroundImage.gameObject.SetActive(true);
        }

        private void ApplyDim(float alpha)
        {
            if (dimmerImage == null)
            {
                return;
            }

            Color c = dimmerImage.color;
            c.a = Mathf.Clamp01(alpha);
            dimmerImage.color = c;
            dimmerImage.gameObject.SetActive(c.a > 0.001f);
        }

        private IEnumerator CameraRoutine(DialogueRow row)
        {
            if (cameraController == null)
            {
                yield break;
            }

            if (!TryParseXy(row.Text, out Vector2 xy))
            {
                Debug.LogWarning($"[CraftDialogue] camera text must be x,y: '{row.Text}'", this);
                yield break;
            }

            float duration = ParseFloatOr(row.Mode, 0f);
            float size = ParseFloatOr(row.AudioKey, 0f);
            cameraController.FrameForDialogue(xy, size, duration);
            if (duration > 0.0001f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    if (skipRequested)
                    {
                        yield break;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        private IEnumerator StandRoutine(DialogueRow row)
        {
            string slot = row.Slot.Trim().ToUpperInvariant();
            Image img = slot == "L" ? standingLeftImage : slot == "R" ? standingRightImage : null;
            if (img == null)
            {
                yield break;
            }

            string mode = row.Mode.Trim().ToLowerInvariant();
            RectTransform rt = img.rectTransform;
            rt.DOKill();

            if (mode == "hide")
            {
                img.gameObject.SetActive(false);
                yield break;
            }

            if (!standLookup.TryGetValue(row.SpriteKey, out Sprite sp) || sp == null)
            {
                Debug.LogWarning($"[CraftDialogue] Stand key not found: '{row.SpriteKey}'", this);
                yield break;
            }

            img.sprite = sp;
            img.gameObject.SetActive(true);
            bool isLeft = slot == "L";
            ApplyStandLayout(img, isLeft, row);
            Vector2 basePos = isLeft ? leftBaseAnchored : rightBaseAnchored;

            if (mode == "lean_in")
            {
                Vector2 from = basePos + new Vector2(isLeft ? -leanInOffsetPixels : leanInOffsetPixels, 0f);
                rt.anchoredPosition = from;
                Tween tw = rt.DOAnchorPos(basePos, Mathf.Max(0.05f, leanInDurationSeconds))
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true);
                yield return tw.WaitForCompletion();
            }
            else
            {
                rt.anchoredPosition = basePos;
            }
        }

        private IEnumerator LineRoutine(DialogueRow row)
        {
            if (speakerText != null)
            {
                speakerText.text = row.Speaker ?? string.Empty;
            }

            if (bodyText == null)
            {
                yield break;
            }

            string full = row.Text ?? string.Empty;
            bodyText.text = string.Empty;
            float lockSeconds = Mathf.Max(0f, lineAdvanceInputLockSeconds);
            float delay = 1f / Mathf.Max(1f, typewriterCharsPerSecond);
            float elapsed = 0f;
            int visible = 0;
            SetCursorVisible(false);

            while (visible < full.Length)
            {
                if (skipRequested)
                {
                    yield break;
                }

                if (lockSeconds > 0f)
                {
                    lockSeconds -= Time.unscaledDeltaTime;
                }
                else if (GetAdvancePressed())
                {
                    visible = full.Length;
                    bodyText.text = full;
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                while (elapsed >= delay && visible < full.Length)
                {
                    elapsed -= delay;
                    visible++;
                    bodyText.text = full.Substring(0, visible);
                }

                yield return null;
            }

            bodyText.text = full;
            SetCursorVisible(true);

            lockSeconds = Mathf.Max(0f, lineAdvanceInputLockSeconds);
            while (true)
            {
                if (skipRequested)
                {
                    yield break;
                }

                if (lockSeconds > 0f)
                {
                    lockSeconds -= Time.unscaledDeltaTime;
                    yield return null;
                    continue;
                }

                if (GetAdvancePressed())
                {
                    PlayAdvanceSe();
                    break;
                }

                yield return null;
            }

            SetCursorVisible(false);
        }

        private void OnSkipClicked()
        {
            if (skipSeClip != null && seSource != null)
            {
                seSource.PlayOneShot(skipSeClip);
            }

            skipRequested = true;
            advanceInputBlocked = true;
        }

        private bool GetAdvancePressed()
        {
            if (advanceInputBlocked || skipRequested)
            {
                return false;
            }

            if (KomayamaCraftAutoPlayInput.IsActive &&
                KomayamaCraftAutoPlayInput.ConsumeDialogueAdvance())
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                return true;
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                // Skip ボタン上のクリックは送らない
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    var results = new List<UnityEngine.EventSystems.RaycastResult>();
                    var ped = new UnityEngine.EventSystems.PointerEventData(
                        UnityEngine.EventSystems.EventSystem.current)
                    {
                        position = mouse.position.ReadValue()
                    };
                    UnityEngine.EventSystems.EventSystem.current.RaycastAll(ped, results);
                    for (int i = 0; i < results.Count; i++)
                    {
                        if (skipButton != null &&
                            results[i].gameObject != null &&
                            results[i].gameObject.transform.IsChildOf(skipButton.transform))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
#endif
            return false;
        }

        private void SetCursorVisible(bool visible)
        {
            if (advanceCursorCanvasGroup != null)
            {
                advanceCursorCanvasGroup.alpha = visible ? 1f : 0f;
            }
        }

        private void PlayAdvanceSe()
        {
            if (advanceTextSeClip != null && seSource != null)
            {
                seSource.PlayOneShot(advanceTextSeClip);
            }
        }

        private void PlaySe(string key)
        {
            if (string.IsNullOrEmpty(key) || seSource == null)
            {
                return;
            }

            if (seLookup.TryGetValue(key, out AudioClip clip) && clip != null)
            {
                seSource.PlayOneShot(clip);
            }
        }

        private void PlayBgm(string key)
        {
            if (bgmSource == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            if (!bgmLookup.TryGetValue(key, out AudioClip clip) || clip == null)
            {
                return;
            }

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.volume = 1f;
            bgmSource.Play();
        }

        private IEnumerator FadeOutAndStopBgm(float seconds)
        {
            if (bgmSource == null || !bgmSource.isPlaying)
            {
                yield break;
            }

            float fade = Mathf.Max(0f, seconds);
            if (fade <= 0.0001f)
            {
                bgmSource.Stop();
                bgmSource.clip = null;
                yield break;
            }

            float start = bgmSource.volume;
            float elapsed = 0f;
            while (elapsed < fade)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / fade));
                yield return null;
            }

            bgmSource.Stop();
            bgmSource.clip = null;
            bgmSource.volume = 1f;
        }

        private void HideStanding()
        {
            if (standingLeftImage != null)
            {
                standingLeftImage.gameObject.SetActive(false);
            }

            if (standingRightImage != null)
            {
                standingRightImage.gameObject.SetActive(false);
            }
        }

        private void ApplyStandingLayoutDefaults()
        {
            ApplyStandingSlot(standingLeftImage, standingLeftAnchor, standingLeftDefaultSize);
            ApplyStandingSlot(standingRightImage, standingRightAnchor, standingRightDefaultSize);
            if (standingLeftImage != null)
            {
                leftBaseAnchored = standingLeftImage.rectTransform.anchoredPosition;
            }

            if (standingRightImage != null)
            {
                rightBaseAnchored = standingRightImage.rectTransform.anchoredPosition;
            }
        }

        private void ApplyStandingSlot(Image image, Vector2 anchor, Vector2 size)
        {
            if (image == null)
            {
                return;
            }

            RectTransform rt = image.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = standingDefaultAnchoredPosition;
            image.preserveAspect = true;
        }

        private void ApplyStandLayout(Image image, bool isLeft, DialogueRow row)
        {
            Vector2 anchor = isLeft ? standingLeftAnchor : standingRightAnchor;
            if (DialogueStandCsvLayout.TryParsePosition(row.Speaker, out Vector2 parsedAnchor))
            {
                anchor = parsedAnchor;
            }

            Vector2 size = isLeft ? standingLeftDefaultSize : standingRightDefaultSize;
            if (DialogueStandCsvLayout.TryParseSize(row.AudioKey, out Vector2 parsedSize))
            {
                if (parsedSize.x > 0f)
                {
                    size.x = parsedSize.x;
                }

                if (parsedSize.y > 0f)
                {
                    size.y = parsedSize.y;
                }
            }

            Vector2 anchored = standingDefaultAnchoredPosition;
            if (DialogueStandCsvLayout.TryParsePosition(row.Text, out Vector2 parsedPos))
            {
                anchored = parsedPos;
            }

            ApplyStandingSlot(image, anchor, size);
            image.rectTransform.anchoredPosition = anchored;
            if (isLeft)
            {
                leftBaseAnchored = anchored;
            }
            else
            {
                rightBaseAnchored = anchored;
            }
        }

        private void BuildLookups()
        {
            FillSprite(bgLookup, backgroundSprites);
            FillSprite(standLookup, standingSprites);
            FillAudio(bgmLookup, bgmClips);
            FillAudio(seLookup, seClips);
        }

        private static void FillSprite(Dictionary<string, Sprite> dict, NamedSpriteEntry[] entries)
        {
            dict.Clear();
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                NamedSpriteEntry e = entries[i];
                if (e == null || string.IsNullOrEmpty(e.key) || e.sprite == null)
                {
                    continue;
                }

                dict[e.key] = e.sprite;
            }
        }

        private static void FillAudio(Dictionary<string, AudioClip> dict, NamedAudioEntry[] entries)
        {
            dict.Clear();
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                NamedAudioEntry e = entries[i];
                if (e == null || string.IsNullOrEmpty(e.key) || e.clip == null)
                {
                    continue;
                }

                dict[e.key] = e.clip;
            }
        }

        private static bool TryParseXy(string raw, out Vector2 xy)
        {
            xy = Vector2.zero;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string[] parts = raw.Split(',');
            if (parts.Length < 2)
            {
                return false;
            }

            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                return false;
            }

            xy = new Vector2(x, y);
            return true;
        }

        private static float ParseFloatOr(string raw, float fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            return float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                ? v
                : fallback;
        }
    }
}
