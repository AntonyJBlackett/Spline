using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
#endif

namespace FantasticSplines
{
    // Tagging a class with the EditorTool attribute and no target type registers a global tool. Global tools are valid for any selection, and are accessible through the top left toolbar in the editor.
#if UNITY_EDITOR

    [CustomEditor( typeof( SplineNormal ) )]
    public class SplineNormalEditor : KeyframedSplineParameterEditor
    {
    }


    [EditorTool( "Spline Normal Tool", typeof( SplineNormal ) )]
    class SplineNormalTool : KeyframedSplineParameterTool<Normal>
    {
        SplineNormal SplineNormal
        {
            get
            {
                return Target as SplineNormal;
            }
        }

        #region Custom tool handles
        protected override bool DoToolHandles( EditorWindow window )
        {
            bool keepActive = base.DoKeyframeToolHandles();

            if( SplineNormal.enableKeyframeHandles )
            {
                var keys = SplineNormal.Keyframes;
                Color rotationHandleColour = Color.green;
                rotationHandleColour.a = 0.5f;
                using( new Handles.DrawingScope( rotationHandleColour ) )
                {
                    for( int i = 0; i < keys.Count; ++i )
                    {
                        EditorGUI.BeginChangeCheck();

                        Vector3 position = keys[i].location.position;
                        Vector3 tangent = keys[i].location.tangent;
                        Vector3 normal = SplineNormal.GetNormal( keys[i] );

                        Vector3 discNormal = tangent.normalized;
                        float discRadius = SplineNormal.GetNormalGizmoScale() * GetHandleSize( position ) * 10;

                        Quaternion normalRotation = Quaternion.LookRotation( normal, tangent.normalized );
                        Quaternion changedRotation = Handles.Disc( normalRotation, position, discNormal, discRadius, false, 0 );

                        if( EditorGUI.EndChangeCheck() )
                        {
                            keepActive = true;
                            Undo.RecordObject( SplineNormal, "Rotate Spline Normal" );
                            SplineNormal.SetKeyframeValue( i, new Normal() { blendType = keys[i].value.blendType, angle = keys[i].value.angle + Vector3.SignedAngle( normalRotation * Vector3.forward, changedRotation * Vector3.forward, keys[i].location.tangent ) } );
                        }
                    }
                }
            }

            return keepActive;
        }

        protected override float GetGUIPropertyWidth()
        {
            return 100;
        }

        protected override bool DoToolGUI( EditorWindow window )
        {
            SerializedObject so = new SerializedObject( Target );
            var rawKeysSP = so.FindProperty( "rawKeyframes" );
            var keys = Target.Keyframes;

            if( keys.Count > 0 )
            {
                float handleSize = SplineHandleUtility.GetNodeHandleSize( keys[0].location.position );

                for( int i = 0; i < keys.Count; ++i )
                {
                    SerializedProperty keySP = rawKeysSP.GetArrayElementAtIndex( i );
                    SerializedProperty keyValue = keySP.FindPropertyRelative( "value" );
                    SerializedProperty angle = keyValue.FindPropertyRelative( "angle" );
                    SerializedProperty blendType = keyValue.FindPropertyRelative( "blendType" );
                    float propertyHeight = EditorGUI.GetPropertyHeight( angle, new GUIContent( "" ), true );
                    propertyHeight += EditorGUI.GetPropertyHeight( blendType, new GUIContent( "" ), true );

                    Vector2 guiPosition = HandleUtility.WorldToGUIPoint( keys[i].location.position ) + new Vector2( 0.5f, 0.5f ) * handleSize;
                    Vector2 rectSize = new Vector2( GetGUIPropertyWidth(), propertyHeight );
                    guiPosition.y += propertyHeight * 0.5f;
                    Vector2 border = new Vector2( 2, 2 );
                    Rect guiRect = new Rect( guiPosition, rectSize + border * 2 );

                    Handles.BeginGUI();
                    GUILayout.BeginArea( guiRect );

                    GUISkin skin = EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector );
                    EditorGUI.DrawRect( new Rect( Vector2.zero, rectSize + border * 2 ), EditorColor );

                    GUILayout.BeginArea( new Rect( border, rectSize ) );

                    EditorGUILayout.PropertyField( angle, new GUIContent(), true );
                    EditorGUILayout.PropertyField( blendType, new GUIContent(), true );

                    GUILayout.EndArea();
                    GUILayout.EndArea();
                    Handles.EndGUI();

                    // hacks: intercept unity scene view controls
                    if( guiRect.Contains( Event.current.mousePosition ) )
                    {
                        Ray ray = HandleUtility.GUIPointToWorldRay( Event.current.mousePosition );
                        Handles.Button( ray.origin + ray.direction, Camera.current.transform.rotation, 0, HandleUtility.GetHandleSize( ray.origin + ray.direction ), Handles.DotHandleCap );
                    }
                }
            }

            bool keepActive = false;
            if( so.hasModifiedProperties )
            {
                keepActive = true;
                so.ApplyModifiedProperties();
            }

            return keepActive;
        }
        #endregion
    }
#endif
}