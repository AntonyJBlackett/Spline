﻿using UnityEngine;
using System.Collections.Generic;
using FantasticSplines;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Spline Keyframe track that stores and blends colors along a spline
public class SplineColor : KeyframedSplineParameter<Color>
{
    SplineColor() : base()
    {
        parameterName = "Spline Color";
    }

    [Header( "Visualisation" )]
    public bool enableVisualisation = false;

    #region SplineDataTrack specialisation
    public override Color GetDefaultKeyframeValue()
    {
        return Color.white;
    }

#if UNITY_EDITOR
    protected override System.Type GetToolType() { return typeof( SplineColorTool ); }
#endif
    #endregion

    #region Gizmos
#if UNITY_EDITOR
    protected override void DrawKeyframeValueGizmo( SplineParameterKeyframe<Color> keyframe )
    {
        float size = SplineNormalTool.GetHandleSize( keyframe.location.position ) * spline.gizmoScale;
        Handles.color = Color.black;
        Vector3 up = SceneView.currentDrawingSceneView.camera.transform.up;
        Handles.DrawLine( keyframe.location.position, keyframe.location.position + up * size * 2.5f, 2 );
        Handles.DrawSolidDisc( keyframe.location.position + up * size * 2.5f, SceneView.currentDrawingSceneView.camera.transform.forward, size * 1.2f * 0.75f );
        Handles.color = keyframe.value;
        Handles.DrawSolidDisc( keyframe.location.position + up * size * 2.5f, SceneView.currentDrawingSceneView.camera.transform.forward, size * 0.75f );
    }

    List<SplineDistance> pointDistances = new List<SplineDistance>();
    protected override void DrawInterpolatedGizmos()
    {
        if( enableVisualisation )
        {
            int nodeCount = spline.NodeCount;
            pointDistances.Clear();
            for( int i = 0; i < nodeCount; ++i )
            {
                if( i == 0 && !spline.IsLoop )
                {
                    continue;
                }

                SplineResult start = spline.GetResultAtNode( i - 1 );
                SplineResult end = spline.GetResultAtNode( i );

                if( i == 0 )
                {
                    end.distance += end.length;
                }

                int segments = 10;
                for( int s = 0; s < segments; ++s )
                {
                    pointDistances.Add( SplineDistance.Lerp( start.distance, end.distance, Mathf.InverseLerp(0, segments-1, s) ) );
                }
            }

            var keys = Keyframes;
            for( int i = 0; i < keys.Count; ++i )
            {
                pointDistances.Add( keys[i].location.distance );
            }

            pointDistances.Sort( ( a, b ) => { return a.CompareTo( b ); } );

            for( int i = 1; i < pointDistances.Count; ++i )
            {
                SplineResult start = spline.GetResultAt( pointDistances[i-1] );
                SplineResult end = spline.GetResultAt( pointDistances[i] );

                Handles.color = GetValueAt( (start.distance+end.distance)*0.5f, GetDefaultKeyframeValue() );
                Handles.DrawLine( start.position, end.position, 5 );
            }
        }
    }
#endif
    #endregion
}