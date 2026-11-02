using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;

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

    [Tooltip("Leave OFF for a fresh random order each participant. " +
             "Turn ON only to run a fixed order typed into Trial Conditions below.")]
    public bool useInspectorOrder = false;

    public List<TrialCondition> trialConditions =
        new List<TrialCondition>();

    public TrialCondition currentCondition;

    public ExperimentState currentState =
        ExperimentState.PreMood;

    public int currentTrialIndex = 0;

    [Tooltip("Seconds per environment. NOTE: the value set on the component in the " +
             "start scene overrides this default - check it there.")]
    public float sceneDuration = 10f;

    [Header("Per-participant calibration")]
    [Tooltip("ON: Start is refused until there is enough clean heart-rate data for a baseline.")]
    public bool requireCalibration = true;
    [Tooltip("Baseline = median HR over this many seconds before Start is clicked.")]
    public float calibrationSeconds = 60f;
    [Tooltip("Minimum samples in that window (the strap sends about 1 per second).")]
    public int minCalibrationSamples = 40;

    private bool started;

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
    }

    private void Start()
    {
        if (Instance != this) return;

        // Previously the list was only built/shuffled if empty, so any entries left in
        // the Inspector silently disabled randomisation.
        if (!useInspectorOrder || trialConditions.Count == 0)
        {
            CreateTrialConditions();
            ShuffleConditions();
        }
        else
        {
            Debug.LogWarning("[Experiment] Using FIXED trial order from the Inspector.");
        }

        Debug.Log("[Experiment] Ready. Order: " + DescribeOrder());

        if (sceneDuration < 60f)
            Debug.LogWarning($"[Experiment] sceneDuration is {sceneDuration}s - is this a test value?");
    }

    public void StartExperiment()
    {
        if (started)
            return; // ignore double clicks

        // Measure this participant's resting HR; the orb's range is centred on it.
        HeartRateManager hrm = HeartRateManager.Instance;
        float baseline = 0f;
        int samples = 0;

        bool calibrated =
            hrm != null &&
            hrm.TryCalibrate(calibrationSeconds, minCalibrationSamples, out baseline, out samples);

        if (!calibrated)
        {
            string msg =
                $"[Experiment] Calibration failed: {samples} heart-rate samples in the last " +
                $"{calibrationSeconds}s (need {minCalibrationSamples}), or the signal is lost.";

            if (requireCalibration)
            {
                Debug.LogError(msg + " Check the strap, wait, then click Start again.");
                return;
            }

            Debug.LogWarning(msg + " Continuing with the fixed orb range.");
        }
        else
        {
            Debug.Log($"[Experiment] Baseline HR = {baseline:0.0} bpm from {samples} samples.");
        }

        started = true;
        currentTrialIndex = 0;
        currentState = ExperimentState.PreMood;

        DataManager.Instance.SaveEvent("TrialOrder_" + DescribeOrder());
        DataManager.Instance.SaveEvent(calibrated
            ? "BaselineHR_" + baseline.ToString("0.0", CultureInfo.InvariantCulture)
            : "BaselineHR_none");

        SceneManager.LoadScene("MoodScene");
    }

    private string DescribeOrder()
    {
        return string.Join("|",
            trialConditions.Select(c => c.environment + "-" + c.biofeedback));
    }

    private void CreateTrialConditions()
    {
        trialConditions.Clear();

        foreach (EnvironmentType env in new[] {
            EnvironmentType.Grey, EnvironmentType.Rainforest, EnvironmentType.Urban })
        {
            trialConditions.Add(new TrialCondition(env, BiofeedbackType.Asynchronous));
            trialConditions.Add(new TrialCondition(env, BiofeedbackType.Synchronous));
        }
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

    private void StartTrial()
    {
        currentCondition = trialConditions[currentTrialIndex];
        currentState = ExperimentState.Environment;

        Debug.Log($"[Experiment] Trial {currentTrialIndex + 1}/{trialConditions.Count}: " +
                  $"{currentCondition.environment} / {currentCondition.biofeedback}");

        LoadEnvironment(currentCondition.environment);

        if (trialTimer != null)
            StopCoroutine(trialTimer);

        trialTimer = StartCoroutine(EnvironmentTimer());
    }

    public void ContinueExperiment()
    {
        switch (currentState)
        {
            // PRE-MOOD -> FIRST ENVIRONMENT
            case ExperimentState.PreMood:
                StartTrial();
                break;

            // ENVIRONMENT -> POST-MOOD (only if something ends the scene early)
            case ExperimentState.Environment:

                if (trialTimer != null)
                {
                    StopCoroutine(trialTimer);
                    trialTimer = null;
                }

                DataManager.Instance.SaveEvent("EnvironmentEndedEarly");

                currentState = ExperimentState.PostMood;
                SceneManager.LoadScene("MoodScene");
                break;

            // POST-MOOD -> NEXT ENVIRONMENT
            case ExperimentState.PostMood:

                currentTrialIndex++;

                if (currentTrialIndex >= trialConditions.Count)
                {
                    EndExperiment();
                    return;
                }

                StartTrial();
                break;
        }
    }

    private IEnumerator EnvironmentTimer()
    {
        yield return new WaitForSeconds(sceneDuration);

        trialTimer = null;

        if (currentState != ExperimentState.Environment)
            yield break;

        DataManager.Instance.SaveEvent("EnvironmentEnd");

        currentState = ExperimentState.PostMood;

        SceneManager.LoadScene("MoodScene");
    }

    private void EndExperiment()
    {
        Debug.Log("[Experiment] Finished!");

        DataManager.Instance.SaveEvent("ExperimentEnd");

        currentState = ExperimentState.Finished;

        SceneManager.LoadScene("EndScene");
    }
}
