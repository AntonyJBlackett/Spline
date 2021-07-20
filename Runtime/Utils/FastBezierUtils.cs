using UnityEngine;

// Authors: Antony Blackett, Matthew Clark
//
// FastBezier utility is a converstion of FastBezier c# from 'HTD' on github along with some added additions.
// The original can be found here https://github.com/HTD/FastBezier
// HTD credits Adrian Colomitchi and Dave Eberly for the original theory. See the bottom of his github page.
// HTD lists the license for FastBezier as Free and Open Source. 
// Therefore FastBezierUtils.cs is also Free and Open Source, but the rest of Fantastic Spline is not.
// For more info contact me at: antony@fantasticfoundry.com

namespace FantasticSplines
{
    public static class Utils
    {
        // Legendre-Gauss abscissae with n=24 (x_i values, defined at i=n as the roots of the nth order Legendre polynomial Pn(x))
        public static readonly float[] Tvalues = new float[] {
            -0.0640568928626056260850430826247450385909f,
            0.0640568928626056260850430826247450385909f,
            -0.1911188674736163091586398207570696318404f,
            0.1911188674736163091586398207570696318404f,
            -0.3150426796961633743867932913198102407864f,
            0.3150426796961633743867932913198102407864f,
            -0.4337935076260451384870842319133497124524f,
            0.4337935076260451384870842319133497124524f,
            -0.5454214713888395356583756172183723700107f,
            0.5454214713888395356583756172183723700107f,
            -0.6480936519369755692524957869107476266696f,
            0.6480936519369755692524957869107476266696f,
            -0.7401241915785543642438281030999784255232f,
            0.7401241915785543642438281030999784255232f,
            -0.8200019859739029219539498726697452080761f,
            0.8200019859739029219539498726697452080761f,
            -0.8864155270044010342131543419821967550873f,
            0.8864155270044010342131543419821967550873f,
            -0.9382745520027327585236490017087214496548f,
            0.9382745520027327585236490017087214496548f,
            -0.9747285559713094981983919930081690617411f,
            0.9747285559713094981983919930081690617411f,
            -0.9951872199970213601799974097007368118745f,
            0.9951872199970213601799974097007368118745f
        };

        // Legendre-Gauss weights with n=24 (w_i values, defined by a function linked to in the Bezier primer article)
        public static readonly float[] Cvalues = new float[]
        {
            0.1279381953467521569740561652246953718517f,
            0.1279381953467521569740561652246953718517f,
            0.1258374563468282961213753825111836887264f,
            0.1258374563468282961213753825111836887264f,
            0.1216704729278033912044631534762624256070f,
            0.1216704729278033912044631534762624256070f,
            0.1155056680537256013533444839067835598622f,
            0.1155056680537256013533444839067835598622f,
            0.1074442701159656347825773424466062227946f,
            0.1074442701159656347825773424466062227946f,
            0.0976186521041138882698806644642471544279f,
            0.0976186521041138882698806644642471544279f,
            0.0861901615319532759171852029837426671850f,
            0.0861901615319532759171852029837426671850f,
            0.0733464814110803057340336152531165181193f,
            0.0733464814110803057340336152531165181193f,
            0.0592985849154367807463677585001085845412f,
            0.0592985849154367807463677585001085845412f,
            0.0442774388174198061686027482113382288593f,
            0.0442774388174198061686027482113382288593f,
            0.0285313886289336631813078159518782864491f,
            0.0285313886289336631813078159518782864491f,
            0.0123412297999871995468056670700372915759f,
            0.0123412297999871995468056670700372915759f
        };
    }

    public struct SimpleLine
    {
        public Vector3 A, B;

        public SimpleLine(Vector3 a, Vector3 b)
        {
            A = a; B = b;
        }

        public Vector3 Derivative(float t)
        {
            return Vector3.Lerp( A, B, t );
        }
    }

