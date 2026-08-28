using UnityEngine;
using System.Diagnostics;
using System.IO;

public class PolarManager : MonoBehaviour
{
    private Process pythonProcess;

    void Start()
    {
        DontDestroyOnLoad(gameObject);
        StartHeartRate();
    }

    void StartHeartRate()
    {
        string projectPath = Directory.GetParent(Application.dataPath).FullName;

        string pythonPath = "python";
        string scriptPath = Path.Combine(projectPath, "Python", "ble_hr_logger.py");
        string csvPath = Path.Combine(projectPath, "Python", "heart_rate.csv");
        string stopFile = Path.Combine(projectPath, "Python", "stop.txt");

        string arguments =
            $"\"{scriptPath}\"" +
            $"--out-csv\"{csvPath}\"" +
            $"--stop-file \"{stopFile}\"";

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        pythonProcess = Process.Start(startInfo);

        UnityEngine.Debug.Log("Heart-rate Python process started");
    }

    private void OnApplicationQuit()
    {
        if (pythonProcess!= null && !pythonProcess.HasExited)
        {
            pythonProcess.Kill();
            pythonProcess.Dispose();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
