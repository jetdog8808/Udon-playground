
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRCSDK2.Validation;
using VRCSDK2.Validation.Performance;
using VRCSDK2.Validation.Performance.Stats;

public partial class VRCSdkControlPanel : EditorWindow
{
    public static System.Action _EnableSpatialization = null;   // assigned in AutoAddONSPAudioSourceComponents

    const string kCantPublishContent = "Before you can upload avatars or worlds, you will need to spend some time in VRChat.";
    const string kCantPublishAvatars = "Before you can upload avatars, you will need to spend some time in VRChat.";
    const string kCantPublishWorlds = "Before you can upload worlds, you will need to spend some time in VRChat.";
    private const string FIX_ISSUES_TO_BUILD_OR_TEST_WARNING_STRING = "You must address the above issues before you can build or test this content!";
    private const string kAvatarOptimizationTipsURL = "https://docs.vrchat.com/docs/avatar-optimizing-tips";
    private const string kAvatarRigRequirementsURL = "https://docs.vrchat.com/docs/rig-requirements";

    static Texture _perfIcon_Excellent;
    static Texture _perfIcon_Good;
    static Texture _perfIcon_Medium;
    static Texture _perfIcon_Poor;
    static Texture _perfIcon_VeryPoor;
    static Texture _bannerImage;

    private void ResetIssues()
    {
        GUIErrors.Clear();
        GUIInfos.Clear();
        GUIWarnings.Clear();
        GUILinks.Clear();
        GUIStats.Clear();
        checkedForIssues = false;
    }

    bool checkedForIssues = false;

    class Issue
    {
        public string issueText;
        public System.Action showThisIssue;
        public System.Action fixThisIssue;
        public PerformanceRating performanceRating;

        public Issue(string text, System.Action show, System.Action fix, PerformanceRating rating = PerformanceRating.None)
        {
            issueText = text;
            showThisIssue = show;
            fixThisIssue = fix;
            performanceRating = rating;
        }

        public class Equality : IEqualityComparer<Issue>, IComparer<Issue>
        {
            public bool Equals(Issue b1, Issue b2)
            {
                return (b1.issueText == b2.issueText);
            }
            public int Compare(Issue b1, Issue b2)
            {
                return string.Compare(b1.issueText, b2.issueText);
            }
            public int GetHashCode(Issue bx)
            {
                return bx.issueText.GetHashCode();
            }
        }
    }

    Dictionary<Object, List<Issue>> GUIErrors = new Dictionary<Object, List<Issue>>();
    Dictionary<Object, List<Issue>> GUIWarnings = new Dictionary<Object, List<Issue>>();
    Dictionary<Object, List<Issue>> GUIInfos = new Dictionary<Object, List<Issue>>();
    Dictionary<Object, List<Issue>> GUILinks = new Dictionary<Object, List<Issue>>();
    Dictionary<Object, List<Issue>> GUIStats = new Dictionary<Object, List<Issue>>();

    private string customNamespace;

    void AddToReport(Dictionary<Object, List<Issue>> report, Object subject, string output, System.Action show, System.Action fix)
    {
        if (subject == null)
            subject = this;
        if (!report.ContainsKey(subject))
            report.Add(subject, new List<Issue>());

        var issue = new Issue(output, show, fix);
        if (!report[subject].Contains(issue, new Issue.Equality()))
        {
            report[subject].Add(issue);
            report[subject].Sort(new Issue.Equality());
        }
    }

    void BuilderAssemblyReload()
    {
        ResetIssues();
    }

    void OnGUIError(Object subject, string output, System.Action show, System.Action fix)
    {
        AddToReport(GUIErrors, subject, output, show, fix);
    }

    void OnGUIWarning(Object subject, string output, System.Action show, System.Action fix)
    {
        AddToReport(GUIWarnings, subject, output, show, fix);
    }

    void OnGUIInformation(Object subject, string output)
    {
        AddToReport(GUIInfos, subject, output, null, null);
    }

    void OnGUILink(Object subject, string output, string link)
    {
        AddToReport(GUILinks, subject, output + "\n" + link, null, null);
    }

    void OnGUIStat(Object subject, string output, PerformanceRating rating, System.Action show, System.Action fix)
    {
        if (subject == null)
            subject = this;
        if (!GUIStats.ContainsKey(subject))
            GUIStats.Add(subject, new List<Issue>());
        GUIStats[subject].Add(new Issue(output, show, fix, rating));
    }

    VRC.SDKBase.VRC_SceneDescriptor[] scenes;
    VRC.SDKBase.VRC_AvatarDescriptor[] avatars;
    Vector2 scrollPos;
    Vector2 builderScrollPos;
    Vector2 avatarListScrollPos;
    VRC.SDKBase.VRC_AvatarDescriptor selectedAvatar;

    public static void SelectAvatar(VRC.SDKBase.VRC_AvatarDescriptor avatar)
    {
        if (window != null)
            window.selectedAvatar = avatar;
    }

    private bool showAvatarPerformanceDetails
    {
        get { return EditorPrefs.GetBool("VRC.SDKBase_showAvatarPerformanceDetails", false); }
        set { EditorPrefs.SetBool("VRC.SDKBase_showAvatarPerformanceDetails", value); }
    }

    private int triggerLineMode
    {
        get { return EditorPrefs.GetInt("VRC.SDKBase_triggerLineMode", 0); }
        set { EditorPrefs.SetInt("VRC.SDKBase_triggerLineMode", value); }
    }

    public void FindScenesAndAvatars()
    {
        var newScenes = (VRC.SDKBase.VRC_SceneDescriptor[])VRC.Tools.FindSceneObjectsOfTypeAll<VRC.SDKBase.VRC_SceneDescriptor>();
        List<VRC.SDKBase.VRC_AvatarDescriptor> allavatars = VRC.Tools.FindSceneObjectsOfTypeAll<VRC.SDKBase.VRC_AvatarDescriptor>().ToList();
        // select only the active avatars
        var newAvatars = allavatars.Where(av => (null != av) && av.gameObject.activeInHierarchy).ToArray();

        if (scenes != null)
        {
            foreach (var s in newScenes)
                if (scenes.Contains(s) == false)
                    checkedForIssues = false;
        }

        if (avatars != null)
        {
            foreach (var a in newAvatars)
                if (avatars.Contains(a) == false)
                    checkedForIssues = false;
        }

        scenes = newScenes;
        avatars = newAvatars;

        if (VRCSdk3Analysis.GetSDKInScene(VRCSdk3Analysis.SdkVersion.VRCSDK3).Count > 0)
            avatars = new VRC.SDKBase.VRC_AvatarDescriptor[0];
    }

    void ShowBuilder()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();

        if (VRC.Core.RemoteConfig.IsInitialized())
        {
            string sdkUnityVersion = VRC.Core.RemoteConfig.GetString("sdkUnityVersion");
            if (Application.unityVersion != sdkUnityVersion)
            {
                OnGUIWarning(null, "You are not using the recommended Unity version for the VRChat SDK. Content built with this version may not work correctly. Please use Unity " + sdkUnityVersion,
                    null,
                    () => { Application.OpenURL("https://unity3d.com/get-unity/download/archive"); }
                    );
            }
        }

        if (VRCSdk3Analysis.IsSdkDllActive(VRCSdk3Analysis.SdkVersion.VRCSDK2) && VRCSdk3Analysis.IsSdkDllActive(VRCSdk3Analysis.SdkVersion.VRCSDK3))
        {
            var sdk2Components = VRCSdk3Analysis.GetSDKInScene(VRCSdk3Analysis.SdkVersion.VRCSDK2);
            var sdk3Components = VRCSdk3Analysis.GetSDKInScene(VRCSdk3Analysis.SdkVersion.VRCSDK3);
            if (sdk2Components.Count > 0 && sdk3Components.Count > 0)
            {
                OnGUIError(null,
                "This scene contains components from the VRChat SDK version 2 and version 3. Version two elements will have to be replaced with their version 3 counterparts to build with SDK3 and UDON.",
                () => { Selection.objects = sdk2Components.ToArray(); },
                null
                );
            }
            //else if (sdk2Components.Count > 0)
            //{
            //    OnGUIError(null,
            //    "This scene uses VRChat SDK 2 scripts. To continue using these scripts you may configure the SDK for VRCSDK2 usage on the settings tab. In order to use VRCSDK3 and Udon you must replace these VRCSDK2 components with the new VRCSDK3 components.",
            //    () => { Selection.objects = sdk2Components.ToArray(); },
            //    () => { VRCSettings.Get().activeWindowPanel = 3; }
            //    );
            //}
            //else
            //{
            //    OnGUIError(null,
            //    "You're almost ready to build content with VRChat SDK 3. You may go to the settings screen to configure your SDK or press Auto-Fix to set up SDK3 usage.",
            //    () => { VRCSettings.Get().activeWindowPanel = 3; },
            //    () => { VRCSdk3Analysis.SetSdkVersionActive(VRCSdk3Analysis.SdkVersion.VRCSDK3); }
            //    );
            //}
        }

        if (postProcessVolumeType != null)
        {
            if (Camera.main && Camera.main.GetComponentInChildren(postProcessVolumeType))
            {
                OnGUIWarning(null,
                "Scene has a PostProcessVolume on the Reference Camera (Main Camera). This Camera is disabled at runtime. Please move the PostProcessVolume to another GameObject.",
                () => { Selection.activeGameObject = Camera.main.gameObject; },
                () => { TryMovePostProcessVolumeAwayFromMainCamera(); }
                );
            }
        }

        if (Lightmapping.giWorkflowMode == Lightmapping.GIWorkflowMode.Iterative)
        {
            OnGUIWarning(null,
            "Automatic lightmap generation is enabled, which may stall the Unity build process. Before building and uploading, consider turning off 'Auto Generate' at the bottom of the Lighting Window.",
            () =>
            {
                EditorWindow lightingWindow = GetLightingWindow();
                if (lightingWindow)
                {
                    lightingWindow.Show();
                    lightingWindow.Focus();
                }
            },
            () =>
            {
                Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
                EditorWindow lightingWindow = GetLightingWindow();
                if (lightingWindow)
                {
                    lightingWindow.Repaint();
                    this.Focus();
                }
            }
            );
        }

        FindScenesAndAvatars();

