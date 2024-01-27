using System;
using UnityEngine;
using MacFsWatcher;
using System.Numerics;
using UnityEngine.UIElements;
using UnityEditor.PackageManager.UI;



#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
#endif

namespace FantasticSplines
{
    // extend this class to make your own spline data editors
    [EditorTool( "Spline Tool", typeof( SplineComponent ) )]
    class SplineTool : EditorTool, IDrawSelectedHandles
    {
        [SerializeField]
        Texture2D m_ToolIcon;

        GUIContent m_IconContent;
        void OnEnable()
        {
            m_IconContent = new GUIContent()
            {
                image = m_ToolIcon,
                text = "Spline Tool",
                tooltip = "Spline Tool"
            };
        }

        public override GUIContent toolbarIcon
        {
            get { return m_IconContent; }
        }

        public override void OnActivated()
        {
            base.OnActivated();
            SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Spline Tool Activated"), .3f);
        }

        // Called before the active tool is changed, or destroyed. The exception to this rule is if you have manually
        // destroyed this tool (ex, calling `Destroy(this)` will skip the OnWillBeDeactivated invocation).
        public override void OnWillBeDeactivated()
        {
            SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Spline Tool Deactivated"), .3f);
        }

        bool IsActive => ToolManager.IsActiveTool(this);

        public void OnDrawHandles()
        {
            // enable tool when selecting a gizmo
            if(!IsActive && Event.current.type == EventType.MouseDown)
            {
                if (SplineEditor.DetectClickSelection(target as IEditableSpline, Event.current).Detected)
                {
                    ToolManager.SetActiveTool(this);
                    SplineEditor.DoSceneViewInput(target as SplineComponent, Event.current);
                    Event.current.Use();
                }
            }
        }

        // Equivalent to Editor.OnSceneGUI.
        public override void OnToolGUI(EditorWindow window)
        {
            if (EditorWindow.focusedWindow != window && EditorWindow.mouseOverWindow == window)
            {
                window.Focus();
            }

            SplineEditor.DoSceneViewDraw(target as SplineComponent, Event.current);

            if (SplineEditor.ShouldDisableTool(target as SplineComponent, Event.current))
            {
                Event.current.Use();
                ToolManager.RestorePreviousTool();
                return;
            }
            SplineEditor.DoSceneViewInput(target as SplineComponent, Event.current);

            window.Repaint();
        }
    }
}