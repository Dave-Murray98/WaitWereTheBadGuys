using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector;

public class AnimationClipSearchWindow : OdinEditorWindow
{
    [ShowInInspector, PropertySpace(10)]
    [OnValueChanged("OnSearchFilterChanged")]
    [LabelText("Search Animations:")]
    private string searchFilter = "";

    [ShowInInspector, ReadOnly]
    [LabelText("Selected Object:")]
    private string selectedObjectName = "None";

    [ShowInInspector, ReadOnly]
    [LabelText("Found Animations:")]
    private string animationCount = "0";

    [ShowInInspector]
    [ListDrawerSettings(ShowFoldout = false, DraggableItems = false, HideAddButton = true, HideRemoveButton = true, ShowItemCount = false, CustomAddFunction = "")]
    [PropertySpace(10)]
    private List<AnimationClipInfo> filteredClips = new List<AnimationClipInfo>();

    // Internal data (not shown in inspector)
    private List<AnimationClip> allClips = new List<AnimationClip>();
    private Animator currentAnimator;
    private GameObject selectedObject;

    [System.Serializable]
    [InlineProperty]
    [HideLabel]
    public class AnimationClipInfo
    {
        [ShowInInspector, ReadOnly, HorizontalGroup("Main", 0.3f)]
        [HideLabel]
        public string clipName;

        [ShowInInspector, ReadOnly, HorizontalGroup("Main", 0.19f)]
        [HideLabel]
        public string duration;

        [ShowInInspector, ReadOnly, HorizontalGroup("Main", 0.19f)]
        [HideLabel]
        public string eventCount;

        [Button("Open"), HorizontalGroup("Main", 0.3f)]
        public void OpenClip()
        {
            var window = EditorWindow.GetWindow<AnimationClipSearchWindow>();
            window.OpenAnimationInWindow(actualClip);
        }

        [HideInInspector]
        public AnimationClip actualClip;

        public AnimationClipInfo(AnimationClip clip)
        {
            actualClip = clip;
            clipName = clip.name;
            duration = $"{clip.length:F2}s";
            eventCount = $"{clip.events.Length} events";
        }
    }

    [MenuItem("Tools/Animation Clip Search")]
    private static void OpenWindow()
    {
        GetWindow<AnimationClipSearchWindow>().Show();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        titleContent = new GUIContent("Animation Search");
        RefreshAnimationList();

        // Listen for selection changes
        Selection.selectionChanged += OnSelectionChanged;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        RefreshAnimationList();
    }

    [Button("Refresh Animation List"), PropertySpace(10)]
    private void RefreshAnimationList()
    {
        allClips.Clear();
        currentAnimator = null;
        selectedObject = Selection.activeGameObject;

        if (selectedObject != null)
        {
            selectedObjectName = selectedObject.name;
            currentAnimator = selectedObject.GetComponent<Animator>();

            if (currentAnimator != null && currentAnimator.runtimeAnimatorController != null)
            {
                // Get all animation clips from the animator controller
                var controller = currentAnimator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                if (controller != null)
                {
                    allClips = controller.animationClips.ToList();
                }
            }
        }
        else
        {
            selectedObjectName = "None";
        }

        FilterAnimations();
    }

    private void OnSearchFilterChanged()
    {
        FilterAnimations();
    }

    private void FilterAnimations()
    {
        List<AnimationClip> clipsToShow;

        if (string.IsNullOrEmpty(searchFilter))
        {
            clipsToShow = new List<AnimationClip>(allClips);
        }
        else
        {
            clipsToShow = allClips.Where(clip =>
                clip.name.ToLower().Contains(searchFilter.ToLower())
            ).ToList();
        }

        // Convert to display format
        filteredClips = clipsToShow.Select(clip => new AnimationClipInfo(clip)).ToList();
        animationCount = $"{filteredClips.Count} of {allClips.Count}";
    }

    public void OpenAnimationInWindow(AnimationClip clip)
    {
        if (selectedObject == null || currentAnimator == null || clip == null)
            return;

        // Ensure the object stays selected
        Selection.activeGameObject = selectedObject;

        // Open Animation window
        var animationWindowType = System.Type.GetType("UnityEditor.AnimationWindow,UnityEditor");
        var animationWindow = EditorWindow.GetWindow(animationWindowType);

        if (animationWindow != null)
        {
            animationWindow.Show();
            animationWindow.Focus();

            // Use EditorApplication.delayCall to ensure the Animation window is fully loaded
            EditorApplication.delayCall += () =>
            {
                SetAnimationClip(animationWindow, clip);
            };
        }

        Debug.Log($"Switching to animation clip: {clip.name} on object: {selectedObject.name}");
    }

    private void SetAnimationClip(EditorWindow animationWindow, AnimationClip clip)
    {
        try
        {
            // Make sure our object is still selected
            if (Selection.activeGameObject != selectedObject)
            {
                Selection.activeGameObject = selectedObject;
            }

            // Get the animation window state
            var animationWindowType = animationWindow.GetType();
            var stateProperty = animationWindowType.GetProperty("state",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (stateProperty != null)
            {
                var state = stateProperty.GetValue(animationWindow);
                if (state != null)
                {
                    var stateType = state.GetType();

                    // Try different methods to set the current clip
                    // Method 1: SetCurrentClip
                    var setCurrentClipMethod = stateType.GetMethod("SetCurrentClip",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (setCurrentClipMethod != null)
                    {
                        setCurrentClipMethod.Invoke(state, new object[] { clip });
                        Debug.Log($"Successfully switched to clip: {clip.name}");
                        return;
                    }

                    // Method 2: Try setting the activeAnimationClip property
                    var activeClipProperty = stateType.GetProperty("activeAnimationClip",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (activeClipProperty != null && activeClipProperty.CanWrite)
                    {
                        activeClipProperty.SetValue(state, clip);
                        Debug.Log($"Successfully switched to clip: {clip.name}");
                        return;
                    }

                    // Method 3: Try m_ActiveAnimationClip field
                    var activeClipField = stateType.GetField("m_ActiveAnimationClip",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (activeClipField != null)
                    {
                        activeClipField.SetValue(state, clip);

                        // Force a repaint of the animation window
                        animationWindow.Repaint();
                        Debug.Log($"Successfully switched to clip: {clip.name}");
                        return;
                    }
                }
            }

            // If all methods fail, fall back to the original approach
            Debug.Log($"Could not automatically switch to clip '{clip.name}'. Please select it manually from the Animation window dropdown.");

        }
        catch (System.Exception e)
        {
            Debug.Log($"Error setting animation clip: {e.Message}. Please select '{clip.name}' manually from the Animation window dropdown.");
        }
    }
}