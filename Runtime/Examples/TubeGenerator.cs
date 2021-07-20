using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FantasticSplines;

[ExecuteInEditMode]
public class TubeGenerator : MonoBehaviour
{
    public ISpline spline;

    public bool regenerate = false;
    public bool autoRegenerate = true;

    Mesh mesh;

    void Start()
    {
        mesh = new Mesh();
    }

    int lastUpdate = 0;
    void OnDrawGizmos()
    {
        if( autoRegenerate || regenerate )
        {
            int update = spline.GetUpdateCount();
            if( update != lastUpdate || regenerate )
            {
                regenerate = false;
                lastUpdate = update;

                GenerateTube();
            }
        }
    }

    void GenerateTube()
    {
        mesh.Clear();
       //Vector3 points = 
    }
}
