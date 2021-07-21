using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FantasticSplines
{
    public static class SplineProcessor
    {
        public static void Sort( ref List<SplineResult> results )
        {
            results.Sort(
                ( a, b ) =>
                {
                    int sort = a.lapCount.CompareTo( b.lapCount );
                    if( sort != 0 )
                    {
                        return sort;
                    }

                    sort = a.segmentResult.index.CompareTo( b.segmentResult.index );
                    if( sort != 0 )
                    {
                        return sort;
                    }

                    return a.segmentResult.distance.CompareTo( b.segmentResult.distance );
                } );
        }

        public static void AddResultsAtNodes( ISpline spline, ref List<SplineResult> results )
        {
            int nodeCount = spline.GetNodeCount();
            for( int i = 0; i < nodeCount; i++ )
            {
                int nodeIndex = spline.LoopIndex( i );

                SplineNode node = spline.GetNode( nodeIndex );
                SplineResult result = spline.GetResultAtNode( nodeIndex );
                if( nodeIndex != 0 )
                {
                    if( node.NodeType == NodeType.Point || node.NodeType == NodeType.Free )
                    {
                        results.Add( spline.GetResultAtSegmentT( result.segmentResult.index - 1, 1 ) ); // in node
                    }
                }

                results.Add( result ); // out node
            }

            if( spline.IsLoop() )
            {
                results.Add( spline.GetResultAtSegmentT( nodeCount-1, 1 ) ); // out node of loop segment
            }

            Sort( ref results );
        }

        public static void AddResultsAtKeys<T>( KeyframedSplineParameter<T> keyframedSplineParameter, ref List<SplineResult> results ) where T : new()
        {
            var keys = keyframedSplineParameter.OrderedKeyframes;
            for( int i = 0; i < keys.Count; i++ )
            {
                results.Add( keys[i].location );
            }
            Sort( ref results );
        }

        public static void AddPointsAtDistanceApart( ISpline spline, float stepDistance, ref List<SplineResult> results )
        {
            int steps = Mathf.FloorToInt( spline.GetLength() / stepDistance ) + 2;
            for( int i = 0; i < steps; i++ )
            {
                float distance = Mathf.Clamp( i * stepDistance, 0, spline.GetLength() );
                results.Add( spline.GetResultAtDistance( distance ) );
            }
            Sort( ref results );
        }

        public static void AddSplitSegmentsByInterval( ISpline spline, float stepDistance, ref List<SplineResult> results )
        {
            int nodeCount = results.Count;
            for( int i = 1; i < nodeCount; ++i )
            {
                float segmentLength = results[i].distance - results[i-1].distance;
                int segmentDivisions = Mathf.FloorToInt( segmentLength / stepDistance );
                if( segmentDivisions > 1 )
                {
                    float segmentStep = segmentLength / segmentDivisions;
                    for( int s = 1; s < segmentDivisions; ++s )
                    {
                        float segmentDistance = segmentStep * s;
                        results.Add( spline.GetResultAtDistance( results[i - 1].distance + segmentDistance ) );
                    }
                }
            }
            Sort( ref results );
        }

        public static void AddPointsByTollerance( ref List<SplineResult> results, ISpline spline, float minStepDistance, System.Func<SplineResult,SplineResult,bool> tolleranceFunction )
        {
            int resultCount = results.Count;
            for( int i = 1; i < resultCount; ++i )
            {
                int index = i;
                int previousIndex = i - 1;

                float segmentLength = results[index].distance - results[previousIndex].distance;
                int segmentDivisions = Mathf.FloorToInt( segmentLength / minStepDistance );
                if( segmentDivisions > 1 )
                {
                    SplineResult previousResult = results[previousIndex];
                    float segmentStep = segmentLength / segmentDivisions;
                    for( int s = 1; s < segmentDivisions; ++s )
                    {
                        float segmentDistance = segmentStep * s;
                        SplineResult result = spline.GetResultAtDistance( results[previousIndex].distance + segmentDistance );

                        if( tolleranceFunction( previousResult, result ) )
                        {
                            results.Add( result );
                            previousResult = result;
                        }
                    }
                }
            }
            Sort( ref results );
        }
    }
}
