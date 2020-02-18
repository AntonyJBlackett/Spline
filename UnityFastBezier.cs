using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityFastBezier {

    /// <summary>
    /// Quadratic Bézier curve calculation class.
    /// </summary>
    class Bezier2 {

        protected const float InterpolationPrecision = 0.001f;

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
        public Vector3 P(float t) => (1.0f - t) * (1.0f - t) * A + 2.0f * t * (1.0f - t) * B + t * t * C;

        /// <summary>
        /// Gets the calculated length.
        /// </summary>
        /// <remarks>
        /// Integral calculation by Dave Eberly, slightly modified for the edge case with colinear control point.
        /// See: http://www.gamedev.net/topic/551455-length-of-a-generalized-quadratic-bezier-curve-in-3d/
        /// </remarks>
        public float Length {
            get {
                if (A == C) {
                    if (A == B) return 0.0f;
                    return (A - B).magnitude;
                }
                if (B == A || B == C) return (A - C).magnitude;
                Vector3 A0 = B - A;
                Vector3 A1 = A - 2.0f * B + C;
                if (A1.sqrMagnitude > float.Epsilon) {
                    float c = 4.0f * Vector3.Dot( A1, A1 );
                    float b = 8.0f * Vector3.Dot( A0, A1 );
                    float a = 4.0f * Vector3.Dot( A0, A0 );
                    float q = 4.0f * a * c - b * b;
                    float twoCpB = 2.0f * c + b;
                    float sumCBA = c + b + a;
                    var l0 = (0.25f / c) * (twoCpB * Mathf.Sqrt(sumCBA) - b * Mathf.Sqrt(a));
                    if (Mathf.Abs(q) <= float.Epsilon) return l0;
                    var l1 = (q / (8.0f * Mathf.Pow(c, 1.5f))) * (Mathf.Log(2.0f * Mathf.Sqrt(c * sumCBA) + twoCpB) - Mathf.Log(2.0f * Mathf.Sqrt(c * a) + b));
                    return l0 + l1;
                }
                else return 2.0f * A0.magnitude;
            }
        }

        /// <summary>
        /// Gets the old slow and inefficient line interpolated length.
        /// </summary>
        public float InterpolatedLength {
            get {
                if (A == C) {
                    if (A == B) return 0;
                    return (A - B).magnitude;
                }
                if (B == A || B == C) return (A - C).magnitude;
                float dt = InterpolationPrecision / (C - A).magnitude, length = 0.0f;
                for (float t = dt; t < 1.0f; t += dt) length += (P(t - dt) - P(t)).magnitude;
                return length;
            }
        }

    }

    /// <summary>
    /// Cubic Bézier curve calculation class.
    /// </summary>
    class Bezier3 {

        public static float InterpolationPrecision = 0.001f;
        public static float LineInterpolationPrecision = 0.05f;

        #region Optimization constants

        protected static float Sqrt3 = Mathf.Sqrt(3f);
        protected static float Div18Sqrt3 = 18f / Sqrt3;
        protected static float OneThird = 1f / 3f;
        protected static float Sqrt3Div36 = Sqrt3 / 36f;

        #endregion

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

        /// <summary>
        /// Creates a cubic Bézier curve.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        public Bezier3(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { A = a; B = b; C = c; D = d; }

        /// <summary>
        /// Interpolated point at t : 0..1 position.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public Vector3 P(float t) => A + 3.0f * t * (B - A) + 3.0f * t * t * (C - 2.0f * B + A) + t * t * t * (D - 3.0f * C + 3.0f * B - A);
        
        /// <summary>
        /// Gets the control point for the mid-point quadratic approximation.
        /// </summary>
        private Vector3 Q => (3.0f * C - D + 3.0f * B - A) / 4.0f;

        /// <summary>
        /// Gets the mid-point quadratic approximation.
        /// </summary>
        public Bezier2 M => new Bezier2(A, Q, D);

        /// <summary>
        /// Splits the curve at given position (t : 0..1).
        /// </summary>
        /// <param name="t">A number from 0 to 1.</param>
        /// <returns>Two curves.</returns>
        /// <remarks>
        /// (De Casteljau's algorithm, see: http://caffeineowl.com/graphics/2d/vectorial/bezierintro.html)
        /// </remarks>
        public Bezier3[] SplitAt(float t) {
            Vector3 a = Vector3.Lerp(A, B, t);
            Vector3 b = Vector3.Lerp(B, C, t);
            Vector3 c = Vector3.Lerp(C, D, t);
            Vector3 m = Vector3.Lerp(a, b, t);
            Vector3 n = Vector3.Lerp(b, c, t);
            Vector3 p = P(t);
            return new[] { new Bezier3(A, a, m, p), new Bezier3(p, n, c, D) };
        }

        /// <summary>
        /// Gets the distance between 0 and 1 quadratic aproximations.
        /// </summary>
        private float D01 => (D - 3.0f * C + 3.0f * B - A).magnitude / 2.0f;

        /// <summary>
        /// Gets the split point for adaptive quadratic approximation.
        /// </summary>
        private float Tmax => Mathf.Pow(Div18Sqrt3 * InterpolationPrecision / D01, OneThird);
        
        /// <summary>
        /// Gets the length of the curve obtained via line interpolation.
        /// </summary>
        public float InterpolatedLength {
            get {
                float dt = LineInterpolationPrecision / (D - A).magnitude, length = 0.0f;
                for (float t = dt; t < 1.0f; t += dt) length += (P(t - dt) - P(t)).magnitude;
                return length;
            }
        }

        /// <summary>
        /// Gets the calculated length of the mid-point quadratic approximation
        /// </summary>
        public float QLength => M.Length;

        /// <summary>
        /// Gets the calculated length of adaptive quadratic approximation.
        /// </summary>
        public float Length {
            get {
                float tmax = 0.0f;
                Bezier3 segment = this;
                List<Bezier3> segments = new List<Bezier3>();
                while ((tmax = segment.Tmax) < 1.0) {
                    var split = segment.SplitAt(tmax);
                    segments.Add(split[0]);
                    segment = split[1];
                }
                segments.Add(segment);
                return segments.Sum(s => s.QLength);
            }
        }
    }

    /// <summary>
    /// Quick demo program.
    /// </summary>
    public class Program {

        /// <summary>
        /// Iterates given function for specified period of time and returns iterations number
        /// </summary>
        /// <param name="a">Action to perform.</param>
        /// <param name="t">Time to iterate.</param>
        /// <param name="name">Name of the test.</param>
        /// <returns>Number of iterations made.</returns>
        static int Benchmark(Action a, TimeSpan t, string name) {
            Debug.Log(String.Format("Testing {0}...", name));
            DateTime start = DateTime.Now;
            int i = 0;
            while (DateTime.Now - start < t) { a(); i++; }
            return (int)(i / t.TotalSeconds);
        }

        /// <summary>
        /// Performs a quick benchmark on some sample data.
        /// </summary>
        static void QuickBench() {
            var test = new Vector3[] {
                new Vector3(-21298.4f, 0.2f, 2627.51f),
                new Vector3(-11.3359f, 0.0f, 0.0f),
                new Vector3(11.2637f, 0.0f, -1.28198f),
                new Vector3(-21332.3f, 0.2f, 2629.43f)
            };
            var testCurve = new Bezier3(test[0], test[0] + test[1], test[2] + test[3], test[3]);
            float l0 = 0, l1 = 0, l2 = 0;
            int s0 = 0, s1 = 0, s2 = 0;
            TimeSpan t = new TimeSpan(0, 0, 3);
            s0 = Benchmark(() => { l0 = testCurve.InterpolatedLength; }, t, "line interpolation");
            s1 = Benchmark(() => { l1 = testCurve.Length; }, t, "adaptive quadratic interpolation");
            s2 = Benchmark(() => { l2 = testCurve.QLength; }, t, "midpoint quadratic interpolation");
            Debug.Log(String.Format(
                "\r\n\t\tLine int.:\t| Adaptive:\t| Midpoint:\r\n" +
                "  Result[m]:\t{0}\t| {1}\t| {2}\r\n" +
                "Speed[op/s]:\t{3}\t\t| {4}\t| {5}",
                l0, l1, l2,
                s0, s1, s2
            ));
            Console.ReadKey(true);
        }

        /// <summary>
        /// Some edge cases with colinear control point test.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when difference between calculated and interpolated lengts are greater than 1%.</exception>
        static void ColinearEdgeCaseTest() {
            var curves = new Bezier2[] {
                new Bezier2(new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(1, 0, 0)),
                new Bezier2(new Vector3(0, 0, 0), new Vector3(0, 2, 0), new Vector3(0, 1, 0)),
                new Bezier2(new Vector3(0, 0, 0), new Vector3(0, 0, 2), new Vector3(0, 0, 1)),
                new Bezier2(new Vector3(0, 0, 0), new Vector3(2, 2, 2), new Vector3(1, 1, 1)),
                new Bezier2(new Vector3(0, 0, 0), new Vector3(-1, -1, -1), new Vector3(1, 1, 1)),
            };
            foreach (var curve in curves) {
                var error = Mathf.Abs(curve.Length - curve.InterpolatedLength);
                if (error > 0.01) throw new InvalidOperationException();
            }
        }

        public static void Main() {
            ColinearEdgeCaseTest();
            QuickBench();
        }

    }
}