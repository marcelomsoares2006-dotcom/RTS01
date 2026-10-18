using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class StandalonePerformanceProbe : MonoBehaviour
{
    private readonly List<float> _samples = new List<float>(4096);
    private float _elapsed;
    private float _nextReport;
    private float _duration;
    private float _warmupSeconds = 3f;
    private string _profile = "default";
    private string _quality;
    private string _outputPath;

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
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            GameObject root = new GameObject("StandalonePerformanceProbe");
            DontDestroyOnLoad(root);
            var probe = root.AddComponent<StandalonePerformanceProbe>();
            probe._duration = Mathf.Clamp(seconds, 3f, 1800f);
            for (int j = 0; j < args.Length; j++)
            {
                if (args[j].StartsWith("-era-audit-warmup-seconds=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(args[j].Substring("-era-audit-warmup-seconds=".Length), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out probe._warmupSeconds);
                else if (args[j].StartsWith("-era-audit-profile=", StringComparison.OrdinalIgnoreCase))
                    probe._profile = args[j].Substring("-era-audit-profile=".Length);
                else if (args[j].StartsWith("-era-audit-quality=", StringComparison.OrdinalIgnoreCase))
                    probe._quality = args[j].Substring("-era-audit-quality=".Length);
                else if (args[j].StartsWith("-era-audit-output=", StringComparison.OrdinalIgnoreCase))
                    probe._outputPath = args[j].Substring("-era-audit-output=".Length).Trim('"');
            }
            probe._warmupSeconds = Mathf.Clamp(probe._warmupSeconds, 0f, probe._duration - 1f);
            if (!string.IsNullOrWhiteSpace(probe._quality))
            {
                string[] names = QualitySettings.names;
                for (int q = 0; q < names.Length; q++)
                {
                    if (!string.Equals(names[q], probe._quality, StringComparison.OrdinalIgnoreCase)) continue;
                    QualitySettings.SetQualityLevel(q, true);
                    break;
                }
            }
            // QualitySettings pode restaurar VSync ao trocar de preset. Reaplique
            // o teto da auditoria depois da seleção para evitar FPS artificial.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            return;
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _elapsed += dt;
        if (_elapsed >= _warmupSeconds && dt > 0f && dt < 1f) _samples.Add(1f / dt);
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
            min = Mathf.Min(min, fps);
            max = Mathf.Max(max, fps);
            sum += fps;
        }

        float average = sum / _samples.Count;
        List<float> ordered = new List<float>(_samples);
        ordered.Sort();
        float p95 = Percentile(ordered, 0.95f);
        float p99 = Percentile(ordered, 0.99f);

        string root = string.IsNullOrWhiteSpace(_outputPath) ? Application.persistentDataPath : _outputPath;
        Directory.CreateDirectory(root);
        string csvPath = Path.Combine(root, "long-fps-samples.csv");
        using (var csv = new StreamWriter(csvPath, false, Encoding.UTF8))
        {
            csv.WriteLine("sampleIndex,fps");
            for (int i = 0; i < _samples.Count; i++)
                csv.WriteLine($"{i},{_samples[i].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
        }

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string json = "{"
            + "\"durationSeconds\":" + _elapsed.ToString("F2", culture)
            + ",\"warmupSeconds\":" + _warmupSeconds.ToString("F2", culture)
            + ",\"frames\":" + _samples.Count
            + ",\"fpsMin\":" + min.ToString("F2", culture)
            + ",\"fpsAverage\":" + average.ToString("F2", culture)
            + ",\"fpsP95\":" + p95.ToString("F2", culture)
            + ",\"fpsP99\":" + p99.ToString("F2", culture)
            + ",\"fpsMax\":" + max.ToString("F2", culture)
            + ",\"profile\":\"" + _profile + "\""
            + ",\"quality\":\"" + (_quality ?? "unchanged") + "\""
            + ",\"samplesPath\":\"" + csvPath + "\"}";
        string path = Path.Combine(root, "era-imperial-standalone-performance.json");
        File.WriteAllText(path, json, Encoding.UTF8);
        Debug.Log($"STANDALONE_PERFORMANCE_PASSED {json} file={path}");
        Application.Quit(0);
    }

    private static float Percentile(List<float> ordered, float percentile)
    {
        if (ordered.Count == 0) return 0f;
        float index = percentile * (ordered.Count - 1);
        int lower = Mathf.FloorToInt(index);
        int upper = Mathf.Min(lower + 1, ordered.Count - 1);
        return Mathf.Lerp(ordered[lower], ordered[upper], index - lower);
    }
}

