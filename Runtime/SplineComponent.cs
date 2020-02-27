﻿using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

namespace FantasticSplines
{
    public class SplineComponent : MonoBehaviour, ISpline, IEditableSpline
    {
        [HideInInspector]
        [SerializeField]
        Curve curve = new Curve(); // spline in local space

        public float GetLength() { return curve.Length; }

        public bool IsLoop() { return curve.Loop; }
        public void SetLoop(bool loop) { curve.Loop = loop; }

        public int GetNodeCount()
        {
            return curve.NodeCount;
        }

        bool IsNodeIndexInRange(int index)
        {
            return MathsUtils.IsInArrayRange( index, GetNodeCount() );
        }

        Vector3 InverseTransformPoint(Vector3 point)
        {
            return transform.InverseTransformPoint( point );
        }

        Vector3 TransformPoint(Vector3 point)
        {
            return transform.TransformPoint( point );
        }

        Vector3 InverseTransformVector(Vector3 vector)
        {
            return transform.InverseTransformVector( vector );
        }

        Vector3 TransformVector(Vector3 vector)
        {
            return transform.TransformVector( vector );
        }

        Vector3 InverseTransformDirection(Vector3 direction)
        {
            return transform.InverseTransformDirection( direction );
        }

        Vector3 TransformDirection(Vector3 direction)
        {
            return transform.TransformDirection( direction );
        }

        SplineNode TransformNode(SplineNode node)
        {
            return node.Transform( transform );
        }

        SplineNode InverseTransformNode(SplineNode node)
        {
            return node.InverseTransform( transform );
        }

        List<Vector3> TransformPoints(List<Vector3> points)
        {
            for( int i = 0; i < points.Count; ++i )
            {
                points[i] = TransformPoint( points[i] );
            }

            return points;
        }

        List<SplineNode> TransformPoints(List<SplineNode> points)
        {
            for( int i = 0; i < points.Count; ++i )
            {
                points[i] = TransformNode( points[i] );
            }

            return points;
        }

        Ray InverseTransformRay(Ray ray)
        {
            ray.origin = InverseTransformPoint( ray.origin );
            ray.direction = InverseTransformDirection( ray.direction );
            return ray;
        }

        SplineResult TransformResult(SplineResult toTransform)
        {
            SplineResult result = toTransform;
            result.segmentResult.position = TransformPoint( toTransform.position );
            result.segmentResult.tangent = TransformVector( toTransform.tangent );
            return result;
        }

        public void AppendNode(SplineNode node)
        {
            curve.AddNode( node.InverseTransform( transform ) );
        }

        public void PrependNode(SplineNode node)
        {
            curve.AddNodeAt( 0, node.InverseTransform( transform ) );
        }

        public void InsertNode(float t)
        {
            curve.InsertNode( t );
        }

        public void RemoveNode(int index)
        {
            curve.RemoveNode( index );
        }

        public SplineNode GetNode(int index)
        {
            if( !IsNodeIndexInRange( index ) )
            {
                return new SplineNode( transform.position );
            }

            return TransformNode( curve.GetNode( index ) );
        }

        public void SetNode(int index, SplineNode node)
        {
            curve.SetNode( index, node.InverseTransform( transform ) );
        }

        public int GetUpdateCount()
        {
            return curve.UpdateCount;
        }

        public SplineResult GetResultAtT(float t)
        {
            return TransformResult( curve.GetResultAtT( t ) );
        }

        public SplineResult GetResultAtDistance(float distance)
        {
            return TransformResult( curve.GetResultAtDistance( distance ) );
        }

        public SplineResult GetResultAtSegmentT(int segentIndex, float segmentT)
        {
            return TransformResult( curve.GetResultAtSegmentT( segentIndex, segmentT ) );
        }

        public SplineResult GetResultAtSegmentDistance(int segentIndex, float segmentDistance)
        {
            return TransformResult( curve.GetResultAtSegmentDistance( segentIndex, segmentDistance ) );
        }

