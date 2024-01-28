using UnityEngine;
using System.Collections.Generic;
using FantasticSplines;

// Spline Keyframe track that stores and blends colors along a spline
public class SplineScale2 : KeyframedSplineParameter<Vector2>
{
    SplineScale2() : base()
    {
        parameterName = "Spline Scale";
    }

    #region SplineDataTrack specialisation
    public override Vector2 GetDefaultKeyframeValue()
    {
        return Vector2.one;
    }
    #endregion
}