        if ((null == scenes) || (null == avatars))
        {
            if (Event.current.type != EventType.Used)
            {
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            return;
        }

        if (scenes.Length > 0 && avatars.Length > 0)
        {
            GameObject[] gos = new GameObject[avatars.Length];
            for (int i = 0; i < avatars.Length; ++i)
                gos[i] = avatars[i].gameObject;
            OnGUIError(null, "A Unity scene containing a VRChat Scene Descriptor should not also contain avatars.",
                delegate ()
                {
                    List<GameObject> show = new List<GameObject>();
                    foreach (var s in scenes)
                        show.Add(s.gameObject);
                    foreach (var a in avatars)
                        show.Add(a.gameObject);
                    Selection.objects = show.ToArray();
                }, null);

            EditorGUILayout.Separator();
            GUILayout.BeginVertical(GUILayout.Width(SdkWindowWidth));
            OnGUIShowIssues();
            GUILayout.EndVertical();
        }
        else if (scenes.Length > 1)
        {
            GameObject[] gos = new GameObject[scenes.Length];
            for (int i = 0; i < scenes.Length; ++i)
                gos[i] = scenes[i].gameObject;
            OnGUIError(null, "A Unity scene containing a VRChat Scene Descriptor should only contain one Scene Descriptor.",
                delegate { Selection.objects = gos; }, null);

            EditorGUILayout.Separator();
            GUILayout.BeginVertical(GUILayout.Width(SdkWindowWidth));
            OnGUIShowIssues();
            GUILayout.EndVertical();
        }
        else if (scenes.Length == 1)
        {
            bool inScroller = true;

            scrollPos = GUILayout.BeginScrollView(scrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Width(SdkWindowWidth));

            try
            {
                bool setupRequired = OnGUISceneSetup(scenes[0]);

                if (!setupRequired)
                {
                    if (!checkedForIssues)
                    {
                        ResetIssues();
                        OnGUISceneCheck(scenes[0]);
                        checkedForIssues = true;
                    }

                    OnGUISceneSettings(scenes[0]);

                    OnGUIShowIssues();
                    OnGUIShowIssues(scenes[0]);

                    GUILayout.FlexibleSpace();

                    GUILayout.EndScrollView();
                    inScroller = false;

                    OnGUIScene(scenes[0]);

                }
                else
                {
                    OnGuiFixIssuesToBuildOrTest();
                    GUILayout.EndScrollView();
                }
            }
            catch (System.Exception)
            {
                if (inScroller)
                    GUILayout.EndScrollView();
            }

        }
        else if (avatars.Length > 0 && VRCSdk3Analysis.GetSDKInScene(VRCSdk3Analysis.SdkVersion.VRCSDK3).Count < 1)
        {
            if (!checkedForIssues)
            {
                ResetIssues();
                for (int i = 0; i < avatars.Length; ++i)
                    OnGUIAvatarCheck(avatars[i]);
                checkedForIssues = true;
            }

            bool drawList = true;
            if (avatars.Length == 1)
            {
                drawList = false;
                selectedAvatar = avatars[0];
            }

            if (drawList)
            {
                GUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"), GUILayout.Width(SdkWindowWidth), GUILayout.MaxHeight(150));
                avatarListScrollPos = GUILayout.BeginScrollView(avatarListScrollPos, false, false);

                for (int i = 0; i < avatars.Length; ++i)
                {
                    var av = avatars[i];
                    EditorGUILayout.Space();
                    if (selectedAvatar == av)
                    {
                        if (GUILayout.Button(av.gameObject.name, listButtonStyleSelected, GUILayout.Width(SdkWindowWidth - 50)))
                            selectedAvatar = null;
                    }
                    else
                    {
                        if (GUILayout.Button(av.gameObject.name, ((i & 0x01) > 0) ? (listButtonStyleOdd) : (listButtonStyleEven), GUILayout.Width(SdkWindowWidth - 50)))
                            selectedAvatar = av;
                    }
                }

                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }

            GUILayout.BeginVertical(GUILayout.Width(SdkWindowWidth));
            OnGUIShowIssues();
            GUILayout.EndVertical();

            EditorGUILayout.Separator();

            if (selectedAvatar != null)
            {
                GUILayout.BeginVertical(boxGuiStyle);
                OnGUIAvatarSettings(selectedAvatar);
                GUILayout.EndVertical();

                scrollPos = GUILayout.BeginScrollView(scrollPos, false, false, GUILayout.Width(SdkWindowWidth));
                OnGUIShowIssues(selectedAvatar);
                GUILayout.EndScrollView();

                GUILayout.FlexibleSpace();

                GUILayout.BeginVertical(boxGuiStyle);
                OnGUIAvatar(selectedAvatar);
                GUILayout.EndVertical();
            }
        }
        else
        {
            EditorGUILayout.Space();
            if (BuildPipeline.isBuildingPlayer)
            {
                GUILayout.Space(20);
                EditorGUILayout.LabelField("Building – Please Wait ...", titleGuiStyle, GUILayout.Width(SdkWindowWidth));
            }
            else
            {
                if (VRCSdk3Analysis.GetSDKInScene(VRCSdk3Analysis.SdkVersion.VRCSDK3).Count > 0)
                {
                    EditorGUILayout.LabelField("Avatars are currently not supported in SDK3", titleGuiStyle, GUILayout.Width(SdkWindowWidth));
                }
                else
                {
                    EditorGUILayout.LabelField("A VRC_SceneDescriptor or VRC_AvatarDescriptor\nis required to build VRChat SDK Content", titleGuiStyle, GUILayout.Width(SdkWindowWidth));
                }
            }
        }

        if (Event.current.type != EventType.Used)
        {
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

    }

    bool showLayerHelp = false;
    int numClients = 1;

    void CheckUploadChanges(VRC.SDKBase.VRC_SceneDescriptor scene)
    {
        if (UnityEditor.EditorPrefs.HasKey("VRC.SDKBase_scene_changed") &&
                UnityEditor.EditorPrefs.GetBool("VRC.SDKBase_scene_changed"))
        {
            UnityEditor.EditorPrefs.DeleteKey("VRC.SDKBase_scene_changed");

            if (UnityEditor.EditorPrefs.HasKey("VRC.SDKBase_capacity"))
            {
                scene.capacity = UnityEditor.EditorPrefs.GetInt("VRC.SDKBase_capacity");
                UnityEditor.EditorPrefs.DeleteKey("VRC.SDKBase_capacity");
            }
            if (UnityEditor.EditorPrefs.HasKey("VRC.SDKBase_content_sex"))
            {
                scene.contentSex = UnityEditor.EditorPrefs.GetBool("VRC.SDKBase_content_sex");
                UnityEditor.EditorPrefs.DeleteKey("VRC.SDKBase_content_sex");
            }
            if (UnityEditor.EditorPrefs.HasKey("VRC.SDKBase_content_violence"))
            {
                scene.contentViolence = UnityEditor.EditorPrefs.GetBool("VRC.SDKBase_content_violence");
                UnityEditor.EditorPrefs.DeleteKey("VRC.SDKBase_content_violence");
            }
            if (UnityEditor.EditorPrefs.HasKey("VRC.SDKBase_content_gore"))
            {
                scene.contentGore = UnityEditor.EditorPrefs.GetBool("VRC.SDKBase_content_gore");
                UnityEditor.EditorPrefs.DeleteKey("VRC.SDKBase_content_gore");
            }
            if (UnityEditor.EditorPrefs.HasKey("VRC.SDKBase_content_other"))
            {
                scene.contentOther = UnityEditor.EditorPrefs.GetBool("VRC.SDKBase_content_other");
                UnityEditor.EditorPrefs.DeleteKey("VRC.SDKBase_content_other");
            }
            if (UnityEditor.EditorPrefs.HasKey("VRC.SDKBase_release_public"))
            {
                scene.releasePublic = UnityEditor.EditorPrefs.GetBool("VRC.SDKBase_release_public");
                UnityEditor.EditorPrefs.DeleteKey("VRC.SDKBase_release_public");
            }

            EditorUtility.SetDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }

    bool ShouldShowLightmapWarning
    {
        get
        {
            const string GraphicsSettingsAssetPath = "ProjectSettings/GraphicsSettings.asset";
            SerializedObject graphicsManager = new SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath(GraphicsSettingsAssetPath)[0]);
            SerializedProperty lightmapStripping = graphicsManager.FindProperty("m_LightmapStripping");
            return lightmapStripping.enumValueIndex == 0;
        }
    }

    bool ShouldShowFogWarning
    {
        get
        {
            const string GraphicsSettingsAssetPath = "ProjectSettings/GraphicsSettings.asset";
            SerializedObject graphicsManager = new SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath(GraphicsSettingsAssetPath)[0]);
            SerializedProperty lightmapStripping = graphicsManager.FindProperty("m_FogStripping");
            return lightmapStripping.enumValueIndex == 0;
        }
    }

