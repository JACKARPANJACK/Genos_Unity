using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class HairPhysicsSystem : MonoBehaviour
{
    [System.Serializable]
    public class HairParticle
    {
        public Transform bone;
        public Vector3 position;
        public Vector3 predictedPosition;
        public Vector3 velocity;
        public float boneLength;
        public Quaternion initialLocalRotation;
        public Vector3 initialLocalPosition;
        public Vector3 restPosition;

        // NEW: Per-bone weight settings
        public float mass = 1.0f;
        [HideInInspector] public float invMass;
    }

    [System.Serializable]
    public class HairStrand
    {
        public Transform rootBone;
        public List<HairParticle> particles = new List<HairParticle>();
    }

    [Header("Setup")]
    [Tooltip("Drag the top-most root bone of each hair strand here.")]
    public List<Transform> hairRoots = new List<Transform>();

    [Header("Mesh Collision")]
    [Tooltip("Assign a Convex MeshCollider, Capsule, or Sphere attached to the head.")]
    public Collider headCollider;
    public float skinWidth = 0.02f;

    [Header("Fallback Sphere Collision")]
    public Transform headTransform;
    public float headRadius = 0.15f;
    public Vector3 headOffset = Vector3.zero;

    [Header("Position Based Dynamics (PBD)")]
    public Vector3 gravity = new Vector3(0, -5.0f, 0); // Lower gravity for anime floatiness
    [Range(0f, 1f)]
    [Tooltip("1 = Floaty/Frictionless, 0.8 = Heavy/Air Resistance")]
    public float damping = 0.95f; // Higher damping value (closer to 1) for bouncier, springy hair
    [Range(1, 10)]
    [Tooltip("Higher = stiffer, more accurate simulation but costs more performance.")]
    public int solverIterations = 4;

    [Header("Shape Retention")]
    [Range(0f, 1f)]
    [Tooltip("How strongly the hair pulls back to its animated shape.")]
    public float shapeStiffness = 0.5f; // Stronger shape retention to maintain the stylized clumps
    [Tooltip("Maximum distance a bone can stray from its animated pose (in meters).")]
    public float maxStretchRadius = 0.1f;

    [Header("Wind System")]
    public Vector3 windDirection = new Vector3(1, 0, 0);
    public float windStrength = 0.5f; // Slight ambient wind for dynamic idle feeling
    public float windTurbulence = 1.0f;
    public float windPulseSpeed = 1.5f;

    [Header("Advanced Shape Conservation")]
    [Range(0f, 1f)]
    [Tooltip("0 = Wet noodle, 1 = Frozen in place. Controls how hard bones fight to keep their original angle.")]
    public float rotationStiffness = 0.7f; // Keep original anime hair volume intact

    [Tooltip("If true, the hair will always try to return to the pose in your Prefab/Model.")]
    public bool matchOriginalLocalRotation = true;

    [Header("High Velocity & Teleport Fixes")]
    [Range(0f, 1f)]
    [Tooltip("0 = Hair reacts to 100% of character movement. 1 = Hair ignores character movement (acts local). ~0.6 gives a nice blend to prevent jumping explosions.")]
    public float movementInertia = 0.65f;
    [Tooltip("If the character moves further than this in a single frame, physics reset (prevents explosions on teleport).")]
    public float teleportThreshold = 1.5f;

    private List<HairStrand> strands = new List<HairStrand>();
    private float windTime;
    private Vector3 lastSystemPosition;

    void Start()
    {
        InitializeBones();
        lastSystemPosition = transform.position;
    }

    // --- INITIALIZATION ---

    private void InitializeBones()
    {
        strands.Clear();

        foreach (Transform root in hairRoots)
        {
            if (root == null) continue;
            HairStrand strand = new HairStrand { rootBone = root };
            // Pass 0 as the starting depth
            BuildChainRecursively(root, strand, null, 0);
            strands.Add(strand);
        }
    }

    private void BuildChainRecursively(Transform currentBone, HairStrand strand, HairParticle parentParticle, int depth)
    {
        HairParticle newParticle = new HairParticle
        {
            bone = currentBone,
            position = currentBone.position,
            initialLocalRotation = currentBone.localRotation,
            initialLocalPosition = currentBone.localPosition,
            // Weighting: Root is mass 1.0, tips get lighter (min 0.1)
            mass = Mathf.Max(0.1f, 1.0f - (depth * 0.15f))
        };

        newParticle.invMass = 1.0f / newParticle.mass;
        newParticle.boneLength = parentParticle != null ? Vector3.Distance(parentParticle.position, currentBone.position) : 0f;

        strand.particles.Add(newParticle);

        if (currentBone.childCount > 0)
        {
            // Recursively find the next bone in the chain
            BuildChainRecursively(currentBone.GetChild(0), strand, newParticle, depth + 1);
        }
    }
    // --- PBD PHYSICS LOOP ---

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt == 0) return;

        windTime += dt * windPulseSpeed;

        // Calculate how much the whole character moved this frame
        Vector3 movementDelta = transform.position - lastSystemPosition;
        lastSystemPosition = transform.position;
        bool wasTeleported = movementDelta.sqrMagnitude > (teleportThreshold * teleportThreshold);

        // 1. Reset bones to their animated local orientations to capture the "Rest Pose"
        foreach (HairStrand strand in strands)
        {
            foreach (HairParticle p in strand.particles)
            {
                p.bone.localPosition = p.initialLocalPosition;
                p.bone.localRotation = p.initialLocalRotation;
            }
        }

        // 2. Capture the exact world-space position the Animator intends for this frame
        foreach (HairStrand strand in strands)
        {
            foreach (HairParticle p in strand.particles)
            {
                p.restPosition = p.bone.position;

                if (wasTeleported)
                {
                    p.position = p.restPosition;
                    p.velocity = Vector3.zero;
                }
                else if (movementInertia > 0f)
                {
                    // Drag the simulated position along with the character to prevent it from getting left behind
                    p.position += movementDelta * movementInertia;
                }
            }
        }

        Vector3 headCenter = headTransform != null ? headTransform.position + headTransform.TransformDirection(headOffset) : Vector3.zero;

        // 3. Execute Dynamics
        foreach (HairStrand strand in strands)
        {
            PredictPositions(strand, dt);
            SolveConstraints(strand, headCenter);
            UpdateVelocitiesAndApply(strand, dt);
        }
    }

    private void PredictPositions(HairStrand strand, float dt)
    {
        for (int i = 1; i < strand.particles.Count; i++)
        {
            HairParticle p = strand.particles[i];

            // Shape retention spring (PGR/Kawaii style) - pulls particles strongly towards animated pose
            Vector3 springForce = ((p.restPosition - p.position) * shapeStiffness) / dt;

            // Organic Wind
            float noise = Mathf.PerlinNoise(p.restPosition.x * windTurbulence + windTime, p.restPosition.y * windTurbulence + windTime);
            Vector3 currentWindForce = windDirection.normalized * (windStrength * noise);

            Vector3 totalForce = gravity + currentWindForce + springForce;

            // Framerate-independent damping
            float drag = 1f - (1f - damping) * (dt * 60f);
            p.velocity *= Mathf.Clamp01(drag);

            // Euler integration
            p.velocity += totalForce * dt;
            p.predictedPosition = p.position + p.velocity * dt;
        }
    }

    private void SolveConstraints(HairStrand strand, Vector3 headCenter)
    {
        for (int iteration = 0; iteration < solverIterations; iteration++)
        {
            strand.particles[0].predictedPosition = strand.particles[0].restPosition;

            for (int i = 1; i < strand.particles.Count; i++)
            {
                HairParticle parent = strand.particles[i - 1];
                HairParticle current = strand.particles[i];

                // --- 1. LOCAL SHAPE CONSERVATION (Stronger roots) ---
                Vector3 targetWorldPos = parent.predictedPosition + parent.bone.TransformDirection(current.initialLocalPosition);
                current.predictedPosition = Vector3.Lerp(current.predictedPosition, targetWorldPos, rotationStiffness * current.mass);

                // --- 2. RIGID LENGTH CONSTRAINT (No stretching anime hair) ---
                Vector3 direction = current.predictedPosition - parent.predictedPosition;
                float currentDistance = direction.magnitude;
                if (currentDistance > 0.0001f)
                {
                    // Enforce exact bone length
                    current.predictedPosition = parent.predictedPosition + (direction / currentDistance) * current.boneLength;
                }

                // --- 3. HARD LIMIT MAX STRETCH RADIUS ---
                Vector3 stretchDir = current.predictedPosition - current.restPosition;
                if (stretchDir.magnitude > maxStretchRadius)
                {
                    current.predictedPosition = current.restPosition + stretchDir.normalized * maxStretchRadius;
                }

                // --- 4. COLLISION ---
                ApplyCollision(current, headCenter);
            }
        }
    }

    private void ApplyCollision(HairParticle current, Vector3 headCenter)
    {
        if (headCollider != null)
        {
            Vector3 surfacePoint = headCollider.ClosestPoint(current.predictedPosition);
            float distToSurface = Vector3.Distance(current.predictedPosition, surfacePoint);

            if (distToSurface < 0.001f)
            {
                Vector3 pushOutDirection = (current.predictedPosition - headCollider.bounds.center).normalized;
                current.predictedPosition += pushOutDirection * (skinWidth + 0.01f);
            }
            else if (distToSurface < skinWidth)
            {
                Vector3 surfaceNormal = (current.predictedPosition - surfacePoint).normalized;
                current.predictedPosition = surfacePoint + surfaceNormal * skinWidth;
            }
        }
        else if (headTransform != null)
        {
            Vector3 toHead = current.predictedPosition - headCenter;
            if (toHead.sqrMagnitude < headRadius * headRadius)
            {
                current.predictedPosition = headCenter + toHead.normalized * headRadius;
            }
        }
    }

    private void UpdateVelocitiesAndApply(HairStrand strand, float dt)
    {
        for (int i = 0; i < strand.particles.Count; i++)
        {
            HairParticle p = strand.particles[i];

            if (i > 0)
            {
                // Derive velocity from constraints
                p.velocity = (p.predictedPosition - p.position) / dt;
            }

            p.position = p.predictedPosition;
            p.bone.position = p.position;
        }

        // Apply Rotations (KawaiiPhysics style bone alignment)
        for (int i = 1; i < strand.particles.Count; i++)
        {
            HairParticle parent = strand.particles[i - 1];
            HairParticle child = strand.particles[i];

            Vector3 restDir = parent.bone.TransformDirection(child.initialLocalPosition).normalized;
            Vector3 simDir = (child.position - parent.position).normalized;

            Quaternion targetRotation = Quaternion.FromToRotation(restDir, simDir) * parent.bone.rotation;
            parent.bone.rotation = targetRotation;
        }

        // Lock root rotation to animation
        strand.particles[0].bone.localRotation = strand.particles[0].initialLocalRotation;

        // Leaf bones fall back to original local rotation
        HairParticle tip = strand.particles[strand.particles.Count - 1];
        if (matchOriginalLocalRotation)
        {
            tip.bone.localRotation = Quaternion.Slerp(tip.bone.localRotation, tip.initialLocalRotation, rotationStiffness);
        }
    }

    // --- DOTWEEN MODIFIERS ---

    public void TweenShapeStiffness(float targetStiffness, float duration)
    {
        DOTween.To(() => shapeStiffness, x => shapeStiffness = x, targetStiffness, duration).SetEase(Ease.InOutSine);
    }

    public void TweenWindStrength(float targetStrength, float duration)
    {
        DOTween.To(() => windStrength, x => windStrength = x, targetStrength, duration).SetEase(Ease.InOutQuad);
    }

    public void TweenGravity(Vector3 targetGravity, float duration)
    {
        DOTween.To(() => gravity, x => gravity = x, targetGravity, duration).SetEase(Ease.InOutQuad);
    }

    // --- DEBUG VISUALIZATION ---

    private void OnDrawGizmos()
    {
        if (headTransform != null && headCollider == null)
        {
            Gizmos.color = Color.yellow;
            Vector3 headCenter = headTransform.position + headTransform.TransformDirection(headOffset);
            Gizmos.DrawWireSphere(headCenter, headRadius);
        }
    }
}