using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Assertions;
#endif

namespace FantasticSplines
{
    public static class BezierCalculator
    {

        /// <summary>
        /// Splits the curve at given position (t : 0..1). alters point1 and point2 control points so the curve remains the same
        /// </summary>
        /// <param name="t">A number from 0 to 1.</param>
        /// <returns>new CurvePoint between two points</returns>
        /// <remarks>
        /// (De Casteljau's algorithm, see: http://caffeineowl.com/graphics/2d/vectorial/bezierintro.html)
        /// </remarks>
        public static SplineNode SplitAt(ref SplineNode node1, ref SplineNode node2, float t)
        {
            Bezier3 bezier = new Bezier3( node1, node2 );

            Vector3 a = Vector3.Lerp( bezier.A, bezier.B, t );
            Vector3 b = Vector3.Lerp( bezier.B, bezier.C, t );
            Vector3 c = Vector3.Lerp( bezier.C, bezier.D, t );
            Vector3 m = Vector3.Lerp( a, b, t );
            Vector3 n = Vector3.Lerp( b, c, t );
            Vector3 p = bezier.GetPosition( t );

            if( node1.NodeType == NodeType.Mirrored )
            {
                node1.SetNodeType( NodeType.Aligned );
            }
            node1.Control2 = a - node1.position;

            if( node2.NodeType == NodeType.Mirrored )
            {
                node2.SetNodeType( NodeType.Aligned );
            }
            node2.Control1 = c - node2.position;

            SplineNode newNode = new SplineNode( p, m - p, n - p, NodeType.Free );
            return newNode;
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
            int segment = result.segmentIndex;
            float segmentT = segments[segment].GetT( result.segmentDistance );

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

        private struct SegmentPointer
        {
            public SegmentPointer(int index, float distance)
            {
                this.index = index;
                this.distance = distance;
            }

            public int index;
            public float distance;
        }

        private SplineResult GetSplineResult(SegmentPointer pointer)
        {
            float splineDistance = segments[pointer.index].startDistanceInSpline + pointer.distance;
            SplineResult result = new SplineResult()
            {
                splineDistance = splineDistance,
                splineT = splineDistance * inverseSplineLength,

                position = segments[pointer.index].GetPositionAtDistance( pointer.distance ),
                tangent = segments[pointer.index].GetTangentAtDistance( pointer.distance ),

                segmentT = segments[pointer.index].GetT( pointer.distance ),
                segmentDistance = pointer.distance,
                segmentIndex = pointer.index,
                segmentLength = segments[pointer.index].Length
            };

            if( Mathf.Approximately( result.tangent.sqrMagnitude, 0 ) )
            {
                result.tangent = Vector3.forward;
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

            float distanceRemain = LoopDistance( distance );
            for( int i = 0; i < segments.Length; ++i )
            {
                if( distanceRemain >= segments[i].Length )
                {
                    distanceRemain -= segments[i].Length;
                }
                else
                {
                    return GetSplineResult( new SegmentPointer( i, distanceRemain ) );
                }
            }
            return GetSplineResult( new SegmentPointer( segments.Length - 1, segments[segments.Length - 1].Length ) );
        }

        public SplineResult GetResultClosestTo(Vector3 point, float paramThreshold = 0.000001f)
        {
            if( SegmentCount == 0 )
            {
                return SplineResult.Default;
            }

            EnsureCacheIsUpdated();

            float minDistSq = float.MaxValue;
            SegmentPointer bestSegment = new SegmentPointer();
            for( int i = 0; i < SegmentCount; i++ )
            {
                Bezier3 curve = segments[i].bezier;
                float curveClosestParam = curve.GetClosestT( point, paramThreshold );

                Vector3 curvePos = curve.GetPosition( curveClosestParam );
                float distSq = (curvePos - point).sqrMagnitude;
                if( distSq < minDistSq )
                {
                    minDistSq = distSq;
                    bestSegment.index = i;
                    bestSegment.distance = segments[i].GetDistance( curveClosestParam );
                }
            }

            return GetSplineResult( bestSegment );
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
            SegmentPointer bestSegment = new SegmentPointer();
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
                        bestSegment.index = i;
                        bestSegment.distance = segments[i].GetDistance( curveClosestParam );
                    }
                }
            }

            return GetSplineResult( bestSegment );
        }
    }
}