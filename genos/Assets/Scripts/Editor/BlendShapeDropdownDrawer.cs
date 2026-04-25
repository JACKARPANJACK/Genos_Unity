using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(BlendShapeDropdownAttribute))]
public class BlendShapeDropdownDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType == SerializedPropertyType.String)
        {
            // Find the parent AnimeFaceController to get mesh references
            AnimeFaceController controller = property.serializedObject.targetObject as AnimeFaceController;
            if (controller != null && controller.blendShapeMeshes != null && controller.blendShapeMeshes.Length > 0)
            {
                // Grab the primary face mesh (Index 0) to build the library of names
                SkinnedMeshRenderer smr = controller.blendShapeMeshes[0];
                if (smr != null && smr.sharedMesh != null)
                {
                    Mesh mesh = smr.sharedMesh;
                    int bsCount = mesh.blendShapeCount;
                    if (bsCount > 0)
                    {
                        string[] options = new string[bsCount];
                        for (int i = 0; i < bsCount; i++)
                        {
                            options[i] = mesh.GetBlendShapeName(i);
                        }

                        // Determine dropdown active selection
                        int currentIndex = System.Array.IndexOf(options, property.stringValue);
                        if (currentIndex == -1) currentIndex = 0;

                        // Draw popup
                        currentIndex = EditorGUI.Popup(position, label.text, currentIndex, options);
                        property.stringValue = options[currentIndex];
                        return;
                    }
                }
            }
        }
        
        // Fallback to normal text input if mesh not assigned natively
        EditorGUI.PropertyField(position, property, label);
    }
}