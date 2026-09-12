using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// menu03 テスト用 OP 演出。<see cref="GameObject"/> は OpOj に付与し、参照は Inspector で繋ぐ。
/// OpOj が非アクティブのときは何もしない。アクティブ時はマウス／タッチのクリックで開始。
/// </summary>
[DisallowMultipleComponent]
public sealed class Menu03OpPresentationController : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private RectTransform oj01Root;
    [SerializeField] private RectTransform opOj2Root;
    [SerializeField, Tooltip("OpOj3（シーンでは Oj03）")]
    private RectTransform opOj3Root;

    [Header("Oj01 — Images")]
    [SerializeField, Tooltip("Oj01 直下の Image（番号なし・背景など）")]
    private RectTransform oj01BackdropImage;
    [SerializeField] private RectTransform oj01Image0;
    [SerializeField] private RectTransform oj01Image1;
    [SerializeField] private RectTransform oj01Image2;
    [SerializeField] private RectTransform oj01Image3;

    [Header("OpOj2 (Oj02) — tween targets")]
    [SerializeField] private RectTransform opOj2Image0;
    [SerializeField, Tooltip("OpOj2 内の SandImage（全景など）")]
    private RectTransform opOj2SandImage;
    [SerializeField] private RectTransform opOj2Image2;
    [SerializeField] private RectTransform opOj2Image3;

    [Header("OpOj3 (Oj03)")]
    [SerializeField] private RectTransform opOj3Title;
    [SerializeField] private RectTransform opOj3KusaImage;
    [SerializeField] private RectTransform opOj3KusaKageImage;
    [SerializeField, Tooltip("null のとき Kusa の親。通常は Oj03 内の全画面 Image")]
    private RectTransform opOj3KusaMovementArea;
    [SerializeField] private float opOj3KusaMoveSpeed = 260f;
    [SerializeField] private float opOj3KusaBounceMaxHeight = 70f;
    [SerializeField] private bool opOj3InvertKusaRotation = true;
    [SerializeField, Tooltip("横移動・バウンド・影は開始直後から。草本体の回転だけこの秒数（unscaled）経過後に開始する。")]
    private float opOj3KusaRotationDelaySeconds = 0.8f;

    [Header("Timing (unscaled)")]
    [SerializeField] private float waitAfterOj01Show = 0.3f;
    [SerializeField] private float waitAfterImage1 = 0.8f;
    [SerializeField] private float waitAfterImage2 = 0.8f;
    [SerializeField] private float waitAfterImage3 = 0.8f;
    [SerializeField] private float waitBeforeOpOj2 = 2.2f;

    [SerializeField] private float opOj2Image0MoveSeconds = 40f;
    [SerializeField] private Vector2 opOj2Image0TargetAnchoredPosition = new Vector2(358f, 318f);

    [SerializeField] private float opOj2SandImageMoveSeconds = 40f;
    [SerializeField] private float opOj2SandImageTargetAnchoredX = -1800f;

    [SerializeField] private float opOj2Image2MoveScaleSeconds = 0.8f;
    [SerializeField] private Vector2 opOj2Image2TargetAnchoredPosition = new Vector2(382f, -274f);
    [SerializeField] private float opOj2Image2TargetUniformScale = 1.5f;

    [SerializeField] private float opOj2RootMoveSeconds = 1.2f;
    [SerializeField] private float opOj2RootTargetAnchoredX = 558f;

    [SerializeField] private float opOj2Image3MoveSeconds = 1.2f;
    [SerializeField] private Vector2 opOj2Image3TargetAnchoredPosition = new Vector2(-787f, -54f);

    [SerializeField] private float waitBeforeOpOj3 = 0.8f;
    [SerializeField] private float waitBeforeOpOj3Title = 0.8f;

    [Header("Boot")]
    [SerializeField, Tooltip("シーン開始時に OpOj2 ルートを非表示にしておく")]
    private bool hideOpOj2UntilReveal = true;
    [SerializeField, Tooltip("シーン開始時に OpOj3（Oj03）ルートを非表示にしておく")]
    private bool hideOpOj3UntilReveal = true;

    private bool sequenceConsumed;

    private readonly Vector3[] worldCornersScratch = new Vector3[4];

    private void Awake()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (hideOpOj2UntilReveal && opOj2Root != null)
        {
            opOj2Root.gameObject.SetActive(false);
        }

        if (hideOpOj3UntilReveal && opOj3Root != null)
        {
            opOj3Root.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (sequenceConsumed || !isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (TryConsumeClick())
        {
            sequenceConsumed = true;
            StartCoroutine(RunSequence());
        }
    }

    private static bool TryConsumeClick()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        Touchscreen ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }

        return false;
