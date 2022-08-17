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
    }
#endif
}