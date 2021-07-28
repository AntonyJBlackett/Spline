using System.Collections.Generic;
using FantasticSplines;
using UnityEngine;

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
        for( int i = 1; i < lines.Length; ++i )
        {
            Vector2 vert = verts[ lines[i] ];
            bool addVert = true;
            for( int v = 0; v < capVerts.Count; ++v )
            {
                if( Vector2.Distance( capVerts[v], vert ) <= 0.001f )
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

public enum CornerType
{
    Simple,
    Mitre,
    Square,
    Round
}


public struct ExtrudePoint
{
    public Vector3 position;
    public Vector3 pointTangent;
    public Vector3 inTangent;
    public Vector3 normal;
    public Vector3 scale;
    public Color color;
    public float distance; // for sorting and uvs
    public int priority; // higher = higher priority

    public ExtrudePoint( SplineResult result, int priority )
    {
        position = result.position;
        pointTangent = result.tangent;
        inTangent = result.tangent;
        normal = Vector3.up;
        scale = Vector3.one;
        color = Color.white;
        distance = result.distance;
        this.priority = priority;
    }

    public static ExtrudePoint Lerp( ExtrudePoint a, ExtrudePoint b, float t )
    {
        return new ExtrudePoint()
        {
            position = Vector3.Lerp( a.position, b.position, t ),
            pointTangent = Vector3.Slerp( a.pointTangent, b.pointTangent, t ),
            inTangent = Vector3.Slerp( a.inTangent, b.inTangent, t ),
            normal = Vector3.Slerp( a.normal, b.normal, t ),
            scale = Vector3.Lerp( a.scale, b.scale, t ),
            color = Color.Lerp( a.color, b.color, t ),
            distance = Mathf.Lerp( a.distance, b.distance, t ),
            priority = Mathf.Max( a.priority, b.priority ),
        };
    }
}

[ExecuteInEditMode]
public class TubeGenerator : MonoBehaviour
{
    [Header( "Setup" )]
    public SplineComponent spline;
    public SplineNormal splineNormal;
    public bool autoRegenerate = true;
    public bool regenerate = false;
    public int updateCount = 0;

    [Header( "Spline Sampling" )]
    public bool sampleSplineControlPoints = true;
    public bool sampleSplineNormalKeys = true;
    public bool enableTangentTollerance = true;
    public bool enableNormalTollerance = true;
    [Range( 0, 45.0f )]
    public float tangentTolleranceAngle = 5;
    [Range( 0, 45.0f )]
    public float normalTolleranceAngle = 5;
    [Range( 0.1f, 10 )]
    public float tolleranceSamplingStepDistance = 0.5f;

    [Header( "Geometry Settings" )]
    public float tubeRadius = 1;
    public float CornerRadius
    {
        get
        {
            return tubeRadius + cornerRadius;
        }
    }
    [Range(0,1)]
    public float shapeFill = 1;
    public CornerType cornerType = CornerType.Mitre;
    [Range( 2, 16 )]
    public int roundCornerSegments = 5;
    public float cornerRadius = 0;
    [Range( 1, 180 )]
    public float cornerAngle = 10;
    public bool backfaces = false;
    public bool enableTwistSubdivision = true;
    [Range( 0.1f, 45.0f )]
    public float twistSubdivisionAngle = 5;

    [Header( "Extrude Shape" )]
    public ExtrudeShape.ShapeSizeType shapeSizeType = ExtrudeShape.ShapeSizeType.Extents;
    public ExtrudeShape.ShapeType shapeType = ExtrudeShape.ShapeType.Line;

    [Header( "UVs" )]
    public bool seamlessUVs = true;

    [Header( "Output" )]
    public MeshFilter meshFilter;
    public MeshCollider meshCollider;

    Mesh mesh;
    ExtrudeShape extrudeShape;
    public SplineExtrudeShape splineShape;

    private void Awake()
    {
        regenerate = true;
    }
    void Initialise()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();

        if( mesh == null )
        {
            mesh = new Mesh();
            mesh.MarkDynamic();
            meshFilter.mesh = mesh;
        }

        if( meshFilter == null )
        {
            meshFilter.sharedMesh = mesh;
        }
        if( meshCollider == null )
        {
            meshFilter.sharedMesh = mesh;
        }

        if( splineShape != null )
        {
            extrudeShape = splineShape.GetExtrudeShape();
        }
        else
        {
            extrudeShape = ExtrudeShape.ConstructExtrudeShape( shapeType );
        }
    }

    void AutoRegenerate()
    {
        int update = spline.GetUpdateCount() + splineNormal.GetUpdateCount();
        if( regenerate ||
            (update != updateCount && autoRegenerate) )
        {
            Regenerate();
        }
    }

    void Regenerate()
    {
        updateCount = spline.GetUpdateCount() + splineNormal.GetUpdateCount();
        Initialise();
        GenerateMesh();
        regenerate = false;
    }

    public void OnValidate()
    {
        cornerRadius = Mathf.Clamp( cornerRadius, 0, cornerRadius );
        regenerate = true;
    }

    private void OnDrawGizmos()
    {
        AutoRegenerate();
    }

    bool ExceedsTangetAngleTollerance( ExtrudePoint first, ExtrudePoint second )
    {
        return Vector3.Angle( first.pointTangent, second.pointTangent ) > tangentTolleranceAngle;
    }

    bool ExceedsNormalAngleTollerance( ExtrudePoint first, ExtrudePoint second )
    {
        Vector3 n1 = GetNormalAtDistance( first.distance );
        Vector3 n2 = GetNormalAtDistance( second.distance );
        return Vector3.Angle( n1, n2 ) > normalTolleranceAngle;
    }

    int TwistAngleSubdivisions( ExtrudePoint first, ExtrudePoint second )
    {
        return Mathf.FloorToInt( Vector3.Angle( first.normal, second.normal ) / twistSubdivisionAngle );
    }

    void ValidateExtrudePoint( ExtrudePoint point )
    {
#if UNITY_EDITOR
        if( Mathf.Approximately( point.pointTangent.magnitude, 0 ) )
        {
            Debug.LogError( "Tangent is zero length." );
        }
#endif
    }

    bool CalculateCornerPoint( out Vector3 cornerCenter, Vector3 position, Vector3 inTangent, Vector3 outTangent )
    {
        // it's a corner
        Vector3 cornerAxis = Vector3.Cross( inTangent, outTangent ).normalized;
        Vector3 inRight = Vector3.Cross( cornerAxis, inTangent ).normalized;
        Vector3 outRight = Vector3.Cross( cornerAxis, outTangent ).normalized;
        Vector3 inEdgePoint = position + inRight * CornerRadius;
        Vector3 outEdgePoint = position + outRight * CornerRadius;
        return MathsUtils.LineLineIntersection( out cornerCenter, inEdgePoint, inTangent, outEdgePoint, outTangent );
    }

    List<ExtrudePoint> splinePoints = new List<ExtrudePoint>();
    List<ExtrudePoint> extrudePoints = new List<ExtrudePoint>();
    void GenerateMesh()
    {
        // Sample the spline
        splinePoints.Clear();
        if( sampleSplineControlPoints ) SplineProcessor.AddResultsAtNodes( ref splinePoints, spline, false );
        if( sampleSplineNormalKeys ) SplineProcessor.AddResultsAtKeys( ref splinePoints, splineNormal );

        if( enableTangentTollerance ) SplineProcessor.AddPointsByTollerance( ref splinePoints, spline, tolleranceSamplingStepDistance, ExceedsTangetAngleTollerance );
        if( enableNormalTollerance ) SplineProcessor.AddPointsByTollerance( ref splinePoints, spline, tolleranceSamplingStepDistance, ExceedsNormalAngleTollerance );

        SplineProcessor.RemovePointsAtSameLocation( ref splinePoints );

        // generation code
        extrudePoints.Clear();
        for( int p = 0; p < splinePoints.Count; ++p )
        {
            ExtrudePoint splinePoint = splinePoints[p];

            // calculate the tangent at the point so we can then
            // calculate a scalar to compensate for pinching corners on acute angled points
            Vector3 pointTangent = splinePoint.pointTangent.normalized;
            Vector3 inTangent = pointTangent;
            Vector3 outTangent = pointTangent;

            bool firstPoint = p == 0;
            bool lastPoint = p == splinePoints.Count - 1;

            if( (!firstPoint && !lastPoint) || spline.IsLoop() )
            {
                int previousIndex = p - 1;
                int nextIndex = p + 1;

                if( spline.IsLoop() && previousIndex < 0 )
                {
                    previousIndex = splinePoints.Count - 2; // -2 because the end point is actually the 'same point'
                }
                if( spline.IsLoop() && nextIndex >= splinePoints.Count )
                {
                    nextIndex = 1; // 1 because the first point is actually the 'same point'
                }

                ExtrudePoint previous = splinePoints[previousIndex];
                ExtrudePoint next = splinePoints[nextIndex];

                inTangent = (splinePoint.position - previous.position).normalized;
                outTangent = (next.position - splinePoint.position).normalized;
                pointTangent = (inTangent + outTangent) * 0.5f;
            }

            bool isCorner = Vector3.Angle( inTangent, outTangent ) > cornerAngle;
            if( isCorner && cornerType != CornerType.Simple )
            {
                AddCornerPoints( ref extrudePoints, splinePoint, pointTangent, inTangent, outTangent, firstPoint );
            }
            else
            {
                ExtrudePoint cornerPoint = ConstructExtrudePoint( splinePoint.position, GetNormalAtDistance( splinePoint.distance ), pointTangent, inTangent, splinePoint.distance, splinePoint.priority );
                ValidateExtrudePoint( cornerPoint );
                extrudePoints.Add( cornerPoint );
            }
        }

        // sometimes corners are too close to eachother and generate points inside the previous corner
        FixIntersectingSegments( ref extrudePoints );

        if( enableTwistSubdivision )
        {
            Subdivide( ref extrudePoints, TwistAngleSubdivisions );
        }

        GenerateMesh( extrudePoints, spline.IsLoop() );
    }

    void FixIntersectingSegments( ref List<ExtrudePoint> points )
    {
        if( points.Count < 2 )
        {
            return;
        }

        for( int i = 0; i < points.Count && points.Count > 1; ++i )
        {
            if( i <= 0 )
            {
                continue;
            }

            ExtrudePoint previous = points[i - 1];
            ExtrudePoint point = points[i];

            if( Vector3.Dot( point.inTangent, point.position - previous.position ) < 0 )
            {
                // intersection
                // we should probably remove points with some priority huristic for better results
                if( point.priority != CornerMidPointPriority && point.priority == previous.priority )
                {
                    points.RemoveAt( i );
                    points.RemoveAt( i - 1 );
                    i -= 2;
                }
                else if( point.priority > previous.priority )
                {
                    points.RemoveAt( i - 1 );
                    i--;
                }
                else
                {
                    points.RemoveAt( i );
                    i--;
                }
                i-=2; // we need to go back and reprocess ealier nodes now.
            }
        }
    }

    void Subdivide( ref List<ExtrudePoint> points, System.Func<ExtrudePoint, ExtrudePoint, int> subdivisionCountFunction )
    {
        int pointCount = points.Count;
        for( int i = 1; i < pointCount; ++i )
        {
            int subdivisions = subdivisionCountFunction( points[i-1], points[i] );
            for( int s = 1; s < subdivisions+1; s++ )
            {
                float t = Mathf.InverseLerp( 0, subdivisions + 1, s );
                points.Add( ExtrudePoint.Lerp( points[i - 1], points[i], t ) );
            }
        }
        points.Sort( (a,b)=> { return a.distance.CompareTo( b.distance ); } );
    }

    ExtrudePoint ConstructExtrudePoint( Vector3 point, Vector3 normal, Vector3 tangent, Vector3 inTangent, float distance, int priority )
    {
        return new ExtrudePoint()
        {
            position = point,
            pointTangent = tangent,
            inTangent = inTangent,
            normal = normal,
            scale = new Vector3( 1, 1, 1 ),
            color = Color.white,
            distance = distance,
            priority = priority,
        };
    }

    Vector3 GetNormalAtDistance( float distance )
    {
        if( splineNormal == null )
        {
            return Vector3.up;
        }

        return splineNormal.GetValueAtDistance( distance, Vector3.up );
    }

    public const int SplineNodePointPriority = 5;
    public const int CornerMidPointPriority = 4;
    public const int CornerEndPointPriority = 3;
    public const int CornerOtherPointPriority = 2;
    public const int SplineKeyframePointPriority = 1;
    public const int TollerancePointPriority = 0;

    void AddCornerPoints( ref List<ExtrudePoint> extrudePoints, ExtrudePoint splinePoint, Vector3 tangent, Vector3 inTangent, Vector3 outTangent, bool firstPoint )
    {
        CalculateCornerPoint( out Vector3 cornerCenter, splinePoint.position, inTangent, outTangent );
        Vector3 cornerAxis = Vector3.Cross( inTangent, outTangent ).normalized;

        Vector3 inPoint = MathsUtils.ProjectPointOnLine( splinePoint.position, inTangent, cornerCenter );
        Vector3 outPoint = MathsUtils.ProjectPointOnLine( splinePoint.position, outTangent, cornerCenter );
        float halfCornerLength = Vector3.Distance( inPoint, splinePoint.position );
        float inDistance = splinePoint.distance - halfCornerLength;
        float outDistance = splinePoint.distance + halfCornerLength;

        ExtrudePoint startCorner = ConstructExtrudePoint( inPoint, GetNormalAtDistance( inDistance ), inTangent, inTangent, inDistance, CornerEndPointPriority );
        ExtrudePoint endCorner = ConstructExtrudePoint( outPoint, GetNormalAtDistance( outDistance ), outTangent, outTangent, outDistance, CornerEndPointPriority );

        if( !firstPoint )
        {
            ValidateExtrudePoint( startCorner );
            extrudePoints.Add( startCorner );

            switch( cornerType )
            {
                case CornerType.Mitre:
                    Vector3 midNormal = Vector3.Slerp( startCorner.normal, endCorner.normal, 0.5f );
                    Vector3 midTangent = Vector3.Slerp( startCorner.pointTangent, endCorner.pointTangent, 0.5f );
                    ExtrudePoint midPoint = ConstructExtrudePoint( splinePoint.position, midNormal, midTangent, inTangent, splinePoint.distance, CornerMidPointPriority );
                    ValidateExtrudePoint( midPoint );
                    extrudePoints.Add( midPoint );
                    break;
                case CornerType.Square:
                    AddSquareCornerPoints( ref extrudePoints, cornerAxis, splinePoint, tangent, startCorner, endCorner, cornerCenter );
                    break;
                case CornerType.Round:
                    AddRoundCornerPoints( ref extrudePoints, cornerAxis, startCorner, endCorner, cornerCenter );
                    break;
            }
        }

        // out always consistent
        ValidateExtrudePoint( endCorner );
        extrudePoints.Add( endCorner );
    }

    void AddRoundCornerPoints( ref List<ExtrudePoint> extrudePoints, Vector3 cornerAxis, ExtrudePoint startCorner, ExtrudePoint endCorner, Vector3 cornerCenter )
    {
        float cornerAngle = Vector3.Angle( startCorner.pointTangent, endCorner.pointTangent );
        int segments = Mathf.Max( 2, roundCornerSegments );
        for( int i = 1; i < segments + 1; ++i )
        {
            float t = Mathf.InverseLerp( 0, segments+1, i );
            Matrix4x4 centerToWorld = Matrix4x4.TRS( cornerCenter, Quaternion.identity, Vector3.one );

            float slerpAngle = cornerAngle * t;
            Matrix4x4 pointStepRotation = Matrix4x4.TRS( Vector3.zero, Quaternion.AngleAxis( slerpAngle, cornerAxis ), Vector3.one );
            Matrix4x4 pointStepMatrix = centerToWorld * pointStepRotation * centerToWorld.inverse;
            float distance = Mathf.Lerp( startCorner.distance, endCorner.distance, t );
            Vector3 point = pointStepMatrix.MultiplyPoint( startCorner.position );

            Quaternion startRotation = Quaternion.LookRotation( startCorner.pointTangent, startCorner.normal );
            Quaternion endRotation = Quaternion.LookRotation( endCorner.pointTangent, endCorner.normal );
            Matrix4x4 directionRotation = Matrix4x4.TRS( Vector3.zero, Quaternion.Slerp( startRotation, endRotation, t ), Vector3.one );
            Vector3 tangent = directionRotation * Vector3.forward;
            Vector3 normal = directionRotation * Vector3.up;

            ExtrudePoint slicePoint = ConstructExtrudePoint( point, normal, tangent, tangent, distance, CornerOtherPointPriority );
            extrudePoints.Add( slicePoint );
        }
    }

    void AddSquareCornerPoints( ref List<ExtrudePoint> extrudePoints, Vector3 cornerAxis, ExtrudePoint splinePoint, Vector3 tangent, ExtrudePoint startCorner, ExtrudePoint endCorner, Vector3 cornerCenter )
    {
        Vector3 midPoint = cornerCenter + (splinePoint.position - cornerCenter).normalized * CornerRadius;
        MathsUtils.LineLineIntersection( out Vector3 inSquarePoint, midPoint, tangent, startCorner.position, startCorner.pointTangent );
        MathsUtils.LineLineIntersection( out Vector3 outSquarePoint, midPoint, tangent, endCorner.position, endCorner.pointTangent );

        Vector3 inPointFromCenter = inSquarePoint - cornerCenter;
        Vector3 outPointFromCenter = outSquarePoint - cornerCenter;

        float squareToEndDistance = Vector3.Distance( inSquarePoint, startCorner.position );
        float p1Distance = startCorner.distance + squareToEndDistance;
        float p3Distance = endCorner.distance - squareToEndDistance;

        Vector3 p1Tangent = Vector3.Cross( inPointFromCenter.normalized, cornerAxis );
        float tangentSign = Mathf.Sign( Vector3.Dot( p1Tangent, tangent ) );
        Vector3 p3Tangent = Vector3.Cross( outPointFromCenter.normalized, cornerAxis ) * tangentSign;
        p1Tangent *= tangentSign;

        ExtrudePoint p1 = ConstructExtrudePoint( inSquarePoint, Vector3.Slerp( startCorner.normal, endCorner.normal, 0.25f), p1Tangent, startCorner.inTangent, p1Distance, CornerOtherPointPriority );
        ExtrudePoint p2 = ConstructExtrudePoint( midPoint, Vector3.Slerp( startCorner.normal, endCorner.normal, 0.5f ), tangent, tangent, splinePoint.distance, CornerMidPointPriority );
        ExtrudePoint p3 = ConstructExtrudePoint( outSquarePoint, Vector3.Slerp( startCorner.normal, endCorner.normal, 0.75f ), p3Tangent, endCorner.inTangent, p3Distance, CornerOtherPointPriority );

        extrudePoints.Add( p1 );
        extrudePoints.Add( p2 );
        extrudePoints.Add( p3 );
    }

    float CalculateVScalar( float length )
    {
        float splineVScalar = 1; // used to best fit v's so they begin and end at the end of the spline
        if( seamlessUVs )
        {
            float vLengthsInSpline = length / (tubeRadius*2);
            float roundedVLength = Mathf.Round( vLengthsInSpline );
            float stretchedSplineLength = roundedVLength * (tubeRadius * 2);
            splineVScalar = stretchedSplineLength / length;
        }
        return splineVScalar;
    }

    public bool startCap = false;
    public bool endCap = false;

    void GenerateMesh( List<ExtrudePoint> tubePoints, bool loop ) 
    {
        if( tubePoints.Count < 2 )
        {
            return;
        }

        bool hasStartCap = startCap && !loop;
        bool hasEndCap = endCap && !loop;

        int capCount = 0;
        if( hasStartCap && !loop ) capCount++;
        if( hasEndCap && !loop ) capCount++;

        List<Vector2> capVerts = extrudeShape.CapVerts;
        int capVertCount = capVerts.Count * capCount;
        List<int> endCapTriangles = extrudeShape.CapTriangles;
        int endCapTriIndexCount = endCapTriangles.Count * capCount;

        // setup mesh generation data
        int vertsInShape = extrudeShape.verts.Length;
        int segments = tubePoints.Count - 1; 
        int edgeLoops = segments + 1;
        int vertCount = vertsInShape * edgeLoops + capVertCount;
        int tubeTriCount = extrudeShape.lines.Length * segments;
        if( backfaces )
        {
            tubeTriCount *= 2;
            endCapTriIndexCount *= 2;
        }
        int triIndexCount = (tubeTriCount * 3) + endCapTriIndexCount;

        float length = SplineProcessor.CalculateLength( tubePoints );
        float vScalar = CalculateVScalar( length );

        int[] triangleIndicies = new int[triIndexCount];
        Vector3[] verticies = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Color[] colors = new Color[vertCount];

        float extrudeSize = extrudeShape.GetSize( shapeSizeType );
        float inverseExtrudeShapeSize = tubeRadius / extrudeSize;
        inverseExtrudeShapeSize *= shapeFill;

        float extrudeDistance = 0;
        Vector3 previousPosiiton = tubePoints[0].position;
        for( int p = 0; p < tubePoints.Count; ++p )
        {
            int edgeLoopIndex = p * vertsInShape;
            ExtrudePoint point = tubePoints[p];
            Vector3 segmentDirection = point.pointTangent;

            extrudeDistance += Vector3.Distance( tubePoints[p].position, previousPosiiton );
            previousPosiiton = tubePoints[p].position;

            bool start = p == 0;
            bool end = p == tubePoints.Count - 1;

            if( loop || (!start && !end) )
            {
                int nextP = p + 1;
                int previousP = p - 1;

                if( nextP >= tubePoints.Count )
                {
                    nextP = 1; // first point is the same as the last point in a loop.
                }
                if( previousP < 0 )
                {
                    previousP = tubePoints.Count - 2; // first point is the same as the last point in a loop.
                }

                ExtrudePoint next = tubePoints[nextP];
                ExtrudePoint previous = tubePoints[previousP];
                segmentDirection = (next.position - point.position).normalized;
                float nextDistance = Vector3.Distance( next.position, point.position );
                float previousDistance = Vector3.Distance( previous.position, point.position );
                if( nextDistance < tubeRadius * 0.1f && previousDistance < tubeRadius * 0.1f )
                {
                    // stop crazy projected geometry
                    segmentDirection = point.pointTangent;
                }
                else if( nextDistance < previousDistance )
                {
                    segmentDirection = (point.position - previous.position).normalized;
                }
            }

            Matrix4x4 normalMatrix = Matrix4x4.TRS( Vector3.zero, Quaternion.LookRotation( point.pointTangent, point.normal ), inverseExtrudeShapeSize * Vector3.one );

            Matrix4x4 sectionSpaceMatrix = Matrix4x4.TRS( point.position, Quaternion.LookRotation( segmentDirection, point.normal ), inverseExtrudeShapeSize * Vector3.one );
            Vector3[] projectToPoints = new Vector3[vertsInShape];
            for( int sv = 0; sv < vertsInShape; ++sv )
            {
                projectToPoints[sv] = sectionSpaceMatrix.MultiplyPoint( extrudeShape.verts[sv] );
            }

            for( int sv = 0; sv < vertsInShape; ++sv )
            {
                int index = edgeLoopIndex + sv;
                verticies[index] = MathsUtils.LinePlaneIntersection( projectToPoints[sv], segmentDirection, point.position, point.pointTangent );
                normals[index] = normalMatrix.MultiplyVector( extrudeShape.normals[sv] );
                colors[index] = Color.white * extrudeShape.colors[sv];
                uvs[index] = new Vector2( extrudeShape.u[sv], extrudeDistance * vScalar );
            }
        }

        // setup triangles
        int ti = 0;
        for( int s = 0; s < segments; ++s )
        {
            int offset = s * vertsInShape;
            for( int l = 0; l < extrudeShape.lines.Length; l+=2 )
            {
                int a = offset + extrudeShape.lines[l] + vertsInShape;
                int b = offset + extrudeShape.lines[l];
                int c = offset + extrudeShape.lines[l+1];
                int d = offset + extrudeShape.lines[l + 1] + vertsInShape;

                triangleIndicies[ti] = a;
                ti++;
                triangleIndicies[ti] = b;
                ti++;
                triangleIndicies[ti] = c;
                ti++;

                triangleIndicies[ti] = c;
                ti++;
                triangleIndicies[ti] = d;
                ti++;
                triangleIndicies[ti] = a;
                ti++;

                if( backfaces )
                {
                    triangleIndicies[ti] = b;
                    ti++;
                    triangleIndicies[ti] = a;
                    ti++;
                    triangleIndicies[ti] = c;
                    ti++;

                    triangleIndicies[ti] = d;
                    ti++;
                    triangleIndicies[ti] = c;
                    ti++;
                    triangleIndicies[ti] = a;
                    ti++;
                }
            }
        }

        if( hasStartCap )
        {
            int capStartIndex = vertCount - capVertCount;

            // add cap verts
            Matrix4x4 sectionSpaceMatrix = Matrix4x4.TRS( tubePoints[0].position, Quaternion.LookRotation( tubePoints[0].pointTangent, tubePoints[0].normal ), inverseExtrudeShapeSize * Vector3.one );
            Vector3[] projectToPoints = new Vector3[capVerts.Count];
            for( int cv = 0; cv < capVerts.Count; ++cv )
            {
                projectToPoints[cv] = sectionSpaceMatrix.MultiplyPoint( capVerts[cv] );
            }

            for( int cv = 0; cv < capVerts.Count; ++cv )
            {
                int index = capStartIndex + cv;

                verticies[index] = MathsUtils.LinePlaneIntersection( projectToPoints[cv], tubePoints[0].pointTangent, tubePoints[0].position, tubePoints[0].pointTangent );
                normals[index] = -tubePoints[0].pointTangent;
                colors[index] = colors[0]; // * extrudeShape.CapColors[i];
                uvs[index] = uvs[0];// new Vector2( extrudeShape.capUs[i], uvs[0].y );
            }

            // add cap tris
            for( int i = 0; i < endCapTriangles.Count; ++i )
            {
                triangleIndicies[ti] = capStartIndex + endCapTriangles[i];
                ti++;
            }
            if( backfaces )
            {
                // add cap tris
                for( int i = endCapTriangles.Count-1; i >= 0; --i )
                {
                    triangleIndicies[ti] = capStartIndex + endCapTriangles[i];
                    ti++;
                }
            }
        }

        if( hasEndCap )
        {
            int endCapIndex = vertCount - capVertCount;
            if( startCap ) endCapIndex += capVerts.Count;

            int p = tubePoints.Count - 1;

            // add cap verts
            Matrix4x4 sectionSpaceMatrix = Matrix4x4.TRS( tubePoints[p].position, Quaternion.LookRotation( tubePoints[p].pointTangent, tubePoints[p].normal ), inverseExtrudeShapeSize * Vector3.one );
            Vector3[] projectToPoints = new Vector3[capVerts.Count];
            for( int cv = 0; cv < capVerts.Count; ++cv )
            {
                projectToPoints[cv] = sectionSpaceMatrix.MultiplyPoint( capVerts[cv] );
            }

            for( int cv = 0; cv < capVerts.Count; ++cv )
            {
                int index = endCapIndex + cv;

                verticies[index] = MathsUtils.LinePlaneIntersection( projectToPoints[cv], tubePoints[p].pointTangent, tubePoints[p].position, tubePoints[p].pointTangent );
                normals[index] = tubePoints[p].pointTangent;


                int edgeLoopIndex = p * vertsInShape;
                colors[index] = colors[edgeLoopIndex]; // * extrudeShape.CapColors[i];
                uvs[index] = uvs[edgeLoopIndex];// new Vector2( extrudeShape.capUs[i], uvs[0].y );
            }

            // add cap tris
            // end cap is reversed
            for( int i = endCapTriangles.Count - 1; i >= 0; --i )
            {
                triangleIndicies[ti] = endCapIndex + endCapTriangles[i];
                ti++;
            }
            if( backfaces )
            {
                for( int i = 0; i < endCapTriangles.Count; ++i )
                {
                    triangleIndicies[ti] = endCapIndex + endCapTriangles[i];
                    ti++;
                }
            }
        }

        // transform from world space to local space of the meshFilter
        Matrix4x4 meshWorldToLocal = meshFilter.transform.worldToLocalMatrix;
        for( int v = 0; v < verticies.Length; ++v )
        {
            verticies[v] = meshWorldToLocal.MultiplyPoint3x4( verticies[v] );
            normals[v] = meshWorldToLocal.MultiplyVector( normals[v] );
        }

        // set the mesh
        mesh.Clear();
        mesh.vertices = verticies;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangleIndicies;
        mesh.RecalculateBounds();
    }
}
