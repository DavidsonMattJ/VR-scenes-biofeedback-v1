using UnityEngine;

public enum BiofeedbackType
{
    Synchronous,
    Asynchronous
}

public class BiofeedbackManager : MonoBehaviour
{
    public BiofeedbackType feedbackType;

    public PolarManager heartRateManager;
    public FakeHeartRateManager fakeHeartRateManager;

    public float DisplayedHeartRate { get; private set; }

    void Update()
    {
        if (feedbackType == BiofeedbackType.Synchronous)
        {
            DisplayedHeartRate = heartRateManager.CurrentHeartRate;
        }
        else if (feedbackType == BiofeedbackType.Asynchronous)
        {
            DisplayedHeartRate = fakeHeartRateManager.CurrentHeartRate;
        }
    }
}
