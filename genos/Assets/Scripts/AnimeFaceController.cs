using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[System.Serializable]
public class BlendShapeTest
{
    [BlendShapeDropdown]
    public string blendShapeName;
    [Range(0, 100)]
    public float weight;
}

[System.Serializable]
public class AnimeFacialAnimation
{
    public string animationName = "New Expression";

    [Header("BlendShapes")]
    public List<BlendShapeTest> blendShapes = new List<BlendShapeTest>();

    [Header("Textures (-1 to ignore)")]
    public int eyeTextureIndex = -1;
    public int expressionMapIndex = -1;
    public int eyeEmissionIndex = -1;

    [Header("Animation Settings")]
    public float transitionDuration = 0.25f;
    public Ease transitionEase = Ease.InOutSine;
}

[System.Serializable]
public class MaterialTarget
{
    [Tooltip("The mesh renderer for this part.")]
    public SkinnedMeshRenderer renderer;

    [Tooltip("The material slot index on this renderer for the target material. (If the mesh only has 1 material, this should be 0!)")]
    public int materialIndex = 0;

    // We store an instanced version of the material so we don't accidentally edit the global asset
    [HideInInspector] 
    public Material materialInstance;
}

public class AnimeFaceController : MonoBehaviour
{
    [Header("BlendShape Syncing")]
    [Tooltip("All meshes that should sync their facial expressions together (Face, Eyes, Teeth, Tongue, etc.)")]
    public SkinnedMeshRenderer[] blendShapeMeshes;

    [Header("Texture Targets")]
    [Tooltip("Only list the meshes that should receive EYE textures (e.g., Left Eye, Right Eye)")]
    public MaterialTarget[] eyeMeshes;
    
    [Tooltip("Only list the meshes that should receive EXPRESSION/AO textures (e.g., Main Face)")]
    public MaterialTarget[] expressionMeshes;

    [Header("Shader Properties")]
    [Tooltip("The shader property name for Eyes (Usually the Main Texture or an Emission Mask).")]
    public string eyeTextureProperty = "_MainTex";
    [Tooltip("The shader property name for expressions (You mentioned Detail Map, which we renamed to AO Map in the shader).")]
    public string expressionMapProperty = "_AOMap";
    [Tooltip("The shader property name for Eye Emission Textures.")]
    public string eyeEmissionTextureProperty = "_EmissionMap";
    [Tooltip("The shader property name for Emission Color (used for glowing eyes).")]
    public string emissionColorProperty = "_EmissionColor";

    [Header("Facial Animations (Presets)")]
    [Tooltip("Store your custom combinations of blendshapes and textures here. Call PlayFacialAnimation(name) from other scripts.")]
    public List<AnimeFacialAnimation> facialAnimations = new List<AnimeFacialAnimation>();

    [Header("Testing & Debug (Play Mode Only)")]
    [Tooltip("Change this number in the Inspector during Play Mode to instantly test an Eye Texture.")]
    public int testEyeIndex = 0;
    [Tooltip("Change this number in the Inspector during Play Mode to instantly test an Expression Map.")]
    public int testExpressionIndex = 0;

    [Tooltip("Change these colors in the Inspector during Play Mode to instantly test Emission Color PER EYE (Index 0 = Eye 1, Index 1 = Eye 2, etc).")]
    [ColorUsage(true, true)]
    public Color[] testEyeEmissionColors = new Color[2] { Color.white, Color.white };

    [Header("BlendShape Testing (Works in Editor & Play Mode)")]
    [Tooltip("Quickly manage, slide, and test blendshapes seamlessly across all synced meshes via Inspector drop down.")]
    public List<BlendShapeTest> testBlendShapes = new List<BlendShapeTest>();

    [Header("Texture Libraries")]
    public Texture2D[] eyeTextures;
    [ColorUsage(true, true)]
    public Color[] eyeEmissionColors;
    public Texture2D[] expressionMaps;
    public Texture2D[] eyeEmissionTextures;

    private int lastTestEyeIndex = -1;
    private int lastTestExpressionIndex = -1;
    private Color[] lastTestEyeEmissionColors;

    private void Awake()
    {
        lastTestEyeEmissionColors = new Color[testEyeEmissionColors.Length];
        InitTargets(eyeMeshes);
        InitTargets(expressionMeshes);
    }

    private void InitTargets(MaterialTarget[] targets)
    {
        if (targets == null) return;
        foreach (var target in targets)
        {
            if (target.renderer != null && target.renderer.sharedMaterials.Length > target.materialIndex)
            {
                target.materialInstance = target.renderer.materials[target.materialIndex];
            }
            else
            {
                Debug.LogWarning($"AnimeFaceController: Renderer or valid material index missing on {target.renderer?.gameObject.name}. (Did you leave Material Index > 0 on a mesh with only 1 material?)");
            }
        }
    }

