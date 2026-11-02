using UnityEngine;
using System.IO;
using System;
using System.Globalization;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance;

    public string ParticipantID { get; private set; }

    private string participantFolder;
    private string eventsFile;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ParticipantID = "P_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

        string projectPath = Directory.GetParent(Application.dataPath).FullName;
        string dataFolder = Path.Combine(projectPath, "ExperimentData");
        participantFolder = Path.Combine(dataFolder, ParticipantID);

        Directory.CreateDirectory(participantFolder);

        eventsFile = Path.Combine(participantFolder, "events.csv");

        CreateEventsFile();

        Debug.Log("Participant folder: " + participantFolder);
        Debug.Log("Participant ID: " + ParticipantID);
    }

    private void CreateEventsFile()
    {
        using (StreamWriter writer = new StreamWriter(eventsFile, false))
        {
            writer.WriteLine(
                "participant_id,event,trial_index,environment,biofeedback,scene,mood,calm,attention,timestamp"
            );
        }
    }

    /// <summary>
    /// Condition columns are only filled while a trial is running or just finished.
    /// (currentCondition is a serialized field, so it is never null - before this check
    ///  the PreMood row was wrongly labelled Grey / Synchronous.)
    /// </summary>
    private void GetCondition(
        out string environment,
        out string biofeedback,
        out string scene,
        out int trialIndex)
    {
        environment = "";
        biofeedback = "";
        scene = "";
        trialIndex = -1;

        ExperimentManager exp = ExperimentManager.Instance;

        if (exp == null)
            return;

        bool inTrial =
            exp.currentState == ExperimentManager.ExperimentState.Environment ||
            exp.currentState == ExperimentManager.ExperimentState.PostMood;

        if (!inTrial || exp.currentCondition == null)
            return;

        trialIndex = exp.currentTrialIndex;
        environment = exp.currentCondition.environment.ToString();
        biofeedback = exp.currentCondition.biofeedback.ToString();
        scene = environment + "Scene";
    }

    private static string Fmt(float value)
    {
        return float.IsNaN(value) ? "" : value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Now()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    public void SaveMood(string moodType, float mood, float calm, float attention)
    {
        GetCondition(out string environment, out string biofeedback,
                     out string scene, out int trialIndex);

        using (StreamWriter writer = new StreamWriter(eventsFile, true))
        {
            writer.WriteLine(string.Join(",",
                ParticipantID,
                moodType,
                trialIndex.ToString(),
                environment,
                biofeedback,
                scene,
                Fmt(mood),
                Fmt(calm),
                Fmt(attention),
                Now()));
        }

        Debug.Log("Mood data saved.");
    }

    public void SaveEvent(string eventName)
    {
        GetCondition(out string environment, out string biofeedback,
                     out string scene, out int trialIndex);

        using (StreamWriter writer = new StreamWriter(eventsFile, true))
        {
            writer.WriteLine(string.Join(",",
                ParticipantID,
                eventName,
                trialIndex.ToString(),
                environment,
                biofeedback,
                scene,
                "", "", "",
                Now()));
        }
    }

    public string GetParticipantFolder()
    {
        return participantFolder;
    }
}
