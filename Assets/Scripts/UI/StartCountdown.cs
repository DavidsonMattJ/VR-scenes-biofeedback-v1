using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Start-scene calibration countdown.
///
/// Shows "Calibrating… 40 seconds remaining" counting down, with the Start button locked.
/// The countdown starts when the first heart-rate reading arrives and lasts ExperimentManager's
/// Calibration Seconds. When it reaches zero AND calibration would succeed, the button unlocks and
/// the text changes to "Press now to begin". If the signal drops, the button locks again until
/// the data are good.
///
/// Put this on the Start scene's canvas (or any object there) and assign the Start button and a label.
/// Leave the button's OnClick wired to ExperimentManager.StartExperiment as it is now.
/// </summary>
public class StartCountdown : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The existing Start button. Its OnClick stays as it is.")]
    public Button startButton;

    [Tooltip("Label for the countdown (TextMeshPro). Can be the button's own text or a separate line above it.")]
    public TMP_Text tmpLabel;

    [Tooltip("Use this instead if the label is a legacy UI Text.")]
    public Text legacyLabel;

    [Tooltip("Leave empty to find it automatically.")]
    public ExperimentManager experimentManager;

    [Header("Messages")]
    public string waitingForSignalText = "Connecting to heart-rate sensor…";
    [Tooltip("{0} is replaced by the seconds remaining.")]
    public string calibratingText = "Calibrating… please sit still and relax.\n{0} seconds remaining";
    public string almostReadyText = "Calibrating… almost ready";
    public string readyText = "Press now to begin";
    public string signalLostText = "Heart-rate signal lost. Please sit still…";

    [Tooltip("How often calibration is checked (s).")]
    public float checkInterval = 0.5f;

    float calibrationSeconds = 60f;
    int minSamples = 40;
    bool requireCalibration = true;

    float firstSampleTime = -1f;
    float nextCheck;
    bool calibrationOk;
    int lastSamples;
    bool ready;
    bool lostAfterReady;

    void Start()
    {
        if (experimentManager == null)
        {
#if UNITY_2023_1_OR_NEWER
            experimentManager = FindFirstObjectByType<ExperimentManager>();
#else
            experimentManager = FindObjectOfType<ExperimentManager>();
#endif
        }

        if (experimentManager != null)
        {
            calibrationSeconds = experimentManager.calibrationSeconds;
            minSamples = experimentManager.minCalibrationSamples;
            requireCalibration = experimentManager.requireCalibration;
        }
        else
        {
            Debug.LogWarning("[Countdown] No ExperimentManager found; using 60 s / 40 samples.");
        }

        if (startButton == null)
            Debug.LogError("[Countdown] Start Button is not assigned.");

        SetButton(!requireCalibration);
        SetLabel(waitingForSignalText);
    }

    void Update()
    {
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + checkInterval;
            CheckCalibration();
        }

        UpdateDisplay();
    }

    void CheckCalibration()
    {
        HeartRateManager hrm = HeartRateManager.Instance;
        calibrationOk = false;
        lastSamples = 0;
        if (hrm == null) return;

        // Same test ExperimentManager runs when Start is clicked, so an unlocked button won't be refused.
        calibrationOk = hrm.TryCalibrate(calibrationSeconds, minSamples, out _, out lastSamples);

        if (firstSampleTime < 0f && lastSamples > 0)
            firstSampleTime = Time.unscaledTime;
    }

    void UpdateDisplay()
    {
        if (firstSampleTime < 0f)
        {
            SetLabel(waitingForSignalText);
            SetButton(!requireCalibration);
            return;
        }

        float remaining = calibrationSeconds - (Time.unscaledTime - firstSampleTime);

        if (remaining > 0f)
        {
            SetLabel(string.Format(calibratingText, Mathf.CeilToInt(remaining)));
            SetButton(!requireCalibration);
            return;
        }

        if (calibrationOk)
        {
            if (!ready) Debug.Log($"[Countdown] Calibration ready ({lastSamples} samples). Start unlocked.");
            ready = true;
            SetLabel(readyText);
            SetButton(true);
            return;
        }

        // Time is up but the data aren't good enough yet (gaps), or the signal dropped after being ready.
        if (ready)
        {
            Debug.LogWarning($"[Countdown] Heart-rate signal lost ({lastSamples}/{minSamples} samples). Start locked.");
            lostAfterReady = true;
        }
        SetLabel(lostAfterReady ? signalLostText : almostReadyText);
        ready = false;
        SetButton(!requireCalibration);
    }

    void SetButton(bool interactable)
    {
        if (startButton != null && startButton.interactable != interactable)
            startButton.interactable = interactable;
    }

    void SetLabel(string text)
    {
        if (tmpLabel != null && tmpLabel.text != text) tmpLabel.text = text;
        if (legacyLabel != null && legacyLabel.text != text) legacyLabel.text = text;
    }
}
