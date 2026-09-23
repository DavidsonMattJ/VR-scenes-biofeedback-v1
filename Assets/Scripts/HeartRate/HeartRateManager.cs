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

        string participantFolder =
            DataManager.Instance.GetParticipantFolder();

        csvPath = Path.Combine(
            participantFolder,
            "heart_rate.csv"
        );

        stopFile = Path.Combine(
            participantFolder,
            "stop.txt"
        );

        if (File.Exists(stopFile))
            File.Delete(stopFile);

        string projectPath =
            Directory.GetParent(Application.dataPath).FullName;

        string scriptPath = Path.Combine(
            projectPath,
            "Python",
            "HR LOG",
            "ble_hr_logger.py"
        );

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "python",
            Arguments =
                $"\"{scriptPath}\" " +
                $"--out-csv \"{csvPath}\" " +
                $"--stop-file \"{stopFile}\" " +
                $"--address CE:99:0C:02:19:B8",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        pythonProcess = Process.Start(startInfo);

        UnityEngine.Debug.Log("HR LOGGER STARTED");
        UnityEngine.Debug.Log("READING HR FROM: " + csvPath);
    }

    void Update()
    {
        ReadLatestHeartRate();
    }

    void ReadLatestHeartRate()
    {
        if (string.IsNullOrEmpty(csvPath))
            return;

        if (!File.Exists(csvPath))
            return;

        try
        {
            // Allow Unity to read the file while Python has it open.
            using (FileStream stream = new FileStream(
                csvPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string latestValidLine = null;

                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (line.StartsWith("timestamp"))
                            continue;

                        string[] values = line.Split(',');

                        if (values.Length < 3)
                            continue;

                        string bpmText = values[2].Trim();

                        if (float.TryParse(
                            bpmText,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out float bpm))
                        {
                            latestValidLine = line;
                        }
                    }

                    if (latestValidLine != null)
                    {
                        string[] values =
                            latestValidLine.Split(',');

                        if (float.TryParse(
                            values[2].Trim(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out float bpm))
                        {
                            CurrentHeartRate = bpm;
                        }
                    }
                }
            }
        }
        catch
        {
            // Python may be writing at this exact moment.
            // Try again on the next frame.
        }
    }

    void OnApplicationQuit()
    {
        if (!string.IsNullOrEmpty(stopFile))
        {
            try
            {
                File.WriteAllText(stopFile, "STOP");
            }
            catch
            {
            }
        }

        if (pythonProcess != null)
        {
            try
            {
                if (!pythonProcess.HasExited)
                    pythonProcess.WaitForExit(2000);

                if (!pythonProcess.HasExited)
                    pythonProcess.Kill();

                pythonProcess.Dispose();
            }
            catch
            {
            }
        }
    }
}
