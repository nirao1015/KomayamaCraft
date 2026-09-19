using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KomayamaCraft
{
    /// <summary>
    /// 画面 UI Canvas の描画帯（UiHud / UiSystem / UiDebug）を固定する。
    /// ワールド Sorting より上に置き、Canvas 同士は Layer を共有しない。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class KomayamaScreenCanvasBands : MonoBehaviour
    {
        [Serializable]
        public sealed class Band
        {
            public Canvas canvas;
            public string sortingLayerName = "UiHud";
            public int sortingOrder;
            public float planeDistance = 1f;
        }

        [SerializeField]
        private Band[] bands =
        {
            new Band { sortingLayerName = "UiHud", sortingOrder = 0, planeDistance = 1f },
            new Band { sortingLayerName = "UiSystem", sortingOrder = 0, planeDistance = 1f },
            new Band { sortingLayerName = "UiDebug", sortingOrder = 0, planeDistance = 1f },
        };

        [SerializeField]
        private bool applyInEditMode = true;

        private void OnEnable()
        {
            Apply();
        }

        private void LateUpdate()
        {
            // 編集中に毎フレーム Apply+SetDirty するとシーンの * が消えない
            if (!Application.isPlaying)
            {
                return;
            }

            Apply();
        }

        [ContextMenu("Apply Screen Canvas Bands Now")]
        public void Apply()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (!Application.isPlaying && !applyInEditMode)
            {
                return;
            }

            if (bands == null)
            {
                return;
            }

            for (int i = 0; i < bands.Length; i++)
            {
                ApplyBand(bands[i]);
            }
        }

        private static void ApplyBand(Band band)
        {
            if (band == null || band.canvas == null || string.IsNullOrEmpty(band.sortingLayerName))
            {
                return;
            }

            if (!SortingLayerExists(band.sortingLayerName))
            {
                return;
            }

            Canvas canvas = band.canvas;
            bool changed = false;

            if (canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                changed = true;
            }

            Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            if (cam != null && canvas.worldCamera != cam)
            {
                canvas.worldCamera = cam;
                changed = true;
            }

            if (canvas.sortingLayerName != band.sortingLayerName)
            {
                canvas.sortingLayerName = band.sortingLayerName;
                changed = true;
            }

            if (!canvas.overrideSorting || canvas.sortingOrder != band.sortingOrder)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = band.sortingOrder;
                changed = true;
            }

            if (cam != null && band.planeDistance > 0f)
            {
                float safePlane = Mathf.Clamp(
                    band.planeDistance,
                    cam.nearClipPlane + 0.1f,
                    cam.farClipPlane - 0.1f);
                if (!Mathf.Approximately(canvas.planeDistance, safePlane))
                {
                    canvas.planeDistance = safePlane;
                    changed = true;
                }
            }

            if (changed)
            {
                MarkDirty(canvas);
            }
        }

        private static bool SortingLayerExists(string name)
        {
            SortingLayer[] layers = SortingLayer.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private static void MarkDirty(UnityEngine.Object target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && target != null)
            {
                EditorUtility.SetDirty(target);
            }
#else
            _ = target;
#endif
        }
    }
}
