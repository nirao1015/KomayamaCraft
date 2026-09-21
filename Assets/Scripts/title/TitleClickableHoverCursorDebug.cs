using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// デバッグ用：クリック可能 Graphic 上にマウスがあるときカーソルを差し替える。
/// <see cref="TitleDebugManager"/> のフラグが ON かつ本番マスター OFF のときだけ有効。
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleClickableHoverCursorDebug : MonoBehaviour
{
    [SerializeField] private Graphic hitGraphic;
    [SerializeField] private Texture2D hoverCursor;
    [SerializeField] private Vector2 hoverCursorHotspot = new Vector2(8f, 8f);

    private static readonly List<RaycastResult> RaycastBuffer = new List<RaycastResult>(16);
    private static Texture2D fallbackCursor;
    private static int hoverRetainCount;
    private bool hovering;

    private void Awake()
    {
        if (hitGraphic == null)
        {
            hitGraphic = GetComponent<Graphic>();
        }
    }

    private void OnDisable()
    {
        ClearHoverIfNeeded();
    }

    private void OnDestroy()
    {
        ClearHoverIfNeeded();
    }

    private void Update()
    {
        if (!TitleDebugManager.IsDebugClickableHoverCursorActive)
        {
            ClearHoverIfNeeded();
            return;
        }

        bool over = IsPointerOverHitGraphic();
        if (over == hovering)
        {
            return;
        }

        if (over)
        {
            BeginHover();
        }
        else
        {
            ClearHoverIfNeeded();
        }
    }

    private bool IsPointerOverHitGraphic()
    {
        if (hitGraphic == null || !hitGraphic.isActiveAndEnabled || !hitGraphic.raycastTarget)
        {
            return false;
        }

        if (EventSystem.current == null || Mouse.current == null)
        {
            return false;
        }

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        RaycastBuffer.Clear();
        EventSystem.current.RaycastAll(eventData, RaycastBuffer);
        for (int i = 0; i < RaycastBuffer.Count; i++)
        {
            GameObject go = RaycastBuffer[i].gameObject;
            if (go == hitGraphic.gameObject)
            {
                return true;
            }
        }

        return false;
    }

    private void BeginHover()
    {
        if (hovering)
        {
            return;
        }

        hovering = true;
        hoverRetainCount++;
        Texture2D tex = ResolveCursorTexture();
        Vector2 hotspot = hoverCursorHotspot;
        if (TitleDebugManager.Instance != null && TitleDebugManager.Instance.DebugClickableHoverCursorTexture != null)
        {
            hotspot = TitleDebugManager.Instance.DebugClickableHoverCursorHotspot;
        }

        Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
    }

    private Texture2D ResolveCursorTexture()
    {
        if (hoverCursor != null)
        {
            return hoverCursor;
        }

        if (TitleDebugManager.Instance != null && TitleDebugManager.Instance.DebugClickableHoverCursorTexture != null)
        {
            return TitleDebugManager.Instance.DebugClickableHoverCursorTexture;
        }

        return GetFallbackCursor();
    }

    private void ClearHoverIfNeeded()
    {
        if (!hovering)
        {
            return;
        }

        hovering = false;
        hoverRetainCount = Mathf.Max(0, hoverRetainCount - 1);
        if (hoverRetainCount == 0)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    private static Texture2D GetFallbackCursor()
    {
        if (fallbackCursor != null)
        {
            return fallbackCursor;
        }

        const int size = 24;
        fallbackCursor = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "TitleDebugHoverCursorFallback"
        };

        var clear = new Color(0f, 0f, 0f, 0f);
        var mark = new Color(0.1f, 1f, 0.85f, 1f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool edge = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                bool cross = (x >= 10 && x <= 13) || (y >= 10 && y <= 13);
                fallbackCursor.SetPixel(x, y, edge || cross ? mark : clear);
            }
        }

        fallbackCursor.Apply(false, true);
        return fallbackCursor;
    }
}
