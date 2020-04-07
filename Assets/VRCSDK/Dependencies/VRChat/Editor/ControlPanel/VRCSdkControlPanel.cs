using UnityEditor;
using UnityEngine;
using VRC.Core;

[ExecuteInEditMode]
public partial class VRCSdkControlPanel : EditorWindow
{
    public static VRCSdkControlPanel window;

    [MenuItem("VRChat SDK/Show Control Panel", false, 600)]
    static void ShowControlPanel()
    {
        if (!RemoteConfig.IsInitialized())
        {
            VRC.Core.API.SetOnlineMode(true, "vrchat");
            RemoteConfig.Init(() => ShowControlPanel());
            return;
        }

        window = (VRCSdkControlPanel)EditorWindow.GetWindow(typeof(VRCSdkControlPanel));
        window.titleContent.text = "VRChat SDK";
        window.minSize = new Vector2(SdkWindowWidth + 4, 600);
        window.maxSize = new Vector2(SdkWindowWidth + 4, 2000);
        window.Init();
        window.Show();
    }

    void Update()
    {
        Repaint();
    }

    static GUIStyle titleGuiStyle;
    static GUIStyle boxGuiStyle;
    static GUIStyle infoGuiStyle;
    static GUIStyle listButtonStyleEven;
    static GUIStyle listButtonStyleOdd;
    static GUIStyle listButtonStyleSelected;
    static GUIStyle scrollViewSeparatorStyle;
    static GUIStyle searchBarStyle;

    void InitializeStyles()
    {
        titleGuiStyle = new GUIStyle();
        titleGuiStyle.fontSize = 15;
        titleGuiStyle.fontStyle = FontStyle.BoldAndItalic;
        titleGuiStyle.alignment = TextAnchor.MiddleCenter;
        titleGuiStyle.wordWrap = true;
        if (EditorGUIUtility.isProSkin)
            titleGuiStyle.normal.textColor = Color.white;
        else
            titleGuiStyle.normal.textColor = Color.black;

        boxGuiStyle = new GUIStyle();
        if (EditorGUIUtility.isProSkin)
        {
            boxGuiStyle.normal.background = CreateBackgroundColorImage(new Color(0.3f, 0.3f, 0.3f));
            boxGuiStyle.normal.textColor = Color.white;
        }
        else
        {
            boxGuiStyle.normal.background = CreateBackgroundColorImage(new Color(0.85f, 0.85f, 0.85f));
            boxGuiStyle.normal.textColor = Color.black;
        }

        infoGuiStyle = new GUIStyle();
        infoGuiStyle.wordWrap = true; ;
        if (EditorGUIUtility.isProSkin)
            infoGuiStyle.normal.textColor = Color.white;
        else
            infoGuiStyle.normal.textColor = Color.black;
        infoGuiStyle.margin = new RectOffset(10, 10, 10, 10);

        listButtonStyleEven = new GUIStyle();
        listButtonStyleEven.margin = new RectOffset(0, 0, 0, 0);
        listButtonStyleEven.border = new RectOffset(0, 0, 0, 0);
        if (EditorGUIUtility.isProSkin)
        {
            listButtonStyleEven.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            listButtonStyleEven.normal.background = CreateBackgroundColorImage(new Color(0.540f, 0.540f, 0.54f));
        }
        else
        {
            listButtonStyleEven.normal.textColor = Color.black;
            listButtonStyleEven.normal.background = CreateBackgroundColorImage(new Color(0.85f, 0.85f, 0.85f));
        }

        listButtonStyleOdd = new GUIStyle();
        listButtonStyleOdd.margin = new RectOffset(0, 0, 0, 0);
        listButtonStyleOdd.border = new RectOffset(0, 0, 0, 0);
        if (EditorGUIUtility.isProSkin)
        {
            listButtonStyleOdd.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            //listButtonStyleOdd.normal.background = CreateBackgroundColorImage(new Color(0.50f, 0.50f, 0.50f));
        }
        else
        {
            listButtonStyleOdd.normal.textColor = Color.black;
            listButtonStyleOdd.normal.background = CreateBackgroundColorImage(new Color(0.90f, 0.90f, 0.90f));
        }

        listButtonStyleSelected = new GUIStyle();
        listButtonStyleSelected.normal.textColor = Color.white;
        listButtonStyleSelected.margin = new RectOffset(0, 0, 0, 0);
        if (EditorGUIUtility.isProSkin)
        {
            listButtonStyleSelected.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            listButtonStyleSelected.normal.background = CreateBackgroundColorImage(new Color(0.4f, 0.4f, 0.4f));
        }
        else
        {
            listButtonStyleSelected.normal.textColor = Color.black;
            listButtonStyleSelected.normal.background = CreateBackgroundColorImage(new Color(0.75f, 0.75f, 0.75f));
        }

        scrollViewSeparatorStyle = new GUIStyle("Toolbar");
        scrollViewSeparatorStyle.fixedWidth = SdkWindowWidth + 10;
        scrollViewSeparatorStyle.fixedHeight = 4;
        scrollViewSeparatorStyle.margin.top = 1;

        searchBarStyle = new GUIStyle("Toolbar");
        searchBarStyle.fixedWidth = SdkWindowWidth;
        searchBarStyle.fixedHeight = 23;
        searchBarStyle.padding.top = 3;

    }

