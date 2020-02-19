using UnityEditor;
using UnityEngine;
using FantasticSplines;

[CustomPropertyDrawer(typeof(FantasticSplines.SplinePosition))]
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
        
        SerializedProperty splineProp = property.FindPropertyRelative(nameof(SplinePosition.spline));
        SerializedProperty segmentPositionProperty = property.FindPropertyRelative(nameof(SplinePosition.segmentPosition));
        SerializedProperty segIndexProp = segmentPositionProperty.FindPropertyRelative("_index");
        SerializedProperty segTProp = segmentPositionProperty.FindPropertyRelative("_segmentT");
        
        SplineComponent spline = splineProp.objectReferenceValue as SplineComponent;
        SegmentPosition segPos = new SegmentPosition(segIndexProp.intValue, segTProp.floatValue);
        
        position.width = splineWidth;
        EditorGUI.PropertyField(position, splineProp, GUIContent.none);
        
        position.x += position.width;
        position.width = sliderWidth;
        using (new EditorGUI.DisabledScope(spline == null))
        {
            float length = (spline == null) ? 1f : spline.GetLength();
            EditorGUI.BeginChangeCheck();
            float distanceOnSpline = spline.GetDistanceOnSpline(segPos);
            float distance = EditorGUI.Slider(position, GUIContent.none, distanceOnSpline, 0f, length);
            if (EditorGUI.EndChangeCheck())
            {
                segPos = spline.GetSegmentAtDistance(distance);
                segIndexProp.intValue = segPos.index;
                segTProp.floatValue = segPos.segmentT;
            }
        }

        EditorGUI.EndProperty();
    }
}
