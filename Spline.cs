using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FantasticSplines;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FantasticSplines
{
    public class Spline : SplineBehaviour
    {

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (Selection.activeObject != gameObject)
            {
                for (int i = 0; i < curve.SegmentCount; ++i)
                {
                    Bezier3 bezier = Bezier3.Transform( curve.GetBezierForSegment(i), transform );
                    Handles.DrawBezier(bezier.start, bezier.end, bezier.B, bezier.C, Color.grey, null,
                        2);
                }
                
                Gizmos.color = Color.white;
                for (int i = 0; i < PointCount; ++i)
                {
                    Gizmos.DrawSphere(GetPoint(i).position, 0.05f);
                }
            }
        }
#endif

        public override void DrawSegmentLengths()
        {
#if UNITY_EDITOR
            for (int i = 0; i < curve.SegmentCount; ++i)
            {
                Bezier3 bezier = Bezier3.Transform( curve.GetBezierForSegment(i), transform );
                Vector3 pos = bezier.GetPos(0.5f);
                Handles.Label(pos, bezier.Length.ToString("N2"));
            }
#endif
        }

        public MCCurve curve = new MCCurve(); // spline in local space

        public bool Loop
        {
            get { return curve.Loop; }
            set { curve.Loop = value; }
        }

        public int PointCount
        {
            get { return curve.PointCount; }
        }

        Vector3 InverseTransformPoint(Vector3 point)
        {
            return transform.InverseTransformPoint(point);
        }

        Vector3 TransformPoint(Vector3 point)
        {
            return transform.TransformPoint(point);
        }

        Vector3 InverseTransformVector(Vector3 vector)
        {
            return transform.InverseTransformVector(vector);
        }

        Vector3 TransformVector(Vector3 vector)
        {
            return transform.TransformVector(vector);
        }

        Vector3 InverseTransformDirection(Vector3 direction)
        {
            return transform.InverseTransformDirection(direction);
        }

        Vector3 TransformDirection(Vector3 direction)
        {
            return transform.TransformDirection(direction);
        }

        CurvePoint TransformPoint(CurvePoint point)
        {
            return point.Transform(transform);
        }

        CurvePoint InverseTransformPoint(CurvePoint point)
        {
            return point.InverseTransform(transform);
        }

        List<Vector3> TransformPoints(List<Vector3> points)
        {
            for (int i = 0; i < points.Count; ++i)
            {
                points[i] = TransformPoint(points[i]);
            }

            return points;
        }

        List<CurvePoint> TransformPoints(List<CurvePoint> points)
        {
            for (int i = 0; i < points.Count; ++i)
            {
                points[i] = TransformPoint(points[i]);
            }

            return points;
        }

        Ray InverseTransformRay(Ray ray)
        {
            ray.origin = InverseTransformPoint(ray.origin);
            ray.direction = InverseTransformDirection(ray.direction);
            return ray;
        }

        public bool IsIndexInRange(int index)
        {
            return index >= 0 && index < PointCount;
        }

        public override void InsertPoint(float t)
        {
            curve.InsertPoint( t );
        }

        public override void AddPoint(CurvePoint point)
        {
            curve.AddPoint(point.InverseTransform(transform));
        }

        public override void AddPointAt( int index, CurvePoint point)
        {
            curve.AddPointAt( index, point.InverseTransform(transform) );
        }

        public override void RemovePoint(int index)
        {
            curve.RemovePoint(index);
        }

        public override CurvePoint GetPoint(int index)
        {
            if (index < 0 || index > PointCount - 1)
            {
                return new CurvePoint(transform.position);
            }

            return TransformPoint(curve.GetPoint(index));
        }

        public override void SetPoint(int index, CurvePoint point)
        {
            curve.SetPoint(index, point.InverseTransform(transform));
        }

        public override bool IsLoop() => Loop;
        public override void SetLoop(bool loop) { Loop = loop; }
        public override int GetPointCount() => PointCount;
        public override Transform GetTransform() => transform;
        public override Component GetComponent() => this;

        const float resolution = 0.1f;

        public List<Vector3> GetPolyLinePoints()
        {
            List<Vector3> points = new List<Vector3>();
            if (PointCount < 2)
            {
                return points;
            }

            float step = resolution / curve.Length;
            for (float f = 0; f <= 1f; f += step)
            {
                points.Add(this.TransformPoint(curve.GetPoint(f)));
            }

            return points;
        }

        public int GetClosestSegmentIndex(Ray ray)
        {
            throw new System.NotImplementedException();
        }

        public override List<CurvePoint> GetPoints()
        {
            return TransformPoints(curve.GetPoints());
        }

        //TODO this will break when scaled
        public override float GetSpeed(float t)
        {
            return curve.GetSpeed(t);
        }

        public override Vector3 GetDirection(float t)
        {
            return TransformVector(curve.GetDirection(t));
        }

        public override Vector3 GetPoint(float t)
        {
            return TransformPoint(curve.GetPoint(t));
        }

        public override float GetDistanceOnSpline(SegmentPosition position)
        {
            return curve.GetDistance(position);
        }
        public override Vector3 GetPosition(SegmentPosition position)
        {
            return curve.GetPosition(position);
        }

        public override Vector3 GetDirection(SegmentPosition position)
        {
            return curve.GetDirection(position);
        }

        public override SegmentPosition GetSegmentAtDistance(float distance)
        {
            return curve.GetSegmentAtDistance(distance);
        }

        public float GetLength()
        {
            return curve.Length;
        }

        public float GetLength(float toNormalisedT)
        {
            return curve.GetDistance(toNormalisedT);
        }

        //TODO this will break when scaled
        public override float GetLength(float fromNormalisedT, float toNormalisedT)
        {
            return curve.GetLength(fromNormalisedT, toNormalisedT);
        }

        //TODO this will break when scaled
        public override float GetT(float length)
        {
            return curve.GetT(length);
        }

        public override float GetClosestT(Vector3 point)
        {
            return curve.GetClosestT(InverseTransformPoint(point));
        }

        public override float GetClosestT(Ray ray)
        {
            return curve.GetClosestT(InverseTransformRay(ray));
        }

        public override Vector3 GetClosestPoint(Vector3 point)
        {
            return TransformPoint(curve.GetClosestPoint(InverseTransformPoint(point)));
        }

        public override Vector3 GetClosestPoint(Ray ray)
        {
            return TransformPoint(curve.GetClosestPoint(InverseTransformRay(ray)));
        }

        //TODO this will break when scaled
        public override float Step(float currentT, float worldDistance)
        {
            return curve.Step(currentT, worldDistance);
        }

        //TODO this will break when scaled
        public override List<Vector3> GetPoints(float worldSpacing, bool includeEndPoint = true,
            bool includeSplinePoints = false)
        {
            return TransformPoints(GetPoints(worldSpacing, includeEndPoint, includeSplinePoints));
        }
    }
}