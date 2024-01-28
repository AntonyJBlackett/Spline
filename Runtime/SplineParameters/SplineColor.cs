using UnityEngine;
using System.Collections.Generic;
using FantasticSplines;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Spline Keyframe track that stores and blends colors along a spline
public class SplineColor : KeyframedSplineParameter<Color>
{
    SplineColor() : base()
    {
        parameterName = "Spline Color";
    }

    #region SplineDataTrack specialisation
    public override Color GetDefaultKeyframeValue()
    {
        return Color.white;
    }
    #endregion
}