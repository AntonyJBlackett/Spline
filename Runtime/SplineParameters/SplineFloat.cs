using UnityEngine;
using System.Collections.Generic;
using FantasticSplines;


// Spline Keyframe track that stores and blends colors along a spline
public class SplineFloat : KeyframedSplineParameter<float>
{
    SplineFloat() : base()
    {
        parameterName = "Spline Float";
    }

    [Header( "Visualisation" )]
    [Range( 1, 100 )]
    public int visualisationSamples = 50;

    #region SplineDataTrack specialisation
    public override float GetDefaultKeyframeValue()
    {
        return 0;
    }
#if UNITY_EDITOR
    protected override System.Type GetToolType() { return typeof( SplineFloatTool ); }
#endif
    #endregion

    #region Gizmos
#if UNITY_EDITOR
    protected override void DrawKeyframeValueGizmo( SplineParameterKeyframe<float> keyframe )
    {
        Gizmos.color = Color.white;
        float radius = keyframe.value;
        Vector3 right = Vector3.Cross( keyframe.location.tangent, Vector3.up ).normalized * radius;
        Gizmos.DrawLine( keyframe.location.position - right * 0.5f, keyframe.location.position + right * 0.5f );
    }

    protected override void DrawInterpolatedGizmos()
    {
        var distance = SplineDistance.Zero;
        var length = spline.Length;
        var step = length / visualisationSamples;
        while( distance < length )
        {
            SplineResult location = spline.GetResultAt( distance );
            Gizmos.color = Color.white;
            float radius = GetValueAt( location.distance, GetDefaultKeyframeValue() );
            Vector3 right = Vector3.Cross( location.tangent, Vector3.up ).normalized * radius;
            Gizmos.DrawLine( location.position - right * 0.5f, location.position + right * 0.5f );
            distance += step;
        }
    }
#endif
    #endregion
}