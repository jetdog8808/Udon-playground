using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC;
using VRC.Core;
using VRC.SDKBase;

public partial class VRCSdkControlPanel : EditorWindow
{
    [MenuItem("VRChat SDK/Help/Developer FAQ")]
    public static void ShowDeveloperFAQ()
    {
        if (!RemoteConfig.IsInitialized())
        {
            RemoteConfig.Init(() => ShowDeveloperFAQ());
            return;
        }

        Application.OpenURL(RemoteConfig.GetString("sdkDeveloperFaqUrl"));
    }

    [MenuItem("VRChat SDK/Help/VRChat Discord")]
    public static void ShowVRChatDiscord()
    {
        if (!RemoteConfig.IsInitialized())
        {
            RemoteConfig.Init(() => ShowVRChatDiscord());
            return;
        }

        Application.OpenURL(RemoteConfig.GetString("sdkDiscordUrl"));
    }

    [MenuItem("VRChat SDK/Help/Avatar Optimization Tips")]
    public static void ShowAvatarOptimizationTips()
    {
        if (!RemoteConfig.IsInitialized())
        {
            RemoteConfig.Init(() => ShowAvatarOptimizationTips());
            return;
        }

        Application.OpenURL(kAvatarOptimizationTipsURL);
    }

    [MenuItem("VRChat SDK/Help/Avatar Rig Requirements")]
    public static void ShowAvatarRigRequirements()
    {
        if (!RemoteConfig.IsInitialized())
        {
            RemoteConfig.Init(() => ShowAvatarRigRequirements());
            return;
        }

        Application.OpenURL(kAvatarRigRequirementsURL);
    }
}