    void Init()
    {
        InitializeStyles();
        ResetIssues();
        InitAccount();
    }

    void OnEnable()
    {
        OnEnableAccount();
        AssemblyReloadEvents.afterAssemblyReload += BuilderAssemblyReload;
    }

    void OnDisable()
    {
        AssemblyReloadEvents.afterAssemblyReload -= BuilderAssemblyReload;
    }

    void OnDestroy()
    {
        AccountDestroy();
    }

    const int SdkWindowWidth = 518;

    void OnGUI()
    {
        if (window == null)
        {
            window = (VRCSdkControlPanel)EditorWindow.GetWindow(typeof(VRCSdkControlPanel));
            InitializeStyles();
        }

        if (_bannerImage == null)
            _bannerImage = AssetDatabase.LoadAssetAtPath("Assets/VRCSDK/Dependencies/VRChat/SdkGraphics/SDK_Panel_Banner.png", typeof(Texture2D)) as Texture2D;

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();

        GUILayout.Box(_bannerImage);

        if (Application.isPlaying)
        {
            GUI.enabled = false;
            GUILayout.Space(20);
            EditorGUILayout.LabelField("Unity Application is running ...\nStop it to access the Control Panel", titleGuiStyle, GUILayout.Width(SdkWindowWidth));
            GUI.enabled = true;
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return;
        }

        EditorGUILayout.Space();

        EnvConfig.SetActiveSDKDefines();

        #if VRC_SDK_VRCSDK2
            VRCSettings.Get().activeWindowPanel = GUILayout.Toolbar(VRCSettings.Get().activeWindowPanel, new string[] { "Authentication", "Builder", "Content Manager", "Settings" }, GUILayout.Width(SdkWindowWidth));
            int showPanel = VRCSettings.Get().activeWindowPanel;
        #elif VRC_SDK_VRCSDK3
            VRC.SDK3.Editor.VRCSettings.Get().activeWindowPanel = GUILayout.Toolbar(VRC.SDK3.Editor.VRCSettings.Get().activeWindowPanel, new string[] { "Authentication", "Builder", "Content Manager", "Settings" }, GUILayout.Width(SdkWindowWidth));
            int showPanel = VRC.SDK3.Editor.VRCSettings.Get().activeWindowPanel;
        #else
            int showPanel = 0;
        #endif

        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (APIUser.IsLoggedInWithCredentials == false && showPanel != 3)
            showPanel = 0;

        switch (showPanel)
        {
            case 1:
                ShowBuilder();
                break;
            case 2:
                ShowContent();
                break;
            case 3:
                ShowSettings();
                break;
            case 0:
            default:
                ShowAccount();
                break;
        }
    }

    [UnityEditor.Callbacks.PostProcessScene]
    static void OnPostProcessScene()
    {
        if (window != null)
            window.Reset();
    }

    private void OnFocus()
    {
        Reset();
    }

    public void Reset()
    {
        ResetIssues();
        // style backgrounds may be nulled on scene load. detect if so has happened
        if((boxGuiStyle != null) && (boxGuiStyle.normal.background == null))
            InitializeStyles();
    }

    [UnityEditor.Callbacks.DidReloadScripts(int.MaxValue)]
    static void DidReloadScripts()
    {
        RefreshApiUrlSetting();
        DetectPostProcessingPackage();
    }
}
