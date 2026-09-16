using UnityEngine;

public class OrbController : MonoBehaviour
{
    [Header("Biofeedback Options")]
    public bool useDiameter = true;
    public bool useHeight = true;
    public bool useColour = true;

    [Header("Heart Rate Input")]
    public BiofeedbackManager biofeedbackManager;

    [Header("Orb Settings")]
    public float minScale = 0.5f;
    public float maxScale = 10f;

    public float minHeight = -2f;
    public float maxHeight = 2f;

    public Color lowHRColour = Color.blue;
    public Color highHRColour = Color.red;

    private Vector3 startScale;
    private Vector3 startPosition;
    private Renderer orbRenderer;

    void Start()
    {
        startScale = transform.localScale;
        startPosition = transform.localPosition;
        orbRenderer = GetComponent<Renderer>();
    }

    void Update()
    {
        if (biofeedbackManager == null)
            return;

        float hr = biofeedbackManager.DisplayedHeartRate;

        // Change these numbers to define your HR range
        float hrNormalised = Mathf.InverseLerp(65f, 80f, hr);

        if (useDiameter)
        {
            float scale = Mathf.Lerp(minScale, maxScale, hrNormalised);
            transform.localScale = startScale * scale;
        }

        if (useHeight)
        {
            float height = Mathf.Lerp(minHeight, maxHeight, hrNormalised);

            transform.localPosition = new Vector3(
                startPosition.x,
                startPosition.y + height,
                startPosition.z
            );
        }

        if (useColour && orbRenderer != null)
        {
            Color colour = Color.Lerp(
                lowHRColour,
                highHRColour,
                hrNormalised
            );

            orbRenderer.material.SetColor("_BaseColor", colour);
            orbRenderer.material.SetColor("_EmissionColor", colour);
        }
    }
}