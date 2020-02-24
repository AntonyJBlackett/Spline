using FantasticSplines;

[System.Serializable]
public struct SplineChangeDetector
{
    readonly ISpline spline;
    readonly bool WasNotNull;
    readonly int updateCount;

    public SplineChangeDetector(ISpline spline)
    {
        this.spline = spline;
        WasNotNull = spline != null;
        updateCount = WasNotNull ? spline.GetUpdateCount() : 0;
    }

    public bool HasChanged()
    {
        if( WasNotNull )
        {
            if( spline == null )
            {
                return true;
            }

            return updateCount != spline.GetUpdateCount();
        }
        else // spline was null
        {
            return spline != null;
        }
    }

    public bool IsDifferentFrom(ISpline compare)
    {
        return spline != compare || (compare != null && updateCount != compare.GetUpdateCount());
    }
}