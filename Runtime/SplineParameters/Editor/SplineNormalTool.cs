using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;

namespace FantasticSplines
{
    // Tagging a class with the EditorTool attribute and no target type registers a global tool. Global tools are valid for any selection, and are accessible through the top left toolbar in the editor.
    [EditorTool( "Spline Normal Tool", typeof( SplineNormal ) )]
    class SplineNormalTool : KeyframedSplineParameterTool<Normal>
    {
        #region Tool properties and initialisation
        // Serialize this value to set a default value in the Inspector.
        [SerializeField]
        Texture2D m_ToolIcon;

        GUIContent m_IconContent;

        SplineNormal SplineNormal
        {
            get
            {
                return Target as SplineNormal;
            }
        }

        void OnEnable()
        {
            m_IconContent = new GUIContent()
            {
                image = m_ToolIcon,
                text = "Spline Normal Tool",
                tooltip = "Spline Normal Tool",
            };
        }

        public override GUIContent toolbarIcon
        {
            get { return m_IconContent; }
        }
        #endregion

        #region Custom tool handles
        protected override void DoToolHandles( EditorWindow window )
        {
            base.DoKeyframeToolHandles();

            if( SplineNormal.enableKeyframeHandles )
            {
                var keys = SplineNormal.Keyframes;
                Color rotationHandleColour = Color.green;
                using( new Handles.DrawingScope( Color.green ) )
                {
                    for( int i = 0; i < keys.Count; ++i )
                    {
                        EditorGUI.BeginChangeCheck();

                        Vector3 position = keys[i].location.position;
                        Vector3 tangent = keys[i].location.tangent;
                        Vector3 normal = SplineNormal.GetNormal( keys[i] );

                        Vector3 discNormal = tangent.normalized;
                        float discRadius = 0.5f * SplineNormal.GetNormalGizmoScale();

                        Quaternion normalRotation = Quaternion.LookRotation( normal, tangent.normalized );

                        rotationHandleColour.a = 0.5f;
                        Handles.color = rotationHandleColour;

                        Quaternion changedRotation = Handles.Disc( normalRotation, position, discNormal, discRadius, false, 0 );

                        if( EditorGUI.EndChangeCheck() )
                        {
                            Undo.RecordObject( SplineNormal, "Rotate Spline Normal" );
                            SplineNormal.SetKeyframeValue( i, new Normal() { blendType = keys[i].value.blendType, angle = keys[i].value.angle + Vector3.SignedAngle( normalRotation * Vector3.forward, changedRotation * Vector3.forward, keys[i].location.tangent ) } );
                        }
                    }
                }
            }
        }

        protected override float GetGUIPropertyWidth()
        {
            return 100;
        }

        protected override void DoToolGUI( EditorWindow window )
        {
            SerializedObject so = new SerializedObject( Target );
            var rawKeysSP = so.FindProperty( "rawKeyframes" );
            var keys = Target.Keyframes;
            for( int i = 0; i < keys.Count; ++i )
            {
                SerializedProperty keySP = rawKeysSP.GetArrayElementAtIndex( i );
                SerializedProperty keyValue = keySP.FindPropertyRelative( "value" );
                SerializedProperty angle = keyValue.FindPropertyRelative( "angle" );
                SerializedProperty blendType = keyValue.FindPropertyRelative( "blendType" );
                float propertyHeight = EditorGUI.GetPropertyHeight( angle, new GUIContent( "" ), true );
                propertyHeight += EditorGUI.GetPropertyHeight( blendType, new GUIContent( "" ), true );

                Vector2 guiPosition = HandleUtility.WorldToGUIPoint( keys[i].location.position ) + new Vector2( 0.5f, 0.5f ) * SplineHandleUtility.GetNodeHandleSize( keys[i].location.position );
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
            so.ApplyModifiedProperties();
        }
        #endregion
    }
}