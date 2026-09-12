using System.Collections;
using UnityEngine;

/// <summary>
/// 仕様: spec/city_screen_midgame_sequence.md（SE・プレイヤー差し替えなし）。
/// T0 後に待機し、CityScreen フェード + Cloud 上昇、シーン転換時間後に背景差し替え→CityScreen オフ。
/// </summary>
[DisallowMultipleComponent]
public class CityScreenController : MonoBehaviour, IGame01MidgameBackgroundEnemySpawnSuppressor
{
    [Header("タイミング")]
    [SerializeField, Tooltip("T0（操作開始）から City 演出（City-B）開始までの秒数")]
    private float delayAfterControlStartSeconds = 2f;

    [SerializeField, Tooltip("演出開始（delayAfterControlStartSeconds）の何秒前から JSON 敵出現を抑止するか")]
    private float enemySpawnSuppressLeadSeconds = 5f;

    [SerializeField, Tooltip("CityScreen オーバーレイ 0→100% に要する秒数")]
    private float cityScreenFadeSeconds = 1.5f;

    [SerializeField, Tooltip("City-B 開始（CityScreen 有効化）から背景差し替え（①）までの秒数")]
    private float sceneTransitionDurationSeconds = 3f;

    [SerializeField, Tooltip("T0 からこの秒数を超えたら演出を強制中断してクリーンアップ（フェーズ5開始前の安全策）")]
    private float sequenceAbortDeadlineFromT0Seconds = 132f;

    [Header("Cloud")]
    [SerializeField, Tooltip("CityScreen 有効化と同時に、ワールド座標で毎秒上方向へ移動する速さ")]
    private float cloudMoveSpeedWorldPerSecond = 12f;

    [SerializeField, Tooltip("未設定時は子の CityScreenCloud の Transform")]
    private Transform cityScreenCloudTransform;

    [Header("表示")]
    [SerializeField, Tooltip("演出中のみ CityScreen の Sorting Order をこの値にする（未使用ならキャッシュ値のまま）")]
    private int overlaySortingOrderDuringEffect = 200;

    [Header("参照")]
    [SerializeField, Tooltip("未設定時はこの GameObject の SpriteRenderer")]
    private SpriteRenderer cityOverlaySpriteRenderer;

    [SerializeField] private SpriteRenderer backgroundSpriteRenderer;
    [SerializeField] private Sprite gameBg03Sprite;
    [SerializeField] private FullHDBackgroundFitter backgroundFitter;

    private bool sequenceLaunched;
    private bool enemySpawnSuppressWindowEnded;
    private bool cityBackgroundSwapped;
    private float cityBStartTime;
    private Color cachedOverlayBaseColor;
    private int cachedOverlaySortingOrder;
    private Vector3 cachedCloudLocalPosition;
    private bool cloudCacheValid;

    /// <summary>GameManager などから T0 直後に1回だけ呼ぶ（Fire と同経路）。</summary>
    public void OnGameplayControlStarted(MonoBehaviour coroutineHost)
    {
        if (sequenceLaunched || coroutineHost == null)
        {
            return;
        }

        sequenceLaunched = true;
        enemySpawnSuppressWindowEnded = false;
        coroutineHost.StartCoroutine(RunSequence(coroutineHost));
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

    private IEnumerator RunSequence(MonoBehaviour host)
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

        cachedOverlayBaseColor = cityOverlaySpriteRenderer.color;
        cachedOverlaySortingOrder = cityOverlaySpriteRenderer.sortingOrder;
        if (cityScreenCloudTransform != null)
        {
            cachedCloudLocalPosition = cityScreenCloudTransform.localPosition;
            cloudCacheValid = true;
        }

        cityBStartTime = Time.time;
        cityBackgroundSwapped = false;
        gameObject.SetActive(true);

        var overlayPeak = new Color(
            cachedOverlayBaseColor.r,
            cachedOverlayBaseColor.g,
            cachedOverlayBaseColor.b,
            1f);
        var overlayFrom = new Color(overlayPeak.r, overlayPeak.g, overlayPeak.b, 0f);
        cityOverlaySpriteRenderer.color = overlayFrom;
        cityOverlaySpriteRenderer.sortingOrder = overlaySortingOrderDuringEffect;

        float fadeDur = Mathf.Max(0.0001f, cityScreenFadeSeconds);
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeDur)
        {
            if (ShouldAbortSequence(host))
            {
                yield return CleanupAfterAbort();
                yield break;
            }

            fadeElapsed += Time.deltaTime;
            float u = Mathf.Clamp01(fadeElapsed / fadeDur);
            cityOverlaySpriteRenderer.color = Color.Lerp(overlayFrom, overlayPeak, u);
            MoveCloud(Time.deltaTime);
            TryApplyCityBackgroundSwapIfDue();
            yield return null;
        }

