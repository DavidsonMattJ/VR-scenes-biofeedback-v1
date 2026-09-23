using UnityEngine;

public class OrbController : MonoBehaviour
{
    [Header("Biofeedback Options")]
    public bool useDiameter = true;
    public bool useHeight = true;
    public bool useColour = true;

    [Header("Heart Rate Input")]
    public BiofeedbackManager biofeedbackManager;

    [Header("Heart Rate Range")]
    public float lowHR = 65f;
    public float highHR = 80f;

    [Header("Orb Size")]
    public float minScale = 0.5f;
    public float maxScale = 2.0f;

    [Header("Orb Height")]
    public float minHeight = -0.5f;
    public float maxHeight = 0.5f;

    [Header("Orb Colour")]
    public Color lowHRColour = Color.blue;
    public Color highHRColour = Color.red;

    private Vector3 startScale;
    private Vector3 startPosition;
    private Renderer orbRenderer;

    void Start()
    {
        // Remember the original position and size of the sphere
        startScale = transform.localScale;
        startPosition = transform.localPosition;

        // Get the Renderer from the normal Sphere
        orbRenderer = GetComponent<Renderer>();

        Debug.Log("OrbController started.");
    }

    void Update()
    {
        if (biofeedbackManager == null)
            return;

        // Get the HR currently being displayed
        float hr = biofeedbackManager.DisplayedHeartRate;

        // Convert HR into a value from 0 to 1
        // 65 BPM = 0
        // 80 BPM = 1
        float hrNormalised = Mathf.InverseLerp(
            lowHR,
            highHR,
            hr
        );

        // -------------------------
        // DIAMETER
        // -------------------------

        if (useDiameter)
        {
            float scale = Mathf.Lerp( minScale,maxScale, hrNormalised );

            transform.localScale = startScale * scale;
        }

        // -------------------------
        // HEIGHT
        // -------------------------

        if (useHeight)
        {
            float height = Mathf.Lerp(minHeight,maxHeight,hrNormalised);

            transform.localPosition = new Vector3(startPosition.x,startPosition.y + height,startPosition.z);
        }

        // -------------------------
        // COLOUR
        // -------------------------

        if (useColour && orbRenderer != null) 
        { 
            Color colour; if (hr < (lowHR + highHR) / 2f) { colour = lowHRColour; } else { colour = highHRColour; } orbRenderer.material.color = colour; 
            }
    }
}
