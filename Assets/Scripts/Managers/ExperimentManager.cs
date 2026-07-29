using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Collections.Generic;
using static ExperimentManager;

[System.Serializable]
public class TrialCondition
{
    public EnvironmentType environment;
    public BiofeedbackType biofeedback;
    public TrialCondition(EnvironmentType environment, BiofeedbackType biofeedback)
    {
        this.environment = environment;
        this.biofeedback = biofeedback;
    }
}
public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager Instance;
    public List<TrialCondition> trialConditions = new List<TrialCondition>();
    public TrialCondition currentCondition;
    public ExperimentState currentState = ExperimentState.PreMood;
    public int currentTrialIndex = 0;
    public float sceneDuration = 10f;
    public enum ExperimentState
    {
        PreMood = 0,
        Environment = 1,
        PostMood = 2,
        Finished = 3,
    }

    public enum EnvironmentType
    {
        Grey = 0,
        Rainforest = 1,
        Urban = 2
    }

    public enum BiofeedbackType
    {
        Synchronous = 0,
        Asynchronous = 1
    }

    private Coroutine trialTimer;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (trialConditions.Count == 0)
        {
            CreateTrialConditions();
            ShuffleConditions();
        }

        Debug.Log("Experiment Ready!");

    }

    private void CreateTrialConditions()
    {
        trialConditions.Clear();

        trialConditions.Add(
            new TrialCondition(EnvironmentType.Grey, (global::BiofeedbackType)BiofeedbackType.Asynchronous));

        trialConditions.Add(
            new TrialCondition(EnvironmentType.Grey, (global::BiofeedbackType)BiofeedbackType.Synchronous));

        trialConditions.Add(
            new TrialCondition(EnvironmentType.Rainforest, (global::BiofeedbackType)BiofeedbackType.Asynchronous));

        trialConditions.Add(
            new TrialCondition(EnvironmentType.Rainforest, (global::BiofeedbackType)BiofeedbackType.Synchronous));

        trialConditions.Add(
            new TrialCondition(EnvironmentType.Urban, (global::BiofeedbackType)BiofeedbackType.Asynchronous));

        trialConditions.Add(
            new TrialCondition(EnvironmentType.Urban, (global::BiofeedbackType)BiofeedbackType.Synchronous));

        for (int i = 0; i < trialConditions.Count; i++) ;
    }

    private void ShuffleConditions()
    {
        trialConditions = trialConditions.OrderBy(condition => Random.value).ToList();
    }

    private void LoadEnvironment(EnvironmentType environment)
    {
        switch (environment)
        {
            case EnvironmentType.Grey: SceneManager.LoadScene("UrbanScene");
                break;

            case EnvironmentType.Rainforest: SceneManager.LoadScene("RainforestScene");
                break;

            case EnvironmentType.Urban: SceneManager.LoadScene("UrbanScene");
                break;
        }
    }

    public void ContinueExperiment()
    {
        Debug.Log("Current trial index = " + currentTrialIndex);

        if (currentTrialIndex >= trialConditions.Count)
        {
            EndExperiment();
            return;
        }
        currentCondition = trialConditions[currentTrialIndex];
        Debug.Log("Starting trial: " + currentCondition.environment + " - " + currentCondition.biofeedback);
        currentTrialIndex++;
        LoadEnvironment(currentCondition.environment);
    }

    private void EndExperiment()
    {
        Debug.Log("Experiment finished!");
        SceneManager.LoadScene("EndScene");
    }

}
