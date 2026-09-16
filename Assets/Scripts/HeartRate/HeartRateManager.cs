using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Globalization;

public class HeartRateManager : MonoBehaviour
{
    private Process pythonProcess;

    public float CurrentHeartRate { get; private set; }

    private string csvPath;
    private string stopFile;

    void Start()
    {
        DontDestroyOnLoad(gameObject);

        // Find the Unity project folder
        string projectPath =
            Directory.GetParent(Application.dataPath).FullName;

        // Participant folder created by ExperimentDataManager
        string participantFolder =
            DataManager.Instance.GetParticipantFolder();

        // HR files for this participant
        csvPath = Path.Combine(
            participantFolder,
            "heart_rate.csv"
        );

        stopFile = Path.Combine(
            participantFolder,
            "stop.txt"
        );

        // Python logger
        string pythonPath = "python";

        string scriptPath = Path.Combine(
            projectPath,
            "Python",
            "HR LOG",
            "ble_hr_logger.py"
        );

        string arguments =
            $"\"{scriptPath}\" " +
            $"--out-csv \"{csvPath}\" " +
            $"--stop-file \"{stopFile}\" " +
            $"--address CE:99:0C:02:19:B8";

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        pythonProcess = Process.Start(startInfo);

        UnityEngine.Debug.Log(
            "HW706 heart-rate Python logger started."
        );
    }

    void Update()
    {
        ReadLatestHeartRate();
    }

    void ReadLatestHeartRate()
    {
        if (!File.Exists(csvPath))
            return;

        try
        {
            string[] lines =
                File.ReadAllLines(csvPath);

            if (lines.Length < 2)
                return;

            string latestLine =
                lines[lines.Length - 1];

            string[] values =
                latestLine.Split(',');

            // CSV format:
            // timestamp, unix_s, bpm

            if (values.Length >= 3 &&
                float.TryParse(
                    values[2],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float heartRate))
            {
                CurrentHeartRate = heartRate;
            }
        }
        catch
        {
            // Python may be writing to the CSV at this exact moment.
        }
    }

    void OnApplicationQuit()
    {
        if (pythonProcess != null &&
            !pythonProcess.HasExited)
        {
            pythonProcess.Kill();
            pythonProcess.Dispose();
        }
    }
}
