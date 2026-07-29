using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class HeartRateManager : MonoBehaviour
{
    List<float> heartRates = new List<float>();
    List<float> times = new List<float>();

    private float elapsedTime = 0f;
    public float CurrentHeartRate { get; private set; }

    void Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "HeartRate.csv");
        string[] lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++) 
        {
            string[] values = lines[i].Split(',');
            float time = float.Parse(values[0]);
            float hr = float.Parse(values[1]);
            times.Add(time);
            heartRates.Add(hr);

            Debug.Log("Loaded " + heartRates.Count + " heart rate samples");
        }
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;
        UpdateHeartRate();
        Debug.Log(CurrentHeartRate);
    }

    void UpdateHeartRate()
    {
        for (int i = 0; i < times.Count -1; i++)
        {
            if (elapsedTime >= times[i] && elapsedTime < times [i+1])
            {
                CurrentHeartRate = heartRates[i];
                return;
            }
        }
    }
}
