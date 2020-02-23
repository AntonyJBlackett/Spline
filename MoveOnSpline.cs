using System.Collections;
using System.Collections.Generic;
using FantasticSplines;
using UnityEngine;

[ExecuteInEditMode]
public class MoveOnSpline : MonoBehaviour
{
	public LiveSplinePosition livePosition = new LiveSplinePosition();

	public float speed;

    private void OnDrawGizmos()
    {
        UpdatePosition();
    }

    private void OnEnable()
    {
        livePosition.Initialise();
    }

    void Update()
    {
        if( Application.isPlaying )
        {
            livePosition.Move( speed * Time.deltaTime );
        }
        UpdatePosition();
    }

    void UpdatePosition()
    {
		if( livePosition.IsValid )
		{
			this.transform.position = livePosition.Position;
			this.transform.rotation = Quaternion.LookRotation(livePosition.Tangent, Vector3.up);
		}
	}
}
