using UnityEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Debug = UnityEngine.Debug;

/// <summary>
/// Persistent heart-rate source (lives for the whole session).
/// - PolarStrap: launches the Python BLE logger and reads new rows from heart_rate.csv.
/// - FakeCsv:    plays back Python/fake_heart_rate.csv at one row per second (testing without a strap).
/// Keeps a rolling history so BiofeedbackManager can show a delayed (asynchronous) signal,
/// and so the delay buffer is already full when an environment scene starts.
/// </summary>
public class HeartRateManager : MonoBehaviour
{
    public static HeartRateManager Instance { get; private set; }

    public enum HeartRateSource { PolarStrap, FakeCsv }

    [Header("Source")]
    public HeartRateSource source = HeartRateSource.PolarStrap;

    [Header("Polar strap (Python BLE logger)")]
    public string pythonExecutable = "python";
    public string deviceAddress = "CE:99:0C:02:19:B8";

    [Header("Fake data (testing without a strap)")]
    public string fakeCsvRelativePath = "Python/fake_heart_rate.csv";
    public bool loopFakeData = true;

    [Header("Signal quality")]
    [Tooltip("If no new sample arrives for this many seconds, the signal is treated as lost.")]
    public float staleAfterSeconds = 3f;
    [Tooltip("How often the CSV is checked for new rows.")]
    public float pollIntervalSeconds = 0.2f;

    [Header("History (used for the asynchronous delay)")]
    public float historySeconds = 120f;

    public float CurrentHeartRate { get; private set; }
    public float LastSampleTime { get; private set; } = -999f;
    public int SampleCount { get; private set; }
    public bool HasSignal =>
        CurrentHeartRate > 0f && (Time.time - LastSampleTime) <= staleAfterSeconds;

    /// Participant's resting heart rate, measured when the experiment starts (0 = not calibrated).
    public float BaselineHR { get; private set; }
    public bool IsCalibrated => BaselineHR > 0f;

    private struct Sample
    {
        public float time;
        public float bpm;
        public Sample(float time, float bpm) { this.time = time; this.bpm = bpm; }
    }

    private readonly List<Sample> history = new List<Sample>();

    // Python process
    private Process pythonProcess;
    private string csvPath;
    private string stopFile;
    private string pythonLogPath;
    private StreamWriter pythonLog;
    private readonly object logLock = new object();
    private bool reportedExit;

    // Incremental CSV reading
    private long readPosition;
    private string partialLine = "";
    private float nextPollTime;

    // Fake playback
    private string[] fakeLines;
    private float fakeStartTime;
    private int fakeLastIndex = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // A second copy (e.g. the start scene was loaded again) would launch a
            // second Python process fighting for the same strap.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (Instance != this) return;

        string projectPath = Directory.GetParent(Application.dataPath).FullName;

        if (source == HeartRateSource.FakeCsv)
        {
            StartFake(projectPath);
            return;
        }

        if (DataManager.Instance == null)
        {
            Debug.LogError("[HR] No DataManager found - cannot create participant folder.");
            return;
        }

        string participantFolder = DataManager.Instance.GetParticipantFolder();
        csvPath = Path.Combine(participantFolder, "heart_rate.csv");
        stopFile = Path.Combine(participantFolder, "stop.txt");
        pythonLogPath = Path.Combine(participantFolder, "hr_logger_log.txt");

        if (File.Exists(stopFile))
            File.Delete(stopFile);

        string scriptPath = Path.Combine(projectPath, "Python", "HR LOG", "ble_hr_logger.py");