    void DrawIssueBox(MessageType msgType, Texture icon, string message, System.Action show, System.Action fix)
    {
        bool haveButtons = ((show != null) || (fix != null));

        GUIStyle style = new GUIStyle("HelpBox");
        style.fixedWidth = (haveButtons ? (SdkWindowWidth - 90) : SdkWindowWidth);
        float minHeight = 40;

        try
        {
            EditorGUILayout.BeginHorizontal();
            if (icon != null)
            {
                GUIContent c = new GUIContent(message, icon);
                float height = style.CalcHeight(c, style.fixedWidth);
                GUILayout.Box(c, style, GUILayout.MinHeight(Mathf.Max(minHeight, height)));
            }
            else
            {
                GUIContent c = new GUIContent(message);
                float height = style.CalcHeight(c, style.fixedWidth);
                Rect rt = GUILayoutUtility.GetRect(c, style, GUILayout.MinHeight(Mathf.Max(minHeight, height)));
                EditorGUI.HelpBox(rt, message, msgType);    // note: EditorGUILayout resulted in uneven button layout in this case
            }

            if (haveButtons)
            {
                EditorGUILayout.BeginVertical();
                float buttonHeight = ((show == null || fix == null) ? minHeight : (minHeight * 0.5f));
                if ((show != null) && GUILayout.Button("Select", GUILayout.Height(buttonHeight)))
                    show();
                if ((fix != null) && GUILayout.Button("Auto Fix", GUILayout.Height(buttonHeight)))
                {
                    fix();
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    checkedForIssues = false;
                    Repaint();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
        }
        catch
        {
            // mutes 'ArgumentException: Getting control 0's position in a group with only 0 controls when doing repaint'
        }
    }

    void OnGuiFixIssuesToBuildOrTest()
    {
        GUIStyle s = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.Space();
        GUILayout.BeginVertical(boxGuiStyle, GUILayout.Height(WARNING_ICON_SIZE), GUILayout.Width(SdkWindowWidth));
        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginHorizontal();
        var textDimensions = s.CalcSize(new GUIContent(FIX_ISSUES_TO_BUILD_OR_TEST_WARNING_STRING));
        GUILayout.Label(new GUIContent(warningIconGraphic), GUILayout.Width(WARNING_ICON_SIZE), GUILayout.Height(WARNING_ICON_SIZE));
        EditorGUILayout.LabelField(FIX_ISSUES_TO_BUILD_OR_TEST_WARNING_STRING, s, GUILayout.Width(textDimensions.x), GUILayout.Height(WARNING_ICON_SIZE));
        EditorGUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
    }

    void OnGUIShowIssues(Object subject = null)
    {
        if (subject == null)
            subject = this;

        EditorGUI.BeginChangeCheck();

        GUIStyle style = GUI.skin.GetStyle("HelpBox");

        if (GUIErrors.ContainsKey(subject))
            foreach (Issue error in GUIErrors[subject].Where(s => !string.IsNullOrEmpty(s.issueText)))
                DrawIssueBox(MessageType.Error, null, error.issueText, error.showThisIssue, error.fixThisIssue);
        if (GUIWarnings.ContainsKey(subject))
            foreach (Issue error in GUIWarnings[subject].Where(s => !string.IsNullOrEmpty(s.issueText)))
                DrawIssueBox(MessageType.Warning, null, error.issueText, error.showThisIssue, error.fixThisIssue);

        if (GUIStats.ContainsKey(subject))
        {
            foreach (var kvp in GUIStats[subject].Where(k => k.performanceRating == PerformanceRating.VeryPoor))
                DrawIssueBox(MessageType.Warning, GetPerformanceIconForRating(kvp.performanceRating), kvp.issueText, kvp.showThisIssue, kvp.fixThisIssue);

            foreach (var kvp in GUIStats[subject].Where(k => k.performanceRating == PerformanceRating.Poor))
                DrawIssueBox(MessageType.Warning, GetPerformanceIconForRating(kvp.performanceRating), kvp.issueText, kvp.showThisIssue, kvp.fixThisIssue);

            foreach (var kvp in GUIStats[subject].Where(k => k.performanceRating == PerformanceRating.Medium))
                DrawIssueBox(MessageType.Warning, GetPerformanceIconForRating(kvp.performanceRating), kvp.issueText, kvp.showThisIssue, kvp.fixThisIssue);

            foreach (var kvp in GUIStats[subject].Where(k => k.performanceRating == PerformanceRating.Good || k.performanceRating == PerformanceRating.Excellent))
                DrawIssueBox(MessageType.Warning, GetPerformanceIconForRating(kvp.performanceRating), kvp.issueText, kvp.showThisIssue, kvp.fixThisIssue);
        }

        if (GUIInfos.ContainsKey(subject))
            foreach (Issue error in GUIInfos[subject].Where(s => !string.IsNullOrEmpty(s.issueText)))
                EditorGUILayout.HelpBox(error.issueText, MessageType.Info);
        if (GUILinks.ContainsKey(subject))
        {
            EditorGUILayout.BeginVertical(style);
            foreach (Issue error in GUILinks[subject].Where(s => !string.IsNullOrEmpty(s.issueText)))
            {
                var s = error.issueText.Split('\n');
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(s[0]);
                if (GUILayout.Button("Open Link", GUILayout.Width(100)))
                    Application.OpenURL(s[1]);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(subject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }

    private Texture GetPerformanceIconForRating(PerformanceRating value)
    {
        if (_perfIcon_Excellent == null)
            _perfIcon_Excellent = Resources.Load<Texture>("PerformanceIcons/Perf_Great_32");
        if (_perfIcon_Good == null)
            _perfIcon_Good = Resources.Load<Texture>("PerformanceIcons/Perf_Good_32");
        if (_perfIcon_Medium == null)
            _perfIcon_Medium = Resources.Load<Texture>("PerformanceIcons/Perf_Medium_32");
        if (_perfIcon_Poor == null)
            _perfIcon_Poor = Resources.Load<Texture>("PerformanceIcons/Perf_Poor_32");
        if (_perfIcon_VeryPoor == null)
            _perfIcon_VeryPoor = Resources.Load<Texture>("PerformanceIcons/Perf_Horrible_32");

        switch (value)
        {
            case PerformanceRating.Excellent:
                return _perfIcon_Excellent;
            case PerformanceRating.Good:
                return _perfIcon_Good;
            case PerformanceRating.Medium:
                return _perfIcon_Medium;
            case PerformanceRating.Poor:
                return _perfIcon_Poor;
            case PerformanceRating.None:
            case PerformanceRating.VeryPoor:
                return _perfIcon_VeryPoor;
        }

        return _perfIcon_Excellent;
    }

    Texture2D CreateBackgroundColorImage(Color color)
    {
        int w = 4, h = 4;
        Texture2D back = new Texture2D(w, h);
        Color[] buffer = new Color[w * h];
        for (int i = 0; i < w; ++i)
            for (int j = 0; j < h; ++j)
                buffer[i + w * j] = color;
        back.SetPixels(buffer);
        back.Apply(false);
        return back;
    }

    void TryMovePostProcessVolumeAwayFromMainCamera()
    {
        if (!Camera.main)
            return;

        if (postProcessVolumeType == null)
            return;
        var oldVolume = Camera.main.GetComponentInChildren(postProcessVolumeType);
        if (!oldVolume)
            return;
        GameObject oldObject = oldVolume.gameObject;
        GameObject newObject = Instantiate(oldObject);
        newObject.name = "Post Processing Volume";
        newObject.tag = "Untagged";
        foreach (Transform child in newObject.transform)
        {
            DestroyImmediate(child.gameObject);
        }
        var newVolume = newObject.GetComponentInChildren(postProcessVolumeType);
        foreach (Component c in newObject.GetComponents<Component>())
        {
            if ((c == newObject.transform) || (c == newVolume))
                continue;
            DestroyImmediate(c);
        }
        DestroyImmediate(oldVolume);
        this.Repaint();
        Selection.activeGameObject = newObject;
    }

    bool IsAudioSource2D(AudioSource src)
    {
        AnimationCurve curve = src.GetCustomCurve(AudioSourceCurveType.SpatialBlend);
        return src.spatialBlend == 0 && (curve == null || curve.keys.Length <= 1);
    }

    void OnGUISceneCheck(VRC.SDKBase.VRC_SceneDescriptor scene)
    {
        CheckUploadChanges(scene);

#if VRC_SDK_VRCSDK2
        if (VRC.Core.APIUser.CurrentUser != null && VRC.Core.APIUser.CurrentUser.hasScriptingAccess && !CustomDLLMaker.DoesScriptDirExist())
            CustomDLLMaker.CreateDirectories();

        // warn those without scripting access if they choose to script locally
        if (VRC.Core.APIUser.CurrentUser != null && !VRC.Core.APIUser.CurrentUser.hasScriptingAccess && CustomDLLMaker.DoesScriptDirExist())
        {
            OnGUIWarning(scene, "Your account does not have permissions to upload custom scripts. You can test locally but need to contact VRChat to publish your world with scripts.", null, null);
        }
#endif
#if !VRC_SDK_VRCSDK2 && !VRC_SDK_VRCSDK3
        bool isSdk3Scene = false;
#elif VRC_SDK_VRCSDK2 && !VRC_SDK_VRCSDK3
        bool isSdk3Scene = false;
#elif !VRC_SDK_VRCSDK2 && VRC_SDK_VRCSDK3
        bool isSdk3Scene = true;
#else
        bool isSdk3Scene = scene as VRC.SDK3.Components.VRCSceneDescriptor != null;
#endif

        var sdkBaseEventHandlers = new List<VRC.SDKBase.VRC_EventHandler>( GameObject.FindObjectsOfType<VRC.SDKBase.VRC_EventHandler>() );
#if VRC_SDK_VRCSDK2
        if (isSdk3Scene == false)
        {
            for (int i = sdkBaseEventHandlers.Count - 1; i >= 0; --i)
                if (sdkBaseEventHandlers[i] as VRCSDK2.VRC_EventHandler)
                    sdkBaseEventHandlers.RemoveAt(i);
        }
#endif
        if (sdkBaseEventHandlers.Count > 0)
        {
            OnGUIError(scene, "You have Event Handlers in your scene that are not allowed in this build configuration.",
                delegate
                {
                    var gos = sdkBaseEventHandlers.ConvertAll<GameObject>(item => (GameObject)item.gameObject);
                    Selection.objects = gos.ToArray();
                },
                delegate
                {
                    foreach (var eh in sdkBaseEventHandlers)
                    {
                        var go = eh.gameObject;
                        DestroyImmediate(eh);
#if VRC_SDK_VRCSDK2
                        if (isSdk3Scene == false)
                        {
                            if (VRCSDK2.VRC_SceneDescriptor.Instance as VRCSDK2.VRC_SceneDescriptor != null)
                                go.AddComponent<VRCSDK2.VRC_EventHandler>();
                        }
#endif
                    }
                });
        }

        Vector3 g = Physics.gravity;
        if (g.x != 0.0f || g.z != 0.0f)
            OnGUIWarning(scene, "Gravity vector is not straight down. Though we support different gravity, player orientation is always 'upwards' so things don't always behave as you intend.",
                delegate { SettingsService.OpenProjectSettings("Project/Physics"); }, null);
        if (g.y > 0)
            OnGUIWarning(scene, "Gravity vector is not straight down, inverted or zero gravity will make walking extremely difficult.",
                delegate { SettingsService.OpenProjectSettings("Project/Physics"); }, null);
        if (g.y == 0)
            OnGUIWarning(scene, "Zero gravity will make walking extremely difficult, though we support different gravity, player orientation is always 'upwards' so this may not have the effect you're looking for.",
                delegate { SettingsService.OpenProjectSettings("Project/Physics"); }, null);

        if (scene.autoSpatializeAudioSources)
        {
            OnGUIWarning(scene, "Your scene previously used the 'Auto Spatialize Audio Sources' feature. This has been deprecated, press 'Fix' to disable. Also, please add VRC_SpatialAudioSource to all your audio sources. Make sure Spatial Blend is set to 3D for the sources you want to spatialize.",
                        null,
                        delegate { scene.autoSpatializeAudioSources = false; }
                        );
        }

        var audioSources = GameObject.FindObjectsOfType<AudioSource>();
        foreach (var a in audioSources)
        {
            if (a.GetComponent<ONSPAudioSource>() != null)
            {
                OnGUIWarning(scene, "Found audio source(s) using ONSP, this is deprecated. Press 'fix' to convert to VRC_SpatialAudioSource.",
                        delegate { Selection.activeObject = a.gameObject; },
                        delegate { Selection.activeObject = a.gameObject; AutoAddSpatialAudioComponents.ConvertONSPAudioSource(a); }
                        );
                break;
            }
            else if (a.GetComponent<VRC.SDKBase.VRC_SpatialAudioSource>() == null)
            {
                string msg = "Found 3D audio source with no VRC Spatial Audio component, this is deprecated. Press 'fix' to add a VRC_SpatialAudioSource.";
                if (IsAudioSource2D(a))
                    msg = "Found 2D audio source with no VRC Spatial Audio component, this is deprecated. Press 'fix' to add a (disabled) VRC_SpatialAudioSource.";

                OnGUIWarning(scene, msg,
                        delegate { Selection.activeObject = a.gameObject; },
                        delegate { Selection.activeObject = a.gameObject; AutoAddSpatialAudioComponents.AddVRCSpatialToBareAudioSource(a); }
                        );
                break;
            }
        }

        if (HasSubstances())
        {
            OnGUIWarning(scene, "One or more scene objects have Substance materials. This is not supported and may break ingame. Please bake your Substances to regular materials.",
                    () => { Selection.objects = GetSubstanceObjects(); },
                    null);
        }

        foreach (VRC.SDKBase.VRC_DataStorage ds in GameObject.FindObjectsOfType<VRC.SDKBase.VRC_DataStorage>())
        {
            VRC.SDKBase.VRC_ObjectSync os = ds.GetComponent<VRC.SDKBase.VRC_ObjectSync>();
            if (os != null && os.SynchronizePhysics)
                OnGUIWarning(scene, ds.name + " has a VRC_DataStorage and VRC_ObjectSync, with SynchronizePhysics enabled.",
                    delegate { Selection.activeObject = os.gameObject; }, null);
        }

        string vrcFilePath = WWW.UnEscapeURL(UnityEditor.EditorPrefs.GetString("lastVRCPath"));
        int fileSize = 0;
        if (!string.IsNullOrEmpty(vrcFilePath) && VRC.ValidationHelpers.CheckIfAssetBundleFileTooLarge(VRC.ContentType.World, vrcFilePath, out fileSize))
        {
            OnGUIWarning(scene, VRC.ValidationHelpers.GetAssetBundleOverSizeLimitMessageSDKWarning(VRC.ContentType.World, fileSize), null, null);
        }

        foreach (GameObject go in FindObjectsOfType<GameObject>())
        {
            if (go.transform.parent == null)
            {
                // check root game objects
#if UNITY_ANDROID
                IEnumerable<Shader> illegalShaders = WorldValidation.FindIllegalShaders(go);
                foreach (Shader s in illegalShaders)
                {
                    OnGUIWarning(scene, "World uses unsupported shader '" + s.name + "'. This could cause low performance or future compatibility issues.", null, null);
                }
#endif
            }
            else
            {
                //if (go.transform.parent.name == "Action-PlayHaptics")
                //    Debug.Log("break");

                // check sibling game objects
                for (int idx = 0; idx < go.transform.parent.childCount; ++idx)
                {
                    Transform t = go.transform.parent.GetChild(idx);
                    if (t == go.transform)
                        continue;
                    else if (t.name == go.transform.name
                            && !(t.GetComponent<VRC.SDKBase.VRC_ObjectSync>()
                                || t.GetComponent<VRC.SDKBase.VRC_SyncAnimation>()
                                || t.GetComponent<VRC.SDKBase.VRC_SyncVideoPlayer>()
                                || t.GetComponent<VRC.SDKBase.VRC_SyncVideoStream>()))
                    {
                        string path = t.name;
                        Transform p = t.parent;
                        while (p != null)
                        {
                            path = p.name + "/" + path;
                            p = p.parent;
                        }

                        OnGUIWarning(scene, "Sibling objects share the same path, which may break network events: " + path,
                            delegate
                            {
                                List<GameObject> gos = new List<GameObject>();
                                for (int c = 0; c < go.transform.parent.childCount; ++c)
                                    if (go.transform.parent.GetChild(c).name == go.name)
                                        gos.Add(go.transform.parent.GetChild(c).gameObject);
                                Selection.objects = gos.ToArray();
                            },
                            delegate
                            {
                                List<GameObject> gos = new List<GameObject>();
                                for (int c = 0; c < go.transform.parent.childCount; ++c)
                                    if (go.transform.parent.GetChild(c).name == go.name)
                                        gos.Add(go.transform.parent.GetChild(c).gameObject);
                                Selection.objects = gos.ToArray();
                                for (int i = 0; i < gos.Count; ++i)
                                    gos[i].name = gos[i].name + "-" + i.ToString("00");
                            });
                        break;
                    }
                }
            }
        }

        // detect dynamic materials and prefabs with identical names (since these could break triggers)
        VRC.SDKBase.VRC_Trigger[] triggers = VRC.Tools.FindSceneObjectsOfTypeAll<VRC.SDKBase.VRC_Trigger>();
        List<GameObject> prefabs = new List<GameObject>();
        List<Material> materials = new List<Material>();

#if VRC_SDK_VRCSDK2
        VRC.AssetExporter.FindDynamicContent(ref prefabs, ref materials);
#elif VRC_SDK_VRCSDK3
            VRC.SDK3.Editor.AssetExporter.FindDynamicContent(ref prefabs, ref materials);
#endif

        foreach (VRC.SDKBase.VRC_Trigger t in triggers)
        {
            foreach (VRC.SDKBase.VRC_Trigger.TriggerEvent triggerEvent in t.Triggers)
            {
                foreach (VRC.SDKBase.VRC_EventHandler.VrcEvent e in triggerEvent.Events.Where(evt => evt.EventType == VRC.SDKBase.VRC_EventHandler.VrcEventType.SpawnObject))
                {
                    GameObject go = AssetDatabase.LoadAssetAtPath(e.ParameterString, typeof(GameObject)) as GameObject;
                    if (go != null)
                    {
                        foreach (GameObject existing in prefabs)
                        {
                            if ((go != existing) && (go.name == existing.name))
                            {
                                OnGUIWarning(scene, "Trigger prefab '" + AssetDatabase.GetAssetPath(go).Replace(".prefab", "") + "' has same name as a prefab in another folder. This may break the trigger.",
                                delegate
                                {
                                    Selection.objects = new GameObject[1] { go };
                                },
                                delegate
                                {
                                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(go), (go.name + "-" + go.GetInstanceID()));
                                    AssetDatabase.Refresh();
                                    e.ParameterString = AssetDatabase.GetAssetPath(go);
                                });
                            }
                        }
                    }
                }

                foreach (VRC.SDKBase.VRC_EventHandler.VrcEvent e in triggerEvent.Events.Where(evt => evt.EventType == VRC.SDKBase.VRC_EventHandler.VrcEventType.SetMaterial))
                {
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(e.ParameterString);
                    if (mat != null && !mat.name.Contains("(Instance)"))
                    {
                        foreach (Material existing in materials)
                        {
                            if ((mat != existing) && (mat.name == existing.name))
                            {
                                OnGUIWarning(scene, "Trigger material '" + AssetDatabase.GetAssetPath(mat).Replace(".mat", "") + "' has same name as a material in another folder. This may break the trigger.",
                                delegate
                                {
                                    Selection.objects = new Material[1] { mat };
                                },
                                delegate
                                {
                                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(mat), (mat.name + "-" + mat.GetInstanceID()));
                                    AssetDatabase.Refresh();
                                    e.ParameterString = AssetDatabase.GetAssetPath(mat);
                                });

                            }
                        }
                    }
                }
            }
        }

    }

    bool OnGUISceneSetup(VRC.SDKBase.VRC_SceneDescriptor scene)
    {
        bool mandatoryExpand = !UpdateLayers.AreLayersSetup() || !UpdateLayers.IsCollisionLayerMatrixSetup();
        if (mandatoryExpand)
            EditorGUILayout.LabelField("VRChat Scene Setup", titleGuiStyle, GUILayout.Height(50));

        if (!UpdateLayers.AreLayersSetup())
        {
            GUILayout.BeginVertical(boxGuiStyle, GUILayout.Height(100), GUILayout.Width(SdkWindowWidth));
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(300));
            EditorGUILayout.Space();
            GUILayout.Label("Layers", infoGuiStyle);
            GUILayout.Label("VRChat scenes must have the same Unity layer configuration as VRChat so we can all predict things like physics and collisions. Pressing this button will configure your project's layers to match VRChat.", infoGuiStyle, GUILayout.Width(300));
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(150));
            GUILayout.Label("", GUILayout.Height(15));
            if (UpdateLayers.AreLayersSetup())
            {
                GUILayout.Label("Step Complete!", infoGuiStyle);
            }
            else if (GUILayout.Button("Setup Layers for VRChat", GUILayout.Width(172)))
            {
                bool doIt = EditorUtility.DisplayDialog("Setup Layers for VRChat", "This adds all VRChat layers to your project and pushes any custom layers down the layer list. If you have custom layers assigned to gameObjects, you'll need to reassign them. Are you sure you want to continue?", "Do it!", "Don't do it");
                if (doIt)
                    UpdateLayers.SetupEditorLayers();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(10);
        }

        if (!UpdateLayers.IsCollisionLayerMatrixSetup())
        {
            GUILayout.BeginVertical(boxGuiStyle, GUILayout.Height(100), GUILayout.Width(SdkWindowWidth));
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(300));
            EditorGUILayout.Space();
            GUILayout.Label("Collision Matrix", infoGuiStyle);
            GUILayout.Label("VRChat uses specific layers for collision. In order for testing and development to run smoothly it is necessary to configure your project's collision matrix to match that of VRChat.", infoGuiStyle, GUILayout.Width(300));
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(150));
            GUILayout.Label("", GUILayout.Height(15));
            if (UpdateLayers.AreLayersSetup() == false)
            {
                GUILayout.Label("You must first configure your layers for VRChat to proceed. Please see above.", infoGuiStyle);
            }
            else if (UpdateLayers.IsCollisionLayerMatrixSetup())
            {
                GUILayout.Label("Step Complete!", infoGuiStyle);
            }
            else
            {
                if (GUILayout.Button("Set Collision Matrix", GUILayout.Width(172)))
                {
                    bool doIt = EditorUtility.DisplayDialog("Setup Collision Layer Matrix for VRChat", "This will setup the correct physics collisions in the PhysicsManager for VRChat layers. Are you sure you want to continue?", "Do it!", "Don't do it");
                    if (doIt)
                    {
                        UpdateLayers.SetupCollisionLayerMatrix();
                    }
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(10);
        }

        return mandatoryExpand;
    }

    void OnGUIAvatarSettings(VRC.SDKBase.VRC_AvatarDescriptor avatar)
    {
        GUILayout.BeginVertical(boxGuiStyle, GUILayout.Width(SdkWindowWidth));

        string name = avatar.gameObject.name;
        if (avatar.apiAvatar != null)
            name = (avatar.apiAvatar as VRC.Core.ApiAvatar).name;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(name, titleGuiStyle);

        VRC.Core.PipelineManager pm = avatar.GetComponent<VRC.Core.PipelineManager>();
        if (pm != null && !string.IsNullOrEmpty(pm.blueprintId))
        {
            if (avatar.apiAvatar == null)
            {
                VRC.Core.ApiAvatar av = VRC.Core.API.FromCacheOrNew<VRC.Core.ApiAvatar>(pm.blueprintId);
                av.Fetch(
                    (c) => avatar.apiAvatar = c.Model as VRC.Core.ApiAvatar,
                    (c) =>
                    {
                        if (c.Code == 404)
                        {
                            VRC.Core.Logger.Log(string.Format("Could not load avatar {0} because it didn't exist.", pm.blueprintId), VRC.Core.DebugLevel.API);
                            VRC.Core.ApiCache.Invalidate<VRC.Core.ApiWorld>(pm.blueprintId);
                        }
                        else
                            Debug.LogErrorFormat("Could not load avatar {0} because {1}", pm.blueprintId, c.Error);
                    });
                avatar.apiAvatar = av;
            }
        }

        if (avatar.apiAvatar != null)
        {
            VRC.Core.ApiAvatar a = (avatar.apiAvatar as VRC.Core.ApiAvatar);
            EditorGUILayout.LabelField("Version: " + a.version.ToString());
            EditorGUILayout.LabelField("Name: " + a.name);
            GUILayout.Label(a.description, infoGuiStyle, GUILayout.Width(400));
            EditorGUILayout.LabelField("Release: " + a.releaseStatus);
            if (a.tags != null)
                foreach (var t in a.tags)
                    EditorGUILayout.LabelField("Tag: " + t);

            if (a.supportedPlatforms == VRC.Core.ApiModel.SupportedPlatforms.Android || a.supportedPlatforms == VRC.Core.ApiModel.SupportedPlatforms.All)
                EditorGUILayout.LabelField("Supports: Android");
            if (a.supportedPlatforms == VRC.Core.ApiModel.SupportedPlatforms.StandaloneWindows || a.supportedPlatforms == VRC.Core.ApiModel.SupportedPlatforms.All)
                EditorGUILayout.LabelField("Supports: Windows");

            //w.imageUrl;
        }
        else
        {
            EditorGUILayout.LabelField("Version: " + "Unpublished");
            EditorGUILayout.LabelField("Name: " + "");
            GUILayout.Label("", infoGuiStyle, GUILayout.Width(400));
            EditorGUILayout.LabelField("Release: " + "");
            //foreach (var t in w.tags)
            //    EditorGUILayout.LabelField("Tag: " + "");

            //if (w.supportedPlatforms == ApiModel.SupportedPlatforms.Android || w.supportedPlatforms == ApiModel.SupportedPlatforms.All)
            //    EditorGUILayout.LabelField("Supports: Android");
            //if (w.supportedPlatforms == ApiModel.SupportedPlatforms.StandaloneWindows || w.supportedPlatforms == ApiModel.SupportedPlatforms.All)
            //    EditorGUILayout.LabelField("Supports: Windows");

            //w.imageUrl;
        }

        GUILayout.EndVertical();
    }

    void OnGUISceneSettings(VRC.SDKBase.VRC_SceneDescriptor scene)
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical(boxGuiStyle, GUILayout.Width(SdkWindowWidth));

        string name = "Unpublished VRChat World";
        if (scene.apiWorld != null)
            name = (scene.apiWorld as VRC.Core.ApiWorld).name;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(name, titleGuiStyle);

        VRC.Core.PipelineManager[] pms = VRC.Tools.FindSceneObjectsOfTypeAll<VRC.Core.PipelineManager>();
        if (pms.Length == 1)
        {
            if (!string.IsNullOrEmpty(pms[0].blueprintId))
            {
                if (scene.apiWorld == null)
                {
                    VRC.Core.ApiWorld world = VRC.Core.API.FromCacheOrNew<VRC.Core.ApiWorld>(pms[0].blueprintId);
                    world.Fetch(null, null,
                        (c) => scene.apiWorld = c.Model as VRC.Core.ApiWorld,
                        (c) =>
                        {
                            if (c.Code == 404)
                            {
                                VRC.Core.Logger.Log(string.Format("Could not load world {0} because it didn't exist.", pms[0].blueprintId), VRC.Core.DebugLevel.All);
                                VRC.Core.ApiCache.Invalidate<VRC.Core.ApiWorld>(pms[0].blueprintId);
                            }
                            else
                                Debug.LogErrorFormat("Could not load world {0} because {1}", pms[0].blueprintId, c.Error);
                        });
                    scene.apiWorld = world;
                }
            }
            else
            {   // clear scene.apiworld if blueprint ID has been detached, so world details in builder panel are also cleared
                scene.apiWorld = null;
            }
        }

        if (scene.apiWorld != null)
        {
            VRC.Core.ApiWorld w = (scene.apiWorld as VRC.Core.ApiWorld);
            EditorGUILayout.LabelField("Version: " + w.version.ToString());
            EditorGUILayout.LabelField("Name: " + w.name);
            GUILayout.Label(w.description, infoGuiStyle, GUILayout.Width(400));
            EditorGUILayout.LabelField("Capacity: " + w.capacity);
            EditorGUILayout.LabelField("Release: " + w.releaseStatus);
            if (w.tags != null)
                foreach (var t in w.tags)
                    EditorGUILayout.LabelField("Tag: " + t);

            if (w.supportedPlatforms == VRC.Core.ApiModel.SupportedPlatforms.Android || w.supportedPlatforms == VRC.Core.ApiModel.SupportedPlatforms.All)
                EditorGUILayout.LabelField("Supports: Android");
            if (w.supportedPlatforms == VRC.Core.ApiModel.SupportedPlatforms.StandaloneWindows || w.supportedPlatforms == VRC.Core.ApiModel.SupportedPlatforms.All)
                EditorGUILayout.LabelField("Supports: Windows");

            //w.imageUrl;
        }
        else
        {
            EditorGUILayout.LabelField("Version: " + "Unpublished");
            EditorGUILayout.LabelField("Name: " + "");
#if UNITY_ANDROID
            EditorGUILayout.LabelField("Platform: " + "Android");
#else
            EditorGUILayout.LabelField("Platform: " + "Windows");
#endif

            GUILayout.Label("", infoGuiStyle, GUILayout.Width(390));
            EditorGUILayout.LabelField("Capacity: " + "");
            EditorGUILayout.LabelField("Release: " + "");
            //foreach (var t in w.tags)
            //    EditorGUILayout.LabelField("Tag: " + "");

            //if (w.supportedPlatforms == ApiModel.SupportedPlatforms.Android || w.supportedPlatforms == ApiModel.SupportedPlatforms.All)
            //    EditorGUILayout.LabelField("Supports: Android");
            //if (w.supportedPlatforms == ApiModel.SupportedPlatforms.StandaloneWindows || w.supportedPlatforms == ApiModel.SupportedPlatforms.All)
            //    EditorGUILayout.LabelField("Supports: Windows");

            //w.imageUrl;
        }

#if VRC_SDK_VRCSDK2
        if (VRC.Core.APIUser.CurrentUser.hasScriptingAccess && VRCSettings.Get().DisplayAdvancedSettings)
#elif VRC_SDK_VRCSDK3
            if (VRC.Core.APIUser.CurrentUser.hasScriptingAccess && VRC.SDK3.Editor.VRCSettings.Get().DisplayAdvancedSettings)
#endif
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            customNamespace = EditorGUILayout.TextField("Custom Namespace", customNamespace);
            if (GUILayout.Button("Regenerate"))
            {
                customNamespace = "vrc" + Path.GetRandomFileName().Replace(".", "");
            }
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    void OnGUIScene(VRC.SDKBase.VRC_SceneDescriptor scene)
    {
        GUILayout.Label("", scrollViewSeparatorStyle);

        builderScrollPos = GUILayout.BeginScrollView(builderScrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Width(SdkWindowWidth), GUILayout.MinHeight(217));

        GUILayout.BeginVertical(boxGuiStyle, GUILayout.Width(SdkWindowWidth));
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(300));
        EditorGUILayout.Space();
        GUILayout.Label("Local Testing", infoGuiStyle);
        GUILayout.Label("Before uploading your world you may build and test it in the VRChat client. You won't be able to invite anyone from online but you can launch multiple of your own clients.", infoGuiStyle);
        GUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.Width(200));
        EditorGUILayout.Space();
        numClients = EditorGUILayout.IntField("Number of Clients", numClients, GUILayout.MaxWidth(190));

        GUI.enabled = (GUIErrors.Count == 0 && checkedForIssues);

        string lastUrl = "";
#if VRC_SDK_VRCSDK2
        lastUrl = VRC_SdkBuilder.GetLastUrl();
#elif VRC_SDK_VRCSDK3
            lastUrl = VRC.SDK3.Editor.VRC_SdkBuilder.GetLastUrl();
#endif

        bool lastBuildPresent = lastUrl != null;
        if (lastBuildPresent == false)
            GUI.enabled = false;
#if VRC_SDK_VRCSDK2
        if (VRCSettings.Get().DisplayAdvancedSettings)
#elif VRC_SDK_VRCSDK3
        if (VRC.SDK3.Editor.VRCSettings.Get().DisplayAdvancedSettings)
#endif
        {
            if (GUILayout.Button("Last Build"))
            {
#if VRC_SDK_VRCSDK2
                VRC_SdkBuilder.shouldBuildUnityPackage = false;
                VRC_SdkBuilder.numClientsToLaunch = numClients;
                VRC_SdkBuilder.RunLastExportedSceneResource();
#elif VRC_SDK_VRCSDK3
                    VRC.SDK3.Editor.VRC_SdkBuilder.shouldBuildUnityPackage = false;
                    VRC.SDK3.Editor.VRC_SdkBuilder.numClientsToLaunch = numClients;
                    VRC.SDK3.Editor.VRC_SdkBuilder.RunLastExportedSceneResource();
#endif
            }
            if (VRC.Core.APIUser.CurrentUser.hasSuperPowers)
            {
                if (GUILayout.Button("Copy Test URL"))
                {
                    TextEditor te = new TextEditor();
                    te.text = lastUrl;
                    te.SelectAll();
                    te.Copy();
                }
            }
        }
        GUI.enabled = (GUIErrors.Count == 0 && checkedForIssues) || VRC.Core.APIUser.CurrentUser.developerType == VRC.Core.APIUser.DeveloperType.Internal;

#if UNITY_ANDROID
        EditorGUI.BeginDisabledGroup(true);
#endif
        if (GUILayout.Button("Build & Test"))
        {
#if VRC_SDK_VRCSDK2
            EnvConfig.ConfigurePlayerSettings();
            VRC_SdkBuilder.shouldBuildUnityPackage = false;
            VRC.AssetExporter.CleanupUnityPackageExport();  // force unity package rebuild on next publish
            VRC_SdkBuilder.numClientsToLaunch = numClients;
            VRC_SdkBuilder.PreBuildBehaviourPackaging();
            VRC_SdkBuilder.ExportSceneResourceAndRun(customNamespace);
#elif VRC_SDK_VRCSDK3
                EnvConfig.ConfigurePlayerSettings();
                VRC.SDK3.Editor.VRC_SdkBuilder.shouldBuildUnityPackage = false;
                VRC.SDK3.Editor.AssetExporter.CleanupUnityPackageExport();  // force unity package rebuild on next publish
                VRC.SDK3.Editor.VRC_SdkBuilder.numClientsToLaunch = numClients;
                VRC.SDK3.Editor.VRC_SdkBuilder.PreBuildBehaviourPackaging();
                VRC.SDK3.Editor.VRC_SdkBuilder.ExportSceneResourceAndRun(customNamespace);
#endif
        }
#if UNITY_ANDROID
        EditorGUI.EndDisabledGroup();
#endif

        GUILayout.EndVertical();

        if (Event.current.type != EventType.Used)
        {
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();
            GUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        GUILayout.BeginVertical(boxGuiStyle, GUILayout.Width(SdkWindowWidth));

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(300));
        EditorGUILayout.Space();
        GUILayout.Label("Online Publishing", infoGuiStyle);
        GUILayout.Label("In order for other people to enter your world in VRChat it must be built and published to our game servers.", infoGuiStyle);
        EditorGUILayout.Space();
        GUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.Width(200));
        EditorGUILayout.Space();

