using UnityEngine;
using UnityEngine.UI;

public class MoodScaleManager : MonoBehaviour
{
    public Slider moodSlider;
    public Slider calmSlider;
    public Slider attentionSlider;
    public GameObject question3;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (question3 == null)
        {
            question3 = GameObject.Find("Question3");
        }

        if (ExperimentManager.Instance.currentState == ExperimentManager.ExperimentState.PreMood)
        {
            question3.SetActive(false);
        }
        else if (ExperimentManager.Instance.currentState == ExperimentManager.ExperimentState.PostMood)
        {
            question3.SetActive(true);
        }
    }

    public void Submit()
    {
        float mood = moodSlider.value;
        float calm = calmSlider.value;
        float attention = attentionSlider.value;

        Debug.Log("Mood: " + mood);
        Debug.Log("Calm: " + calm);
        Debug.Log("Attention: " + attention);

        ExperimentManager.Instance.ContinueExperiment();
    }
}
