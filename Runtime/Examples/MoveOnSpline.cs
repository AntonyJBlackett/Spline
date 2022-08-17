
using FantasticSplines;
using UnityEngine;

// Authors: Antony Blackett
// For more info contact me at: antony@fantasticfoundry.com
// (C) copyright Fantastic Foundry Limited 2020, New Zealand

[ExecuteInEditMode]
public class MoveOnSpline : MonoBehaviour
{
    public SplineDistance currentDistance;
    public SplineDistance speed;

    public SplineComponent spline;

    void Update()
    {
        if( Application.isPlaying )
        {
            currentDistance += speed * Time.deltaTime;
        }

        if( spline != null )
        {
            SplineResult result = spline.GetResultAt( currentDistance );
            this.transform.position = result.position;
            this.transform.rotation = Quaternion.LookRotation( result.tangent, Vector3.up );
        }
    }
}
