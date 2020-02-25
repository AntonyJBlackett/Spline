using FantasticSplines;
using UnityEngine;

// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

[System.Serializable]
public struct SplineSnapshot
{
    readonly ISpline spline;
    readonly bool wasNotNull;
    readonly int updateCount;
    readonly Matrix4x4 matrix;

    public SplineSnapshot(ISpline spline)
    {
        this.spline = spline;
        wasNotNull = spline != null;
        updateCount = wasNotNull ? spline.GetUpdateCount() : 0;
        matrix = wasNotNull ? spline.GetTransform().localToWorldMatrix : Matrix4x4.identity;
    }

    public bool EqualsSnapshot(ISpline compare)
    {
        if( compare == null )
        {
            if( wasNotNull )
            {
                return false;
            }

            return true;
        }

        return updateCount == compare.GetUpdateCount() && matrix == compare.GetTransform().localToWorldMatrix;
    }

    public bool IsOutOfDate()
    {
        return !EqualsSnapshot( spline );
    }

    public bool IsDifferentFrom(ISpline compare)
    {
        return !EqualsSnapshot( compare );
    }
}