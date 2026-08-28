using UnityEngine;
using System.IO;
using System.Globalization;

public class FakeHeartRateManager : MonoBehaviour
{
    public float CurrentHeartRate { get; private set; }

    private string csvPath;

    private string[] lines;
    private int currentLine = 1;

    private float startTime;

    void Start()
    {
        string projectPath = Directory.GetParent(Application.dataPath).FullName;

        csvPath = Path.Combine(
            projectPath,
            "Python",
            "fake_heart_rate.csv"
        );

        if (!File.Exists(csvPath))
        {
            Debug.LogError("Fake heart-rate CSV not found: " + csvPath);
            return;
        }

        lines = File.ReadAllLines(csvPath);

        startTime = Time.time;

        Debug.Log("Fake heart-rate playback started.");
    }

    void Update()
    {
        if (lines == null || lines.Length < 2)
            return;

        float elapsedTime = Time.time - startTime;

        int targetLine = Mathf.FloorToInt(elapsedTime) + 1;

        if (targetLine >= lines.Length)
        {
            targetLine = lines.Length - 1;
        }

        if (targetLine != currentLine)
        {
            currentLine = targetLine;
        }

        string[] values = lines[currentLine].Split(',');

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
}
