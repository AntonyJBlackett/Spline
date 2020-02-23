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
        public static CurvePoint SplitAt(ref CurvePoint point1, ref CurvePoint point2, float t)
        {
            Bezier3 bezier = new Bezier3( point1, point2 );

            Vector3 a = Vector3.Lerp( bezier.A, bezier.B, t );
            Vector3 b = Vector3.Lerp( bezier.B, bezier.C, t );
            Vector3 c = Vector3.Lerp( bezier.C, bezier.D, t );
            Vector3 m = Vector3.Lerp( a, b, t );
            Vector3 n = Vector3.Lerp( b, c, t );
            Vector3 p = bezier.GetPosition( t );

            if( point1.PointType == PointType.Mirrored )
            {
                point1.SetPointType( PointType.Aligned );
            }
            point1.Control2 = a - point1.position;

            if( point2.PointType == PointType.Mirrored )
            {
                point2.SetPointType( PointType.Aligned );
            }
            point2.Control1 = c - point2.position;

            CurvePoint newCurvePoint = new CurvePoint( p, m - p, n - p, PointType.Free );
            return newCurvePoint;
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
        private List<CurvePoint> curvePoints = new List<CurvePoint>();

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

        public int CurvePointCount { get { return curvePoints.Count; } }
        public int SegmentCount
        {
            get
            {
                if( loop )
                {
                    return Mathf.Max( 0, curvePoints.Count );
                }
                return Mathf.Max( 0, curvePoints.Count - 1 );
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

        bool IsCurvePointIndexInRange(int index)
        {
            return MathHelper.IsInArrayRange( index, CurvePointCount );
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

        public CurvePoint GetCurvePoint(int index)
        {
            return curvePoints[index];
        }

        public void AddCurvePoint(CurvePoint point)
        {
            curvePoints.Add( point );
            isDirty = true;
        }

        public void AddCurvePointAt(int index, CurvePoint point)
        {
            Debug.Assert( IsCurvePointIndexInRange( index ) );

            curvePoints.Insert( index, point );
            isDirty = true;
        }

        public bool RemoveCurvePoint(int index)
        {
            Debug.Assert( IsCurvePointIndexInRange( index ) );

            curvePoints.RemoveAt( index );
            isDirty = true;
            return true;
        }

        public void InsertCurvePoint(float normalisedT)
        {
            SplineResult result = GetResultAtDistance( normalisedT * Length );
            int segment = result.segmentIndex;
            float segmentT = segments[segment].GetT( result.segmentDistance );

            if( segment < 0 || segment > SegmentCount )
            {
                return;
            }

            int index1 = segment;
            int index2 = (segment + 1) % CurvePointCount;

            CurvePoint point1 = curvePoints[index1];
            CurvePoint point2 = curvePoints[index2];

            CurvePoint split = BezierCalculator.SplitAt( ref point1, ref point2, segmentT );
            curvePoints[index1] = point1;
            curvePoints[index2] = point2;

            if( point1.PointType == PointType.Point && point2.PointType == PointType.Point )
            {
                split.SetPointType( PointType.Point );
            }

            curvePoints.Insert( segment + 1, split );
            isDirty = true;
        }

        public void SetCurvePoint(int index, CurvePoint point)
        {
            Debug.Assert( IsCurvePointIndexInRange( index ) );
            curvePoints[index] = point;
            isDirty = true;
        }

        public Bezier3 CalculateSegment(int segment)
        {
            segment = LoopSegementIndex( segment );
            int index1 = segment;
            int index2 = LoopCurvePointIndex( segment + 1 );
            return new Bezier3( curvePoints[index1], curvePoints[index2] );
        }

        int LoopSegementIndex(int segment)
        {
            if( loop && SegmentCount > 0 )
            {
                return segment % SegmentCount;
            }

            return Mathf.Clamp( segment, 0, SegmentCount - 1 );
        }

        int LoopCurvePointIndex(int index)
        {
            return index % CurvePointCount;
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
            return new SplineResult()
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
        }

        public SplineResult GetResultAtDistance(float distance)
        {
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
            EnsureCacheIsUpdated();

            float minDistSqWorld = float.MaxValue;
            float minDistSqProjected = float.MaxValue;
            SegmentPointer bestSegment = new SegmentPointer();
            bool foundPointInFront = false;
            for( int i = 0; i < SegmentCount; i++ )
            {
                int index1 = i;
                int index2 = i + 1;
                if( index2 >= CurvePointCount )
                {
                    index2 = 0;
                }

                Bezier3 curve = new Bezier3( GetCurvePoint( index1 ), GetCurvePoint( index2 ) );
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