        if (!File.Exists(scriptPath))
        {
            Debug.LogError("[HR] Python logger script not found: " + scriptPath);
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            // -u = unbuffered, so Python's messages reach the log file immediately
            Arguments =
                $"-u \"{scriptPath}\" " +
                $"--out-csv \"{csvPath}\" " +
                $"--stop-file \"{stopFile}\" " +
                $"--address {deviceAddress}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            pythonLog = new StreamWriter(pythonLogPath, true) { AutoFlush = true };

            pythonProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            pythonProcess.OutputDataReceived += (s, e) => WritePythonLog("OUT", e.Data);
            pythonProcess.ErrorDataReceived += (s, e) => WritePythonLog("ERR", e.Data);
            pythonProcess.Start();
            pythonProcess.BeginOutputReadLine();
            pythonProcess.BeginErrorReadLine();

            Debug.Log("[HR] Python logger started for strap " + deviceAddress);
            Debug.Log("[HR] Reading heart rate from: " + csvPath);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[HR] Could not start Python ('" + pythonExecutable +
                "'). Is Python installed and on PATH? " + ex.Message);
        }
    }

    void Update()
    {
        if (Instance != this) return;

        if (source == HeartRateSource.FakeCsv)
        {
            UpdateFake();
            return;
        }

        if (pythonProcess != null && !reportedExit)
        {
            bool exited;
            try { exited = pythonProcess.HasExited; } catch { exited = false; }

            if (exited)
            {
                reportedExit = true;
                Debug.LogError("[HR] Python logger stopped unexpectedly. See " + pythonLogPath);
            }
        }

        if (Time.time >= nextPollTime)
        {
            nextPollTime = Time.time + pollIntervalSeconds;
            ReadNewLines();
        }
    }

    /// <summary>
    /// Returns the most recent sample recorded at or before <paramref name="time"/> (Unity Time.time).
    /// Returns false if there is not yet enough history to look that far back.
    /// </summary>
    public bool TryGetHeartRateAt(float time, out float bpm)
    {
        bpm = 0f;

        if (history.Count == 0 || history[0].time > time)
            return false;

        for (int i = history.Count - 1; i >= 0; i--)
        {
            if (history[i].time <= time)
            {
                bpm = history[i].bpm;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sets BaselineHR to the median of the samples received in the last <paramref name="seconds"/>.
    /// Median rather than mean so a few artefact beats don't shift it.
    /// Fails if the signal is currently lost or there are fewer than <paramref name="minSamples"/>.
    /// </summary>
    public bool TryCalibrate(float seconds, int minSamples, out float baseline, out int samplesUsed)
    {
        baseline = 0f;
        samplesUsed = 0;

        float from = Time.time - seconds;
        List<float> values = new List<float>();

        foreach (Sample s in history)
        {
            if (s.time >= from)
                values.Add(s.bpm);
        }

        samplesUsed = values.Count;

        if (!HasSignal || values.Count < minSamples)
            return false;

        values.Sort();
        int n = values.Count;
        baseline = n % 2 == 1
            ? values[n / 2]
            : (values[n / 2 - 1] + values[n / 2]) / 2f;

        BaselineHR = baseline;
        return true;
    }

    // ---------- Reading ----------

    /// Reads only the bytes appended since the last poll (the old version re-read the
    /// whole file every frame, which gets slower as the session goes on).
    void ReadNewLines()
    {
        if (string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
            return;

        try
        {
            using (FileStream stream = new FileStream(
                csvPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length < readPosition)
                {
                    // File was recreated - start again from the top.
                    readPosition = 0;
                    partialLine = "";
                }

                if (stream.Length == readPosition)
                    return;

                stream.Seek(readPosition, SeekOrigin.Begin);

                byte[] buffer = new byte[stream.Length - readPosition];
                int read = 0;

                while (read < buffer.Length)
                {
                    int n = stream.Read(buffer, read, buffer.Length - read);
                    if (n <= 0) break;
                    read += n;
                }

                readPosition += read;

                string text = partialLine + Encoding.UTF8.GetString(buffer, 0, read);
                int lastNewline = text.LastIndexOf('\n');

                if (lastNewline < 0)
                {
                    // No complete line yet - Python is mid-write.
                    partialLine = text;
                    return;
                }

                partialLine = text.Substring(lastNewline + 1);

                foreach (string raw in text.Substring(0, lastNewline).Split('\n'))
                    TryParseLine(raw);
            }
        }
        catch (IOException)
        {
            // File busy - try again next poll.
        }
    }

    void TryParseLine(string raw)
    {
        string line = raw.Trim();

        if (line.Length == 0 || line.StartsWith("timestamp"))
            return;

        string[] values = line.Split(',');

        if (values.Length < 3)
            return;

        if (float.TryParse(
                values[2].Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float bpm) && bpm > 0f)
        {
            AddSample(bpm);
        }
    }

    void AddSample(float bpm)
    {
        float now = Time.time;

        CurrentHeartRate = bpm;
        LastSampleTime = now;
        SampleCount++;

        history.Add(new Sample(now, bpm));

        int remove = 0;
        while (remove < history.Count && history[remove].time < now - historySeconds)
            remove++;

        if (remove > 0)
            history.RemoveRange(0, remove);
    }

    // ---------- Fake playback ----------

    void StartFake(string projectPath)
    {
        string path = Path.Combine(projectPath, fakeCsvRelativePath);

        if (!File.Exists(path))
        {
            Debug.LogError("[HR] Fake heart-rate CSV not found: " + path);
            return;
        }

        fakeLines = File.ReadAllLines(path);
        fakeStartTime = Time.time;

        Debug.LogWarning(
            "[HR] USING FAKE HEART-RATE DATA (" + path +
            "). Set Source to PolarStrap before running participants.");
    }

    void UpdateFake()
    {
        if (fakeLines == null || fakeLines.Length < 2)
            return;

        int dataRows = fakeLines.Length - 1;
        int index = Mathf.FloorToInt(Time.time - fakeStartTime);
        index = loopFakeData ? index % dataRows : Mathf.Min(index, dataRows - 1);

        if (index == fakeLastIndex)
            return;

        fakeLastIndex = index;
        TryParseLine(fakeLines[index + 1]); // +1 skips the header row
    }

    // ---------- Python log & shutdown ----------

    void WritePythonLog(string stream, string line)
    {
        if (line == null) return;

        lock (logLock)
        {
            try { pythonLog?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{stream}] {line}"); }
            catch { }
        }
    }

    void OnApplicationQuit()
    {
        if (Instance != this) return;

        if (!string.IsNullOrEmpty(stopFile))
        {
            try { File.WriteAllText(stopFile, "STOP"); } catch { }
        }

        if (pythonProcess != null)
        {
            try
            {
                if (!pythonProcess.HasExited)
                    pythonProcess.WaitForExit(3000);

                if (!pythonProcess.HasExited)
                    pythonProcess.Kill();

                pythonProcess.Dispose();
            }
            catch { }

            pythonProcess = null;
        }

        lock (logLock)
        {
            try { pythonLog?.Dispose(); } catch { }
            pythonLog = null;
        }
    }
}
