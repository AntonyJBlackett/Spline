
using UnityEngine;

// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

namespace FantasticSplines
{
    public struct SegmentResult
    {
        public int index;
        public float t;
        public float distance;
        public float length;
        public Vector3 position;
        public Vector3 tangent;

        public bool AtSegmentEnd => Mathf.Approximately( t, 1 );
        public bool AtSegmentStart => Mathf.Approximately( t, 0 );

        public static SegmentResult Default
        {
            get
            {
                return new SegmentResult()
                {
                    index = 0,
                    t = 0,
                    distance = 0,
                    length = 0,
                    tangent = Vector3.forward,
                    position = Vector3.zero,

                };
            }
        }
    }

    public struct SplineResult
    {
        public int updateCount; // can be used to see if the spline has changed since this result
        public float t; // distance / length
        public float loopT; // 0 - 1 along spline
        public float length; // real world distance along spline
        public float distance; // real world distance along spline
        public float loopDistance; // real world distance along spline
        public bool isLoop;

        public Vector3 position => segmentResult.position;
        public Vector3 tangent => segmentResult.tangent;

        public int lapCount;

        public bool AtEnd => !isLoop && Mathf.Approximately( t, 1 );
        public bool AtStart => !isLoop && Mathf.Approximately( t, 0 );

        public SegmentResult segmentResult;

        public static SplineResult Default
        {
            get
            {
                return new SplineResult()
                {
                    segmentResult = SegmentResult.Default,
                };
            }
        }

        // transforms the result from spline component space to transform space.
        // Useful for instancing objects in multiple locations with a single spline.
        public SplineResult ConvertTransform(Transform originalTransform, Transform newTransform)
        {
            SplineResult result = this;
            result.segmentResult.position = newTransform.TransformPoint( originalTransform.InverseTransformPoint( position ) );
            result.segmentResult.tangent = newTransform.TransformVector( originalTransform.InverseTransformVector( tangent ) );
            return result;
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
        Transform GetTransform();
        int GetUpdateCount();
        SplineResult GetResultAtT(float splineT);
        SplineResult GetResultAtDistance(float splineDistance);
        SplineResult GetResultAtSegmentT(int segmentIndex, float segmentT);
        SplineResult GetResultAtSegmentDistance(int segmentIndex, float segementT);
        SplineResult GetResultClosestTo(Vector3 point);
        SplineResult GetResultClosestTo(Ray ray);
        SplineResult GetResultAtWorldDistanceFrom(float startDistance, float worldDistance, float stepDistance);
    }

    // Interface for added additional data to a place
    public interface ISplineParameter<T>
    {
        T GetValueAt(float t);
    }
}