using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEditor;

public class VRCSdk3Analysis
{
    static Assembly GetAssemblyByName(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies().
               SingleOrDefault(assembly => assembly.GetName().Name == name);
    }

    static List<Component> GetSceneComponentsFromAssembly( Assembly assembly )
    {
        if (assembly == null)
            return new List<Component>();

        Type[] types = assembly.GetTypes();

        List<Component> present = new List<Component>();
        foreach (var type in types )
        {
            if (!type.IsSubclassOf(typeof(MonoBehaviour)))
                continue;

            var monos = VRC.Tools.FindSceneObjectsOfTypeAll(type);
            present.AddRange(monos);
        }
        return present;
    }

    public enum SdkVersion
    {
        VRCSDK2,
        VRCSDK3
    };

    public static List<Component> GetSDKInScene(SdkVersion version)
    {
        var assembly = GetAssemblyByName( version.ToString() );
        return GetSceneComponentsFromAssembly(assembly);
    }

    public static bool IsSdkDllActive(SdkVersion version)
    {
        string assembly = version.ToString();
        PluginImporter importer = AssetImporter.GetAtPath("Assets/VRCSDK/Plugins/" + assembly + ".dll") as PluginImporter;
        if (importer == false)
            return false;
        return importer.GetCompatibleWithAnyPlatform();
    }

    //public static void SetSdkVersionActive(SdkVersion version)
    //{
    //    AssetDatabase.StartAssetEditing();

    //    PluginImporter importer = AssetImporter.GetAtPath("Assets/VRCSDK/Plugins/" + version + ".dll") as PluginImporter;
    //    if (importer == null)
    //        return;

    //    SetSDKDefine(version);

    //    var other = SdkVersion.VRCSDK2;
    //    if (version == SdkVersion.VRCSDK2)
    //        other = SdkVersion.VRCSDK3;

    //    SetSDKAssembly(other, false);
    //    SetSDKAssembly(version, true);

    //    AssetDatabase.StopAssetEditing();
    //}

    //static void SetSDKAssembly( SdkVersion version, bool active )
    //{
    //    string assembly = version.ToString();
    //    PluginImporter importer = AssetImporter.GetAtPath("Assets/VRCSDK/Plugins/" + assembly + ".dll") as PluginImporter;
    //    if (importer)
    //    {
    //        importer.SetCompatibleWithAnyPlatform(active);
    //        importer.SetCompatibleWithEditor(active);
    //        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, active);
    //        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, active);
    //        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux, active);
    //        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, active);
    //        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, active);
    //        importer.SetCompatibleWithPlatform(BuildTarget.Android, active);
    //        importer.SaveAndReimport();
    //    }
    //}

    //static void SetSDKDefine( SdkVersion version )
    //{
    //    string defineString = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
    //    var defs = defineString.Split(';');
    //    string finalDefs = null;
    //    foreach( var s in defs )
    //    {
    //        if (s != "VRC_SDK_VRCSDK2" && s != "VRC_SDK_VRCSDK3")
    //        {
    //            if (finalDefs == null)
    //                finalDefs = s;
    //            else
    //                finalDefs += ";" + s;
    //        }
    //    }
    //    if (version == SdkVersion.VRCSDK2)
    //        finalDefs += ";VRC_SDK_VRCSDK2";
    //    else if (version == SdkVersion.VRCSDK3)
    //        finalDefs += ";VRC_SDK_VRCSDK3";
    //    PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, finalDefs);
    //}
}
