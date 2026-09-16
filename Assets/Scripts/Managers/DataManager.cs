using UnityEngine;
using System.IO;
using System;

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

        // create participant ID
        ParticipantID = "P_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // create participant folder
        string projectPath = Directory.GetParent(Application.dataPath).FullName;

        string dataFolder = Path.Combine(projectPath, "ExperimentData");

        participantFolder = Path.Combine(dataFolder, ParticipantID);

        Directory.CreateDirectory(participantFolder);

        // events csv
        eventsFile = Path.Combine(participantFolder,"events.csv" );

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

    public void SaveMood(
        string moodType,
        float mood,
        float calm,
        float attention
    )
    {
        string environment = "";
        string biofeedback = "";
        string scene = "";

        if (ExperimentManager.Instance != null &&
            ExperimentManager.Instance.currentCondition != null)
        {
            environment =
                ExperimentManager.Instance.currentCondition.environment.ToString();

            biofeedback =
                ExperimentManager.Instance.currentCondition.biofeedback.ToString();

            scene = environment + "Scene";
        }

        int trialIndex = -1;

        if (ExperimentManager.Instance != null)
        {
            trialIndex = ExperimentManager.Instance.currentTrialIndex;
        }

        using (StreamWriter writer = new StreamWriter(eventsFile, true))
        {
            writer.WriteLine(
                $"{ParticipantID}," +
                $"{moodType}," +
                $"{trialIndex}," +
                $"{environment}," +
                $"{biofeedback}," +
                $"{scene}," +
                $"{mood}," +
                $"{calm}," +
                $"{attention}," +
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}"
            );
        }

        Debug.Log("Mood data saved.");
    }

    public void SaveEvent(string eventName)
    {
        string environment = "";
        string biofeedback = "";

        if (ExperimentManager.Instance != null &&
            ExperimentManager.Instance.currentCondition != null)
        {
            environment =
                ExperimentManager.Instance.currentCondition.environment.ToString();

            biofeedback =
                ExperimentManager.Instance.currentCondition.biofeedback.ToString();
        }

        int trialIndex = -1;

        if (ExperimentManager.Instance != null)
        {
            trialIndex = ExperimentManager.Instance.currentTrialIndex;
        }

        using (StreamWriter writer = new StreamWriter(eventsFile, true))
        {
            writer.WriteLine(
                $"{ParticipantID}," +
                $"{eventName}," +
                $"{trialIndex}," +
                $"{environment}," +
                $"{biofeedback}," +
                $"{environment}Scene," +
                ",,," +
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}"
            );
        }
    }

    public string GetParticipantFolder()
    {
        return participantFolder;
    }
}