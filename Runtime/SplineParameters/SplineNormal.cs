using UnityEngine;
using System.Collections.Generic;
using FantasticSplines;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Spline Keyframe track that stores local spline space directions that represent the normal (up direction) of the spline
public class SplineNormal : KeyframedSplineParameter<Vector3>
{
    [Range( 1, 100 )]
    public int visualisationSamples = 50;

    [Header( "Spline Normal Settings" )]
    public bool forceUnitLength = true;
    public bool forcePerpendicularToTangent = true;

    [Header("Normal Generation")]
    public bool automaticNormals = false;
    [Range( 0, 1 )]
    public float bankingStrength;
    public float bankingBlendStep = 1.0f;

    #region SplineDataTrack specialisation
#if UNITY_EDITOR
    protected override System.Type GetToolType() { return typeof( SplineNormalTool ); }
#endif

    public SplineNormal()
    {
        onSplineChanged += UpdateNormalConstraints;
        CustomInterpolator = SlerpNormals;
    }

    Vector3 SlerpNormals( ISpline spline, SplineParameterKeyframe<Vector3> first, SplineParameterKeyframe<Vector3> second, float t )
    {
        return Vector3.Slerp( first.value, second.value, t );
    }

    public override Vector3 GetDefaultKeyframeValue()
    {
        return Vector3.up;
    }

    // transform to world space and constrain the normal
    public override SplineParameterKeyframe<Vector3> TransformKeyframe( SplineParameterKeyframe<Vector3> keyframe )
    {
        keyframe.value = ConstrainNormal( spline.TransformDirection( keyframe.value ), keyframe.location.tangent );
        return keyframe;
    }

    // transform to local space and constrain the normal
    public override SplineParameterKeyframe<Vector3> InverseTransformKeyframe( SplineParameterKeyframe<Vector3> keyframe )
    {
        keyframe.value = spline.InverseTransformDirection( ConstrainNormal( keyframe.value, keyframe.location.tangent ) );
        return keyframe;
    }

    public override Vector3 GetValueAtDistance( float distance, Vector3 defaultValue )
    {
        SplineResult result = spline.GetResultAtDistance( distance );
        return GetNormalAtSplineResult( result );
    }
    #endregion

    public Vector3 GetNormalAtSplineResult( SplineResult splineResult)
    {
        if( automaticNormals )
        {
            return CalculateAutomaticNormal( splineResult);
        }
        else
        {
            return InterpolateKeyframeNormals( splineResult);
        }
    }

    public Vector3 InterpolateKeyframeNormals( SplineResult splineResult)
    {
        Vector3 normal = base.GetValueAtDistance( splineResult.distance, GetDefaultKeyframeValue() );
        return ConstrainNormal( normal, splineResult.tangent );
    }

    public Vector3 CalculateAutomaticNormal( SplineResult splineResult)
    {
        // everything here is calculated in world space.
        Vector3 tangentDirection = splineResult.tangent.normalized;
        Vector3 biNormal = Vector3.Cross( tangentDirection, Vector3.up ).normalized;
        Vector3 normal = Vector3.Cross( biNormal, tangentDirection );

        if( bankingStrength > 0.01f )
        {
            SplineResult bankingResult = spline.GetResultAtDistance( splineResult.distance + bankingBlendStep );
            Vector3 bankingTangentDirection = bankingResult.tangent.normalized;

            if( Vector3.Dot( tangentDirection, bankingTangentDirection ) < 0.999f )
            {
                Vector3 bankingBiNormal = Vector3.Cross( tangentDirection, bankingTangentDirection ).normalized;
                Vector3 bankingNormal = Vector3.Cross( bankingBiNormal, tangentDirection ).normalized;

                normal = Vector3.Lerp( normal, bankingNormal, bankingStrength );
            }
        }

        return normal;
    }

    Vector3 ConstrainNormal( Vector3 normal, Vector3 tangentDirection )
    {
        if( forcePerpendicularToTangent )
        {
            float length = 1;
            if( !forceUnitLength )
            {
                length = normal.magnitude;
            }
            tangentDirection = tangentDirection.normalized;
            normal = normal.normalized;
            Vector3 biNormal = Vector3.Cross( tangentDirection, normal );
            normal = Vector3.Cross( biNormal, tangentDirection ).normalized * length;
        }

        if( forceUnitLength )
        {
            if( Mathf.Approximately( normal.sqrMagnitude, 0 ) )
            {
                normal = GetDefaultKeyframeValue();
            }
            normal = normal.normalized;
        }

        return normal;
    }

    void UpdateNormalConstraints()
    {
        var keys = Keyframes;
        for( int i = 0; i < keys.Count; ++i )
        {
            SetKeyframeValue( i, keys[i].value ); // calls InverseTransformKeyframe
        }
    }

    #region Gizmos
#if UNITY_EDITOR
    protected override void DrawKeyframeValueGizmo( SplineParameterKeyframe<Vector3> keyframe )
    {
        Vector3 worldPosition = keyframe.location.position;
        Vector3 normal = keyframe.value;

        Handles.color = Color.green;
        Handles.DrawLine( worldPosition, worldPosition + normal, 2 );
        Handles.ConeHandleCap( 0, worldPosition + normal, Quaternion.LookRotation( normal ), SplineNormalTool.GetHandleSize( worldPosition ) * 1.3f, EventType.Repaint );
    }

    private void OnDrawGizmosSelected()
    {
        if( spline == null )
        {
            return;
        }

        DrawInterpolatedNormals();
    }

    void DrawInterpolatedNormals()
    {
        Gizmos.color = Color.green;

        float distance = 0;
        float length = spline.GetLength();
        float step = length / visualisationSamples;
        while( distance < length )
        {
            SplineResult location = spline.GetResultAtDistance( distance );
            Vector3 normal = GetNormalAtSplineResult( location );
            Gizmos.DrawLine( location.position, location.position + normal );
            distance += step;
        }

        SplineResult locationEnd = spline.GetResultAtDistance( length );
        Vector3 normalEnd = GetNormalAtSplineResult( locationEnd );
        Gizmos.DrawLine( locationEnd.position, locationEnd.position + normalEnd );
    }
#endif
    #endregion
}