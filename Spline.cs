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
        public static Vector3 GetPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01( t );
            float oneMinusT = 1f - t;
            return
                oneMinusT * oneMinusT * oneMinusT * p0 +
                3f * oneMinusT * oneMinusT * t * p1 +
                3f * oneMinusT * t * t * p2 +
                t * t * t * p3;
        }

        public static Vector3 GetTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01( t );
            float oneMinusT = 1f - t;
            return
                3f * oneMinusT * oneMinusT * (p1 - p0) +
                6f * oneMinusT * t * (p2 - p1) +
                3f * t * t * (p3 - p2);
        }

        /// <summary>
        /// Splits the curve at given position (t : 0..1).
        /// </summary>
        /// <param name="t">A number from 0 to 1.</param>
        /// <returns>Two curves.</returns>
        /// <remarks>
        /// (De Casteljau's algorithm, see: http://caffeineowl.com/graphics/2d/vectorial/bezierintro.html)
        /// </remarks>
        public static CurvePoint SplitAt(ref CurvePoint point1, ref CurvePoint point2, float t)
        {
            Vector3 A = point1.position;
            Vector3 B = point1.position + point1.Control2;
            Vector3 C = point2.position + point2.Control1;
            Vector3 D = point2.position;

            Vector3 a = Vector3.Lerp( A, B, t );
            Vector3 b = Vector3.Lerp( B, C, t );
            Vector3 c = Vector3.Lerp( C, D, t );
            Vector3 m = Vector3.Lerp( a, b, t );
            Vector3 n = Vector3.Lerp( b, c, t );
            Vector3 p = GetPoint( A, B, C, D, t );

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
    public class Curve
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

        public void AddPoint(Vector3 position)
        {
            points.Add( new CurvePoint( position ) );
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

        public Vector3 GetPointPosition( int index )
        {
            if( index < 0 || index > points.Count )
            {
                return Vector3.zero;
            }
            return points[index].position;
        }

        public Vector3 GetPointPosition(int segment, float t)
        {
            int index1 = segment;
            int index2 = segment+1;

            index2 %= PointCount;

            return BezierCalculator.GetPoint( 
                points[index1].position, 
                points[index1].position + points[index1].Control2, 
                points[index2].position + points[index2].Control1, 
                points[index2].position, 
                t );
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
    }

    public class Spline : MonoBehaviour
    {
        public Curve curve;
        public bool Loop { get { return curve.loop; } set { curve.loop = value; } }
        
        const int resolution = 10; 
        public List<Vector3> GetPoints()
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

        Vector3 InverseTransformPoint( Vector3 point )
        {
            point = transform.InverseTransformPoint( point );
            return point;
        }

        Vector3 TransformPoint( Vector3 point )
        {
            point = transform.TransformPoint( point );
            return point;
        }

        public bool IsIndexInRange( int index )
        {
            return index >= 0 && index < PointCount;
        }

        public void InsertPoint( int segment, float t )
        {
            curve.InsertPoint( segment, t );
        }

        public void AddPoint(Vector3 point)
        {
            curve.AddPoint( InverseTransformPoint( point ) );
        }

        public void AddPoint(CurvePoint point)
        {
            curve.AddPoint( point.InverseTransform( transform ) );
        }

        public void RemovePoint(int index)
        {
            curve.RemovePoint( index );
        }

        public Vector3 GetPointPosition(int index)
        {
            return TransformPoint( curve.GetPointPosition( index ) );
        }

        public CurvePoint GetPoint( int index )
        {
            return curve.points[index].Transform( transform );
        }

        public void SetPointPosition( int index, Vector3 point )
        {
            curve.SetPointPosition( index, InverseTransformPoint( point ) );
        }

        public void SetPoint(int index, CurvePoint point)
        {
            curve.SetPoint( index, point.InverseTransform( transform ) );
        }

        public int PointCount { get { return curve.PointCount; } }

        void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if( Selection.activeObject == gameObject )
            {
                return;
            }
#endif
            Gizmos.color = Color.white;
            for( int i = 0; i < PointCount; ++i )
            {
                Gizmos.DrawSphere( GetPointPosition( i ), 0.05f );
            }

            List<Vector3> points = GetPoints();
            for( int i = 1; i < points.Count; ++i )
            {
                Gizmos.DrawLine( points[i-1], points[i] );
            }

            /*
            if( PointCount > 1 )
            {
                Vector3 start = GetPoint( 0 );
                Vector3 next = GetPoint( 1 );
                Vector3 direction = (next - start).normalized;
                Vector3 right = Vector3.Cross( transform.up, direction );

                float arrowSize = 0.1f;
                Vector3 arrowLeft = -right * arrowSize;
                Vector3 arrowRight = right * arrowSize;
                Vector3 arrowHead = direction * arrowSize * 2;

                Mesh arrow = new Mesh();
                arrow.vertices = new Vector3[] { arrowHead, arrowLeft, arrowRight };
                arrow.normals = new Vector3[] { transform.up, transform.up, transform.up };
                arrow.triangles = new int[] { 0, 1, 2, 1, 0, 2 };
                arrow.colors = new Color[] { Color.white, Color.white, Color.white };
                Gizmos.DrawMesh( arrow, 0, start );
            }
            */
        }
    }
}