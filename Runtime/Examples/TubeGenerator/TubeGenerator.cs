using System.Collections.Generic;
using UnityEngine;


namespace FantasticSplines
{
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
        public Vector2 scale; // scales the shape along x,y
        public Color color;
        public float distance; // for sorting and uvs
        public float priority; // higher = higher priority
        public float segment; // for sorting.

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
            segment = result.segmentResult.index;
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
                priority = Mathf.Lerp( a.priority, b.priority, t ),
                segment = Mathf.Lerp( a.segment, b.segment, t )
            };
        }
    }

    [ExecuteInEditMode]
    public class TubeGenerator : MonoBehaviour
    {
        [InspectorButton( "Regenerate" )]
        public bool regenerate;
        public bool autoRegenerate = true;
        [HideInInspector]
        [SerializeField]
        int lastUpdateCount = 0;

        [Header( "Inputs" )]
        public SplineComponent spline;
        public SplineNormal splineNormal;
        public KeyframedSplineParameter<Vector2> splineScale;
        public KeyframedSplineParameter<Color> splineColor;

        [Header( "Spline Sampling" )]
        public bool sampleSplineControlPoints = true;
        public bool sampleSplineNormalKeys = true;
        public bool sampleSplineScaleKeys = true;
        public bool sampleSplineColorKeys = true;
        public bool enableTangentTollerance = true;
        public bool enableNormalTollerance = true;
        public bool enableScaleTollerance = true;
        public bool enableColorTollerance = true;
        [Range( 0, 45.0f )]
        public float tangentTolleranceAngle = 5;
        [Range( 0, 45.0f )]
        public float normalTolleranceAngle = 5;
        [Range( 0.01f, 1f )]
        public float scaleTollerance = 0.1f;
        [Range( 0.01f, 1f )]
        public float colorTollerance = 0.1f;
        [Range( 0.1f, 10 )]
        public float tolleranceSamplingStepDistance = 0.5f;

        [Header( "Extrude Shape" )]
        public ExtrudeShape.ShapeSizeType shapeSizeType = ExtrudeShape.ShapeSizeType.Extents;
        public ExtrudeShape.ShapeType shapeType = ExtrudeShape.ShapeType.Line;
        public SplineExtrudeShape splineShape;
        [Range( 0, 1 )]
        public float shapeFill = 1;

        [Header( "Tube Geometry Settings" )]
        public float tubeRadius = 1;
        public float cornerRadius = 0;
        public CornerType cornerType = CornerType.Mitre;
        [Range( 2, 16 )]
        public int roundCornerSegments = 5;
        [Range( 1, 180 )]
        public float cornerAngleThreshold = 10;

        [Header( "Detail Subdivisions" )]
        public bool enableTwistSubdivision = true;
        [Range( 0.1f, 45.0f )]
        public float twistSubdivisionAngle = 5;

        public enum VSpace
        {
            SplineDistance,
            SplineT
        }
        [Header( "UVs" )]
        public VSpace vSpace = VSpace.SplineDistance;
        public float vTiling = 1;
        public float vOffset = 0;
        public bool seamlessVs = true;
        public bool swapUVs = false;
        public bool mirrorU = false;
        public bool mirrorV = false;

        [Header( "Faces" )]
        public bool backfaces = false;
        public bool startCap = false;
        public bool endCap = false;

        [Header( "Outputs" )]
        public MeshFilter meshFilter;
        public MeshCollider meshCollider;

        Mesh mesh;
        ExtrudeShape extrudeShape;

        public float CornerRadius
        {
            get
            {
                return tubeRadius + cornerRadius;
            }
        }

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

            if( splineShape != null && shapeType == ExtrudeShape.ShapeType.SplineShape )
            {
                extrudeShape = splineShape.GetExtrudeShape();
            }
            else
            {
                extrudeShape = ExtrudeShape.ConstructExtrudeShape( shapeType );
            }
        }

        [SerializeField]
        [HideInInspector]
        int selfUpdateCount = 0;
        public int UpdateCount
        {
            get
            {
                int update = spline.GetUpdateCount();

                if( splineNormal != null ) update += splineNormal.GetUpdateCount();
                if( splineScale != null ) update += splineScale.GetUpdateCount();
                if( splineShape != null ) update += splineShape.GetUpdateCount();
                if( splineColor != null ) update += splineColor.GetUpdateCount();
                return update + selfUpdateCount;
            }
        }

        void AutoRegenerate()
        {
            if( regenerate ||
                (UpdateCount != lastUpdateCount && autoRegenerate) )
            {
                Regenerate();
            }
        }

        void Regenerate()
        {
            lastUpdateCount = UpdateCount;
            Initialise();
            GenerateMesh();
            regenerate = false;

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        public void OnValidate()
        {
            selfUpdateCount++;
            cornerRadius = Mathf.Clamp( cornerRadius, 0, cornerRadius );
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

        bool ExceedsVector2Tollerance( ExtrudePoint first, ExtrudePoint second )
        {
            Vector2 a = GetScaleAtDistance( first.distance );
            Vector2 b = GetScaleAtDistance( second.distance );
            return Vector2.Distance( a, b ) > scaleTollerance;
        }

        bool ExceedsColorTollerance( ExtrudePoint first, ExtrudePoint second )
        {
            Color a = GetColorAtDstance( first.distance );
            Color b = GetColorAtDstance( second.distance );
            float difference = Mathf.Abs( a.r - b.r ) + Mathf.Abs( a.g - b.g ) + Mathf.Abs( a.b - b.b ) + Mathf.Abs( a.a - b.a );
            return difference > colorTollerance;
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
            if( spline == null )
            {
                return;
            }

            // Sample the spline
                splinePoints.Clear();
            if( sampleSplineControlPoints ) SplineProcessor.AddResultsAtNodes( ref splinePoints, spline );
            if( sampleSplineNormalKeys && splineNormal ) SplineProcessor.AddResultsAtKeys( ref splinePoints, splineNormal );
            if( sampleSplineScaleKeys && splineScale ) SplineProcessor.AddResultsAtKeys( ref splinePoints, splineScale );
            if( sampleSplineColorKeys && splineColor ) SplineProcessor.AddResultsAtKeys( ref splinePoints, splineColor );

            if( enableTangentTollerance ) SplineProcessor.AddPointsByTollerance( ref splinePoints, spline, tolleranceSamplingStepDistance, ExceedsTangetAngleTollerance );
            if( enableNormalTollerance ) SplineProcessor.AddPointsByTollerance( ref splinePoints, spline, tolleranceSamplingStepDistance, ExceedsNormalAngleTollerance );
            if( enableScaleTollerance ) SplineProcessor.AddPointsByTollerance( ref splinePoints, spline, tolleranceSamplingStepDistance, ExceedsVector2Tollerance );
            if( enableColorTollerance ) SplineProcessor.AddPointsByTollerance( ref splinePoints, spline, tolleranceSamplingStepDistance, ExceedsColorTollerance );

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

                    Vector3 inVector = splinePoint.position - previous.position;
                    Vector3 outVector = next.position - splinePoint.position;

                    inTangent = inVector.normalized;
                    outTangent = outVector.normalized;

                    /* experimental code, trying to find a better corner start and end position for concave entry spline arcs.
                    float angle = Vector3.Angle( inVector, outVector );
                    float halfArcDistance = Mathf.PI * CornerRadius * (angle/360);
                    float halfCordLegnth = CornerRadius * Mathf.Sin( angle * Mathf.Deg2Rad * 0.5f );
                    int seekIterations = 10;
                    float seekThreshold = 0.05f;
                    float seekDistance = halfArcDistance;
                    float bestDistance = float.MaxValue;
                    for( int i = 0; i < seekIterations; ++i )
                    {

                        SplineResult inResult = spline.GetResultAtDistance( splinePoint.distance - seekDistance );
                        SplineResult outResult = spline.GetResultAtDistance( splinePoint.distance + seekDistance );

                        float distance = (inResult.position - outResult.position).magnitude;
                        float distanceDifference = (halfCordLegnth*2) - distance;

                        if( Mathf.Abs( distanceDifference ) < bestDistance )
                        {
                            bestDistance = Mathf.Abs( distanceDifference );
                            inTangent = inResult.tangent.normalized;
                            outTangent = outResult.tangent.normalized;
                        }

                        if( Mathf.Abs( distanceDifference ) < seekThreshold )
                        {
                            break;
                        }

                        seekDistance += distanceDifference*0.9f;
                    }*/

                    pointTangent = (inTangent + outTangent) * 0.5f;
                }

                bool isCorner = Vector3.Angle( inTangent, outTangent ) > cornerAngleThreshold;
                if( isCorner && cornerType != CornerType.Simple )
                {
                    AddCornerPoints( ref extrudePoints, splinePoint, pointTangent, inTangent, outTangent, firstPoint );
                }
                else
                {
                    ExtrudePoint cornerPoint = ConstructExtrudePoint( splinePoint.position, pointTangent, inTangent, splinePoint.distance, splinePoint.priority );
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
                    i -= 2; // we need to go back and reprocess ealier nodes now.
                }
            }
        }

        void Subdivide( ref List<ExtrudePoint> points, System.Func<ExtrudePoint, ExtrudePoint, int> subdivisionCountFunction )
        {
            int pointCount = points.Count;
            for( int i = 1; i < pointCount; ++i )
            {
                int subdivisions = subdivisionCountFunction( points[i - 1], points[i] );
                for( int s = 1; s < subdivisions + 1; s++ )
                {
                    float t = Mathf.InverseLerp( 0, subdivisions + 1, s );
                    points.Add( ExtrudePoint.Lerp( points[i - 1], points[i], t ) );
                }
            }
            points.Sort( ( a, b ) => { return a.distance.CompareTo( b.distance ); } );
        }

        ExtrudePoint ConstructExtrudePoint( Vector3 point, Vector3 pointTangent, Vector3 inTangent, float distance, float priority )
        {
            return ConstructExtrudePoint( point, GetNormalAtDistance( distance ), GetScaleAtDistance( distance ), GetColorAtDstance( distance ), pointTangent, inTangent, distance, priority );
        }

        ExtrudePoint ConstructExtrudePoint( Vector3 point, Vector3 normal, Vector2 scale, Color color, Vector3 tangent, Vector3 inTangent, float distance, float priority )
        {
            return new ExtrudePoint()
            {
                position = point,
                pointTangent = tangent,
                inTangent = inTangent,
                normal = normal,
                scale = scale,
                color = color,
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

            return splineNormal.GetNormalAtDistance( distance );
        }

        Vector2 GetScaleAtDistance( float distance )
        {
            if( splineScale == null )
            {
                return Vector2.one;
            }

            return splineScale.GetValueAtDistance( distance, Vector2.one );
        }

        Color GetColorAtDstance( float distance )
        {
            if( splineColor == null )
            {
                return Color.white;
            }

            return splineColor.GetValueAtDistance( distance, Color.white );
        }

        public const int SplineNodePointPriority = 5;
        public const int CornerMidPointPriority = 4;
        public const int CornerEndPointPriority = 3;
        public const int CornerOtherPointPriority = 2;
        public const int SplineKeyframePointPriority = 1;
        public const int TollerancePointPriority = 0;

        void AddCornerPoints( ref List<ExtrudePoint> extrudePoints, ExtrudePoint splinePoint, Vector3 tangent, Vector3 inTangent, Vector3 outTangent, bool firstPoint )
        {
            // default values
            CalculateCornerPoint( out Vector3 cornerCenter, splinePoint.position, inTangent, outTangent );
            Vector3 cornerAxis = Vector3.Cross( inTangent, outTangent ).normalized;
            Vector3 inPoint = MathsUtils.ProjectPointOnLine( splinePoint.position, inTangent, cornerCenter );
            Vector3 outPoint = MathsUtils.ProjectPointOnLine( splinePoint.position, outTangent, cornerCenter );
            float halfCornerLength = Vector3.Distance( inPoint, splinePoint.position );
            float inDistance = splinePoint.distance - halfCornerLength;
            float outDistance = splinePoint.distance + halfCornerLength;

            ExtrudePoint startCorner = ConstructExtrudePoint( inPoint, inTangent, inTangent, inDistance, CornerEndPointPriority );
            ExtrudePoint endCorner = ConstructExtrudePoint( outPoint, outTangent, outTangent, outDistance, CornerEndPointPriority );

            //experimental code, trying to find a better corner start and end position for concave entry spline arcs.
            /*float angle = Vector3.Angle( inTangent, outTangent );
            float halfArcDistance = Mathf.PI * CornerRadius * (angle / 360);
            float halfCordLegnth = CornerRadius * Mathf.Sin( angle * Mathf.Deg2Rad * 0.5f );
            int seekIterations = 10;
            float seekThreshold = 0.01f;
            float seekDistance = halfArcDistance;
            float bestDistance = float.MaxValue;
            for( int i = 0; i < seekIterations; ++i )
            {

                SplineResult inResult = spline.GetResultAtDistance( splinePoint.distance - seekDistance );
                SplineResult outResult = spline.GetResultAtDistance( splinePoint.distance + seekDistance );

                float distance = (inResult.position - outResult.position).magnitude;
                float distanceDifference = (halfCordLegnth * 2) - distance;

                if( Mathf.Abs( distanceDifference ) < bestDistance )
                {
                    bestDistance = Mathf.Abs( distanceDifference );
                    //inTangent = inResult.tangent.normalized;
                    //outTangent = outResult.tangent.normalized;

                    inPoint = inResult.position;
                    outPoint = outResult.position;

                    Vector3 cord = (outPoint - inPoint);
                    cornerCenter = (inPoint + outPoint) * 0.5f;
                    cornerAxis = Vector3.Cross( (splinePoint.position - inResult.position).normalized, cord.normalized );

                    inDistance = inResult.distance;
                    outDistance = outResult.distance;
                }

                if( Mathf.Abs( distanceDifference ) < seekThreshold )
                {
                    break;
                }

                seekDistance += distanceDifference * 0.97f;
            }*/


            /* SplineResult inResult;
             SplineResult outResult;

             //experimental code, trying to find a better corner start and end position for concave entry spline arcs.
             float CornetFitThreshold = 0.05f; // this is a percenage of actual corner cord length compared to the correct corner arc length
             float angle = Vector3.Angle( inTangent, outTangent );
             float halfCordLegnth = CornerRadius * Mathf.Sin( angle * Mathf.Deg2Rad * 0.5f );
             float otherAngle = (180 - angle) * 0.5f;
             float seekDistance = halfCordLegnth * Mathf.Sin( angle ) / Mathf.Sin( otherAngle );
             int maxIterations = 10;
             int i = 0;
             do
             {
                 inResult = spline.GetResultAtDistance( splinePoint.distance - seekDistance );
                 outResult = spline.GetResultAtDistance( splinePoint.distance + seekDistance );

                 float distance = (inResult.position - outResult.position).magnitude;
                 angle = Vector3.Angle( inResult.tangent, outResult.tangent );
                 halfCordLegnth = CornerRadius * Mathf.Sin( angle * Mathf.Deg2Rad * 0.5f );

                 float test = distance / (halfCordLegnth * 2);
                 if( test > 0 && test < CornetFitThreshold )
                 {
                     // good enough, let's build the corner!
                     break;
                 }

                 // let's keep seeking.
                 seekDistance += (halfCordLegnth * 2) - distance;
                 ++i;
             } while( i < maxIterations );

             ExtrudePoint startCorner = ConstructExtrudePoint( inResult.position, GetNormalAtDistance( inResult.distance ), inResult.tangent.normalized, inResult.tangent.normalized, inResult.distance, CornerEndPointPriority );
             ExtrudePoint endCorner = ConstructExtrudePoint( outResult.position, GetNormalAtDistance( outResult.distance ), outResult.tangent.normalized, outResult.tangent.normalized, outResult.distance, CornerEndPointPriority );
             Vector3 cornerAxis = Vector3.Cross( inResult.tangent.normalized, outResult.tangent.normalized ).normalized;
             Vector3 cornerCenter1 = inResult.position + Vector3.Cross( cornerAxis, inResult.tangent.normalized ).normalized * CornerRadius;
             Vector3 cornerCenter2 = outResult.position + Vector3.Cross( cornerAxis, outResult.tangent.normalized ).normalized * CornerRadius;
             Vector3 cornerCenter = (cornerCenter1 + cornerCenter2) * 0.5f;*/

            if( !firstPoint )
            {
                ValidateExtrudePoint( startCorner );
                extrudePoints.Add( startCorner );

                switch( cornerType )
                {
                    case CornerType.Mitre:
                        Vector3 midNormal = Vector3.Slerp( startCorner.normal, endCorner.normal, 0.5f );
                        Vector3 midTangent = Vector3.Slerp( startCorner.pointTangent, endCorner.pointTangent, 0.5f );
                        Vector2 midScale = Vector2.Lerp( startCorner.scale, endCorner.scale, 0.5f );
                        Color midColor = Color.Lerp( startCorner.color, endCorner.color, 0.5f );
                        float midDistance = Mathf.Lerp( startCorner.distance, endCorner.distance, 0.5f );
                        ExtrudePoint midPoint = ConstructExtrudePoint( splinePoint.position, midNormal, midScale, midColor, midTangent, inTangent, midDistance, CornerMidPointPriority );
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
                float t = Mathf.InverseLerp( 0, segments + 1, i );
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
                Vector2 scale = Vector2.Lerp( startCorner.scale, endCorner.scale, t );
                Color color = Color.Lerp( startCorner.color, endCorner.color, t );

                ExtrudePoint slicePoint = ConstructExtrudePoint( point, normal, scale, color, tangent, tangent, distance, CornerOtherPointPriority );
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

            ExtrudePoint p1 = ConstructExtrudePoint( inSquarePoint, Vector3.Slerp( startCorner.normal, endCorner.normal, 0.25f ), Vector2.Lerp( startCorner.scale, endCorner.scale, 0.25f ), Color.Lerp( startCorner.color, endCorner.color, 0.25f ), p1Tangent, startCorner.inTangent, p1Distance, CornerOtherPointPriority );
            ExtrudePoint p2 = ConstructExtrudePoint( midPoint, Vector3.Slerp( startCorner.normal, endCorner.normal, 0.5f ), Vector2.Lerp( startCorner.scale, endCorner.scale, 0.5f ), Color.Lerp( startCorner.color, endCorner.color, 0.5f ), tangent, tangent, splinePoint.distance, CornerMidPointPriority );
            ExtrudePoint p3 = ConstructExtrudePoint( outSquarePoint, Vector3.Slerp( startCorner.normal, endCorner.normal, 0.75f ), Vector2.Lerp( startCorner.scale, endCorner.scale, 0.75f ), Color.Lerp( startCorner.color, endCorner.color, 0.75f ), p3Tangent, endCorner.inTangent, p3Distance, CornerOtherPointPriority );

            extrudePoints.Add( p1 );
            extrudePoints.Add( p2 );
            extrudePoints.Add( p3 );
        }

        float CalculateVScalar( float length )
        {
            float splineVScalar = length / vTiling;
            if( seamlessVs )
            {
                float roundedVLength = Mathf.Round( splineVScalar );
                float stretchedSplineLength = roundedVLength * vTiling;
                splineVScalar = stretchedSplineLength / length;
            }
            return splineVScalar;
        }

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

            float tubeLength = Mathf.Max( 0.001f, SplineProcessor.CalculateLength( tubePoints ) );
            float vScalar = (vSpace == VSpace.SplineT) ? vTiling / tubeLength : vTiling;
            if( seamlessVs )
            {
                tubeLength = (vSpace == VSpace.SplineT) ? 1 : tubeLength;

                float tiles = tubeLength / (1/vTiling);
                float roundedTiles = Mathf.Round( tiles );
                vScalar *= roundedTiles / tiles;
            }

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

                Matrix4x4 sectionSpaceMatrix = Matrix4x4.TRS( point.position, Quaternion.LookRotation( segmentDirection, point.normal ), inverseExtrudeShapeSize * point.scale );
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
                    colors[index] = point.color * extrudeShape.colors[sv];

                    float u = extrudeShape.u[sv];
                    float v = (extrudeDistance+vOffset) * vScalar;
                    if( mirrorU ) u *= -1;
                    if( mirrorV ) v *= -1;
                    uvs[index] = swapUVs ? new Vector2( v, u ) : new Vector2( u, v );
                }
            }

            // setup triangles
            int ti = 0;
            for( int s = 0; s < segments; ++s )
            {
                int offset = s * vertsInShape;
                for( int l = 0; l < extrudeShape.lines.Length; l += 2 )
                {
                    int a = offset + extrudeShape.lines[l] + vertsInShape;
                    int b = offset + extrudeShape.lines[l];
                    int c = offset + extrudeShape.lines[l + 1];
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
                Matrix4x4 sectionSpaceMatrix = Matrix4x4.TRS( tubePoints[0].position, Quaternion.LookRotation( tubePoints[0].pointTangent, tubePoints[0].normal ), inverseExtrudeShapeSize * tubePoints[0].scale );
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
                    for( int i = endCapTriangles.Count - 1; i >= 0; --i )
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
                Matrix4x4 sectionSpaceMatrix = Matrix4x4.TRS( tubePoints[p].position, Quaternion.LookRotation( tubePoints[p].pointTangent, tubePoints[p].normal ), inverseExtrudeShapeSize * tubePoints[p].scale );
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
}