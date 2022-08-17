using UnityEditor;

namespace FantasticSplines
{
#if UNITY_EDITOR
    public class KeyframedSplineParameterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField( "How to use" );
            EditorGUILayout.HelpBox(
                "To Edit: Select the corresponding tool from the toolbar in the Scene View.\n" +
                "The Parameter Name field names the tool in the toolbar.\n" +
                "\n" +
                "Add Keyframe: " + CrossPlatformControlCommandKey() + " Click.\n" +
                "Delete Keyframe: Shift + " + CrossPlatformControlCommandKey() + " + Click\n" +
                "\n" +
                "Edit Keyframe Values: Turn on 'Enable Values Gui' in the Inspector" +
                "\n" +                "Undo/Redo: " + CrossPlatformControlCommandKey() + " + Z / " + CrossPlatformControlCommandKey() + " + Shift + Z"
                , MessageType.None, true );

            EditorGUILayout.Space();

            DrawDefaultInspector();

            static string CrossPlatformControlCommandKey()
            {
#if UNITY_EDITOR_OSX
                return "Command";
#else
            return "Control";
#endif
            }
        }
    }
#endif
}