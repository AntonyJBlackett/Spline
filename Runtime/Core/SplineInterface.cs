using System.Collections.Generic;
using UnityEngine;

// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

namespace FantasticSplines
{
    // used to reposition keys when the spline is edited.
    public enum RepositionMode
    {
        SplineDistance,
        SplineT,
        SegmentDistance,
        SegmentT,
        ClosestPointOnSpline,
        ClosestPointOnSegment,
    }

    [System.Serializable]
    public struct SegmentResult
    {
        public int index; // segment index
        [Range(0,1)]
        public float t; // t value along the segment
        public float distance; // distance in world space along the segment
        public float length; // length of the segment
        public Vector3 position; // world space position
        public Vector3 tangent; // world space tangent
        public Vector3 localPosition; // spline space position
        public Vector3 localTangent; // spline space tangent

        public bool AtSegmentEnd => Mathf.Approximately( t, 1 ); // true if result is approximately at the start of the segment
        public bool AtSegmentStart => Mathf.Approximately( t, 0 ); // true if result is approximately at the end of the segment

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
                    localTangent = Vector3.forward,
                    localPosition = Vector3.zero,
                };
            }
        }
    }

    [System.Serializable]
    public struct SplineResult
    {
        public int updateCount; // can be used to see if the spline has changed since this result
        [Range( 0, 1 )]
        public float t; // distance / length
        public float loopT; // 0 - 1 along spline
        public float length; // real world distance along spline
        public float distance; // real world distance along spline
        public float loopDistance; // real world distance along spline
        public bool isLoop; // true if the spline is a loop (closed)

        public Vector3 position => segmentResult.position;
        public Vector3 tangent => segmentResult.tangent;
        public Vector3 localPosition => segmentResult.localPosition;
        public Vector3 localTangent => segmentResult.localTangent;

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

    // Functions used to update objects on a spline when a spline changes.
    public static class SplineChangedEventHelper
    {
        public static SplineResult OnNodeAdded( SplineResult input, ISpline spline, int nodeIndex, RepositionMode mode = RepositionMode.SplineDistance )
        {
            if( mode == RepositionMode.SegmentDistance
                || mode == RepositionMode.SegmentT
                || mode == RepositionMode.ClosestPointOnSegment )
            {
                if( input.segmentResult.index == nodeIndex-1 )
                {
                    // our segment has been chopped in half! We need to re-calculate where we are on the segment.
                    SplineResult firstSegment = spline.GetResultAtSegmentT( nodeIndex-1, input.segmentResult.t );
                    if( input.segmentResult.distance < firstSegment.segmentResult.length )
                    {
                        input = spline.GetResultAtSegmentDistance( input.segmentResult.index, input.segmentResult.distance );
                    }
                    else
                    {
                        input.segmentResult.index++;
                        input.segmentResult.distance -= firstSegment.segmentResult.length;
                        input = spline.GetResultAtSegmentDistance( input.segmentResult.index, input.segmentResult.distance );
                    }
                }
                else if( input.segmentResult.index > nodeIndex-1 )
                {
                    // a new segment added before us, shuffle along.
                    input.segmentResult.index++;
                    input = spline.GetResultAtSegmentDistance( input.segmentResult.index, input.segmentResult.distance );
                }
            }

            return OnSplineLengthChanged( input, spline, mode );
        }

        public static SplineResult OnNodeRemoved( SplineResult input, ISpline spline, int nodeIndex, RepositionMode mode = RepositionMode.SplineDistance )
        {
            if( mode == RepositionMode.SegmentDistance
                || mode == RepositionMode.SegmentT
                || mode == RepositionMode.ClosestPointOnSegment )
            {
                if( nodeIndex == input.segmentResult.index && nodeIndex == 0 )
                {
                    // first segment removed.
                    if( spline.IsLoop() )
                    {
                        // need to handle loop case here where we merge with the last(loop) segment.
                        SplineResult newSegment = spline.GetResultAtSegmentT( spline.GetSegmentCount(), 0.5f );

                        input.segmentResult.index = newSegment.segmentResult.index;
                        input.segmentResult.distance = newSegment.segmentResult.length - input.segmentResult.length + input.segmentResult.distance;

                        input = spline.GetResultAtSegmentDistance( input.segmentResult.index, input.segmentResult.distance );
                    }
                    else
                    {
                        input.segmentResult.distance = -input.segmentResult.length + input.segmentResult.distance; // negative distance.
                        input = spline.GetResultAtSegmentDistance( input.segmentResult.index, input.segmentResult.distance );
                    }
                }
                else if( nodeIndex == input.segmentResult.index )
                {
                    // merge with previous segment.
                    SplineResult newSegment = spline.GetResultAtSegmentT( nodeIndex-1, 0.5f );

                    input.segmentResult.index = newSegment.segmentResult.index;
                    input.segmentResult.distance = newSegment.segmentResult.length - input.segmentResult.length + input.segmentResult.distance;

                    input = spline.GetResultAtSegmentDistance( input.segmentResult.index, input.segmentResult.distance );
                }
                else if( nodeIndex - 1 == input.segmentResult.index )
                {
                    // merge with next segment. which is still our index and distance so do nothing
                    input = spline.GetResultAtSegmentDistance( input.segmentResult.index, input.segmentResult.distance );
                }
                else if( nodeIndex < input.segmentResult.index )
                {
                    // segment before us was removed, shuffle down
                    input = spline.GetResultAtSegmentDistance( input.segmentResult.index-1, input.segmentResult.distance );
                }
                // else, segment was after us, we don't need to do anyhing.
            }

            return OnSplineLengthChanged( input, spline, mode );
        }

        public static SplineResult OnSplineLengthChanged( SplineResult input, ISpline spline, RepositionMode mode = RepositionMode.SplineDistance )
        {
            SplineResult result;

            // internally the spline uses distance lookup for almost everything which means using t values can be a little inaccurate.
            // to compensate we set the t values back to what they were on input for t lookup modes.
            switch( mode )
            {
                case RepositionMode.SplineDistance:
                    return spline.GetResultAtDistance( input.distance );
                case RepositionMode.SplineT:
                    result = spline.GetResultAtT( input.t );
                    result.t = input.t; 
                    return result;
                case RepositionMode.SegmentDistance:
                    return spline.GetResultAtSegmentDistance( input.segmentResult.index, input.segmentResult.distance );
                case RepositionMode.SegmentT:
                    result = spline.GetResultAtSegmentT( input.segmentResult.index, input.segmentResult.t );
                    result.segmentResult.t = input.segmentResult.t;
                    return result;
                case RepositionMode.ClosestPointOnSpline:
                    return spline.GetResultClosestTo( input.position );
                case RepositionMode.ClosestPointOnSegment:
                    return spline.GetResultClosestToSegment( input.segmentResult.index, input.position );
            }

            // case RepositionMode.SplineDistance:
            return spline.GetResultAtDistance( input.distance );
        }
    }

    // Proxy interface to enable tools to easily use the spline editor in their own editors
    public interface IEditorSplineProxy
    {
        IEditableSpline GetEditableSpline();
        Object[] GetUndoObjects( );
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
        int LoopIndex( int index );
        float GetLength();

        SplineNode GetNode(int index);
        void SetNode(int index, SplineNode node);
        void AppendNode(SplineNode node); // adds the given CurvePoint to the end of the curve
        void PrependNode(SplineNode node); // adds the given CurvePoint to the start of the curve
        void InsertNode(SplineNode node, int index); // inserts a node at node index
        int InsertNode(float t); // inserts a point on the curve without changing its shape and return it
        void RemoveNode(int index); // removes a Curve Point at index
        
        SplineResult GetResultClosestTo(Vector3 point);
        SplineResult GetResultClosestTo(Ray ray);

        // gizmo options
        Color GetColor();
        void SetColor(Color newColor);
        bool GetZTest();
        void SetZTest(bool test);
        float GetGizmoScale();
        void SetGizmoScale(float newScale);
        void DrawSegmentLengths();
        void DrawNodeCoordinates( Space space );
    }

    // This is just a curve in 3D space
    // No rotations, no colours, no normals
    public interface ISpline
    {
        Transform GetTransform();
        int GetUpdateCount();
        float GetLength( );
        bool IsLoop();
        int GetNodeCount();
        int GetSegmentCount();
        SplineResult GetResultAtT(float splineT);
        SplineResult GetResultAtDistance(float splineDistance);
        SplineResult GetResultAtSegmentT(int segmentIndex, float segmentT);
        SplineResult GetResultAtSegmentDistance(int segmentIndex, float segementT);
        SplineResult GetResultClosestTo(Vector3 point);
        SplineResult GetResultClosestTo(Ray ray);
        SplineResult GetResultClosestToSegment( int segmentIndex, Vector3 point );
        SplineResult GetResultAtWorldDistanceFrom(float startDistance, float worldDistance, float stepDistance);
        SplineResult GetResultAtNode( int nodeIndex );
        int LoopIndex(int index);
    }

    // Interface for added additional data to a place
    public interface ISplineParameter<T>
    {
        T GetValueAtT(float t, T defaultValue );
        T GetValueAtDistance( float t, T defaultValue );
    }
}