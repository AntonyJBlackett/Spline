using System.Collections.Generic;
using UnityEngine;

namespace FantasticSplines
{
    [System.Serializable]
    public class ExtrudeShape
    {
        public enum ShapeSizeType
        {
            Extents,
            MaxWidthHeight,
            Width,
            Height
        }

        public enum ShapeType
        {
            SplineShape,
            Line,
            LineSubdivided,
            Rectangle,
            RectangleSubdivided,
            GroundRectangle
        }

        public Vector2[] verts;
        public Vector2[] normals;
        public float[] u;
        public Color[] colors;
        public int[] lines;

        public ExtrudeShape()
        {
        }

        public const float LineLengthThreshold = 0.001f;

        public static ExtrudeShape Line
        {
            get
            {
                ExtrudeShape shape = new ExtrudeShape();
                shape.verts = new Vector2[] {
                Vector2.right, Vector2.left,
            };
                shape.normals = new Vector2[] {
                Vector2.up, Vector2.up,
            };
                shape.u = new float[] {
                0, 1.0f,
            };
                shape.colors = new Color[] {
                Color.white, Color.white,
            };
                shape.lines = new int[] {
                0,1,
            };

                return shape;
            }
        }

        public static ExtrudeShape SubdividedLine
        {
            get
            {
                ExtrudeShape shape = new ExtrudeShape();
                shape.verts = new Vector2[] {
                Vector2.right, Vector3.zero, Vector2.left,
            };
                shape.normals = new Vector2[] {
                Vector2.up, Vector2.up,Vector2.up,
            };
                shape.u = new float[] {
                0, 0.5f, 1.0f,
            };
                shape.colors = new Color[] {
                Color.white, Color.white, Color.white,
            };
                shape.lines = new int[] {
                0,1,
                1,2
            };

                return shape;
            }
        }

        public static ExtrudeShape Rectangle
        {
            get
            {
                ExtrudeShape shape = new ExtrudeShape();
                shape.verts = new Vector2[] {
                Vector2.right, Vector2.right + Vector2.up,
                Vector2.right + Vector2.up,Vector2.left + Vector2.up,
                Vector2.left + Vector2.up, Vector2.left,
                Vector2.left, Vector2.right};
                shape.normals = new Vector2[] {
                Vector2.right, Vector2.right,
                Vector2.up,Vector2.up,
                Vector2.left, Vector2.left,
                Vector2.down, Vector2.down };
                shape.u = new float[] {
                0, 0.2f,
                0.2f, 0.6f,
                0.6f, 0.8f,
                0.8f, 1 };
                shape.colors = new Color[] {
                Color.white, Color.white,
                Color.white, Color.white,
                Color.white, Color.white,
                Color.white, Color.white };
                shape.lines = new int[] {
                0,1,
                2,3,
                4,5,
                6,7,
                };
                return shape;
            }
        }

        public static ExtrudeShape SubdivideRectangle
        {
            get
            {
                ExtrudeShape shape = new ExtrudeShape();
                shape.verts = new Vector2[] {
                Vector2.right, Vector2.right + Vector2.up,
                Vector2.right + Vector2.up,Vector2.up,Vector2.left + Vector2.up,
                Vector2.left + Vector2.up, Vector2.left,
                Vector2.left, Vector2.right};
                shape.normals = new Vector2[] {
                Vector2.right, Vector2.right,
                Vector2.up, Vector2.up,Vector2.up,
                Vector2.left, Vector2.left,
                Vector2.down, Vector2.down };
                shape.u = new float[] {
                0, 0.2f,
                0.2f, 0.4f,0.6f,
                0.6f, 0.8f,
                0.8f, 1 };
                shape.colors = new Color[] {
                Color.white, Color.white,
                Color.white, Color.white,Color.white,
                Color.white, Color.white,
                Color.white, Color.white };
                shape.lines = new int[] {
                0,1,
                2,3,
                3,4,
                5,6,
                7,8};

                return shape;
            }
        }

