using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Probe opcional para auditoria do Player standalone. Ele só é criado quando o processo recebe
/// -era-audit-seconds=N; em jogo normal não cria HUD, arquivo nem altera o frame rate.
/// </summary>
public sealed class StandalonePerformanceProbe : MonoBehaviour
{
    private readonly List<float> _samples = new List<float>(4096);
    private float _elapsed;
    private float _nextReport;
    private float _duration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Application.isEditor || !Application.isPlaying) return;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("-era-audit-seconds=", StringComparison.OrdinalIgnoreCase)) continue;
            if (!float.TryParse(args[i].Substring("-era-audit-seconds=".Length), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float seconds)) return;
            // A auditoria mede a capacidade de sustentar um alvo conhecido; sem limite o player
            // headless/oculto pode reportar milhares de FPS e o número não representa o jogo.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            GameObject root = new GameObject("StandalonePerformanceProbe");
            DontDestroyOnLoad(root);
            root.AddComponent<StandalonePerformanceProbe>()._duration = Mathf.Clamp(seconds, 3f, 300f);
            return;
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f && dt < 1f) _samples.Add(1f / dt);
        _elapsed += dt;
        if (_elapsed >= _nextReport)
        {
            _nextReport = _elapsed + 1f;
            Debug.Log($"[StandalonePerformanceProbe] t={_elapsed:F1}s frames={_samples.Count} fps={CurrentAverage():F1}");
        }
        if (_elapsed >= _duration) Finish();
    }

    private float CurrentAverage()
    {
        if (_samples.Count == 0) return 0f;
        float total = 0f;
        for (int i = 0; i < _samples.Count; i++) total += _samples[i];
        return total / _samples.Count;
    }

    private void Finish()
    {
        if (_samples.Count == 0) return;
        float min = float.MaxValue, max = 0f, sum = 0f;
        for (int i = 0; i < _samples.Count; i++)
        {
            float fps = _samples[i];
            min = Mathf.Min(min, fps); max = Mathf.Max(max, fps); sum += fps;
        }
        float average = sum / _samples.Count;
        string json = $"{{\"durationSeconds\":{_elapsed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},\"frames\":{_samples.Count},\"fpsMin\":{min.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},\"fpsAverage\":{average.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},\"fpsMax\":{max.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}}}";
        string path = Path.Combine(Application.persistentDataPath, "era-imperial-standalone-performance.json");
        File.WriteAllText(path, json, Encoding.UTF8);
        Debug.Log($"STANDALONE_PERFORMANCE_PASSED {json} file={path}");
        Application.Quit(0);
    }
}
