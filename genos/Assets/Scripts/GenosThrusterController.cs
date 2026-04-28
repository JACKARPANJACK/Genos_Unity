using UnityEngine;
using UnityEngine.VFX;

public class GenosThrusterController : MonoBehaviour
{
    [Header("VFX References")]
    public VisualEffect flameEffect;
    public VisualEffect auraEffect;
    public VisualEffect sparkEffect;
    public ParticleSystem distortionEffect;
    public Light thrusterLight;

    [Header("Intensity Settings")]
    [Range(0f, 1f)]
    public float targetIntensity = 0.5f;
public float rampSpeed = 2f;
    public float pulseSpeed = 5f;
    public float pulseMagnitude = 0.1f;

    [Header("Speed Sensitivity")]
    public bool useSpeedSensitivity = true;
    public float minIntensity = 0.2f;
    public float maxIntensity = 1.0f;
    public float speedThreshold = 20f;

    [Header("Color Gradients")]
    public Gradient coreGradient;
    public Gradient sparkGradient;

    private float currentIntensity = 0f;
    private Vector3 lastPosition;
    private float currentSpeed;

    void Start()
    {
        lastPosition = transform.position;
        InitializeGradients();
    }

    void InitializeGradients()
    {
        if (coreGradient == null)
        {
            coreGradient = new Gradient();
            coreGradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(Color.white, 0.0f), 
                    new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0.2f),
                    new GradientColorKey(new Color(1f, 0.4f, 0f), 0.5f),
                    new GradientColorKey(new Color(0.8f, 0.1f, 0f), 0.8f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1.0f, 0.0f), 
                    new GradientAlphaKey(1.0f, 0.6f), 
                    new GradientAlphaKey(0.0f, 1.0f) 
                }
            );
        }
    }

    void Update()
    {
        // 1. Calculate Velocity
        float dist = Vector3.Distance(transform.position, lastPosition);
        currentSpeed = dist / Time.deltaTime;
        lastPosition = transform.position;

        // 2. Determine target intensity
        float desiredIntensity = targetIntensity;
        if (useSpeedSensitivity)
        {
            float speedFactor = Mathf.Clamp01(currentSpeed / speedThreshold);
            desiredIntensity = Mathf.Lerp(minIntensity, maxIntensity, speedFactor);
        }

        // 3. Smoothly ramp
        currentIntensity = Mathf.MoveTowards(currentIntensity, desiredIntensity, Time.deltaTime * rampSpeed);

        // 4. Pulse
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseMagnitude * currentIntensity;
        float finalIntensity = Mathf.Clamp01(currentIntensity + pulse);

        // 5. Update VFX Parameters
        UpdateVFX(finalIntensity);
    }

    void UpdateVFX(float intensity)
    {
        if (flameEffect != null)
        {
            if (flameEffect.HasFloat("Spawn Rate")) flameEffect.SetFloat("Spawn Rate", intensity * 1000f);
            if (flameEffect.HasFloat("Initial Size")) flameEffect.SetFloat("Initial Size", intensity * 2f);
            if (flameEffect.HasGradient("Fire Colors")) flameEffect.SetGradient("Fire Colors", coreGradient);
        }

        if (auraEffect != null)
        {
            if (auraEffect.HasFloat("Spawn Rate")) auraEffect.SetFloat("Spawn Rate", intensity * 300f);
            if (auraEffect.HasFloat("Initial Size")) auraEffect.SetFloat("Initial Size", intensity * 4f);
        }

        if (sparkEffect != null)
        {
            if (sparkEffect.HasFloat("Spawn Rate")) sparkEffect.SetFloat("Spawn Rate", intensity * 500f);
            if (sparkEffect.HasFloat("Turbulence Intensity")) sparkEffect.SetFloat("Turbulence Intensity", intensity * 15f);
        }

        if (distortionEffect != null)
        {
            var emission = distortionEffect.emission;
            emission.rateOverTime = intensity * 20f;
            var main = distortionEffect.main;
            main.startSize = 2f + intensity * 5f;
        }

        if (thrusterLight != null)
        {
            thrusterLight.intensity = intensity * 150f;
            thrusterLight.range = 5f + intensity * 15f;
        }
    }
    }
