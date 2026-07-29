using UnityEngine;

public enum BiofeedbackType
{
    Synchronous,
    Asynchronous
}

public class BiofeedbackManager : MonoBehaviour
{

    public BiofeedbackType feedbackType;
    public HeartRateManager heartrateManager;

    public float DisplayedHeartRate { get; private set; }

    void Update()
    {
        if (feedbackType == BiofeedbackType.Synchronous)
        {
            DisplayedHeartRate = heartrateManager.CurrentHeartRate;
        }

        else if (feedbackType == BiofeedbackType.Asynchronous)
        {
            DisplayedHeartRate = 70f;
        }
    }
}
