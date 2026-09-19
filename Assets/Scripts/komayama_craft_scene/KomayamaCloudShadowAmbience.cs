using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 雲影アンビエント。同時最大1。
    /// Layer_Effects に置いた雲オブジェクトの大きさ・色（透明度）をベースにし、出現時は大きさ倍率だけランダム加算する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaCloudShadowAmbience : KomayamaMapAmbienceMapChannel
    {
        [Header("参照")]
        [SerializeField] private KomayamaMapAmbienceController controller;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private SpriteRenderer cloudVisual;
        [SerializeField] private Sprite[] cloudSprites;

        [Header("移動（ワールド単位/秒）")]
        [SerializeField, Min(0.1f)] private float speedMin = 0.8f;
        [SerializeField, Min(0.1f)] private float speedMax = 1.6f;

        [Header("大きさ倍率（ベース Scale に乗算）")]
        [SerializeField, Min(0.1f)] private float scaleMultiplierMin = 1f;
        [SerializeField, Min(0.1f)] private float scaleMultiplierMax = 1.8f;

        [Header("再出現待機（ゲーム秒）")]
        [SerializeField, Min(0f)] private float respawnWaitMin = 60f;
        [SerializeField, Min(0f)] private float respawnWaitRandom = 180f;

        private float waitRemaining;
        private bool active;
        private float moveSpeed;
        private float halfWidthWorld;
        private Vector3 baseLocalScale = Vector3.one;
        private Color baseColor = Color.white;
        private bool hasBase;

        public override bool WantsStart =>
            !active &&
            waitRemaining <= 0f &&
            cloudVisual != null &&
            cloudSprites != null &&
            cloudSprites.Length > 0;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<KomayamaMapAmbienceController>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            CaptureBaseFromVisual();
            if (cloudVisual != null)
            {
                cloudVisual.gameObject.SetActive(false);
            }

            waitRemaining = respawnWaitMin + Random.Range(0f, respawnWaitRandom);
        }

        private void CaptureBaseFromVisual()
        {
            if (cloudVisual == null)
            {
                hasBase = false;
                return;
            }

            baseLocalScale = cloudVisual.transform.localScale;
            baseColor = cloudVisual.color;
            hasBase = true;
        }

        public override void OnAmbienceTick(float tickIntervalSeconds)
        {
            if (active || KomayamaGameClock.ResolveIsPaused())
            {
                return;
            }

            if (waitRemaining > 0f)
            {
                waitRemaining = Mathf.Max(0f, waitRemaining - tickIntervalSeconds);
            }
        }

        public override bool TryStartAmbience()
        {
            if (!WantsStart || targetCamera == null || !hasBase)
            {
                return false;
            }

            Sprite sprite = cloudSprites[Random.Range(0, cloudSprites.Length)];
            if (sprite == null)
            {
                return false;
            }

            float multiplier = Random.Range(scaleMultiplierMin, scaleMultiplierMax);
            moveSpeed = Random.Range(speedMin, speedMax);

            Transform cloudTransform = cloudVisual.transform;
            cloudTransform.localScale = baseLocalScale * multiplier;
            cloudVisual.sprite = sprite;
            cloudVisual.color = baseColor;

            float viewHeight = targetCamera.orthographicSize * 2f;
            float viewWidth = viewHeight * targetCamera.aspect;
            Vector3 camPos = targetCamera.transform.position;

            Vector3 lossy = cloudTransform.lossyScale;
            halfWidthWorld = sprite.bounds.extents.x * Mathf.Abs(lossy.x);
            float halfHeightWorld = sprite.bounds.extents.y * Mathf.Abs(lossy.y);

            float yMin = camPos.y - targetCamera.orthographicSize + halfHeightWorld;
            float yMax = camPos.y + targetCamera.orthographicSize - halfHeightWorld;
            if (yMax < yMin)
            {
                yMin = yMax = camPos.y;
            }

            float x = camPos.x - viewWidth * 0.5f - halfWidthWorld - 0.5f;
            float y = Random.Range(yMin, yMax);
            cloudTransform.position = new Vector3(x, y, cloudTransform.position.z);

            cloudVisual.gameObject.SetActive(true);
            active = true;
            return true;
        }

        private void Update()
        {
            if (!active || cloudVisual == null || targetCamera == null)
            {
                return;
            }

            if (KomayamaGameClock.ResolveIsPaused())
            {
                return;
            }

            cloudVisual.transform.position += Vector3.right * (moveSpeed * Time.deltaTime);

            float viewWidth = targetCamera.orthographicSize * 2f * targetCamera.aspect;
            float rightEdge = targetCamera.transform.position.x + viewWidth * 0.5f;
            float cloudLeft = cloudVisual.transform.position.x - halfWidthWorld;
            if (cloudLeft > rightEdge + 0.5f)
            {
                FinishCloud();
            }
        }

        private void FinishCloud()
        {
            if (cloudVisual != null)
            {
                cloudVisual.transform.localScale = baseLocalScale;
                cloudVisual.color = baseColor;
                cloudVisual.gameObject.SetActive(false);
            }

            active = false;
            waitRemaining = respawnWaitMin + Random.Range(0f, respawnWaitRandom);
            if (controller != null)
            {
                controller.ReleaseCost(Cost);
            }
        }

        private void OnDisable()
        {
            if (active)
            {
                FinishCloud();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (cloudVisual != null && !Application.isPlaying)
            {
                CaptureBaseFromVisual();
            }
        }
#endif
    }
}