        if (lastBuildPresent == false)
            GUI.enabled = false;
#if VRC_SDK_VRCSDK2
        if (VRCSettings.Get().DisplayAdvancedSettings)
#elif VRC_SDK_VRCSDK3
        if (VRC.SDK3.Editor.VRCSettings.Get().DisplayAdvancedSettings)
#endif
        {
            if (GUILayout.Button("Last Build"))
            {
                if (VRC.Core.APIUser.CurrentUser.canPublishWorlds)
                {
#if VRC_SDK_VRCSDK2
                    VRC_SdkBuilder.shouldBuildUnityPackage = VRCSdkControlPanel.FutureProofPublishEnabled;
                    VRC_SdkBuilder.UploadLastExportedSceneBlueprint();
#elif VRC_SDK_VRCSDK3
                        VRC.SDK3.Editor.VRC_SdkBuilder.shouldBuildUnityPackage = VRCSdkControlPanel.FutureProofPublishEnabled;
                        VRC.SDK3.Editor.VRC_SdkBuilder.UploadLastExportedSceneBlueprint();
#endif
                }
                else
                {
                    ShowContentPublishPermissionsDialog();
                }
            }
        }
        GUI.enabled = (GUIErrors.Count == 0 && checkedForIssues) || VRC.Core.APIUser.CurrentUser.developerType == VRC.Core.APIUser.DeveloperType.Internal;
        if (GUILayout.Button("Build & Publish"))
        {
            if (VRC.Core.APIUser.CurrentUser.canPublishWorlds)
            {
                EnvConfig.ConfigurePlayerSettings();
#if VRC_SDK_VRCSDK2
                VRC_SdkBuilder.shouldBuildUnityPackage = VRCSdkControlPanel.FutureProofPublishEnabled;
                VRC_SdkBuilder.PreBuildBehaviourPackaging();
                VRC_SdkBuilder.ExportAndUploadSceneBlueprint(customNamespace);
#elif VRC_SDK_VRCSDK3
                    VRC.SDK3.Editor.VRC_SdkBuilder.shouldBuildUnityPackage = VRCSdkControlPanel.FutureProofPublishEnabled;
                    VRC.SDK3.Editor.VRC_SdkBuilder.PreBuildBehaviourPackaging();
                    VRC.SDK3.Editor.VRC_SdkBuilder.ExportAndUploadSceneBlueprint(customNamespace);
#endif
            }
            else
            {
                ShowContentPublishPermissionsDialog();
            }
        }
        GUILayout.EndVertical();
        GUI.enabled = true;

