using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace FantasticSplines
{
    [System.Serializable]
    public struct SplinePoint
    {
        public Vector3 position;
    }

    [System.Serializable]
    public class Spline
    {
        public List<SplinePoint> points = new List<SplinePoint>();

        public int PointCount { get { return points.Count; } }

        public void AddPoint(Vector3 position)
        {
            points.Add( new SplinePoint { position = position } );
        }

        public void RemovePoint(int index)
        {
            if( index < 0 || index > PointCount )
            {
                return;
            }
            points.RemoveAt( index );
        }

        public void InsertPoint(int index, Vector3 position)
        {
            if( index < 0 || index > PointCount )
            {
                return;
            }
            points.Insert( index, new SplinePoint { position = position } );
        }

        public Vector3 GetPointPosition( int index)
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
            SplinePoint point = points[index];
            point.position = position;
            points[index] = point;
        }
    }

    public class SplineComponent : MonoBehaviour
    {
        public Spline spline;

        public List<Vector3> GetPoints()
        {
            List<Vector3> points = new List<Vector3>();
            for( int i = 0; i < PointCount; ++i )
            {
                points.Add( GetPoint( i ) );
            }
            return points;
        }

        Vector3 TransformPoint( Vector3 point, Space space )
        {
            if( space == Space.World )
            {
                point = transform.InverseTransformPoint( point );
            }
            return point;
        }

        public bool IsIndexInRange( int index )
        {
            return index >= 0 && index < PointCount;
        }

        public void InsertPoint( int index, Vector3 point, Space space )
        {
            spline.InsertPoint( index, TransformPoint( point, space ) );
        }

        public void AddPoint(Vector3 point, Space space)
        {
            spline.AddPoint( TransformPoint( point, space ) );
        }

        public void RemovePoint(int index)
        {
            spline.RemovePoint( index );
        }

        public Vector3 GetPoint(int index)
        {
            return transform.TransformPoint( spline.GetPointPosition( index ) );
        }

        public void SetPoint( int index, Vector3 point, Space space )
        {
            spline.SetPointPosition( index, TransformPoint( point, space ) );
        }

        public int PointCount { get { return spline.PointCount; } }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            for( int i = 0; i < PointCount; ++i )
            {
                Gizmos.DrawSphere( GetPoint( i ), 0.05f );
            }

            for( int i = 0; i < PointCount - 1; ++i )
            {
                Gizmos.DrawLine( GetPoint( i ), GetPoint( i + 1 ) );
            }
        }
    }
}