using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Authors: Antony Blackett, Matthew Clark
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

namespace FantasticSplines
{

    public static class BezierCalculator
    {
        public static SplineNode SplitAt(ref SplineNode node1, ref SplineNode node2, SegmentT t)
        {
            Bezier3 segment = new Bezier3( node1, node2 );
            Bezier3 leftSplit = segment.LeftSplitAt( t );
            Bezier3 rightSplit = segment.RightSplitAt( t );

            float averageAutoTangent = (node1.automaticTangentLength + node2.automaticTangentLength) * 0.5f;

            bool wasPoint = node1.NodeType == NodeType.Point;
            SplineNode split = new SplineNode( leftSplit.end, leftSplit.endControl, rightSplit.startControl, averageAutoTangent );
            node1.SetNodeType( NodeType.Free );
            node1.LocalOutControlPoint = leftSplit.startControl;
            if( wasPoint )
            {
                node1.LocalInControlPoint = Vector3.zero;
            }
            node1.SetNodeType( SplineNode.GetNodeTypeFromControls( node1 ) );

            wasPoint = node2.NodeType == NodeType.Point;
            node2.SetNodeType( NodeType.Free );
            node2.LocalInControlPoint = rightSplit.endControl;
            if( wasPoint )
            {
                node2.LocalOutControlPoint = Vector3.zero;
            }
            node2.SetNodeType( SplineNode.GetNodeTypeFromControls( node2 ) );

            return split;
        }
    }

    [System.Serializable]
    public partial class Spline : ISerializationCallbackReceiver
    {
        const float paramAccuracyThreshold = 0.001f;

        public bool isDirty { get; set; } = true;
        [System.NonSerialized] private SplineDistance splineLength;
        [System.NonSerialized] private float inverseSplineLength;
        [System.NonSerialized] private SegmentCache[] segments;
        [System.NonSerialized] private int cacheUpdateCount = 0;

        [SerializeField]
        [FormerlySerializedAs( "loop" )]
        private bool isLoop = false;
        public bool IsLoop
        {
            get
            {
                return isLoop;
            }
            set
            {
                if( value != isLoop )
                {
                    isDirty = true;
                }
                isLoop = value;
            }
        }

        [SerializeField]
        [FormerlySerializedAs( "curvePoints" )]
        public List<SplineNode> nodes = new List<SplineNode>();

        public int NodeCount { get { return nodes.Count; } }
        public int SegmentCount
        {
            get
            {
                if( isLoop )
                {
                    return Mathf.Max( 0, nodes.Count );
                }
                return Mathf.Max( 0, nodes.Count - 1 );
            }
        }

        public SplineDistance Length
        {
            get
            {
                EnsureCacheIsUpdated();
                return splineLength;
            }
        }
        public float InverseLength
        {
            get
            {
                EnsureCacheIsUpdated();
                return inverseSplineLength;
            }
        }

        public SplineDistance GetDistance(SplinePercent t)
        {
            return new SplineDistance( t.value * Length.value );
        }

        public SplinePercent GetT(SplineDistance length)
        {
            return new SplinePercent( length.value * InverseLength );
        }

        private bool IsNodeIndexInRange(int index)
        {
            return MathsUtils.IsInArrayRange( index, NodeCount );
        }

        public int UpdateCount
        {
            get
            {
                if( isDirty )
                {
                    return cacheUpdateCount + 1; // what count we would be on after a GetResult is called.
                }
                return cacheUpdateCount;
            }
        }

        private void UpdateCachedData()
        {
            cacheUpdateCount++;

            int segCount = SegmentCount;
            if( segments == null || segments.Length != segCount )
            {
                segments = new SegmentCache[segCount];
            }

            var length = new SplineDistance( 0f );
            for( int seg = 0; seg < segCount; ++seg )
            {
                if( segments[seg] == null )
                {
                    segments[seg] = new SegmentCache( CalculateSegment( seg ), length );
                }
                else
                {
                    segments[seg].Initialise( CalculateSegment( seg ), length );
                }
                length += segments[seg].Length;
            }

            splineLength = length;
            inverseSplineLength = 1;

            if( length.value > float.Epsilon )
            {
                inverseSplineLength = 1f / length.value;
            }
            isDirty = false;
        }

        private void EnsureCacheIsUpdated()
        {
            if( isDirty )
            {
                UpdateCachedData();
            }
        }

        public SplineNode GetNode(int index)
        {
            return nodes[index];
        }

        public void AddNode(SplineNode node)
        {
            nodes.Add( node );
            isDirty = true;
        }

        public void AddNodeAt(int index, SplineNode node)
        {
            Debug.Assert(index >= 0 && index <= nodes.Count);

            nodes.Insert( index, node );
            isDirty = true;
        }

