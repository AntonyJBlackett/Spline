using System.Collections.Generic;
using UnityEngine;
namespace FantasticSplines
{
    // Interface for SplineEditor. Any component that implements this can be used with the editor
    public interface IEditableSpline
    {
        Transform GetTransform();
        Component GetComponent(); // return component that is or stores this spline. So that the editor can inform unity of changes to the component

        //CurvePoint[] GetPoints();
        int GetPointCount();
        bool IsLoop();
        void SetLoop( bool loop );

        CurvePoint GetPoint(int index);
        void SetPoint(int index, CurvePoint point);

        void InsertPoint( float t ); // inserts a point on the curve without changing its shape
        void AddPoint(CurvePoint point); // appends a point to the end of the curve at position
        void AddPointAt(int index, CurvePoint point); // adds the given CurvePoint at index in the list of CurvePoint in curve
        void RemovePoint(int index); // removes a Curve Point at index

        float GetClosestT(Vector3 point);
        Vector3 GetClosestPoint(Vector3 point);
        Vector3 GetClosestPoint(Ray ray);
    }

    // This is just a curve in 3D space
    // No rotations, no colours, no normals
    public interface ISpline
    {
        float GetSpeed(float t);
        Vector3 GetDirection(float t);
        Vector3 GetPoint(float t);
        float GetLength(float fromT = 0, float toT = 1);
        float GetT(float length);

        float GetClosestT(Vector3 point);
        float GetClosestT(Ray ray);
        Vector3 GetClosestPoint(Vector3 point);
        Vector3 GetClosestPoint(Ray ray);

        float Step(float t, float worldDistance);

        List<CurvePoint> GetPoints(); // returns points that define the curve
        List<Vector3> GetPoints( float worldSpacing, 
            bool includeEndPoint = true, // adds in the end point if worldSpacing does not land on it
            bool includeSplinePoints = false ); // adds in the points used to define the spline
    }

    // Interface for added additional data to a place
    public interface ISplineParameter<T>
    {
        T GetValueAt( float t );
    }

    public static class SplineUtils
    {
        public static Vector3 GetPositionAtDistance(this ISpline spline, float distance)
        {
            return spline.GetPoint(spline.GetT(distance));
        }
        public static Vector3 GetTangentAtDistance(this ISpline spline, float distance)
        {
            return spline.GetDirection(spline.GetT(distance));
        }
    }
}