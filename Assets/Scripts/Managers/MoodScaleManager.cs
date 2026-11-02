using UnityEngine;
using UnityEngine.UI;

public class MoodScaleManager : MonoBehaviour
{
    public Slider moodSlider;
    public Slider calmSlider;
    public Slider attentionSlider;
    public GameObject question3;

    private bool submitted;

    void Start()
    {
        if (question3 == null)
            question3 = GameObject.Find("Question3");

        if (question3 == null)
        {
            Debug.LogError("[Mood] Question3 not found - assign it in the Inspector.");
            return;
        }

        // Attention question is only asked after an environment.
        question3.SetActive(
            ExperimentManager.Instance.currentState ==
            ExperimentManager.ExperimentState.PostMood);
    }

    public void Submit()
    {
        // A double click / double trigger press used to call ContinueExperiment twice,
        // which could skip straight through an environment.
        if (submitted)
            return;

        submitted = true;

        float mood = moodSlider.value;
        float calm = calmSlider.value;

        // Hidden question is recorded as blank rather than the slider's default value.
        bool attentionAsked = question3 != null && question3.activeInHierarchy;
        float attention = attentionAsked ? attentionSlider.value : float.NaN;

        string moodType = "";

        if (ExperimentManager.Instance.currentState ==
            ExperimentManager.ExperimentState.PreMood)
        {
            moodType = "PreMood";
        }
        else if (ExperimentManager.Instance.currentState ==
                 ExperimentManager.ExperimentState.PostMood)
        {
            moodType = "PostMood";
        }

        Debug.Log($"[Mood] {moodType}: mood={mood} calm={calm} attention={attention}");

        DataManager.Instance.SaveMood(moodType, mood, calm, attention);

        ExperimentManager.Instance.ContinueExperiment();
    }
}
