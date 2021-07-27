using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace FantasticSplines
{
    public class SplineExtrudeShape : MonoBehaviour
    {
        public bool softEdges = false;

        List<ExtrudePoint> samples = new List<ExtrudePoint>();
        public ExtrudeShape GetExtrudeShape()
        {
            SplineComponent spline = GetComponent<SplineComponent>();

            samples.Clear();

            SplineProcessor.AddResultsAtNodes( ref samples, spline, softEdges ); // returns two samples for each nodes, in and out tangents

            return GenerateExtrudeShape( samples, spline.IsLoop() );
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
        ExtrudeShape GenerateExtrudeShape( List<ExtrudePoint> samples, bool loop )
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

                Vector2 position = ConvertToVector2( localPositon );
                Vector2 normal = GenerateNormal( ConvertToVector2( localTangent ) );

                distance += Vector3.Distance( previousPosition, position );
                previousPosition = position;

                points.Add( new ShapePoint() { position = position, normal = normal, u = distance * inverseLength, color = Color.white } );
            }

            /*if( softEdges )
            {
                SoftenEdges( ref points, loop );
            }*/

            return GenerateExtrudeShape( points, softEdges, loop );
        }

        void SoftenEdges( ref List<ShapePoint> points, bool loop )
        {
            for( int i = 0; i < points.Count-1; i+=2 )
            {
                int inPointIndex = i - 1;
                if( inPointIndex < 0 )
                {
                    if( loop )
                    {
                        inPointIndex = points.Count - 1;
                    }
                    else
                    {
                        continue;
                    }
                }
                int outPointIndex = i;

                ShapePoint softPoint = ShapePoint.Lerp( points[inPointIndex], points[outPointIndex], 0.5f );
                points[inPointIndex] = softPoint;
                points[outPointIndex] = softPoint;
            }
        }

        public static ExtrudeShape GenerateExtrudeShape( List<ShapePoint> points, bool softEdges, bool loop )
        {
            if( points.Count < 2 )
            {
                return ExtrudeShape.Line;
            }

            Vector2[] verts = new Vector2[points.Count];
            Vector2[] normals = new Vector2[points.Count];
            Color[] colors = new Color[points.Count];
            float[] u = new float[points.Count];
            int segmentCount = softEdges ? points.Count - 1 : (points.Count / 2);
            if( loop ) segmentCount += 1;

            int[] lines = new int[segmentCount*2];

            for( int i = 0; i < points.Count; ++i )
            {
                verts[i] = points[i].position;
                normals[i] = points[i].normal;
                u[i] = points[i].u;
                colors[i] = Color.white;
            }

            int vertIndex = 0;
            for( int s = 0; s < segmentCount; s++ )
            {
                int l = s * 2;
                lines[l] = vertIndex;
                ++vertIndex;
                vertIndex %= points.Count;
                lines[l + 1] = vertIndex;
                if( !softEdges )
                {
                    ++vertIndex;
                    vertIndex %= points.Count;
                }
            }

            return new ExtrudeShape()
            {
                verts = verts,
                normals = normals,
                colors = colors,
                u = u,
                lines = lines
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