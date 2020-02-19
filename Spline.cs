using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FantasticSplines
{
    public enum PointType
    {
        Point, // control points ignored
        Free, // free moving control points
        Aligned, // control points are aligned but can be different magnitudes
        Mirrored // control points are aligned and equal in magnitude
    }

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
            Vector3 p = bezier.GetPoint( t );

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

    public struct Bezier3
    {
        FastBezier.Bezier3 bezier3;

        // confused?
        public Vector3 A => bezier3.A;
        public Vector3 B => bezier3.B;
        public Vector3 C => bezier3.C;
        public Vector3 D => bezier3.D;
        public Vector3 start => bezier3.A;
        public Vector3 startTargent => bezier3.B;
        public Vector3 endTargent => bezier3.C;
        public Vector3 end => bezier3.D;

        public Bezier3(CurvePoint start, CurvePoint end)
        {
            bezier3.A = start.position;
            bezier3.B = start.position + start.Control2;
            bezier3.C = end.position + end.Control1;
            bezier3.D = end.position;
        }

        public Bezier3(FastBezier.Bezier3 bez)
        {
            this.bezier3 = bez;
        }

        public Bezier3 LeftSplit(float t)
        {
            if (Mathf.Approximately(t, 1f))
            {
                return this;
            }
            return new Bezier3(bezier3.LeftSplitAt(t));
        }
        public Bezier3 RightSplit(float t)
        {
            if (Mathf.Approximately(t, 0f))
            {
                return this;
            }
            return new Bezier3(bezier3.RightSplitAt(t));
        }
        public Bezier3 MiddleSplit(float t1, float t2)
        {
            return new Bezier3(bezier3.MiddleSplitAt(t1, t2));
        }

        public float GetClosestT(Vector3 pos, float paramThreshold = 0.000001f)
        {
            return GetClosestTRec(pos, 0.0f, 1.0f, paramThreshold);
        }

        float GetClosestTRec(Vector3 pos, float beginT, float endT, float thresholdT)
        {
            float mid = (beginT + endT)/2.0f;

            // Base case for recursion.
            if ((endT - beginT) < thresholdT)
                return mid;

            // The two halves have param range [start, mid] and [mid, end]. We decide which one to use by using a midpoint param calculation for each section.
            float paramA = (beginT+mid) / 2.0f;
            float paramB = (mid+endT) / 2.0f;
        
            Vector3 posA = GetPoint(paramA);
            Vector3 posB = GetPoint(paramB);
            float distASq = (posA - pos).sqrMagnitude;
            float distBSq = (posB - pos).sqrMagnitude;

            if (distASq < distBSq)
                endT = mid;
            else
                beginT = mid;

            // The (tail) recursive call.
            return GetClosestTRec(pos, beginT, endT, thresholdT);
        }

        public Vector3 GetPoint( float t )
        {
            return bezier3.P( t );
        }

        public Vector3 GetTangent( float t )
        {
            t = Mathf.Clamp( t, 0.0001f, 0.9999f ); // clamp like this of there is no tangent at position exactly on the curve point
            float oneMinusT = 1f - t;
            return
                3f * oneMinusT * oneMinusT * (B - A) +
                6f * oneMinusT * t * (C - B) +
                3f * t * t * (D - C);
        }

        public float GetDistanceAt(float t)
        {
            return bezier3.GetDistanceAt(t);
        }
        
        public float Length => bezier3.Length;
    }

    [System.Serializable]
    public struct CurvePoint
    {
        public Vector3 position;

        // relative to position
        [SerializeField]
        Vector3 control1;
        [SerializeField]
        Vector3 control2;

        int lastChangedControl;

        [SerializeField]
        PointType pointType;

        public static Vector3 ConstrainControlPoint(Vector3 master, Vector3 constrain, PointType type)
        {
            switch( type )
            {
                case PointType.Aligned:
                    constrain = -master.normalized * constrain.magnitude;
                    break;
                case PointType.Mirrored:
                    constrain = -master;
                    break;
            }

            return constrain;
        }

        public Vector3 Control1
        {
            get
            {
                if( pointType == PointType.Point )
                {
                    return Vector3.zero;
                }

                return control1;
            }
            set
            {
                control1 = value;
                control2 = ConstrainControlPoint( control1, control2, pointType );
                lastChangedControl = 1;
            }
        }

        public Vector3 Control2
        {
            get
            {
                if( pointType == PointType.Point )
                {
                    return Vector3.zero;
                }

                return control2;
            }
            set
            {
                control2 = value;
                control1 = ConstrainControlPoint( control2, control1, pointType );
                lastChangedControl = 2;
            }
        }

        public PointType PointType
        {
            get
            {
                return pointType;
            }
        }

        public void SetPointType(PointType type)
        {
            pointType = type;

            if( lastChangedControl == 2 )
            {
                control1 = ConstrainControlPoint( control2, control1, type );
            }
            else
            {
                control2 = ConstrainControlPoint( control1, control2, type );
            }
        }

        public CurvePoint(Vector3 position)
        {
            pointType = PointType.Point;
            this.position = position;
            control1 = control2 = Vector3.zero;
            lastChangedControl = 0;
        }

        public CurvePoint(Vector3 position, Vector3 control1, Vector3 control2, PointType type)
        {
            this.position = position;
            this.control1 = control1;
            this.control2 = control2;
            pointType = type;
            lastChangedControl = 0;
        }

        public CurvePoint(CurvePoint other)
        {
            position = other.position;
            control1 = other.control1;
            control2 = other.control2;
            pointType = other.pointType;
            lastChangedControl = other.lastChangedControl;
        }

        public CurvePoint Transform(Transform transform)
        {
            CurvePoint result = this;
            result.position = transform.TransformPoint( result.position );
            result.control1 = transform.TransformVector( result.control1 );
            result.control2 = transform.TransformVector( result.control2 );
            return result;
        }

        public CurvePoint InverseTransform(Transform transform)
        {
            CurvePoint result = this;
            result.position = transform.InverseTransformPoint( result.position );
            result.control1 = transform.InverseTransformVector( result.control1 );
            result.control2 = transform.InverseTransformVector( result.control2 );
            return result;
        }
    }

    
    [System.Serializable]
    public class Curve : ISpline
    {
        public const int DEFAULT_SEGMENT_LUT_ACCURACY = 8;
        
        [SerializeField]
        private List<CurvePoint> curvePoints = new List<CurvePoint>();

        public bool loop = false;

        [System.NonSerialized] private bool isDirty = true; 
        [System.NonSerialized] private float _splineLength;
        [System.NonSerialized] private float _invSplineLength;
        [System.NonSerialized] private SegmentCache[] _segments;

        private struct SegmentPointer
        {
            public SegmentPointer(Curve c, int segIndex, float distance)
            {
                curve = c;
                segmentIndex = segIndex;
                segmentDistance = distance;
            }
            public Curve curve;
            public int segmentIndex;
            public float segmentDistance;

            public Vector3 Position => curve._segments[segmentIndex].GetPositionAtDistance(segmentDistance);
            public Vector3 Tangent => curve._segments[segmentIndex].GetTangentAtDistance(segmentDistance);

            public float DistanceOnSpline
            {
                get
                {
                    float distance = segmentDistance;
                    for (int s = segmentIndex - 1; s >= 0; --s)
                    {
                        distance += curve._segments[s].Length;
                    }
                    return distance;
                }
            }
        }
        
        private class SegmentCache
        {
            public Bezier3 bezier;
            private Vector2[] tdMapping;

            public float Length => tdMapping[tdMapping.Length - 1].y;
            public SegmentCache(Bezier3 bez, int accuracy = DEFAULT_SEGMENT_LUT_ACCURACY)
            {
                this.bezier = bez;
                tdMapping = new Vector2[accuracy];
                float invAccuracy = 1f / (accuracy-1);
                for (int i = 0; i < accuracy; ++i)
                {
                    float t = i * invAccuracy;
                    float d = bez.GetDistanceAt(t);
                    tdMapping[i] = new Vector2(t,d);
                }
            }

            public Vector3 GetPositionAtT(float t) => bezier.GetPoint(t);
            public Vector3 GetTangentAtT(float t) => bezier.GetTangent(t);
            public Vector3 GetPositionAtDistance(float distance) => GetPositionAtT(GetT(distance));
            public Vector3 GetTangentAtDistance(float distance) => GetTangentAtT(GetT(distance));
            
            public float GetT(float d)
            {
                // TODO: Binary search this
                for (int i = 1; i < tdMapping.Length; ++i)
                {
                    if (d <= tdMapping[i].y)
                    {
                        float ratio = Mathf.InverseLerp(tdMapping[i - 1].y, tdMapping[i].y, d);
                        return Mathf.Lerp(tdMapping[i - 1].x, tdMapping[i].x, ratio);
                    }
                }

                return 1f;
            }

            public float GetDistance(float t)
            {
                // TODO: Binary search this
                for (int i = 1; i < tdMapping.Length; ++i)
                {
                    if (t <= tdMapping[i].x)
                    {
                        float ratio = Mathf.InverseLerp(tdMapping[i - 1].x, tdMapping[i].x, t);
                        return Mathf.Lerp(tdMapping[i - 1].y, tdMapping[i].y, ratio);
                    }
                }

                return Length;
            }
        }
        
        public float Length
        {
            get
            {
                EnsureCacheIsUpdated();
                return _splineLength;
            }
        }
        public float InverseLength
        {
            get
            {
                EnsureCacheIsUpdated();
                return _invSplineLength;
            }
        }
        
        void UpdateCachedData()
        {
            if (_segments == null || _segments.Length != SegmentCount)
            {
                _segments = new SegmentCache[SegmentCount];
            }

            float length = 0f;
            for (int seg = 0; seg < SegmentCount; ++seg)
            {
                _segments[seg] = new SegmentCache(CalculateSegment(seg));
                length += _segments[seg].Length;
            }

            _splineLength = length;
            _invSplineLength = 1f / length;
            isDirty = false;
        }

        void EnsureCacheIsUpdated() { if (isDirty) { UpdateCachedData(); } }
        
        public int CurvePointCount { get { return curvePoints.Count; } }
        public int SegmentCount
        {
            get
            {
                if( loop )
                {
                    return curvePoints.Count;
                }
                return curvePoints.Count - 1;
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

        public void RemoveCurvePoint(int index)
        {
            if( index < 0 || index > CurvePointCount )
            {
                return;
            }
            curvePoints.RemoveAt( index );
            isDirty = true;
        }

        public void InsertCurvePoint( float normalisedT )
        {
            SegmentPointer seg = GetSegmentPointerAtDistance(normalisedT * Length);
            int segment = seg.segmentIndex;
            float segmentT = _segments[segment].GetT(seg.segmentDistance);

            if( segment < 0 || segment > SegmentCount )
            {
                return;
            }

            int index1 = segment;
            int index2 = (segment+1) % CurvePointCount;

            CurvePoint point1 = curvePoints[index1];
            CurvePoint point2 = curvePoints[index2];

            CurvePoint split = BezierCalculator.SplitAt(ref point1, ref point2, segmentT);
            curvePoints[index1] = point1;
            curvePoints[index2] = point2;
    
            if(point1.PointType == PointType.Point && point2.PointType == PointType.Point )
            {
                split.SetPointType( PointType.Point );
            }

            curvePoints.Insert( segment+1, split );
            isDirty = true;
        }

        public void SetCurvePointPosition( int index, Vector3 position )
        {
            if( index < 0 || index > curvePoints.Count )
            {
                return;
            }
            CurvePoint point = curvePoints[index];
            point.position = position;
            curvePoints[index] = point;
            isDirty = true;
        }

        public void SetCurvePoint( int index, CurvePoint point )
        {
            curvePoints[index] = point;
            isDirty = true;
        }

        public Bezier3 CalculateSegment( int segment )
        {
            segment = LoopSegementIndex( segment );
            int index1 = segment;
            int index2 = LoopCurvePointIndex(segment+1);
            return new Bezier3( curvePoints[index1], curvePoints[index2] );
        }

        int LoopSegementIndex( int segment )
        {
            if( loop )
            {
                return segment % SegmentCount;
            }

            return Mathf.Clamp( segment, 0, SegmentCount );
        }

        int LoopCurvePointIndex( int index )
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

        public float GetSpeed(float normalisedT)
        {
            return GetDirection(normalisedT).magnitude;
        }

        public Vector3 GetDirection(float normalisedT)
        {
            return GetSegmentPointerAtDistance(normalisedT * Length).Tangent;
        }

        public Vector3 GetPoint( float normalisedT )
        {
            return GetSegmentPointerAtDistance(normalisedT * Length).Position;
        }

        public int GetSegmentIndexAtT( float normalisedT )
        {
            return GetSegmentPointerAtDistance(normalisedT * Length).segmentIndex;
        }

        public float GetLength(float fromNormalisedT, float toNormalisedT)
        {
            return GetDistance(toNormalisedT) - GetDistance(fromNormalisedT);
        }

        SegmentPointer GetSegmentPointerAtDistance(float distance)
        {
            EnsureCacheIsUpdated();

            float distanceRemain = LoopDistance(distance);
            for (int i = 0; i < _segments.Length; ++i)
            {
                if (distanceRemain >= _segments[i].Length)
                {
                    distanceRemain -= _segments[i].Length;
                }
                else
                {
                    return new SegmentPointer(this, i, distanceRemain);
                }
            }
            return new SegmentPointer(this, _segments.Length-1, _segments[_segments.Length-1].Length);
        }

        public float GetDistance(float t)
        {
            return t * Length;
        }

        public float GetT(float length)
        {
            return length * InverseLength;
        }

        private SegmentPointer GetClosestSegmentPointer(Vector3 point, float paramThreshold = 0.000001f)
        {
            EnsureCacheIsUpdated();
            
            float minDistSq = float.MaxValue;
            SegmentPointer bestSeg = new SegmentPointer(this, 0, 0f);
            for (int i = 0; i < SegmentCount; i++)
            {
                Bezier3 curve = _segments[i].bezier;
                float curveClosestParam = curve.GetClosestT(point, paramThreshold);

                Vector3 curvePos = curve.GetPoint(curveClosestParam);
                float distSq = (curvePos - point).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    bestSeg.segmentIndex = i;
                    bestSeg.segmentDistance = _segments[i].GetDistance(curveClosestParam);
                }
            }

            return bestSeg;
        }

        public int GetClosestSegmentIndex(Vector3 point, float paramThreshold = 0.000001f)
        {
            return GetClosestSegmentPointer(point, paramThreshold).segmentIndex;
        }


        public float GetClosestD(Vector3 point)
        {
            return GetClosestSegmentPointer(point).DistanceOnSpline;
        }

        public float GetClosestT(Vector3 point)
        {
            return GetClosestD(point) * InverseLength;
        }

        public float GetClosestT(Ray ray)
        {
            throw new System.NotImplementedException();
        }
        
        public Vector3 GetClosestPoint(Vector3 point)
        {
            return GetClosestSegmentPointer(point).Position;
        }

        public Vector3 GetClosestPoint(Ray ray)
        {
            throw new System.NotImplementedException();
        }

        public float Step(float t, float worldDistance)
        {
            float step = worldDistance * InverseLength;
            return LoopNormalisedT(t + step);
        }

        public List<CurvePoint> GetPoints()
        {
            List<CurvePoint> result = new List<CurvePoint>();
            result.AddRange( curvePoints );
            return result;
        }

        public List<Vector3> GetPoints(float worldSpacing, bool includeEndPoint = true, bool includeSplinePoints = false)
        {
            throw new System.NotImplementedException();
        }
    }

}