    /// <summary>
    /// Quadratic Bézier curve calculation class.
    /// </summary>
    public struct Bezier2
    {
        /// <summary>
        /// Start point.
        /// </summary>
        public Vector3 A;
        /// <summary>
        /// Control point.
        /// </summary>
        public Vector3 B;
        /// <summary>
        /// End point.
        /// </summary>
        public Vector3 C;

        /// <summary>
        /// Creates a quadratic Bézier curve.
        /// </summary>
        /// <param name="a">Start point.</param>
        /// <param name="b">Control point.</param>
        /// <param name="c">End point.</param>
        public Bezier2(Vector3 a, Vector3 b, Vector3 c) { A = a; B = b; C = c; }

        /// <summary>
        /// Interpolated point at t : 0..1 position
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public Vector3 GetPos(float t)
        {
            float mt = (1.0f - t);
            return mt * mt * A + 2.0f * t * mt * B + t * t * C;
        }

        public Vector3 P(float t) => GetPos( t );

        public float GetLengthInterpolated(int steps)
        {
            float step = 1f / steps;
            float distance = 0f;
            Vector3 prev = GetPos( 0f );
            for( float t = step; t < 1f; t += step )
            {
                Vector3 curr = GetPos( t );
                distance += (curr - prev).magnitude;
                prev = curr;
            }

            return distance;
        }

        public float Length
        {
            get
            {
                SimpleLine cube = DerivedCurve();

                float z = 0.5f;
                float sum = 0;
                int len = Utils.Tvalues.Length;
                for( int i = 0; i < len; i++ )
                {
                    float t = z * Utils.Tvalues[i] + z;
                    sum += Utils.Cvalues[i] * cube.Derivative( t ).magnitude;
                }
                return z * sum;
            }
        }

        public SimpleLine DerivedCurve()
        {
            return new SimpleLine(
                2 * (B - A),
                2 * (C - B) );
        }

        public Vector3 Derivative(float t)
        {
            float mt = 1f - t;
            float a = mt * mt;
            float b = mt * t * 2;
            float c = t * t;
            return (a * A) + (b * B) + (c * C);
        }
    }

    /// <summary>
    /// Cubic Bézier curve calculation class.
    /// </summary>
    [System.Serializable]
    public struct Bezier3
    {
        const float InterpolationPrecision = 0.001f;
        const float LineInterpolationPrecision = 0.05f;

        const float Sqrt3 = 1.7320508076f; // Mathf.Sqrt(3f)
        const float Div18Sqrt3 = 18f / Sqrt3;
        const float OneThird = 1f / 3f;
        const float Sqrt3Div36 = Sqrt3 / 36f;

        /// <summary>
        /// Start point.
        /// </summary>
        public Vector3 A;
        /// <summary>
        /// Control point 1.
        /// </summary>
        public Vector3 B;
        /// <summary>
        /// Control point 2.
        /// </summary>
        public Vector3 C;
        /// <summary>
        /// End point.
        /// </summary>
        public Vector3 D;

        public Vector3 start => A;
        public Vector3 startControl => B - A;
        public Vector3 endControl => C - D;
        public Vector3 end => D;

        /// <summary>
        /// Creates a cubic Bézier curve.
        /// </summary>
        public Bezier3(Vector3 startPoint, Vector3 control1, Vector3 control2, Vector3 endPoint) { A = startPoint; B = control1; C = control2; D = endPoint; }

        /// <summary>
        /// Creates a cubic Bézier curve.
        /// </summary>
        public Bezier3(SplineNode start, SplineNode end)
        {
            A = start.position;
            B = start.position + start.LocalOutControlPoint;
            C = end.position + end.LocalInControlPoint;
            D = end.position;
        }

        /// <summary>
        /// Interpolated point at t : 0..1 position.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public Vector3 GetPosition(float t)
        {
            return A + 3.0f * t * (B - A) + 3.0f * t * t * (C - 2.0f * B + A) + t * t * t * (D - 3.0f * C + 3.0f * B - A);
        }

