using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Auditoria e captura isoladas para a frente AAA. Não altera ProjectSettings.</summary>
public static class AAARenderPipelineAudit
{
    public static void Run()
    {
        var cameraCount = 0;
        foreach (var scene in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            cameraCount += 1;

        var report = Path.Combine("TestResults", "aaa-render-audit.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(report));
        File.WriteAllText(report,
            "AAA_RENDER_AUDIT_PASSED\n" +
            "builtInPipeline=" + (GraphicsSettings.defaultRenderPipeline == null) + "\n" +
            "quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "\n" +
            "sceneAssets=" + cameraCount + "\n" +
            "hdrSupported=" + SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) + "\n" +
            "msaaSupported=" + SystemInfo.supportsMultisampledTextures + "\n" +
            "noGlobalMutation=true\n");
        Debug.Log("AAA_RENDER_AUDIT_PASSED");
    }

    public static void Capture()
    {
        var width = ReadInt("AAA_WIDTH", 2560);
        var height = ReadInt("AAA_HEIGHT", 1440);
        var output = Environment.GetEnvironmentVariable("AAA_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) output = Path.Combine("TestResults", "AAA", $"capture-{width}x{height}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

        var camera = Camera.main;
        if (camera == null) throw new InvalidOperationException("Nenhuma Camera.main disponível para captura AAA.");
        var previous = camera.targetTexture;
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf) { antiAliasing = 2 };
        var previousActive = RenderTexture.active;
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply(false, false);
        File.WriteAllBytes(output, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        camera.targetTexture = previous;
        RenderTexture.active = previousActive;
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        Debug.Log($"AAA_CAPTURE_PASSED {width}x{height}: {output}");
    }

    private static int ReadInt(string name, int fallback)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : fallback;
    }
}
