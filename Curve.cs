using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using UnityEngine.Serialization;
#endif

namespace FantasticSplines
{
    public static class BezierCalculator
    {
        public static SplineNode SplitAt(ref SplineNode node1, ref SplineNode node2, float t)
        {
            Bezier3 segement = new Bezier3( node1, node2 );
            Bezier3 leftSplit = segement.LeftSplitAt( t );
            Bezier3 rightSplit = segement.RightSplitAt( t );

            SplineNode split = new SplineNode( leftSplit.end, leftSplit.endControl, rightSplit.startControl );
            node1.SetNodeType( NodeType.Free );
            node1.Control2 = leftSplit.startControl;
            node1.SetNodeType( SplineNode.GetNodeTypeFromControls( node1 ) );

            node2.SetNodeType( NodeType.Free );
            node2.Control1 = rightSplit.endControl;
            node2.SetNodeType( SplineNode.GetNodeTypeFromControls( node2 ) );

            return split;
        }
    }

    [System.Serializable]
    public partial class Curve
    {
        [System.NonSerialized] private bool isDirty = true;
        [System.NonSerialized] private float splineLength;
        [System.NonSerialized] private float inverseSplineLength;
        [System.NonSerialized] private SegmentCache[] segments;

        [SerializeField]
        [FormerlySerializedAsAttribute( "curvePoints" )]
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
            return MathHelper.IsInArrayRange( index, NodeCount );
        }

        void UpdateCachedData()
        {
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
            Debug.Assert( IsNodeIndexInRange( index ) );

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

        public void InsertNode(float normalisedT)
        {
            SplineResult result = GetResultAtDistance( normalisedT * Length );
            int segment = result.segmentResult.index;
            float segmentT = result.segmentResult.t;

            if( segment < 0 || segment > SegmentCount )
            {
                return;
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

            nodes.Insert( segment + 1, split );
            isDirty = true;
        }

        public void SetNode(int index, SplineNode node)
        {
            Debug.Assert( IsNodeIndexInRange( index ) );
            nodes[index] = node;
            isDirty = true;
        }

        public Bezier3 CalculateSegment(int segment)
        {
            segment = LoopSegementIndex( segment );
            int index1 = segment;
            int index2 = LoopNodeIndex( segment + 1 );
            return new Bezier3( nodes[index1], nodes[index2] );
        }

        int LoopSegementIndex(int segment)
        {
            if( loop && SegmentCount > 0 )
            {
                return segment % SegmentCount;
            }

            return Mathf.Clamp( segment, 0, SegmentCount - 1 );
        }

        int LoopNodeIndex(int index)
        {
            return index % NodeCount;
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

        private SegmentResult GetSegmentResultAtDistance(float distance)
        {
            float distanceRemaining = LoopDistance( distance );
            for( int i = 0; i < segments.Length; ++i )
            {
                if( distanceRemaining >= segments[i].Length )
                {
                    distanceRemaining -= segments[i].Length;
                }
                else
                {
                    return new SegmentResult()
                    {
                        index = i,
                        distance = distanceRemaining,
                        length = segments[i].Length,
                        t = segments[i].GetT( distanceRemaining ),
                        position = segments[i].GetPositionAtDistance( distanceRemaining ),
                        tangent = segments[i].GetTangentAtDistance( distanceRemaining ),
                    };
                }
            }

            int index = segments.Length - 1;
            return new SegmentResult()
            {
                index = index,
                distance = segments[index].Length,
                length = segments[index].Length,
                t = 1,
                position = segments[index].GetPositionAtT( 1 ),
                tangent = segments[index].GetTangentAtT( 1 )
            };
        }

        private SplineResult GetSplineResult(float distance)
        {
            float loopDistance = LoopDistance( distance );
            SegmentResult segmentResult = GetSegmentResultAtDistance( distance );

            SplineResult result = new SplineResult()
            {
                distance = distance,
                loopDistance = loopDistance,
                t = distance * inverseSplineLength,
                loopT = loopDistance * inverseSplineLength,
                lapCount = Mathf.FloorToInt( distance * inverseSplineLength ),
                isLoop = loop,

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
            segmentIndex = LoopSegementIndex( segmentIndex );
            return GetResultAtDistance( segments[segmentIndex].startDistanceInSpline + segmentDistance );
        }

        public SplineResult GetResultAtSegmentT(int segmentIndex, float segmentT)
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();
            segmentIndex = LoopSegementIndex( segmentIndex );
            return GetResultAtDistance( segments[segmentIndex].startDistanceInSpline + segmentT * segments[segmentIndex].Length );
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
    }
}