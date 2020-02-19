using UnityEditor;
using UnityEngine;
using FantasticSplines;

[CustomPropertyDrawer(typeof(SplineComponent.SplinePosition))]
public class SplineComponentPositionEditor : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position = EditorGUI.PrefixLabel(position, label);
        float splineWidth = position.width / 3f;
        float sliderWidth = position.width - splineWidth;
        
        SerializedProperty splineProp = property.FindPropertyRelative(nameof(SplineComponent.SplinePosition.spline));
        SerializedProperty distanceProp = property.FindPropertyRelative(nameof(SplineComponent.SplinePosition.distance));
        SplineComponent spline = splineProp.objectReferenceValue as SplineComponent;
        
        position.width = splineWidth;
        EditorGUI.PropertyField(position, splineProp, GUIContent.none);
        
        position.x += position.width;
        position.width = sliderWidth;
        using (new EditorGUI.DisabledScope(spline == null))
        {
            float length = (spline == null) ? 1f : spline.GetLength();
            EditorGUI.BeginChangeCheck();
            float distance = EditorGUI.Slider(position, GUIContent.none, distanceProp.floatValue, 0f, length);
            if (EditorGUI.EndChangeCheck())
            {
                distanceProp.floatValue = distance;
            }
        }

        EditorGUI.EndProperty();
    }
}
