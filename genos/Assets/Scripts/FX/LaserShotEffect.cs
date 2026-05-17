using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LaserShotEffect : MonoBehaviour
{
    [Header("Visuals")]
    public Material beamMaterial;
    public Material glowMaterial;
    public Color beamColor = new Color(1f, 0.5f, 0.1f, 1f);
    public float baseWidth = 0.4f;
    public float glowWidth = 0.8f;
    public GameObject muzzleFlashPrefab;
    public GameObject hitImpactPrefab;

    [Header("Optional Shell")]
    public GameObject meshShellPrefab;
    public float shellWidth = 1.8f;

    [Header("Beam Animation")]
    public float growthTime = 0.03f;
    public float beamDuration = 0.08f;
    public float fadeTime = 0.1f;
    public float intensityMultiplier = 25f;
    public float shakeAmount = 0.1f;

    [Header("Flipbook Muzzle")]
    public bool useFlipbookMuzzle = true;
    public float muzzleFlashInterval = 0.06f;
    public int flipbookRows = 4;
    public int flipbookCols = 4;
    public float flipbookFps = 30f;

    [Header("Machine Gun / Barrage")]
    public GameObject projectilePrefab;
    public GameObject fireballPrefab;
    public GameObject fireBulletPrefab;
    public GameObject missilePrefab;
    public GameObject plasmaPrefab;
    public GameObject electricPrefab;
    public GameObject bombPrefab;
    public float projectileSpeed = 150f;
    public float projectileScale = 1.0f;
    public float barrelRecoil = 0.15f;
    public Vector3[] barrelLocalOffsets = new Vector3[] { Vector3.zero };
    public float trajectoryPatternSpeed = 15f;
    public Vector3 trajectoryTilt = new Vector3(0, 0, 0);

    [Header("Targeting")]
    public Transform currentHomingTarget;
    public Vector3 targetOverride;
    public bool isHoming = false;
    public float homingStrength = 8f;

    public enum RangedMode { Laser, Fireball, Fire, Missile, Plasma, Electric, Bomb }
    public RangedMode currentMode = RangedMode.Laser;

    [Header("Settings")]
    public float maxDistance = 100f;
    public LayerMask hitMask = -1;

    [Header("Rapid Fire")]
    public float fireRate = 22f;
    public float spread = 3.5f;

    private Camera mainCam;
    private bool isFiringContinuous = false;
    private Vector3 originalLocalPos;
    private int currentBarrelIndex = 0;
    private float trajectoryTimer = 0f;

    [Header("Muzzle")]
    public Transform muzzleTransform;

    void Awake()
    {
        mainCam = Camera.main;
        originalLocalPos = transform.localPosition;
        
        if (muzzleTransform == null)
        {
            Transform flash = transform.Find("FireMuzzleFlash");
            if (flash != null) muzzleTransform = flash;
            else muzzleTransform = transform;
        }

        if (meshShellPrefab != null && meshShellPrefab.transform.IsChildOf(transform))
        {
            meshShellPrefab.SetActive(false);
        }
    }

    [ContextMenu("Toggle Template Visibility")]
    public void ToggleTemplate()
    {
        if (meshShellPrefab != null)
        {
            meshShellPrefab.SetActive(!meshShellPrefab.activeSelf);
        }
    }

    [ContextMenu("Fire Single Shot")]
    public void FireSingle()
    {
        FireWithSpread();
    }

    [ContextMenu("Fire 10 Shot Burst")]
    public void FireTenBurst()
    {
        StartCoroutine(BurstRoutine(10));
    }

    [ContextMenu("Start Continuous Fire")]
    public void StartFiring()
    {
        if (isFiringContinuous) return;
        isFiringContinuous = true;
        StartCoroutine(ContinuousFireRoutine());
    }

    [ContextMenu("Stop Continuous Fire")]
    public void StopFiring()
    {
        isFiringContinuous = false;
    }

    private IEnumerator ContinuousFireRoutine()
    {
        while (isFiringContinuous)
        {
            trajectoryTimer += Time.deltaTime * trajectoryPatternSpeed;
            
            Vector3 patternOffset = new Vector3(
                Mathf.Sin(trajectoryTimer) * spread,
                Mathf.Cos(trajectoryTimer * 0.7f) * spread,
                0
            ) + trajectoryTilt;

            Vector3 barrelOffset = (barrelLocalOffsets != null && barrelLocalOffsets.Length > 0) ? barrelLocalOffsets[currentBarrelIndex] : Vector3.zero;
            if(barrelLocalOffsets != null && barrelLocalOffsets.Length > 0)
                currentBarrelIndex = (currentBarrelIndex + 1) % barrelLocalOffsets.Length;

            Vector3 finalSpread = patternOffset + new Vector3(
                Random.Range(-spread * 0.1f, spread * 0.1f),
                Random.Range(-spread * 0.1f, spread * 0.1f),
                0
            );

            if (currentMode == RangedMode.Laser)
            {
                StartCoroutine(ShotRoutine(finalSpread, barrelOffset));
                if (projectilePrefab != null) SpawnProjectile(projectilePrefab, finalSpread, barrelOffset);
            }
            else
            {
                GameObject prefabToSpawn = null;
                switch (currentMode)
                {
                    case RangedMode.Fireball: prefabToSpawn = fireballPrefab; break;
                    case RangedMode.Fire: prefabToSpawn = fireBulletPrefab; break;
                    case RangedMode.Missile: prefabToSpawn = missilePrefab; break;
                    case RangedMode.Plasma: prefabToSpawn = plasmaPrefab; break;
                    case RangedMode.Electric: prefabToSpawn = electricPrefab; break;
                    case RangedMode.Bomb: prefabToSpawn = bombPrefab; break;
                }

                if (prefabToSpawn != null)
                {
                    SpawnProjectile(prefabToSpawn, finalSpread, barrelOffset);
                    SpawnMuzzleFlash(transform.TransformPoint(barrelOffset), transform.rotation);
                }
            }

            StopCoroutine("RecoilRoutine");
            StartCoroutine(RecoilRoutine());

            yield return new WaitForSeconds(1f / fireRate);
        }
    }

    public void FireWithSpread()
    {
        Vector3 barrelOffset = (barrelLocalOffsets != null && barrelLocalOffsets.Length > 0) ? barrelLocalOffsets[currentBarrelIndex] : Vector3.zero;
        if(barrelLocalOffsets != null && barrelLocalOffsets.Length > 0)
            currentBarrelIndex = (currentBarrelIndex + 1) % barrelLocalOffsets.Length;

        Vector3 randomSpread = new Vector3(Random.Range(-spread, spread), Random.Range(-spread, spread), 0);
        
        if (currentMode == RangedMode.Laser)
        {
            StartCoroutine(ShotRoutine(randomSpread, barrelOffset));
            if (projectilePrefab != null) SpawnProjectile(projectilePrefab, randomSpread, barrelOffset);
        }
        else
        {
            GameObject prefabToSpawn = null;
            switch (currentMode)
            {
                case RangedMode.Fireball: prefabToSpawn = fireballPrefab; break;
                case RangedMode.Fire: prefabToSpawn = fireBulletPrefab; break;
                case RangedMode.Missile: prefabToSpawn = missilePrefab; break;
                case RangedMode.Plasma: prefabToSpawn = plasmaPrefab; break;
                case RangedMode.Electric: prefabToSpawn = electricPrefab; break;
                case RangedMode.Bomb: prefabToSpawn = bombPrefab; break;
            }

            if (prefabToSpawn != null)
            {
                SpawnProjectile(prefabToSpawn, randomSpread, barrelOffset);
                SpawnMuzzleFlash(transform.TransformPoint(barrelOffset), transform.rotation);
            }
        }

        StopCoroutine("RecoilRoutine");
        StartCoroutine(RecoilRoutine());
    }

    private void SpawnProjectile(GameObject prefab, Vector3 spreadOffset, Vector3 localOffset)
    {
        Vector3 worldPos = muzzleTransform.position;
        Vector3 targetPoint = targetOverride != Vector3.zero ? targetOverride : (worldPos + transform.forward * maxDistance);
        
        if (mainCam == null) mainCam = Camera.main;
        
        if (targetOverride == Vector3.zero)
        {
            Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            int mask = (1 << 0) | (1 << 9) | (1 << 10);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, mask, QueryTriggerInteraction.Ignore)) targetPoint = hit.point;
            else targetPoint = ray.origin + ray.direction * maxDistance;
        }

        if (currentHomingTarget != null) 
        {
            targetPoint = currentHomingTarget.position + Vector3.up * 1.0f;
        }

        Vector3 direction = (targetPoint - worldPos).normalized;
        Quaternion rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(spreadOffset);
        
        GameObject proj = Instantiate(prefab, worldPos, rotation);
        proj.transform.localScale = Vector3.one * projectileScale;
        
        var rb = proj.GetComponent<Rigidbody>();
        if (rb == null) rb = proj.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = (currentMode == RangedMode.Bomb);
        rb.linearVelocity = proj.transform.forward * projectileSpeed;

        if (isHoming && currentHomingTarget != null)
        {
            var homing = proj.AddComponent<HomingProjectile>();
            homing.target = currentHomingTarget;
            homing.speed = projectileSpeed;
            homing.turnSpeed = homingStrength;
        }

        if (currentMode == RangedMode.Bomb)
        {
            rb.linearVelocity += Vector3.up * 5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
        
        foreach(var ps in proj.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.None;
        }

        Collider playerCol = GetComponentInParent<Collider>();
        Collider projCol = proj.GetComponentInChildren<Collider>();
        if (playerCol != null && projCol != null) Physics.IgnoreCollision(playerCol, projCol);

        if (Application.isPlaying) Destroy(proj, 4f);
        else DestroyImmediate(proj);
    }

    private IEnumerator RecoilRoutine()
    {
        float t = 0;
        float duration = 0.04f;
        Vector3 recoilPos = originalLocalPos - Vector3.forward * barrelRecoil;
        
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.localPosition = Vector3.Lerp(originalLocalPos, recoilPos, t);
            yield return null;
        }
        
        t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.localPosition = Vector3.Lerp(recoilPos, originalLocalPos, t);
            yield return null;
        }
        transform.localPosition = originalLocalPos;
    }

    private IEnumerator BurstRoutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            FireWithSpread();
            yield return new WaitForSeconds(1f / fireRate);
        }
    }

    private IEnumerator ShotRoutine(Vector3 spreadOffset, Vector3 localOffset)
    {
        Vector3 origin = muzzleTransform.position;
        Vector3 targetPoint = targetOverride != Vector3.zero ? targetOverride : (origin + transform.forward * maxDistance);
        
        if (mainCam == null) mainCam = Camera.main;
        
        if (targetOverride == Vector3.zero)
        {
            Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            int mask = (1 << 0) | (1 << 9) | (1 << 10);
            if (Physics.Raycast(ray, out RaycastHit hitCam, maxDistance, mask, QueryTriggerInteraction.Ignore)) targetPoint = hitCam.point;
            else targetPoint = ray.origin + ray.direction * maxDistance;
        }

        if (currentHomingTarget != null) 
        {
            targetPoint = currentHomingTarget.position + Vector3.up * 1.0f;
        }

        Vector3 direction = (targetPoint - origin).normalized;
        Quaternion rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(spreadOffset);
        direction = rotation * Vector3.forward;

        float finalDistance = maxDistance;
        Vector3 finalHitPoint = origin + direction * maxDistance;

        if (Physics.Raycast(origin + direction * 0.1f, direction, out RaycastHit hit, maxDistance, (1 << 0) | (1 << 9) | (1 << 10), QueryTriggerInteraction.Ignore))
        {
            finalDistance = hit.distance + 0.1f;
            finalHitPoint = hit.point;
        }

        GameObject pulseObj = new GameObject("LaserPulse");
        pulseObj.transform.position = origin;
        pulseObj.transform.rotation = rotation;

        LineRenderer lr = pulseObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, origin);
        lr.SetPosition(1, origin);

        lr.material = beamMaterial != null ? new Material(beamMaterial) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.startWidth = baseWidth;
        lr.endWidth = baseWidth;

        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(pulseObj.transform);
        LineRenderer glowLr = glowObj.AddComponent<LineRenderer>();
        glowLr.useWorldSpace = true;
        glowLr.positionCount = 2;
        glowLr.SetPosition(0, origin);
        glowLr.SetPosition(1, origin);
        glowLr.material = glowMaterial != null ? new Material(glowMaterial) : lr.material;
        glowLr.startWidth = glowWidth;
        glowLr.endWidth = glowWidth;

        Color hdrColor = beamColor * intensityMultiplier;
        lr.startColor = Color.white;
        lr.endColor = Color.white;
        glowLr.startColor = hdrColor;
        glowLr.endColor = hdrColor;

        Transform shellXform = null;
        List<Material> shellMaterials = new List<Material>();
        if (meshShellPrefab != null)
        {
            GameObject shell = Instantiate(meshShellPrefab, pulseObj.transform);
            shell.SetActive(true);
            shellXform = shell.transform;
            shellXform.localScale = new Vector3(shellWidth, shellWidth, 0);

            foreach (var renderer in shell.GetComponentsInChildren<Renderer>())
            {
                shellMaterials.Add(renderer.material);
            }
            foreach (var ps in shell.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Play();
            }
        }

        float elapsed = 0;
        float muzzleTimer = 999f; 
        bool impactSpawned = false;

        while (elapsed < growthTime + beamDuration + fadeTime)
        {
            if (pulseObj == null) yield break;
            float dt = Application.isPlaying ? Time.deltaTime : 0.02f;
            elapsed += dt;

            if (elapsed < growthTime + beamDuration)
            {
                muzzleTimer += dt;
                if (muzzleTimer >= muzzleFlashInterval)
                {
                    SpawnMuzzleFlash(origin, rotation);
                    muzzleTimer = 0;
                }
            }

            float currentDist;
            if (elapsed < growthTime)
            {
                currentDist = Mathf.Lerp(0, finalDistance, elapsed / growthTime);
            }
            else
            {
                currentDist = finalDistance;
                if (!impactSpawned && hitImpactPrefab != null)
                {
                    GameObject impact = Instantiate(hitImpactPrefab, finalHitPoint, Quaternion.LookRotation(finalHitPoint - origin));
                    impact.transform.localScale = Vector3.one * 1.5f;
                    if (Application.isPlaying) Destroy(impact, 1.2f);
                    else DestroyImmediate(impact);
                    impactSpawned = true;
                }
            }

            float alpha = 1f;
            if (elapsed > growthTime + beamDuration)
            {
                alpha = Mathf.Pow(1f - (elapsed - (growthTime + beamDuration)) / fadeTime, 2);
            }

            float wave = (1f + Mathf.Sin(elapsed * 120f) * 0.15f) * (1f + Random.Range(-0.05f, 0.05f));
            Vector3 jitter = Random.insideUnitSphere * shakeAmount * alpha;
            lr.SetPosition(1, origin + (direction * currentDist) + jitter);
            lr.startWidth = baseWidth * alpha * wave;
            lr.endWidth = lr.startWidth;
            lr.startColor = Color.white * alpha;
            lr.endColor = lr.startColor;

            glowLr.SetPosition(1, origin + (direction * currentDist) + jitter);
            glowLr.startWidth = glowWidth * alpha * wave * (1.1f + Mathf.Cos(elapsed * 80f) * 0.1f);
            glowLr.endWidth = glowLr.startWidth;
            glowLr.startColor = hdrColor * alpha;
            glowLr.endColor = glowLr.startColor;

            if (shellXform != null)
            {
                shellXform.localScale = new Vector3(shellWidth * alpha, shellWidth * alpha, currentDist / 13.4f);
                shellXform.localPosition = Vector3.forward * (currentDist / 2f) + jitter;
                
                foreach (var mat in shellMaterials)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_TextureScrollXSpeed")) mat.SetFloat("_TextureScrollXSpeed", 25f * alpha);
                    if (mat.HasProperty("_DistortionScrollXSpeed")) mat.SetFloat("_DistortionScrollXSpeed", 12f * alpha);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hdrColor * alpha);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", hdrColor * alpha);
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
        muzzle.SetActive(true);

        if (useFlipbookMuzzle)
        {
            var mRenderer = muzzle.GetComponentInChildren<Renderer>();
            if (mRenderer != null)
            {
                Material mat = Application.isPlaying ? mRenderer.material : new Material(mRenderer.sharedMaterial);
                if (!Application.isPlaying) mRenderer.sharedMaterial = mat;
                StartCoroutine(AnimateFlipbook(mat));
            }
        }

        if (Application.isPlaying) Destroy(muzzle, 0.5f);
        else DestroyImmediate(muzzle);
    }

    private IEnumerator AnimateFlipbook(Material mat)
    {
        if (mat == null) yield break;
        float frameTime = 1f / flipbookFps;
        int totalFrames = flipbookRows * flipbookCols;
        Vector2 scale = new Vector2(1f / flipbookCols, 1f / flipbookRows);
        string texProp = mat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
        mat.SetTextureScale(texProp, scale);

        for (int i = 0; i < totalFrames; i++)
        {
            if (mat == null) yield break;
            mat.SetTextureOffset(texProp, new Vector2((float)(i % flipbookCols) / flipbookCols, 1f - (float)(i / flipbookCols + 1) / flipbookRows));
            if (Application.isPlaying) yield return new WaitForSeconds(frameTime);
            else yield return null;
        }
        if (!Application.isPlaying) DestroyImmediate(mat);
    }
}