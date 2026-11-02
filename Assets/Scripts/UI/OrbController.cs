using UnityEngine;

public class OrbController : MonoBehaviour
{
    public bool useDiameter = true;
    public bool useHeight = true;
    public bool useColour = true;

    public BiofeedbackManager biofeedbackManager;

    [Header("Heart-rate mapping")]
    [Tooltip("HR at which the orb is smallest / lowest.")]
    public float hrAtMin = 65f;
    [Tooltip("HR at which the orb is largest / highest.")]
    public float hrAtMax = 120f;
    [Tooltip("ON: colour blends smoothly from low to high colour across hrAtMin–hrAtMax. " +
             "OFF: hard switch at Colour Threshold BPM.")]
    public bool continuousColour = true;
    [Tooltip("Only used when Continuous Colour is off.")]
    public float colourThresholdBPM = 80f;

    [Header("Per-participant calibration")]
    [Tooltip("When a baseline was measured at Start, the range becomes " +
             "baseline - Below to baseline + Above, replacing Hr At Min / Max.")]
    public bool useCalibration = true;
    public float bpmBelowBaseline = 10f;
    public float bpmAboveBaseline = 20f;

    [Header("Size")]
    public float minScale = 0.5f;
    public float maxScale = 1.25f;

    [Header("Height")]
    public float minHeight = -1.5f;
    public float maxHeight = 1.5f;

    [Header("Colour")]
    public Color lowHRColour = Color.blue;
    public Color highHRColour = Color.red;

    [Header("Smoothing")]
    public float smoothSpeed = 3f;

    [Header("Floating")]
    public float floatAmount = 0.08f;
    public float floatSpeed = 1.2f;
    public float rotationSpeed = 10f;

    private Vector3 startScale;
    private Vector3 startPosition;
    private Renderer orbRenderer;

    private float currentScale;
    private float currentHeight;
    private float currentColourT = -1f; // -1 = not yet initialised

    void Start()
    {
        startScale = transform.localScale;
        startPosition = transform.localPosition;
        orbRenderer = GetComponent<Renderer>();

        currentScale = 1f;
        currentHeight = 0f;

        if (biofeedbackManager == null)
            biofeedbackManager = FindFirstObjectByType<BiofeedbackManager>();

        if (biofeedbackManager == null)
            Debug.LogError("[Orb] No BiofeedbackManager in this scene - orb will not respond to HR.");
    }

    void Update()
    {
        float hr = biofeedbackManager != null ? biofeedbackManager.DisplayedHeartRate : 0f;
        bool hasHR = hr > 0f;

        // HR-driven size, height and colour only update when there is a value.
        // (Previously the whole Update returned early, so the orb also stopped
        //  floating/rotating - which made the start of async trials look different.)
        if (hasHR)
        {
            float lo = hrAtMin, hi = hrAtMax;
            HeartRateManager hrm = HeartRateManager.Instance;

            if (useCalibration && hrm != null && hrm.IsCalibrated)
            {
                lo = hrm.BaselineHR - bpmBelowBaseline;
                hi = hrm.BaselineHR + bpmAboveBaseline;
            }

            float hrNormalised = Mathf.InverseLerp(lo, hi, hr);

            if (useDiameter)
            {
                float targetScale = Mathf.Lerp(minScale, maxScale, hrNormalised);
                currentScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * smoothSpeed);
                transform.localScale = startScale * currentScale;
            }

            if (useHeight)
            {
                float targetHeight = Mathf.Lerp(minHeight, maxHeight, hrNormalised);
                currentHeight = Mathf.Lerp(currentHeight, targetHeight, Time.deltaTime * smoothSpeed);
            }

            if (useColour && orbRenderer != null)
            {
                Color c;

                if (continuousColour)
                {
                    // Same smoothing as size/height, so all three cues move together.
                    currentColourT = currentColourT < 0f
                        ? hrNormalised
                        : Mathf.Lerp(currentColourT, hrNormalised, Time.deltaTime * smoothSpeed);

                    c = Color.Lerp(lowHRColour, highHRColour, currentColourT);
                }
                else
                {
                    c = hr < colourThresholdBPM ? lowHRColour : highHRColour;
                }

                Material m = orbRenderer.material;

                // URP/HDRP Lit use _BaseColor; Built-in Standard uses _Color.
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            }
        }

        // Ambient motion always runs, in every condition.
        float floatingOffset = Mathf.Sin(Time.time * floatSpeed) * floatAmount;

        transform.localPosition = new Vector3(
            startPosition.x,
            startPosition.y + currentHeight + floatingOffset,
            startPosition.z);

        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
    }
}
