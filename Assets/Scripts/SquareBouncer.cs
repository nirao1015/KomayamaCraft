using UnityEngine;

/// <summary>
/// Squareをランダム方向に移動し、画面端で反射させます。
/// スプライトの一部が画面外に出たら即反射し、常に画面内へ収めます。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SquareBouncer : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.5f;

    private Camera mainCamera;
    private SpriteRenderer spriteRenderer;
    private Vector2 velocity;

    private void Awake()
    {
        mainCamera = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        SetRandomDirection();
    }

    private void Update()
    {
        if (mainCamera == null || spriteRenderer == null)
        {
            return;
        }

        if (PlayerLife.Instance != null && PlayerLife.Instance.IsGameOver)
        {
            return;
        }

        Vector3 position = transform.position;
        position += (Vector3)(velocity * Time.deltaTime);

        Vector2 extents = spriteRenderer.bounds.extents;
        Vector3 camPos = mainCamera.transform.position;
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;

        float left = camPos.x - halfWidth;
        float right = camPos.x + halfWidth;
        float bottom = camPos.y - halfHeight;
        float top = camPos.y + halfHeight;

        bool reflectX = false;
        bool reflectY = false;

        if (position.x - extents.x < left)
        {
            position.x = left + extents.x;
            reflectX = true;
        }
        else if (position.x + extents.x > right)
        {
            position.x = right - extents.x;
            reflectX = true;
        }

        if (position.y - extents.y < bottom)
        {
            position.y = bottom + extents.y;
            reflectY = true;
        }
        else if (position.y + extents.y > top)
        {
            position.y = top - extents.y;
            reflectY = true;
        }

        if (reflectX)
        {
            velocity.x *= -1f;
        }

        if (reflectY)
        {
            velocity.y *= -1f;
        }

        transform.position = position;
    }

    private void SetRandomDirection()
    {
        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.right;
        }

        velocity = direction * moveSpeed;
    }
}

