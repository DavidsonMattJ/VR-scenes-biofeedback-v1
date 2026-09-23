using UnityEngine;

public class OrbController : MonoBehaviour
{
    public bool useDiameter = true;
    public bool useHeight = true;
    public bool useColour = true;

    public BiofeedbackManager biofeedbackManager;

    [Header("Size")]
    public float minScale = 0.5f;
    public float maxScale = 1.25f;

    [Header("Height")]
    public float minHeight = -1f;
    public float maxHeight = 1f;

    [Header("Colour")]
    public Color lowHRColour = Color.blue;
    public Color highHRColour = Color.red;

    [Header("Smoothing")]
    public float smoothSpeed = 3f;

    private Vector3 startScale;
    private Vector3 startPosition;
    private Renderer orbRenderer;

    private float currentScale;
    private float currentHeight;

    void Start()
    {
        startScale = transform.localScale;
        startPosition = transform.localPosition;
        orbRenderer = GetComponent<Renderer>();

        currentScale = 1f;
        currentHeight = 0f;
    }

    void Update()
    {
        if (biofeedbackManager == null)
            return;

        float hr = biofeedbackManager.DisplayedHeartRate;

        if (hr <= 0)
            return;

        float hrNormalised = Mathf.InverseLerp(65f, 80f, hr);

        // SIZE
        if (useDiameter)
        {
            float targetScale = Mathf.Lerp(
                minScale,
                maxScale,
                hrNormalised
            );

            currentScale = Mathf.Lerp(
                currentScale,
                targetScale,
                Time.deltaTime * smoothSpeed
            );

            transform.localScale =
                startScale * currentScale;
        }

        // HEIGHT
        if (useHeight)
        {
            float targetHeight = Mathf.Lerp(
                minHeight,
                maxHeight,
                hrNormalised
            );

            currentHeight = Mathf.Lerp(
                currentHeight,
                targetHeight,
                Time.deltaTime * smoothSpeed
            );

            transform.localPosition = new Vector3(
                startPosition.x,
                startPosition.y + currentHeight,
                startPosition.z
            );
        }

        // COLOUR
        if (useColour && orbRenderer != null)
        {
            if (hr < 72.5f)
            {
                orbRenderer.material.SetColor(
                    "_BaseColor",
                    lowHRColour
                );
            }
            else
            {
                orbRenderer.material.SetColor(
                    "_BaseColor",
                    highHRColour
                );
            }
        }
    }
}
