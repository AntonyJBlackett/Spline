using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FantasticSplines
{
    [System.Serializable]
    public struct SegmentPosition
    {
        [SerializeField] private int _index;
        [SerializeField] private float _segmentT;

        public int index { get { return _index; } }
        public float segmentT { get { return _segmentT; } }
        
        public SegmentPosition(int index, float segmentT)
        {
            this._index = index; 
            this._segmentT = segmentT; 
        }
    }
    
    [System.Serializable]
    public struct SplinePosition
    {
        public SplineComponent spline;
        public SegmentPosition segmentPosition;
        
        public float DistanceOnSpline => spline.GetDistanceOnSpline(segmentPosition);
        public Vector3 Position => spline.GetPositionAtDistance(DistanceOnSpline);
        public Vector3 Tangent => spline.GetPositionAtDistance(DistanceOnSpline);

        public SplinePosition(SplineComponent spline, float distance)
        {
            this.spline = spline;
            this.segmentPosition = spline.GetSegmentAtDistance(distance);
        }

        public SplinePosition Move(float stepDistance)
        {
            return new SplinePosition(spline, DistanceOnSpline + stepDistance);
        }
    }
}