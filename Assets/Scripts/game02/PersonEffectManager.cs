using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game02
{
    /// <summary>
    /// 演出・雰囲気用パラメータの置き場。猫ボタン用は Inspector「猫の挙動」。異星人（Parson01）カーソル回避は「異星人演出」。
    /// 潰れ: スロットの squashAxis で Y（高さ）または X（幅）に潰す。底辺／左辺／右辺のワールド固定は worldAnchorLock。横: Z シアー。
    /// ParsonL / ParsonR は worldAnchorLock（左辺／右辺）で辺固定＋縦方向の避けを有効化。
    /// 異星人は <see cref="alienParsonSlots"/> で複数ルートを設定可能。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PersonEffectManager : MonoBehaviour
    {
        private enum AlienWorldAnchorLockKind
        {
            BottomEdgeMid = 0,
            LeftEdgeMid = 1,
            RightEdgeMid = 2,
        }

        private enum AlienSquashAxisKind
        {
            Y = 0,
            X = 1,
        }

        [Serializable]
        private sealed class AlienParsonSlot
        {
            [Tooltip("演出対象の親。直下の子 RectTransform に変形を適用する。")]
            public RectTransform parson01Root;

            [Tooltip("距離計算の基準（中心までのピクセル）。未設定なら子 AABB 中心。直下の子なら変形から除外される。")]
            public RectTransform alienDistanceProxy;

            [Tooltip("潰れをかけるローカル軸。Y=高さ方向（Parson 既定）、X=幅方向（ParsonL 向け）。")]
            public AlienSquashAxisKind squashAxis;

            [Tooltip("横傾き（Z）の左右を反転。前面スプライトや Y=180 の子など、既定だとカーソル側へ傾いて見えるときオン。")]
            public bool invertHorizontalShear;

            [Tooltip("歪み適用後にワールドで固定する辺。Left=左辺中点（ParsonL）、Right=右辺中点（ParsonR）。既定は底辺中点。")]
            public AlienWorldAnchorLockKind worldAnchorLock;
        }

        [Header("猫の挙動")]
        [SerializeField, Range(0, 100), Tooltip("拡大になる確率（100 分率）。残りは縮小。")]
        private int enlargeProbabilityPercent = 50;

        [SerializeField, Tooltip("拡大時に CatButton の localScale に乗算する倍率（正の値推奨）。")]
        private float enlargeScaleFactor = 1.05f;

        [SerializeField, Tooltip("縮小時に CatButton の localScale に乗算する倍率（正の値推奨）。")]
        private float shrinkScaleFactor = 0.95f;

        [SerializeField, Tooltip("猫 SE の最短間隔（秒・unscaled）。0 以下はインターバルなし。")]
        private float catSeIntervalSeconds = 0.35f;

        [SerializeField, Tooltip("拡大時のみ表示スケールを目標へ追従させる SmoothDamp の時間（秒・unscaled）。0 以下は即時（従来どおり）。")]
        private float enlargeSmoothSeconds = 0.12f;

        [Header("異星人演出（Parson01）")]
        [SerializeField, Tooltip("対象ルートと距離用 Rect のペア。要素を増やして複数ブロックに適用できる。")]
        private List<AlienParsonSlot> alienParsonSlots = new List<AlienParsonSlot>();

        [SerializeField, Tooltip("PanelCanvas/ParsonObject。activeInHierarchy が false のとき全スロットの本演出を止める。")]
        private RectTransform parsonObjectRoot;

        [SerializeField, Tooltip("このピクセル距離で歪み係数 0。より近いと 1 に近づく（線形）。")]
        private float alienReactionDistancePixels = 280f;

        [SerializeField]
        [FormerlySerializedAs("alienMinScaleX")]
        [Tooltip("最大歪み時の潰れ乗数の下限（1 未満）。スロットの squashAxis で X か Y に乗算。旧シーン名 alienMinScaleX から引き継ぐ。")]
        private float alienMinSquashScale = 0.55f;

        [SerializeField, Tooltip("横接近時の平行四辺形風の傾きとして加える Z 回転（度）の最大値。")]
        private float alienMaxShearDegrees = 12f;

        [SerializeField, Range(0.05f, 1f), Tooltip("カーソルがブロック AABB の左側にあるとき、横傾きに乗せる距離係数（1 で左右対称）。")]
        private float alienLeftSideHorizontalMul = 1f;

        [SerializeField, Tooltip("目標変形へ追従する SmoothDamp の時間（秒・unscaled）。0 以下は即時。")]
        private float alienSmoothSeconds = 0.1f;

        [SerializeField, Tooltip("底辺／左辺／右辺アンカーのワールド位置をシーン基準に固定する（推奨オン）。")]
        private bool alienLockBottomWorld = true;

        [SerializeField, Tooltip("worldAnchorLock が左辺または右辺のスロット用。ポインタが基準より下なら上へ、上なら下へ避ける anchored Y の最大オフセット（ピクセル・親ローカル）。")]
        private float alienVerticalFleeMaxPixels = 28f;

        private readonly List<AlienParsonGroupRuntime> alienGroups = new List<AlienParsonGroupRuntime>(4);
        private readonly Vector3[] alienWorldCornersScratch = new Vector3[4];
        private bool alienForceResyncGroups = true;
        private Coroutine deferredBaselineRefreshCoroutine;

        private const float MinBaselineRectSize = 1f;

        public int EnlargeProbabilityPercent => Mathf.Clamp(enlargeProbabilityPercent, 0, 100);

        public float EnlargeScaleFactor => enlargeScaleFactor;

        public float ShrinkScaleFactor => shrinkScaleFactor;

        public float CatSeIntervalSeconds => catSeIntervalSeconds;

        public float EnlargeSmoothSeconds => Mathf.Max(0f, enlargeSmoothSeconds);

        /// <summary>セーブ復帰や Parson スロットの active 切替後、レイアウト確定してから基準姿勢を取り直す。</summary>
        public static void ScheduleBaselineRefreshAfterLayoutForAll()
        {
            PersonEffectManager manager = FindFirstObjectByType<PersonEffectManager>(FindObjectsInactive.Include);
            manager?.ScheduleBaselineRefreshAfterLayout();
        }

        /// <summary>Parson スロットの表示状態が変わった直後に呼ぶ（次のレイアウト更新後に基準を再取得）。</summary>
        public static void NotifyParsonSlotHierarchyChanged()
        {
            PersonEffectManager manager = FindFirstObjectByType<PersonEffectManager>(FindObjectsInactive.Include);
            manager?.MarkAllGroupsBaselinesDirty();
        }

        public void ScheduleBaselineRefreshAfterLayout()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (deferredBaselineRefreshCoroutine != null)
            {
                StopCoroutine(deferredBaselineRefreshCoroutine);
            }

            deferredBaselineRefreshCoroutine = StartCoroutine(CoRefreshBaselinesAfterLayout());
        }

        private IEnumerator CoRefreshBaselinesAfterLayout()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            deferredBaselineRefreshCoroutine = null;
            alienForceResyncGroups = true;
            MarkAllGroupsBaselinesDirty();
            SyncRuntimeGroupsIfNeeded();
            ResetAllGroupsDeformStateToIdentity();
            for (int i = 0; i < alienGroups.Count; i++)
            {
                AlienParsonGroupRuntime g = alienGroups[i];
                if (g.Root == null || !g.Root.gameObject.activeInHierarchy)
                {
                    continue;
                }

                RebuildBaselinesForGroup(g);
                g.BaselinesDirty = false;
            }
        }

        private void MarkAllGroupsBaselinesDirty()
        {
            SyncRuntimeGroupsIfNeeded();
            for (int i = 0; i < alienGroups.Count; i++)
            {
                AlienParsonGroupRuntime g = alienGroups[i];
                g.BaselinesDirty = true;
                g.BaselinesReady = false;
            }
        }

        private void ResetAllGroupsDeformStateToIdentity()
        {
            for (int i = 0; i < alienGroups.Count; i++)
            {
                AlienParsonGroupRuntime g = alienGroups[i];
                g.SquashMul = 1f;
                g.ShearZ = 0f;
                g.VerticalFleeY = 0f;
                g.VelSquashMul = 0f;
                g.VelShearZ = 0f;
                g.VelVerticalFlee = 0f;
            }
        }

        private void OnEnable()
        {
            alienForceResyncGroups = true;
            MarkAllGroupsBaselinesDirty();
        }

        private void OnDisable()
        {
            if (deferredBaselineRefreshCoroutine != null)
            {
                StopCoroutine(deferredBaselineRefreshCoroutine);
                deferredBaselineRefreshCoroutine = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            alienForceResyncGroups = true;
        }
#endif

        private void LateUpdate()
        {
            UpdateAlienParsonSquash();
        }

        private void SyncRuntimeGroupsIfNeeded()
        {
            if (alienForceResyncGroups || NeedsResyncRuntimeGroups())
            {
                RebuildRuntimeGroupsFromSlots();
                alienForceResyncGroups = false;
            }
        }

        private bool NeedsResyncRuntimeGroups()
        {
            int valid = CountValidSlots();
            if (valid != alienGroups.Count)
            {
                return true;
            }

            int j = 0;
            if (alienParsonSlots == null)
            {
                return alienGroups.Count > 0;
            }

            for (int i = 0; i < alienParsonSlots.Count; i++)
            {
                AlienParsonSlot s = alienParsonSlots[i];
                if (s.parson01Root == null)
                {
                    continue;
                }

                if (j >= alienGroups.Count)
                {
                    return true;
                }

                AlienParsonGroupRuntime g = alienGroups[j];
                if (g.Root != s.parson01Root || g.Proxy != s.alienDistanceProxy ||
                    g.SquashAxis != s.squashAxis ||
                    g.InvertHorizontalShear != s.invertHorizontalShear ||
                    g.WorldAnchorLock != s.worldAnchorLock)
                {
                    return true;
                }

                j++;
            }

            return false;
        }

        private int CountValidSlots()
        {
            if (alienParsonSlots == null)
            {
                return 0;
            }

            int n = 0;
            for (int i = 0; i < alienParsonSlots.Count; i++)
            {
                if (alienParsonSlots[i].parson01Root != null)
                {
                    n++;
                }
            }

            return n;
        }

        private void RebuildRuntimeGroupsFromSlots()
        {
            alienGroups.Clear();
            if (alienParsonSlots == null)
            {
                return;
            }

            for (int i = 0; i < alienParsonSlots.Count; i++)
            {
                AlienParsonSlot s = alienParsonSlots[i];
                if (s.parson01Root == null)
                {
                    continue;
                }

                alienGroups.Add(new AlienParsonGroupRuntime(
                    s.parson01Root,
                    s.alienDistanceProxy,
                    s.squashAxis,
                    s.invertHorizontalShear,
                    s.worldAnchorLock));
            }
        }

        private void UpdateAlienParsonSquash()
        {
            SyncRuntimeGroupsIfNeeded();
            if (alienGroups.Count == 0)
            {
                return;
            }

            bool gameOk = GameManager.Instance != null &&
                !GameManager.Instance.ShouldSuppressPlayerInteractions;
            bool parsonObjectOk = parsonObjectRoot == null || parsonObjectRoot.gameObject.activeInHierarchy;

            float smoothTime = Mathf.Max(0.0001f, alienSmoothSeconds);
            float dt = Time.unscaledDeltaTime;
            bool havePointer = TryGetPointerScreenPosition(out Vector2 pointer);

            for (int gi = 0; gi < alienGroups.Count; gi++)
            {
                AlienParsonGroupRuntime g = alienGroups[gi];
                if (g.Root == null)
                {
                    continue;
                }

                if (!g.Root.gameObject.activeInHierarchy)
                {
                    g.BaselinesDirty = true;
                    g.BaselinesReady = false;
                }

                if (g.BaselinesDirty)
                {
                    RebuildBaselinesForGroup(g);
                    g.BaselinesDirty = false;
                }

                Canvas canvas = ResolveCanvas(g.Root, g.Proxy);
                bool hierarchyOk = g.Root.gameObject.activeInHierarchy && parsonObjectOk;
                Camera eventCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

                if (!hierarchyOk || !gameOk || canvas == null)
                {
                    SmoothTowardIdentityForGroup(g, smoothTime, dt, alienSmoothSeconds <= 0f);
                    continue;
                }

                if (!g.BaselinesReady)
                {
                    continue;
                }

                float maxDist = Mathf.Max(1f, alienReactionDistancePixels);
                float maxVflee = Mathf.Max(0f, alienVerticalFleeMaxPixels);

                if (!TryGetUnionScreenBoundsOfDirectChildren(g.Root, g.Proxy, eventCam, out Rect blockScreen) &&
                    !TryGetRectScreenBounds(g.Root, eventCam, out blockScreen))
                {
                    SmoothTowardIdentityForGroup(g, smoothTime, dt, alienSmoothSeconds <= 0f);
                    continue;
                }

                Vector2 refCenterScreen;
                if (g.Proxy != null)
                {
                    if (!TryGetRectScreenCenter(g.Proxy, eventCam, out refCenterScreen))
                    {
                        refCenterScreen = blockScreen.center;
                    }
                }
                else
                {
                    refCenterScreen = blockScreen.center;
                }

                float t = 0f;
                if (havePointer)
                {
                    float dist = Vector2.Distance(pointer, refCenterScreen);
                    t = 1f - Mathf.Clamp01(dist / maxDist);
                }

                bool cursorFromRight = !havePointer || pointer.x >= blockScreen.center.x;
                float tHoriz = cursorFromRight ? t : t * Mathf.Clamp01(alienLeftSideHorizontalMul);

                float targetSquashMul = Mathf.Lerp(1f, Mathf.Clamp(alienMinSquashScale, 0.01f, 1f), t);
                float maxShear = Mathf.Max(0f, alienMaxShearDegrees);
                float horizSign = g.InvertHorizontalShear ? -1f : 1f;
                float targetShearZ = Mathf.Lerp(0f, maxShear, tHoriz) * (cursorFromRight ? -1f : 1f) * horizSign;

                float targetVerticalFlee = 0f;
                if (havePointer && UsesVerticalFleeForWorldAnchor(g.WorldAnchorLock) && maxVflee > 0f)
                {
                    float dirY = Mathf.Sign(refCenterScreen.y - pointer.y);
                    targetVerticalFlee = dirY * maxVflee * t;
                }

                if (alienSmoothSeconds <= 0f)
                {
                    g.SquashMul = targetSquashMul;
                    g.ShearZ = targetShearZ;
                    g.VerticalFleeY = targetVerticalFlee;
                }
                else
                {
                    g.SquashMul = Mathf.SmoothDamp(g.SquashMul, targetSquashMul, ref g.VelSquashMul, smoothTime, Mathf.Infinity, dt);
                    g.ShearZ = Mathf.SmoothDamp(g.ShearZ, targetShearZ, ref g.VelShearZ, smoothTime, Mathf.Infinity, dt);
                    g.VerticalFleeY = Mathf.SmoothDamp(
                        g.VerticalFleeY,
                        targetVerticalFlee,
                        ref g.VelVerticalFlee,
                        smoothTime,
                        Mathf.Infinity,
                        dt);
                }

                ApplyDeformForGroup(g);
            }
        }

        private static Canvas ResolveCanvas(RectTransform root, RectTransform proxy)
        {
            Canvas canvas = root != null ? root.GetComponentInParent<Canvas>() : null;
            if (canvas == null && proxy != null)
            {
                canvas = proxy.GetComponentInParent<Canvas>();
            }

            if (canvas == null && root != null)
            {
                foreach (RectTransform child in root)
                {
                    if (child == null)
                    {
                        continue;
                    }

                    canvas = child.GetComponentInParent<Canvas>();
                    if (canvas != null)
                    {
                        break;
                    }
                }
            }

            return canvas;
        }

        private static void SmoothTowardIdentityForGroup(AlienParsonGroupRuntime g, float smoothTime, float dt, bool immediate)
        {
            if (immediate)
            {
                g.SquashMul = 1f;
                g.ShearZ = 0f;
                g.VerticalFleeY = 0f;
                g.VelSquashMul = 0f;
                g.VelShearZ = 0f;
                g.VelVerticalFlee = 0f;
                return;
            }

            g.SquashMul = Mathf.SmoothDamp(g.SquashMul, 1f, ref g.VelSquashMul, smoothTime, Mathf.Infinity, dt);
            g.ShearZ = Mathf.SmoothDamp(g.ShearZ, 0f, ref g.VelShearZ, smoothTime, Mathf.Infinity, dt);
            g.VerticalFleeY = Mathf.SmoothDamp(g.VerticalFleeY, 0f, ref g.VelVerticalFlee, smoothTime, Mathf.Infinity, dt);
        }

        private void RebuildBaselinesForGroup(AlienParsonGroupRuntime g)
        {
            g.Baselines.Clear();
            g.BaselinesReady = false;
            RectTransform root = g.Root;
            if (root == null || !root.gameObject.activeInHierarchy)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            foreach (RectTransform child in root)
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (g.Proxy != null && child == g.Proxy)
                {
                    continue;
                }

                if (!HasRenderableRectSize(child))
                {
                    continue;
                }

                AlienChildBaseline b = default;
                b.Rt = child;
                b.BaselineLocalScale = child.localScale;
                b.BaselineLocalEuler = child.localEulerAngles;
                b.BaselineAnchoredPosition = child.anchoredPosition;
                child.GetWorldCorners(alienWorldCornersScratch);
                b.WorldAnchorPointBaseline = GetWorldAnchorMidFromCorners(alienWorldCornersScratch, g.WorldAnchorLock);
                g.Baselines.Add(b);
            }

            g.BaselinesReady = g.Baselines.Count > 0;
        }

        private static bool HasRenderableRectSize(RectTransform rt)
        {
            if (rt == null)
            {
                return false;
            }

            Rect r = rt.rect;
            return r.width >= MinBaselineRectSize && r.height >= MinBaselineRectSize;
        }

        private void ApplyDeformForGroup(AlienParsonGroupRuntime g)
        {
            if (!g.BaselinesReady || g.Baselines.Count == 0)
            {
                return;
            }

            float squashMul = g.SquashMul;
            float shearZ = g.ShearZ;

            for (int i = 0; i < g.Baselines.Count; i++)
            {
                AlienChildBaseline b = g.Baselines[i];
                RectTransform rt = b.Rt;
                if (rt == null || !rt.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 scale = g.SquashAxis == AlienSquashAxisKind.X
                    ? new Vector3(b.BaselineLocalScale.x * squashMul, b.BaselineLocalScale.y, b.BaselineLocalScale.z)
                    : new Vector3(b.BaselineLocalScale.x, b.BaselineLocalScale.y * squashMul, b.BaselineLocalScale.z);
                Vector3 euler = b.BaselineLocalEuler + new Vector3(0f, 0f, shearZ);
                rt.localScale = scale;
                rt.localEulerAngles = euler;
                rt.anchoredPosition = b.BaselineAnchoredPosition;

                if (alienLockBottomWorld)
                {
                    rt.GetWorldCorners(alienWorldCornersScratch);
                    Vector3 anchorNow = GetWorldAnchorMidFromCorners(alienWorldCornersScratch, g.WorldAnchorLock);
                    Vector3 worldDelta = b.WorldAnchorPointBaseline - anchorNow;
                    RectTransform parent = rt.parent as RectTransform;
                    if (parent != null)
                    {
                        Vector3 local = parent.InverseTransformVector(worldDelta);
                        rt.anchoredPosition = b.BaselineAnchoredPosition + new Vector2(local.x, local.y);
                    }
                    else
                    {
                        rt.anchoredPosition = b.BaselineAnchoredPosition + new Vector2(worldDelta.x, worldDelta.y);
                    }
                }

                if (UsesVerticalFleeForWorldAnchor(g.WorldAnchorLock) && Mathf.Abs(g.VerticalFleeY) > 0.0001f)
                {
                    rt.anchoredPosition += new Vector2(0f, g.VerticalFleeY);
                }
            }
        }

        private static bool UsesVerticalFleeForWorldAnchor(AlienWorldAnchorLockKind worldAnchorLock)
        {
            return worldAnchorLock == AlienWorldAnchorLockKind.LeftEdgeMid ||
                   worldAnchorLock == AlienWorldAnchorLockKind.RightEdgeMid;
        }

        private static Vector3 GetWorldAnchorMidFromCorners(Vector3[] corners, AlienWorldAnchorLockKind lockKind)
        {
            if (corners == null || corners.Length < 4)
            {
                return Vector3.zero;
            }

            switch (lockKind)
            {
                case AlienWorldAnchorLockKind.LeftEdgeMid:
                    return 0.5f * (corners[0] + corners[1]);
                case AlienWorldAnchorLockKind.RightEdgeMid:
                    return 0.5f * (corners[2] + corners[3]);
                default:
                    return 0.5f * (corners[0] + corners[3]);
            }
        }

        private static bool TryGetPointerScreenPosition(out Vector2 screen)
        {
#if ENABLE_INPUT_SYSTEM
            if (Pointer.current != null)
            {
                screen = Pointer.current.position.ReadValue();
                return true;
            }

            if (Mouse.current != null)
            {
                screen = Mouse.current.position.ReadValue();
                return true;
            }

            screen = default;
            return false;
#else
            screen = (Vector2)Input.mousePosition;
            return true;
#endif
        }

        private static bool TryGetRectScreenCenter(RectTransform rt, Camera eventCam, out Vector2 center)
        {
            center = default;
            if (rt == null)
            {
                return false;
            }

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(eventCam, corners[i]);
                sum += sp;
            }

            center = sum * 0.25f;
            return true;
        }

        private static bool TryGetUnionScreenBoundsOfDirectChildren(
            RectTransform parson01Root,
            RectTransform excludeFromScale,
            Camera eventCam,
            out Rect union)
        {
            union = default;
            bool any = false;
            float xmin = 0f, xmax = 0f, ymin = 0f, ymax = 0f;

            foreach (RectTransform child in parson01Root)
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (excludeFromScale != null && child == excludeFromScale)
                {
                    continue;
                }

                if (!TryGetRectScreenBounds(child, eventCam, out Rect r))
                {
                    continue;
                }

                if (!any)
                {
                    union = r;
                    xmin = r.xMin;
                    xmax = r.xMax;
                    ymin = r.yMin;
                    ymax = r.yMax;
                    any = true;
                }
                else
                {
                    xmin = Mathf.Min(xmin, r.xMin);
                    xmax = Mathf.Max(xmax, r.xMax);
                    ymin = Mathf.Min(ymin, r.yMin);
                    ymax = Mathf.Max(ymax, r.yMax);
                }
            }

            if (!any)
            {
                return false;
            }

            union = Rect.MinMaxRect(xmin, ymin, xmax, ymax);
            return true;
        }

        private static bool TryGetRectScreenBounds(RectTransform rt, Camera eventCam, out Rect rect)
        {
            rect = default;
            if (rt == null)
            {
                return false;
            }

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float xmin = float.PositiveInfinity, xmax = float.NegativeInfinity;
            float ymin = float.PositiveInfinity, ymax = float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(eventCam, corners[i]);
                xmin = Mathf.Min(xmin, sp.x);
                xmax = Mathf.Max(xmax, sp.x);
                ymin = Mathf.Min(ymin, sp.y);
                ymax = Mathf.Max(ymax, sp.y);
            }

            if (float.IsInfinity(xmin))
            {
                return false;
            }

            rect = Rect.MinMaxRect(xmin, ymin, xmax, ymax);
            return true;
        }

        private sealed class AlienParsonGroupRuntime
        {
            public readonly RectTransform Root;
            public readonly RectTransform Proxy;
            public readonly AlienSquashAxisKind SquashAxis;
            public readonly bool InvertHorizontalShear;
            public readonly AlienWorldAnchorLockKind WorldAnchorLock;
            public readonly List<AlienChildBaseline> Baselines = new List<AlienChildBaseline>(8);
            public bool BaselinesDirty = true;
            public bool BaselinesReady;
            public float SquashMul = 1f;
            public float ShearZ;
            public float VerticalFleeY;
            public float VelSquashMul;
            public float VelShearZ;
            public float VelVerticalFlee;

            public AlienParsonGroupRuntime(
                RectTransform root,
                RectTransform proxy,
                AlienSquashAxisKind squashAxis,
                bool invertHorizontalShear,
                AlienWorldAnchorLockKind worldAnchorLock)
            {
                Root = root;
                Proxy = proxy;
                SquashAxis = squashAxis;
                InvertHorizontalShear = invertHorizontalShear;
                WorldAnchorLock = worldAnchorLock;
            }
        }

        private struct AlienChildBaseline
        {
            public RectTransform Rt;
            public Vector3 BaselineLocalScale;
            public Vector3 BaselineLocalEuler;
            public Vector2 BaselineAnchoredPosition;
            public Vector3 WorldAnchorPointBaseline;
        }
    }
}
