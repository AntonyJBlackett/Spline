﻿using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
#endif

namespace FantasticSplines
{
    // Tagging a class with the EditorTool attribute and no target type registers a global tool. Global tools are valid for any selection, and are accessible through the top left toolbar in the editor.
#if UNITY_EDITOR

    [CustomEditor( typeof( SplineScale2 ) )]
    public class SplineScale2Editor : KeyframedSplineParameterEditor
    {
    }

    [EditorTool("Spline Scale2 Tool", typeof( SplineScale2 ) )]
    class SplineScale2Tool : KeyframedSplineParameterTool<Vector2>
    {
        SplineScale2 SplineScale2
        {
            get
            {
                return Target as SplineScale2;
            }
        }

        #region Custom tool handles
        protected override float GetGUIPropertyWidth()
        {
            return 100;
        }

        public static void ScaleCubeCap( int controlId, Vector3 position, Quaternion rotation, float size, EventType eventType )
        {
            Camera camera = SceneView.currentDrawingSceneView.camera;

            Vector3 forward = camera.transform.forward;
            if( !camera.orthographic )
            {
                forward = (position - camera.transform.position).normalized;
            }

            Handles.CubeHandleCap( controlId, position, Quaternion.LookRotation( forward, camera.transform.up ), size, eventType );
        }

        protected override bool DoToolHandles( EditorWindow window )
        {
            bool keepActive = base.DoKeyframeToolHandles();

            if( SplineScale2.enableKeyframeHandles )
            {
                var keys = SplineScale2.Keyframes;
                using( new Handles.DrawingScope( Color.white ) )
                {
                    for( int i = 0; i < keys.Count; ++i )
                    {
                        EditorGUI.BeginChangeCheck();

                        Vector3 position = keys[i].location.position;
                        Vector3 tangent = keys[i].location.tangent;
                        Vector2 scale = SplineScale2.GetValueAt( keys[i].location.distance, Vector2.one );

                        Quaternion rotation = Quaternion.LookRotation( tangent.normalized, SplineScale2.transform.up );

                        Vector3 xDirection = rotation * Vector3.right * scale.x * 0.5f;
                        Vector3 xScaleHandle = position + xDirection;
                        Vector3 newxScaleHandle = Handles.Slider( xScaleHandle, xDirection, SplineScale2.spline.gizmoScale * 0.1f, ScaleCubeCap, 0 );

                        Vector3 yDirection = rotation * Vector3.up * scale.y * 0.5f;
                        Vector3 yScaleHandle = position + yDirection;
                        Vector3 newyScaleHandle = Handles.Slider( yScaleHandle, yDirection, SplineScale2.spline.gizmoScale * 0.1f, ScaleCubeCap, 0 );


                        if( EditorGUI.EndChangeCheck() )
                        {
                            keepActive = true;
                            scale.x += Vector3.Distance( xScaleHandle, newxScaleHandle ) * Vector3.Dot( xDirection.normalized, (newxScaleHandle - xScaleHandle).normalized );
                            scale.y += Vector3.Distance( yScaleHandle, newyScaleHandle ) * Vector3.Dot( yDirection.normalized, (newyScaleHandle - yScaleHandle).normalized );

                            Undo.RecordObject( SplineScale2, "Spline Scale changed" );
                            SplineScale2.SetKeyframeValue( i, scale );
                        }
                    }
                }
            }
            return keepActive;
        }

        #endregion
    }
#endif
}