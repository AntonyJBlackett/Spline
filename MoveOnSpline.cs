using System.Collections;
using System.Collections.Generic;
using FantasticSplines;
using UnityEngine;

[ExecuteInEditMode]
public class MoveOnSpline : MonoBehaviour
{
	public SplineComponent.SplinePosition myPosition;

	public float speed;
	
	void Update()
	{
		if (Application.isPlaying)
		{
			myPosition = myPosition.Move(speed * Time.deltaTime);
		}

		if (myPosition.spline != null)
		{
			this.transform.position = myPosition.Position;
			this.transform.rotation = Quaternion.LookRotation(myPosition.Tangent, Vector3.up);
		}
	}
}