        if (Event.current.type != EventType.Used)
        {
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }
    }

    void OnGUISceneLayer(int layer, string name, string description)
    {
        if (LayerMask.LayerToName(layer) != name)
            OnGUIError(null, "Layer " + layer + " must be renamed to '" + name + "'",
                delegate { SettingsService.OpenProjectSettings("Project/Physics"); }, null);

        if (showLayerHelp)
            OnGUIInformation(null, "Layer " + layer + " " + name + "\n" + description);
    }

    bool IsAncestor(Transform ancestor, Transform child)
    {
        bool found = false;
        Transform thisParent = child.parent;
        while (thisParent != null)
        {
            if (thisParent == ancestor) { found = true; break; }
            thisParent = thisParent.parent;
        }

        return found;
    }

    List<Transform> FindBonesBetween(Transform top, Transform bottom)
    {
        List<Transform> list = new List<Transform>();
        if (top == null || bottom == null) return list;
        Transform bt = top.parent;
        while (bt != bottom && bt != null)
        {
            list.Add(bt);
            bt = bt.parent;
        }
        return list;
    }

    bool AnalyzeIK(VRC.SDKBase.VRC_AvatarDescriptor ad, GameObject avObj, Animator anim)
    {
        bool hasHead = false;
        bool hasFeet = false;
        bool hasHands = false;
        bool hasThreeFingers = false;
        //bool hasToes = false;
        bool correctSpineHierarchy = false;
        bool correctLeftArmHierarchy = false;
        bool correctRightArmHierarchy = false;
        bool correctLeftLegHierarchy = false;
        bool correctRightLegHierarchy = false;

        bool status = true;

        Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
        Transform lfoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rfoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        Transform lhand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rhand = anim.GetBoneTransform(HumanBodyBones.RightHand);

        hasHead = null != head;
        hasFeet = (null != lfoot && null != rfoot);
        hasHands = (null != lhand && null != rhand);

        if (!hasHead || !hasFeet || !hasHands)
        {
            OnGUIError(ad, "Humanoid avatar must have head, hands and feet bones mapped.", delegate () { Selection.activeObject = anim.gameObject; }, null);
            return false;
        }

        Transform lthumb = anim.GetBoneTransform(HumanBodyBones.LeftThumbProximal);
        Transform lindex = anim.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
        Transform lmiddle = anim.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
        Transform rthumb = anim.GetBoneTransform(HumanBodyBones.RightThumbProximal);
        Transform rindex = anim.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        Transform rmiddle = anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);

        hasThreeFingers = null != lthumb && null != lindex && null != lmiddle && null != rthumb && null != rindex && null != rmiddle;

        if (!hasThreeFingers)
        {
            // although its only a warning, we return here because the rest
            // of the analysis is for VRIK
            OnGUIWarning(ad, "Thumb, Index, and Middle finger bones are not mapped, Full-Body IK will be disabled.", delegate () { Selection.activeObject = anim.gameObject; }, null);
            status = false;
        }

        Transform pelvis = anim.GetBoneTransform(HumanBodyBones.Hips);
        Transform chest = anim.GetBoneTransform(HumanBodyBones.Chest);
        Transform upperchest = anim.GetBoneTransform(HumanBodyBones.UpperChest);
        Transform torso = anim.GetBoneTransform(HumanBodyBones.Spine);

        Transform neck = anim.GetBoneTransform(HumanBodyBones.Neck);
        Transform lclav = anim.GetBoneTransform(HumanBodyBones.LeftShoulder);
        Transform rclav = anim.GetBoneTransform(HumanBodyBones.RightShoulder);


        if (null == neck || null == lclav || null == rclav || null == pelvis || null == torso || null == chest)
        {
            string missingElements =
            ((null == neck) ? "Neck, " : "") +
            (((null == lclav) || (null == rclav)) ? "Shoulders, " : "") +
            ((null == pelvis) ? "Pelvis, " : "") +
            ((null == torso) ? "Spine, " : "") +
            ((null == chest) ? "Chest, " : "");
            missingElements = missingElements.Remove(missingElements.LastIndexOf(',')) + ".";
            OnGUIError(ad, "Spine hierarchy missing elements, please map: " + missingElements, delegate () { Selection.activeObject = anim.gameObject; }, null);
            return false;
        }

        if (null != upperchest)
            correctSpineHierarchy = lclav.parent == upperchest && rclav.parent == upperchest && neck.parent == upperchest;
        else
            correctSpineHierarchy = lclav.parent == chest && rclav.parent == chest && neck.parent == chest;

        if (!correctSpineHierarchy)
        {
            OnGUIError(ad, "Spine hierarchy incorrect. Make sure that the parent of both Shoulders and the Neck is the Chest (or UpperChest if set).", delegate ()
            {
                List<GameObject> gos = new List<GameObject>();
                gos.Add(lclav.gameObject);
                gos.Add(rclav.gameObject);
                gos.Add(neck.gameObject);
                gos.Add((null != upperchest) ? upperchest.gameObject : chest.gameObject);
                Selection.objects = gos.ToArray();
            }, null);
            return false;
        }

        Transform lshoulder = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform lelbow = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Transform rshoulder = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform relbow = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);

        correctLeftArmHierarchy = (lshoulder && lelbow && (lshoulder.GetChild(0) == lelbow) && lhand && (lelbow.GetChild(0) == lhand));
        correctRightArmHierarchy = (rshoulder && relbow && (rshoulder.GetChild(0) == relbow) && rhand && (relbow.GetChild(0) == rhand));

        if (!(correctLeftArmHierarchy && correctRightArmHierarchy))
        {
            OnGUIWarning(ad, "LowerArm is not first child of UpperArm or Hand is not first child of LowerArm: you may have problems with Forearm rotations.", delegate ()
            {
                List<GameObject> gos = new List<GameObject>();
                if (!correctLeftArmHierarchy && lshoulder)
                    gos.Add(lshoulder.gameObject);
                if (!correctRightArmHierarchy && rshoulder)
                    gos.Add(rshoulder.gameObject);
                if (gos.Count > 0)
                    Selection.objects = gos.ToArray();
                else
                    Selection.activeObject = anim.gameObject;
            }, null);
            status = false;
        }

        Transform lhip = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform lknee = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        Transform rhip = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform rknee = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);

        correctLeftLegHierarchy = lhip && lknee && (lhip.GetChild(0) == lknee) && (lknee.GetChild(0) == lfoot);
        correctRightLegHierarchy = rhip && rknee && (rhip.GetChild(0) == rknee) && (rknee.GetChild(0) == rfoot);

        if (!(correctLeftLegHierarchy && correctRightLegHierarchy))
        {
            OnGUIWarning(ad, "LowerLeg is not first child of UpperLeg or Foot is not first child of LowerLeg: you may have problems with Shin rotations.", delegate ()
            {
                List<GameObject> gos = new List<GameObject>();
                if (!correctLeftLegHierarchy && lhip)
                    gos.Add(lhip.gameObject);
                if (!correctRightLegHierarchy && rhip)
                    gos.Add(rhip.gameObject);
                if (gos.Count > 0)
                    Selection.objects = gos.ToArray();
                else
                    Selection.activeObject = anim.gameObject;
            }, null);
            status = false;
        }

        if (!(IsAncestor(pelvis, rfoot) && IsAncestor(pelvis, lfoot) && IsAncestor(pelvis, lhand) && IsAncestor(pelvis, rhand)))
        {
            OnGUIWarning(ad, "This avatar has a split hierarchy (Hips bone is not the ancestor of all humanoid bones). IK may not work correctly.", delegate ()
            {
                List<GameObject> gos = new List<GameObject>();
                gos.Add(pelvis.gameObject);
                if (!IsAncestor(pelvis, rfoot))
                    gos.Add(rfoot.gameObject);
                if (!IsAncestor(pelvis, lfoot))
                    gos.Add(lfoot.gameObject);
                if (!IsAncestor(pelvis, lhand))
                    gos.Add(lhand.gameObject);
                if (!IsAncestor(pelvis, rhand))
                    gos.Add(rhand.gameObject);
                Selection.objects = gos.ToArray();
            }, null);
            status = false;
        }

        // if thigh bone rotations diverge from 180 from hip bone rotations, full-body tracking/ik does not work well
        if (lhip && rhip)
        {
            Vector3 hipLocalUp = pelvis.InverseTransformVector(Vector3.up);
            Vector3 legLDir = lhip.TransformVector(hipLocalUp);
            Vector3 legRDir = rhip.TransformVector(hipLocalUp);
            float angL = Vector3.Angle(Vector3.up, legLDir);
            float angR = Vector3.Angle(Vector3.up, legRDir);
            if (angL < 175f || angR < 175f)
            {
                string angle = string.Format("{0:F1}", Mathf.Min(angL, angR));
                OnGUIWarning(ad, "The angle between pelvis and thigh bones should be close to 180 degrees (this avatar's angle is " + angle + "). Your avatar may not work well with full-body IK and Tracking.", delegate ()
                {
                    List<GameObject> gos = new List<GameObject>();
                    if (angL < 175f)
                        gos.Add(rfoot.gameObject);
                    if (angR < 175f)
                        gos.Add(lfoot.gameObject);
                    Selection.objects = gos.ToArray();
                }, null);
                status = false;
            }
        }
        return status;
    }

    void ShowRestrictedComponents(IEnumerable<Component> componentsToRemove)
    {
        List<GameObject> gos = new List<GameObject>();
        foreach (var c in componentsToRemove)
            gos.Add(c.gameObject);
        Selection.objects = gos.ToArray();
    }

    void FixRestrictedComponents(IEnumerable<Component> componentsToRemove)
    {
        List<Component> list = (componentsToRemove as List<Component>);
        for (int v = (list.Count - 1); v > -1; v--)
        {
            DestroyImmediate(list[v]);
        }
    }

    void OnGUIAvatarCheck(VRC.SDKBase.VRC_AvatarDescriptor avatar)
    {
        string vrcFilePath = WWW.UnEscapeURL(UnityEditor.EditorPrefs.GetString("currentBuildingAssetBundlePath"));
        int fileSize = 0;
        if (!string.IsNullOrEmpty(vrcFilePath) && VRC.ValidationHelpers.CheckIfAssetBundleFileTooLarge(VRC.ContentType.Avatar, vrcFilePath, out fileSize))
        {
            OnGUIWarning(avatar, VRC.ValidationHelpers.GetAssetBundleOverSizeLimitMessageSDKWarning(VRC.ContentType.Avatar, fileSize), delegate () { Selection.activeObject = avatar.gameObject; }, null);
        }

        AvatarPerformanceStats perfStats = new AvatarPerformanceStats();
        AvatarPerformance.CalculatePerformanceStats(avatar.Name, avatar.gameObject, perfStats);

        OnGUIPerformanceInfo(avatar, perfStats, AvatarPerformanceCategory.Overall, GetAvatarSubSelectAction(avatar, typeof(VRC.SDKBase.VRC_AvatarDescriptor)), null);
        OnGUIPerformanceInfo(avatar, perfStats, AvatarPerformanceCategory.PolyCount, GetAvatarSubSelectAction(avatar, new System.Type[] { typeof(MeshRenderer), typeof(SkinnedMeshRenderer) }), null);
        OnGUIPerformanceInfo(avatar, perfStats, AvatarPerformanceCategory.AABB, GetAvatarSubSelectAction(avatar, typeof(VRC.SDKBase.VRC_AvatarDescriptor)), null);

        if (avatar.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape && avatar.VisemeSkinnedMesh == null)
            OnGUIError(avatar, "This avatar uses Visemes but the Face Mesh is not specified.", delegate () { Selection.activeObject = avatar.gameObject; }, null);

        if (ShaderKeywordsUtility.DetectCustomShaderKeywords(avatar))
            OnGUIWarning(avatar, "A Material on this avatar has custom shader keywords. Please consider optimizing it using the Shader Keywords Utility.",
                () => { Selection.activeObject = avatar.gameObject; },
                () => { EditorApplication.ExecuteMenuItem("VRChat SDK/Utilities/Avatar Shader Keywords Utility"); });

        VerifyAvatarMipMapStreaming(avatar);

        var anim = avatar.GetComponent<Animator>();
        if (anim == null)
        {
            OnGUIWarning(avatar, "This avatar does not contain an Animator, and will not animate in VRChat.", delegate () { Selection.activeObject = avatar.gameObject; }, null);
        }
        else if (anim.isHuman == false)
        {
            OnGUIWarning(avatar, "This avatar is not imported as a humanoid rig and will not play VRChat's provided animation set.", delegate () { Selection.activeObject = avatar.gameObject; }, null);
        }
        else if (avatar.gameObject.activeInHierarchy == false)
        {
            OnGUIError(avatar, "Your avatar is disabled in the scene hierarchy!", delegate () { Selection.activeObject = avatar.gameObject; }, null);
        }
        else
        {
            Transform lfoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rfoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            if ((lfoot == null) || (rfoot == null))
                OnGUIError(avatar, "Your avatar is humanoid, but its feet aren't specified!", delegate () { Selection.activeObject = avatar.gameObject; }, null);
            if (lfoot != null && rfoot != null)
            {
                Vector3 footPos = lfoot.position - avatar.transform.position;
                if (footPos.y < 0)
                    OnGUIWarning(avatar, "Avatar feet are beneath the avatar's origin (the floor). That's probably not what you want.", delegate ()
                    {
                        List<GameObject> gos = new List<GameObject>();
                        gos.Add(rfoot.gameObject);
                        gos.Add(lfoot.gameObject);
                        Selection.objects = gos.ToArray();
                    }, null);
            }

            Transform lshoulder = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rshoulder = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if ((lshoulder == null) || (rshoulder == null))
                OnGUIError(avatar, "Your avatar is humanoid, but its upper arms aren't specified!", delegate () { Selection.activeObject = avatar.gameObject; }, null);
            if (lshoulder != null && rshoulder != null)
            {
                Vector3 shoulderPosition = lshoulder.position - avatar.transform.position;
                if (shoulderPosition.y < 0.2f)
                    OnGUIError(avatar, "This avatar is too short. The minimum is 20cm shoulder height.", delegate () { Selection.activeObject = avatar.gameObject; }, null);
                else if (shoulderPosition.y < 1.0f)
                    OnGUIWarning(avatar, "This avatar is short. This is probably shorter than you want.", delegate () { Selection.activeObject = avatar.gameObject; }, null);
                else if (shoulderPosition.y > 5.0f)
                    OnGUIWarning(avatar, "This avatar is too tall. The maximum is 5m shoulder height.", delegate () { Selection.activeObject = avatar.gameObject; }, null);
                else if (shoulderPosition.y > 2.5f)
                    OnGUIWarning(avatar, "This avatar is tall. This is probably taller than you want.", delegate () { Selection.activeObject = avatar.gameObject; }, null);
            }

            if (AnalyzeIK(avatar, avatar.gameObject, anim) == false)
                OnGUILink(avatar, "See Avatar Rig Requirements for more information.", kAvatarRigRequirementsURL);
        }

        IEnumerable<Component> componentsToRemove = AvatarValidation.FindIllegalComponents(avatar.gameObject);
        HashSet<string> componentsToRemoveNames = new HashSet<string>();
        foreach (Component c in componentsToRemove)
        {
            if (componentsToRemoveNames.Contains(c.GetType().Name) == false)
                componentsToRemoveNames.Add(c.GetType().Name);
        }

        if (componentsToRemoveNames.Count > 0)
            OnGUIError(avatar, "The following component types are found on the Avatar and will be removed by the client: " + string.Join(", ", componentsToRemoveNames.ToArray()), delegate () { ShowRestrictedComponents(componentsToRemove); }, delegate () { FixRestrictedComponents(componentsToRemove); });

        List<AudioSource> audioSources = avatar.gameObject.GetComponentsInChildren<AudioSource>(true).ToList<AudioSource>();
        if (audioSources.Count > 0)
            OnGUIWarning(avatar, "Audio sources found on Avatar, they will be adjusted to safe limits, if necessary.",
                GetAvatarSubSelectAction(avatar, typeof(AudioSource)), null);

        List<VRC.SDKBase.VRCStation> stations = avatar.gameObject.GetComponentsInChildren<VRC.SDKBase.VRCStation>(true).ToList<VRC.SDKBase.VRCStation>();
        if (stations.Count > 0)
            OnGUIWarning(avatar, "Stations found on Avatar, they will be adjusted to safe limits, if necessary.",
                GetAvatarSubSelectAction(avatar, typeof(VRC.SDKBase.VRCStation)), null);

        if (HasSubstances(avatar.gameObject))
        {
            OnGUIWarning(avatar, "This avatar has one or more Substance materials, which is not supported and may break ingame. Please bake your Substances to regular materials.",
                    () => { Selection.objects = GetSubstanceObjects(avatar.gameObject); },
                    null);
        }

#if UNITY_ANDROID
        IEnumerable<Shader> illegalShaders = AvatarValidation.FindIllegalShaders(avatar.gameObject);
        foreach (Shader s in illegalShaders)
        {
            OnGUIError(avatar, "Avatar uses unsupported shader '" + s.name + "'. You can only use the shaders provided in 'VRChat/Mobile' for Quest avatars.", delegate () { Selection.activeObject = avatar.gameObject; }, null);
        }
#endif

        foreach (AvatarPerformanceCategory perfCategory in System.Enum.GetValues(typeof(AvatarPerformanceCategory)))
        {
            if (perfCategory == AvatarPerformanceCategory.Overall ||
                perfCategory == AvatarPerformanceCategory.PolyCount ||
                perfCategory == AvatarPerformanceCategory.AABB ||
                perfCategory == AvatarPerformanceCategory.AvatarPerformanceCategoryCount)
            {
                continue;
            }

            System.Action show = null;

            switch (perfCategory)
            {
                case AvatarPerformanceCategory.AnimatorCount: show = GetAvatarSubSelectAction(avatar, typeof(Animator)); break;
                case AvatarPerformanceCategory.AudioSourceCount: show = GetAvatarSubSelectAction(avatar, typeof(AudioSource)); break;
                case AvatarPerformanceCategory.BoneCount: show = GetAvatarSubSelectAction(avatar, typeof(SkinnedMeshRenderer)); break;
                case AvatarPerformanceCategory.ClothCount: show = GetAvatarSubSelectAction(avatar, typeof(Cloth)); break;
                case AvatarPerformanceCategory.ClothMaxVertices: show = GetAvatarSubSelectAction(avatar, typeof(Cloth)); break;
                case AvatarPerformanceCategory.LightCount: show = GetAvatarSubSelectAction(avatar, typeof(Light)); break;
                case AvatarPerformanceCategory.LineRendererCount: show = GetAvatarSubSelectAction(avatar, typeof(LineRenderer)); break;
                case AvatarPerformanceCategory.MaterialCount: show = GetAvatarSubSelectAction(avatar, new System.Type[] { typeof(MeshRenderer), typeof(SkinnedMeshRenderer) }); break;
                case AvatarPerformanceCategory.MeshCount: show = GetAvatarSubSelectAction(avatar, new System.Type[] { typeof(MeshRenderer), typeof(SkinnedMeshRenderer) }); break;
                case AvatarPerformanceCategory.ParticleCollisionEnabled: show = GetAvatarSubSelectAction(avatar, typeof(ParticleSystem)); break;
                case AvatarPerformanceCategory.ParticleMaxMeshPolyCount: show = GetAvatarSubSelectAction(avatar, typeof(ParticleSystem)); break;
                case AvatarPerformanceCategory.ParticleSystemCount: show = GetAvatarSubSelectAction(avatar, typeof(ParticleSystem)); break;
                case AvatarPerformanceCategory.ParticleTotalCount: show = GetAvatarSubSelectAction(avatar, typeof(ParticleSystem)); break;
                case AvatarPerformanceCategory.ParticleTrailsEnabled: show = GetAvatarSubSelectAction(avatar, typeof(ParticleSystem)); break;
                case AvatarPerformanceCategory.PhysicsColliderCount: show = GetAvatarSubSelectAction(avatar, typeof(Collider)); break;
                case AvatarPerformanceCategory.PhysicsRigidbodyCount: show = GetAvatarSubSelectAction(avatar, typeof(Rigidbody)); break;
                case AvatarPerformanceCategory.PolyCount: show = GetAvatarSubSelectAction(avatar, new System.Type[] { typeof(MeshRenderer), typeof(SkinnedMeshRenderer) }); break;
                case AvatarPerformanceCategory.SkinnedMeshCount: show = GetAvatarSubSelectAction(avatar, typeof(SkinnedMeshRenderer)); break;
                case AvatarPerformanceCategory.TrailRendererCount: show = GetAvatarSubSelectAction(avatar, typeof(TrailRenderer)); break;
            }

            // we can only show these buttons if DynamicBone is installed

            System.Type dynamicBoneType = typeof(VRCSDK2.Validation.AvatarValidation).Assembly.GetType("DynamicBone");
            System.Type dynamicBoneColliderType = typeof(VRCSDK2.Validation.AvatarValidation).Assembly.GetType("DynamicBoneCollider");
            if ((dynamicBoneType != null) && (dynamicBoneColliderType != null))
            {
                switch (perfCategory)
                {
                    case AvatarPerformanceCategory.DynamicBoneColliderCount: show = GetAvatarSubSelectAction(avatar, dynamicBoneColliderType); break;
                    case AvatarPerformanceCategory.DynamicBoneCollisionCheckCount: show = GetAvatarSubSelectAction(avatar, dynamicBoneColliderType); break;
                    case AvatarPerformanceCategory.DynamicBoneComponentCount: show = GetAvatarSubSelectAction(avatar, dynamicBoneType); break;
                    case AvatarPerformanceCategory.DynamicBoneSimulatedBoneCount: show = GetAvatarSubSelectAction(avatar, dynamicBoneType); break;
                }
            }

            OnGUIPerformanceInfo(avatar, perfStats, perfCategory, show, null);
        }

        OnGUILink(avatar, "Avatar Optimization Tips", kAvatarOptimizationTipsURL);
    }

    GameObject[] GetSubstanceObjects(GameObject obj = null, bool earlyOut = false)
    {
        // if 'obj' is null we check entire scene
        // if 'earlyout' is true we only return 1st object (to detect if substances are present)

        var objs = new List<GameObject>();
        Renderer[] renderers;

        renderers = (obj ? obj.GetComponentsInChildren<Renderer>(true) : FindObjectsOfType<Renderer>());

        if ((renderers == null) || (renderers.Length < 1))
            return null;
        foreach (Renderer r in renderers)
        {
            if (r.sharedMaterials.Length < 1)
                continue;
            foreach (Material m in r.sharedMaterials)
            {
                if (!m)
                    continue;
                string path = AssetDatabase.GetAssetPath(m);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (path.EndsWith(".sbsar", true, System.Globalization.CultureInfo.InvariantCulture))
                {
                    objs.Add(r.gameObject);
                    if (earlyOut)
                        return objs.ToArray();
                }
            }
        }
        if (objs.Count < 1)
            return null;

        return objs.ToArray();
    }

    bool HasSubstances(GameObject obj = null)
    {
        return (GetSubstanceObjects(obj, true) != null);
    }

    void VerifyAvatarMipMapStreaming(VRC.SDKBase.VRC_AvatarDescriptor avatar)
    {
        List<TextureImporter> badTextures = new List<TextureImporter>();
        foreach (Renderer r in avatar.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (!m)
                    continue;
                int[] texIDs = m.GetTexturePropertyNameIDs();
                if (texIDs == null)
                    continue;
                foreach (int i in texIDs)
                {
                    Texture t = m.GetTexture(i);
                    if (!t)
                        continue;
                    string path = AssetDatabase.GetAssetPath(t);
                    if (string.IsNullOrEmpty(path))
                        continue;
                    TextureImporter importer = (TextureImporter.GetAtPath(path) as TextureImporter);
                    if (importer && importer.mipmapEnabled && !importer.streamingMipmaps)
                        badTextures.Add(importer);
                }
            }
        }

        if (badTextures.Count == 0)
            return;

        OnGUIError(avatar, "This avatar has mipmapped textures without 'Streaming Mip Maps' enabled.",
        () => { Selection.objects = badTextures.ToArray(); },
        () =>
        {
            List<string> paths = new List<string>();
            foreach (TextureImporter t in badTextures)
            {
                Undo.RecordObject(t, "Set Mip Map Streaming");
                t.streamingMipmaps = true;
                t.streamingMipmapsPriority = 0;
                EditorUtility.SetDirty(t);
                paths.Add(t.assetPath);
            }
            AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
            AssetDatabase.Refresh();
        });

    }

    System.Action GetAvatarSubSelectAction(VRC.SDKBase.VRC_AvatarDescriptor avatar, System.Type[] types)
    {
        return () =>
        {
            var gos = new List<GameObject>();
            foreach (System.Type t in types)
            {
                Component[] components = avatar.GetComponentsInChildren(t, true);
                foreach (Component c in components)
                    gos.Add(c.gameObject);
            }
            if ((gos != null) && (gos.Count > 0))
                Selection.objects = gos.ToArray();
            else
                Selection.objects = new Object[] { avatar.gameObject };
        };
    }

    System.Action GetAvatarSubSelectAction(VRC.SDKBase.VRC_AvatarDescriptor avatar, System.Type type)
    {
        var t = new List<System.Type>();
        t.Add(type);
        return GetAvatarSubSelectAction(avatar, t.ToArray());
    }

    EditorWindow GetLightingWindow()
    {
        var editorAsm = typeof(UnityEditor.Editor).Assembly;
        return EditorWindow.GetWindow(editorAsm.GetType("UnityEditor.LightingWindow"));
    }

    void OnGUIPerformanceInfo(VRC.SDKBase.VRC_AvatarDescriptor avatar, AvatarPerformanceStats perfStats, AvatarPerformanceCategory perfCategory, System.Action show, System.Action fix)
    {
        string text;
        PerformanceInfoDisplayLevel displayLevel;
        PerformanceRating rating = perfStats.GetPerformanceRatingForCategory(perfCategory);
        SDKPerformanceDisplay.GetSDKPerformanceInfoText(perfStats, perfCategory, out text, out displayLevel);

        switch (displayLevel)
        {
            case PerformanceInfoDisplayLevel.None:
                {
                    break;
                }
            case PerformanceInfoDisplayLevel.Verbose:
                {
                    if (showAvatarPerformanceDetails)
                    {
                        OnGUIStat(avatar, text, rating, show, fix);
                    }
                    break;
                }
            case PerformanceInfoDisplayLevel.Info:
                {
                    OnGUIStat(avatar, text, rating, show, fix);
                    break;
                }
            case PerformanceInfoDisplayLevel.Warning:
                {
                    OnGUIStat(avatar, text, rating, show, fix);
                    break;
                }
            case PerformanceInfoDisplayLevel.Error:
                {
                    OnGUIStat(avatar, text, rating, show, fix);
                    OnGUIError(avatar, text, delegate () { Selection.activeObject = avatar.gameObject; }, null);
                    break;
                }
            default:
                {
                    OnGUIError(avatar, "Unknown performance display level.", delegate () { Selection.activeObject = avatar.gameObject; }, null);
                    break;
                }
        }
    }

    void OnGUIAvatar(VRC.SDKBase.VRC_AvatarDescriptor avatar)
    {
        GUILayout.Label("", scrollViewSeparatorStyle);
        EditorGUILayout.Space();

        //GUILayout.BeginVertical(boxGuiStyle, GUILayout.Width(SdkWindowWidth));

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(300));
        EditorGUILayout.Space();
        GUILayout.Label("Online Publishing", infoGuiStyle);
        GUILayout.Label("In order for other people to see your avatar in VRChat it must be built and published to our game servers.", infoGuiStyle);
        GUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.Width(200));
        EditorGUILayout.Space();

        GUI.enabled = (GUIErrors.Count == 0 && checkedForIssues) || VRC.Core.APIUser.CurrentUser.developerType == VRC.Core.APIUser.DeveloperType.Internal;
        if (GUILayout.Button("Build & Publish"))
        {
            if (VRC.Core.APIUser.CurrentUser.canPublishAvatars)
            {
                EnvConfig.ForceEnableFog();
#if VRC_SDK_VRCSDK2
                VRC_SdkBuilder.shouldBuildUnityPackage = VRCSdkControlPanel.FutureProofPublishEnabled;
                VRC_SdkBuilder.ExportAndUploadAvatarBlueprint(avatar.gameObject);
#elif VRC_SDK_VRCSDK3
                    VRC.SDK3.Editor.VRC_SdkBuilder.shouldBuildUnityPackage = VRCSdkControlPanel.FutureProofPublishEnabled;
                    VRC.SDK3.Editor.VRC_SdkBuilder.ExportAndUploadAvatarBlueprint(avatar.gameObject);
#endif
            }
            else
            {
                ShowContentPublishPermissionsDialog();
            }
        }
        GUI.enabled = true;
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    public static void ShowContentPublishPermissionsDialog()
    {
        if (!VRC.Core.RemoteConfig.IsInitialized())
        {
            VRC.Core.RemoteConfig.Init(() => ShowContentPublishPermissionsDialog());
            return;
        }

        string message = VRC.Core.RemoteConfig.GetString("sdkNotAllowedToPublishMessage");
        int result = UnityEditor.EditorUtility.DisplayDialogComplex("VRChat SDK", message, "Developer FAQ", "VRChat Discord", "OK");
        if (result == 0)
        {
            ShowDeveloperFAQ();
        }
        if (result == 1)
        {
            ShowVRChatDiscord();
        }
    }

    static System.Type postProcessVolumeType;
    static void DetectPostProcessingPackage()
    {
        postProcessVolumeType = null;
        try
        {
            System.Reflection.Assembly postProcAss = System.Reflection.Assembly.Load("Unity.PostProcessing.Runtime");
            postProcessVolumeType = postProcAss.GetType("UnityEngine.Rendering.PostProcessing.PostProcessVolume");
        }
        catch
        {
            // -> post processing not installed
        }
    }

}