    private void Update()
    {
        // Run tests continuously in the editor during Play mode 
        if (Application.isPlaying)
        {
            if (testEyeIndex != lastTestEyeIndex)
            {
                SetEyeTexture(testEyeIndex);
                SetEyeEmissionTexture(testEyeIndex);
                lastTestEyeIndex = testEyeIndex;
            }

            if (testExpressionIndex != lastTestExpressionIndex)
            {
                SetExpressionMap(testExpressionIndex);
                lastTestExpressionIndex = testExpressionIndex;
            }

            if (testEyeEmissionColors != null && eyeMeshes != null)
            {
                if (lastTestEyeEmissionColors == null || lastTestEyeEmissionColors.Length != testEyeEmissionColors.Length)
                    lastTestEyeEmissionColors = new Color[testEyeEmissionColors.Length];

                for (int i = 0; i < Mathf.Min(testEyeEmissionColors.Length, eyeMeshes.Length); i++)
                {
                    if (testEyeEmissionColors[i] != lastTestEyeEmissionColors[i])
                    {
                        SetEyeEmissionColor(i, testEyeEmissionColors[i]);
                        lastTestEyeEmissionColors[i] = testEyeEmissionColors[i];
                    }
                }
            }

            // Sync tests constantly during play mode
            if (testBlendShapes != null)
            {
                foreach (var bs in testBlendShapes)
                {
                    if (!string.IsNullOrEmpty(bs.blendShapeName))
                    {
                        SetBlendShape(bs.blendShapeName, bs.weight);
                    }
                }
            }
        }
    }

    private void OnValidate()
    {
        // Allow testing blendshapes smoothly even outside of play mode (in Editor Window)
        if (testBlendShapes != null && !Application.isPlaying)
        {
            foreach (var bs in testBlendShapes)
            {
                if (!string.IsNullOrEmpty(bs.blendShapeName))
                {
                    SetBlendShape(bs.blendShapeName, bs.weight);
                }
            }
        }
    }

    /// <summary>
    /// Changes the base eye texture on the specific eye meshes
    /// </summary>
    public void SetEyeTexture(int index)
    {
        if (eyeTextures == null || eyeTextures.Length == 0 || eyeMeshes == null) return;
        index = Mathf.Clamp(index, 0, eyeTextures.Length - 1);
        Texture2D tex = eyeTextures[index];
        if (tex == null) return;

        foreach (var target in eyeMeshes)
        {
            if (target.materialInstance != null)
            {
                target.materialInstance.SetTexture(eyeTextureProperty, tex);
                if (eyeEmissionColors != null && index < eyeEmissionColors.Length)
                {
                    target.materialInstance.SetColor(emissionColorProperty, eyeEmissionColors[index]);
                }
            }
        }
    }

    /// <summary>
    /// Changes the expression map on the specific expression meshes (Face)
    /// </summary>
    public void SetExpressionMap(int index)
    {
        if (expressionMaps == null || expressionMaps.Length == 0 || expressionMeshes == null) return;
        index = Mathf.Clamp(index, 0, expressionMaps.Length - 1);
        Texture2D tex = expressionMaps[index];

        foreach (var target in expressionMeshes)
        {
            if (target.materialInstance != null)
            {
                if (tex != null)
                {
                    target.materialInstance.SetTexture(expressionMapProperty, tex);
                    target.materialInstance.SetFloat("_UseAO", 1f);
                    target.materialInstance.EnableKeyword("_USE_AO");
                }
                else
                {
                    // If the texture is None (null), safely disable AO so the face doesn't turn pitch black!
                    target.materialInstance.SetFloat("_UseAO", 0f);
                    target.materialInstance.DisableKeyword("_USE_AO");
                }
            }
        }
    }

    /// <summary>
    /// Changes the emission texture on the specific eye meshes
    /// </summary>
    public void SetEyeEmissionTexture(int index)
    {
        if (eyeEmissionTextures == null || eyeEmissionTextures.Length == 0 || eyeMeshes == null) return;
        index = Mathf.Clamp(index, 0, eyeEmissionTextures.Length - 1);
        Texture2D tex = eyeEmissionTextures[index];

        foreach (var target in eyeMeshes)
        {
            if (target.materialInstance != null)
            {
                target.materialInstance.SetTexture(eyeEmissionTextureProperty, tex);
            }
        }
    }

    /// <summary>
    /// Changes the base emission color (HDR) on all eye meshes homogeneously
    /// </summary>
    public void SetEyeEmissionColor(Color newColor)
    {
        if (eyeMeshes == null) return;

        foreach (var target in eyeMeshes)
        {
            if (target.materialInstance != null)
                target.materialInstance.SetColor(emissionColorProperty, newColor);
        }
    }

    /// <summary>
    /// Changes the base emission color (HDR) on a specific eye mesh index (e.g. index 0 = Left Eye, index 1 = Right Eye)
    /// </summary>
    public void SetEyeEmissionColor(int eyeIndex, Color newColor)
    {
        if (eyeMeshes == null || eyeIndex < 0 || eyeIndex >= eyeMeshes.Length) return;

        var target = eyeMeshes[eyeIndex];
        if (target.materialInstance != null)
        {
            target.materialInstance.SetColor(emissionColorProperty, newColor);
        }
    }

