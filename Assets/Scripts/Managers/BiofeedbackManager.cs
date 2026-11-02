using UnityEngine;
using System;
using System.Globalization;
using System.IO;

public enum BiofeedbackType
{
    Synchronous,
    Asynchronous
}

/// <summary>
/// One per environment scene. Decides which heart rate the orb shows:
/// - Synchronous:  the live value.
/// - Asynchronous: the value from delaySeconds ago.
/// The condition is taken from ExperimentManager's current trial, not the Inspector.
/// </summary>
public class BiofeedbackManager : MonoBehaviour
{
    [Header("Biofeedback Condition")]
    [Tooltip("Only used when this scene is played on its own. During the experiment " +
             "the condition comes from ExperimentManager.currentCondition.")]
    public BiofeedbackType feedbackType;

    [Header("Heart Rate")]
    public HeartRateManager heartRateManager;

    [Header("Asynchronous Delay")]
    public float delaySeconds = 10f;

    [Header("Logging")]
    [Tooltip("Writes actual vs displayed HR to displayed_heart_rate.csv in the participant folder.")]
    public bool logDisplayedHeartRate = true;
    public float logIntervalSeconds = 1f;

    public float DisplayedHeartRate { get; private set; }
    public bool SignalOk { get; private set; }

    private StreamWriter displayLog;
    private float nextLogTime;
    private bool warnedNoHistory;
    private bool warnedSignalLost;

    void Start()
    {
        // Always prefer the persistent instance (an Inspector reference may point at
        // a duplicate that has been destroyed).
        if (HeartRateManager.Instance != null)
            heartRateManager = HeartRateManager.Instance;
        else if (heartRateManager == null)
            heartRateManager = FindFirstObjectByType<HeartRateManager>();

        if (heartRateManager == null)
            Debug.LogError("[Biofeedback] NO HEART RATE MANAGER FOUND - did you start from the start scene?");

        // *** Main fix: apply the randomised condition for this trial. ***
        ExperimentManager exp = ExperimentManager.Instance;

        if (exp != null &&
            exp.currentState == ExperimentManager.ExperimentState.Environment &&
            exp.currentCondition != null)
        {
            feedbackType = exp.currentCondition.biofeedback;
        }
        else
        {
            Debug.LogWarning("[Biofeedback] No active trial - using Inspector value: " + feedbackType);
        }

        Debug.Log("[Biofeedback] Condition applied: " + feedbackType +
                  (feedbackType == BiofeedbackType.Asynchronous ? $" (delay {delaySeconds}s)" : ""));

        if (DataManager.Instance != null)
        {
            // Lets you verify after the session that what was shown matches the plan.
            DataManager.Instance.SaveEvent("FeedbackApplied_" + feedbackType);

            if (logDisplayedHeartRate)
                OpenDisplayLog();
        }
    }

    void Update()
    {
        if (heartRateManager == null)
            return;

        float liveHR = heartRateManager.CurrentHeartRate;
        SignalOk = heartRateManager.HasSignal;

        if (feedbackType == BiofeedbackType.Synchronous)
        {
            if (liveHR > 0f)
                DisplayedHeartRate = liveHR;
        }
        else
        {
            if (heartRateManager.TryGetHeartRateAt(Time.time - delaySeconds, out float delayedHR))
            {
                DisplayedHeartRate = delayedHR;
            }
            else if (!warnedNoHistory)
            {
                warnedNoHistory = true;
                Debug.LogWarning($"[Biofeedback] Less than {delaySeconds}s of heart-rate history yet; " +
                                 "orb holds its resting state until there is.");
            }
        }

        if (!SignalOk && !warnedSignalLost)
        {
            warnedSignalLost = true;
            Debug.LogWarning("[Biofeedback] Heart-rate signal lost or stale - check the strap.");
        }
        else if (SignalOk)
        {
            warnedSignalLost = false;
        }

        if (displayLog != null && Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + logIntervalSeconds;
            WriteLogRow(liveHR);
        }
    }

    void OpenDisplayLog()
    {
        try
        {
            string path = Path.Combine(
                DataManager.Instance.GetParticipantFolder(),
                "displayed_heart_rate.csv");

            bool newFile = !File.Exists(path);

            displayLog = new StreamWriter(path, true) { AutoFlush = true };

            if (newFile)
                displayLog.WriteLine(
                    "participant_id,trial_index,environment,biofeedback,actual_hr,displayed_hr,signal_ok,timestamp");
        }
        catch (Exception ex)
        {
            Debug.LogError("[Biofeedback] Could not open display log: " + ex.Message);
            displayLog = null;
        }
    }

    void WriteLogRow(float liveHR)
    {
        ExperimentManager exp = ExperimentManager.Instance;

        string pid = DataManager.Instance != null ? DataManager.Instance.ParticipantID : "";
        string trial = exp != null ? exp.currentTrialIndex.ToString() : "-1";
        string env = (exp != null && exp.currentCondition != null)
            ? exp.currentCondition.environment.ToString()
            : "";

        try
        {
            displayLog.WriteLine(string.Join(",",
                pid,
                trial,
                env,
                feedbackType.ToString(),
                liveHR.ToString("0.##", CultureInfo.InvariantCulture),
                DisplayedHeartRate.ToString("0.##", CultureInfo.InvariantCulture),
                SignalOk ? "1" : "0",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")));
        }
        catch { }
    }

    void OnDestroy()
    {
        try { displayLog?.Dispose(); } catch { }
        displayLog = null;
    }
}
