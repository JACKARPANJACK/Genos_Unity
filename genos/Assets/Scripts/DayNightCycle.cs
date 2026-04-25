using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [Header("Lighting")]
    [Tooltip("The Directional Light serving as the sun.")]
    public Light sunLight;

    [Header("Time & Rotation")]
    [Tooltip("The current time of day in hours (0 = midnight, 12 = noon).")]
    [Range(0, 24)]
    public float timeOfDay = 12f;

    [Tooltip("The speed at which the day/night cycle progresses. (e.g., 60 means 1 real second = 60 game seconds)")]
    public float timeMultiplier = 60f;

    [Tooltip("The day's rotation offset path on the Y axis.")]
    public float sunYAxisRotation = -30f;

    private void Start()
    {
        // Try to automatically find the sun if it's not assigned
        if (sunLight == null)
        {
            sunLight = RenderSettings.sun;
        }

        if (sunLight == null)
        {
            Debug.LogWarning("No Sun Light assigned in DayNightCycle. Please assign a Directional Light.");
        }
    }

    private void Update()
    {
        // Advance the time of day
        // Time.deltaTime is real seconds. Multiplying by timeMultiplier gives game seconds.
        // Divide by 3600 to convert game seconds to game hours.
        timeOfDay += (Time.deltaTime * timeMultiplier) / 3600f;

        // Wrap around at 24 hours
        if (timeOfDay >= 24f)
        {
            timeOfDay %= 24f;
        }

        UpdateLighting();
    }

    private void UpdateLighting()
    {
        if (sunLight != null)
        {
            // Time ratio: 0.0 at midnight, 0.5 at noon, 1.0 at next midnight
            float timeRatio = timeOfDay / 24f;

            // Map timeRatio to rotation:
            // 0h: -90 deg (pointing up, midnight)
            // 6h: 0 deg (sunrise)
            // 12h: 90 deg (noon)
            // 18h: 180 deg (sunset)
            // 24h: 270 deg (midnight)
            float sunRotation = Mathf.Lerp(-90f, 270f, timeRatio);

            // Apply the rotation
            // We rotate around the X axis for the day/night cycle elevation
            // We use sunYAxisRotation to set the direction the sun rises/sets from
            sunLight.transform.rotation = Quaternion.Euler(sunRotation, sunYAxisRotation, 0f);
        }
    }
}
