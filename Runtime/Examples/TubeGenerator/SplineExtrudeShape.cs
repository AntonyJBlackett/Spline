using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace FantasticSplines
{
    [RequireComponent( typeof( SplineComponent ))]
    public class SplineExtrudeShape : MonoBehaviour
    {
        public float tangentTolleranceAngle = 5;
        public float tolleranceStep = 0.2f;
        public bool reverse = false;
        public List<ExtrudePoint> samples = new List<ExtrudePoint>();

        bool ExceedsTangetAngleTollerance( ExtrudePoint first, ExtrudePoint second )
        {
            return Vector3.Angle( first.pointTangent, second.pointTangent ) > tangentTolleranceAngle;
        }

        SplineComponent Spline
        {
            get
            {
                return GetComponent<SplineComponent>();
            }
        }

        public int UpdateCount { get; set; }
        public void OnValidate()
        {
            UpdateCount++;
        }

        public int GetUpdateCount()
        {
            return Spline.UpdateCount + UpdateCount;
        }

        public ExtrudeShape GetExtrudeShape()
        {
            SplineComponent spline = Spline;

            samples.Clear();

            SplineProcessor.AddResultsAtNodes( ref samples, spline );
            SplineProcessor.AddPointsByTollerance( ref samples, spline, new SplineDistance( tolleranceStep ), ExceedsTangetAngleTollerance );

            if( reverse ) samples.Reverse();

            return GenerateExtrudeShape( samples );
        }

        public struct ShapePoint
        {
            public Vector2 position;
            public Vector2 normal;
            public Color color;
            public float u;

            public static ShapePoint Lerp( ShapePoint a, ShapePoint b, float t )
            {
                return new ShapePoint()
                {
                    position = Vector2.Lerp( a.position, b.position, t ),
                    normal = Vector2.Lerp( a.normal, b.normal, t ).normalized,
                    color = Color.Lerp( a.color, b.color, t ),
                    u = Mathf.Lerp( a.u, b.u, t )
                };
            }
        }

        List<ShapePoint> points = new List<ShapePoint>();
        ExtrudeShape GenerateExtrudeShape( List<ExtrudePoint> samples )
        {
            float length = SplineProcessor.CalculateLength( samples );
            float inverseLength = 1 / length;

            Vector2 previousPosition = transform.worldToLocalMatrix.MultiplyPoint3x4( samples[0].position );
            float distance = 0;

            points.Clear();
            for( int i = 0; i < samples.Count; ++i )
            {
                Vector3 localPositon = transform.worldToLocalMatrix.MultiplyPoint3x4( samples[i].position );
                Vector3 localTangent = transform.worldToLocalMatrix.MultiplyVector( samples[i].pointTangent );

                if( reverse ) localTangent = -localTangent;

                Vector2 position = ConvertToVector2( localPositon );
                Vector2 normal = GenerateNormal( ConvertToVector2( localTangent ) );

                distance += Vector3.Distance( previousPosition, position );
                previousPosition = position;

                points.Add( new ShapePoint() { position = position, normal = normal, u = distance * inverseLength, color = Color.white } );
            }

            return GenerateExtrudeShape( points );
        }

        static List<int> lines = new List<int>();
        public static ExtrudeShape GenerateExtrudeShape( List<ShapePoint> points )
        {
            if( points.Count < 2 )
            {
                return ExtrudeShape.Line;
            }

            lines.Clear();
            Vector2[] verts = new Vector2[points.Count];
            Vector2[] normals = new Vector2[points.Count];
            Color[] colors = new Color[points.Count];
            float[] u = new float[points.Count];

            for( int i = 0; i < points.Count; ++i )
            {
                verts[i] = points[i].position;
                normals[i] = points[i].normal;
                u[i] = points[i].u;
                colors[i] = Color.white;

                if( i < points.Count - 1 )
                {
                    Vector3 next = points[i + 1].position;
                    Vector3 current = points[i].position;
                    if( Vector3.Distance( next, current ) > ExtrudeShape.LineLengthThreshold )
                    {
                        lines.Add( i );
                        lines.Add( i + 1 );
                    }
                }
            }

            return new ExtrudeShape()
            {
                verts = verts,
                normals = normals,
                colors = colors,
                u = u,
                lines = lines.ToArray()
            };
        }

        public static Vector2 ConvertToVector2( Vector3 vector3 )
        {
            return new Vector2( vector3.x, vector3.z );
        }

        public static Vector2 GenerateNormal( Vector2 tangent )
        {
            return -Vector2.Perpendicular( tangent.normalized );
        }
    }
}