        /// <summary>
        /// Interpolated tangent at t : 0..1 position.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public Vector3 GetTangent(float t)
        {
            t = Mathf.Clamp( t, 0.0001f, 0.9999f ); // clamp like this of there is no tangent at position exactly on the curve point
            float oneMinusT = 1f - t;
            return
                3f * oneMinusT * oneMinusT * (B - A) +
                6f * oneMinusT * t * (C - B) +
                3f * t * t * (D - C);
        }

        /// <summary>
        /// Gets the control point for the mid-point quadratic approximation.
        /// </summary>
        private Vector3 Q
        {
            get { return (3.0f * C - D + 3.0f * B - A) / 4.0f; }
        }

        /// <summary>
        /// Gets the mid-point quadratic approximation.
        /// </summary>
        public Bezier2 ApproximateQuadratic
        {
            get { return new Bezier2( A, Q, D ); }
        }

        /// <summary>
        /// Gets the calculated length of the mid-point quadratic approximation
        /// </summary>
        public float ApproximateQuadraticLength
        {
            get { return ApproximateQuadratic.Length; }
        }

        /// <summary>
        /// Splits the curve at given position (t : 0..1).
        /// </summary>
        /// <param name="t">A number from 0 to 1.</param>
        /// <returns>Two curves.</returns>
        /// <remarks>
        /// (De Casteljau's algorithm, see: http://caffeineowl.com/graphics/2d/vectorial/bezierintro.html)
        /// </remarks>
        public void SplitAt(float t, out Bezier3 left, out Bezier3 right)
        {
            Vector3 a = Vector3.Lerp( A, B, t );
            Vector3 b = Vector3.Lerp( B, C, t );
            Vector3 c = Vector3.Lerp( C, D, t );
            Vector3 m = Vector3.Lerp( a, b, t );
            Vector3 n = Vector3.Lerp( b, c, t );
            Vector3 p = GetPosition( t );

            left = new Bezier3( A, a, m, p );
            right = new Bezier3( p, n, c, D );
        }

        public Bezier3 MiddleSplitAt(float t1, float t2)
        {
            Bezier3 right = RightSplitAt( t1 );
            float newT = Mathf.InverseLerp( t1, 1f, t2 );
#if DEBUG
            Debug.Assert( (right.GetPosition( newT ) - GetPosition( t2 )).sqrMagnitude < 0.0001f );
#endif
            return right.LeftSplitAt( newT );
        }

        public Bezier3 LeftSplitAt(float t)
        {
            if( Mathf.Approximately( t, 1f ) )
            {
                return this;
            }
            Vector3 a = Vector3.Lerp( A, B, t );
            Vector3 b = Vector3.Lerp( B, C, t );
            Vector3 m = Vector3.Lerp( a, b, t );
            Vector3 p = GetPosition( t );

            return new Bezier3( A, a, m, p );
        }

        public Bezier3 RightSplitAt(float t)
        {
            if( Mathf.Approximately( t, 0f ) )
            {
                return this;
            }
            Vector3 b = Vector3.Lerp( B, C, t );
            Vector3 c = Vector3.Lerp( C, D, t );
            Vector3 n = Vector3.Lerp( b, c, t );
            Vector3 p = GetPosition( t );

            return new Bezier3( p, n, c, D );
        }

        /// <summary>
        /// Gets the derivative curve
        /// </summary>
        private Bezier2 DerivedCurve()
        {
            return new Bezier2(
                3 * (B - A),
                3 * (C - B),
                3 * (D - C)
            );
        }

        /// <summary>
        /// Gets the calculated length of adaptive quadratic approximation.
        /// </summary>
        public float Length
        {
            get
            {
                Bezier2 cube = DerivedCurve();

                float z = 0.5f;
                float sum = 0;
                int len = Utils.Tvalues.Length;
                for( int i = 0; i < len; i++ )
                {
                    float t = z * Utils.Tvalues[i] + z;
                    sum += Utils.Cvalues[i] * cube.Derivative( t ).magnitude;
                }
                return z * sum;
            }
        }

