using UnityEngine;
using System.Collections.Generic;

public enum BiofeedbackType
{
    Synchronous,
    Asynchronous
}

public class BiofeedbackManager : MonoBehaviour
{
    [Header("Biofeedback Condition")]
    public BiofeedbackType feedbackType;

    [Header("Heart Rate")]
    public HeartRateManager heartRateManager;

    [Header("Asynchronous Delay")]
    public float delaySeconds = 10f;

    public float DisplayedHeartRate { get; private set; }
    private Queue<HeartRateSample> heartRateHistory = new Queue<HeartRateSample>();

    private class HeartRateSample
    {
        public float time;
        public float heartRate;
        public HeartRateSample(float time, float heartRate)
        {
            this.time = time;
            this.heartRate = heartRate;
        }

    }

    void Update()
    {
        if (heartRateManager == null)
            return;

        float currentTime = Time.time;
        float currentHR = heartRateManager.CurrentHeartRate;

        // store current real hr
        heartRateHistory.Enqueue(
            new HeartRateSample(currentTime, currentHR)
            );

        if (feedbackType == BiofeedbackType.Synchronous)
        {
            // show real hr data immediately
            DisplayedHeartRate = currentHR;
        }
        else if (feedbackType == BiofeedbackType.Asynchronous)
        {
            // find sample that is 10 sec old
            float targetTime = currentTime - delaySeconds;
            while(
                heartRateHistory.Count > 1 &&
                heartRateHistory.ToArray()[1].time <= targetTime)
            {
                heartRateHistory.Dequeue();
            }

            if (heartRateHistory.Count > 0)
            {
                HeartRateSample oldestSample = heartRateHistory.Peek();
                if (oldestSample.time <= targetTime)
                {
                    DisplayedHeartRate = oldestSample.heartRate;
                }
             }
        }

        while(heartRateHistory.Count > 0 && heartRateHistory.Peek().time < currentTime - 60f)
        {
            heartRateHistory.Dequeue();
        }
    }
}
