using FantasticSplines;

[System.Serializable]
public struct SplineSnapshot
{
    readonly ISpline spline;
    readonly bool wasNotNull;
    readonly int updateCount;

    public SplineSnapshot(ISpline spline)
    {
        this.spline = spline;
        wasNotNull = spline != null;
        updateCount = wasNotNull ? spline.GetUpdateCount() : 0;
    }

    public bool IsOutOfDate()
    {
        if( wasNotNull )
        {
            if( spline == null )
            {
                return true;
            }

            return updateCount != spline.GetUpdateCount();
        }

        return false;
    }

    public bool IsDifferentFrom(ISpline compare)
    {
        return spline != compare || (compare != null && updateCount != compare.GetUpdateCount());
    }
}