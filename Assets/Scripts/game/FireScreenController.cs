using System.Collections;
using UnityEngine;

/// <summary>
/// 仕様: spec/fire_screen_midgame_sequence.md（SE は SoundSettingsManager の SE ゲインに追従）。
/// T0（操作可能）からの遅延後にフェーズ1、フェーズ1開始からシーン転換時間後にフェーズ2。
/// </summary>
public class FireScreenController : MonoBehaviour, IGame01MidgameBackgroundEnemySpawnSuppressor
{
    private const float PlayerFireTargetAlpha = 0.8f;

    [Header("タイミング")]
    [SerializeField, Tooltip("T0 からフェーズ1開始までの秒数（指定時間）")]
    private float delayAfterControlStartSeconds = 3f;

    [SerializeField, Tooltip("演出開始（delayAfterControlStartSeconds）の何秒前から JSON 敵出現を抑止するか")]
    private float enemySpawnSuppressLeadSeconds = 5f;

    [SerializeField, Tooltip("Player 子 fire のアルファ 0→80% に要する秒数")]
    private float playerFireFadeSeconds = 2f;

    [SerializeField, Tooltip("FireScreen オーバーレイ 0→100% に要する秒数（Player fire とは独立）")]
    private float fireScreenFadeSeconds = 2f;

    [SerializeField, Tooltip("フェーズ1開始からフェーズ2までの秒数（シーン転換時間）")]
    private float sceneTransitionDurationSeconds = 5f;

    [Header("SE")]
    [SerializeField, Tooltip("炎演出の SE を管理する Game01SeManager。未設定時は無音。")]
    private Game01SeManager game01SeManager;

    [SerializeField, Tooltip("音量 0→ピークまでの秒数（仕様: 線形フェードイン）")]
    private float seFadeInSeconds = 1f;

    [SerializeField, Tooltip("フェーズ2で音量を下げ切る秒数（急峻にするほど短く）")]
    private float seFadeOutSeconds = 0.2f;

    [SerializeField, Tooltip("フェードイン完了時の相対ピーク（0〜1）。実音量は × SoundSettingsManager.GetSeGain01()")]
    private float fireSePeakVolume = 1f;

    [Header("表示")]
    [SerializeField, Tooltip("演出中のみ FireScreen の Sorting Order をこの値にする（前面表示用）")]
    private int overlaySortingOrderDuringEffect = 200;

