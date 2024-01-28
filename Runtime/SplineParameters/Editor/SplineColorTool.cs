using System;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
#endif

namespace FantasticSplines
{
    // Tagging a class with the EditorTool attribute and no target type registers a global tool. Global tools are valid for any selection, and are accessible through the top left toolbar in the editor.
#if UNITY_EDITOR

    [EditorTool("Spline Color Tool", typeof( SplineColor ) )]
    class SplineColorTool : KeyframedSplineParameterTool<Color>
    {
        protected override void DrawKeyframeValueGizmo(SplineParameterKeyframe<Color> keyframe)
        {
            float size = GetHandleSize(keyframe.location.position);
            Handles.color = Color.black;
            Vector3 up = SceneView.currentDrawingSceneView.camera.transform.up;
            Handles.DrawLine(keyframe.location.position, keyframe.location.position + up * size * 2.5f, 2);
            Handles.DrawSolidDisc(keyframe.location.position + up * size * 2.5f, SceneView.currentDrawingSceneView.camera.transform.forward, size * 1.2f * 0.75f);
            Handles.color = keyframe.value;
            Handles.DrawSolidDisc(keyframe.location.position + up * size * 2.5f, SceneView.currentDrawingSceneView.camera.transform.forward, size * 0.75f);
        }

        List<SplineDistance> pointDistances = new List<SplineDistance>();
        protected override void DrawInterpolatedGizmos()
        {
            var spline = Target.spline;

            int nodeCount = spline.NodeCount;
            pointDistances.Clear();
            for(int i = 0; i < nodeCount; ++i)
            {
                if(i == 0 && !spline.IsLoop)
                {
                    continue;
                }

                SplineResult start = spline.GetResultAtNode(i - 1);
                SplineResult end = spline.GetResultAtNode(i);

                if(i == 0)
                {
                    end.distance += end.length;
                }

                int segments = 10;
                for(int s = 0; s < segments; ++s)
                {
                    pointDistances.Add(SplineDistance.Lerp(start.distance, end.distance, Mathf.InverseLerp(0, segments - 1, s)));
                }
            }

            var keys = Target.Keyframes;
            for(int i = 0; i < keys.Count; ++i)
            {
                pointDistances.Add(keys[i].location.distance);
            }

            pointDistances.Sort((a, b) => { return a.CompareTo(b); });

            for(int i = 1; i < pointDistances.Count; ++i)
            {
                SplineResult start = spline.GetResultAt(pointDistances[i - 1]);
                SplineResult end = spline.GetResultAt(pointDistances[i]);

                Handles.color = Target.GetValueAt((start.distance + end.distance) * 0.5f,Target.GetDefaultKeyframeValue());
                Handles.DrawLine(start.position, end.position, 5);
            }
        }
    }
#endif
}