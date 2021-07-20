using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;

namespace FantasticSplines
{
    // Tagging a class with the EditorTool attribute and no target type registers a global tool. Global tools are valid for any selection, and are accessible through the top left toolbar in the editor.
    [EditorTool("Spline Float Tool", typeof( SplineFloat ) )]
    class SplineFloatTool : KeyframedSplineParameterTool<float>
    {
        #region Tool properties and initialisation
        // Serialize this value to set a default value in the Inspector.
        [SerializeField]
        Texture2D m_ToolIcon;

        GUIContent m_IconContent;

        void OnEnable()
        {
            m_IconContent = new GUIContent()
            {
                image = m_ToolIcon,
                text = "Spline Float Tool",
                tooltip = "Spline Float Tool",
            };
        }

        public override GUIContent toolbarIcon
        {
            get { return m_IconContent; }
        }
        #endregion


        #region Custom tool handles
        #endregion
    }
}