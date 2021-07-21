using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;

namespace FantasticSplines
{
    // Tagging a class with the EditorTool attribute and no target type registers a global tool. Global tools are valid for any selection, and are accessible through the top left toolbar in the editor.
    [EditorTool( "Spline Normal Tool", typeof( SplineNormal ) )]
    class SplineNormalTool : KeyframedSplineParameterTool<Vector3>
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
        protected override float GetGUIPropertyWidth()
        {
            return 200;
        }

        protected override void DoToolHandles( EditorWindow window )
        {
            base.DoKeyframeToolHandles();

            if( SplineNormal.enableValueHandles )
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
                        Vector3 normal = keys[i].value;

                        Vector3 discNormal = tangent.normalized;
                        float discRadius = 0.5f;

                        Quaternion normalRotation = Quaternion.LookRotation( normal, tangent.normalized );

                        rotationHandleColour.a = 0.5f;
                        Handles.color = rotationHandleColour;

                        if( !SplineNormal.forcePerpendicularToTangent )
                        {
                            normalRotation = Handles.FreeRotateHandle( normalRotation, position, discRadius );
                        }
                        else
                        {
                            normalRotation = Handles.Disc( normalRotation, position, discNormal, discRadius, false, 0 );
                        }

                        if( EditorGUI.EndChangeCheck() )
                        {
                            Undo.RecordObject( SplineNormal, "Rotate Spline Normal" );
                            SplineNormal.SetKeyframeValue( i, normalRotation * Vector3.forward );
                        }
                    }
                }
            }
        }
        #endregion
    }
}