using System.Collections.Generic;
using UnityEngine;
namespace FantasticSplines
{
    public struct SplineResult
    {
        public float splineT; // 0 - 1 along spline
        public float splineDistance; // real world distance along spline

        public Vector3 position; // world position
        public Vector3 tangent; // world direction

        public int segmentIndex;
        public float segmentT;
        public float segmentDistance;
        public float segmentLength;

        public bool AtEnd => Mathf.Approximately( splineT, 1 );
        public bool AtStart => Mathf.Approximately( splineT, 0 );

        public bool AtSegmentEnd => Mathf.Approximately( segmentT, 1 );
        public bool AtSegmentStart => Mathf.Approximately( segmentT, 0 );

        public static SplineResult Default
        {
            get
            {
                return new SplineResult()
                {
                    position = Vector3.zero,
                    tangent = Vector3.forward,

                    splineT = 0,
                    splineDistance = 0,

                    segmentIndex = 0,
                    segmentT = 0,
                    segmentDistance = 0,
                    segmentLength = 0
                };
            }
        }
    }

    // Proxy interfact to enable tools to easily use the spline editor in their own editors
    public interface IEditorSplineProxy
    {
        IEditableSpline GetEditableSpline();
        Object GetUndoObject();
    }

    // Interface for SplineEditor. Any component that implements this can be used with the editor
    public interface IEditableSpline : IEditorSplineProxy
    {
        // object that has the spline
        Transform GetTransform();
        // componenet that has the spline, used for undo/redo
        Component GetComponent();

        int GetNodeCount();
        bool IsLoop();
        void SetLoop(bool loop);
        float GetLength();

        SplineNode GetNode(int index);
        void SetNode(int index, SplineNode node);
        void AppendNode(SplineNode node); // adds the given CurvePoint to the end of the curve
        void PrependNode(SplineNode node); // adds the given CurvePoint to the start of the curve
        void InsertNode(float t); // inserts a point on the curve without changing its shape
        void RemoveNode(int index); // removes a Curve Point at index

        SplineResult GetResultClosestTo(Vector3 point);
        SplineResult GetResultClosestTo(Ray ray);

        // gizmo options
        Color GetColor();
        void SetColor(Color newColor);
        bool GetZTest();
        void SetZTest(bool test);
        void DrawSegmentLengths();
    }

    // This is just a curve in 3D space
    // No rotations, no colours, no normals
    public interface ISpline
    {
        SplineResult GetResultAtT(float t);
        SplineResult GetResultAtDistance(float distance);
        SplineResult GetResultClosestTo(Vector3 point);
        SplineResult GetResultClosestTo(Ray ray);
    }

    // Interface for added additional data to a place
    public interface ISplineParameter<T>
    {
        T GetValueAt(float t);
    }
}