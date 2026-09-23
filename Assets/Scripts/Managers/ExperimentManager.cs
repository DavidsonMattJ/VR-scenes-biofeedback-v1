using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

[System.Serializable]
public class TrialCondition
{
    public ExperimentManager.EnvironmentType environment;
    public BiofeedbackType biofeedback;

    public TrialCondition(
        ExperimentManager.EnvironmentType environment,
        BiofeedbackType biofeedback)
    {
        this.environment = environment;
        this.biofeedback = biofeedback;
    }
}

public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager Instance;

    public List<TrialCondition> trialConditions =
        new List<TrialCondition>();

    public TrialCondition currentCondition;

    public ExperimentState currentState =
        ExperimentState.PreMood;

    public int currentTrialIndex = 0;

    public float sceneDuration = 10f;

    public enum ExperimentState
    {
        PreMood = 0,
        Environment = 1,
        PostMood = 2,
        Finished = 3
    }

    public enum EnvironmentType
    {
        Grey = 0,
        Rainforest = 1,
        Urban = 2
    }

    private Coroutine trialTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log(
            "ExperimentManager Instance created: " + Instance
        );
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

    public void StartExperiment()
    {
        currentTrialIndex = 0;
        currentState = ExperimentState.PreMood;

        SceneManager.LoadScene("MoodScene");
    }

    private void CreateTrialConditions()
    {
        trialConditions.Clear();

        trialConditions.Add(
            new TrialCondition(
                EnvironmentType.Grey,
                global::BiofeedbackType.Asynchronous
            )
        );

        trialConditions.Add(
            new TrialCondition(
                EnvironmentType.Grey,
                global::BiofeedbackType.Synchronous
            )
        );

        trialConditions.Add(
            new TrialCondition(
                EnvironmentType.Rainforest,
                global::BiofeedbackType.Asynchronous
            )
        );

        trialConditions.Add(
            new TrialCondition(
                EnvironmentType.Rainforest,
                global::BiofeedbackType.Synchronous
            )
        );

        trialConditions.Add(
            new TrialCondition(
                EnvironmentType.Urban,
                global::BiofeedbackType.Asynchronous
            )
        );

        trialConditions.Add(
            new TrialCondition(
                EnvironmentType.Urban,
                global::BiofeedbackType.Synchronous
            )
        );
    }

    private void ShuffleConditions()
    {
        trialConditions =
            trialConditions
            .OrderBy(condition => Random.value)
            .ToList();
    }

    private void LoadEnvironment(EnvironmentType environment)
    {
        DataManager.Instance.SaveEvent("EnvironmentStart");

        switch (environment)
        {
            case EnvironmentType.Grey:
                SceneManager.LoadScene("GreyScene");
                break;

            case EnvironmentType.Rainforest:
                SceneManager.LoadScene("RainforestScene");
                break;

            case EnvironmentType.Urban:
                SceneManager.LoadScene("UrbanScene");
                break;
        }
    }

    public void ContinueExperiment()
    {
        switch (currentState)
        {
            // PRE-MOOD → FIRST ENVIRONMENT
            case ExperimentState.PreMood:

                currentCondition =
                    trialConditions[currentTrialIndex];

                currentState =
                    ExperimentState.Environment;

                LoadEnvironment(
                    currentCondition.environment
                );

                StartCoroutine(EnvironmentTimer());

                break;


            // ENVIRONMENT → POST-MOOD
            case ExperimentState.Environment:

                currentState =
                    ExperimentState.PostMood;

                SceneManager.LoadScene("MoodScene");

                break;


            // POST-MOOD → NEXT ENVIRONMENT
            case ExperimentState.PostMood:

                currentTrialIndex++;

                // Are we finished?
                if (currentTrialIndex >= trialConditions.Count)
                {
                    EndExperiment();
                    return;
                }

                // Set up next condition
                currentCondition =
                    trialConditions[currentTrialIndex];

                currentState =
                    ExperimentState.Environment;

                LoadEnvironment(
                    currentCondition.environment
                );

                StartCoroutine(EnvironmentTimer());

                break;
        }
    }

    private IEnumerator EnvironmentTimer()
    {
        yield return new WaitForSeconds(sceneDuration);

        DataManager.Instance.SaveEvent(
            "EnvironmentEnd"
        );

        currentState =
            ExperimentState.PostMood;

        SceneManager.LoadScene("MoodScene");
    }

    private void EndExperiment()
    {
        Debug.Log("Experiment finished!");

        DataManager.Instance.SaveEvent(
            "ExperimentEnd"
        );

        currentState =
            ExperimentState.Finished;

        SceneManager.LoadScene("EndScene");
    }
}

