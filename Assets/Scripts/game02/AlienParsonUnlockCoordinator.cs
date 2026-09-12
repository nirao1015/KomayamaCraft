using UnityEngine;

namespace Game02
{
    /// <summary>
    /// 異星人スロットの解放判定と <see cref="GameObject.SetActive"/>。仕様: alien_parson_unlock_spec.md
    /// </summary>
    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    public sealed class AlienParsonUnlockCoordinator : MonoBehaviour
    {
        [Header("スロット参照（未設定時は名前で補完）")]
        [SerializeField] private Transform parson01;
        [SerializeField] private Transform parson02;
        [SerializeField] private Transform parson03;
        [SerializeField] private Transform parson04;
        [SerializeField] private Transform parson05;
        [SerializeField] private Transform parsonL01;
        [SerializeField] private Transform parsonR01;
        [SerializeField] private Transform parsonR02;
        [SerializeField] private Transform parsonB01;
        [SerializeField] private Transform parsonB02;
        [SerializeField] private Transform parsonB03;
        [SerializeField] private Transform sideParson01;
        [SerializeField] private Transform sideParson02;

        [Header("閾値（インスペクター調整）")]
        [SerializeField] private int uploadsRequiredParson01 = 2;
        [SerializeField] private int uploadsRequiredParson02 = 5;
        [SerializeField] private int uploadsRequiredParson03 = 30;
        [SerializeField] private long popularityAboveParson04 = 10_000L;
        [SerializeField] private long popularityAboveParson05 = 100_000L;
        [SerializeField] private long popularityAboveParsonL01 = 1_000L;
        [SerializeField] private int editorBuysRequiredParsonR01 = 2;
        [SerializeField] private int editorBuysRequiredParsonR02 = 3;
        [SerializeField] private int sidePanelOpensRequiredSideParson01 = 8;
        [SerializeField] private int sidePanelOpensRequiredSideParson02 = 20;
        [SerializeField] private int appearBuzzMoviesRequiredParsonB01 = 2;
        [SerializeField] private int appearBuzzMoviesRequiredParsonB02 = 5;
        [SerializeField] private int appearBuzzMoviesRequiredParsonB03 = 10;

        private Game02AlienProgressTracker tracker;

        private void Awake()
        {
            ResolveSlotRefsIfNeeded();
        }

        private void OnEnable()
        {
            tracker = Game02AlienProgressTracker.EnsureExists();
            if (tracker != null)
            {
                tracker.ProgressChanged += OnProgressChanged;
            }
        }

        private void OnDisable()
        {
            if (tracker != null)
            {
                tracker.ProgressChanged -= OnProgressChanged;
            }
        }

        private void Start()
        {
            RefreshAllSlots();
        }

        private void OnProgressChanged()
        {
            RefreshAllSlots();
        }

        private void ResolveSlotRefsIfNeeded()
        {
            Transform parsonRoot = ResolveParsonObjectRoot();
            Transform sideRoot = ResolveSidePanelParsonObjectRoot();
            parson01 ??= FindDirectChildNamed(parsonRoot, "Parson01");
            parson02 ??= FindDirectChildNamed(parsonRoot, "Parson02");
            parson03 ??= FindDirectChildNamed(parsonRoot, "Parson03");
            parson04 ??= FindDirectChildNamed(parsonRoot, "Parson04");
            parson05 ??= FindDirectChildNamed(parsonRoot, "Parson05");
            parsonL01 ??= FindDirectChildNamed(parsonRoot, "ParsonL01");
            parsonR01 ??= FindDirectChildNamed(parsonRoot, "ParsonR01");
            parsonR02 ??= FindDirectChildNamed(parsonRoot, "ParsonR02");
            parsonB01 ??= FindDirectChildNamed(parsonRoot, "ParsonB01");
            parsonB02 ??= FindDirectChildNamed(parsonRoot, "ParsonB02");
            parsonB03 ??= FindDirectChildNamed(parsonRoot, "ParsonB03");
            sideParson01 ??= FindDirectChildNamed(sideRoot, "SideParson01");
            sideParson02 ??= FindDirectChildNamed(sideRoot, "SideParson02");
        }

        private static Transform ResolveParsonObjectRoot()
        {
            GameObject go = GameObject.Find("PanelCanvas/ParsonObject");
            return go != null ? go.transform : null;
        }

        private static Transform ResolveSidePanelParsonObjectRoot()
        {
            GameObject go =
                GameObject.Find("PanelCanvas/PanelObject/SidePanelObject/SidePanelView/SidePanelParsonObject");
            return go != null ? go.transform : null;
        }

        private static Transform FindDirectChildNamed(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            int n = root.childCount;
            for (int i = 0; i < n; i++)
            {
                Transform c = root.GetChild(i);
                if (c != null && c.name == childName)
                {
                    return c;
                }
            }

            return null;
        }

        public void RefreshAllSlots()
        {
            tracker = tracker != null ? tracker : Game02AlienProgressTracker.Instance;
            if (tracker == null)
            {
                return;
            }

            GameManager gm = GameManager.Instance;
            long popularity = gm != null ? gm.CurrentPopularity : 0L;
            int uploads = tracker.LifetimeWorkMovie1UploadCount;
            int buzzMovies = tracker.LifetimeBuzzMovieUploadCount;
            int sideOpens = tracker.LifetimeSidePanelOpenCount;
            int editorDistinct = Game02AlienProgressTracker.CountPurchasedEditorBuysInScene();

            bool u01 = tracker.UnlockedParson01 ||
                       uploads >= Mathf.Max(0, uploadsRequiredParson01);
            bool u02 = tracker.UnlockedParson02 ||
                       uploads >= Mathf.Max(0, uploadsRequiredParson02);
            bool u03 = tracker.UnlockedParson03 ||
                       uploads >= Mathf.Max(0, uploadsRequiredParson03);
            bool u04 = tracker.UnlockedParson04 ||
                       popularity > popularityAboveParson04;
            bool u05 = tracker.UnlockedParson05 ||
                       popularity > popularityAboveParson05;
            bool uL01 = tracker.UnlockedParsonL01 ||
                        popularity > popularityAboveParsonL01;
            bool uR01 = tracker.UnlockedParsonR01 ||
                        editorDistinct >= Mathf.Max(0, editorBuysRequiredParsonR01);
            bool uR02 = tracker.UnlockedParsonR02 ||
                        editorDistinct >= Mathf.Max(0, editorBuysRequiredParsonR02);
            bool us01 = tracker.UnlockedSideParson01 ||
                        sideOpens >= Mathf.Max(0, sidePanelOpensRequiredSideParson01);
            bool us02 = tracker.UnlockedSideParson02 ||
                        sideOpens >= Mathf.Max(0, sidePanelOpensRequiredSideParson02);

            bool uB01 = buzzMovies >= Mathf.Max(0, appearBuzzMoviesRequiredParsonB01);
            bool uB02 = buzzMovies >= Mathf.Max(0, appearBuzzMoviesRequiredParsonB02);
            bool uB03 = buzzMovies >= Mathf.Max(0, appearBuzzMoviesRequiredParsonB03);

            tracker.AssignUnlockFlags(u01, u02, u03, u04, u05, uL01, uR01, uR02, us01, us02);

            bool parson01WasVisible = parson01 != null && parson01.gameObject.activeSelf;
            ApplyActive(parson01, u01);
            bool parson01NowVisible = parson01 != null && parson01.gameObject.activeSelf;
            if (parson01NowVisible && !parson01WasVisible)
            {
                Game02MsgManager.TryGet()?.NotifyParson01BecameVisible(GameManager.Instance);
            }

            ApplyActive(parsonR01, uR01);

            ApplyActive(parson02, u02);
            ApplyActive(parson03, u03);
            ApplyActive(parson04, u04);
            ApplyActive(parson05, u05);
            ApplyActive(parsonL01, uL01);
            ApplyActive(parsonR02, uR02);
            ApplyActive(parsonB01, uB01);
            ApplyActive(parsonB02, uB02);
            ApplyActive(parsonB03, uB03);
            ApplyActive(sideParson01, us01);
            ApplyActive(sideParson02, us02);
        }

        private static void ApplyActive(Transform t, bool unlocked)
        {
            if (t == null)
            {
                return;
            }

            GameObject go = t.gameObject;
            if (go.activeSelf != unlocked)
            {
                go.SetActive(unlocked);
                PersonEffectManager.NotifyParsonSlotHierarchyChanged();
            }
        }
    }
}