#else
        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }

        if (Input.touchCount <= 0)
        {
            return false;
        }

        Touch t = Input.GetTouch(0);
        return t.phase == TouchPhase.Began;
#endif
    }

    private IEnumerator RunSequence()
    {
        if (oj01Root != null)
        {
            oj01Root.gameObject.SetActive(true);
        }

        SetActiveSafe(oj01BackdropImage, true);
        SetActiveSafe(oj01Image0, true);
        SetActiveSafe(oj01Image1, false);
        SetActiveSafe(oj01Image2, false);
        SetActiveSafe(oj01Image3, false);

        yield return WaitUnscaled(waitAfterOj01Show);

        SetActiveSafe(oj01Image0, false);
        SetActiveSafe(oj01Image1, true);

        yield return WaitUnscaled(waitAfterImage1);

        SetActiveSafe(oj01Image1, false);
        SetActiveSafe(oj01Image2, true);

        yield return WaitUnscaled(waitAfterImage2);

        SetActiveSafe(oj01Image2, false);
        SetActiveSafe(oj01Image3, true);

        yield return WaitUnscaled(waitAfterImage3);

        SetActiveSafe(oj01Image3, false);

        yield return WaitUnscaled(waitBeforeOpOj2);

        if (oj01Root != null)
        {
            oj01Root.gameObject.SetActive(false);
        }

        if (opOj2Root != null)
        {
            opOj2Root.gameObject.SetActive(true);
        }

        // 待ち5 / 待ち9: Image0 と SandImage をそれぞれ40秒（完了は待たない）
        StartCoroutine(RunOpOj2Image0MoveRoutine());
        StartCoroutine(RunOpOj2SandImageMoveRoutine());

        // 待ち6: Image2 は同時スタートだが、こちらは終わるまで待つ
        yield return RunOpOj2Image2TweenRoutine();

        // 待ち7・待ち8: ルート + Image3（長尺トゥイーンとは独立）
        yield return RunOpOj2RootAndImage3Slide();

        // 待ち9: OpOj2 を閉じて OpOj3 を表示（Title はまだ非表示）
        yield return WaitUnscaled(waitBeforeOpOj3);

        SetActiveSafe(opOj2Root, false);
        SetActiveSafe(opOj3Title, false);
        if (opOj3Root != null)
        {
            opOj3Root.gameObject.SetActive(true);
        }

        // 待ち10: Title と Canvas 常時と同じ草ロール（Menu03EffectManager）を同時開始
        yield return WaitUnscaled(waitBeforeOpOj3Title);

        SetActiveSafe(opOj3Title, true);

        if (opOj3KusaImage != null)
        {
            StartCoroutine(RunOpOj3KusaSinglePassRoutine());
        }
    }

    private IEnumerator RunOpOj2Image0MoveRoutine()
    {
        if (opOj2Image0 == null)
        {
            yield break;
        }

        Vector2 start = opOj2Image0.anchoredPosition;
        Vector2 end = opOj2Image0TargetAnchoredPosition;
        float dur = Mathf.Max(0.0001f, opOj2Image0MoveSeconds);

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            opOj2Image0.anchoredPosition = Vector2.LerpUnclamped(start, end, k);
            yield return null;
        }

        opOj2Image0.anchoredPosition = end;
    }

    private IEnumerator RunOpOj2SandImageMoveRoutine()
    {
        if (opOj2SandImage == null)
        {
            yield break;
        }

        Vector2 start = opOj2SandImage.anchoredPosition;
        Vector2 end = new Vector2(opOj2SandImageTargetAnchoredX, start.y);
        float dur = Mathf.Max(0.0001f, opOj2SandImageMoveSeconds);

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            opOj2SandImage.anchoredPosition = Vector2.LerpUnclamped(start, end, k);
            yield return null;
        }

        opOj2SandImage.anchoredPosition = end;
    }

    private IEnumerator RunOpOj2Image2TweenRoutine()
    {
        if (opOj2Image2 == null)
        {
            yield break;
        }

        Vector2 posStart = opOj2Image2.anchoredPosition;
        Vector3 scaleStart = opOj2Image2.localScale;
        Vector3 scaleEnd = Vector3.one * Mathf.Max(0.0001f, opOj2Image2TargetUniformScale);

        float dur = Mathf.Max(0.0001f, opOj2Image2MoveScaleSeconds);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            opOj2Image2.anchoredPosition =
                Vector2.LerpUnclamped(posStart, opOj2Image2TargetAnchoredPosition, k);
            opOj2Image2.localScale = Vector3.LerpUnclamped(scaleStart, scaleEnd, k);
            yield return null;
        }

        opOj2Image2.anchoredPosition = opOj2Image2TargetAnchoredPosition;
        opOj2Image2.localScale = scaleEnd;
    }

    /// <summary>待ち7・待ち8: OpOj2 ルートの X 移動と Image3 の位置移動を同時に行う。</summary>
    private IEnumerator RunOpOj2RootAndImage3Slide()
    {
        if (opOj2Root == null && opOj2Image3 == null)
        {
            yield break;
        }

        Vector2 rootStart = opOj2Root != null ? opOj2Root.anchoredPosition : Vector2.zero;
        Vector2 rootEnd = opOj2Root != null ? new Vector2(opOj2RootTargetAnchoredX, rootStart.y) : Vector2.zero;

        Vector2 img3Start = opOj2Image3 != null ? opOj2Image3.anchoredPosition : Vector2.zero;

        float durRoot = Mathf.Max(0.0001f, opOj2RootMoveSeconds);
        float durImg3 = Mathf.Max(0.0001f, opOj2Image3MoveSeconds);
        float total = Mathf.Max(
            opOj2Root != null ? durRoot : 0f,
            opOj2Image3 != null ? durImg3 : 0f);
        if (total <= 0f)
        {
            yield break;
        }

        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            if (opOj2Root != null)
            {
                float k = Mathf.Clamp01(t / durRoot);
                opOj2Root.anchoredPosition = Vector2.LerpUnclamped(rootStart, rootEnd, k);
            }

            if (opOj2Image3 != null)
            {
                float k = Mathf.Clamp01(t / durImg3);
                opOj2Image3.anchoredPosition =
                    Vector2.LerpUnclamped(img3Start, opOj2Image3TargetAnchoredPosition, k);
            }

            yield return null;
        }

        if (opOj2Root != null)
        {
            opOj2Root.anchoredPosition = rootEnd;
        }

        if (opOj2Image3 != null)
        {
            opOj2Image3.anchoredPosition = opOj2Image3TargetAnchoredPosition;
        }
    }

    /// <summary>
    /// Canvas の <see cref="Menu03EffectManager"/> と同じ移動・バウンド・影の仕様で、Oj03 内の草を左へ1パス転がす。
    /// 回転は <see cref="opOj3KusaRotationDelaySeconds"/> だけ遅らせる（それまでスライドのみ）。
    /// </summary>
    private IEnumerator RunOpOj3KusaSinglePassRoutine()
    {
        RectTransform kusa = opOj3KusaImage;
        RectTransform kage = opOj3KusaKageImage;
        if (kusa == null)
        {
            yield break;
        }

        RectTransform area = opOj3KusaMovementArea != null ? opOj3KusaMovementArea : kusa.parent as RectTransform;

        Vector2 kusaInitialAnchoredPosition = kusa.anchoredPosition;
        float kusaInitialZRotation = kusa.localEulerAngles.z;
        Vector2 kusaKageInitialAnchoredPosition = Vector2.zero;
        float kusaKageInitialZRotation = 0f;
        Vector2 kusaKageOffsetFromKusa = Vector2.zero;
        Vector3 kusaKageInitialScale = Vector3.one;

        if (kage != null)
        {
            kusaKageInitialAnchoredPosition = kage.anchoredPosition;
            kusaKageInitialZRotation = kage.localEulerAngles.z;
            kusaKageOffsetFromKusa = kusaKageInitialAnchoredPosition - kusaInitialAnchoredPosition;
            kusaKageInitialScale = kage.localScale;
        }

        float speed = Mathf.Max(0f, opOj3KusaMoveSpeed);
        float bounceMax = Mathf.Max(0f, opOj3KusaBounceMaxHeight);
        float kusaRotationZ = kusaInitialZRotation;

        float Pulse01(float t, float center, float halfWidth)
        {
            float start = center - halfWidth;
            float end = center + halfWidth;
            if (t <= start || t >= end)
            {
                return 0f;
            }

            float local = (t - start) / Mathf.Max(0.001f, end - start);
            return 4f * local * (1f - local);
        }

        float ComputeBounce(float t01)
        {
            if (bounceMax <= 0f)
            {
                return 0f;
            }

            return bounceMax * (Pulse01(t01, 0.34f, 0.15f) + Pulse01(t01, 0.68f, 0.14f));
        }

        void ApplyPose(float x, float bounceYOffset, float rotationDt, bool advanceRotation)
        {
            Vector2 pos = new Vector2(x, kusaInitialAnchoredPosition.y + bounceYOffset);
            kusa.anchoredPosition = pos;

            if (advanceRotation)
            {
                float rotationSpeed = speed * 0.65f;
                float rotationSign = opOj3InvertKusaRotation ? 1f : -1f;
                kusaRotationZ += rotationSign * rotationSpeed * rotationDt;
            }

            kusa.localRotation = Quaternion.Euler(0f, 0f, kusaRotationZ);

            if (kage != null)
            {
                Vector2 kagePos = new Vector2(pos.x + kusaKageOffsetFromKusa.x, kusaKageInitialAnchoredPosition.y);
                kage.anchoredPosition = kagePos;
                kage.localRotation = Quaternion.Euler(0f, 0f, kusaKageInitialZRotation);

                float maxHeight = Mathf.Max(0.01f, bounceMax);
                float heightRate = Mathf.Clamp01(bounceYOffset / maxHeight);
                float scaleFactor = Mathf.Lerp(1f, 0.72f, heightRate);
                kage.localScale = new Vector3(
                    kusaKageInitialScale.x * scaleFactor,
                    kusaKageInitialScale.y * scaleFactor,
                    kusaKageInitialScale.z);
            }
        }

        void RestoreInitialPose()
        {
            kusaRotationZ = kusaInitialZRotation;
            ApplyPose(kusaInitialAnchoredPosition.x, 0f, 0f, false);
        }

        ApplyPose(kusaInitialAnchoredPosition.x, 0f, 0f, false);

        float rotationStandbyElapsed = 0f;
        float rotationDelay = Mathf.Max(0f, opOj3KusaRotationDelaySeconds);

        while (true)
        {
            float dt = Time.unscaledDeltaTime;
            if (area == null)
            {
                RestoreInitialPose();
                yield break;
            }

            rotationStandbyElapsed += dt;
            bool advanceRotation = rotationStandbyElapsed >= rotationDelay;

            float halfWidth = GetRectWidthInParentLocal(area, kusa) * 0.5f;
            float leftOutsideX = area.rect.xMin - halfWidth;

            Vector2 p = kusa.anchoredPosition;
            p.x -= speed * dt;

            float travelDistance = Mathf.Max(0.01f, kusaInitialAnchoredPosition.x - leftOutsideX);
            float movedDistance = Mathf.Clamp(kusaInitialAnchoredPosition.x - p.x, 0f, travelDistance);
            float progress = Mathf.Clamp01(movedDistance / travelDistance);
            float bounceY = ComputeBounce(progress);
            ApplyPose(p.x, bounceY, dt, advanceRotation);

            if (p.x <= leftOutsideX)
            {
                break;
            }

            yield return null;
        }

        RestoreInitialPose();
    }

    private float GetRectWidthInParentLocal(RectTransform parent, RectTransform target)
    {
        if (parent == null || target == null)
        {
            return 0f;
        }

        target.GetWorldCorners(worldCornersScratch);
        Vector2 p0 = ParentLocalPoint(parent, worldCornersScratch[0]);
        Vector2 min = p0;
        Vector2 max = p0;
        for (int i = 1; i < 4; i++)
        {
            Vector2 p = ParentLocalPoint(parent, worldCornersScratch[i]);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        return Mathf.Abs(max.x - min.x);
    }

    private static Vector2 ParentLocalPoint(RectTransform parent, Vector3 worldPoint)
    {
        Vector3 local = parent.InverseTransformPoint(worldPoint);
        return new Vector2(local.x, local.y);
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float s = Mathf.Max(0f, seconds);
        float t = 0f;
        while (t < s)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static void SetActiveSafe(RectTransform rt, bool active)
    {
        if (rt != null)
        {
            rt.gameObject.SetActive(active);
        }
    }
}