        public bool RemoveNode(int index)
        {
            Debug.Assert( IsNodeIndexInRange( index ) );

            nodes.RemoveAt( index );
            isDirty = true;
            return true;
        }

        public void ClearSpline()
        {
            nodes.Clear();
            isDirty = true;
        }

        public int CreateNode(SplinePercent percent )
        {
            SplineResult result = GetResultAt( percent );
            int segment = result.segmentResult.index;
            var segmentT = result.segmentResult.t;

            int segCount = SegmentCount;
            if( segment < 0 || segment > segCount )
            {
                throw new System.Exception( "Error creating node at spline percent value: " + percent.ToString());
            }

            int index1 = segment;
            int index2 = (segment + 1) % NodeCount;

            SplineNode node1 = nodes[index1];
            SplineNode node2 = nodes[index2];

            SplineNode split = BezierCalculator.SplitAt( ref node1, ref node2, segmentT );

            nodes[index1] = node1;
            nodes[index2] = node2;

            if( node1.NodeType == NodeType.Point && node2.NodeType == NodeType.Point )
            {
                split.SetNodeType( NodeType.Point );
            }

            int index = segment + 1;
            nodes.Insert( index, split );
            isDirty = true;

            return index;
        }

        public void SetNode(int index, SplineNode node)
        {
            Debug.Assert( IsNodeIndexInRange( index ) );
            nodes[index] = node;
            isDirty = true;
        }

        public Bezier3 CalculateSegment(int segment)
        {
            segment = LoopSegmentIndex( segment );
            int index1 = segment;
            int index2 = LoopNodeIndex( segment + 1 );
            return new Bezier3( nodes[index1], nodes[index2] );
        }

        private int LoopSegmentIndex(int segment)
        {
            int segCount = SegmentCount;
            while( segment < 0 )
            {
                segment += segCount;
            }

            if( isLoop && segCount > 0 )
            {
                return segment % segCount;
            }

            return Mathf.Clamp( segment, 0, segCount - 1 );
        }

        public int LoopNodeIndex(int index)
        {
            int nodeCount = NodeCount;
            while( index < 0 )
            {
                index += nodeCount;
            }

            if( isLoop && nodeCount > 0 )
            {
                return index % nodeCount;
            }

            return Mathf.Clamp( index, 0, nodeCount - 1 );
        }

        public SplineDistance LoopDistance( SplineDistance distance )
        {
            if( isLoop )
            {
                return new SplineDistance(Mathf.Repeat( distance.value, Length.value ));
            }

            return new SplineDistance(Mathf.Clamp( distance.value, 0f, Length.value ) );
        }

        private SegmentResult CreateSegmentResult( int index, SegmentT t, SegmentDistance segmentDistance )
        {
            Vector3 localPos = segments[index].GetPositionAt( t );
            Vector3 localTan = segments[index].GetTangentAt( t );
            Vector3 localNormal = Vector3.Cross( Vector3.Cross( localTan, Vector3.up ).normalized, localTan ).normalized;

            /*var curvature = segments[index].GetCurvatureAtT( t.value );
            var radius = Bezier3.CalculateRadiusFromCurvature( curvature );*/
            var curvature = 0;
            var radius = float.MaxValue;

            return new SegmentResult()
            {
                index = index,
                distance = segmentDistance,
                length = segments[index].Length,
                percent = new SegmentPercent( segmentDistance / segments[index].Length ),
                t = t,
                localPosition = localPos,
                localTangent = localTan,
                localNormal = localNormal,

                // gets transformed later
                position = localPos,
                tangent = localTan,
                normal = localNormal,

                curvature = curvature,
                radius = radius,
            };
        }

        private SegmentResult GetSegmentResultAt( int index, SegmentDistance segmentDistance )
        {
            int segCount = SegmentCount;
            if( index < 0 || index >= segCount )
            {
                Debug.LogError( "Segment index out of range." );
                return SegmentResult.Default;
            }

            return CreateSegmentResult( index, segments[index].GetT( segmentDistance ), segmentDistance );
        }

        private SegmentResult GetSegmentResultAt(SplineDistance distance)
        {
            var distanceRemaining = LoopDistance( distance );
            int segCount = SegmentCount;
            for( int i = 0; i < segCount; ++i )
            {
                var length = segments[i].Length;
                if( distanceRemaining >= length )
                {
                    distanceRemaining -= length;
                }
                else
                {
                    return GetSegmentResultAt( i, new SegmentDistance( distanceRemaining.value ) );
                }
            }

            int finalIndex = segCount - 1;
            return CreateSegmentResult( finalIndex, SegmentT.End, segments[finalIndex].Length );
        }

