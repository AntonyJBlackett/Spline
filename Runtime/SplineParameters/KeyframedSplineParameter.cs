using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
#endif

namespace FantasticSplines
{
    // Some data at some point along the spline
    [System.Serializable]
    public struct SplineParameterKeyframe<T>
    {
        public float inTangent;
        public float outTangent;
        public SplineResult location;
        public T value;

        public SplineParameterKeyframe( T value, SplineResult location, float inT, float outT )
        {
            this.value = value;
            this.location = location;
            inTangent = inT;
            outTangent = outT;
        }
    }

    public enum KeyframeInterpolationModes
    {
        Linear,
        SmoothStep,
        Tangents,
        Constant
    }

    // A collection of data points along a spline
    [System.Serializable]
    [ExecuteInEditMode]
    public class KeyframedSplineParameter<T> : MonoBehaviour, ISplineParameter<T> where T : new()
    {
#if UNITY_EDITOR
        // Active KeyframedSplineParameter<T> instance being edited by the
        // KeyframedSplineParameter<T> tool
        static KeyframedSplineParameter<T> editInstance;
        public KeyframedSplineParameter<T> EditInstance
        {
            get
            {
                if( editInstance == null )
                {
                    editInstance = this;
                }
                return editInstance;
            }
        }

        // Button displayed in the inspector to quickly select the
        // Correct editor tool for this KeyframedSplineParameter<T>
        [InspectorButton( "ToggleEditor" )]
        public bool toggleEditor = false;
        public void ToggleEditor()
        {
            if( ToolActive )
            {
                editInstance = null;
                SplineEditor.EditorActive = true;
                ToolManager.RestorePreviousPersistentTool();
                EditorApplication.QueuePlayerLoopUpdate();
            }
            else
            {
                editInstance = this;
                SplineEditor.EditorActive = false;
                ToolManager.SetActiveTool( GetToolType() );
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        // The tool type used to edit this KeyframedSplineParameter<T>
        protected virtual System.Type GetToolType() { Debug.LogError( "You must override GetToolType() and supply the correct tool type." ); return typeof( SplineParameterKeyframe<T> ); }
#endif

        public string parameterName = "";
        public int ParameterNameHash
        {
            get; private set;
        }

        // Event called when a key is moved or the value is changed.
        public System.Action onKeyframesChanged; // called when a key is moved or the value is changed.

        // Event called when the spline is moved or edited
        public System.Action onSplineChanged; 

        // Spline that keyframes are attached to. Often refered to as the 'parent spline'.
        public SplineComponent spline;
        // Spline reference used to detect if the spline component reference has changed
        SplineComponent lastSimple; 

        // Keyframe positioning behaviour to handle when the parent spline is edited.
        public RepositionMode keyRepositionMode = RepositionMode.SegmentT;

        // Paramters to enable and disable editor functionality and gizmos.
        [Header( "Handles" )]
        public bool KeepEditorActive = false;
        public bool enableKeyframeHandles = true;
        public bool enableValuesGui = false;

        [Header( "Interpolation" )]
        public KeyframeInterpolationModes keyframeInterpolationMode = KeyframeInterpolationModes.Linear;


        // A function that when set is used to override the default interpolator.
        // Set this to handle interpolation of any arbitrary type or to interpolate in a different way, such as Mathf.SmoothStep()
        public System.Func<ISpline, SplineParameterKeyframe<T>, SplineParameterKeyframe<T>, float, T> CustomInterpolator { get; set; }


        // The raw keyframe values. This is the core data of the KeyframedSpineParamter<T> and is serialised.
        [HideInInspector]
        [SerializeField]
        List<SplineParameterKeyframe<T>> rawKeyframes = new List<SplineParameterKeyframe<T>>();

        // keeps keyframes safe from external parties and handles transformation for data that needs to respect the spline transform.
        List<SplineParameterKeyframe<T>> keyframesExternal = new List<SplineParameterKeyframe<T>>();

        // Returns an up to date list of the raw key frames
        // - RawKeyframes are NOT transformed to world space!
        // - RawKeyframes are NOT SORTED they are in key index order
        // This list is only valid until the next call to Keyframes or OrderedKeyframes
        // if you want to keep it, then copy the list.
        public List<SplineParameterKeyframe<T>> RawKeyframes
        {
            get
            {
                UpdateKeyframeLocations();
                keyframesExternal.Clear();
                keyframesExternal.AddRange(rawKeyframes);
                return keyframesExternal;
            }
        }

        // Returns an up to date list of keyframes
        // - Keyframes ARE transformed to world space
        // - Keyframes are NOT SORTED, they are in keyframe index order
        // This list is only valid until the next call to Keyframes or OrderedKeyframes
        // if you want to keep it, then copy the list.
        public List<SplineParameterKeyframe<T>> Keyframes
        {
            get
            {
                UpdateKeyframeLocations();

                keyframesExternal.Clear();
                keyframesExternal.AddRange( rawKeyframes );
                for( int i = 0; i < keyframesExternal.Count; ++i )
                {
                    var key = keyframesExternal[i];
                    key = TransformKeyframe( key );
                    keyframesExternal[i] = key;
                }

                return keyframesExternal;
            }
        }

        // Returns an up to date list of keyframes
        // - OrderedKeyframes ARE transformed to world space
        // - OrderedKeyframes SORTED in spline distance order
        // This list is only valid until the next call to Keyframes or OrderedKeyframes
        // if you want to keep it, then copy the list
        public List<SplineParameterKeyframe<T>> OrderedKeyframes
        {
            get
            {
                UpdateKeyframeLocations();

                keyframesExternal.Clear();
                keyframesExternal.AddRange(rawKeyframes);
                keyframesExternal.Sort( SortKeyframesByLoopDistance );
                for( int i = 0; i < keyframesExternal.Count; ++i )
                {
                    var key = keyframesExternal[i];
                    key = TransformKeyframe( key );
                    keyframesExternal[i] = key;
                }

                return keyframesExternal;
            }
        }

        // Returns true if keyframe tangent handles are enabled
        public bool EnableKeyframeTangents
        {
            get
            {
                return keyframeInterpolationMode == KeyframeInterpolationModes.Tangents;
            }
        }

        // Sorting function to sort keys into the order they are along the spline
        int SortKeyframesByLoopDistance( SplineParameterKeyframe<T> a, SplineParameterKeyframe<T> b )
        {
            return a.location.loopDistance.CompareTo( b.location.loopDistance );
        }

        // Event when a keyframe is added, edited or removed, including when the spline is moved and keys update their positions.
        void OnKeyframesChanged()
        {
            ++updateCount;
            // do custom stuff here.
            onKeyframesChanged?.Invoke();
        }

        int updateCount = 0;
        public int GetUpdateCount()
        {
            return updateCount;
        }

        // Creates a default keyframe
        SplineParameterKeyframe<T> CreateKeyframe( T value, SplineResult location, float inTangent, float outTangent )
        {
            return new SplineParameterKeyframe<T>( value, location, inTangent, outTangent );
        }

        // Returns true if the the editor for this KeyframedSplineParameter<T> is active.
        bool ToolActive
        {
            get
            {
                return Selection.activeObject == gameObject && editInstance == this && ToolManager.activeToolType == GetToolType();
            }
        }

        // Transforms a local space keyframe into world space.
        public virtual SplineParameterKeyframe<T> TransformKeyframe( SplineParameterKeyframe<T> keyframe )
        {
            return keyframe;
        }
        // Transforms a world space keyframe into world space.
        public virtual SplineParameterKeyframe<T> InverseTransformKeyframe( SplineParameterKeyframe<T> keyframe )
        {
            return keyframe;
        }

        // Returns a default keyframe value
        public virtual T GetDefaultKeyframeValue()
        {
            return new T();
        }

        // Returns the interpolated value at distance along the spline
        public virtual T GetValueAtDistance( float distance, T defaultValue )
        {
            if( rawKeyframes.Count == 0 )
            {
                return defaultValue;
            }

            if( rawKeyframes.Count == 1 )
            {
                return TransformKeyframe( rawKeyframes[0] ).value;
            }

            float loopDistance = spline.IsLoop() ? distance % spline.GetLength() : Mathf.Clamp( distance, 0, spline.GetLength() );
            return InterpolateWithDistance( loopDistance );
        }

        // Returns the interpolated value at t along the spline
        public virtual T GetValueAtT( float t, T defaultValue )
        {
            return GetValueAtDistance( spline.GetLength() * t, defaultValue );
        }

        // Returns the keyframe at the given index
        public SplineParameterKeyframe<T> GetKeyframe( int index )
        {
            if( index < 0 || index >= rawKeyframes.Count )
            {
                Debug.LogError( "Keyframe index is out of range. " + index.ToString() );
                return new SplineParameterKeyframe<T>();
            }

            UpdateKeyframeLocation( index, rawKeyframes[index] );
            return TransformKeyframe( rawKeyframes[ index ] );
        }

        // Sets the spline location of the keyframe at index
        public void SetKeyframeLocation( int index, SplineResult location )
        {
            var key = rawKeyframes[index];
            SetKeyframe( index, CreateKeyframe( TransformKeyframe( key ).value, location, key.inTangent, key.outTangent ) );
        }

        // Sets value stored of the keyframe at index
        public void SetKeyframeValue( int index, T value )
        {
            var key = rawKeyframes[index];
            SetKeyframe( index, CreateKeyframe( value, key.location, key.inTangent, key.outTangent ) );
        }

        // Sets value stored and the spline location of the keyframe at index
        public void SetKeyframe( int index, T value, SplineResult location )
        {
            var key = rawKeyframes[index];
            SetKeyframe( index, CreateKeyframe( value, location, key.inTangent, key.outTangent ) );
        }

        // Sets value stored and the spline location of the keyframe at index
        public void SetKeyframeTangents( int index, float inTangent, float outTangent )
        {
            var key = rawKeyframes[index];
            SetKeyframe( index, CreateKeyframe( TransformKeyframe( key ).value, key.location, key.inTangent, key.outTangent ) );
        }

        // Sets the keyframe at index
        public void SetKeyframe( int index, SplineParameterKeyframe<T> dataKey )
        {
            if( index < 0 || index >= rawKeyframes.Count )
            {
                Debug.LogError( "Keyframe index is out of range. " + index.ToString() );
                return;
            }

            dataKey = InverseTransformKeyframe( dataKey );
            rawKeyframes[index] = dataKey;
            OnKeyframesChanged();
        }

        // Creates a new keyframe at the given distance along the spline
        public int InsertAtDistance( T value, float distance )
        {
            SplineResult result = spline.GetResultAtDistance( distance );
            return Insert( value, result );
        }

        // Creates a new keyframe at the given spline location
        public int Insert( T value, SplineResult location )
        {
            return Insert( CreateKeyframe( value, location, 0, 0 ) );
        }

        // Adds the given keyframe
        public int Insert( SplineParameterKeyframe<T> patameterKey )
        {
            patameterKey = InverseTransformKeyframe( patameterKey );

            int index = -1;
            for( int i = 0; i < rawKeyframes.Count; ++i )
            {
                if( rawKeyframes[i].location.distance > patameterKey.location.distance )
                {
                    rawKeyframes.Insert( i, patameterKey );
                    index = i;
                    break;
                }
            }

            if( index == -1 )
            {
                rawKeyframes.Add( patameterKey );
                index = rawKeyframes.Count - 1;
            }
            
            OnKeyframesChanged();
            return index;
        }

        // Removes the keyframe at index
        public void RemoveAt( int index )
        {
            if( index < 0 || index >= rawKeyframes.Count )
            {
                Debug.LogError( "Keyframe index is out of range. " + index.ToString() );
                return;
            }

            rawKeyframes.RemoveAt( index );
            OnKeyframesChanged();
        }

        float GetBlend( SplineParameterKeyframe<T> first, SplineParameterKeyframe<T> second, float x )
        {
            switch( keyframeInterpolationMode )
            {
                case KeyframeInterpolationModes.Linear:
                    return x;
                case KeyframeInterpolationModes.SmoothStep:
                    return Mathf.SmoothStep( 0, 1, x );
                case KeyframeInterpolationModes.Tangents:
                    // t = A*(1-x)^3+3*B*(1-x)^2*x+3*C*(1-x)*x^2+D*x^3
                    float A = 0;
                    // remap these so that tangent = 0 is linear, tangent = 1 is smooth.
                    float B = 1 - MathsUtils.Remap( first.outTangent, 0, 1, 0.66f, 1 );
                    float C = MathsUtils.Remap( second.inTangent, 0, 1, 0.66f, 1 );
                    float D = 1;

                    float oneMinusX = 1 - x;
                    return A * (oneMinusX * oneMinusX * oneMinusX) + 3 * B * (oneMinusX * oneMinusX) * x + 3 * C * oneMinusX * (x * x) + D * (x * x * x);
                case KeyframeInterpolationModes.Constant:
                    return 1;
            }
            return x;
        }

        // Returns an interpolated keyframe value of T at distance on the spline
        T InterpolateWithDistance( float distance )
        {
            var orderedKeys = OrderedKeyframes;

            SplineParameterKeyframe<T> first = GetKeyframeBeforeDistance( orderedKeys, distance );
            SplineParameterKeyframe<T> second = GetKeyframeAfterDistance( orderedKeys, distance );

            float firstDistance = first.location.loopDistance;
            float secondDistance = second.location.loopDistance;

            if( first.location.loopDistance > second.location.loopDistance )
            {
                firstDistance -= first.location.length;
            }

            if( distance < firstDistance )
            {
                distance += first.location.length;
            }

            if( distance > secondDistance )
            {
                distance -= first.location.length;
            }

            float t = Mathf.InverseLerp( firstDistance, secondDistance, distance );
            float blend = GetBlend( first, second, t );

            return Interpolate( first, second, blend );
        }

        // Returns an interpolated keyframe value of T between two keyframes
        T Interpolate( SplineParameterKeyframe<T> first, SplineParameterKeyframe<T> second, float t )
        {
            if( CustomInterpolator != null )
            {
                return CustomInterpolator( spline, first, second, t );
            }
            return Interpolator.Interpolate( first.value, second.value, t );
        }

        // Returns the first key before the specified distance along the spline
        SplineParameterKeyframe<T> GetKeyframeBeforeDistance( List<SplineParameterKeyframe<T>> orderedKeys, float distance )
        {
            for( int i = orderedKeys.Count - 1; i >= 0; --i )
            {
                if(orderedKeys[i].location.loopDistance < distance
                    || Mathf.Approximately(orderedKeys[i].location.loopDistance, distance ) )
                {
                    return orderedKeys[i];
                }
            }

            return spline.IsLoop() ? orderedKeys[orderedKeys.Count - 1] : orderedKeys[0];
        }

        // Returns the first key after the specified distance along the spline
        SplineParameterKeyframe<T> GetKeyframeAfterDistance( List<SplineParameterKeyframe<T>> orderedKeys, float distance )
        {
            for( int i = 0; i < orderedKeys.Count; ++i )
            {
                if(orderedKeys[i].location.loopDistance >= distance
                    || Mathf.Approximately(orderedKeys[i].location.loopDistance, distance ) )
                {
                    return orderedKeys[i];
                }
            }

            return spline.IsLoop() ? orderedKeys[0] : orderedKeys[orderedKeys.Count - 1];
        }

        // Registers event handlers with the parent spline
        void RegisterListeners( SplineComponent spline )
        {
            if( spline == null )
            {
                return;
            }

            // no chance of double registration
            UnregisterListeners( spline );

            spline.onUpdated += OnSplineUpdated;
            spline.onNodeAdded += OnSplineNodeAdded;
            spline.onNodeRemoved += OnSplineNodeRemoved;
            spline.onGetUndoObjects += OnGetUndoObjects;
        }

        // Unregisterd event handlers with the parent spline
        void UnregisterListeners( SplineComponent spline )
        {
            if( spline == null )
            {
                return;
            }

            spline.onUpdated -= OnSplineUpdated;
            spline.onNodeAdded -= OnSplineNodeAdded;
            spline.onNodeRemoved -= OnSplineNodeRemoved;
            spline.onGetUndoObjects -= OnGetUndoObjects;
        }

        // Returns all objects needed to handle editor undo/redo
        void OnGetUndoObjects( List<Object> undoObjects )
        {
            undoObjects.Add( this );
        }

        // Initialises the KeyframedSplineParameter<T>
        void OnEnable()
        {
            ParameterNameHash = Animator.StringToHash( parameterName );
            if( Application.isEditor && !Application.isPlaying )
            {
                if( spline == null )
                {
                    spline = GetComponent<SplineComponent>();
                }
                lastSimple = spline;
                RegisterListeners( spline );
            }
        }

        // Monitors the spline set as the parent spline and handles
        // Registration and Unregistration of event handlers if the
        // Spline is changed.
        void Update()
        {
            if( lastSimple != spline )
            {
                if( lastSimple != null )
                {
                    UnregisterListeners( lastSimple );
                }
                RegisterListeners( spline );
                lastSimple = spline;
            }
        }

        void OnValidate()
        {
            OnKeyframesChanged();
        }

        // Updates the keyframe at index's location on the spline using the specified location reposition mode
        void UpdateKeyframeLocation( int index, SplineParameterKeyframe<T> key )
        {
            if( key.location.updateCount != spline.GetUpdateCount() )
            {
                SetKeyframeLocation( index, SplineChangedEventHelper.OnSplineLengthChanged( key.location, spline, keyRepositionMode ) );
            }
        }

        // Updates keyframes locations on the spline.
        void UpdateKeyframeLocations()
        {
            List<SplineParameterKeyframe<T>> keys = rawKeyframes;
            for (int i = 0; i < keys.Count; ++i)
            {
                UpdateKeyframeLocation(i, keys[i]);
            }
        }

        // Event handler for when the spline is edited.
        void OnSplineUpdated()
        {
            UpdateKeyframeLocations();
            onSplineChanged?.Invoke();
        }

        // Event handler for when the spline has a new node added
        void OnSplineNodeAdded( int newNodeIndex )
        {
            List<SplineParameterKeyframe<T>> keys = rawKeyframes;
            for( int i = 0; i < keys.Count; ++i )
            {
                SetKeyframeLocation( i, SplineChangedEventHelper.OnNodeAdded( keys[i].location, spline, newNodeIndex, keyRepositionMode ) );
            }
            onSplineChanged?.Invoke();
        }

        // Event handler for when the spline has a node removed
        void OnSplineNodeRemoved( int removedNodeIndex )
        {
            List<SplineParameterKeyframe<T>> keys = rawKeyframes;
            for( int i = 0; i < keys.Count; ++i )
            {
                SetKeyframeLocation( i, SplineChangedEventHelper.OnNodeRemoved( keys[i].location, spline, removedNodeIndex, keyRepositionMode ) );
            }
            onSplineChanged?.Invoke();
        }

        // Draws generic keyframe gizmos
        // override this to draw completely custom gizmos
        protected void OnDrawGizmos()
        {
            if( !enableKeyframeHandles )
            {
                return;
            }

            if( spline == null )
            {
                return;
            }

            Handles.zTest = spline.GetZTest() ? UnityEngine.Rendering.CompareFunction.LessEqual : UnityEngine.Rendering.CompareFunction.Always;
            DrawKeyframeGizmos();
        }

        // Draws generic keyframe gizmos
        protected void DrawKeyframeGizmos( )
        {
            var keys = Keyframes;
            for( int i = 0; i < keys.Count; ++i )
            {
                DrawKeyframeGizmo( keys[i] );
            }
        }

        // Draws a generic keyframe gizmo
        protected void DrawKeyframeGizmo( SplineParameterKeyframe<T> key )
        {
            DrawKeyframeValueGizmo( key );

#if UNITY_EDITOR
            // default keyframe gizmos
            // keyframe colour, same as the animator window
            using( new Handles.DrawingScope( ToolActive ? KeyframedSplineParameterTool<Vector3>.ActiveColor : KeyframedSplineParameterTool<Vector3>.InactiveColor ) )
            {
                KeyframedSplineParameterTool<Vector3>.KeyframeHandleCap( 0, key.location.position, Quaternion.identity, KeyframedSplineParameterTool<Vector3>.GetHandleSize( key.location.position ) * spline.GetGizmoScale(), EventType.Repaint );
            }
#endif
        }

        // Draws custom gizmos for a keyframe
        // override this to draw completely custom gizmos for data on keys while still using default key gizmos
        protected virtual void DrawKeyframeValueGizmo( SplineParameterKeyframe<T> key )
        {
            // override me to draw special gizmos for data
        }
    }
}