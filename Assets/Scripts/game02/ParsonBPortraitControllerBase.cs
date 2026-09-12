using System.Collections;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// ParsonObject / ParsonB* の抽象基底（コンポーネントとしてはアタッチしない）。
    /// 具象は <see cref="ParsonB01Controller"/> / <see cref="ParsonB02Controller"/> / <see cref="ParsonB03Controller"/> を使う。
    /// </summary>
    public abstract class ParsonBPortraitControllerBase : MonoBehaviour
    {
        [Header("対象（各 ParsonB ごとにインスペクターで指定）")]
        [Tooltip("非バズ時に表示するオブジェクトの RectTransform（例: ParsonB01Image / SideParsonB01Image）。")]
        [SerializeField]
        protected RectTransform primaryImageRect;

        [Tooltip("バズ動画稼働中に表示するオブジェクト（例: ParsonB01Image_2 / SideParsonB01Image_2）。")]
        [SerializeField]
        protected GameObject buzzOverlayGo;

        private bool lastBuzzResolved;
        private bool hasSyncedBuzzOnce;

        private Coroutine deferredSaveBuzzCoroutine;

        public static void RefreshAllAfterSaveApplied()
        {
            ParsonBPortraitControllerBase[] arr =
                FindObjectsByType<ParsonBPortraitControllerBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < arr.Length; i++)
            {
                ParsonBPortraitControllerBase c = arr[i];
                if (c != null)
                {
                    c.NotifySaveAppliedDeferred();
                }
            }
        }

        public void NotifySaveAppliedDeferred()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (deferredSaveBuzzCoroutine != null)
            {
                StopCoroutine(deferredSaveBuzzCoroutine);
            }

            deferredSaveBuzzCoroutine = StartCoroutine(CoDeferredSaveBuzzSync());
        }

        private IEnumerator CoDeferredSaveBuzzSync()
        {
            yield return null;
            deferredSaveBuzzCoroutine = null;
            bool w = WorkMovieUploadController.SceneHasBuzzMovieInBuzzPeriod();
            lastBuzzResolved = w;
            hasSyncedBuzzOnce = true;
            ApplyBuzzVisual(w);
        }

        private void Awake()
        {
            WarnIfTargetsMissing();
        }

        private void OnEnable()
        {
            hasSyncedBuzzOnce = false;
            WorkMovieUploadController.BuzzTriggered += OnWorkMovieBuzzChanged;
            WorkMovieUploadController.MovieUploadAccepted += OnMovieUploadAccepted;

            WarnIfTargetsMissing();
        }

        private void OnDisable()
        {
            WorkMovieUploadController.BuzzTriggered -= OnWorkMovieBuzzChanged;
            WorkMovieUploadController.MovieUploadAccepted -= OnMovieUploadAccepted;

            if (deferredSaveBuzzCoroutine != null)
            {
                StopCoroutine(deferredSaveBuzzCoroutine);
                deferredSaveBuzzCoroutine = null;
            }
        }

        private void LateUpdate()
        {
            bool w = WorkMovieUploadController.SceneHasBuzzMovieInBuzzPeriod();
            if (!hasSyncedBuzzOnce || w != lastBuzzResolved)
            {
                hasSyncedBuzzOnce = true;
                lastBuzzResolved = w;
                ApplyBuzzVisual(w);
            }
        }

        private void OnWorkMovieBuzzChanged()
        {
            ApplyBuzzFromWorldImmediate();
        }

        private void OnMovieUploadAccepted(int _)
        {
            ApplyBuzzFromWorldImmediate();
        }

        private void ApplyBuzzFromWorldImmediate()
        {
            bool w = WorkMovieUploadController.SceneHasBuzzMovieInBuzzPeriod();
            if (hasSyncedBuzzOnce && w == lastBuzzResolved)
            {
                return;
            }

            lastBuzzResolved = w;
            hasSyncedBuzzOnce = true;
            ApplyBuzzVisual(w);
        }

        private void WarnIfTargetsMissing()
        {
            if (primaryImageRect == null)
            {
                Debug.LogWarning(
                    $"{GetType().Name}: 「対象」の通常画像 RectTransform が未設定です。インスペクターで Primary Image Rect を指定してください。",
                    this);
            }

            if (buzzOverlayGo == null)
            {
                Debug.LogWarning(
                    $"{GetType().Name}: 「対象」のバズ時オブジェクトが未設定です。インスペクターで Buzz Overlay を指定してください。",
                    this);
            }
        }

        private void ApplyBuzzVisual(bool buzzMovieActive)
        {
            if (primaryImageRect != null)
            {
                primaryImageRect.gameObject.SetActive(!buzzMovieActive);
            }

            if (buzzOverlayGo != null)
            {
                buzzOverlayGo.SetActive(buzzMovieActive);
            }
        }
    }
}