        private SplineResult CreateSplineResult(SplineDistance distance)
        {
            var loopDistance = LoopDistance( distance );
            SegmentResult segmentResult = GetSegmentResultAt( distance );

            SplineResult result = new SplineResult()
            {
                updateCount = UpdateCount,
                distance = distance,
                loopDistance = loopDistance,
                percent = new SplinePercent( distance.value * inverseSplineLength ),
                lapCount = Mathf.FloorToInt( distance.value * inverseSplineLength ),
                isLoop = isLoop,
                length = Length,
                segmentResult = segmentResult
            };

            if( Mathf.Approximately( result.tangent.sqrMagnitude, 0 ) )
            {
                result.segmentResult.tangent = Vector3.forward;
            }

            return result;
        }

        public SplineResult GetResultAt(SplineDistance distance)
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();
            return CreateSplineResult( distance );
        }

        public SplineResult GetResultAt(SplinePercent percent )
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();

            return CreateSplineResult( Length * percent.value );
        }

        public SplineResult GetResultAtSegment( int segmentIndex, SegmentPercent segmentPercent )
        {
            int segCount = SegmentCount;
            if( segCount == 0 )
            {
                return SplineResult.Default;
            }
            EnsureCacheIsUpdated();

            var loopedSegmentIndex = LoopSegmentIndex( segmentIndex );
            return GetResultAtSegment( loopedSegmentIndex, segments[loopedSegmentIndex].Length * segmentPercent );
        }

        public SplineResult GetResultAtSegment( int segmentIndex, SegmentDistance segmentDistance )
        {
            int segCount = SegmentCount;
            if( segCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();

            int loopedSegmentIndex = LoopSegmentIndex( segmentIndex );
            segmentDistance = segmentDistance.Clamp( segments[segmentIndex].Length );

            SegmentResult segmentResult = GetSegmentResultAt( loopedSegmentIndex, segmentDistance );

            int lapCount = segmentIndex / segCount;
            var splineDistance = lapCount * Length;
            for( int i = 0; i < loopedSegmentIndex; ++i )
            {
                splineDistance += segments[i].Length;
            }
            splineDistance += segmentResult.distance;
            var loopSplineDistance = LoopDistance( splineDistance );

            SplineResult result = new SplineResult()
            {
                updateCount = UpdateCount,
                distance = splineDistance,
                loopDistance = loopSplineDistance,
                percent = new SplinePercent( splineDistance.value * inverseSplineLength ),
                lapCount = lapCount,
                isLoop = isLoop,
                length = Length,
                segmentResult = segmentResult
            };

            return result;
        }

        public SplineResult GetResultAtSegment(int segmentIndex, SegmentT segmentT )
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();
            segmentIndex = LoopSegmentIndex( segmentIndex );
            segmentT = segmentT.Clamp01();
            return GetResultAtSegment( segmentIndex, segments[segmentIndex].GetDistance( segmentT ) );
        }

        public SplineResult GetResultClosestToSegment( int segmentIndex, Vector3 point, float paramThreshold = paramAccuracyThreshold )
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();
            segmentIndex = LoopSegmentIndex( segmentIndex );

            Bezier3 curve = segments[segmentIndex].bezier;
            var segmentT = curve.GetClosestT( point, paramThreshold );
            return GetResultAtSegment( segmentIndex, segments[segmentIndex].GetDistance( segmentT ) );
        }

        public SplineResult GetResultClosestToSegmentUsingLinearApproximation( int segmentIndex, Vector3 point )
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();
            segmentIndex = LoopSegmentIndex( segmentIndex );

            int nodeIndex1 = LoopNodeIndex( segmentIndex );
            int nodeIndex2 = LoopNodeIndex( segmentIndex + 1 );

            Vector3 previousNode = nodes[nodeIndex1].position;
            Vector3 currentNode = nodes[nodeIndex2].position;

            Vector3 linePointToPoint = point - previousNode;

            Vector3 lineVec = currentNode - previousNode;
            float percentAlongLine = Mathf.Clamp01( Vector3.Dot( linePointToPoint, lineVec.normalized ) / lineVec.magnitude );

            return GetResultAtSegment( segmentIndex, new SegmentPercent( percentAlongLine ) );
        }

        public SplineResult GetResultClosestToSegment( int segmentIndex, Ray ray, TestDirection testDirection = TestDirection.Forward, float paramThreshold = paramAccuracyThreshold )
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();
            segmentIndex = LoopSegmentIndex( segmentIndex );

            var originResult = GetResultClosestToSegment( segmentIndex, ray.origin, paramAccuracyThreshold );

            float minDistSqWorld = (originResult.position - ray.origin).sqrMagnitude;
            float minDistSqProjected = (MathsUtils.ProjectPointOnPlane( ray.origin, ray.direction, originResult.position ) - ray.origin).sqrMagnitude;
            var bestSegmentDistance = originResult.segmentResult.distance;

            if( ClosestRayTest( ray, segments[segmentIndex], SegmentT.Start, SegmentT.End, testDirection, paramThreshold, out var distSqProjected, out var distSqWorld, out var distance, out var segmentDistance ) )
            {
                if( (distSqProjected < minDistSqProjected)
                    || (Mathf.Approximately( distSqProjected, minDistSqProjected ) && distSqWorld < minDistSqWorld)
                )
                {
                    bestSegmentDistance = segmentDistance;
                }
            }

            return GetResultAtSegment( segmentIndex, bestSegmentDistance );
        }

        public SplineResult GetResultClosestToSegmentUsingLinearApproximation( int segmentIndex, Ray ray, TestDirection testDirection = TestDirection.Forward )
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();
            segmentIndex = LoopSegmentIndex( segmentIndex );


            var originResult = GetResultClosestToSegmentUsingLinearApproximation( segmentIndex, ray.origin );

            float minDistSqWorld = (originResult.position - ray.origin).sqrMagnitude;
            float minDistSqProjected = (MathsUtils.ProjectPointOnPlane( ray.origin, ray.direction, originResult.position ) - ray.origin).sqrMagnitude;
            var bestSegmentDistance = originResult.segmentResult.distance;

            if( ClosestRayTestUsingLinearApproximation( ray, segments[segmentIndex], SegmentT.Start, SegmentT.End, testDirection, out var distSqProjected, out var distSqWorld, out var distance, out var segmentDistance ) )
            {
                if( (distSqProjected < minDistSqProjected)
                    || (Mathf.Approximately( distSqProjected, minDistSqProjected ) && distSqWorld < minDistSqWorld)
                )
                {
                    bestSegmentDistance = segmentDistance;
                }
            }

            return GetResultAtSegment( segmentIndex, bestSegmentDistance );
        }

        public SplineResult GetResultClosestTo(Vector3 point, float paramThreshold = 0.001f)
        {
            int segCount = SegmentCount;
            if( segCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();

            float minDistSq = float.MaxValue;
            SplineDistance bestDistance = SplineDistance.Zero;
            for( int i = 0; i < segCount; i++ )
            {
                Bezier3 curve = segments[i].bezier;
                var segmentT = curve.GetClosestT( point, paramThreshold );

                Vector3 curvePos = curve.GetPosition( segmentT );
                float distSq = (curvePos - point).sqrMagnitude;
                if( distSq < minDistSq )
                {
                    minDistSq = distSq;
                    bestDistance = segments[i].startDistanceInSpline + segments[i].GetDistance( segmentT );
                }
            }

            return CreateSplineResult( bestDistance );
        }

        public SplineResult GetResultClosestToWithinDistanceWindow( Vector3 point, SplineDistance beginDistance, SplineDistance endDistance, float paramThreshold = paramAccuracyThreshold )
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();

            beginDistance = LoopDistance( beginDistance );
            endDistance = LoopDistance( endDistance );

            if( beginDistance > endDistance )
            {
                var beginResult = GetResultClosestToWithinDistanceWindowInternal( point, SplineDistance.Zero, endDistance, paramThreshold );
                var endResult = GetResultClosestToWithinDistanceWindowInternal( point, beginDistance, splineLength, paramThreshold );

                if( Vector3.Distance( beginResult.position, point ) < Vector3.Distance( endResult.position, point ) )
                {
                    return beginResult;
                }
                else
                {
                    return endResult;
                }
            }
            else
            {
                return GetResultClosestToWithinDistanceWindowInternal( point, beginDistance, endDistance, paramThreshold );
            }
        }

        private SplineResult GetResultClosestToWithinDistanceWindowInternal( Vector3 point, SplineDistance beginDistance, SplineDistance endDistance, float paramThreshold = paramAccuracyThreshold )
        {
            float minDistSq = float.MaxValue;
            SplineDistance bestDistance = SplineDistance.Zero;

            int segCount = SegmentCount;
            for( int i = 0; i < segCount; i++ )
            {
                if( segments[i].startDistanceInSpline + segments[i].Length <= beginDistance )
                {
                    continue;
                }

                if( segments[i].startDistanceInSpline > endDistance )
                {
                    break;
                }

                SegmentT beginT = segments[i].startDistanceInSpline < beginDistance ? segments[i].GetT( new SegmentDistance( (beginDistance - segments[i].startDistanceInSpline).value ) ) : SegmentT.Start;
                SegmentT endT = segments[i].startDistanceInSpline + segments[i].Length > endDistance ? segments[i].GetT( new SegmentDistance( (endDistance - segments[i].startDistanceInSpline).value ) ) : SegmentT.End;

                Bezier3 curve = segments[i].bezier;

                var segmentT = curve.GetClosestT( point, beginT, endT, paramThreshold );

                Vector3 curvePos = curve.GetPosition( segmentT );
                float distSq = (curvePos - point).sqrMagnitude;
                if( distSq < minDistSq )
                {
                    minDistSq = distSq;
                    bestDistance = segments[i].startDistanceInSpline + segments[i].GetDistance( segmentT );
                }
            }

            return CreateSplineResult( bestDistance );
        }

        public enum TestDirection
        {
            Forward,
            ForwardAndBackward,
        }

        private bool ClosestRayTest( Ray ray, SegmentCache segment, SegmentT beginT, SegmentT endT, TestDirection testDirection, float paramThreshold, out float distSqProjected, out float distSqWorld, out SplineDistance distance, out SegmentDistance segmentDistance )
        {
            distSqProjected = float.MaxValue;
            distSqWorld = float.MaxValue;
            distance = new SplineDistance( 0 );
            segmentDistance = new SegmentDistance( 0 );

            Bezier3 projected = Bezier3.ProjectToPlane( segment.bezier, ray.origin, ray.direction );

            var segmentT = projected.GetClosestT( ray.origin, beginT, endT, paramThreshold );
            Vector3 projectedPos = projected.GetPosition( segmentT );
            Vector3 pos = segment.bezier.GetPosition( segmentT );

            bool allowableDirection = testDirection == TestDirection.ForwardAndBackward || Vector3.Dot( ray.direction, pos - ray.origin ) >= 0;
            if( allowableDirection )
            {
                distSqProjected = (projectedPos - ray.origin).sqrMagnitude;
                distSqWorld = (pos - ray.origin).sqrMagnitude;
                distance = segment.startDistanceInSpline + segment.GetDistance( segmentT );
                segmentDistance = segment.GetDistance( segmentT );
            }

            return allowableDirection;
        }

        private bool ClosestRayTestUsingLinearApproximation( Ray ray, SegmentCache segment, SegmentT beginT, SegmentT endT, TestDirection testDirection, out float distSqProjected, out float distSqWorld, out SplineDistance distance, out SegmentDistance segmentDistance )
        {
            distSqProjected = float.MaxValue;
            distSqWorld = float.MaxValue;
            distance = new SplineDistance( 0 );
            segmentDistance = new SegmentDistance( 0 );

            Vector3 start = segment.GetPositionAt( beginT );
            Vector3 end = segment.GetPositionAt( endT );

            Vector3 projectedStart = MathsUtils.ProjectPointOnPlane( ray.origin, ray.direction, start );
            Vector3 projectedEnd = MathsUtils.ProjectPointOnPlane( ray.origin, ray.direction, end );


            Vector3 linePointToPoint = ray.origin - projectedStart;

            Vector3 lineVec = end - start;
            Vector3 lineVecProjected = projectedEnd - projectedStart;
            float percentAlongLine = Mathf.Clamp01( Vector3.Dot( linePointToPoint, lineVec.normalized ) / lineVec.magnitude );

            var pointOnLineProjected = projectedStart + lineVecProjected * percentAlongLine;
            var pointOnLine = start + lineVec * percentAlongLine;

            bool allowableDirection = testDirection == TestDirection.ForwardAndBackward || Vector3.Dot( ray.direction, pointOnLine - ray.origin ) >= 0;
            if( allowableDirection )
            {
                distSqProjected = (pointOnLineProjected - ray.origin).sqrMagnitude;
                distSqWorld = (pointOnLine - ray.origin).sqrMagnitude;
                distance = segment.startDistanceInSpline + segment.Length * percentAlongLine;
                segmentDistance = segment.Length * percentAlongLine;
            }

            return allowableDirection;
        }

        public SplineResult GetResultClosestTo(Ray ray, TestDirection testDirection = TestDirection.Forward, float paramThreshold = paramAccuracyThreshold )
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();
            return GetResultClosestToWithinDistanceWindowInternal( ray, SplineDistance.Zero, Length, testDirection, paramThreshold );
        }

        public SplineResult GetResultClosestToWithinDistanceWindow( Ray ray, SplineDistance beginDistance, SplineDistance endDistance, TestDirection testDirection = TestDirection.Forward, float paramThreshold = paramAccuracyThreshold )
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();

            beginDistance = LoopDistance( beginDistance );
            endDistance = LoopDistance( endDistance );

            if( beginDistance > endDistance )
            {
                var beginResult = GetResultClosestToWithinDistanceWindowInternal( ray, SplineDistance.Zero, endDistance, testDirection, paramThreshold );
                var endResult = GetResultClosestToWithinDistanceWindowInternal( ray, beginDistance, splineLength, testDirection, paramThreshold );

                Vector3 projectedBegin = MathsUtils.ProjectPointOnPlane( ray.origin, ray.direction, beginResult.position );
                Vector3 projectedEnd = MathsUtils.ProjectPointOnPlane( ray.origin, ray.direction, beginResult.position );

                var p1 = Vector3.Distance( projectedBegin, ray.origin );
                var p2 = Vector3.Distance( projectedEnd, ray.origin );

                if( Mathf.Approximately( p1, p2) )
                {
                    p1 = Vector3.Distance( beginResult.position, ray.origin );
                    p2 = Vector3.Distance( endResult.position, ray.origin );
                }

                if( p1 <= p2 )
                {
                    return beginResult;
                }
                else
                {
                    return endResult;
                }
            }
            else
            {
                return GetResultClosestToWithinDistanceWindowInternal( ray, beginDistance, endDistance, testDirection, paramThreshold );
            }
        }

        private SplineResult GetResultClosestToWithinDistanceWindowInternal( Ray ray, SplineDistance beginDistance, SplineDistance endDistance, TestDirection testDirection, float paramThreshold = paramAccuracyThreshold )
        {
            var originResult = GetResultClosestToWithinDistanceWindow( ray.origin, beginDistance, endDistance, paramAccuracyThreshold );

            float minDistSqWorld = (originResult.position - ray.origin).sqrMagnitude;
            float minDistSqProjected = (MathsUtils.ProjectPointOnPlane( ray.origin, ray.direction, originResult.position ) - ray.origin).sqrMagnitude;
            var bestDistance = originResult.distance;

            int segCount = SegmentCount;
            for( int i = 0; i < segCount; i++ )
            {
                if( segments[i].startDistanceInSpline + segments[i].Length <= beginDistance )
                {
                    continue;
                }

                if( segments[i].startDistanceInSpline > endDistance )
                {
                    break;
                }

                var beginT = segments[i].startDistanceInSpline < beginDistance ? segments[i].GetT( new SegmentDistance( (beginDistance - segments[i].startDistanceInSpline).value ) ) : SegmentT.Start;
                var endT = segments[i].startDistanceInSpline + segments[i].Length > endDistance ? segments[i].GetT( new SegmentDistance( (endDistance - segments[i].startDistanceInSpline).value ) ) : SegmentT.End;

                if( ClosestRayTest( ray, segments[i], beginT, endT, testDirection, paramThreshold, out var distSqProjected, out var distSqWorld, out var distance, out var segmentDistance ) )
                {
                    if( (distSqProjected < minDistSqProjected)
                        || (Mathf.Approximately( distSqProjected, minDistSqProjected ) && distSqWorld < minDistSqWorld)
                    )
                    {
                        minDistSqProjected = distSqProjected;
                        minDistSqWorld = distSqWorld;
                        bestDistance = distance;
                    }
                }
            }

            return CreateSplineResult( bestDistance );
        }

        public SplineResult GetResultClosestToUsingSegmentApproximation( Vector3 point )
        {
            EnsureCacheIsUpdated();

            int closestSegment = 0;
            float closestDistanceSq = float.MaxValue;

            // find approximate segement percent
            int nodeCount = nodes.Count;
            for( int i = 1; i <= nodeCount; ++i )
            {
                if( !isLoop && i == nodeCount )
                {
                    break;
                }

                int previousIndex = i - 1;
                int currentIndex = i == nodeCount ? 0 : i;

                var previousNode = nodes[previousIndex];
                var currentNode = nodes[currentIndex];

                Vector3 linePointToPoint = point - previousNode.position;

                Vector3 lineVec = currentNode.position - previousNode.position;
                float percentAlongLine = Mathf.Clamp01( Vector3.Dot( linePointToPoint, lineVec.normalized ) / lineVec.magnitude );

                var pointOnLine = previousNode.position + lineVec * percentAlongLine;

                float distanceSq = (pointOnLine - point).sqrMagnitude;
                if( distanceSq < closestDistanceSq )
                {
                    closestDistanceSq = distanceSq;
                    closestSegment = i - 1;
                }
            }

            return GetResultClosestToSegment( closestSegment, point );
        }

        public SplineResult GetResultClosestToUsingSegmentApproximation( Ray ray, TestDirection testDirection )
        {
            EnsureCacheIsUpdated();

            var closestPointResult = GetResultClosestToUsingSegmentApproximation( ray.origin );

            int closestSegment = closestPointResult.segmentResult.index;
            float closestProjectedDistanceSq = (ray.origin - MathsUtils.ProjectPointOnPlane( ray.origin, ray.direction, closestPointResult.position )).sqrMagnitude;
            float closestDistanceSq = (ray.origin - closestPointResult.position).sqrMagnitude;

            // find approximate segement percent
            int nodeCount = nodes.Count;
            for( int i = 1; i <= nodeCount; ++i )
            {
                if( !isLoop && i == nodeCount )
                {
                    break;
                }

                int previousIndex = i - 1;
                int currentIndex = i == nodeCount ? 0 : i;

                var previousNode = nodes[previousIndex];
                var currentNode = nodes[currentIndex];

                var projectedPrevious = MathsUtils.ProjectPointOnPlane( ray.origin, ray.direction, previousNode.position );
                var projectedCurrent = MathsUtils.ProjectPointOnPlane( ray.origin, ray.direction, currentNode.position );

                Vector3 linePointToPoint = ray.origin - projectedPrevious;

                Vector3 lineVec = currentNode.position - previousNode.position;
                Vector3 projectedlineVec = projectedCurrent - projectedPrevious;
                float percentAlongLine = Mathf.Clamp01( Vector3.Dot( linePointToPoint, projectedlineVec.normalized ) / projectedlineVec.magnitude );

                var pointOnLine = previousNode.position + lineVec * percentAlongLine;

                float dorRayDirection = Vector3.Dot( pointOnLine - ray.origin, ray.direction );
                if( dorRayDirection < 0 && testDirection == TestDirection.Forward )
                {
                    continue;
                }

                var projectedPointOnLine = projectedPrevious + projectedlineVec * percentAlongLine;
                float projectedDistance = (projectedPointOnLine - ray.origin).sqrMagnitude;
                float distanceSq = (pointOnLine - ray.origin).sqrMagnitude;

                if( projectedDistance < closestProjectedDistanceSq
                    || Mathf.Approximately( closestProjectedDistanceSq, projectedDistance ) && distanceSq < closestDistanceSq )
                {
                    closestProjectedDistanceSq = projectedDistance;
                    closestDistanceSq = distanceSq;
                    closestSegment = i - 1;
                }
            }

            return GetResultClosestToSegment( closestSegment, ray, testDirection );
        }

        public SplineResult GetResultClosestToUsingLinearApproximation( Vector3 point )
        {
            EnsureCacheIsUpdated();

            int closestSegment = 0;
            float closestDistanceSq = float.MaxValue;
            SegmentPercent closestSegmentPercent = new SegmentPercent(0);

            // find approximate segement percent
            int nodeCount = nodes.Count;
            for( int i = 1; i <= nodeCount; ++i )
            {
                if( !isLoop && i == nodeCount )
                {
                    break;
                }

                int previousIndex = i - 1;
                int currentIndex = i == nodeCount ? 0 : i;

                var previousNode = nodes[previousIndex];
                var currentNode = nodes[currentIndex];

                Vector3 linePointToPoint = point - previousNode.position;

                Vector3 lineVec = currentNode.position - previousNode.position;
                float percentAlongLine = Mathf.Clamp01( Vector3.Dot( linePointToPoint, lineVec.normalized ) / lineVec.magnitude );

                var pointOnLine = previousNode.position + lineVec * percentAlongLine;

                float distanceSq = (pointOnLine - point).sqrMagnitude;
                if( distanceSq < closestDistanceSq )
                {
                    closestDistanceSq = distanceSq;
                    closestSegment = i - 1;
                    closestSegmentPercent = new SegmentPercent( percentAlongLine );
                }
            }

            return GetResultAtSegment( closestSegment, closestSegmentPercent );
        }

        public SplineResult GetResultClosestToUsingLinearApproximation( Ray ray, TestDirection testDirection )
        {
            EnsureCacheIsUpdated();

            var closestPointResult = GetResultClosestToUsingLinearApproximation( ray.origin );

            int closestSegment = closestPointResult.segmentResult.index;
            float closestProjectedDistanceSq = (ray.origin - MathsUtils.ProjectPointOnPlane( ray.origin, ray.direction, closestPointResult.position )).sqrMagnitude;
            float closestDistanceSq = (ray.origin - closestPointResult.position).sqrMagnitude;
            var closestSegmentPercent = new SegmentPercent( closestPointResult.segmentResult.distance / closestPointResult.segmentResult.length );

            // find approximate segement percent
            int nodeCount = nodes.Count;
            for( int i = 1; i <= nodeCount; ++i )
            {
                if( !isLoop && i == nodeCount )
                {
                    break;
                }

                int previousIndex = i - 1;
                int currentIndex = i == nodeCount ? 0 : i;

                var previousNode = nodes[previousIndex];
                var currentNode = nodes[currentIndex];

                var projectedPrevious = MathsUtils.ProjectPointOnPlane( ray.origin, ray.direction, previousNode.position );
                var projectedCurrent = MathsUtils.ProjectPointOnPlane( ray.origin, ray.direction, currentNode.position );

                Vector3 linePointToPoint = ray.origin - projectedPrevious;

                Vector3 lineVec = currentNode.position - previousNode.position;
                Vector3 projectedlineVec = projectedCurrent - projectedPrevious;
                float percentAlongLine = Mathf.Clamp01( Vector3.Dot( linePointToPoint, projectedlineVec.normalized ) / projectedlineVec.magnitude );

                var pointOnLine = previousNode.position + lineVec * percentAlongLine;

                float dorRayDirection = Vector3.Dot( pointOnLine - ray.origin, ray.direction );
                if( dorRayDirection < 0 && testDirection == TestDirection.Forward )
                {
                    continue;
                }

                var projectedPointOnLine = projectedPrevious + projectedlineVec * percentAlongLine;
                float projectedDistance = (projectedPointOnLine - ray.origin).sqrMagnitude;
                float distanceSq = (pointOnLine - ray.origin).sqrMagnitude;

                if( projectedDistance < closestProjectedDistanceSq
                    || Mathf.Approximately( closestProjectedDistanceSq, projectedDistance ) && distanceSq < closestDistanceSq )
                {
                    closestProjectedDistanceSq = projectedDistance;
                    closestDistanceSq = distanceSq;
                    closestSegment = i - 1;
                    closestSegmentPercent = new SegmentPercent( percentAlongLine );
                }
            }

            return GetResultAtSegment( closestSegment, closestSegmentPercent );
        }

        public SplineResult GetResultAtNode( int nodeIndex )
        {
            if( SegmentCount == 0 )
            {
                return new SplineResult();
            }

            EnsureCacheIsUpdated();

            if( nodeIndex == segments.Length )
            {
                return GetResultAtSegment( segments.Length-1, SegmentT.End );
            }

            return GetResultAtSegment( nodeIndex, SegmentT.Start );
        }

        public SplineNodeResult GetNodeResult( int nodeIndex )
        {
            if( SegmentCount == 0 )
            {
                return new SplineNodeResult();
            }

            EnsureCacheIsUpdated();

            SplineResult result = GetResultAtNode( nodeIndex );

            var smallDistance = new SplineDistance( 0.01f );
            SplineResult beforeResult = GetResultAt( result.distance - smallDistance );
            SplineResult afterResult = GetResultAt( result.distance + smallDistance );

            Vector3 inTangent = beforeResult.tangent;
            Vector3 outTangent = afterResult.tangent;
            SegmentResult segmentResult = result.segmentResult;
            segmentResult.localTangent = segmentResult.tangent = (inTangent + outTangent) * 0.5f;
            result.segmentResult = segmentResult;

            SplineNodeResult nodeResult = new SplineNodeResult()
            {
                nodeIndex = nodeIndex,
                loopNodeIndex = LoopNodeIndex( nodeIndex ),
                inTangent = inTangent,
                outTangent = outTangent,
                splineNode = GetNode(nodeIndex),
                splineResult = result,
            };

            return nodeResult;
        }

        public void OnDrawGizmos( Color color, float gizmoScale )
        {
#if UNITY_EDITOR
            if( NodeCount <= 0 )
            {
                return;
            }

            EnsureCacheIsUpdated();
            for( int i = 0; i < segments.Length; ++i )
            {
                Bezier3 bezier = segments[i].bezier;
                Handles.DrawBezier( bezier.start, bezier.end, bezier.B, bezier.C, color * .9f, null, 3f );
            }

            float size = SplineHandleUtility.GetNodeHandleSize( nodes[0].position );
            // this stops selection of the spline when we're doing other things.
            if( Selection.activeObject == null )
            {
                Gizmos.color = Color.white;
                for( int i = 0; i < NodeCount; ++i )
                {
                    Gizmos.DrawSphere( nodes[i].position, size * 0.25f * gizmoScale );
                }
            }
#endif
        }

        public void DrawDirecitonIndicators( Color color, float gizmoScale )
        {
#if UNITY_EDITOR
            if( NodeCount <= 0 )
            {
                return;
            }
            using( new Handles.DrawingScope( color ) )
            {
                Handles.color = color;

                float arrowSize = SplineHandleUtility.GetNodeHandleSize( nodes[0].position );
                var result = GetResultAtSegment( 0, new SegmentPercent(0.5f) );

                Handles.ConeHandleCap( 0, result.position + result.tangent.normalized * arrowSize, Quaternion.LookRotation( result.tangent, Vector3.up ), arrowSize * gizmoScale, EventType.Repaint );

                if( SegmentCount >= 2 )
                {
                    result = GetResultAtSegment( SegmentCount / 2, new SegmentPercent( 0.5f ) );
                    Handles.ConeHandleCap( 0, result.position + result.tangent.normalized * arrowSize, Quaternion.LookRotation( result.tangent, Vector3.up ), arrowSize * gizmoScale, EventType.Repaint );
                }
            }
#endif
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            UpdateCachedData();
        }
    }
}