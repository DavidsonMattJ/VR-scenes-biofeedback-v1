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
    public float minScale = 0.8f;
    public float maxScale = 1.3f;

    public float minHeight = -0.2f;
    public float maxHeight = 0.2f;

    public Color lowHRColour = Color.blue;
    public Color highHRColour = Color.red;

    private Vector3 startScale;
    private Vector3 startPosition;
    private Renderer orbRenderer;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        startScale = transform.localScale;
        startPosition = transform.localPosition;
        orbRenderer = GetComponent<Renderer>();
    }

    // Update is called once per frame
    void Update()
    {
        float hr = biofeedbackManager.DisplayedHeartRate;
        float hrNormalised = Mathf.InverseLerp(60f, 100f, hr);

        // Diamater
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
                startPosition.z);
        }

        if (useColour && orbRenderer != null)
        {
            orbRenderer.material.color =
                Color.Lerp(lowHRColour, highHRColour, hrNormalised);
        }

    }
}