        public static ExtrudeShape GroundRectangle
        {
            get
            {
                ExtrudeShape shape = new ExtrudeShape();
                shape.verts = new Vector2[] {
                Vector2.right, Vector2.right + Vector2.down,
                Vector2.right + Vector2.down, Vector2.left + Vector2.down,
                Vector2.left + Vector2.down, Vector2.left,
                Vector2.left, Vector2.right};
                shape.normals = new Vector2[] {
                Vector2.right, Vector2.right,
                Vector2.down,Vector2.down,
                Vector2.left, Vector2.left,
                Vector2.up, Vector2.up };
                shape.u = new float[] {
                0, 0.2f,
                0.2f, 0.6f,
                0.6f, 0.8f,
                0.8f, 1 };
                shape.colors = new Color[] {
                Color.white, Color.white,
                Color.white, Color.white,
                Color.white, Color.white,
                Color.white, Color.white };
                shape.lines = new int[] {
                1,0,
                3,2,
                5,4,
                7,6,
                };

                return shape;
            }
        }

        public static ExtrudeShape ConstructExtrudeShape( ShapeType type )
        {
            switch( type )
            {
                case ShapeType.Line:
                    return Line;
                case ShapeType.LineSubdivided:
                    return SubdividedLine;
                case ShapeType.Rectangle:
                    return Rectangle;
                case ShapeType.RectangleSubdivided:
                    return SubdivideRectangle;
                case ShapeType.GroundRectangle:
                    return GroundRectangle;
            }
            return Line;
        }

        public float GetSize( ShapeSizeType sizeType )
        {
            Vector2 min = verts[0];
            Vector2 max = verts[0];
            for( int i = 0; i < verts.Length; ++i )
            {
                min = Vector2.Min( min, verts[i] );
                max = Vector2.Max( max, verts[i] );
            }

            // reflect around the orgin
            min = Vector2.Min( min, -max );
            max = Vector2.Max( -min, max );

            Bounds bounds = new Bounds( (min + max) * 0.5f, max - min );

            switch( sizeType )
            {
                case ShapeSizeType.Extents:
                    return bounds.extents.magnitude;
                case ShapeSizeType.MaxWidthHeight:
                    return Mathf.Max( bounds.extents.x, bounds.extents.y );
                case ShapeSizeType.Width:
                    return bounds.extents.x;
                case ShapeSizeType.Height:
                    return bounds.extents.y;
            }

            return bounds.extents.magnitude;
        }

        void GenerateCapVerts()
        {
            capVerts.Clear();

            if( lines.Length < 2 )
            {
                return;
            }

            for( int i = 1; i < lines.Length; ++i )
            {
                Vector2 vert = verts[lines[i]];
                bool addVert = true;
                for( int v = 0; v < capVerts.Count; ++v )
                {
                    if( Vector2.Distance( capVerts[v], vert ) <= LineLengthThreshold )
                    {
                        // we already have a vert at this location.
                        addVert = false;
                        break;
                    }
                }
                if( addVert ) capVerts.Add( vert );
            }
        }

        void GenerateCapTriangles()
        {
            triangles.Clear();

            if( capVerts.Count <= 2 )
            {
                return;
            }

            Triangulator.Triangulate( capVerts, ref triangles );
            UnityEngine.Assertions.Assert.IsTrue( capVerts.Count - 2 == triangles.Count / 3 );
        }

        bool capGenerated = false;
        public void GenerateCap()
        {
            capGenerated = true;
            GenerateCapVerts();
            GenerateCapTriangles();
        }

        List<int> triangles = new List<int>();
        public List<int> CapTriangles
        {
            get
            {
                if( !capGenerated ) GenerateCap();
                return triangles;
            }
        }

        List<Vector2> capVerts = new List<Vector2>();
        public List<Vector2> CapVerts
        {
            get
            {
                if( !capGenerated ) GenerateCap();
                return capVerts;
            }
        }
    }
}