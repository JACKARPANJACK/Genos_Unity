using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LaserShotEffect : MonoBehaviour
{
    [Header("Visuals")]
    public Material beamMaterial;
    public Color beamColor = new Color(1f, 0.5f, 0.1f, 1f);
    public float baseWidth = 0.8f;
    public GameObject muzzleFlashPrefab;
    public GameObject hitImpactPrefab;

    [Header("Optional Shell")]
    public GameObject meshShellPrefab;
    public float shellWidth = 2.5f;

    [Header("Beam Animation")]
    public float growthTime = 0.03f;
    public float beamDuration = 0.08f;
    public float fadeTime = 0.1f;
    public float intensityMultiplier = 15f;
    public float shakeAmount = 0.05f;

    [Header("Flipbook Muzzle")]
    public bool useFlipbookMuzzle = true;
    public float muzzleFlashInterval = 0.05f;
    public int flipbookRows = 4;
    public int flipbookCols = 4;
    public float flipbookFps = 30f;

    [Header("Settings")]
    public float maxDistance = 100f;
    public LayerMask hitMask = -1;

    [Header("Rapid Fire")]
    public float fireRate = 12f;
    public float spread = 1.5f;

    [ContextMenu("Fire Single Shot")]
    public void FireSingle()
    {
        StartCoroutine(ShotRoutine(Vector3.zero));
    }

    [ContextMenu("Fire 10 Shot Burst")]
    public void FireTenBurst()
    {
        StartCoroutine(BurstRoutine(10));
    }

    private IEnumerator BurstRoutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 randomSpread = new Vector3(Random.Range(-spread, spread), Random.Range(-spread, spread), 0);
            StartCoroutine(ShotRoutine(randomSpread));
            yield return new WaitForSeconds(1f / fireRate);
        }
    }

    private IEnumerator ShotRoutine(Vector3 spreadOffset)
    {
        // 1. Calculate direction and raycast
        Quaternion rotation = transform.rotation * Quaternion.Euler(spreadOffset);
        Vector3 direction = rotation * Vector3.forward;
        Vector3 origin = transform.position;

        float finalDistance = maxDistance;
        Vector3 finalHitPoint = origin + direction * maxDistance;

        if (Physics.Raycast(origin + direction * 0.1f, direction, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            finalDistance = hit.distance + 0.1f;
            finalHitPoint = hit.point;
        }

        // 2. Setup Container Object
        GameObject pulseObj = new GameObject("LaserPulse");
        pulseObj.transform.position = origin;
        pulseObj.transform.rotation = rotation;

        // Line Core
        LineRenderer lr = pulseObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, origin);
        lr.SetPosition(1, origin); // Start at 0 length

        lr.material = beamMaterial != null ? new Material(beamMaterial) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.startWidth = baseWidth;
        lr.endWidth = baseWidth;

        // Setup initial color with high intensity
        Color hdrColor = beamColor * intensityMultiplier;
        lr.startColor = hdrColor;
        lr.endColor = hdrColor;

        // Mesh Shell
        Transform shellXform = null;
        Material shellMat = null;
        if (meshShellPrefab != null)
        {
            GameObject shell = Instantiate(meshShellPrefab, pulseObj.transform);
            shell.SetActive(true);
            shellXform = shell.transform;
            shellXform.localScale = new Vector3(shellWidth, shellWidth, 0); // Start at 0 length

            var renderer = shell.GetComponentInChildren<MeshRenderer>();
            if (renderer != null) shellMat = renderer.material;
        }

        // 3. Animation Loop
        float elapsed = 0;
        float muzzleTimer = muzzleFlashInterval; // Force immediate first spawn
        bool impactSpawned = false;

        while (elapsed < growthTime + beamDuration + fadeTime)
        {
            if (pulseObj == null) yield break;
            elapsed += Time.deltaTime;

            // Continuous Muzzle Flash Spawning
            if (elapsed < growthTime + beamDuration)
            {
                muzzleTimer += Time.deltaTime;
                if (muzzleTimer >= muzzleFlashInterval)
                {
                    SpawnMuzzleFlash(origin, rotation);
                    muzzleTimer = 0;
                }
            }

            // Calculate Current Length (Growth Phase)
            float currentDist;
            if (elapsed < growthTime)
            {
                float t = elapsed / growthTime;
                currentDist = Mathf.Lerp(0, finalDistance, t);
            }
            else
            {
                currentDist = finalDistance;

                // Spawn Impact once we hit full length
                if (!impactSpawned && hitImpactPrefab != null)
                {
                    GameObject impact = Instantiate(hitImpactPrefab, finalHitPoint, Quaternion.LookRotation(finalHitPoint - origin));
                    impact.transform.localScale = Vector3.one * 1.5f;
                    if (Application.isPlaying) Destroy(impact, 1.2f);
                    else DestroyImmediate(impact);
                    impactSpawned = true;
                }
            }

            // Alpha/Intensity decay
            float alpha = 1f;
            if (elapsed > growthTime + beamDuration)
            {
                alpha = 1f - (elapsed - (growthTime + beamDuration)) / fadeTime;
                alpha = Mathf.Pow(alpha, 2); // Snappier decay
            }

            // Jitter for violent feel
            Vector3 jitter = Random.insideUnitSphere * shakeAmount * alpha;
            Vector3 currentTarget = origin + (direction * currentDist) + jitter;

            // Update Line
            lr.SetPosition(1, currentTarget);
            lr.startWidth = baseWidth * alpha * (1f + Mathf.Sin(Time.time * 100f) * 0.1f);
            lr.endWidth = lr.startWidth;
            lr.startColor = hdrColor * alpha;
            lr.endColor = lr.startColor;

            // Update Mesh Shell
            if (shellXform != null)
            {
                shellXform.localScale = new Vector3(shellWidth * alpha, shellWidth * alpha, currentDist / 13.4f);
                shellXform.localPosition = Vector3.forward * (currentDist / 2f) + jitter;

                // Animate Shell Material Speeds for "Violent" flow
                if (shellMat != null)
                {
                    if (shellMat.HasProperty("_TextureScrollXSpeed"))
                        shellMat.SetFloat("_TextureScrollXSpeed", 15f * alpha);
                    if (shellMat.HasProperty("_DistortionScrollXSpeed"))
                        shellMat.SetFloat("_DistortionScrollXSpeed", 8f * alpha);
                    if (shellMat.HasProperty("_BaseColor"))
                        shellMat.SetColor("_BaseColor", hdrColor * alpha);
                }
            }

            yield return null;
        }

        if (Application.isPlaying) Destroy(pulseObj);
        else DestroyImmediate(pulseObj);
    }

    private void SpawnMuzzleFlash(Vector3 position, Quaternion rotation)
    {
        if (muzzleFlashPrefab == null) return;

        GameObject muzzle = Instantiate(muzzleFlashPrefab, position, rotation, transform);
        if (useFlipbookMuzzle)
        {
            var mRenderer = muzzle.GetComponentInChildren<Renderer>();
            if (mRenderer != null) StartCoroutine(AnimateFlipbook(mRenderer.material));
        }

        if (Application.isPlaying) Destroy(muzzle, 0.5f);
        else DestroyImmediate(muzzle);
    }

    private IEnumerator AnimateFlipbook(Material mat)
    {
        float frameTime = 1f / flipbookFps;
        int totalFrames = flipbookRows * flipbookCols;
        Vector2 scale = new Vector2(1f / flipbookCols, 1f / flipbookRows);

        string texProp = mat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
        mat.SetTextureScale(texProp, scale);

        for (int i = 0; i < totalFrames; i++)
        {
            if (mat == null) yield break;
            int col = i % flipbookCols;
            int row = i / flipbookCols;
            Vector2 offset = new Vector2((float)col / flipbookCols, 1f - (float)(row + 1) / flipbookRows);
            mat.SetTextureOffset(texProp, offset);
            yield return new WaitForSeconds(frameTime);
        }
    }
}
