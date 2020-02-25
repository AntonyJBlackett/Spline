using UnityEngine;
using FantasticSplines;

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
    public IEditableSpline GetEditableSpline() { return spline; }
    public Object GetUndoObject() { return spline; }

    public SplineComponent spline;
    public GameObject post;
    public GameObject segment;
    public SegmentForwardAxis segmentForwardAxis = SegmentForwardAxis.Z;
    public BoundsType boundsType = BoundsType.Collider;
    public Vector3 boundsAlignmentScalar = Vector3.zero;
    public float separation = 0;
    public bool clear = false;
    public bool regenerate = false;
    public bool autoRegenerate = false;

    SplineSnapshot changeDetector;

    [HideInInspector]
    [SerializeField]
    PrefabInstanceBucket instanceBucket;

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
            if( spline == null )
            {
                Clear();
                return;
            }

            if( changeDetector.IsDifferentFrom( spline ) )
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

        bool escape = false;
        if( spline == null )
        {
            escape = true;
            Debug.LogWarning( "No spline is set.", gameObject );
        }

        if( segment == null )
        {
            escape = true;
            Debug.LogWarning( "No prefab set.", gameObject );
        }

        if( escape )
        {
            return;
        }

        SplineResult post1Position = spline.GetResultAtDistance( 0 );
        float segmentLength = segment.transform.localScale.z;
        Quaternion axisRotation = Quaternion.identity;
        Bounds segmentBounds = new Bounds( Vector3.zero, segment.transform.localScale );
        switch( boundsType )
        {
            case BoundsType.Collider:
                Collider segmentCollider = segment.GetComponentInChildren<Collider>();
                if( segmentCollider != null )
                {
                    if( segmentCollider is BoxCollider )
                    {
                        BoxCollider box = segmentCollider as BoxCollider;
                        segmentBounds = new Bounds( box.center, box.size );
                    }
                    if( segmentCollider is MeshCollider )
                    {
                        MeshCollider mesh = segmentCollider as MeshCollider;
                        segmentBounds = mesh.bounds;
                    }
                }
                break;
            case BoundsType.Renderer:
                Renderer segmentRenderer = segment.GetComponentInChildren<Renderer>();
                segmentBounds = segmentRenderer != null ? segmentRenderer.bounds : segmentBounds;
                break;
        }

        switch( segmentForwardAxis )
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

        float worldDistance = segmentLength + separation;
        float step = worldDistance * 0.5f;

        SplineResult post2Position = spline.GetResultAtWorldDistanceFrom( post1Position.distance, worldDistance, step );

        // first segment
        if( post != null )
        {
            GameObject postInstance = instanceBucket.GetInstance( post );
            postInstance.SetActive( true );
            postInstance.transform.position = post1Position.position;
            postInstance.transform.rotation = Quaternion.LookRotation( post1Position.tangent, Vector3.up );
        }

        float splineLength = spline.GetLength();

        changeDetector = new SplineSnapshot( spline );
        if( worldDistance < 0.001f )
        {
            Debug.LogWarning( "worldDistance is too small we may loop forever!" );
            return;
        }

        if( splineLength > 0 )
        {
            float lengthLeft = splineLength;
            int limit = Mathf.CeilToInt( 1 + splineLength / worldDistance ); // we should never need more segments than a dead straight spline needs
            while( post1Position.loopT < post2Position.loopT )
            {
                GameObject segmentInstance = instanceBucket.GetInstance( segment );
                segmentInstance.SetActive( true );

                Vector3 segmentDirection = (post2Position.position - post1Position.position).normalized;
                Vector3 segmentPosition = post1Position.position + segmentDirection * worldDistance * 0.5f;

                if( Mathf.Approximately( segmentDirection.sqrMagnitude, 0 ) )
                {
                    segmentDirection = post1Position.tangent;
                }

                Vector3 alignmentAdjustment = Vector3.Scale( segmentBounds.center, boundsAlignmentScalar );
                Quaternion splineDirectionRotation = axisRotation * Quaternion.LookRotation( segmentDirection, Vector3.up );

                segmentInstance.transform.position = segmentPosition - splineDirectionRotation * alignmentAdjustment;
                segmentInstance.transform.rotation = splineDirectionRotation;

                if( post != null )
                {
                    Vector3 nextPostPosition = post1Position.position + segmentDirection * worldDistance;
                    GameObject postInstance = instanceBucket.GetInstance( post );
                    postInstance.SetActive( true );
                    postInstance.transform.position = nextPostPosition;
                    postInstance.transform.rotation = Quaternion.LookRotation( post2Position.tangent, Vector3.up );
                }

                post1Position = post2Position;
                post2Position = spline.GetResultAtWorldDistanceFrom( post2Position.distance, worldDistance, step );

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

    void Clear()
    {
        clear = false;
        instanceBucket.Clear();
    }
}