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

            Vector3 a = Vector3.Lerp( bezier.p0, bezier.p1, t );
            Vector3 b = Vector3.Lerp( bezier.p1, bezier.p2, t );
            Vector3 c = Vector3.Lerp( bezier.p2, bezier.p3, t );
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
        public Vector3 p0 => bezier3.A;
        public Vector3 p1 => bezier3.B;
        public Vector3 p2 => bezier3.C;
        public Vector3 p3 => bezier3.D;
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
                3f * oneMinusT * oneMinusT * (p1 - p0) +
                6f * oneMinusT * t * (p2 - p1) +
                3f * t * t * (p3 -p2);
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
        public List<CurvePoint> curvePoints = new List<CurvePoint>();

        public bool loop = false;

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
        }

        public void RemoveCurvePoint(int index)
        {
            if( index < 0 || index > CurvePointCount )
            {
                return;
            }
            curvePoints.RemoveAt( index );
        }

        public void InsertCurvePoint(int segement, float t)
        {
            if( segement < 0 || segement > SegmentCount )
            {
                return;
            }

            int index1 = segement;
            int index2 = (segement+1) % CurvePointCount;

            CurvePoint point1 = curvePoints[index1];
            CurvePoint point2 = curvePoints[index2];

            CurvePoint split = BezierCalculator.SplitAt(ref point1, ref point2, t);
            curvePoints[index1] = point1;
            curvePoints[index2] = point2;
    
            if(point1.PointType == PointType.Point && point2.PointType == PointType.Point )
            {
                split.SetPointType( PointType.Point );
            }

            curvePoints.Insert( segement+1, split );
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
        }

        public void SetCurvePoint( int index, CurvePoint point )
        {
            curvePoints[index] = point;
        }

        public Bezier3 GetSegment( int segment )
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

        float GetSegmentT( float normalisedT )
        {
            if( loop )
            {
                return Mathf.Repeat( normalisedT * SegmentCount, 1 );
            }
            else
            {
                float totalT = Mathf.Clamp01(normalisedT) * SegmentCount;
                int segment = GetSegmentIndex( normalisedT );
                return totalT - segment;
            }
        }

        int GetSegmentIndex( float normalisedT )
        {
            normalisedT = LoopNormalisedT( normalisedT );
            int segment = (int)(normalisedT * SegmentCount);
            return Mathf.Clamp( segment, 0, SegmentCount-1 );
        }

        public float GetSpeed(float normalisedT)
        {
            return GetSegment( GetSegmentIndex( normalisedT ) ).GetTangent( GetSegmentT( normalisedT ) ).magnitude;
        }

        public Vector3 GetDirection(float normalisedT)
        {
            return GetSegment( GetSegmentIndex( normalisedT ) ).GetTangent( GetSegmentT( normalisedT ) );
        }

        public Vector3 GetPoint( float normalisedT )
        {
            return GetSegment( GetSegmentIndex( normalisedT ) ).GetPoint( GetSegmentT( normalisedT ) );
        }

        public float GetLength(float fromNormalisedT = 0, float toNormalisedT = 1)
        {
            float length = 0;

            int fromSegment = GetSegmentIndex( fromNormalisedT );
            float fromSegmentT = GetSegmentT( fromNormalisedT );

            int toSegment = GetSegmentIndex( toNormalisedT );
            float toSegmentT = GetSegmentT( toNormalisedT );
            for( int i = fromSegment; i <= toSegment; ++i )
            {
                length += GetSegment( i ).Length;
            }

            length -= GetSegment( fromSegment ).Length * fromSegmentT;
            length -= GetSegment( toSegment ).Length * (1-toSegmentT);

            return length;
        }

        public float GetT(float length)
        {
            throw new System.NotImplementedException();
        }

        public float GetClosestT(Vector3 point)
        {
            throw new System.NotImplementedException();
        }

        public float GetClosestT(Ray ray)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetClosestPoint(Vector3 point)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetClosestPoint(Ray ray)
        {
            throw new System.NotImplementedException();
        }

        public float Step(float t, float worldDistance)
        {
            throw new System.NotImplementedException();
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

    public class Spline : MonoBehaviour, ISpline, IEditableSpline
    {
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if( Selection.activeObject == gameObject )
            {
                return;
            }
            for( int i = 0; i < curve.SegmentCount; ++i )
            {
                Bezier3 bezier = curve.GetSegment(i);
                Handles.DrawBezier( bezier.start, bezier.end, bezier.startTargent, bezier.endTargent, Color.grey, null, 2 );
            }
            Gizmos.color = Color.white;
            for( int i = 0; i < PointCount; ++i )
            {
                Gizmos.DrawSphere( GetPoint( i ).position, 0.05f );
            }
        }
#endif

        public Curve curve; // spline in local space
        public bool Loop { get { return curve.loop; } set { curve.loop = value; } }
        public int PointCount { get { return curve.CurvePointCount; } }

        Vector3 InverseTransformPoint( Vector3 point )
        {
            return transform.InverseTransformPoint( point );
        }

        Vector3 TransformPoint( Vector3 point )
        {
            return transform.TransformPoint( point );
        }

        Vector3 InverseTransformVector( Vector3 vector )
        {
            return transform.InverseTransformVector( vector );
        }

        Vector3 TransformVector( Vector3 vector )
        {
            return transform.TransformVector( vector );
        }

        Vector3 InverseTransformDirection( Vector3 direction )
        {
            return transform.InverseTransformDirection( direction );
        }

        Vector3 TransformDirection( Vector3 direction )
        {
            return transform.TransformDirection( direction );
        }

        CurvePoint TransformPoint( CurvePoint point )
        {
            return point.Transform( transform );
        }

        CurvePoint InverseTransformPoint( CurvePoint point )
        {
            return point.InverseTransform( transform );
        }

        List<Vector3> TransformPoints( List<Vector3> points )
        {
            for( int i = 0; i < points.Count; ++i )
            {
                points[i] = TransformPoint( points[i] );
            }
            return points;
        }

        List<CurvePoint> TransformPoints( List<CurvePoint> points )
        {
            for( int i = 0; i < points.Count; ++i )
            {
                points[i] = TransformPoint( points[i] );
            }
            return points;
        }

        Ray InverseTransformRay( Ray ray )
        {
            ray.origin = InverseTransformPoint( ray.origin );
            ray.direction = InverseTransformPoint( ray.direction );
            return ray;
        }

        public bool IsIndexInRange( int index )
        {
            return index >= 0 && index < PointCount;
        }

        public void InsertPoint( int segment, float t )
        {
            curve.InsertCurvePoint( segment, t );
        }

        public void AddPoint(CurvePoint point)
        {
            curve.AddCurvePoint( point.InverseTransform( transform ) );
        }

        public void RemovePoint(int index)
        {
            curve.RemoveCurvePoint( index );
        }

        public CurvePoint GetPoint( int index )
        {
            if( index < 0 || index > PointCount - 1 )
            {
                return new CurvePoint( transform.position );
            }

            return TransformPoint( curve.GetCurvePoint(index) );
        }

        public void SetPoint(int index, CurvePoint point)
        {
            curve.SetCurvePoint( index, point.InverseTransform( transform ) );
        }

        public bool IsLoop() => Loop;
        public void SetLoop(bool loop) => Loop = loop;
        public int GetPointCount() => PointCount;
        public Transform GetTransform() => transform;
        public Component GetComponent() => this;

        const int resolution = 10; 
        public List<Vector3> GetPolyLinePoints()
        {
            List<Vector3> points = new List<Vector3>();
            if( PointCount < 2 )
            {
                return points;
            }

            int curveSegments = PointCount;
            if( Loop )
            {
                curveSegments += 1;
            }
            for( int i = 1; i < curveSegments; ++i )
            {
                float step = 1.0f / (float)resolution;
                float t = 0;

                int index1 = (i - 1);
                int index2 = i % PointCount;

                CurvePoint point1 = GetPoint(index1);
                CurvePoint point2 = GetPoint(index2);

                if( point1.PointType == PointType.Point && point2.PointType == PointType.Point )
                {
                    points.Add( point1.position );
                    if( i == curveSegments - 1 )
                    {
                        points.Add( point2.position );
                    }
                    continue;
                }

                for( int s = 0; s < resolution+1; ++s )
                {
                    points.Add( TransformPoint( curve.GetSegment( index1 ).GetPoint( t ) ) );
                    t += step;
                }
            }
            return points;
        }
        public List<int> GetSegmentsForPoints()
        {
            List<int> segments = new List<int>();

            int curveSegments = PointCount;
            if( Loop )
            {
                curveSegments += 1;
            }
            for( int i = 1; i < curveSegments; ++i )
            {
                int index1 = (i - 1);
                int index2 = i % PointCount;

                CurvePoint point1 = GetPoint(index1);
                CurvePoint point2 = GetPoint(index2);

                if( point1.PointType == PointType.Point && point2.PointType == PointType.Point )
                {
                    segments.Add( index1 );
                    if( i == curveSegments - 1 )
                    {
                        segments.Add( index1 );
                    }
                    continue;
                }

                for( int s = 0; s < resolution+1; ++s )
                {
                    segments.Add( index1 );
                }
            }
            return segments;
        }

        public int GetClosestSegmentIndex(Ray ray)
        {
            throw new System.NotImplementedException();
        }

        public List<CurvePoint> GetPoints()
        {
            return TransformPoints( curve.GetPoints() );
        }
        
        //TODO this will break when scaled
        public float GetSpeed(float t)
        {
            return curve.GetSpeed(t);
        }

        public Vector3 GetDirection(float t)
        {
            return TransformVector( curve.GetDirection(t) );
        }

        public Vector3 GetPoint(float t)
        {
            return TransformPoint( curve.GetPoint(t) );
        }
        
        public float GetLength()
        {
            return curve.GetLength(0, 1);
        }
        public float GetLength(float toNormalisedT)
        {
            return curve.GetLength(0, toNormalisedT);
        }
        //TODO this will break when scaled
        public float GetLength(float fromNormalisedT, float toNormalisedT)
        {
            return curve.GetLength(fromNormalisedT, toNormalisedT);
        }
        
        //TODO this will break when scaled
        public float GetT(float length)
        {
            return curve.GetT(length);
        }

        public float GetClosestT(Vector3 point)
        {
            return curve.GetClosestT( InverseTransformPoint( point ) );
        }

        public float GetClosestT(Ray ray)
        {
            return curve.GetClosestT( InverseTransformRay( ray ) );
        }

        public Vector3 GetClosestPoint(Vector3 point)
        {
            return TransformPoint( curve.GetClosestPoint( InverseTransformPoint( point ) ) );
        }

        public Vector3 GetClosestPoint(Ray ray)
        {
            return TransformPoint( curve.GetClosestPoint( InverseTransformRay( ray ) ) );
        }
        
        //TODO this will break when scaled
        public float Step(float currentT, float worldDistance)
        {
            return curve.Step(currentT, worldDistance);
        }

        //TODO this will break when scaled
        public List<Vector3> GetPoints(float worldSpacing, bool includeEndPoint = true, bool includeSplinePoints = false)
        {
            return TransformPoints( GetPoints( worldSpacing, includeEndPoint, includeSplinePoints ) );
        }
    }
}