#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

/// <summary>
/// エディタ再生中のみ。メインカメラの描画結果を高解像度 PNG で保存する（製品ビルドには含まれない）。
/// 仕様: spec/dev_steam_main_camera_screenshot_spec.md
/// </summary>
public static class DevSteamMainCameraScreenshotMenu
{
    private const string MenuPathJp = "ツール/開発用/Steam用スクショ（メインカメラ） 2x";
    private const string MenuPathEn = "Tools/Development/Steam Screenshot (Main Camera) 2x";
    private const int SuperSize = 2;
    private const string OutputFolderRelativeToProjectRoot = "Screenshots/SteamDevMainCamera";

    [MenuItem(MenuPathJp, false, 1)]
    private static void CaptureMainCamera2xJp()
    {
        CaptureMainCamera2xInternal();
    }

    [MenuItem(MenuPathEn, false, 1)]
    private static void CaptureMainCamera2xEn()
    {
        CaptureMainCamera2xInternal();
    }

    private static void CaptureMainCamera2xInternal()
    {
        Camera cam = Camera.main;
        if (!Application.isPlaying || cam == null)
        {
            return;
        }

        int w = Mathf.Max(1, cam.pixelWidth * SuperSize);
        int h = Mathf.Max(1, cam.pixelHeight * SuperSize);

        // 色が暗くなるのを防ぐため、保存用RTはsRGBのLDRターゲットを明示する。
        // （Linear色空間プロジェクトでもPNGの見え方をゲームビューに寄せる）
        RenderTextureDescriptor desc = new RenderTextureDescriptor(w, h)
        {
            depthBufferBits = 24,
            msaaSamples = 1,
            useMipMap = false,
            autoGenerateMips = false,
            graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB,
            sRGB = true
        };
        RenderTexture rt = RenderTexture.GetTemporary(desc);

        RenderTexture prevTarget = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;

        try
        {
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputFolderRelativeToProjectRoot));
            Directory.CreateDirectory(dir);

            string sceneToken = SanitizeFileToken(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string fileName = $"maincamera_{sceneToken}_{stamp}_{SuperSize}x.png";
            string fullPath = Path.Combine(dir, fileName);

            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);

            Debug.Log($"[DevSteamScreenshot] Saved main camera PNG ({w}x{h}): {fullPath}");
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    [MenuItem(MenuPathJp, true)]
    private static bool CaptureMainCamera2xValidateJp()
    {
        return IsCaptureAvailable();
    }

    [MenuItem(MenuPathEn, true)]
    private static bool CaptureMainCamera2xValidateEn()
    {
        return IsCaptureAvailable();
    }

    private static bool IsCaptureAvailable()
    {
        return Application.isPlaying && Camera.main != null;
    }

    private static string SanitizeFileToken(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return "scene";
        }

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            sceneName = sceneName.Replace(c, '_');
        }

        return sceneName;
    }
}
#endif
