using UnityEngine;
using FantasticSplines;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;

#endif

// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

[ExecuteInEditMode]
public class FenceBuilder : MonoBehaviour, IEditorSplineProxy
{
    public enum SegmentForwardAxis
    {
        X,
        Y,
        Z,
        NX,
        NY,
        NZ
    }

    public enum BoundsType
    {
        Collider,
        Renderer
    }

    // can now use the spline editor when this object is selected
    public IEditableSpline GetEditableSpline()
    {
        return parameters.spline;
    }

    public Object[] GetUndoObjects( )
    {
        return parameters.spline.GetUndoObjects();
    }

    [System.Serializable]
    public struct FenceSpawnerParameters
    {
        public SplineComponent spline;
        public GameObject post;
        public GameObject segment;
        public SegmentForwardAxis segmentForwardAxis;
        public BoundsType boundsType;
        public Vector3 boundsAlignmentScalar;
        public float scaleOffset; // 0 == 1
        public float padding;

        public float Scale => scaleOffset + 1;

        public bool Equals( FenceSpawnerParameters other )
        {
            return spline == other.spline
                   && post == other.post
                   && segment == other.segment
                   && segmentForwardAxis == other.segmentForwardAxis
                   && boundsType == other.boundsType
                   && boundsAlignmentScalar == other.boundsAlignmentScalar
                   && Mathf.Approximately( scaleOffset, other.scaleOffset )
                   && Mathf.Approximately( padding, other.padding );
        }

        public bool IsDifferentFrom( FenceSpawnerParameters other )
        {
            return !Equals( other );
        }
    }

    public FenceSpawnerParameters parameters;

    public bool clear = false;
    public bool regenerate = false;
    public bool autoRegenerate = false;

    [SerializeField][HideInInspector] SplineSnapshot changeDetector;
    [SerializeField][HideInInspector] FenceSpawnerParameters lastParameters;

    PrefabInstanceBucket instanceBucket;

    private void OnEnable()
    {
        instanceBucket = GetComponentInChildren<PrefabInstanceBucket>();
    }

    void Update()
    {
        if( regenerate )
        {
            Regenerate();
        }

        if( clear )
        {
            Clear();
        }

        AutoRegenerate();
    }

    void OnDrawGizmos()
    {
        Update();
    }

    void AutoRegenerate()
    {
        if( autoRegenerate )
        {
            if( parameters.spline == null )
            {
                Clear();
                return;
            }

            if( lastParameters.IsDifferentFrom( parameters ) || changeDetector.IsDifferentFrom( parameters.spline ) )
            {
                Regenerate();
            }
        }
    }