    /// <summary>
    /// Sets a BlendShape weight securely by its string name across all linked meshes.
    /// Safely ignores meshes that don't have that specific BlendShape.
    /// </summary>
    public void SetBlendShape(string blendShapeName, float weight)
    {
        if (blendShapeMeshes == null) return;
        foreach (var smr in blendShapeMeshes)
        {
            if (smr == null || smr.sharedMesh == null) continue;

            int bsIndex = smr.sharedMesh.GetBlendShapeIndex(blendShapeName);
            if (bsIndex != -1)
            {
                smr.SetBlendShapeWeight(bsIndex, weight);
            }
        }
    }

    /// <summary>
    /// Fast direct index setting for BlendShapes across all linked meshes.
    /// Make sure the meshes share the exact same BlendShape index order if used.
    /// </summary>
    public void SetBlendShape(int index, float weight)
    {
        if (blendShapeMeshes == null) return;
        foreach (var smr in blendShapeMeshes)
        {
            if (smr != null && smr.sharedMesh != null && index >= 0 && index < smr.sharedMesh.blendShapeCount)
            {
                smr.SetBlendShapeWeight(index, weight);
            }
        }
    }


    /// <summary>
    /// Plays exactly one predefined facial animation preset by name smoothly using DOTween.
    /// </summary>
    public void PlayFacialAnimation(string animName)
    {
        if (facialAnimations == null) return;

        var anim = facialAnimations.Find(x => x.animationName == animName);
        if (anim == null)
        {
            Debug.LogWarning($"AnimeFaceController: Could not find facial animation '{animName}'");
            return;
        }

        // Texture swaps instantly trigger for snap-like crispness
        if (anim.eyeTextureIndex >= 0) SetEyeTexture(anim.eyeTextureIndex);
        if (anim.expressionMapIndex >= 0) SetExpressionMap(anim.expressionMapIndex);
        if (anim.eyeEmissionIndex >= 0) SetEyeEmissionTexture(anim.eyeEmissionIndex);

        // Map all targeted blendshapes so we know which ones NOT to zero out
        HashSet<string> targetBlendShapes = new HashSet<string>();
        if (anim.blendShapes != null)
        {
            foreach (var bs in anim.blendShapes)
            {
                if (!string.IsNullOrEmpty(bs.blendShapeName))
                {
                    targetBlendShapes.Add(bs.blendShapeName);
                    AnimateBlendShapeTo(bs.blendShapeName, bs.weight, anim.transitionDuration, anim.transitionEase);
                }
            }
        }

        // Smoothly return ALL other active blendshapes back to 0 to prevent terrifying face stacking!
        ResetUnreferencedBlendShapes(targetBlendShapes, anim.transitionDuration, anim.transitionEase);
    }

    private void AnimateBlendShapeTo(string blendShapeName, float targetWeight, float duration, Ease ease)
    {
        if (blendShapeMeshes == null) return;

        foreach (var smr in blendShapeMeshes)
        {
            if (smr == null || smr.sharedMesh == null) continue;

            int bsIndex = smr.sharedMesh.GetBlendShapeIndex(blendShapeName);
            if (bsIndex != -1)
            {
                float startWeight = smr.GetBlendShapeWeight(bsIndex);

                // Kill any active tweens on this specific property for safety
                DOTween.Kill(smr.GetInstanceID() + "_" + bsIndex);

                DOVirtual.Float(startWeight, targetWeight, duration, (v) => {
                    if (smr != null) smr.SetBlendShapeWeight(bsIndex, v);
                })
                .SetEase(ease)
                .SetId(smr.GetInstanceID() + "_" + bsIndex)
                .SetTarget(smr);
            }
        }
    }

    private void ResetUnreferencedBlendShapes(HashSet<string> exclusions, float duration, Ease ease)
    {
        if (blendShapeMeshes == null) return;

        foreach (var smr in blendShapeMeshes)
        {
            if (smr == null || smr.sharedMesh == null) continue;

            for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
            {
                string name = smr.sharedMesh.GetBlendShapeName(i);

                if (!exclusions.Contains(name))
                {
                    float currentWeight = smr.GetBlendShapeWeight(i);
                    // Only animate if it's actually not zero
                    if (currentWeight > 0.01f)
                    {
                        int captureIndex = i; // local capture

                        DOTween.Kill(smr.GetInstanceID() + "_" + captureIndex);
                        DOVirtual.Float(currentWeight, 0f, duration, (v) => {
                            if (smr != null) smr.SetBlendShapeWeight(captureIndex, v);
                        })
                        .SetEase(ease)
                        .SetId(smr.GetInstanceID() + "_" + captureIndex)
                        .SetTarget(smr);
                    }
                }
            }
        }
    }
}