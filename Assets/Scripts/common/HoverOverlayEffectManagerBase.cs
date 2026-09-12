using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public abstract class HoverOverlayEffectManagerBase : MonoBehaviour
{
    protected sealed class HoverStateTracker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public bool EnableDebugLog { get; set; }
        public string DebugLabel { get; set; }

        public bool IsPointerInside { get; private set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsPointerInside = true;
            if (EnableDebugLog)
            {
                Debug.Log($"[HoverOverlay] PointerEnter source={DebugLabel}");
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsPointerInside = false;
            if (EnableDebugLog)
            {
                Debug.Log($"[HoverOverlay] PointerExit source={DebugLabel}");
            }
        }

        private void OnDisable()
        {
            IsPointerInside = false;
        }
    }

    [Serializable]
    protected sealed class HoverOverlayBinding
    {
        [Tooltip("ホバー判定元。Button 以外の Selectable も指定できます。")]
        public Selectable hoverSource;
        [Tooltip("明るくする対象 Graphic。")]
        public Graphic targetOverlayGraphic;
        [NonSerialized] public Material runtimeMaterial;
        [NonSerialized] public float currentIntensity;
        [NonSerialized] public HoverStateTracker hoverTracker;
        [NonSerialized] public bool lastHighlighted;
    }

    [Header("HoverOverlay 設定")]
    [SerializeField, Tooltip("有効時、Inspector で指定した HoverOverlay を制御します。")]
    private bool enableHoverOverlay = true;
    [SerializeField, Range(0f, 1f), Tooltip("ホバー時の加算明度。")]
    private float hoverIntensity = 0.24f;
    [SerializeField, Tooltip("ホバー解除時に元へ戻す秒数。")]
    private float fadeOutSeconds = 0.08f;
    [SerializeField, Tooltip("未設定時は UI/Title/HoverAdditiveOverlay を使用。")]
    private Shader overlayShader;
    [SerializeField, Tooltip("HoverOverlay 対象の一覧。")]
    private HoverOverlayBinding[] hoverOverlayBindings = Array.Empty<HoverOverlayBinding>();
    [SerializeField, Tooltip("有効時、ホバー到達と強調ON/OFFのログを出します。")]
    private bool debugHoverLogs;

    private const string ShaderPath = "UI/Title/HoverAdditiveOverlay";
    private const string IntensityProperty = "_Intensity";

    protected virtual void Awake()
    {
        EnsureMaterials();
        ResetAllOverlayIntensity();
    }

    protected virtual void OnEnable()
    {
        EnsureMaterials();
        ResetAllOverlayIntensity();
    }

    /// <summary>PausePanel 表示時など、バインド追加後にマテリアルを再構築する。</summary>
    public void RefreshHoverOverlayMaterials()
    {
        ReleaseAllRuntimeMaterials();
        EnsureMaterials();
        ResetAllOverlayIntensity();
    }

    protected virtual void Update()
    {
        if (!enableHoverOverlay || hoverOverlayBindings == null)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float fadeSpeed = fadeOutSeconds > 0f ? 1f / fadeOutSeconds : float.PositiveInfinity;

        for (int i = 0; i < hoverOverlayBindings.Length; i++)
        {
            HoverOverlayBinding binding = hoverOverlayBindings[i];
            if (binding == null || binding.runtimeMaterial == null)
            {
                continue;
            }

            bool highlighted = IsHovering(binding);
            float target = highlighted ? hoverIntensity : 0f;
            if (debugHoverLogs && binding.lastHighlighted != highlighted)
            {
                string sourceName = binding.hoverSource != null ? binding.hoverSource.name : "(null)";
                string targetName = binding.targetOverlayGraphic != null ? binding.targetOverlayGraphic.name : "(null)";
                Debug.Log($"[HoverOverlay] Highlight {(highlighted ? "ON" : "OFF")} source={sourceName} target={targetName}");
                binding.lastHighlighted = highlighted;
            }

            if (highlighted || fadeOutSeconds <= 0f)
            {
                binding.currentIntensity = target;
            }
            else
            {
                binding.currentIntensity = Mathf.MoveTowards(binding.currentIntensity, target, dt * fadeSpeed);
            }

            binding.runtimeMaterial.SetFloat(IntensityProperty, Mathf.Clamp01(binding.currentIntensity));
        }
    }

    protected virtual void OnDestroy()
    {
        if (hoverOverlayBindings == null)
        {
            return;
        }

        for (int i = 0; i < hoverOverlayBindings.Length; i++)
        {
            HoverOverlayBinding binding = hoverOverlayBindings[i];
            if (binding == null || binding.runtimeMaterial == null)
            {
                continue;
            }

            Destroy(binding.runtimeMaterial);
            binding.runtimeMaterial = null;
        }
    }

    private void EnsureMaterials()
    {
        if (hoverOverlayBindings == null)
        {
            return;
        }

        if (overlayShader == null)
        {
            overlayShader = Shader.Find(ShaderPath);
        }

        if (overlayShader == null)
        {
            return;
        }

        for (int i = 0; i < hoverOverlayBindings.Length; i++)
        {
            HoverOverlayBinding binding = hoverOverlayBindings[i];
            if (binding == null || binding.targetOverlayGraphic == null || binding.runtimeMaterial != null)
            {
                continue;
            }

            if (binding.hoverSource != null)
            {
                binding.hoverTracker = binding.hoverSource.GetComponent<HoverStateTracker>();
                if (binding.hoverTracker == null)
                {
                    binding.hoverTracker = binding.hoverSource.gameObject.AddComponent<HoverStateTracker>();
                }

                binding.hoverTracker.EnableDebugLog = debugHoverLogs;
                binding.hoverTracker.DebugLabel = binding.hoverSource.name;
            }

            binding.runtimeMaterial = new Material(overlayShader)
            {
                name = "Runtime_HoverOverlay",
                hideFlags = HideFlags.DontSave
            };

            if (binding.targetOverlayGraphic is MaskableGraphic maskable)
            {
                maskable.material = binding.runtimeMaterial;
            }
            else
            {
                binding.targetOverlayGraphic.material = binding.runtimeMaterial;
            }
        }
    }

    private static bool IsHovering(HoverOverlayBinding binding)
    {
        if (binding == null)
        {
            return false;
        }

        Selectable hoverSource = binding.hoverSource;
        if (hoverSource == null || !hoverSource.isActiveAndEnabled || !hoverSource.interactable)
        {
            return false;
        }

        if (binding.hoverTracker != null && binding.hoverTracker.IsPointerInside)
        {
            return true;
        }

        return IsPointerOverSelectableHierarchy(hoverSource);
    }

    private static bool IsPointerOverSelectableHierarchy(Selectable hoverSource)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || hoverSource == null)
        {
            return false;
        }

        Vector2 screenPosition;
        if (Mouse.current != null)
        {
            screenPosition = Mouse.current.position.ReadValue();
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else
        {
            return false;
        }

        var pointerData = new PointerEventData(eventSystem) { position = screenPosition };
        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);
        Transform hoverRoot = hoverSource.transform;
        for (int i = 0; i < results.Count; i++)
        {
            Transform hit = results[i].gameObject.transform;
            if (hit == hoverRoot || hit.IsChildOf(hoverRoot))
            {
                return true;
            }
        }

        return false;
    }

    private void ResetAllOverlayIntensity()
    {
        if (hoverOverlayBindings == null)
        {
            return;
        }

        for (int i = 0; i < hoverOverlayBindings.Length; i++)
        {
            HoverOverlayBinding binding = hoverOverlayBindings[i];
            if (binding == null)
            {
                continue;
            }

            binding.currentIntensity = 0f;
            binding.lastHighlighted = false;
            if (binding.runtimeMaterial != null)
            {
                binding.runtimeMaterial.SetFloat(IntensityProperty, 0f);
            }
        }
    }

    private void ReleaseAllRuntimeMaterials()
    {
        if (hoverOverlayBindings == null)
        {
            return;
        }

        for (int i = 0; i < hoverOverlayBindings.Length; i++)
        {
            HoverOverlayBinding binding = hoverOverlayBindings[i];
            if (binding == null)
            {
                continue;
            }

            if (binding.targetOverlayGraphic is MaskableGraphic maskable && binding.runtimeMaterial != null)
            {
                if (maskable.material == binding.runtimeMaterial)
                {
                    maskable.material = null;
                }
            }
            else if (binding.targetOverlayGraphic != null && binding.runtimeMaterial != null &&
                     binding.targetOverlayGraphic.material == binding.runtimeMaterial)
            {
                binding.targetOverlayGraphic.material = null;
            }

            if (binding.runtimeMaterial != null)
            {
                Destroy(binding.runtimeMaterial);
                binding.runtimeMaterial = null;
            }
        }
    }
}
