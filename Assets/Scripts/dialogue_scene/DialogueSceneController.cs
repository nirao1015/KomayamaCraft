using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DialogueScene
{
    /// <summary>
    /// CSV 会話ランナー（TASK-101）。参照未設定時はランタイムで UI を生成する。
    /// </summary>
    public class DialogueSceneController : MonoBehaviour
    {
        [Header("CSV")]
        [Tooltip("カタログ読み込み失敗時（Editor のみ）に使う相対パス（プロジェクトルート基準）。例: spec/dialogue_sample.csv")]
        [SerializeField] private string fallbackCsvRelativePath = "spec/dialogue_sample.csv";

        [Header("UI（任意・空ならランタイム生成）")]
        [Tooltip("背景 Image。未設定なら実行時に生成。")]
        [SerializeField] private Image backgroundImage;
        [Tooltip("立ち絵左。未設定なら実行時に生成。")]
        [SerializeField] private Image standingLeftImage;
        [Tooltip("立ち絵右。未設定なら実行時に生成。")]
        [SerializeField] private Image standingRightImage;
        [Tooltip("話者名（TMP）。None のときは再生中にコードが TMP を生成するため、シーン上でフォントを指定できません。シーンに Canvas／TMP を置いてここに割り当ててください。")]
        [SerializeField] private TextMeshProUGUI speakerText;
        [Tooltip("本文（TMP）。None のときは実行時生成。フォントを Inspector で固定したい場合はシーンの TMP を割り当ててください。")]
        [SerializeField] private TextMeshProUGUI bodyText;
        [Tooltip("送りカーソル用 CanvasGroup。未設定なら実行時に生成。")]
        [SerializeField] private CanvasGroup advanceCursorCanvasGroup;
        [Tooltip("送りカーソル画像。未設定なら実行時に生成。")]
        [SerializeField] private Image advanceCursorImage;
        [Tooltip("送りカーソルの既定スプライト。advanceCursorImage がある場合はこれを優先適用。")]
        [SerializeField] private Sprite defaultAdvanceCursorSprite;
        [Tooltip("スキップ背景の既定スプライト（横方向ストレッチで適用）。")]
        [SerializeField] private Sprite defaultSkipBackgroundSprite;
        [Tooltip("スキップ背景 Image の色（アルファ含む）。defaultSkipBackgroundSprite 適用時の乗算色。")]
        [SerializeField] private Color skipBackgroundTint = new Color(1f, 1f, 1f, 0.8f);
        [Tooltip("スキップ Button。未設定なら実行時に生成。")]
        [SerializeField] private Button skipButton;

        [Header("会話テキスト")]
        [Tooltip("会話で使う TMP Font Asset（SDF）。未設定のときは各 TextMeshProUGUI に設定されているフォントのまま。")]
        [SerializeField] private TMP_FontAsset dialogueFont;
        [Tooltip("スキップボタン専用の TMP Font Asset。未設定時は dialogueFont を使う。")]
        [SerializeField] private TMP_FontAsset skipButtonFont;
        [SerializeField] private Color skipLabelOutlineColor = Color.black;
        [Range(0f, 1f)]
        [SerializeField] private float skipLabelOutlineWidth = 0.08f;
        [SerializeField] private Color skipLabelShadowColor = new Color(0f, 0f, 0f, 0.9f);
        [SerializeField] private Vector2 skipLabelShadowOffset = new Vector2(1.5f, -1.5f);
        [SerializeField] private float skipLabelFontSize = 42f;

        [Header("Audio")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource seSource;
        [Tooltip("テキスト送り時に鳴らすSE（例: SE/diaalog/DM-CGS-21）")]
        [SerializeField] private AudioClip advanceTextSeClip;
        [Tooltip("スキップ押下時に鳴らすSE（例: 魔王魂 効果音 ワンポイント30）")]
        [SerializeField] private AudioClip skipSeClip;

        [Header("アセット辞書（キーは CSV の sprite_key / audio_key）")]
        [SerializeField] private NamedSpriteEntry[] backgroundSprites = Array.Empty<NamedSpriteEntry>();
        [SerializeField] private NamedSpriteEntry[] standingSprites = Array.Empty<NamedSpriteEntry>();
        [SerializeField] private NamedAudioEntry[] bgmClips = Array.Empty<NamedAudioEntry>();
        [SerializeField] private NamedAudioEntry[] seClips = Array.Empty<NamedAudioEntry>();

        [Header("背景揺れ（fx_shake*）")]
        [SerializeField] private DialogueBackgroundShakeEffect backgroundShakeEffect;
        [Header("game03 ED 専用（fx_game03_ed_finale）")]
        [SerializeField] private DialogueGame03EdFinaleEffect game03EdFinaleEffect;

        [Header("立ち絵レイアウト（会話窓より上）")]
        [SerializeField] private Vector2 standingLeftAnchor = new Vector2(0.2f, 0.36f);
        [SerializeField] private Vector2 standingRightAnchor = new Vector2(0.8f, 0.36f);
        [Tooltip("stand 行の text / audio_key が空のときの anchoredPosition（例: 0,-100）。")]
        [SerializeField] private Vector2 standingDefaultAnchoredPosition = new Vector2(0f, -100f);
        [Tooltip("stand 行の audio_key が空のときの L サイズ（例: 幅 700）。")]
        [SerializeField] private Vector2 standingLeftDefaultSize = new Vector2(700f, 780f);
        [Tooltip("stand 行の audio_key が空のときの R サイズ。")]
        [SerializeField] private Vector2 standingRightDefaultSize = new Vector2(420f, 780f);
        [Header("立ち絵フォーカス（L/R 両方表示時）")]
        [SerializeField] private bool standingFocusDimEnabled = true;
        [Tooltip("非話者の Image.color（スプライト形状のまま暗くする。例: 0.45,0.45,0.45,1）。")]
        [SerializeField] private Color standingDimTintColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        [Tooltip("片方だけ表示のときに学習するほか、最初から R とみなす話者名。")]
        [SerializeField] private string[] standingRightSpeakerNames = { "駒山キリ" };

        [Header("演出")]
        [SerializeField] private float typewriterCharsPerSecond = 32f;
        [SerializeField] private float leanInDurationSeconds = 0.45f;
        [SerializeField] private float leanInOffsetPixels = 900f;
        [SerializeField] private float cursorBlinkPeriodSeconds = 0.5f;
        [SerializeField] private float lineAdvanceInputLockSeconds = 0.2f;

        [Header("遷移")]
        [Tooltip("会話終了後の遷移先（SceneTransitionContext 未設定時）。空なら gameStageSceneName / 複合キーの scene 部分を使用。")]
        [SerializeField] private string defaultPostDialogueSceneName;
        [Tooltip("会話 CSV・遷移の既定。単独シーン名（game_stage_scene）または scene-stage（game03_scene-ed → game03_scene-ed.csv）。")]
        [SerializeField] private string gameStageSceneName = "game_stage_scene";
        [SerializeField] private float fadeOutSeconds = 0.35f;
        [SerializeField] private float fadeInSeconds = 0.25f;
        [Tooltip("メニューの FadeCanvas（DontDestroyOnLoad）が黒のまま残る場合に、入場時に明るく戻す秒数。0 以下で無効。")]
        [SerializeField] private float incomingMenuFadeOutSeconds = 0.5f;
        [SerializeField] private float bgmFadeOutSeconds = 1f;

        private readonly Dictionary<string, Sprite> _bgLookup = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> _standLookup = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> _bgmLookup = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> _seLookup = new Dictionary<string, AudioClip>(StringComparer.Ordinal);

        private List<DialogueRow> _rows = new List<DialogueRow>();
        private int _rowIndex;
        private string _resolvedPostDialogueSceneName;
        private Coroutine _dialogueCoroutine;
        private bool _lineTypingComplete;
        private bool _advanceInputBlocked;
        private Vector2 _leftBaseAnchored;
        private Vector2 _rightBaseAnchored;
        private DialogueStandingFocusEffect _standingFocusEffect;

        private void Awake()
        {
            BuildLookupTables();
            if (backgroundImage == null || bodyText == null)
            {
                BuildRuntimeUi();
            }
            ConfigureAspectCanvases();

            ApplyStandingLayout();
            EnsureStandingFocusEffect();
            if (advanceCursorCanvasGroup != null)
            {
                advanceCursorCanvasGroup.alpha = 0f;
            }
            ApplyDefaultAdvanceCursorSprite();
            ApplySkipButtonBackgroundStyle();

            if (skipButton != null)
            {
                skipButton.onClick.RemoveListener(OnSkipClicked);
                skipButton.onClick.AddListener(OnSkipClicked);
            }

            EnsureSeparatedDialogueAudioSources();
            EnsureBackgroundShakeEffect();
            if (game03EdFinaleEffect == null)
            {
                game03EdFinaleEffect = GetComponent<DialogueGame03EdFinaleEffect>();
            }

            if (bgmSource != null)
            {
                bgmSource.volume = 1f;
            }

            if (seSource != null)
            {
                seSource.volume = 1f;
            }

            ApplyDialogueFontIfSet();
            ApplySkipLabelOutline();
        }

        private void ApplyDialogueFontIfSet()
        {
            void Apply(TextMeshProUGUI t)
            {
                if (t != null)
                {
                    t.font = dialogueFont;
                }
            }

            if (dialogueFont != null)
            {
                Apply(speakerText);
                Apply(bodyText);
            }

            ApplySkipButtonFont();
        }

        private void ApplySkipButtonFont()
        {
            if (skipButton == null)
            {
                return;
            }

            TMP_FontAsset font = skipButtonFont != null ? skipButtonFont : dialogueFont;
            if (font == null)
            {
                return;
            }

            TextMeshProUGUI skipLabel = skipButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (skipLabel != null)
            {
                skipLabel.font = font;
            }
        }

        private void Start()
        {
            TryClearIncomingMenuFade();
            LoadDialogueRows();
            if (_rows.Count == 0)
            {
                Debug.LogError("[DialogueScene] No dialogue rows loaded.");
                return;
            }

            _dialogueCoroutine = StartCoroutine(RunDialogueSequence());
        }

        private void TryClearIncomingMenuFade()
        {
            if (incomingMenuFadeOutSeconds <= 0f)
            {
                return;
            }

            Fade fade = FindAnyObjectByType<Fade>();
            if (fade == null)
            {
                return;
            }

            fade.FadeOut(Mathf.Max(0.01f, incomingMenuFadeOutSeconds));
        }

        private void Update()
        {
            if (advanceCursorCanvasGroup != null && _lineTypingComplete && _dialogueCoroutine != null)
            {
                float a = (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / Mathf.Max(0.1f, cursorBlinkPeriodSeconds))) + 1f) * 0.5f;
                advanceCursorCanvasGroup.alpha = a;
            }
            else if (advanceCursorCanvasGroup != null)
            {
                advanceCursorCanvasGroup.alpha = 0f;
            }
        }

        private void OnDestroy()
        {
            ShutdownBackgroundShake();

            if (standingLeftImage != null)
            {
                standingLeftImage.rectTransform.DOKill();
            }

            if (standingRightImage != null)
            {
                standingRightImage.rectTransform.DOKill();
            }
        }

        private void BuildLookupTables()
        {
            FillDict(_bgLookup, backgroundSprites);
            FillDict(_standLookup, standingSprites);
            FillAudioDict(_bgmLookup, bgmClips);
            FillAudioDict(_seLookup, seClips);
        }

        private void EnsureSeparatedDialogueAudioSources()
        {
            if (bgmSource == null)
            {
                bgmSource = GetComponent<AudioSource>();
            }

            if (seSource == null || seSource == bgmSource)
            {
                seSource = gameObject.AddComponent<AudioSource>();
            }

            if (bgmSource != null)
            {
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
                bgmSource.volume = 1f;
            }

            if (seSource != null)
            {
                seSource.playOnAwake = false;
                seSource.loop = false;
                seSource.volume = 1f;
            }
        }

        private static void FillDict(Dictionary<string, Sprite> dict, NamedSpriteEntry[] entries)
        {
            dict.Clear();
            if (entries == null)
            {
                return;
            }

            foreach (NamedSpriteEntry e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.key) || e.sprite == null)
                {
                    continue;
                }

                dict[e.key] = e.sprite;
            }
        }

        private static void FillAudioDict(Dictionary<string, AudioClip> dict, NamedAudioEntry[] entries)
        {
            dict.Clear();
            if (entries == null)
            {
                return;
            }

            foreach (NamedAudioEntry e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.key) || e.clip == null)
                {
                    continue;
                }

                dict[e.key] = e.clip;
            }
        }

        private void LoadDialogueRows()
        {
            _rows.Clear();
            ResolveDialogueTransitionKeys(out string csvSceneName, out string csvStageName);

            if (!TryLoadDialogueRowsFromCatalog(csvSceneName, csvStageName))
            {
                Debug.LogError(
                    $"[DialogueScene] Dialogue CSV not found for scene='{csvSceneName}' stage='{csvStageName}'. " +
                    "Run Tools/GameData/Sync All Catalogs and ensure Assets/GameData/Dialogue has the file.");
            }
        }

        private bool TryLoadDialogueRowsFromCatalog(string csvSceneName, string csvStageName)
        {
            DialogueScriptCatalog catalog = GameDataCatalogs.Dialogue;
            if (catalog != null && catalog.TryGetTextAsset(csvSceneName, csvStageName, out TextAsset csv))
            {
                _rows.AddRange(DialogueCsvParser.ParseFromText(csv.text));
                return _rows.Count > 0;
            }

#if UNITY_EDITOR
            string editorPath = DialogueCsvPaths.GetEditorDialogueCsvPath(csvSceneName, csvStageName);
            if (File.Exists(editorPath))
            {
                _rows.AddRange(DialogueCsvParser.ParseFromEditorGameDataStage(csvSceneName, csvStageName));
                return _rows.Count > 0;
            }

            if (!string.IsNullOrEmpty(fallbackCsvRelativePath))
            {
                try
                {
                    _rows.AddRange(DialogueCsvParser.ParseFromFilePath(fallbackCsvRelativePath));
                    Debug.LogWarning(
                        $"[DialogueScene] Catalog miss; using fallback CSV: {fallbackCsvRelativePath}");
                    return _rows.Count > 0;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DialogueScene] Fallback CSV failed: {e.Message}");
                }
            }
