using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
#endif

#if UNITY_EDITOR
namespace FantasticSplines
{
    class SplineKeyFrameTool : EditorTool
    {
    }

    class KeyframedSplineParameterTool<T> : SplineKeyFrameTool, IDrawSelectedHandles
        where T : new()
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
                text = Target.parameterName + " Tool",
                tooltip = Target.parameterName + " Tool",
            };
        }

        public override GUIContent toolbarIcon
        {
            get { return m_IconContent; }
        }
        #endregion

        protected KeyframedSplineParameter<T> Target
        {
            get
            {
                return target as KeyframedSplineParameter<T>;
            }
        }

        bool AddKeyModifiers( Event guiEvent )
        {
#if UNITY_EDITOR_OSX
            return guiEvent.command && !guiEvent.shift;
#else
            return guiEvent.control && !guiEvent.shift;
#endif
        }

        bool RemoveKeyModifiers( Event guiEvent )
        {
#if UNITY_EDITOR_OSX
            return guiEvent.command && guiEvent.shift;
#else
            return guiEvent.control && guiEvent.shift;
#endif
        }

        public override bool IsAvailable()
        {
            return Target != null && Target.spline != null;
        }

        protected bool DoKeyframeToolHandles()
        {
            if( !Target.enableKeyframeHandles )
            {
                return false;
            }

            bool addKey = AddKeyModifiers( Event.current );
            bool removeKey = RemoveKeyModifiers( Event.current );

            if( addKey )
            {
                DoAddKeyframe();
                Selection.activeObject = Target.gameObject;
                return true;
            }
            else if( removeKey )
            {
                DoRemoveKeyframe();
                Selection.activeObject = Target.gameObject;
                return true;
            }
            else
            {
                return DoSplineKeyframeHandle();
            }
        }

        public static void KeyframeHandleCap( int controlId, Vector3 position, Quaternion rotation, float size, EventType eventType )
        {
            Camera camera = SceneView.currentDrawingSceneView.camera;
            Vector3 handleForward = (position - camera.transform.position).normalized;
            if( camera.orthographic )
            {
                handleForward = camera.transform.forward;
            }
            Quaternion handleRotation = Quaternion.AngleAxis( 45, handleForward ) * Quaternion.LookRotation( handleForward, camera.transform.up );

            using( new Handles.DrawingScope( Color.black ) )
            {
                Handles.CubeHandleCap( controlId, position, handleRotation, size * 1.3f, eventType ); // outline
            }
            Handles.CubeHandleCap( controlId, position, handleRotation, size, eventType ); // fill
        }

        public static void KeyframeTangentCap( int controlId, Vector3 position, Quaternion rotation, float size, EventType eventType )
        {
            Camera camera = SceneView.currentDrawingSceneView.camera;
            Vector3 handleForward = (position - camera.transform.position).normalized;
            if( camera.orthographic )
            {
                handleForward = camera.transform.forward;
            }
            Quaternion handleRotation = Quaternion.LookRotation( handleForward, camera.transform.up );

            using( new Handles.DrawingScope( Color.black ) )
            {
                Handles.SphereHandleCap( controlId, position, handleRotation, size * 1.3f, eventType ); // outline
            }
            Handles.SphereHandleCap( controlId, position, handleRotation, size, eventType ); // fill
        }

        public static float GetHandleSize( Vector3 position )
        {
            return SplineHandleUtility.GetHandleSize( position );
        }

        void DoAddKeyframe()
        {
            using( new Handles.DrawingScope( InactiveColor ) )
            {
                Ray ray = HandleUtility.GUIPointToWorldRay( Event.current.mousePosition );
                SplineResult result = Target.spline.GetResultClosestTo( ray );

                float size = GetHandleSize( result.position );
                bool click = Handles.Button( result.position, Quaternion.identity, size, size, KeyframeHandleCap );
                click = click || Handles.Button( ray.origin, Quaternion.identity, 0, GetHandleSize( ray.origin ) , KeyframeHandleCap );

                if( click )
                {
                    Undo.RecordObject( Target, "Add Spline Keyframe" );

                    T interpolatedValue = Target.GetValueAt( result.distance, Target.GetDefaultKeyframeValue() );
                    Target.Insert( interpolatedValue, result.distance );
                }
            }
        }

        void DoRemoveKeyframe()
        {
            using( new Handles.DrawingScope( DeleteColor ) )
            {
                var keys = Target.Keyframes;
                for( int i = 0; i < keys.Count; ++i )
                {
                    float size = GetHandleSize( keys[i].location.position );
                    bool click = Handles.Button( keys[i].location.position, Quaternion.identity, size, size, KeyframeHandleCap );
                    if( click )
                    {
                        Undo.RecordObject( Target, "Delete Spline Keyframe" );
                        Target.RemoveAt( i );
                        return;
                    }
                }
            }
        }

        public static Color ActiveColor
        {
            get
            {
                return new Color( 0.3411765f, 0.5215687f, 0.8509805f );
            }
        }

        public static Color InactiveColor
        {
            get
            {
                return new Color( 0.6f, 0.6f, 0.6f );
            }
        }

        public static Color DeleteColor
        {
            get
            {
                return new Color( 0.8509805f, 0.3411765f, 0.3411765f );
            }
        }

        public static Color EditorColor
        {
            get
            {
                Color color = EditorGUIUtility.isProSkin
             ? (Color)new Color32( 56, 56, 56, 255 )
             : (Color)new Color32( 194, 194, 194, 255 );

                return color;
            }
        }

        bool DoSplineKeyframeHandle()
        {
            bool mouseOverKeyframe = false;
            var keys = Target.Keyframes;
            using( new Handles.DrawingScope( ActiveColor ) )
            {
                for( int i = 0; i < keys.Count; ++i )
                {
                    if( DoKeyframeMoveHandle( i, keys[i] ) )
                    {
                        mouseOverKeyframe = true;
                    }
                    DrawKeyframeValueGizmo(keys[i]);
                }
            }
            return mouseOverKeyframe;
        }

        protected virtual void DrawKeyframeValueGizmo(SplineParameterKeyframe<T> key)
        {
            // override me to draw special gizmos for visualising data
        }

        protected virtual void DrawInterpolatedGizmos()
        {
            // override me to draw special gizmos for visualising data
        }

        bool DoKeyframeMoveHandle( int keyFrameIndex, SplineParameterKeyframe<T> key )
        {
            bool interacted = false;

            Ray ray = HandleUtility.GUIPointToWorldRay( Event.current.mousePosition );

            float handleSize = GetHandleSize( key.location.position );
            var handleOffset = SplineDistance.Zero;
            SplineResult resultAfter = Target.spline.GetResultAt( Target.GetKeyframe( keyFrameIndex ).location.distance + handleOffset );

            float minHandleLength = handleSize * 1.5f;
            Vector3 outHandleOrigin = resultAfter.position + resultAfter.tangent.normalized * minHandleLength;
            Vector3 inHandleOrigin = resultAfter.position - resultAfter.tangent.normalized * minHandleLength;
            Vector3 outHandlePosition = outHandleOrigin + resultAfter.tangent.normalized * key.outTangent;
            Vector3 inHandlePosition = inHandleOrigin - resultAfter.tangent.normalized * key.inTangent;

            if( Target.EnableKeyframeTangents )
            {
                using( new Handles.DrawingScope( Color.black ) )
                {
                    Handles.DrawLine( outHandlePosition, inHandlePosition, 1 );
                }
            }

            EditorGUI.BeginChangeCheck();
            // free move handle is used to check drag input. We'll recalculate the position ourselves with the mouse position
            Handles.FreeMoveHandle( resultAfter.position, handleSize, Vector3.zero, KeyframeHandleCap );
            if( EditorGUI.EndChangeCheck() )
            {
                interacted = true;
                Undo.RecordObject( Target, "Move Spline Keyframe" );
                Target.SetKeyframeLocation( keyFrameIndex, Target.spline.GetResultAt( Target.spline.GetResultClosestTo( ray ).distance - handleOffset ) );
            }

            if( Target.EnableKeyframeTangents )
            {
                EditorGUI.BeginChangeCheck();
                Vector3 outTangent = Handles.Slider( outHandlePosition, resultAfter.tangent, handleSize * 0.75f, KeyframeTangentCap, 0 ) - outHandleOrigin;
                Vector3 inTangent = Handles.Slider( inHandlePosition, resultAfter.tangent, handleSize * 0.75f, KeyframeTangentCap, 0 ) - inHandleOrigin;
                if( EditorGUI.EndChangeCheck() )
                {
                    interacted = true;
                    if( Vector3.Dot( outTangent, resultAfter.tangent ) < 0 )
                    {
                        outTangent = Vector3.zero;
                    }
                    if( Vector3.Dot( inTangent, resultAfter.tangent ) > 0 )
                    {
                        inTangent = Vector3.zero;
                    }

                    key.outTangent = outTangent.magnitude;
                    key.inTangent = inTangent.magnitude;

                    key.outTangent = Mathf.Clamp( key.outTangent, 0, 1 );
                    key.inTangent = Mathf.Clamp( key.inTangent, 0, 1 );
                    Undo.RecordObject( Target, "Edit Spline Keyframe Tangent" );
                    Target.SetKeyframe( keyFrameIndex, key );
                }
            }

            return interacted;
        }

        bool IsActive => ToolManager.IsActiveTool(this);

        public void OnDrawHandles()
        {
            var keys = Target.Keyframes;
            foreach(var key in keys)
            {
                float handleSize = GetHandleSize(key.location.position);
                using(new Handles.DrawingScope(KeyframedSplineParameterTool<Vector3>.InactiveColor))
                {
                    KeyframedSplineParameterTool<Vector3>.KeyframeHandleCap(0, key.location.position, Quaternion.identity, handleSize, EventType.Repaint);
                }
            }

            // enable tool when selecting a gizmo
            if(DoSplineKeyframeHandle())
            {
                ToolManager.SetActiveTool(this);
                Event.current.Use();
            }

            if( Target.enableVisualisation )
            {
                DrawInterpolatedGizmos();
            }
        }

        // This is called for each window that your tool is active in. Put the functionality of your tool here.
        public override void OnToolGUI( EditorWindow window )
        {
            Handles.zTest = Target.spline.zTest ? UnityEngine.Rendering.CompareFunction.LessEqual : UnityEngine.Rendering.CompareFunction.Always;

            bool keepActive = false;
            if( Target.enableKeyframeHandles )
            {
                if( DoToolHandles( window ) )
                {
                    keepActive = true;
                }
            }
            if( Target.enableValuesGui )
            {
                if( DoToolGUI( window ) )
                {
                    keepActive = true;
                }
            }

            if( keepActive )
            {
                Selection.activeObject = Target.gameObject;
            }
        }

        protected virtual float GetGUIPropertyWidth()
        {
            return 50;
        }

        protected virtual bool DoToolHandles( EditorWindow window )
        {
            return DoKeyframeToolHandles();
        }

        protected virtual bool DoToolGUI( EditorWindow window )
        {
            SerializedObject so = new SerializedObject( Target );
            var rawKeysSP = so.FindProperty( "rawKeyframes" );
            var keys = Target.Keyframes;

            if( keys.Count > 0 )
            {
                float handleSize = GetHandleSize( keys[0].location.position );

                for( int i = 0; i < keys.Count; ++i )
                {
                    SerializedProperty keySP = rawKeysSP.GetArrayElementAtIndex( i );
                    SerializedProperty keyValue = keySP.FindPropertyRelative( "value" );
                    float propertyHeight = EditorGUI.GetPropertyHeight( keyValue, new GUIContent( "" ), true );

                    Vector2 guiPosition = HandleUtility.WorldToGUIPoint( keys[i].location.position ) + new Vector2( 0.5f, 0.5f ) * handleSize;
                    Vector2 rectSize = new Vector2( GetGUIPropertyWidth(), propertyHeight );
                    guiPosition.y += propertyHeight * 0.5f;
                    Vector2 border = new Vector2( 2, 2 );
                    Rect guiRect = new Rect( guiPosition, rectSize + border * 2 );

                    Handles.BeginGUI();
                    GUILayout.BeginArea( guiRect );

                    GUISkin skin = EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector );
                    EditorGUI.DrawRect( new Rect( Vector2.zero, rectSize + border * 2 ), EditorColor );

                    GUILayout.BeginArea( new Rect( border, rectSize ) );

                    EditorGUILayout.PropertyField( keyValue, new GUIContent(), true );
                    GUILayout.EndArea();

                    GUILayout.EndArea();
                    Handles.EndGUI();

                    // hacks: intercept unity scene view controls
                    if( guiRect.Contains( Event.current.mousePosition ) )
                    {
                        Ray ray = HandleUtility.GUIPointToWorldRay( Event.current.mousePosition );
                        Handles.Button( ray.origin + ray.direction, Camera.current.transform.rotation, 0, GetHandleSize( ray.origin + ray.direction ), Handles.DotHandleCap );
                    }
                }
            }

            bool keepActive = false;
            if( so.hasModifiedProperties )
            {
                so.ApplyModifiedProperties();
                keepActive = true;
            }

            return keepActive;
        }
    }
}
#endif