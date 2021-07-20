using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
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
        public System.Action<int> onNodeAdded;
        public System.Action<int> onNodeRemoved;
        public System.Action onUpdated;
        public System.Action<List<Object>> onGetUndoObjects;

        [HideInInspector]
        [SerializeField]
        [FormerlySerializedAs( "curve" )]
        Spline localSpline = new Spline(); // spline in local space

        public float GetLength() { return localSpline.Length; }

        public bool IsLoop() { return localSpline.Loop; }
        public void SetLoop(bool loop)
        {
            if( localSpline.Loop != loop )
            {
                // looping and unlooping ads and removes a segment
                if( loop )
                {
                    onNodeAdded?.Invoke( GetNodeCount()+1 );
                }
                else
                {
                    onNodeRemoved( GetNodeCount() );
                }
            }

            localSpline.Loop = loop;

            onUpdated?.Invoke();
        }

        public int LoopIndex( int index )
        {
           return localSpline.LoopNodeIndex( index );
        }
        public int GetNodeCount()
        {
            return localSpline.NodeCount;
        }
        public int GetSegmentCount()
        {
            return localSpline.SegmentCount;
        }

        bool IsNodeIndexInRange(int index)
        {
            return MathsUtils.IsInArrayRange( index, GetNodeCount() );
        }

        public Vector3 InverseTransformPoint(Vector3 point)
        {
            return transform.InverseTransformPoint( point );
        }

        public Vector3 TransformPoint(Vector3 point)
        {
            return transform.TransformPoint( point );
        }

        public Vector3 InverseTransformVector(Vector3 vector)
        {
            return transform.InverseTransformVector( vector );
        }

        public Vector3 TransformVector(Vector3 vector)
        {
            return transform.TransformVector( vector );
        }

        public Vector3 InverseTransformDirection(Vector3 direction)
        {
            return transform.InverseTransformDirection( direction );
        }

        public Vector3 TransformDirection(Vector3 direction)
        {
            return transform.TransformDirection( direction );
        }

        public SplineNode TransformNode(SplineNode node)
        {
            return node.Transform( transform );
        }

        public SplineNode InverseTransformNode(SplineNode node)
        {
            return node.InverseTransform( transform );
        }

        public List<Vector3> TransformPoints(List<Vector3> points)
        {
            for( int i = 0; i < points.Count; ++i )
            {
                points[i] = TransformPoint( points[i] );
            }

            return points;
        }

        public List<SplineNode> TransformPoints(List<SplineNode> points)
        {
            for( int i = 0; i < points.Count; ++i )
            {
                points[i] = TransformNode( points[i] );
            }

            return points;
        }

        public Ray InverseTransformRay(Ray ray)
        {
            ray.origin = InverseTransformPoint( ray.origin );
            ray.direction = InverseTransformVector( ray.direction );
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
            localSpline.AddNode( node.InverseTransform( transform ) );

            onNodeAdded?.Invoke( GetNodeCount() );
            onUpdated?.Invoke();
        }
        public void PrependNode(SplineNode node)
        {
            localSpline.AddNodeAt( 0, node.InverseTransform( transform ) );

            onNodeAdded?.Invoke( 0 );
            onUpdated?.Invoke();
        }
        public void InsertNode(SplineNode node, int index)
        {
            // Add node without changing the curve
            localSpline.AddNodeAt(index, node.InverseTransform(transform));

            onNodeAdded?.Invoke( index );
            onUpdated?.Invoke();
        }
        public int InsertNode(float t)
        {
            int index = localSpline.CreateNode( t );

            onNodeAdded?.Invoke( index );
            onUpdated?.Invoke();
            return index;
        }
        public void RemoveNode(int index)
        {
            localSpline.RemoveNode( index );

            onNodeRemoved?.Invoke( index );
            onUpdated?.Invoke();
        }

        public SplineNode GetNode(int index)
        {
            if( !IsNodeIndexInRange( index ) )
            {
                return new SplineNode( transform.position, 0 );
            }

            return TransformNode( localSpline.GetNode( index ) );
        }

        public void SetNode(int index, SplineNode node)
        {
            localSpline.SetNode( index, node.InverseTransform( transform ) );
            onUpdated?.Invoke();
        }

        public int GetUpdateCount()
        {
            return localSpline.UpdateCount;
        }

        public SplineResult GetResultAtT(float t)
        {
            return TransformResult( localSpline.GetResultAtT( t ) );
        }

        public SplineResult GetResultAtDistance(float distance)
        {
            return TransformResult( localSpline.GetResultAtDistance( distance ) );
        }

        public SplineResult GetResultAtSegmentT(int segentIndex, float segmentT)
        {
            return TransformResult( localSpline.GetResultAtSegmentT( segentIndex, segmentT ) );
        }

        public SplineResult GetResultAtSegmentDistance(int segentIndex, float segmentDistance)
        {
            return TransformResult( localSpline.GetResultAtSegmentDistance( segentIndex, segmentDistance ) );
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
            return TransformResult( localSpline.GetResultClosestTo( InverseTransformPoint( point ) ) );
        }

        public SplineResult GetResultClosestToSegment( int segementIndex, Vector3 point )
        {
            return TransformResult( localSpline.GetResultClosestToSegment( segementIndex, InverseTransformPoint( point ) ) );
        }

        public SplineResult GetResultClosestTo(Ray ray)
        {
            return TransformResult( localSpline.GetResultClosestTo( InverseTransformRay( ray ) ) );
        }

        public SplineResult GetResultAtNode( int nodeIndex )
        {
            nodeIndex = LoopIndex( nodeIndex );
            return TransformResult( localSpline.GetResultAtNode( nodeIndex ) );
        }

        // Editor related things
        public Transform GetTransform() => transform;
        public Component GetComponent() => this;

        public IEditableSpline GetEditableSpline() { return this; }
        public Object[] GetUndoObjects( )
        {
            List<Object> inOutUndoObjects = new List<Object>();
            inOutUndoObjects.Add( this );
            onGetUndoObjects?.Invoke( inOutUndoObjects );
            return inOutUndoObjects.ToArray();
        }

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
                localSpline.OnDrawGizmos( GetColor(), gizmoScale );
                Gizmos.matrix = Matrix4x4.identity;
                Handles.matrix = Matrix4x4.identity;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            }
        }
#endif

        public void DrawSegmentLengths()
        {
#if UNITY_EDITOR
            for( int i = 0; i < localSpline.SegmentCount; ++i )
            {
                Bezier3 bezier = Bezier3.Transform( localSpline.CalculateSegment( i ), transform );
                Vector3 pos = bezier.GetPosition( 0.5f );
                Handles.Label( pos, bezier.Length.ToString( "N2" ) );
            }
#endif
        }

        public void DrawNodeCoordinates( Space space )
        {
#if UNITY_EDITOR
            for( int i = 0; i < localSpline.NodeCount; ++i )
            {
                SplineNode node = localSpline.GetNode(i);
                Vector3 displayPosition = space == Space.World ? TransformPoint( node.position ) : node.position;
                Vector3 guiPosition = TransformPoint( node.position );
                Vector3 offset = Vector3.right * SplineHandleUtility.GetNodeHandleSize( guiPosition ) * 0.5f;
                Handles.Label( guiPosition + offset, string.Format( "{0}{1}", space == Space.World ? "world" : "local", displayPosition.ToString( "N1" ) ) );
            }
#endif
        }
    }
}