        public SplineResult GetResultAtWorldDistanceFrom(float startDistance, float worldDistance, float stepDistance)
        {
            if( Mathf.Approximately( stepDistance, 0 ) )
            {
                Debug.LogWarning( "Step is too small." );
                return GetResultAtDistance( startDistance );
            }
            if( GetLength() < worldDistance )
            {
                // early out for short splines
                if( stepDistance > 0 )
                {
                    return GetResultAtDistance( 1 );
                }
                else
                {
                    return GetResultAtDistance( 0 );
                }
            }

            int maxIterations = Mathf.CeilToInt( worldDistance * 5f / stepDistance );
            int iterationsLeft = maxIterations;

            SplineResult currentPosition = GetResultAtDistance( startDistance );
            int startLapCount = currentPosition.lapCount;
            float startLoopDistance = currentPosition.loopDistance;

            Vector3 origin = currentPosition.position;

            float currentWorldDistance;
            SplineResult previousPosition;
            do
            {
                if(!IsLoop() && currentPosition.t >= 1 && stepDistance >= 0 || currentPosition.t <= 0 && stepDistance < 0)
                {
                    return currentPosition;
                }
                else if( startLapCount != currentPosition.lapCount )
                {
                    if( startLoopDistance < currentPosition.loopDistance && stepDistance >= 0 )
                    {
                        return currentPosition;
                    }
                    else if( startLoopDistance > currentPosition.loopDistance && stepDistance < 0 )
                    {
                        return currentPosition;
                    }
                }

                previousPosition = currentPosition;
                currentPosition = GetResultAtDistance( currentPosition.distance + stepDistance );
                currentWorldDistance = Vector3.Distance( currentPosition.position, origin );

                --iterationsLeft;
                if( iterationsLeft < 0 )
                {
                    Debug.LogWarning( "Hit iterations limit of " + maxIterations + " in MoveUntilAtWorldDistance() on spline" );
                    break;
                }
            } while( currentWorldDistance < worldDistance );

            if( maxIterations - iterationsLeft > 10 )
            {
                Debug.LogWarning( "Increase step distance for better performance. Num iterations to resolve: " + (maxIterations - iterationsLeft).ToString() );
            }

            float previousWorldDistance = Vector3.Distance( previousPosition.position, origin );
            float lerpT = Mathf.InverseLerp( previousWorldDistance, currentWorldDistance, worldDistance );

            return GetResultAtDistance( currentPosition.distance - stepDistance + (stepDistance * lerpT) );
        }

        public SplineResult GetResultClosestTo(Vector3 point)
        {
            return TransformResult( curve.GetResultClosestTo( InverseTransformPoint( point ) ) );
        }

        public SplineResult GetResultClosestTo(Ray ray)
        {
            return TransformResult( curve.GetResultClosestTo( InverseTransformRay( ray ) ) );
        }

        // Editor related things
        public Transform GetTransform() => transform;
        public Component GetComponent() => this;

        public IEditableSpline GetEditableSpline() { return this; }
        public Object GetUndoObject() { return this; }
        [SerializeField] [HideInInspector] Color color = Color.white;
        public Color GetColor() { return color; }
        public void SetColor(Color newColor) { color = newColor; }
        [SerializeField] [HideInInspector] bool zTest = false;
        public bool GetZTest() { return zTest; }
        public void SetZTest(bool test) { zTest = test; }
        [SerializeField] [HideInInspector] float gizmoScale = 1;
        public float GetGizmoScale() { return gizmoScale; }
        public void SetGizmoScale(float newscale) { gizmoScale = newscale; }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if( Selection.activeObject != gameObject )
            {
                Handles.zTest = GetZTest() ? UnityEngine.Rendering.CompareFunction.LessEqual : UnityEngine.Rendering.CompareFunction.Always;
                Gizmos.matrix = transform.localToWorldMatrix;
                Handles.matrix = transform.localToWorldMatrix;
                curve.OnDrawGizmos( GetColor(), gizmoScale );
                Gizmos.matrix = Matrix4x4.identity;
                Handles.matrix = Matrix4x4.identity;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            }
        }
#endif

        public void DrawSegmentLengths()
        {
#if UNITY_EDITOR
            for( int i = 0; i < curve.SegmentCount; ++i )
            {
                Bezier3 bezier = Bezier3.Transform( curve.CalculateSegment( i ), transform );
                Vector3 pos = bezier.GetPosition( 0.5f );
                Handles.Label( pos, bezier.Length.ToString( "N2" ) );
            }
#endif
        }
    }
}