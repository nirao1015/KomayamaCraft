using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// ReturnText (TMP) の点滅と遷移確定入力（Escape 即時／WASD・Space・左クリック）。
/// </summary>
public sealed class Game03ReturnTextPrompt
{
    public enum ConfirmInputMode
    {
        DoubleTap,
        SinglePress
    }
    private enum FastTapInput
    {
        None,
        MouseLeft,
        W,
        A,
        S,
        D,
        Space
    }

    private TextMeshProUGUI textMesh;
    private float blinkPeriodSeconds = 3f;
    private float doubleTapIntervalSeconds = 0.28f;
    private ConfirmInputMode confirmInputMode = ConfirmInputMode.DoubleTap;
    private bool active;
    private FastTapInput lastTap = FastTapInput.None;
    private float lastTapTime = -999f;

    public bool IsActive => active;

    public void Begin(
        TextMeshProUGUI target,
        float blinkPeriod = 3f,
        float doubleTapInterval = 0.28f,
        ConfirmInputMode inputMode = ConfirmInputMode.DoubleTap)
    {
        textMesh = target;
        blinkPeriodSeconds = Mathf.Max(0.01f, blinkPeriod);
        doubleTapIntervalSeconds = Mathf.Max(0.05f, doubleTapInterval);
        confirmInputMode = inputMode;
        active = textMesh != null;
        ResetTapTracking();

        if (textMesh == null)
        {
            return;
        }

        textMesh.gameObject.SetActive(true);
        Color c = textMesh.color;
        c.a = 0f;
        textMesh.color = c;
    }

    public void End()
    {
        active = false;
        ResetTapTracking();
    }

    public void TickBlink()
    {
        if (!active || textMesh == null)
        {
            return;
        }

        float a = (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / blinkPeriodSeconds)) + 1f) * 0.5f;
        Color c = textMesh.color;
        c.a = a;
        textMesh.color = c;
    }

    public bool TryConsumeConfirm()
    {
        if (!active)
        {
            return false;
        }

        if (TryEscapeImmediateTransitionPressed())
        {
            ResetTapTracking();
            return true;
        }

        FastTapInput tap = GetTapPressedThisFrame();
        if (confirmInputMode == ConfirmInputMode.SinglePress)
        {
            return tap != FastTapInput.None;
        }

        return TryConsumeDoubleTapConfirm(tap);
    }

    private void ResetTapTracking()
    {
        lastTap = FastTapInput.None;
        lastTapTime = -999f;
    }

    private bool TryConsumeDoubleTapConfirm(FastTapInput tap)
    {
        if (tap == FastTapInput.None)
        {
            return false;
        }

        float now = Time.unscaledTime;
        if (tap == lastTap && now - lastTapTime <= doubleTapIntervalSeconds)
        {
            ResetTapTracking();
            return true;
        }

        lastTap = tap;
        lastTapTime = now;
        return false;
    }

    private static bool TryEscapeImmediateTransitionPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    private static FastTapInput GetTapPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        FastTapInput t = GetTapFromNewInput();
        if (t != FastTapInput.None)
        {
            return t;
        }

        return FastTapInput.None;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return GetTapFromLegacy();
#else
        return FastTapInput.None;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static FastTapInput GetTapFromNewInput()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return FastTapInput.MouseLeft;
        }

        if (Keyboard.current == null)
        {
            return FastTapInput.None;
        }

        if (Keyboard.current.wKey.wasPressedThisFrame)
        {
            return FastTapInput.W;
        }

        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            return FastTapInput.A;
        }

        if (Keyboard.current.sKey.wasPressedThisFrame)
        {
            return FastTapInput.S;
        }

        if (Keyboard.current.dKey.wasPressedThisFrame)
        {
            return FastTapInput.D;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return FastTapInput.Space;
        }

        return FastTapInput.None;
    }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
    private static FastTapInput GetTapFromLegacy()
    {
        if (Input.GetMouseButtonDown(0))
        {
            return FastTapInput.MouseLeft;
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            return FastTapInput.W;
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            return FastTapInput.A;
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            return FastTapInput.S;
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            return FastTapInput.D;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            return FastTapInput.Space;
        }

        return FastTapInput.None;
    }
#endif
}
