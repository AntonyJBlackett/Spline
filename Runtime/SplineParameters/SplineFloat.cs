using UnityEngine;
using System.Collections.Generic;
using FantasticSplines;


// Spline Keyframe track that stores and blends colors along a spline
public class SplineFloat : KeyframedSplineParameter<float>
{
    SplineFloat() : base()
    {
        parameterName = "Spline Float";
    }

    #region SplineDataTrack specialisation
    public override float GetDefaultKeyframeValue()
    {
        return 0;
    }
    #endregion
}