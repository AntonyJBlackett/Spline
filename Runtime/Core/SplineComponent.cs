using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif


// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

namespace FantasticSplines
{
    public class SplineComponent : MonoBehaviour, ISpline, IEditableSpline
    {
        public System.Action<int> onNodeAdded;
        public System.Action<int> onNodeRemoved;
        public System.Action onUpdated;
        public System.Action<List<Object>> onGetUndoObjects;

        [SerializeField]
        [FormerlySerializedAs( "curve" )]
        private Spline localSpline = new Spline(); // spline in local space

        SplineNormal customNormals;
        public bool hasCustomNormals => customNormals != null;
        void Start()
        {
            customNormals = GetComponentInChildren<SplineNormal>();
        }

        public SplineDistance Length => localSpline.Length;

        public bool IsLoop
        {
            get
            {
                return localSpline.IsLoop;
            }
            set
            {
                if( localSpline.IsLoop != value )
                {
                    // looping and unlooping ads and removes a segment
                    if( value )
                    {
                        onNodeAdded?.Invoke( NodeCount + 1 );
                    }
                    else
                    {
                        onNodeRemoved?.Invoke( NodeCount );
                    }
                }

                localSpline.IsLoop = value;

                onUpdated?.Invoke();
            }
        }
        public int NodeCount => localSpline.NodeCount;
        public int SegmentCount => localSpline.SegmentCount;

        void OnValidate()
        {
            localSpline.isDirty = true;
        }

        public SplineDistance LoopDistance( SplineDistance distance )
        {
            return localSpline.LoopDistance( distance );
        }

        public int LoopIndex( int index )
        {
           return localSpline.LoopNodeIndex( index );
        }

        bool IsNodeIndexInRange(int index)
        {
            return MathsUtils.IsInArrayRange( index, NodeCount );
        }

        public Vector3 InverseTransformPoint(Vector3 point)
        {
            return transform.InverseTransformPoint( point );
        }

        public Vector3 TransformPoint(Vector3 point)
        {
            return transform.TransformPoint( point );
        }

        public Vector3 InverseTransformVector(Vector3 vector)
        {
            return transform.InverseTransformVector( vector );
        }

        public Vector3 TransformVector(Vector3 vector)
        {
            return transform.TransformVector( vector );
        }

        public Vector3 InverseTransformDirection(Vector3 direction)
        {
            return transform.InverseTransformDirection( direction );
        }

        public Vector3 TransformDirection(Vector3 direction)
        {
            return transform.TransformDirection( direction );
        }

        public SplineNode TransformNode(SplineNode node)
        {
            return node.Transform( transform );
        }

        public SplineNode InverseTransformNode(SplineNode node)
        {
            return node.InverseTransform( transform );
        }

        public List<Vector3> TransformPoints(List<Vector3> points)
        {
            for( int i = 0; i < points.Count; ++i )
            {
                points[i] = TransformPoint( points[i] );
            }

            return points;
        }

        public List<SplineNode> TransformPoints(List<SplineNode> points)
        {
            for( int i = 0; i < points.Count; ++i )
            {
                points[i] = TransformNode( points[i] );
            }

            return points;
        }

        public Ray InverseTransformRay(Ray ray)
        {
            ray.origin = InverseTransformPoint( ray.origin );
            ray.direction = InverseTransformVector( ray.direction );
            return ray;
        }

        public SplineResult UpdateRoadResultWithCustomNormals( SplineResult result )
        {
            if( hasCustomNormals ) result.segmentResult.normal = customNormals.GetNormalAtSplineResult( result );
            return result;
        }

        SplineResult TransformResult(SplineResult toTransform)
        {
            toTransform.segmentResult.position = TransformPoint( toTransform.segmentResult.position );
            toTransform.segmentResult.tangent = TransformVector( toTransform.segmentResult.tangent );
            toTransform.segmentResult.normal = TransformVector( toTransform.segmentResult.normal );

            UpdateRoadResultWithCustomNormals( toTransform );

            return toTransform;
        }

        SplineNodeResult TransformNodeResult( SplineNodeResult toTransform )
        {
            toTransform.splineResult = TransformResult( toTransform.splineResult );

            toTransform.inTangent = TransformVector( toTransform.inTangent );
            toTransform.outTangent = TransformVector( toTransform.outTangent );

            return toTransform;
        }

