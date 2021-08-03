﻿using UnityEngine;
using System.Collections.Generic;
using FantasticSplines;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum NormalBlendType
{
    FloatLerp,
    AngleLerp,
    VertorSlerp
}

[System.Serializable]
public struct Normal
{
    public float angle;
    public NormalBlendType blendType;
}

// Spline Keyframe track that stores local spline space directions that represent the normal (up direction) of the spline
public class SplineNormal : KeyframedSplineParameter<Normal>
{
    [Header( "Gizmos" )]
    public bool enableVisualisation = true;

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
        CustomInterpolator = SlerpNormals;
    }

    Normal SlerpNormals( ISpline spline, SplineParameterKeyframe<Normal> first, SplineParameterKeyframe<Normal> second, float t )
    {
        float angle1 = first.value.angle;
        float angle2 = second.value.angle;

        float angle = 0;

        switch( second.value.blendType )
        {
            case NormalBlendType.FloatLerp:
                angle = Mathf.Lerp( angle1, angle2, t );
                break;
            case NormalBlendType.AngleLerp:
                angle = Mathf.LerpAngle( angle1, angle2, t );
                break;
            case NormalBlendType.VertorSlerp:
                Vector3 n1 = GetNormal( first.location.tangent, angle1 );
                Vector3 n2 = GetNormal( second.location.tangent, angle2 );
                Vector3 slerpedNormal = Vector3.Slerp( n1, n2, t );
                Vector3 tangentAtT = spline.GetResultAtDistance( Mathf.Lerp( first.location.distance, second.location.distance, t ) ).tangent;
                Vector3 right = Vector3.Cross( tangentAtT.normalized, slerpedNormal.normalized );
                Vector3 trueNormal = Vector3.Cross( right, tangentAtT.normalized );
                angle = GetAngleFromNormal( tangentAtT, trueNormal );
                break;
        }

        return new Normal() { blendType = second.value.blendType, angle = angle };
    }

    public override Normal GetDefaultKeyframeValue()
    {
        return new Normal() { blendType = NormalBlendType.FloatLerp, angle = 0 };
    }
    #endregion

    public float GetAngleFromNormal( Vector3 tangent, Vector3 normal )
    {
        Vector3 defaultNormal = GetNormal( tangent, 0 );
        return Vector3.SignedAngle( defaultNormal, normal, tangent );
    }

    public Vector3 GetNormal( Vector3 tangent, float angle )
    {
        return Quaternion.LookRotation( tangent.normalized, transform.up ) * Quaternion.AngleAxis( angle, Vector3.forward ) * Vector3.up;
    }

    public Vector3 GetNormal( SplineParameterKeyframe<Normal> keyframe )
    {
        return GetNormal( keyframe.location.tangent.normalized, keyframe.value.angle );
    }

    public Vector3 GetNormalAtSplineResult( SplineResult splineResult )
    {
        return GetNormal( splineResult.tangent, GetValueAtDistance( splineResult.distance, GetDefaultKeyframeValue() ).angle );
    }

    public Vector3 GetNormalAtDistance( float distance )
    {
        return GetNormalAtSplineResult( spline.GetResultAtDistance( distance ) );
    }

    public Vector3 CalculateAutomaticNormal( SplineResult splineResult )
    {
        // everything here is calculated in world space.
        Vector3 normal = GetNormalAtSplineResult( splineResult );

        if( bankingStrength > 0.01f )
        {
            SplineResult bankingResult = spline.GetResultAtDistance( splineResult.distance + bankingBlendStep );
            Vector3 bankingTangentDirection = bankingResult.tangent.normalized;

            Vector3 tangentDirection = splineResult.tangent.normalized;
            if( Vector3.Dot( tangentDirection, bankingTangentDirection ) < 0.999f )
            {
                Vector3 bankingBiNormal = Vector3.Cross( tangentDirection, bankingTangentDirection ).normalized;
                Vector3 bankingNormal = Vector3.Cross( bankingBiNormal, tangentDirection ).normalized;

                normal = Vector3.Lerp( normal, bankingNormal, bankingStrength );
            }
        }

        return normal;
    }

    #region Gizmos

    public float GetNormalGizmoScale()
    {
        return spline.GetGizmoScale() * 0.5f;
    }

#if UNITY_EDITOR
    protected override void DrawKeyframeValueGizmo( SplineParameterKeyframe<Normal> keyframe )
    {
        Vector3 worldPosition = keyframe.location.position;
        Vector3 normal = GetNormalAtSplineResult( keyframe.location );

        Handles.color = Color.green;
        float gizmosScale = GetNormalGizmoScale();
        Handles.DrawLine( worldPosition, worldPosition + normal * gizmosScale, 3* gizmosScale );
        Handles.ConeHandleCap( 0, worldPosition + normal * gizmosScale, Quaternion.LookRotation( normal ), SplineNormalTool.GetHandleSize( worldPosition ) * gizmosScale * 3f, EventType.Repaint );
    }

    void OnDrawGizmosSelected()
    {
        if( spline == null )
        {
            return;
        }

        DrawInterpolatedNormals();
    }

    void DrawInterpolatedNormals()
    {
        if( !enableVisualisation )
        {
            return;
        }

        Gizmos.color = Color.green;

        float gizmosScale = GetNormalGizmoScale();
        float distance = 0;
        float length = spline.GetLength();
        float step = 0.1f;
        while( distance < length )
        {
            SplineResult location = spline.GetResultAtDistance( distance );
            Vector3 normal = GetNormalAtSplineResult( location );
            Gizmos.DrawLine( location.position, location.position + normal* gizmosScale );
            distance += step;
        }

        SplineResult locationEnd = spline.GetResultAtDistance( length );
        Vector3 normalEnd = GetNormalAtSplineResult( locationEnd ) * spline.GetGizmoScale();
        Gizmos.DrawLine( locationEnd.position, locationEnd.position + normalEnd* gizmosScale );
    }
#endif
    #endregion
}