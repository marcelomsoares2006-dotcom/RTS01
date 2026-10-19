using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class StandalonePerformanceProbe : MonoBehaviour
{
    private readonly List<float> _samples = new List<float>(4096);
    private readonly List<StartupSample> _startupSamples = new List<StartupSample>(256);
    private float _elapsed;
    private float _nextReport;
    private float _duration;
    private float _warmupSeconds = 3f;
    private string _profile = "default";
    private string _quality;
    private string _outputPath;
    private int _requestedVSync;
    private bool _hasRequestedVSync;
    private string _activeScene;
    private float _probeInstallRealtime;
    private bool _finished;

    private struct StartupSample
    {
        public float elapsed;
        public float deltaSeconds;
        public float fps;
        public string phase;
    }

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
            // Reserve the expected sample count up front. This avoids list
            // growth/GC hitches during long probes without changing samples.
            probe._samples.Capacity = Mathf.Max(
                probe._samples.Capacity,
                Mathf.CeilToInt(probe._duration * 60f));
            // Do not log on the installation frame: Unity's managed stack
            // trace for that first Debug.Log can become a measured startup hitch.
            probe._nextReport = 10f;
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
                else if (args[j].StartsWith("-era-audit-vsync=", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(args[j].Substring("-era-audit-vsync=".Length), out int vsync))
                {
                    probe._requestedVSync = Mathf.Max(0, vsync);
                    probe._hasRequestedVSync = true;
                }
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
            if (probe._hasRequestedVSync) QualitySettings.vSyncCount = probe._requestedVSync;
            else QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            probe._activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            probe._probeInstallRealtime = Time.realtimeSinceStartup;
            return;
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _elapsed += dt;
        float fps = dt > 0f && dt < 1f ? 1f / dt : 0f;
        if (_startupSamples.Count < 256)
        {
            _startupSamples.Add(new StartupSample
            {
                elapsed = _elapsed,
                deltaSeconds = dt,
                fps = fps,
                phase = _elapsed < _warmupSeconds ? "warmup" : "sustained"
            });
        }
        if (_elapsed >= _warmupSeconds && dt > 0f && dt < 1f) _samples.Add(1f / dt);
        if (_elapsed >= _nextReport)
        {
            // Unity's file logger captures a managed stack trace for every
            // Debug.Log. A frequent progress report can itself create a
            // frame-time outlier, so keep only a low-frequency heartbeat.
            _nextReport = _elapsed + 60f;
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
        if (_finished) return;
        _finished = true;
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
        string startupCsvPath = Path.Combine(root, "startup-fps-samples.csv");
        using (var startupCsv = new StreamWriter(startupCsvPath, false, Encoding.UTF8))
        {
            startupCsv.WriteLine("sampleIndex,elapsedSeconds,deltaSeconds,fps,phase,scene,vSyncCount,targetFrameRate,quality");
            for (int i = 0; i < _startupSamples.Count; i++)
            {
                StartupSample sample = _startupSamples[i];
                startupCsv.WriteLine(string.Join(",",
                    i,
                    sample.elapsed.ToString("F4", culture),
                    sample.deltaSeconds.ToString("F6", culture),
                    sample.fps.ToString("F4", culture),
                    sample.phase,
                    Csv(_activeScene),
                    QualitySettings.vSyncCount,
                    Application.targetFrameRate,
                    Csv(_quality ?? "unchanged")));
            }
        }

        string json = "{"
            + "\"durationSeconds\":" + _elapsed.ToString("F2", culture)
            + ",\"warmupSeconds\":" + _warmupSeconds.ToString("F2", culture)
            + ",\"frames\":" + _samples.Count
            + ",\"fpsMin\":" + min.ToString("F2", culture)
            + ",\"fpsAverage\":" + average.ToString("F2", culture)
            + ",\"fpsP95\":" + p95.ToString("F2", culture)
            + ",\"fpsP99\":" + p99.ToString("F2", culture)
            + ",\"fpsMax\":" + max.ToString("F2", culture)
            + ",\"profile\":\"" + Json(_profile) + "\""
            + ",\"quality\":\"" + Json(_quality ?? "unchanged") + "\""
            + ",\"scene\":\"" + Json(_activeScene) + "\""
            + ",\"vSyncCount\":" + QualitySettings.vSyncCount
            + ",\"targetFrameRate\":" + Application.targetFrameRate
            + ",\"probeInstallRealtimeSeconds\":" + _probeInstallRealtime.ToString("F3", culture)
            + ",\"startupSampleCount\":" + _startupSamples.Count
            + ",\"samplesPath\":\"" + Json(csvPath) + "\""
            + ",\"startupSamplesPath\":\"" + Json(startupCsvPath) + "\"}";
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

    private static string Json(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}