        public void AppendNode(SplineNode node)
        {
            localSpline.AddNode( node.InverseTransform( transform ) );

            onNodeAdded?.Invoke( NodeCount );
            onUpdated?.Invoke();
        }
        public void PrependNode(SplineNode node)
        {
            localSpline.AddNodeAt( 0, node.InverseTransform( transform ) );

            onNodeAdded?.Invoke( 0 );
            onUpdated?.Invoke();
        }
        public void InsertNode(SplineNode node, int index)
        {
            // Add node without changing the curve
            localSpline.AddNodeAt(index, node.InverseTransform(transform));

            onNodeAdded?.Invoke( index );
            onUpdated?.Invoke();
        }
        public int InsertNode(SplinePercent percent)
        {
            int index = localSpline.CreateNode( percent );

            onNodeAdded?.Invoke( index );
            onUpdated?.Invoke();
            return index;
        }
        public void RemoveNode(int index)
        {
            localSpline.RemoveNode( index );

            onNodeRemoved?.Invoke( index );
            onUpdated?.Invoke();
        }

        public SplineNode GetNode(int index)
        {
            if( !IsNodeIndexInRange( index ) )
            {
                return new SplineNode( transform.position, 0 );
            }

            return TransformNode( localSpline.GetNode( index ) );
        }

        public void SetNode(int index, SplineNode node)
        {
            localSpline.SetNode( index, node.InverseTransform( transform ) );
            onUpdated?.Invoke();
        }

        public int UpdateCount => localSpline.UpdateCount;

        public SplineSnapshot GetSnapshot()
        {
            return new SplineSnapshot(this);
        }

        public SplineResult GetResultAt( SplinePercent percent )
        {
            return TransformResult( localSpline.GetResultAt( percent ) );
        }

        public SplineResult GetResultAt(SplineDistance distance)
        {
            return TransformResult( localSpline.GetResultAt( distance ) );
        }

        public SplineResult GetResultAtSegment(int segentIndex, SegmentT segmentT )
        {
            return TransformResult( localSpline.GetResultAtSegment( segentIndex, segmentT ) );
        }

        public SplineResult GetResultAtSegment(int segentIndex, SegmentDistance segmentDistance)
        {
            return TransformResult( localSpline.GetResultAtSegment( segentIndex, segmentDistance ) );
        }

        public SplineResult GetResultAtSegment( int segentIndex, SegmentPercent segmentPercent )
        {
            return TransformResult( localSpline.GetResultAtSegment( segentIndex, segmentPercent ) );
        }

        public SplineResult GetResultAtWorldDistanceFrom(SplineDistance startDistance, float worldDistance, SplineDistance stepDistance )
        {
            if( SplineDistance.Approximately( stepDistance, SplineDistance.Zero ) )
            {
                Debug.LogWarning( "Step is too small." );
                return GetResultAt( startDistance );
            }

            var splineLength = Length;
            if( splineLength.value < worldDistance )
            {
                // early out for short splines
                if( stepDistance > SplineDistance.Zero )
                {
                    return GetResultAt( splineLength );
                }
                else
                {
                    return GetResultAt( SplineDistance.Zero );
                }
            }

            int maxIterations = Mathf.CeilToInt( worldDistance * 5f / stepDistance.value );
            int iterationsLeft = maxIterations;

            SplineResult currentPosition = GetResultAt( startDistance );
            int startLapCount = currentPosition.lapCount;
            var startLoopDistance = currentPosition.loopDistance;

            Vector3 origin = currentPosition.position;

            float currentWorldDistance;
            SplineResult previousPosition;
            do
            {
                if(currentPosition.AtEnd && stepDistance >= SplineDistance.Zero || currentPosition.AtStart && stepDistance < SplineDistance.Zero )
                {
                    return currentPosition;
                }
                else if( startLapCount != currentPosition.lapCount )
                {
                    if( startLoopDistance < currentPosition.loopDistance && stepDistance >= SplineDistance.Zero )
                    {
                        return currentPosition;
                    }
                    else if( startLoopDistance > currentPosition.loopDistance && stepDistance < SplineDistance.Zero )
                    {
                        return currentPosition;
                    }
                }

                previousPosition = currentPosition;
                currentPosition = GetResultAt( currentPosition.distance + stepDistance );
                currentWorldDistance = Vector3.Distance( currentPosition.position, origin );

                --iterationsLeft;
                if( iterationsLeft < 0 )
                {
                    Debug.LogWarning( "Hit iterations limit of " + maxIterations + " in MoveUntilAtWorldDistance() on spline" );
                    break;
                }
            } while( currentWorldDistance < worldDistance );

            if( maxIterations - iterationsLeft > 10 )
            {
                Debug.LogWarning( "Increase step distance for better performance. Num iterations to resolve: " + (maxIterations - iterationsLeft).ToString() );
            }

            float previousWorldDistance = Vector3.Distance( previousPosition.position, origin );
            float lerpT = Mathf.InverseLerp( previousWorldDistance, currentWorldDistance, worldDistance );

            return GetResultAt( currentPosition.distance - stepDistance + (stepDistance * lerpT) );
        }

