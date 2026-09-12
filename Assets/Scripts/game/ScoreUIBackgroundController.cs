using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ScoreUIBackground にアタッチし、5秒ごとのスコア加算と ScoreUI 表示更新を行う。
/// </summary>
[DisallowMultipleComponent]
public class ScoreUIBackgroundController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI scoreUI;
    [SerializeField] private TextMeshProUGUI scoreAddUI;

    [Header("References")]
    [SerializeField] private Game01Manager gameManager;
    [SerializeField] private Game01PlayerLife playerLife;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Game01EffectManager game01EffectManager;

    [Header("Score Rule")]
    [SerializeField] private float addIntervalSeconds = 5f;
    [SerializeField] private int nearScore = 10;
    [SerializeField] private int farScore = 100;

    [Header("Distance Threshold (Inspector Tuning)")]
    [SerializeField] private float nearDistance = 1.5f;
    [SerializeField] private float farDistance = 8f;

    [Header("Behavior")]
    [SerializeField] private bool stopScoringWhenStageCleared = true;
    private const float DistanceMinGap = 0.0001f;

    private long currentScore;
    private float addTimer;
    private bool hasStartedCount;
    private Vector2 lastValidCursorWorldPosition;
    private bool hasCursorEnteredScreen;
    private void Awake()
    {
        ResolveReferences();
        ResetScore();
    }

    private void Update()
    {
        ResolveReferences();
        UpdateCursorCache();

        if (!hasStartedCount)
        {
            if (!IsControlStarted())
            {
                return;
            }

            hasStartedCount = true;
            addTimer = 0f;
            ResetScore();
        }

        if (IsScoringStopped())
        {
            return;
        }

        float interval = Mathf.Max(0.01f, addIntervalSeconds);
        addTimer += Time.deltaTime;
        while (addTimer >= interval)
        {
            addTimer -= interval;
            AddScoreByCurrentState();
        }
    }

    private void ResolveReferences()
    {
        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main;
        }
    }

    private void ResetScore()
    {
        currentScore = 0;
        UpdateScoreUI();
        ClearScoreAddUI();
    }

    private bool IsControlStarted()
    {
        if (gameManager == null)
        {
            return true;
        }

        return !gameManager.WaitForStageIntro || gameManager.IsStageIntroComplete;
    }

    private bool IsScoringStopped()
    {
        if (playerLife != null && playerLife.IsGameOver)
        {
            return true;
        }

        if (stopScoringWhenStageCleared && gameManager != null && gameManager.IsStageCleared)
        {
            return true;
        }

        return false;
    }

    private void UpdateCursorCache()
    {
        if (gameplayCamera == null || Mouse.current == null)
        {
            return;
        }

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 viewport = gameplayCamera.ScreenToViewportPoint(mouseScreenPosition);
        bool isInsideScreen = viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        if (!isInsideScreen)
        {
            return;
        }

        lastValidCursorWorldPosition = gameplayCamera.ScreenToWorldPoint(mouseScreenPosition);
        hasCursorEnteredScreen = true;
    }

    private void AddScoreByCurrentState()
    {
        int lives = playerLife != null ? Mathf.Max(0, playerLife.CurrentLives) : 0;
        if (lives <= 0)
        {
            return;
        }

        int positionScore = CalculatePositionScore();
        int addScore = lives * positionScore;
        currentScore += addScore;
        UpdateScoreUI();
        ShowScoreAddUI(addScore);
    }

    private int CalculatePositionScore()
    {
        if (playerTransform == null)
        {
            return Mathf.Clamp(nearScore, 0, farScore);
        }

        Vector2 playerPosition = playerTransform.position;
        Vector2 cursorPosition = hasCursorEnteredScreen ? lastValidCursorWorldPosition : playerPosition;
        float distance = Vector2.Distance(playerPosition, cursorPosition);

        float safeNearDistance = Mathf.Max(0f, nearDistance);
        float safeFarDistance = Mathf.Max(safeNearDistance + DistanceMinGap, farDistance);

        int safeNearScore = Mathf.Min(nearScore, farScore);
        int safeFarScore = Mathf.Max(nearScore, farScore);

        if (distance <= safeNearDistance)
        {
            return safeNearScore;
        }

        if (distance >= safeFarDistance)
        {
            return safeFarScore;
        }

        float t = Mathf.InverseLerp(safeNearDistance, safeFarDistance, distance);
        float rawScore = Mathf.Lerp(safeNearScore, safeFarScore, t);
        return Mathf.FloorToInt(rawScore);
    }

    private void UpdateScoreUI()
    {
        if (scoreUI == null)
        {
            return;
        }

        scoreUI.text = currentScore.ToString("#,0", CultureInfo.InvariantCulture);
    }

    private void ClearScoreAddUI()
    {
        if (scoreAddUI == null)
        {
            return;
        }

        scoreAddUI.text = string.Empty;
        Color color = scoreAddUI.color;
        color.a = 0f;
        scoreAddUI.color = color;
        if (scoreAddUI.gameObject.activeSelf)
        {
            scoreAddUI.gameObject.SetActive(false);
        }
    }

    private void ShowScoreAddUI(int addScore)
    {
        if (scoreAddUI == null || addScore <= 0)
        {
            return;
        }

        if (game01EffectManager == null)
        {
            return;
        }

        game01EffectManager.PlayScoreAddUI(scoreAddUI, addScore);
    }
}
