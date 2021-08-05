using UnityEngine;
using System.Collections.Generic;
using FantasticSplines;

// Spline Keyframe track that stores and blends colors along a spline
public class SplineScale2 : KeyframedSplineParameter<Vector2>
{
    [Header( "Visualisation" )]
    public bool enableVisualisation = false;

    #region SplineDataTrack specialisation
    public override Vector2 GetDefaultKeyframeValue()
    {
        return Vector2.one;
    }

#if UNITY_EDITOR
    protected override System.Type GetToolType() { return typeof( SplineScale2Tool ); }
#endif
    #endregion

    #region Gizmos
    protected override void DrawKeyframeValueGizmo( SplineParameterKeyframe<Vector2> keyframe )
    {
        Gizmos.color = Color.white;
        DrawGizmos( keyframe.location.position, Quaternion.LookRotation( keyframe.location.tangent, transform.up ), keyframe.value );
        Gizmos.matrix = Matrix4x4.identity;
    }

    private void DrawGizmos( Vector3 position, Quaternion rotation, Vector2 scale )
    {
        Vector3 size = scale;
        Matrix4x4 visualMatrix = Matrix4x4.TRS( position, rotation, Vector3.one );
        Gizmos.matrix = visualMatrix;
        Gizmos.DrawWireCube( Vector3.zero, size );

    }

    private void OnDrawGizmosSelected()
    {
        DrawInterpolatedValue();
    }

    void DrawInterpolatedValue()
    {
        if( !enableVisualisation )
        {
            return;
        }

        Gizmos.color = Color.white;

        float distance = 0;
        float length = spline.GetLength();
        float step = length / 50f;
        while( distance < length )
        {
            SplineResult location = spline.GetResultAtDistance( distance );
            Gizmos.color = Color.white;

            Vector2 scale = GetValueAtDistance( location.distance, GetDefaultKeyframeValue() );
            DrawGizmos( location.position, Quaternion.LookRotation( location.tangent, transform.up ), scale );

            distance += step;
        }

        SplineResult locationEnd = spline.GetResultAtDistance( length );
        Vector2 scaleEnd = GetValueAtDistance( locationEnd.distance, GetDefaultKeyframeValue() );
        DrawGizmos( locationEnd.position, Quaternion.LookRotation( locationEnd.tangent, transform.up ), scaleEnd );
    }
    #endregion
}