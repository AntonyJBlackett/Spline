using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
#endif

namespace FantasticSplines
{
#if UNITY_EDITOR

    [CustomEditor( typeof( SplineFloat ) )]
    public class SplineFloatEditor : KeyframedSplineParameterEditor
    {
    }

    // Tagging a class with the EditorTool attribute and no target type registers a global tool. Global tools are valid for any selection, and are accessible through the top left toolbar in the editor.
    [EditorTool("Spline Float Tool", typeof( SplineFloat ) )]
    class SplineFloatTool : KeyframedSplineParameterTool<float>
    {
        [Range( 1, 100 )]
        public int visualisationSamples = 50;

        protected override void DrawKeyframeValueGizmo(SplineParameterKeyframe<float> keyframe)
        {
            Gizmos.color = Color.white;
            float radius = keyframe.value;
            Vector3 right = Vector3.Cross(keyframe.location.tangent, Vector3.up).normalized * radius;
            Gizmos.DrawLine(keyframe.location.position - right * 0.5f, keyframe.location.position + right * 0.5f);
        }

        protected override void DrawInterpolatedGizmos()
        {
            var spline = Target.spline;
            var distance = SplineDistance.Zero;
            var length = spline.Length;
            var step = length / visualisationSamples;
            while(distance < length)
            {
                SplineResult location = spline.GetResultAt(distance);
                Gizmos.color = Color.white;
                float radius = Target.GetValueAt(location.distance, Target.GetDefaultKeyframeValue());
                Vector3 right = Vector3.Cross(location.tangent, Vector3.up).normalized * radius;
                Gizmos.DrawLine(location.position - right * 0.5f, location.position + right * 0.5f);
                distance += step;
            }
        }
    }
#endif
}