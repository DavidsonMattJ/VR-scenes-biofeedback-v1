using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Globalization;

public class PolarManager : MonoBehaviour
{
    private Process pythonProcess;

    public float CurrentHeartRate { get; private set; }

    private string csvPath;

    void Start()
    {
        DontDestroyOnLoad(gameObject);

        string projectPath = Directory.GetParent(Application.dataPath).FullName;

        string pythonPath = "python";
        string scriptPath = Path.Combine(projectPath, "Python", "heart_rate.py");
        csvPath = Path.Combine(projectPath, "Python", "heart_rate.csv");
        string stopFile = Path.Combine(projectPath, "Python", "stop.txt");

        string arguments =
            $"\"{scriptPath}\" " +
            $"--out-csv \"{csvPath}\" " +
            $"--stop-file \"{stopFile}\"";

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        pythonProcess = Process.Start(startInfo);

        UnityEngine.Debug.Log("Heart-rate Python process started.");
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
            string[] lines = File.ReadAllLines(csvPath);

            if (lines.Length < 2)
                return;

            string latestLine = lines[lines.Length - 1];

            string[] values = latestLine.Split(',');

            if (values.Length >= 3 && float.TryParse(values[2],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float heartRate))
            {
                CurrentHeartRate = heartRate;
            }
        }
        catch
        {
            // CSV may be being written at the exact moment Unity tries to read it.
        }
    }

    void OnApplicationQuit()
    {
        if (pythonProcess != null && !pythonProcess.HasExited)
        {
            pythonProcess.Kill();
            pythonProcess.Dispose();
        }
    }
}