        public float CalculateDistanceAt(float t)
        {
            return LeftSplitAt( t ).Length;
        }

        public float CalculateRadiusAt( float t )
        {
            float curvature = CalculateCurvatureAt( t );
            if( Mathf.Approximately( curvature, 0 ) )
            {
                return float.MaxValue;
            }
            return 1.0f / curvature;
        }

        public float CalculateCurvatureAt( float t )
        {
            // I found this here:
            // https://stackoverflow.com/questions/16140281/how-to-determine-curvature-of-a-cubic-bezier-path-at-an-end-point
            /*ax = P[1].x - P[0].x;               //  a = P1 - P0
            ay = P[1].y - P[0].y;
            bx = P[2].x - P[1].x - ax;          //  b = P2 - P1 - a
            by = P[2].y - P[1].y - ay;
            cx = P[3].x - P[2].x - bx * 2 - ax;   //  c = P3 - P2 - 2b - a
            cy = P[3].y - P[2].y - by * 2 - ay;

            bc = bx * cy - cx * by;
            ac = ax * cy - cx * ay;
            ab = ax * by - bx * ay;

            r = ab + ac * t + bc * t * t;*/

            Vector3 a = B - A;
            Vector3 b = C - B - a;
            Vector3 c = D - C - 2 * b - a;

            float bc = Vector3.Dot( b, c );
            float ac = Vector3.Dot( a, c );
            float ab = Vector3.Dot( a, b );

            return ab + ac * t + bc * t * t / Length;
        }

        public float GetLengthInterpolated(int steps)
        {
            float step = 1f / steps;
            float distance = 0f;
            Vector3 prev = GetPosition( 0f );
            for( float t = step; t < 1f; t += step )
            {
                Vector3 curr = GetPosition( t );
                distance += (curr - prev).magnitude;
                prev = curr;
            }

            return distance;
        }

        public float GetClosestT(Vector3 pos, float paramThreshold = 0.000001f)
        {
            return GetClosestTRecursive( pos, 0.0f, 1.0f, paramThreshold );
        }

        float GetClosestTRecursive(Vector3 pos, float beginT, float endT, float thresholdT)
        {
            float mid = (beginT + endT) / 2.0f;

            // Base case for recursion.
            if( (endT - beginT) < thresholdT )
                return mid;

            // The two halves have param range [start, mid] and [mid, end]. We decide which one to use by using a midpoint param calculation for each section.
            float paramA = (beginT + mid) / 2.0f;
            float paramB = (mid + endT) / 2.0f;

            Vector3 posA = GetPosition( paramA );
            Vector3 posB = GetPosition( paramB );
            float distASq = (posA - pos).sqrMagnitude;
            float distBSq = (posB - pos).sqrMagnitude;

            if( distASq < distBSq )
                endT = mid;
            else
                beginT = mid;

            // The (tail) recursive call.
            return GetClosestTRecursive( pos, beginT, endT, thresholdT );
        }

        public static Bezier3 ProjectToPlane(Bezier3 curve, Vector3 planePoint, Vector3 planeNormal)
        {
            Bezier3 result = curve;
            result.A = MathsUtils.LinePlaneIntersection( curve.A, planeNormal, planePoint, planeNormal );
            result.B = MathsUtils.LinePlaneIntersection( curve.B, planeNormal, planePoint, planeNormal );
            result.C = MathsUtils.LinePlaneIntersection( curve.C, planeNormal, planePoint, planeNormal );
            result.D = MathsUtils.LinePlaneIntersection( curve.D, planeNormal, planePoint, planeNormal );
            return result;
        }

        public static Bezier3 Transform(Bezier3 curve, Transform transform)
        {
            Bezier3 result = curve;
            result.A = transform.TransformPoint( curve.A );
            result.B = transform.TransformPoint( curve.B );
            result.C = transform.TransformPoint( curve.C );
            result.D = transform.TransformPoint( curve.D );
            return result;
        }
    }

}
