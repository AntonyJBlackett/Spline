using System.Collections;
using System.Collections.Generic;
using FantasticSplines;
using UnityEngine;

[ExecuteInEditMode]
public class MoveOnSpline : MonoBehaviour
{
    public float currentDistance;
    public float speed;

    public SplineComponent spline;

    void Update()
    {
        if( Application.isPlaying )
        {
            currentDistance += speed * Time.deltaTime;
        }

        if( spline != null )
        {
            SplineResult result = spline.GetResultAtDistance( currentDistance );
            this.transform.position = result.position;
            this.transform.rotation = Quaternion.LookRotation( result.tangent, Vector3.up );
        }
    }
}
