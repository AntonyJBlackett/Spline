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
        public static Vector3 GetPoint( Bezier3 bezier, float t )
        {
            t = Mathf.Clamp01( t );
            float oneMinusT = 1f - t;
            return
                oneMinusT * oneMinusT * oneMinusT * bezier.p0 +
                3f * oneMinusT * oneMinusT * t * bezier.p1 +
                3f * oneMinusT * t * t * bezier.p2 +
                t * t * t * bezier.p3;
        }

        public static Vector3 GetTangent( Bezier3 bezier, float t)
        {
            t = Mathf.Clamp01( t );
            float oneMinusT = 1f - t;
            return
                3f * oneMinusT * oneMinusT * (bezier.p1 - bezier.p0) +
                6f * oneMinusT * t * (bezier.p2 - bezier.p1) +
                3f * t * t * (bezier.p3 -bezier.p2);
        }

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
            Vector3 p = GetPoint( bezier, t );

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
        // start, control 1, control 2, end
        public Vector3 p0, p1, p2, p3;
        public Vector3[] points => new Vector3[] { p0, p1, p2, p3 };

        public Bezier3(CurvePoint start, CurvePoint end)
        {
            p0 = start.position;
            p1 = start.position + start.Control2;
            p2 = end.position + end.Control1;
            p3 = end.position;
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
    public class Curve : ISpline
    {
        public List<CurvePoint> points = new List<CurvePoint>();

        public bool loop = false;

        public int PointCount { get { return points.Count; } }
        public int SegmentCount
        {
            get
            {
                if( loop )
                {
                    return points.Count;
                }
                return points.Count - 1;
            }
        }
        public CurvePoint GetPoint(int index)
        {
            return points[index];
        }

        public void AddPoint(CurvePoint point)
        {
            points.Add( point );
        }

        public void RemovePoint(int index)
        {
            if( index < 0 || index > PointCount )
            {
                return;
            }
            points.RemoveAt( index );
        }

        public void InsertPoint(int segement, float t)
        {
            if( segement < 0 || segement > SegmentCount )
            {
                return;
            }

            int index1 = segement;
            int index2 = (segement+1) % PointCount;

            CurvePoint point1 = points[index1];
            CurvePoint point2 = points[index2];

            CurvePoint split = BezierCalculator.SplitAt(ref point1, ref point2, t);
            points[index1] = point1;
            points[index2] = point2;
    
            if(point1.PointType == PointType.Point && point2.PointType == PointType.Point )
            {
                split.SetPointType( PointType.Point );
            }

            points.Insert( segement+1, split );
        }

        public Vector3 GetPointPosition(int segment, float t)
        {
            int index1 = segment;
            int index2 = segment+1;

            index2 %= PointCount;

            return BezierCalculator.GetPoint( new Bezier3( points[index1], points[index2] ), t );
        }

        public void SetPointPosition( int index, Vector3 position )
        {
            if( index < 0 || index > points.Count )
            {
                return;
            }
            CurvePoint point = points[index];
            point.position = position;
            points[index] = point;
        }

        public void SetPoint( int index, CurvePoint point )
        {
            points[index] = point;
        }

        public int LoopIndex( int index )
        {
            return index % PointCount;
        }

        public float GetSpeed(float t)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetDirection(float t)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetPoint(float t)
        {
            throw new System.NotImplementedException();
        }

        public float GetLength(float fromT = 0, float toT = 1)
        {
            throw new System.NotImplementedException();
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
            result.AddRange( points );
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
            for( int i = 1; i < PointCount; ++i )
            {
                Bezier3 bezier = new Bezier3(GetPoint( i - 1 ), GetPoint( i ) );
                Handles.DrawBezier( bezier.p0, bezier.p3, bezier.p1, bezier.p2, Color.grey, null, 2 );
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
        public int PointCount { get { return curve.PointCount; } }

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
            curve.InsertPoint( segment, t );
        }

        public void AddPoint(CurvePoint point)
        {
            curve.AddPoint( point.InverseTransform( transform ) );
        }

        public void RemovePoint(int index)
        {
            curve.RemovePoint( index );
        }

        public CurvePoint GetPoint( int index )
        {
            if( index < 0 || index > PointCount - 1 )
            {
                return new CurvePoint( transform.position );
            }

            return TransformPoint( curve.GetPoint(index) );
        }

        public void SetPoint(int index, CurvePoint point)
        {
            curve.SetPoint( index, point.InverseTransform( transform ) );
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
                    points.Add( TransformPoint( curve.GetPointPosition( index1, t ) ) );
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
        
        //TODO this will break when scaled
        public float GetLength(float fromT = 0, float toT = 1)
        {
            return curve.GetLength(fromT, toT);
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