        public SplineResult GetResultClosestTo(Vector3 point )
        {
            return TransformResult( localSpline.GetResultClosestTo( InverseTransformPoint( point ) ) );
        }

        public SplineResult GetResultClosestToWithinDistanceWindow( Vector3 point, SplineDistance minDistance, SplineDistance maxDistance )
        {
            return TransformResult( localSpline.GetResultClosestToWithinDistanceWindow( InverseTransformPoint( point ), minDistance, maxDistance ) );
        }

        public SplineResult GetResultClosestToSegment( int segementIndex, Vector3 point )
        {
            return TransformResult( localSpline.GetResultClosestToSegment( segementIndex, InverseTransformPoint( point ) ) );
        }

        public SplineResult GetResultClosestToSegmentUsingLinearApproximation( int segementIndex, Vector3 point )
        {
            return TransformResult( localSpline.GetResultClosestToSegmentUsingLinearApproximation( segementIndex, InverseTransformPoint( point ) ) );
        }

        public SplineResult GetResultClosestToSegment( int segementIndex, Ray ray, Spline.TestDirection testDirection = Spline.TestDirection.Forward )
        {
            return TransformResult( localSpline.GetResultClosestToSegment( segementIndex, InverseTransformRay( ray ), testDirection ) );
        }

        public SplineResult GetResultClosestToSegmentUsingLinearApproximation( int segementIndex, Ray ray, Spline.TestDirection testDirection = Spline.TestDirection.Forward )
        {
            return TransformResult( localSpline.GetResultClosestToSegmentUsingLinearApproximation( segementIndex, InverseTransformRay( ray ), testDirection ) );
        }

        public SplineResult GetResultClosestToWithinDistanceWindow( Ray ray, SplineDistance minDistance, SplineDistance maxDistance, Spline.TestDirection testDirection = Spline.TestDirection.Forward )
        {
            return TransformResult( localSpline.GetResultClosestToWithinDistanceWindow( InverseTransformRay( ray ), minDistance, maxDistance, testDirection ) );
        }

        public SplineResult GetResultClosestTo( Ray ray )
        {
            return TransformResult( localSpline.GetResultClosestTo( InverseTransformRay( ray ), Spline.TestDirection.Forward ) );
        }

        public SplineResult GetResultClosestTo(Ray ray, Spline.TestDirection testDirection )
        {
            return TransformResult( localSpline.GetResultClosestTo( InverseTransformRay( ray ), testDirection ) );
        }

        public SplineResult GetResultAtNode( int nodeIndex )
        {
            nodeIndex = LoopIndex( nodeIndex );
            return TransformResult( localSpline.GetResultAtNode( nodeIndex ) );
        }

        public SplineNodeResult GetNodeResult( int nodeIndex )
        {
            nodeIndex = LoopIndex( nodeIndex );
            return TransformNodeResult( localSpline.GetNodeResult( nodeIndex ) );
        }

        public SplineResult GetResultClosestToUsingSegmentApproximation( Vector3 point )
        {
            return TransformResult( localSpline.GetResultClosestToUsingSegmentApproximation( InverseTransformPoint( point ) ) );
        }

        public SplineResult GetResultClosestToUsingSegmentApproximation( Ray ray )
        {
            return GetResultClosestToUsingSegmentApproximation( ray, Spline.TestDirection.Forward );
        }

