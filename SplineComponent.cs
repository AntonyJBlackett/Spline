using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FantasticSplines;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FantasticSplines
{
    public class SplineComponent : MonoBehaviour, ISpline, IEditableSpline
    {
        [System.Serializable]
        public struct SplinePosition
        {
            public SplineComponent spline;
            public float distance;

            public SplinePosition(SplineComponent spline, float distance)
            {
                this.spline = spline;
                this.distance = distance;
            }

            public Vector3 Position => spline.GetPositionAtDistance(distance);
            public Vector3 Tangent => spline.GetTangentAtDistance(distance);

            public SplinePosition Move(float stepDistance)
            {
                return new SplinePosition(spline, distance + stepDistance);
            }
        }
        
#if UNITY_EDITOR
        public static bool ShowSegmentLengths
        {
            get => UnityEditor.EditorPrefs.GetBool("FantasticSplinesShowLength", false);
            set => UnityEditor.EditorPrefs.SetBool("FantasticSplinesShowLength", value);
        }
        
        void OnDrawGizmos()
        {
            if (Selection.activeObject != gameObject)
            {
                for (int i = 0; i < curve.SegmentCount; ++i)
                {
                    Bezier3 bezier = Bezier3.Transform( curve.CalculateSegment(i), transform );
                    Handles.DrawBezier(bezier.start, bezier.end, bezier.startTargent, bezier.endTargent, Color.grey, null,
                        2);
                }
                
                Gizmos.color = Color.white;
                for (int i = 0; i < PointCount; ++i)
                {
                    Gizmos.DrawSphere(GetPoint(i).position, 0.05f);
                }
            }

            /*Ray ray = new Ray( Vector3.zero, Vector3.up );
            for (int i = 0; i < curve.SegmentCount; ++i)
            {
                Bezier3 bezier = Bezier3.ProjectToPlane( Bezier3.Transform( curve.CalculateSegment(i), transform ), ray.origin, ray.direction );
                Handles.DrawBezier(bezier.start, bezier.end, bezier.startTargent, bezier.endTargent, Color.red, null,
                    2);

            }*/
            
        }

        private void OnDrawGizmosSelected()
        {
            if (ShowSegmentLengths)
            {
                Handles.matrix = this.transform.localToWorldMatrix;
                for (int i = 0; i < curve.SegmentCount; ++i)
                {
                    Bezier3 bezier = curve.CalculateSegment(i);
                    Vector3 pos = bezier.GetPoint(0.5f);
                    Handles.Label(pos, bezier.Length.ToString("N2"));
                }
                Handles.matrix = Matrix4x4.identity;
            }
        }
#endif

        public Curve curve; // spline in local space

        public bool Loop
        {
            get { return curve.loop; }
            set { curve.loop = value; }
        }

        public int PointCount
        {
            get { return curve.CurvePointCount; }
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

        public void InsertPoint(float t)
        {
            curve.InsertCurvePoint(t);
        }

        public void AddPoint(CurvePoint point)
        {
            curve.AddCurvePoint(point.InverseTransform(transform));
        }

        public void AddPointAt( int index, CurvePoint point)
        {
            curve.AddCurvePointAt( index, point.InverseTransform(transform) );
        }

        public void RemovePoint(int index)
        {
            curve.RemoveCurvePoint(index);
        }

        public CurvePoint GetPoint(int index)
        {
            if (index < 0 || index > PointCount - 1)
            {
                return new CurvePoint(transform.position);
            }

            return TransformPoint(curve.GetCurvePoint(index));
        }

        public void SetPoint(int index, CurvePoint point)
        {
            curve.SetCurvePoint(index, point.InverseTransform(transform));
        }

        public bool IsLoop() => Loop;
        public void SetLoop(bool loop) => Loop = loop;
        public int GetPointCount() => PointCount;
        public Transform GetTransform() => transform;
        public Component GetComponent() => this;

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

        public List<CurvePoint> GetPoints()
        {
            return TransformPoints(curve.GetPoints());
        }

        //TODO this will break when scaled
        public float GetSpeed(float t)
        {
            return curve.GetSpeed(t);
        }

        public Vector3 GetDirection(float t)
        {
            return TransformVector(curve.GetDirection(t));
        }

        public Vector3 GetPoint(float t)
        {
            return TransformPoint(curve.GetPoint(t));
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
        public float GetLength(float fromNormalisedT, float toNormalisedT)
        {
            return curve.GetDistance(toNormalisedT) - curve.GetDistance(fromNormalisedT);
        }

        //TODO this will break when scaled
        public float GetT(float length)
        {
            return curve.GetT(length);
        }

        public float GetClosestT(Vector3 point)
        {
            return curve.GetClosestT(InverseTransformPoint(point));
        }

        public float GetClosestT(Ray ray)
        {
            return curve.GetClosestT(InverseTransformRay(ray));
        }

        public Vector3 GetClosestPoint(Vector3 point)
        {
            return TransformPoint(curve.GetClosestPoint(InverseTransformPoint(point)));
        }

        public Vector3 GetClosestPoint(Ray ray)
        {
            return TransformPoint(curve.GetClosestPoint(InverseTransformRay(ray)));
        }

        //TODO this will break when scaled
        public float Step(float currentT, float worldDistance)
        {
            return curve.Step(currentT, worldDistance);
        }

        //TODO this will break when scaled
        public List<Vector3> GetPoints(float worldSpacing, bool includeEndPoint = true,
            bool includeSplinePoints = false)
        {
            return TransformPoints(GetPoints(worldSpacing, includeEndPoint, includeSplinePoints));
        }
    }
}