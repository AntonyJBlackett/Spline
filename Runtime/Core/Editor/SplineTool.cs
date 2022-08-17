using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
#endif

namespace FantasticSplines
{
#if UNITY_EDITOR
    // extend this class to make your own spline data editors for you
    [EditorTool( "Spline Tool", typeof( SplineComponent ) )]
    class SplineTool : EditorTool
    {
        protected SplineComponent Target
        {
            get
            {
                return target as SplineComponent;
            }
        }

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

        public override bool IsAvailable()
        {
            return Target != null;
        }

        public override void OnActivated()
        {
            base.OnActivated();
        }
    }
#endif
}