        public SplineResult GetResultClosestToUsingSegmentApproximation( Ray ray, Spline.TestDirection testDirection )
        {
            return TransformResult( localSpline.GetResultClosestToUsingSegmentApproximation( InverseTransformRay( ray ), testDirection ) );
        }

        public SplineResult GetResultClosestToUsingLinearApproximation( Vector3 point )
        {
            return TransformResult( localSpline.GetResultClosestToUsingLinearApproximation( InverseTransformPoint( point ) ) );
        }

        public SplineResult GetResultClosestToUsingLinearApproximation( Ray ray )
        {
            return GetResultClosestToUsingLinearApproximation( ray, Spline.TestDirection.Forward );
        }

        public SplineResult GetResultClosestToUsingLinearApproximation( Ray ray, Spline.TestDirection testDirection )
        {
            return TransformResult( localSpline.GetResultClosestToUsingLinearApproximation( InverseTransformRay( ray ), testDirection ) );
        }

        // Editor related things
        public Transform Transform => transform;
        public Component Component => this;

        public IEditableSpline GetEditableSpline() { return this; }
        public Object[] GetUndoObjects( )
        {
            List<Object> inOutUndoObjects = new List<Object>();
            inOutUndoObjects.Add( this );
            onGetUndoObjects?.Invoke( inOutUndoObjects );
            return inOutUndoObjects.ToArray();
        }

        public Color color { get; set; } = Color.white;
        public bool zTest { get; set; } = false;
        public float gizmoScale { get; set; } = 1;
        public bool alwaysDraw { get; set; } = true;
        public bool showDefaultNormals { get; set; } = false;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Handles.zTest = zTest ? UnityEngine.Rendering.CompareFunction.LessEqual : UnityEngine.Rendering.CompareFunction.Always;
            Gizmos.matrix = transform.localToWorldMatrix;
            Handles.matrix = transform.localToWorldMatrix;
            if( Selection.activeObject != gameObject )
            {
                if( alwaysDraw )
                {
                    localSpline.OnDrawGizmos( color, gizmoScale );
                }
            }
            else if( Selection.activeObject == gameObject )
            {
                localSpline.DrawDirecitonIndicators( color, gizmoScale );
                DrawNormals();
            }

            Gizmos.matrix = Matrix4x4.identity;
            Handles.matrix = Matrix4x4.identity;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        }
#endif

        private void DrawNormals()
        {
            if( showDefaultNormals )
            {
                Gizmos.color = Color.green;
                for( int i = 0; i < localSpline.SegmentCount; ++i )
                {
                    if( i == 0 )
                    {
                        var result = localSpline.GetResultAtSegment( 0, new SegmentT( 0 ) );
                        Gizmos.DrawLine( result.position, result.position + result.normal * 3 );
                    }

                    int samples = 3;
                    for( int s = 1; s <= samples; ++s )
                    {
                        var t = new SegmentT( (float)s / samples);
                        var result = localSpline.GetResultAtSegment( i, t );
                        Gizmos.DrawLine( result.position, result.position + result.normal * 3 );
                    }
                }
            }
        }

        public void DrawSegmentLengths()
        {
#if UNITY_EDITOR
            int segCount = localSpline.SegmentCount;
            for( int i = 0; i < segCount; ++i )
            {
                var result = GetResultAtSegment( i, new SegmentPercent( 0.5f ) );
                Handles.Label( result.position, result.segmentResult.length.ToString( "N2" ) );
            }
#endif
        }

        public void DrawNodeCoordinates( Space space )
        {
#if UNITY_EDITOR
            for( int i = 0; i < localSpline.NodeCount; ++i )
            {
                SplineNode node = localSpline.GetNode(i);
                Vector3 displayPosition = space == Space.World ? TransformPoint( node.position ) : node.position;
                Vector3 guiPosition = TransformPoint( node.position );
                Vector3 offset = Vector3.right * SplineHandleUtility.GetNodeHandleSize( guiPosition ) * 0.5f;
                Handles.Label( guiPosition + offset, string.Format( "{0}{1}", space == Space.World ? "world" : "local", displayPosition.ToString( "N1" ) ) );
            }
#endif
        }
    }
}