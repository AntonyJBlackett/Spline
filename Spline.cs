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
            }
        }

        public PointType PointType
        {
            get
            {
                return pointType;
            }
        }

        public void SetPointType( PointType type )
        {
            pointType = type;
            control2 = ConstrainControlPoint( control1, control2, type );
        }

        public CurvePoint( Vector3 position )
        {
            pointType = PointType.Point;
            this.position = position;
            control1 = control2 = Vector3.zero;
        }

        public CurvePoint( Vector3 position, Vector3 direction, PointType type )
        {
            this.position = position;
            control1 = -direction;
            control2 = direction;
            pointType = type;
        }
    }

    [System.Serializable]
    public class Curve
    {
        public List<CurvePoint> points = new List<CurvePoint>();
        public bool loop = false;

        public int PointCount { get { return points.Count; } }

        public void AddPoint( Vector3 position )
        {
            points.Add( new CurvePoint( position ) );
        }

        public void AddPoint( CurvePoint point )
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

        public void InsertPoint(int index, Vector3 position )
        {
            if( index < 0 || index > PointCount )
            {
                return;
            }

            points.Insert( index, new CurvePoint( position ) );
        }

        public Vector3 GetPointPosition( int index )
        {
            if( index < 0 || index > points.Count )
            {
                return Vector3.zero;
            }
            return points[index].position;
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

        int LoopIndex( int index )
        {
            return index % PointCount;
        }
    }

    public class Spline : MonoBehaviour
    {
        public Curve curve;
        public bool Loop { get { return curve.loop; } set { curve.loop = value; } }

        public List<Vector3> GetPoints()
        {
            List<Vector3> points = new List<Vector3>();
            for( int i = 0; i < PointCount; ++i )
            {
                points.Add( GetPointPosition( i ) );
            }
            return points;
        }

        Vector3 InverseTransformPoint( Vector3 point, Space space )
        {
            if( space == Space.World )
            {
                point = transform.InverseTransformPoint( point );
            }
            return point;
        }

        CurvePoint InverseTransformPoint( CurvePoint point, Space space )
        {
            if( space == Space.World )
            {
                point.position = transform.InverseTransformPoint( point.position );
                point.Control1 = transform.InverseTransformVector( point.Control1 );
                point.Control2 = transform.InverseTransformVector( point.Control2 );
            }
            return point;
        }

        Vector3 TransformPoint( Vector3 point, Space space )
        {
            if( space == Space.World )
            {
                point = transform.TransformPoint( point );
            }
            return point;
        }

        CurvePoint TransformPoint( CurvePoint point, Space space )
        {
            if( space == Space.World )
            {
                point.position = transform.TransformPoint( point.position );
                point.Control1 = transform.TransformVector( point.Control1 );
                point.Control2 = transform.TransformVector( point.Control2 );
            }
            return point;
        }

        public bool IsIndexInRange( int index )
        {
            return index >= 0 && index < PointCount;
        }

        public void InsertPoint( int index, Vector3 point, Space space )
        {
            curve.InsertPoint( index, InverseTransformPoint( point, space ) );
        }

        public void AddPoint(Vector3 point, Space space)
        {
            curve.AddPoint( InverseTransformPoint( point, space ) );
        }

        public void AddPoint(CurvePoint point, Space space)
        {
            curve.AddPoint( InverseTransformPoint( point, space ) );
        }

        public void RemovePoint(int index)
        {
            curve.RemovePoint( index );
        }

        public Vector3 GetPointPosition(int index)
        {
            return TransformPoint( curve.GetPointPosition( index ), Space.World );
        }

        public CurvePoint GetPoint( int index )
        {
            return TransformPoint( curve.points[index], Space.World );
        }

        public void SetPoint( int index, Vector3 point, Space space )
        {
            curve.SetPointPosition( index, InverseTransformPoint( point, space ) );
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

            for( int i = 1; i < PointCount; ++i )
            {
                int segments = 4;
                float step = 1.0f / (float)segments;
                float t = 0;

                CurvePoint point1 = GetPoint(i-1);
                CurvePoint point2 = GetPoint(i);
                 
                Vector3 pos1 = BezierCalculator.GetPoint( point1.position, point1.position + point1.Control2, point2.position + point2.Control1, point2.position, t );
                for( int s = 0; s < segments; ++s )
                {
                    t += step;
                    Vector3 pos2 = BezierCalculator.GetPoint( point1.position, point1.position + point1.Control2, point2.position + point2.Control1, point2.position, t );
                    Gizmos.DrawLine( pos1, pos2 );
                    pos1 = pos2;
                }
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