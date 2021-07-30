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
        public static SplineNode SplitAt(ref SplineNode node1, ref SplineNode node2, float t)
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
        [System.NonSerialized] private bool isDirty = true;
        [System.NonSerialized] private float splineLength;
        [System.NonSerialized] private float inverseSplineLength;
        [System.NonSerialized] private SegmentCache[] segments;
        [System.NonSerialized] private int cacheUpdateCount = 0;

        [SerializeField]
        [FormerlySerializedAs( "curvePoints" )]
        private List<SplineNode> nodes = new List<SplineNode>();

        [SerializeField]
        private bool loop = false;
        public bool Loop
        {
            get
            {
                return loop;
            }
            set
            {
                if( value != loop )
                {
                    isDirty = true;
                }
                loop = value;
            }
        }

        public int NodeCount { get { return nodes.Count; } }
        public int SegmentCount
        {
            get
            {
                if( loop )
                {
                    return Mathf.Max( 0, nodes.Count );
                }
                return Mathf.Max( 0, nodes.Count - 1 );
            }
        }

        public float Length
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

        public float GetDistance(float t)
        {
            return t * Length;
        }

        public float GetT(float length)
        {
            return length * InverseLength;
        }

        bool IsNodeIndexInRange(int index)
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

        void UpdateCachedData()
        {
            cacheUpdateCount++;

            if( segments == null || segments.Length != SegmentCount )
            {
                segments = new SegmentCache[SegmentCount];
            }

            float length = 0f;
            for( int seg = 0; seg < SegmentCount; ++seg )
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

            if( length > float.Epsilon )
            {
                inverseSplineLength = 1f / length;
            }
            isDirty = false;
        }

        void EnsureCacheIsUpdated()
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

        public int CreateNode(float normalisedT)
        {
            SplineResult result = GetResultAtDistance( normalisedT * Length );
            int segment = result.segmentResult.index;
            float segmentT = result.segmentResult.t;

            if( segment < 0 || segment > SegmentCount )
            {
                throw new System.Exception("Error creating node at t value: " + normalisedT.ToString());
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

        int LoopSegmentIndex(int segment)
        {
            if( loop && SegmentCount > 0 )
            {
                return segment % SegmentCount;
            }

            return Mathf.Clamp( segment, 0, SegmentCount - 1 );
        }

        public int LoopNodeIndex(int index)
        {
            return ((index %= NodeCount) < 0) ? index + NodeCount : index;
        }
        float LoopNormalisedT(float normalisedT)
        {
            if( loop )
            {
                return Mathf.Repeat( normalisedT, 1 );
            }

            return Mathf.Clamp01( normalisedT );
        }

        float LoopDistance(float distance)
        {
            if( loop )
            {
                return Mathf.Repeat( distance, Length );
            }
        
            return Mathf.Clamp( distance, 0f, Length );
        }

        private SegmentResult GetResultAtSegmentDistanceInternal( int index, float distance )
        {
            if( index < 0 || index >= SegmentCount )
            {
                Debug.LogError( "Segment index out of range." );
                return SegmentResult.Default;
            }

            float t = segments[index].GetT( distance );
            Vector3 localPos = segments[index].GetPositionAtT( t );
            Vector3 localTan = segments[index].GetTangentAtT( t );

            return new SegmentResult()
            {
                index = index,
                distance = distance,
                length = segments[index].Length,
                t = t,
                localPosition = localPos,
                localTangent = localTan,

                // gets transformed later
                position = localPos,
                tangent = localTan,
            };
        }

        private SegmentResult GetSegmentResultAtDistance(float distance)
        {
            float distanceRemaining = LoopDistance( distance );
            for( int i = 0; i < SegmentCount; ++i )
            {
                if( distanceRemaining >= segments[i].Length )
                {
                    distanceRemaining -= segments[i].Length;
                }
                else
                {
                    return GetResultAtSegmentDistanceInternal( i, distanceRemaining );
                }
            }

            return GetResultAtSegmentDistanceInternal( SegmentCount - 1, 1 );
        }

        private SplineResult GetSplineResult(float distance)
        {
            float loopDistance = LoopDistance( distance );
            SegmentResult segmentResult = GetSegmentResultAtDistance( distance );

            SplineResult result = new SplineResult()
            {
                updateCount = UpdateCount,
                distance = distance,
                loopDistance = loopDistance,
                t = distance * inverseSplineLength,
                loopT = loopDistance * inverseSplineLength,
                lapCount = Mathf.FloorToInt( distance * inverseSplineLength ),
                isLoop = loop,
                length = Length,
                segmentResult = segmentResult
            };

            if( Mathf.Approximately( result.tangent.sqrMagnitude, 0 ) )
            {
                result.segmentResult.tangent = Vector3.forward;
            }

            return result;
        }

        public SplineResult GetResultAtDistance(float distance)
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();
            return GetSplineResult( distance );
        }

        public SplineResult GetResultAtT(float t)
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();
            return GetSplineResult( Length * t );
        }

        public SplineResult GetResultAtSegmentDistance(int segmentIndex, float segmentDistance)
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();

            float segmentLength = segments[segmentIndex].Length;
            int loopedSegmentIndex = LoopSegmentIndex( segmentIndex );
            segmentDistance = Mathf.Clamp( segmentDistance, 0, segmentLength );

            SegmentResult segmentResult = GetResultAtSegmentDistanceInternal( loopedSegmentIndex, segmentDistance );

            int lapCount = segmentIndex / SegmentCount;
            float splineDistance = lapCount * Length;
            for( int i = 0; i < loopedSegmentIndex; ++i )
            {
                splineDistance += segments[i].Length;
            }
            splineDistance += segmentResult.distance;
            float loopSplineDistance = LoopDistance( splineDistance );

            SplineResult result = new SplineResult()
            {
                updateCount = UpdateCount,
                distance = splineDistance,
                loopDistance = loopSplineDistance,
                t = splineDistance * inverseSplineLength,
                loopT = loopSplineDistance * inverseSplineLength,
                lapCount = lapCount,
                isLoop = loop,
                length = Length,
                segmentResult = segmentResult
            };

            return result;
        }

        public SplineResult GetResultAtSegmentT(int segmentIndex, float segmentT)
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();
            segmentIndex = LoopSegmentIndex( segmentIndex );
            segmentT = Mathf.Clamp01( segmentT );
            return GetResultAtSegmentDistance( segmentIndex, segments[segmentIndex].GetDistance( segmentT ) );
        }

        public SplineResult GetResultClosestToSegment( int segmentIndex, Vector3 point, float paramThreshold = 0.000001f )
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();
            segmentIndex = LoopSegmentIndex( segmentIndex );

            Bezier3 curve = segments[segmentIndex].bezier;
            float segmentT = curve.GetClosestT( point, paramThreshold );
            return GetResultAtSegmentDistance( segmentIndex, segments[segmentIndex].GetDistance( segmentT ) );
        }

        public SplineResult GetResultClosestTo(Vector3 point, float paramThreshold = 0.000001f)
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();

            float minDistSq = float.MaxValue;
            float bestDistance = 0;
            for( int i = 0; i < SegmentCount; i++ )
            {
                Bezier3 curve = segments[i].bezier;
                float curveClosestParam = curve.GetClosestT( point, paramThreshold );

                Vector3 curvePos = curve.GetPosition( curveClosestParam );
                float distSq = (curvePos - point).sqrMagnitude;
                if( distSq < minDistSq )
                {
                    minDistSq = distSq;
                    bestDistance = segments[i].startDistanceInSpline + segments[i].GetDistance( curveClosestParam );
                }
            }

            return GetSplineResult( bestDistance );
        }

        public SplineResult GetResultClosestTo(Ray ray, float paramThreshold = 0.000001f)
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();

            float minDistSqWorld = float.MaxValue;
            float minDistSqProjected = float.MaxValue;
            float bestDistance = 0;
            bool foundPointInFront = false;
            for( int i = 0; i < SegmentCount; i++ )
            {
                int index1 = i;
                int index2 = i + 1;
                if( index2 >= NodeCount )
                {
                    index2 = 0;
                }

                Bezier3 curve = new Bezier3( GetNode( index1 ), GetNode( index2 ) );
                Bezier3 projected = Bezier3.ProjectToPlane( curve, ray.origin, ray.direction );

                float curveClosestParam = projected.GetClosestT( ray.origin, paramThreshold );

                Vector3 projectedPos = projected.GetPosition( curveClosestParam );
                Vector3 pos = curve.GetPosition( curveClosestParam );

                bool infront = Vector3.Dot( ray.direction, pos - ray.origin ) >= 0;
                if( infront || !foundPointInFront )
                {
                    if( !foundPointInFront )
                    {
                        minDistSqWorld = float.MaxValue;
                        minDistSqProjected = float.MaxValue;
                        foundPointInFront = true;
                    }

                    float distSqProjected = (projectedPos - ray.origin).sqrMagnitude;
                    float distSqWorld = (pos - ray.origin).sqrMagnitude;
                    if(
                        (distSqProjected < minDistSqProjected)
                        || (Mathf.Abs( distSqProjected - minDistSqProjected ) < float.Epsilon && distSqWorld < minDistSqWorld)
                    )
                    {
                        minDistSqProjected = distSqProjected;
                        minDistSqWorld = distSqWorld;
                        bestDistance = segments[i].startDistanceInSpline + segments[i].GetDistance( curveClosestParam );
                    }
                }
            }

            return GetSplineResult( bestDistance );
        }

        public SplineResult GetResultAtNode( int nodeIndex )
        {
            if( segments.Length == 0 )
            {
                Debug.LogError( "Node index out of range." );
                return new SplineResult();
            }

            if( nodeIndex == segments.Length )
            {
                return GetResultAtSegmentT( segments.Length-1, 1 );
            }

            return GetResultAtSegmentT( nodeIndex, 0 );
        }

        public void OnDrawGizmos( Color color, float gizmoScale )
        {
#if UNITY_EDITOR
            EnsureCacheIsUpdated();
            for( int i = 0; i < segments.Length; ++i )
            {
                Bezier3 bezier = segments[i].bezier;
                Handles.DrawBezier( bezier.start, bezier.end, bezier.B, bezier.C, color * .9f, null, 3f );
            }

            // this stops selection of the spline when we're doing other things.
            if( Selection.activeObject == null )
            {
                Gizmos.color = Color.white;
                for( int i = 0; i < NodeCount; ++i )
                {
                    float size = SplineHandleUtility.GetNodeHandleSize( nodes[i].position );
                    Gizmos.DrawSphere( nodes[i].position, size * 0.5f * gizmoScale );
                }
            }

            DrawDirecitonIndicators( color, gizmoScale );
#endif
        }

        public void DrawDirecitonIndicators( Color color, float gizmoScale )
        {
#if UNITY_EDITOR
            using( new Handles.DrawingScope( color ) )
            {
                Handles.color = color;
                for( int i = 0; i < SegmentCount; ++i )
                {
                    if( loop && i == SegmentCount - 1 )
                    {
                        // differentiate the looped section
                        break;
                    }
                    // direction indicators
                    SplineResult result = GetResultAtSegmentT( i, 0.5f );
                    float arrowSize = SplineHandleUtility.GetNodeHandleSize( result.position ) * 0.3f;
                    Handles.ConeHandleCap( 0, result.position + result.localTangent.normalized * arrowSize, Quaternion.LookRotation( result.localTangent.normalized, Vector3.up ), arrowSize * gizmoScale, EventType.Repaint );
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