    [Header("参照")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    [SerializeField] private Sprite playerFireSprite;
    [SerializeField] private GameObject fireRoot;
    [SerializeField] private SpriteRenderer fireSpriteRenderer;
    [SerializeField] private SpriteRenderer fireScreenSpriteRenderer;
    [SerializeField] private SpriteRenderer backgroundSpriteRenderer;
    [SerializeField] private Sprite gameBg02Sprite;

    [SerializeField, Tooltip("背景スプライト変更後にスケール再計算（未設定時のみシーンから補完）")]
    private FullHDBackgroundFitter backgroundFitter;

    private bool sequenceLaunched;
    private bool enemySpawnSuppressWindowEnded;
    private Sprite cachedPlayerSprite;
    private Color cachedFireBaseColor;
    private Color cachedFireScreenBaseColor;
    private int cachedFireScreenSortingOrder;

    private bool fireSePlaying;

    /// <summary>GameManager などから T0 直後に1回だけ呼ぶ。</summary>
    public void OnGameplayControlStarted(MonoBehaviour coroutineHost)
    {
        if (sequenceLaunched || coroutineHost == null)
        {
            return;
        }

        sequenceLaunched = true;
        enemySpawnSuppressWindowEnded = false;
        coroutineHost.StartCoroutine(RunSequence());
    }

    public bool IsSuppressingEnemySpawns(float gameplayElapsedSinceT0Seconds)
    {
        if (!sequenceLaunched || enemySpawnSuppressWindowEnded)
        {
            return false;
        }

        float start = Mathf.Max(0f, delayAfterControlStartSeconds - Mathf.Max(0f, enemySpawnSuppressLeadSeconds));
        return gameplayElapsedSinceT0Seconds >= start;
    }

    private void EndEnemySpawnSuppressWindow()
    {
        enemySpawnSuppressWindowEnded = true;
    }

    private IEnumerator RunSequence()
    {
        if (delayAfterControlStartSeconds > 0f)
        {
            yield return new WaitForSeconds(delayAfterControlStartSeconds);
        }

        if (!ResolveReferences())
        {
            EndEnemySpawnSuppressWindow();
            yield break;
        }

        if (playerFireSprite == null)
        {
            Debug.LogWarning("[FireScreenController] playerFireSprite が未設定のため中止します。", this);
            EndEnemySpawnSuppressWindow();
            yield break;
        }

        cachedPlayerSprite = playerSpriteRenderer.sprite;
        cachedFireBaseColor = fireSpriteRenderer.color;
        cachedFireScreenBaseColor = fireScreenSpriteRenderer.color;
        cachedFireScreenSortingOrder = fireScreenSpriteRenderer.sortingOrder;

        var overlayPeak = new Color(
            cachedFireScreenBaseColor.r,
            cachedFireScreenBaseColor.g,
            cachedFireScreenBaseColor.b,
            1f);

        float phase1StartTime = Time.time;

        gameObject.SetActive(true);

        playerSpriteRenderer.sprite = playerFireSprite;

        fireRoot.SetActive(true);
        var fireFrom = new Color(cachedFireBaseColor.r, cachedFireBaseColor.g, cachedFireBaseColor.b, 0f);
        var fireTo = new Color(cachedFireBaseColor.r, cachedFireBaseColor.g, cachedFireBaseColor.b, PlayerFireTargetAlpha);
        fireSpriteRenderer.color = fireFrom;

        var overlayFrom = new Color(overlayPeak.r, overlayPeak.g, overlayPeak.b, 0f);
        fireScreenSpriteRenderer.color = overlayFrom;
        fireScreenSpriteRenderer.sortingOrder = overlaySortingOrderDuringEffect;

        BeginFireSeIfNeeded();

        float playerFade = Mathf.Max(0.0001f, playerFireFadeSeconds);
        float screenFade = Mathf.Max(0.0001f, fireScreenFadeSeconds);
        float seFadeIn = Mathf.Max(0.0001f, seFadeInSeconds);
        float fadeWindow = Mathf.Max(playerFade, screenFade);
        if (game01SeManager != null)
        {
            fadeWindow = Mathf.Max(fadeWindow, seFadeIn);
        }

        float elapsedFade = 0f;

        while (elapsedFade < fadeWindow)
        {
            elapsedFade += Time.deltaTime;
            float ft = Mathf.Clamp01(elapsedFade / playerFade);
            float st = Mathf.Clamp01(elapsedFade / screenFade);
            fireSpriteRenderer.color = Color.Lerp(fireFrom, fireTo, ft);
            fireScreenSpriteRenderer.color = Color.Lerp(overlayFrom, overlayPeak, st);
            if (fireSePlaying)
            {
                float sv = Mathf.Clamp01(elapsedFade / seFadeIn);
                game01SeManager?.SetFireLoopSeVolume01(sv * fireSePeakVolume);
            }

            yield return null;
        }

        fireSpriteRenderer.color = fireTo;
        fireScreenSpriteRenderer.color = overlayPeak;
        if (fireSePlaying)
        {
            game01SeManager?.SetFireLoopSeVolume01(fireSePeakVolume);
        }

        float elapsedSincePhase1 = Time.time - phase1StartTime;
        float waitRemain = sceneTransitionDurationSeconds - elapsedSincePhase1;
        float waitEndTime = Time.time + waitRemain;
        while (Time.time < waitEndTime)
        {
            if (fireSePlaying)
            {
                game01SeManager?.SetFireLoopSeVolume01(fireSePeakVolume);
            }

            yield return null;
        }

        if (backgroundSpriteRenderer != null && gameBg02Sprite != null)
        {
            backgroundSpriteRenderer.sprite = gameBg02Sprite;
            if (backgroundFitter != null)
            {
                backgroundFitter.RefreshBackgroundFitAfterSpriteChange();
            }
        }

        yield return FadeOutAndStopFireSe();

        fireScreenSpriteRenderer.color = cachedFireScreenBaseColor;
        fireScreenSpriteRenderer.sortingOrder = cachedFireScreenSortingOrder;

        gameObject.SetActive(false);

        RestorePlayerSpriteAfterFireSequence();

        fireRoot.SetActive(false);
        fireSpriteRenderer.color = cachedFireBaseColor;

        EndEnemySpawnSuppressWindow();
    }

    private void BeginFireSeIfNeeded()
    {
        fireSePlaying = false;
        if (game01SeManager == null)
        {
            return;
        }

        game01SeManager.PlayFireLoopSe();
        game01SeManager.SetFireLoopSeVolume01(0f);
        fireSePlaying = true;
    }

    private void RestorePlayerSpriteAfterFireSequence()
    {
        if (playerSpriteRenderer == null)
        {
            return;
        }

        Game01PlayerLife life = playerSpriteRenderer.GetComponent<Game01PlayerLife>();
        if (life != null)
        {
            life.RestoreNormalSpriteAfterFireScreenEffect();
        }
        else
        {
            playerSpriteRenderer.sprite = cachedPlayerSprite;
        }
    }

    private IEnumerator FadeOutAndStopFireSe()
    {
        if (!fireSePlaying)
        {
            yield break;
        }

        float startVol = fireSePeakVolume;
        float outDur = Mathf.Max(0.0001f, seFadeOutSeconds);
        float t = 0f;
        while (t < outDur)
        {
            t += Time.deltaTime;
            float current = Mathf.Lerp(startVol, 0f, Mathf.Clamp01(t / outDur));
            game01SeManager?.SetFireLoopSeVolume01(current);
            yield return null;
        }

        game01SeManager?.StopFireLoopSe();
        fireSePlaying = false;
    }

    private bool ResolveReferences()
    {
        if (fireSpriteRenderer == null && fireRoot != null)
        {
            fireSpriteRenderer = fireRoot.GetComponent<SpriteRenderer>();
        }

        if (fireScreenSpriteRenderer == null)
        {
            fireScreenSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (backgroundFitter == null)
        {
            backgroundFitter = FindAnyObjectByType<FullHDBackgroundFitter>();
        }

        if (playerSpriteRenderer == null || fireRoot == null || fireSpriteRenderer == null || fireScreenSpriteRenderer == null)
        {
            Debug.LogWarning("[FireScreenController] 必須参照が不足しています。", this);
            return false;
        }

        return true;
    }

}
