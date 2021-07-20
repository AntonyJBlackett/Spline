using FantasticSplines;
using UnityEngine;

// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

[System.Serializable]
public struct SplineSnapshot
{
    [SerializeField] ISpline spline;
    [SerializeField] bool wasNotNull;
    [SerializeField] int updateCount;
    [SerializeField] Matrix4x4 matrix;

    public SplineSnapshot(ISpline spline)
    {
        this.spline = spline;
        wasNotNull = spline != null;
        updateCount = spline != null ? spline.GetUpdateCount() : 0;
        matrix = spline != null ? spline.GetTransform().localToWorldMatrix : Matrix4x4.identity;
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