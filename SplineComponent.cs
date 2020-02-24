using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FantasticSplines;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;
#endif

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
            return MathHelper.IsInArrayRange( index, GetNodeCount() );
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

        public SplineResult GetResultAtT(float t)
        {
            return TransformResult( curve.GetResultAtDistance( t * curve.Length ) );
        }

        public SplineResult GetResultAtDistance(float distance)
        {
            return TransformResult( curve.GetResultAtDistance( distance ) );
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
            if( maxIterations > 10 )
            {
                Debug.LogWarning( "Increase step distance for better performance." );
            }

            int iterationsLeft = maxIterations;

            SplineResult currentPosition = GetResultAtDistance( startDistance );
            SplineResult previousPosition = currentPosition;
            Vector3 origin = currentPosition.position;
            float worldDistanceTest = 0;

            do
            {
                // early out, there's no more spline
                if( currentPosition.AtEnd && stepDistance > 0 )
                {
                    return currentPosition;
                }
                if( currentPosition.AtStart && stepDistance < 0 )
                {
                    return currentPosition;
                }

                previousPosition = currentPosition;
                currentPosition = GetResultAtDistance( currentPosition.distance + stepDistance );
                worldDistanceTest = Vector3.Distance( currentPosition.position, origin );

                --iterationsLeft;
                if( iterationsLeft < 0 )
                {
                    Debug.LogWarning( "Hit iterations limit of " + maxIterations + " in MoveUntilAtWorldDistance() on spline" );
                    break;
                }
            } while( worldDistanceTest < worldDistance );

            float lastWorldDistanceTest = Vector3.Distance( previousPosition.position, origin );
            float lerpT = Mathf.InverseLerp( lastWorldDistanceTest, worldDistanceTest, worldDistance );

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
        [SerializeField] [HideInInspector] Color color;
        public Color GetColor() { return color; }
        public void SetColor(Color newColor) { color = newColor; }
        [SerializeField] [HideInInspector] bool zTest = false;
        public bool GetZTest() { return zTest; }
        public void SetZTest(bool test) { zTest = test; }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if( Selection.activeObject != gameObject )
            {
                Handles.zTest = GetZTest() ? UnityEngine.Rendering.CompareFunction.LessEqual : UnityEngine.Rendering.CompareFunction.Always;
                for( int i = 0; i < curve.SegmentCount; ++i )
                {
                    Bezier3 bezier = Bezier3.Transform( curve.CalculateSegment( i ), transform );

                    Handles.DrawBezier( bezier.start, bezier.end, bezier.B, bezier.C, GetColor() * .9f, null,
                        2f );
                }

                // this stops selection of the spline when we're doing other things.
                if( Selection.activeObject == null )
                {
                    Gizmos.color = Color.white;
                    for( int i = 0; i < GetNodeCount(); ++i )
                    {
                        Gizmos.DrawSphere( GetNode( i ).position, 0.05f );
                    }
                }
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