    void Regenerate()
    {
        if( instanceBucket == null )
        {
            instanceBucket = PrefabInstanceBucket.Instantiate( transform );
        }

        regenerate = false;
        instanceBucket.DeactivateInstances();

        bool warn = lastParameters.IsDifferentFrom( parameters ) || changeDetector.IsDifferentFrom( parameters.spline );

        bool escape = false;
        if( parameters.spline == null )
        {
            escape = true;
            if( warn ) Debug.LogWarning( "No spline is set.", gameObject );
        }

        if( parameters.segment == null )
        {
            escape = true;
            if( warn ) Debug.LogWarning( "No prefab set.", gameObject );
        }

        changeDetector = new SplineSnapshot( parameters.spline );
        lastParameters = parameters;

        if( escape )
        {
            return;
        }

        SplineResult post1Position = parameters.spline.GetResultAt( SplineDistance.Zero );
        float segmentLength = parameters.segment.transform.localScale.z;
        Quaternion axisRotation = Quaternion.identity;
        Bounds segmentBounds = new Bounds( Vector3.zero, parameters.segment.transform.localScale );
        switch( parameters.boundsType )
        {
            case BoundsType.Collider:
                Collider segmentCollider = parameters.segment.GetComponentInChildren<Collider>();
                if( segmentCollider != null )
                {
                    if( segmentCollider is BoxCollider )
                    {
                        BoxCollider box = segmentCollider as BoxCollider;
                        segmentBounds = new Bounds( box.center, box.size );
                    }
                    else if( segmentCollider is MeshCollider )
                    {
                        MeshCollider mesh = segmentCollider as MeshCollider;
                        segmentBounds = mesh.bounds;
                    }
                }

                break;
            case BoundsType.Renderer:
                Renderer segmentRenderer = parameters.segment.GetComponentInChildren<Renderer>();
                segmentBounds = segmentRenderer != null ? segmentRenderer.bounds : segmentBounds;
                break;
        }

        switch( parameters.segmentForwardAxis )
        {
            case SegmentForwardAxis.X:
                segmentLength = segmentBounds.size.x;
                axisRotation = Quaternion.LookRotation( Vector3.right );
                break;
            case SegmentForwardAxis.Y:
                segmentLength = segmentBounds.size.y;
                axisRotation = Quaternion.LookRotation( Vector3.up );
                break;
            case SegmentForwardAxis.Z:
                segmentLength = segmentBounds.size.z;
                axisRotation = Quaternion.LookRotation( Vector3.forward );
                break;
            case SegmentForwardAxis.NX:
                segmentLength = segmentBounds.size.x;
                axisRotation = Quaternion.LookRotation( -Vector3.right );
                break;
            case SegmentForwardAxis.NY:
                segmentLength = segmentBounds.size.y;
                axisRotation = Quaternion.LookRotation( -Vector3.up );
                break;
            case SegmentForwardAxis.NZ:
                segmentLength = segmentBounds.size.z;
                axisRotation = Quaternion.LookRotation( -Vector3.forward );
                break;
        }

        segmentBounds.size *= parameters.Scale;
        segmentBounds.center *= parameters.Scale;
        segmentLength *= parameters.Scale;

        var worldDistance = segmentLength + parameters.padding;
        if( worldDistance < parameters.spline.Length.value * 0.001f )
        {
            Debug.LogWarning( "worldDistance is too small we may loop forever!" );
            return;
        }

        var step = new SplineDistance( worldDistance * 0.5f );
        SplineResult post2Position =
            parameters.spline.GetResultAtWorldDistanceFrom( post1Position.distance, worldDistance, step );

        // first segment
        if( parameters.post != null )
        {
            SpawnObject( parameters.post, post1Position.position,
                Quaternion.LookRotation( post2Position.tangent, Vector3.up ), parameters.Scale );
        }

        var splineLength = parameters.spline.Length;

        if( splineLength > SplineDistance.Zero )
        {
            int limit = Mathf.CeilToInt( 1 + splineLength.value / worldDistance ); // we should never need more segments than a dead straight spline needs
            while( post1Position.percent.Looped < post2Position.percent.Looped )
            {
                Vector3 segmentDirection = (post2Position.position - post1Position.position).normalized;
                Vector3 segmentPosition = post1Position.position + segmentDirection * worldDistance * 0.5f;
                if( Mathf.Approximately( segmentDirection.sqrMagnitude, 0 ) )
                {
                    segmentDirection = post1Position.tangent.normalized;
                }

                Vector3 alignmentAdjustment = Vector3.Scale( segmentBounds.center, parameters.boundsAlignmentScalar );
                Quaternion splineDirectionRotation =
                    axisRotation * Quaternion.LookRotation( segmentDirection, Vector3.up );

                SpawnObject( parameters.segment, segmentPosition - splineDirectionRotation * alignmentAdjustment,
                    splineDirectionRotation, parameters.Scale );

                if( parameters.post != null )
                {
                    Vector3 nextPostPosition = post1Position.position + (segmentDirection * worldDistance);
                    SpawnObject( parameters.post, nextPostPosition,
                        Quaternion.LookRotation( post2Position.tangent, Vector3.up ), parameters.Scale );
                }

                post1Position = post2Position;
                post2Position =
                    parameters.spline.GetResultAtWorldDistanceFrom( post2Position.distance, worldDistance, step );

                --limit;
                if( limit < 0 )
                {
                    Debug.LogWarning( "Segment limit reached" );
                    break;
                }
            }
        }

        instanceBucket.CleanUpUnusedInstances();
    }

    void SpawnObject( GameObject prefab, Vector3 position, Quaternion rotation, float scale )
    {
        GameObject postInstance = instanceBucket.GetInstance( prefab );
        postInstance.transform.position = position;
        postInstance.transform.rotation = rotation;
        postInstance.transform.localScale = Vector3.one * scale;
    }

    void Clear()
    {
        clear = false;
        instanceBucket.Clear();
    }
}