        cityOverlaySpriteRenderer.color = overlayPeak;

        while (Time.time - cityBStartTime < sceneTransitionDurationSeconds)
        {
            if (ShouldAbortSequence(host))
            {
                yield return CleanupAfterAbort();
                yield break;
            }

            MoveCloud(Time.deltaTime);
            TryApplyCityBackgroundSwapIfDue();
            yield return null;
        }

        TryApplyCityBackgroundSwapIfDue();

        cityOverlaySpriteRenderer.color = cachedOverlayBaseColor;
        cityOverlaySpriteRenderer.sortingOrder = cachedOverlaySortingOrder;
        if (cloudCacheValid && cityScreenCloudTransform != null)
        {
            cityScreenCloudTransform.localPosition = cachedCloudLocalPosition;
        }

        gameObject.SetActive(false);
        EndEnemySpawnSuppressWindow();
    }

    private bool ShouldAbortSequence(MonoBehaviour host)
    {
        var gm = Game01Manager.Instance;
        if (gm == null || !gm.HasGameplayControlStarted)
        {
            return false;
        }

        if (gm.GameplayElapsedSinceControlSeconds < sequenceAbortDeadlineFromT0Seconds)
        {
            return false;
        }

        // フェーズ5到達時は背景差し替えを先に済ませてから中止する（ビルドで同フレーム競合すると②が飛ぶ）。
        return cityBackgroundSwapped;
    }

    private void TryApplyCityBackgroundSwapIfDue()
    {
        if (cityBackgroundSwapped)
        {
            return;
        }

        if (Time.time - cityBStartTime < sceneTransitionDurationSeconds)
        {
            return;
        }

        if (backgroundSpriteRenderer == null || gameBg03Sprite == null)
        {
            return;
        }

        backgroundSpriteRenderer.sprite = gameBg03Sprite;
        if (backgroundFitter != null)
        {
            backgroundFitter.RefreshBackgroundFitAfterSpriteChange();
        }

        cityBackgroundSwapped = true;
    }

    private IEnumerator CleanupAfterAbort()
    {
        TryApplyCityBackgroundSwapIfDue();

        if (cityOverlaySpriteRenderer != null)
        {
            cityOverlaySpriteRenderer.color = cachedOverlayBaseColor;
            cityOverlaySpriteRenderer.sortingOrder = cachedOverlaySortingOrder;
        }

        if (cloudCacheValid && cityScreenCloudTransform != null)
        {
            cityScreenCloudTransform.localPosition = cachedCloudLocalPosition;
        }

        gameObject.SetActive(false);
        EndEnemySpawnSuppressWindow();
        yield break;
    }

    private void MoveCloud(float deltaTime)
    {
        if (cityScreenCloudTransform == null || cloudMoveSpeedWorldPerSecond == 0f)
        {
            return;
        }

        cityScreenCloudTransform.position += Vector3.up * (cloudMoveSpeedWorldPerSecond * deltaTime);
    }

    private bool ResolveReferences()
    {
        if (cityOverlaySpriteRenderer == null)
        {
            cityOverlaySpriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (cityScreenCloudTransform == null)
        {
            var t = transform.Find("CityScreenCloud");
            if (t != null)
            {
                cityScreenCloudTransform = t;
            }
        }

        if (backgroundFitter == null)
        {
            backgroundFitter = FindAnyObjectByType<FullHDBackgroundFitter>();
        }

        if (backgroundSpriteRenderer == null)
        {
            Debug.LogWarning("[CityScreenController] backgroundSpriteRenderer が未設定です。", this);
            return false;
        }

        if (cityOverlaySpriteRenderer == null)
        {
            Debug.LogWarning("[CityScreenController] CityScreen の SpriteRenderer が見つかりません。", this);
            return false;
        }

        if (gameBg03Sprite == null)
        {
            Debug.LogWarning("[CityScreenController] gameBg03Sprite が未設定のため、背景差し替えをスキップします。", this);
        }

        return true;
    }
}
