using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
#endif

namespace FantasticSplines
{
    // Tagging a class with the EditorTool attribute and no target type registers a global tool. Global tools are valid for any selection, and are accessible through the top left toolbar in the editor.
#if UNITY_EDITOR

    [CustomEditor( typeof( SplineColor ) )]
    public class SplineColorEditor : KeyframedSplineParameterEditor
    {
    }

    [EditorTool("Spline Color Tool", typeof( SplineColor ) )]
    class SplineColorTool : KeyframedSplineParameterTool<Color>
    {
    }
#endif
}