#endif
            return false;
        }

        private void ResolveDialogueTransitionKeys(out string csvSceneName, out string csvStageName)
        {
            csvStageName = null;
            string contextDestination = SceneTransitionContext.DestinationSceneName;
            string contextStage = SceneTransitionContext.StageName;
            string csvSceneOverride = SceneTransitionContext.DialogueStreamingSceneNameOverride;
            bool usedInspectorComposite = false;

            if (!string.IsNullOrWhiteSpace(csvSceneOverride))
            {
                SceneTransitionContext.DialogueStreamingSceneNameOverride = null;
                csvSceneName = csvSceneOverride.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(contextDestination))
            {
                csvSceneName = contextDestination.Trim();
            }
            else if (DialogueCsvPaths.TryParseCompositeSceneStage(gameStageSceneName, out string parsedScene, out string parsedStage))
            {
                csvSceneName = parsedScene;
                csvStageName = parsedStage;
                usedInspectorComposite = true;
            }
            else
            {
                csvSceneName = string.IsNullOrWhiteSpace(gameStageSceneName)
                    ? "game_stage_scene"
                    : gameStageSceneName.Trim();
            }

            // Inspector の scene-stage 指定時は StageName 既定値 stage_01 で上書きしない。
            if (!usedInspectorComposite && string.IsNullOrWhiteSpace(csvStageName))
            {
                csvStageName = string.IsNullOrWhiteSpace(contextStage) ? null : contextStage.Trim();
            }

            if (!string.IsNullOrWhiteSpace(contextDestination))
            {
                _resolvedPostDialogueSceneName = contextDestination.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(defaultPostDialogueSceneName))
            {
                _resolvedPostDialogueSceneName = defaultPostDialogueSceneName.Trim();
            }
            else if (DialogueCsvPaths.TryParseCompositeSceneStage(gameStageSceneName, out string postScene, out _))
            {
                _resolvedPostDialogueSceneName = postScene;
            }
            else
            {
                _resolvedPostDialogueSceneName = string.IsNullOrWhiteSpace(gameStageSceneName)
                    ? "game_stage_scene"
                    : gameStageSceneName.Trim();
            }
        }

        private void ApplyStandingLayout()
        {
            ApplyStandingSlotLayout(
                standingLeftImage,
                standingLeftAnchor,
                standingLeftDefaultSize,
                standingDefaultAnchoredPosition);
            ApplyStandingSlotLayout(
                standingRightImage,
                standingRightAnchor,
                standingRightDefaultSize,
                standingDefaultAnchoredPosition);
            CacheStandingBases();
        }

        private void ApplyStandingSlotLayout(Image standingImage, Vector2 anchor, Vector2 size, Vector2 anchoredPosition)
        {
            if (standingImage == null)
            {
                return;
            }

            RectTransform rt = standingImage.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPosition;
            standingImage.preserveAspect = true;
        }

        private void ApplyStandRowLayout(Image standingImage, bool isLeft, DialogueRow row)
        {
            if (standingImage == null)
            {
                return;
            }

            Vector2 anchor = isLeft ? standingLeftAnchor : standingRightAnchor;
            if (DialogueStandCsvLayout.TryParsePosition(row.Speaker, out Vector2 parsedAnchor))
            {
                anchor = parsedAnchor;
            }

            Vector2 defaultSize = isLeft ? standingLeftDefaultSize : standingRightDefaultSize;
            Vector2 size = defaultSize;
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

            Vector2 anchoredPosition = standingDefaultAnchoredPosition;
            if (DialogueStandCsvLayout.TryParsePosition(row.Text, out Vector2 parsedPosition))
            {
                anchoredPosition = parsedPosition;
            }

            ApplyStandingSlotLayout(standingImage, anchor, size, anchoredPosition);

            if (isLeft)
            {
                _leftBaseAnchored = anchoredPosition;
            }
            else
            {
                _rightBaseAnchored = anchoredPosition;
            }
        }

        private void CacheStandingBases()
        {
            if (standingLeftImage != null)
            {
                _leftBaseAnchored = standingLeftImage.rectTransform.anchoredPosition;
            }

            if (standingRightImage != null)
            {
                _rightBaseAnchored = standingRightImage.rectTransform.anchoredPosition;
            }
        }

        private IEnumerator RunDialogueSequence()
        {
            _rowIndex = 0;
            while (_rowIndex < _rows.Count)
            {
                DialogueRow row = _rows[_rowIndex];
                switch (row.Type)
                {
                    case DialogueRowType.Line:
                        yield return LineRoutine(row);
                        _rowIndex++;
                        break;
                    case DialogueRowType.Stand:
                        yield return StandRoutine(row);
                        _rowIndex++;
                        break;
                    case DialogueRowType.BgmStop:
                        yield return BgmStopRoutine(row);
                        _rowIndex++;
                        break;
                    case DialogueRowType.FxGame03EdFinale:
                        yield return Game03EdFinaleRoutine();
                        _rowIndex++;
                        break;
                    case DialogueRowType.End:
                        yield return GoToGameStageRoutine();
                        yield break;
                    default:
                        ApplyImmediateRow(row);
                        _rowIndex++;
                        yield return null;
                        break;
                }
            }

            yield return GoToGameStageRoutine();
        }

        private void ApplyImmediateRow(DialogueRow row)
        {
            switch (row.Type)
            {
                case DialogueRowType.Bg:
                    ApplyBackground(row.SpriteKey);
                    break;
                case DialogueRowType.Se:
                    PlaySe(row.AudioKey);
                    break;
                case DialogueRowType.Bgm:
                    PlayBgm(row.AudioKey);
                    break;
                case DialogueRowType.FxShake:
                    PlayBackgroundShakeOneShot(row.Mode);
                    break;
                case DialogueRowType.FxShakeStart:
                    StartBackgroundShakeContinuous(row.Mode);
                    break;
                case DialogueRowType.FxShakeStop:
                    StopBackgroundShake();
                    break;
            }
        }

        private IEnumerator BgmStopRoutine(DialogueRow row)
        {
            yield return FadeOutAndStopBgm(ParseBgmFadeSeconds(row.Mode));
        }

        private IEnumerator Game03EdFinaleRoutine()
        {
            if (game03EdFinaleEffect == null)
            {
                Debug.LogWarning("[DialogueScene] fx_game03_ed_finale: DialogueGame03EdFinaleEffect is missing.");
                yield break;
            }

            if (!_bgLookup.TryGetValue("dialog_title", out Sprite titleSprite))
            {
                Debug.LogWarning("[DialogueScene] fx_game03_ed_finale: background key 'dialog_title' not found.");
                yield break;
            }

            _advanceInputBlocked = true;
            SetSkipInteractable(false);
            ShutdownBackgroundShake();

            GameObject[] hideTargets = CollectFinaleHideTargets();
            yield return game03EdFinaleEffect.PlayFinale(backgroundImage, titleSprite, hideTargets);

            _advanceInputBlocked = false;
            SetSkipInteractable(true);
        }

        private GameObject[] CollectFinaleHideTargets()
        {
            var list = new List<GameObject>(4);
            if (standingLeftImage != null)
            {
                list.Add(standingLeftImage.gameObject);
            }

            if (standingRightImage != null)
            {
                list.Add(standingRightImage.gameObject);
            }

            if (speakerText != null && speakerText.transform.parent != null)
            {
                list.Add(speakerText.transform.parent.gameObject);
            }

            return list.ToArray();
        }

        private void SetSkipInteractable(bool interactable)
        {
            if (skipButton != null)
            {
                skipButton.interactable = interactable;
            }
        }

        private IEnumerator StandRoutine(DialogueRow row)
        {
            string slot = row.Slot.Trim().ToUpperInvariant();
            Image img = slot == "L" ? standingLeftImage : slot == "R" ? standingRightImage : null;
            if (img == null)
            {
                Debug.LogWarning($"[DialogueScene] Invalid stand slot '{row.Slot}'.");
                yield break;
            }

            string mode = row.Mode.Trim().ToLowerInvariant();
            RectTransform rt = img.rectTransform;
            rt.DOKill();

            if (mode == "hide")
            {
                img.gameObject.SetActive(false);
                _standingFocusEffect?.RefreshAfterStandChange();
                yield break;
            }

            img.gameObject.SetActive(true);
            if (!_standLookup.TryGetValue(row.SpriteKey, out Sprite sp))
            {
                Debug.LogWarning($"[DialogueScene] Standing sprite key not found: '{row.SpriteKey}'");
                yield break;
            }

            img.sprite = sp;
            bool isLeft = slot == "L";
            ApplyStandRowLayout(img, isLeft, row);
            Vector2 basePos = isLeft ? _leftBaseAnchored : _rightBaseAnchored;

            if (mode == "lean_in")
            {
                Vector2 from = basePos + new Vector2(slot == "L" ? -leanInOffsetPixels : leanInOffsetPixels, 0f);
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

            _standingFocusEffect?.RefreshAfterStandChange();
        }

        private void EnsureStandingFocusEffect()
        {
            if (_standingFocusEffect == null)
            {
                _standingFocusEffect = new DialogueStandingFocusEffect(
                    standingFocusDimEnabled,
                    standingDimTintColor,
                    standingRightSpeakerNames);
            }

            _standingFocusEffect.Bind(standingLeftImage, standingRightImage);
        }

        private IEnumerator LineRoutine(DialogueRow row)
        {
            _lineTypingComplete = false;
            _standingFocusEffect?.ApplyForLine(row.Speaker);
            if (speakerText != null)
            {
                speakerText.text = row.Speaker;
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

            // 1文字送り中に入力があれば全文表示する。
            while (visible < full.Length)
            {
                if (lockSeconds > 0f)
                {
                    lockSeconds -= Time.unscaledDeltaTime;
                }
                else if (GetAdvancePressedIfAllowed())
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
            _lineTypingComplete = true;

            // 全文表示後も連打暴発を防ぐため、次行送り入力を短時間ロックする。
            lockSeconds = Mathf.Max(0f, lineAdvanceInputLockSeconds);
            while (true)
            {
                if (lockSeconds > 0f)
                {
                    lockSeconds -= Time.unscaledDeltaTime;
                    yield return null;
                    continue;
                }

                if (GetAdvancePressedIfAllowed())
                {
                    PlayAdvanceTextSe();
                    break;
                }

                yield return null;
            }

            _lineTypingComplete = false;
        }

        private void ApplyBackground(string key)
        {
            if (backgroundImage == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            if (_bgLookup.TryGetValue(key, out Sprite sp))
            {
                backgroundImage.sprite = sp;
                // 生成時の暗色をリセットして、背景スプライトを原色で表示する。
                backgroundImage.color = Color.white;
                RebindBackgroundShake();
            }
            else
            {
                Debug.LogWarning($"[DialogueScene] Background key not found: '{key}'");
            }
        }

        private void EnsureBackgroundShakeEffect()
        {
            if (backgroundShakeEffect == null)
            {
                backgroundShakeEffect = GetComponent<DialogueBackgroundShakeEffect>();
            }

            if (backgroundShakeEffect == null)
            {
                backgroundShakeEffect = gameObject.AddComponent<DialogueBackgroundShakeEffect>();
            }

            RebindBackgroundShake();
        }

        private void RebindBackgroundShake()
        {
            if (backgroundShakeEffect == null || backgroundImage == null)
            {
                return;
            }

            backgroundShakeEffect.Bind(backgroundImage.rectTransform);
        }

        private void PlayBackgroundShakeOneShot(string preset)
        {
            backgroundShakeEffect?.PlayOneShot(preset);
        }

        private void StartBackgroundShakeContinuous(string preset)
        {
            backgroundShakeEffect?.StartContinuous(preset);
        }

        private void StopBackgroundShake()
        {
            backgroundShakeEffect?.StopAll();
        }

        private void ShutdownBackgroundShake()
        {
            if (backgroundShakeEffect != null)
            {
                backgroundShakeEffect.ShutdownImmediate();
                return;
            }

            if (backgroundImage != null)
            {
                backgroundImage.rectTransform.DOKill();
            }
        }

        private void PlayBgm(string key)
        {
            if (bgmSource == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            if (!_bgmLookup.TryGetValue(key, out AudioClip clip))
            {
                Debug.LogWarning($"[DialogueScene] BGM key not found: '{key}'");
                return;
            }

            bgmSource.loop = true;
            bgmSource.clip = clip;
            bgmSource.volume = SoundSettingsManager.Instance.GetBgmGain01();
            bgmSource.Play();
        }

        private void StopBgm()
        {
            if (bgmSource != null)
            {
                bgmSource.Stop();
            }
        }

        private float ParseBgmFadeSeconds(string modeToken)
        {
            if (!string.IsNullOrWhiteSpace(modeToken) &&
                float.TryParse(modeToken, out float parsed))
            {
                return Mathf.Max(0f, parsed);
            }

            return Mathf.Max(0f, bgmFadeOutSeconds);
        }

        private IEnumerator FadeOutAndStopBgm(float duration)
        {
            if (bgmSource == null || !bgmSource.isPlaying)
            {
                yield break;
            }

            if (duration <= 0.01f)
            {
                StopBgm();
                yield break;
            }

            float startVolume = bgmSource.volume;
            float elapsed = 0f;
            while (elapsed < duration && bgmSource != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }

            if (bgmSource != null)
            {
                StopBgm();
                bgmSource.volume = SoundSettingsManager.Instance.GetBgmGain01();
            }
        }

        private void PlaySe(string key)
        {
            if (seSource == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            if (!_seLookup.TryGetValue(key, out AudioClip clip))
            {
                Debug.LogWarning($"[DialogueScene] SE key not found: '{key}'");
                return;
            }

            seSource.PlayOneShot(clip, SoundSettingsManager.Instance.GetSeGain01());
        }

        private IEnumerator GoToGameStageRoutine()
        {
            ShutdownBackgroundShake();

            string destinationSceneName = string.IsNullOrWhiteSpace(_resolvedPostDialogueSceneName)
                ? gameStageSceneName
                : _resolvedPostDialogueSceneName;

            FadeManager fade = EnsureFadeManager();
            if (fade != null)
            {
                fade.LoadScene(destinationSceneName, fadeOutSeconds, fadeInSeconds);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(destinationSceneName);
            }

            // 消費後はクリアし、次回の dialogue 遷移へ持ち越さない。
            SceneTransitionContext.DestinationSceneName = null;
            SceneTransitionContext.DialogueStreamingSceneNameOverride = null;
            SceneTransitionContext.StageName = null;

            yield break;
        }

        private static FadeManager EnsureFadeManager()
        {
            // FadeManager.Instance は見つからないと LogError するため、直接検索する
            FadeManager fm = FindAnyObjectByType<FadeManager>();
            if (fm == null)
            {
                GameObject go = new GameObject("FadeManager");
                fm = go.AddComponent<FadeManager>();
                fm.DebugMode = false;
            }
            else
            {
                fm.DebugMode = false;
            }

            return fm;
        }

        private void OnSkipClicked()
        {
            if (_advanceInputBlocked)
            {
                return;
            }

            PlaySkipSe();
            ShutdownBackgroundShake();
            StopAllCoroutines();
            _dialogueCoroutine = null;
            StartCoroutine(GoToGameStageRoutine());
        }

        private void PlayAdvanceTextSe()
        {
            if (advanceTextSeClip == null || seSource == null)
            {
                return;
            }

            seSource.PlayOneShot(advanceTextSeClip, SoundSettingsManager.Instance.GetSeGain01());
        }

        private void PlaySkipSe()
        {
            if (skipSeClip == null || seSource == null)
            {
                return;
            }

            seSource.PlayOneShot(skipSeClip, SoundSettingsManager.Instance.GetSeGain01());
        }

        private bool GetAdvancePressedIfAllowed()
        {
            if (_advanceInputBlocked)
            {
                return false;
            }

            return GetAdvancePressed();
        }

        private static bool GetAdvancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                return true;
            }
#endif
            return false;
        }

        private void BuildRuntimeUi()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                Debug.LogWarning("[DialogueScene] EventSystem がありません。シーンに EventSystem（Input System UI）を追加してください。スキップボタンが反応しない場合があります。");
            }

            GameObject canvasGo = new GameObject("DialogueCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<FixedAspectCanvasFitter>();

            RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();

            Sprite white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));

            backgroundImage = CreateStretchImage(canvasRt, "Background", white, new Color(0.12f, 0.12f, 0.15f, 1f));
            EnsureBackgroundShakeEffect();

            GameObject standRoot = new GameObject("StandingRoot");
            standRoot.transform.SetParent(canvasRt, false);
            RectTransform standRootRt = standRoot.AddComponent<RectTransform>();
            StretchFull(standRootRt);

            standingLeftImage = CreateStanding(
                standRootRt,
                "StandingLeft",
                white,
                standingLeftAnchor,
                standingLeftDefaultSize,
                standingDefaultAnchoredPosition);
            standingRightImage = CreateStanding(
                standRootRt,
                "StandingRight",
                white,
                standingRightAnchor,
                standingRightDefaultSize,
                standingDefaultAnchoredPosition);
            standingLeftImage.gameObject.SetActive(false);
            standingRightImage.gameObject.SetActive(false);

            GameObject panel = new GameObject("DialoguePanel");
            panel.transform.SetParent(canvasRt, false);
            RectTransform panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.05f, 0.05f);
            panelRt.anchorMax = new Vector2(0.95f, 0.32f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            Image panelBg = panel.AddComponent<Image>();
            panelBg.sprite = white;
            panelBg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);

            GameObject speakerGo = new GameObject("Speaker");
            speakerGo.transform.SetParent(panelRt, false);
            RectTransform speakerRt = speakerGo.AddComponent<RectTransform>();
            speakerRt.anchorMin = new Vector2(0.02f, 0.72f);
            speakerRt.anchorMax = new Vector2(0.98f, 0.98f);
            speakerRt.offsetMin = Vector2.zero;
            speakerRt.offsetMax = Vector2.zero;
            speakerText = speakerGo.AddComponent<TextMeshProUGUI>();
            speakerText.fontSize = 28f;
            speakerText.color = Color.white;

            GameObject bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(panelRt, false);
            RectTransform bodyRt = bodyGo.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.02f, 0.08f);
            bodyRt.anchorMax = new Vector2(0.98f, 0.68f);
            bodyRt.offsetMin = Vector2.zero;
            bodyRt.offsetMax = Vector2.zero;
            bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
            bodyText.fontSize = 32f;
            bodyText.color = Color.white;
            bodyText.enableWordWrapping = true;
            bodyText.overflowMode = TextOverflowModes.Overflow;

            GameObject cursorGo = new GameObject("AdvanceCursor");
            cursorGo.transform.SetParent(panelRt, false);
            RectTransform cursorRt = cursorGo.AddComponent<RectTransform>();
            cursorRt.anchorMin = new Vector2(0.92f, 0.02f);
            cursorRt.anchorMax = new Vector2(0.98f, 0.12f);
            cursorRt.offsetMin = Vector2.zero;
            cursorRt.offsetMax = Vector2.zero;
            advanceCursorImage = cursorGo.AddComponent<Image>();
            advanceCursorImage.sprite = white;
            advanceCursorImage.color = new Color(1f, 1f, 1f, 0.9f);
            advanceCursorImage.preserveAspect = true;
            advanceCursorCanvasGroup = cursorGo.AddComponent<CanvasGroup>();

            GameObject skipGo = new GameObject("SkipButton");
            skipGo.transform.SetParent(canvasRt, false);
            RectTransform skipRt = skipGo.AddComponent<RectTransform>();
            skipRt.anchorMin = new Vector2(0.85f, 0.88f);
            skipRt.anchorMax = new Vector2(0.98f, 0.98f);
            skipRt.offsetMin = Vector2.zero;
            skipRt.offsetMax = Vector2.zero;
            Image skipImg = skipGo.AddComponent<Image>();
            skipImg.sprite = white;
            skipImg.color = skipBackgroundTint;
            skipImg.type = Image.Type.Simple;
            skipImg.preserveAspect = false;
            skipButton = skipGo.AddComponent<Button>();
            GameObject skipLabel = new GameObject("Label");
            skipLabel.transform.SetParent(skipRt, false);
            RectTransform skipLabelRt = skipLabel.AddComponent<RectTransform>();
            StretchFull(skipLabelRt);
            TextMeshProUGUI skipTmp = skipLabel.AddComponent<TextMeshProUGUI>();
            skipTmp.text = "スキップ";
            skipTmp.alignment = TextAlignmentOptions.Center;
            skipTmp.fontSize = Mathf.Max(1f, skipLabelFontSize);
            skipTmp.color = Color.white;
            // skipTmp.outlineColor = skipLabelOutlineColor;
            // skipTmp.outlineWidth = GetSafeSkipOutlineWidth(skipTmp.fontSize);
            ApplySkipLabelReadableStyle(skipTmp);

            GameObject bgmGo = new GameObject("BgmSource");
            bgmGo.transform.SetParent(transform, false);
            bgmSource = bgmGo.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;

            GameObject seGo = new GameObject("SeSource");
            seGo.transform.SetParent(transform, false);
            seSource = seGo.AddComponent<AudioSource>();
            seSource.playOnAwake = false;
            seSource.loop = false;
        }

        private void ApplyDefaultAdvanceCursorSprite()
        {
            if (advanceCursorImage == null || defaultAdvanceCursorSprite == null)
            {
                return;
            }

            advanceCursorImage.sprite = defaultAdvanceCursorSprite;
            advanceCursorImage.preserveAspect = true;
        }

        private void ApplySkipButtonBackgroundStyle()
        {
            if (skipButton == null)
            {
                return;
            }

            Image bg = skipButton.GetComponent<Image>();
            if (bg == null)
            {
                return;
            }

            if (defaultSkipBackgroundSprite != null)
            {
                bg.sprite = defaultSkipBackgroundSprite;
            }

            bg.type = Image.Type.Simple;
            bg.preserveAspect = false; // 横方向へ伸ばして使う
            bg.color = skipBackgroundTint;
        }

        private void ApplySkipLabelOutline()
        {
            if (skipButton == null)
            {
                return;
            }

            TextMeshProUGUI label = skipButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
            {
                return;
            }

            if (label.fontSize < 1f)
            {
                label.fontSize = Mathf.Max(1f, skipLabelFontSize);
            }

            // label.outlineColor = skipLabelOutlineColor;
            // label.outlineWidth = GetSafeSkipOutlineWidth(label.fontSize);
            ApplySkipLabelReadableStyle(label);
        }

        private void ApplySkipLabelReadableStyle(TextMeshProUGUI label)
        {
            if (label == null)
            {
                return;
            }

            if (label.color.a <= 0f)
            {
                label.color = Color.white;
            }

            Shadow shadow = label.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = label.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = skipLabelShadowColor;
            shadow.effectDistance = skipLabelShadowOffset;
            shadow.useGraphicAlpha = true;
        }

        private float GetSafeSkipOutlineWidth(float fontSize)
        {
            float width = Mathf.Clamp(skipLabelOutlineWidth, 0f, 0.15f);
            if (fontSize <= 24f)
            {
                width = Mathf.Min(width, 0.1f);
            }

            return width;
        }

        private static void ConfigureAspectCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                if (canvas.rootCanvas != canvas)
                {
                    continue;
                }

                if (canvas.GetComponent<FixedAspectCanvasFitter>() == null)
                {
                    canvas.gameObject.AddComponent<FixedAspectCanvasFitter>();
                }
            }
        }

        private static Image CreateStretchImage(RectTransform parent, string name, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            StretchFull(rt);
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private Image CreateStanding(
            RectTransform parent,
            string name,
            Sprite sprite,
            Vector2 anchor,
            Vector2 size,
            Vector2 anchoredPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = Color.white;
            ApplyStandingSlotLayout(img, anchor, size, anchoredPosition);
            return img;
        }
    }

    [Serializable]
    public class NamedSpriteEntry
    {
        public string key;
        public Sprite sprite;
    }

    [Serializable]
    public class NamedAudioEntry
    {
        public string key;
        public AudioClip clip;
    }
}
