using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 海上の風筋。Layer_Sea の発生タイルから左→右へ短く滑らせる。同時最大 1。
    /// 出現時フェードイン、寿命後半でフェードアウト。速度は一定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaOceanWindAmbience : KomayamaMapAmbienceMapChannel
    {
        [Header("参照")]
        [SerializeField] private KomayamaMapAmbienceController controller;
        [SerializeField] private SpriteRenderer windVisual;
        [SerializeField] private Sprite[] windSprites;
        [SerializeField, Tooltip("発生開始: Layer_Sea の Region_X-1_Y2〜Y6 など")]
        private Transform[] spawnOrigins;

        [Header("寿命・移動（ゲーム秒）")]
        [SerializeField, Min(0.1f)] private float lifeSecondsMin = 1.5f;
        [SerializeField, Min(0.1f)] private float lifeSecondsMax = 3f;
        [SerializeField, Range(0.05f, 2f), Tooltip("移動距離＝スプライト幅 × この倍率（短め＝ゆっくり）")]
        private float travelWidthMin = 0.25f;
        [SerializeField, Range(0.05f, 2f)] private float travelWidthMax = 0.45f;

        [Header("出現間隔（ゲーム秒）")]
        [SerializeField, Min(0f)] private float respawnWaitMin = 4f;
        [SerializeField, Min(0f)] private float respawnWaitRandom = 8f;
        [SerializeField, Tooltip("ON: 終了直後に再出現（テスト用。本番は OFF）")]
        private bool debugImmediateRespawn;

        [Header("フェード")]
        [SerializeField, Range(0.05f, 0.5f), Tooltip("寿命の先頭何割でフェードイン完了")]
        private float fadeInLifeRatio = 0.25f;

        private float waitRemaining;
        private bool active;
        private float lifeElapsed;
        private float lifeDuration;
        private float moveSpeed;
        private Vector3 baseLocalScale = Vector3.one;
        private Color baseColor = Color.white;
        private bool hasBase;

        public override bool WantsStart =>
            !active &&
            waitRemaining <= 0f &&
            windVisual != null &&
            windSprites != null &&
            windSprites.Length > 0 &&
            hasBase &&
            HasAnySpawnOrigin();

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<KomayamaMapAmbienceController>();
            }

            CaptureBaseFromVisual();
            if (windVisual != null)
            {
                windVisual.gameObject.SetActive(false);
            }

            waitRemaining = respawnWaitMin + Random.Range(0f, respawnWaitRandom);
        }

        private void CaptureBaseFromVisual()
        {
            if (windVisual == null)
            {
                hasBase = false;
                return;
            }

            baseLocalScale = windVisual.transform.localScale;
            baseColor = windVisual.color;
            hasBase = true;
        }

        private bool HasAnySpawnOrigin()
        {
            if (spawnOrigins == null || spawnOrigins.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < spawnOrigins.Length; i++)
            {
                if (spawnOrigins[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private Transform PickSpawnOrigin()
        {
            if (spawnOrigins == null || spawnOrigins.Length == 0)
            {
                return null;
            }

            int valid = 0;
            for (int i = 0; i < spawnOrigins.Length; i++)
            {
                if (spawnOrigins[i] != null)
                {
                    valid++;
                }
            }

            if (valid == 0)
            {
                return null;
            }

            int pick = Random.Range(0, valid);
            for (int i = 0; i < spawnOrigins.Length; i++)
            {
                if (spawnOrigins[i] == null)
                {
                    continue;
                }

                if (pick == 0)
                {
                    return spawnOrigins[i];
                }

                pick--;
            }

            return null;
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
            if (!WantsStart)
            {
                return false;
            }

            Transform origin = PickSpawnOrigin();
            Sprite sprite = windSprites[Random.Range(0, windSprites.Length)];
            if (origin == null || sprite == null)
            {
                return false;
            }

            lifeDuration = Random.Range(
                Mathf.Min(lifeSecondsMin, lifeSecondsMax),
                Mathf.Max(lifeSecondsMin, lifeSecondsMax));
            float widthMul = Random.Range(
                Mathf.Min(travelWidthMin, travelWidthMax),
                Mathf.Max(travelWidthMin, travelWidthMax));

            Transform t = windVisual.transform;
            t.localScale = baseLocalScale;
            windVisual.sprite = sprite;

            float halfW = sprite.bounds.extents.x * Mathf.Abs(baseLocalScale.x);
            float travel = halfW * 2f * widthMul;
            moveSpeed = travel / Mathf.Max(0.1f, lifeDuration);

            // タイル中心から右へ滑る（発生開始＝Region 位置）
            Vector3 originPos = origin.position;
            t.position = new Vector3(originPos.x, originPos.y, t.position.z);

            Color c = baseColor;
            c.a = 0f;
            windVisual.color = c;
            windVisual.gameObject.SetActive(true);

            lifeElapsed = 0f;
            active = true;
            return true;
        }

        private void Update()
        {
            if (!active || windVisual == null)
            {
                return;
            }

            if (KomayamaGameClock.ResolveIsPaused() || KomayamaCraftLoadGate.HoldGameTime)
            {
                return;
            }

            float dt = Time.deltaTime;
            lifeElapsed += dt;
            windVisual.transform.position += Vector3.right * (moveSpeed * dt);
            ApplyFade();

            if (lifeElapsed >= lifeDuration)
            {
                FinishWind();
            }
        }

        private void ApplyFade()
        {
            float t = lifeDuration > 0.001f
                ? Mathf.Clamp01(lifeElapsed / lifeDuration)
                : 1f;
            float a = baseColor.a;

            float fadeInEnd = Mathf.Clamp(fadeInLifeRatio, 0.05f, 0.49f);
            if (t < fadeInEnd)
            {
                a *= t / fadeInEnd;
            }
            else if (t >= 0.5f)
            {
                a *= 1f - ((t - 0.5f) / 0.5f);
            }

            Color c = baseColor;
            c.a = Mathf.Clamp01(a);
            windVisual.color = c;
        }

        private void FinishWind()
        {
            if (windVisual != null)
            {
                windVisual.transform.localScale = baseLocalScale;
                windVisual.color = baseColor;
                windVisual.gameObject.SetActive(false);
            }

            active = false;
            if (controller != null)
            {
                controller.ReleaseCost(Cost);
            }

            if (debugImmediateRespawn)
            {
                waitRemaining = 0f;
                if (controller != null && controller.TryReserveCost(Cost))
                {
                    if (!TryStartAmbience())
                    {
                        controller.ReleaseCost(Cost);
                    }
                }

                return;
            }

            waitRemaining = respawnWaitMin + Random.Range(0f, respawnWaitRandom);
        }

        private void OnDisable()
        {
            if (active)
            {
                FinishWind();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (windVisual != null && !Application.isPlaying)
            {
                CaptureBaseFromVisual();
            }
        }
#endif
    }
}
