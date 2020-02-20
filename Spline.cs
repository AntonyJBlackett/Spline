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
            Vector3 p = bezier.GetPos( t );

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
    public partial class Curve : ISpline
    {
        public const int DEFAULT_SEGMENT_LUT_ACCURACY = 8;
        
        [SerializeField]
        private List<CurvePoint> curvePoints = new List<CurvePoint>();

        public bool loop = false;

        [System.NonSerialized] private bool isDirty = true; 
        [System.NonSerialized] private float _splineLength;
        [System.NonSerialized] private float _invSplineLength;
        [System.NonSerialized] private SegmentCache[] _segments;

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
                if (_segments[seg] == null)
                {
                    _segments[seg] = new SegmentCache(CalculateSegment(seg), length);
                }
                else
                {
                    _segments[seg].Initialise(CalculateSegment(seg), length);
                }
                length += _segments[seg].Length;
            }

            _splineLength = length;
            _invSplineLength = 1;

            if( length > float.Epsilon )
            {
                _invSplineLength = 1f / length;
            }
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
                    return Mathf.Max(0,curvePoints.Count);
                }
                return Mathf.Max(0,curvePoints.Count-1);
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

        public void AddCurvePointAt( int index, CurvePoint point )
        {
            curvePoints.Insert( index, point );
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

            return Mathf.Clamp( segment, 0, SegmentCount-1 );
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

        public SegmentPointer GetSegmentPointer(SegmentPosition segmentPosition)
        {
            EnsureCacheIsUpdated();
            return new SegmentPointer(this, segmentPosition);
        }

        public SegmentPointer GetSegmentPointerAtDistance(float distance)
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

                Vector3 curvePos = curve.GetPos(curveClosestParam);
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

        private SegmentPointer GetClosestSegmentPointer(Ray ray, float paramThreshold = 0.000001f)
        {
            EnsureCacheIsUpdated();

            float minDistSqWorld = float.MaxValue;
            float minDistSqProjected = float.MaxValue;
            SegmentPointer bestSeg = new SegmentPointer(this, 0, 0f);
            bool foundPointInFront = false;
            for (int i = 0; i < SegmentCount; i++)
            {
                Bezier3 curve = _segments[i].bezier;
                Bezier3 projected = Bezier3.ProjectToPlane( curve, ray.origin, ray.direction );

                float curveClosestParam = projected.GetClosestT(ray.origin, paramThreshold);

                Vector3 projectedPos = projected.GetPos(curveClosestParam);
                Vector3 pos = curve.GetPos(curveClosestParam);

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
                        ( distSqProjected < minDistSqProjected )
                        || ( Mathf.Abs( distSqProjected - minDistSqProjected ) < float.Epsilon && distSqWorld < minDistSqWorld )  
                    )
                    {
                        minDistSqProjected = distSqProjected;
                        minDistSqWorld = distSqWorld;
                        bestSeg.segmentIndex = i;
                        bestSeg.segmentDistance = _segments[i].GetDistance( curveClosestParam );
                    }
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
            return GetClosestSegmentPointer( point).Position;
        }

        public Vector3 GetClosestPoint(Ray ray)
        {
            return GetClosestSegmentPointer(ray).Position;
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