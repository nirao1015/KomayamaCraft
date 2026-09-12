using UnityEngine;

/// <summary>
/// Enemy 共通で使用するプレイヤー位置サンプラー。
/// 0.5秒ごとにプレイヤー位置を記録し、Enemy12 が参照する。
/// </summary>
[DisallowMultipleComponent]
public class EnemyPlayerPositionSampler : MonoBehaviour
{
    [SerializeField] private float sampleIntervalSeconds = 0.5f;

    private static EnemyPlayerPositionSampler instance;

    private Transform playerTransform;
    private float sampleTimer;
    private bool hasSample;
    private Vector2 latestSampledPosition;

    public static EnemyPlayerPositionSampler Instance => instance;

    public static EnemyPlayerPositionSampler EnsureExists()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindAnyObjectByType<EnemyPlayerPositionSampler>();
        if (instance != null)
        {
            return instance;
        }

        GameObject samplerObject = new GameObject("EnemyPlayerPositionSampler");
        instance = samplerObject.AddComponent<EnemyPlayerPositionSampler>();
        return instance;
    }

    public static Vector2 GetLatestPositionOr(Vector2 fallback)
    {
        EnemyPlayerPositionSampler sampler = EnsureExists();
        if (sampler == null)
        {
            return fallback;
        }

        return sampler.hasSample ? sampler.latestSampledPosition : fallback;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        sampleTimer = 0f;
        TryResolvePlayerTransform();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            return;
        }

        if (PlayerLife.Instance != null && PlayerLife.Instance.IsGameOver)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsStageCleared)
        {
            return;
        }

        if (!TryResolvePlayerTransform())
        {
            return;
        }

        if (!hasSample)
        {
            latestSampledPosition = playerTransform.position;
            hasSample = true;
        }

        float interval = Mathf.Max(0.01f, sampleIntervalSeconds);
        sampleTimer += Time.deltaTime;
        if (sampleTimer < interval)
        {
            return;
        }

        while (sampleTimer >= interval)
        {
            sampleTimer -= interval;
        }

        latestSampledPosition = playerTransform.position;
    }

    private bool TryResolvePlayerTransform()
    {
        if (playerTransform != null)
        {
            return true;
        }

        if (PlayerLife.Instance != null)
        {
            playerTransform = PlayerLife.Instance.transform;
            return true;
        }

        GameObject playerObject = GameObject.Find("Player");
        if (playerObject == null)
        {
            return false;
        }

        playerTransform = playerObject.transform;
        return true;
    }
}
