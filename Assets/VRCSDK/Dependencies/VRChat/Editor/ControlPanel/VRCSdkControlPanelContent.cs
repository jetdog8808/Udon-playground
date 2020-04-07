using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using VRC.Core;

public partial class VRCSdkControlPanel : EditorWindow
{
    const int PageLimit = 20;

    static List<ApiAvatar> uploadedAvatars = null;
    static List<ApiWorld> uploadedWorlds = null;

    public static Dictionary<string, Texture2D> ImageCache = new Dictionary<string, Texture2D>();

    static List<string> justDeletedContents;
    static List<ApiAvatar> justUpdatedAvatars;

    static EditorCoroutine fetchingAvatars = null, fetchingWorlds = null;

    private static string searchString = "";
    private static bool WorldsToggle = true;
    private static bool AvatarsToggle = true;

    const int SCROLLBAR_RESERVED_REGION_WIDTH = 50;

    const int WORLD_DESCRIPTION_FIELD_WIDTH = 140;
    const int WORLD_IMAGE_BUTTON_WIDTH = 100;
    const int WORLD_IMAGE_BUTTON_HEIGHT = 100;
    const int WORLD_RELEASE_STATUS_FIELD_WIDTH = 150;
    const int COPY_WORLD_ID_BUTTON_WIDTH = 75;
    const int DELETE_WORLD_BUTTON_WIDTH = 75;
    const int WORLD_ALL_INFORMATION_MAX_WIDTH = WORLD_DESCRIPTION_FIELD_WIDTH + WORLD_IMAGE_BUTTON_WIDTH + WORLD_RELEASE_STATUS_FIELD_WIDTH + COPY_WORLD_ID_BUTTON_WIDTH + DELETE_WORLD_BUTTON_WIDTH + SCROLLBAR_RESERVED_REGION_WIDTH;
    const int WORLD_REDUCED_INFORMATION_MAX_WIDTH = WORLD_DESCRIPTION_FIELD_WIDTH + WORLD_IMAGE_BUTTON_WIDTH + WORLD_RELEASE_STATUS_FIELD_WIDTH + SCROLLBAR_RESERVED_REGION_WIDTH;

    const int AVATAR_DESCRIPTION_FIELD_WIDTH = 140;
    const int AVATAR_IMAGE_BUTTON_WIDTH = WORLD_IMAGE_BUTTON_WIDTH;
    const int AVATAR_IMAGE_BUTTON_HEIGHT = WORLD_IMAGE_BUTTON_HEIGHT;
    const int AVATAR_RELEASE_STATUS_FIELD_WIDTH = 150;
    const int SET_AVATAR_STATUS_BUTTON_WIDTH = 100;
    const int COPY_AVATAR_ID_BUTTON_WIDTH = COPY_WORLD_ID_BUTTON_WIDTH;
    const int DELETE_AVATAR_BUTTON_WIDTH = DELETE_WORLD_BUTTON_WIDTH;
    const int AVATAR_ALL_INFORMATION_MAX_WIDTH = AVATAR_DESCRIPTION_FIELD_WIDTH + AVATAR_IMAGE_BUTTON_WIDTH + AVATAR_RELEASE_STATUS_FIELD_WIDTH + SET_AVATAR_STATUS_BUTTON_WIDTH + COPY_AVATAR_ID_BUTTON_WIDTH + DELETE_AVATAR_BUTTON_WIDTH + SCROLLBAR_RESERVED_REGION_WIDTH;
    const int AVATAR_REDUCED_INFORMATION_MAX_WIDTH = AVATAR_DESCRIPTION_FIELD_WIDTH + AVATAR_IMAGE_BUTTON_WIDTH + AVATAR_RELEASE_STATUS_FIELD_WIDTH + SCROLLBAR_RESERVED_REGION_WIDTH;

    const int MAX_ALL_INFORMATION_WIDTH = WORLD_ALL_INFORMATION_MAX_WIDTH > AVATAR_ALL_INFORMATION_MAX_WIDTH ? WORLD_ALL_INFORMATION_MAX_WIDTH : AVATAR_ALL_INFORMATION_MAX_WIDTH;
    const int MAX_REDUCED_INFORMATION_WIDTH = WORLD_REDUCED_INFORMATION_MAX_WIDTH > AVATAR_REDUCED_INFORMATION_MAX_WIDTH ? WORLD_REDUCED_INFORMATION_MAX_WIDTH : AVATAR_REDUCED_INFORMATION_MAX_WIDTH;

    public static void ClearContent()
    {
        if (uploadedWorlds != null)
            uploadedWorlds = null;
        if (uploadedAvatars != null)
            uploadedAvatars = null;
        ImageCache.Clear();
    }

    IEnumerator FetchUploadedData()
    {
        if (!RemoteConfig.IsInitialized())
            RemoteConfig.Init();

        if (!APIUser.IsLoggedInWithCredentials)
            yield break;

        ApiCache.ClearResponseCache();
        VRCCachedWWW.ClearOld();

        if (fetchingAvatars == null)
            fetchingAvatars = EditorCoroutine.Start(() => FetchAvatars());
        if (fetchingWorlds == null)
            fetchingWorlds = EditorCoroutine.Start(() => FetchWorlds());
    }

    static void FetchAvatars(int offset = 0)
    {
        ApiAvatar.FetchList(
            delegate (IEnumerable<ApiAvatar> obj)
            {
                Debug.LogFormat("<color=yellow>Fetching Avatar Bucket {0}</color>", offset);
                if (obj.FirstOrDefault() != null)
                    fetchingAvatars = EditorCoroutine.Start(() =>
                    {
                        var l = obj.ToList();
                        int count = l.Count;
                        SetupAvatarData(l);
                        FetchAvatars(offset + count);
                    });
                else
                {
                    fetchingAvatars = null;
                    foreach (ApiAvatar a in uploadedAvatars)
                        DownloadImage(a.id, a.thumbnailImageUrl);
                }
            },
            delegate (string obj)
            {
                Debug.LogError("Error fetching your uploaded avatars:\n" + obj);
                fetchingAvatars = null;
            },
            ApiAvatar.Owner.Mine,
            ApiAvatar.ReleaseStatus.All,
            null,
            PageLimit,
            offset,
            ApiAvatar.SortHeading.None,
            ApiAvatar.SortOrder.Descending,
            null,
            null, 
            true,
            false,
            null,
            false
            );
    }

    static void FetchWorlds(int offset = 0)
    {
        ApiWorld.FetchList(
            delegate (IEnumerable<ApiWorld> obj)
            {
                Debug.LogFormat("<color=yellow>Fetching World Bucket {0}</color>", offset);
                if (obj.FirstOrDefault() != null)
                    fetchingWorlds = EditorCoroutine.Start(() =>
                    {
                        var l = obj.ToList();
                        int count = l.Count;
                        SetupWorldData(l);
                        FetchWorlds(offset + count);
                    });
                else
                {
                    fetchingWorlds = null;

                    foreach (ApiWorld w in uploadedWorlds)
                        DownloadImage(w.id, w.thumbnailImageUrl);
                }
            },
            delegate (string obj)
            {
                Debug.LogError("Error fetching your uploaded worlds:\n" + obj);
                fetchingWorlds = null;
            },
            ApiWorld.SortHeading.Updated,
            ApiWorld.SortOwnership.Mine,
            ApiWorld.SortOrder.Descending,
            offset,
            PageLimit,
            "",
            null,
            null,
            null,
            "",
            ApiWorld.ReleaseStatus.All,
            null,
            null, 
            true,
            false);
    }

    static void SetupWorldData(List<ApiWorld> worlds)
    {
        if (worlds == null || uploadedWorlds == null)
            return;

        worlds.RemoveAll(w => w == null || w.name == null || uploadedWorlds.Any(w2 => w2.id == w.id));

        if (worlds.Count > 0)
        {
            uploadedWorlds.AddRange(worlds);
            uploadedWorlds.Sort((w1, w2) => w1.name.CompareTo(w2.name));
        }
    }

    static void SetupAvatarData(List<ApiAvatar> avatars)
    {
        if (avatars == null || uploadedAvatars == null )
            return;

        avatars.RemoveAll(a => a == null || a.name == null || uploadedAvatars.Any(a2 => a2.id == a.id));

        if (avatars.Count > 0)
        {
            uploadedAvatars.AddRange(avatars);
            uploadedAvatars.Sort((w1, w2) => w1.name.CompareTo(w2.name));
        }
    }

    static void DownloadImage(string id, string url)
    {
        if (ImageCache.ContainsKey(id) && ImageCache[id] != null)
            return;

        System.Action<WWW> onDone = (www) =>
        {
            if (string.IsNullOrEmpty(www.error))
            {
                try
                {   // converting Texture2D to use linear color space fixes issue with SDK world & avatar thumbnails appearing too dark (also enables mipmaps to improve appearance of thumbnails)
                    Texture2D newTexture2DWithLinearEnabled;
                    newTexture2DWithLinearEnabled = new Texture2D(4, 4, TextureFormat.DXT1, true, true);
                    www.LoadImageIntoTexture(newTexture2DWithLinearEnabled);
                    ImageCache[id] = newTexture2DWithLinearEnabled;
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                }
            }
            else if (ImageCache.ContainsKey(id))
                ImageCache.Remove(id);
        };

        EditorCoroutine.Start(VRCCachedWWW.Get(url, onDone));
    }

    Vector2 contentScrollPos;

    bool OnGUIUserInfo()
    {
        bool updatedContent = false;

        if (!RemoteConfig.IsInitialized())
            RemoteConfig.Init();

        if (APIUser.IsLoggedInWithCredentials && uploadedWorlds != null && uploadedAvatars != null)
        {

            bool expandedLayout = false; // (position.width > MAX_ALL_INFORMATION_WIDTH);    // uncomment for future wide layouts

            if (!expandedLayout)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
            }

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(searchBarStyle);

            EditorGUILayout.BeginHorizontal();

            float searchFieldShrinkOffset = 30f;
            GUILayoutOption layoutOption = (expandedLayout ? GUILayout.Width(position.width - searchFieldShrinkOffset) : GUILayout.Width(SdkWindowWidth - searchFieldShrinkOffset));
            searchString = EditorGUILayout.TextField(searchString, GUI.skin.FindStyle("SearchTextField"), layoutOption);
            GUIStyle searchButtonStyle = searchString == string.Empty
                ? GUI.skin.FindStyle("SearchCancelButtonEmpty")
                : GUI.skin.FindStyle("SearchCancelButton");
            if (GUILayout.Button(string.Empty, searchButtonStyle))
            {
                searchString = string.Empty;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            if (!expandedLayout)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
            }

            layoutOption = (expandedLayout ? GUILayout.Width(position.width) : GUILayout.Width(SdkWindowWidth));
            contentScrollPos = EditorGUILayout.BeginScrollView(contentScrollPos, layoutOption);

            GUIStyle descriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
            descriptionStyle.wordWrap = true;

            if (uploadedWorlds.Count > 0)
            {
                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("WORLDS", EditorStyles.boldLabel, GUILayout.ExpandWidth(false), GUILayout.Width(58));
                WorldsToggle = EditorGUILayout.Foldout(WorldsToggle, new GUIContent(""));
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space();

                if (WorldsToggle)
                {

                    List<ApiWorld> tmpWorlds = new List<ApiWorld>();

                    if (uploadedWorlds.Count > 0)
                        tmpWorlds = new List<ApiWorld>(uploadedWorlds);

                    foreach (ApiWorld w in tmpWorlds)
                    {
                        if (justDeletedContents != null && justDeletedContents.Contains(w.id))
                        {
                            uploadedWorlds.Remove(w);
                            continue;
                        }

                        if (!w.name.ToLowerInvariant().Contains(searchString.ToLowerInvariant()))
                        {
                            continue;
                        }

                        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                        EditorGUILayout.BeginHorizontal(GUILayout.Width(WORLD_IMAGE_BUTTON_WIDTH));

                        if (ImageCache.ContainsKey(w.id))
                        {
                            if (GUILayout.Button(ImageCache[w.id], GUILayout.Height(WORLD_IMAGE_BUTTON_HEIGHT),
                                GUILayout.Width(WORLD_IMAGE_BUTTON_WIDTH)))
                            {
                                Application.OpenURL(w.imageUrl);
                            }
                        }
                        else
                        {
                            if (GUILayout.Button("", GUILayout.Height(WORLD_IMAGE_BUTTON_HEIGHT),
                                GUILayout.Width(WORLD_IMAGE_BUTTON_WIDTH)))
                            {
                                Application.OpenURL(w.imageUrl);
                            }
                        }

                        if (expandedLayout)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(w.name, descriptionStyle,
                                GUILayout.Width(position.width - MAX_ALL_INFORMATION_WIDTH +
                                                WORLD_DESCRIPTION_FIELD_WIDTH));
                        }
                        else
                        {
                            EditorGUILayout.BeginVertical();
                            EditorGUILayout.LabelField(w.name, descriptionStyle);
                        }

                        EditorGUILayout.LabelField("Release Status: " + w.releaseStatus,
                            GUILayout.Width(WORLD_RELEASE_STATUS_FIELD_WIDTH));
                        if (GUILayout.Button("Copy ID", GUILayout.Width(COPY_WORLD_ID_BUTTON_WIDTH)))
                        {
                            TextEditor te = new TextEditor();
                            te.text = w.id;
                            te.SelectAll();
                            te.Copy();
                        }

                        if (GUILayout.Button("Delete", GUILayout.Width(DELETE_WORLD_BUTTON_WIDTH)))
                        {
                            if (EditorUtility.DisplayDialog("Delete " + w.name + "?",
                                "Are you sure you want to delete " + w.name + "? This cannot be undone.", "Delete",
                                "Cancel"))
                            {
                                foreach (VRC.Core.PipelineManager pm in FindObjectsOfType<VRC.Core.PipelineManager>()
                                    .Where(pm => pm.blueprintId == w.id))
                                {
                                    pm.blueprintId = "";
                                    pm.completedSDKPipeline = false;

                                    UnityEditor.EditorUtility.SetDirty(pm);
                                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pm.gameObject.scene);
                                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(pm.gameObject.scene);
                                }

                                API.Delete<ApiWorld>(w.id);
                                uploadedWorlds.RemoveAll(world => world.id == w.id);
                                if (ImageCache.ContainsKey(w.id))
                                    ImageCache.Remove(w.id);

                                if (justDeletedContents == null) justDeletedContents = new List<string>();
                                justDeletedContents.Add(w.id);
                                updatedContent = true;
                            }
                        }

                        if (expandedLayout)
                            EditorGUILayout.EndHorizontal();
                        else
                            EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Space();
                    }
                }
            }

            if (uploadedAvatars.Count > 0)
            {
                EditorGUILayout.Space();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("AVATARS", EditorStyles.boldLabel, GUILayout.ExpandWidth(false), GUILayout.Width(65));
                AvatarsToggle = EditorGUILayout.Foldout(AvatarsToggle, new GUIContent(""));
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space();

                if (AvatarsToggle)
                {

                    List<ApiAvatar> tmpAvatars = new List<ApiAvatar>();

                    if (uploadedAvatars.Count > 0)
                        tmpAvatars = new List<ApiAvatar>(uploadedAvatars);

                    if (justUpdatedAvatars != null)
                    {
                        foreach (ApiAvatar a in justUpdatedAvatars)
                        {
                            int index = tmpAvatars.FindIndex((av) => av.id == a.id);
                            if (index != -1)
                                tmpAvatars[index] = a;
                        }
                    }

                    foreach (ApiAvatar a in tmpAvatars)
                    {
                        if (justDeletedContents != null && justDeletedContents.Contains(a.id))
                        {
                            uploadedAvatars.Remove(a);
                            continue;
                        }
                        
                        if (!a.name.ToLowerInvariant().Contains(searchString.ToLowerInvariant()))
                        {
                            continue;
                        }

                        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                        EditorGUILayout.BeginHorizontal(GUILayout.Width(AVATAR_DESCRIPTION_FIELD_WIDTH));
                        if (ImageCache.ContainsKey(a.id))
                        {
                            if (GUILayout.Button(ImageCache[a.id], GUILayout.Height(AVATAR_IMAGE_BUTTON_HEIGHT),
                                GUILayout.Width(AVATAR_IMAGE_BUTTON_WIDTH)))
                            {
                                Application.OpenURL(a.imageUrl);
                            }
                        }
                        else
                        {
                            if (GUILayout.Button("", GUILayout.Height(AVATAR_IMAGE_BUTTON_HEIGHT),
                                GUILayout.Width(AVATAR_IMAGE_BUTTON_WIDTH)))
                            {
                                Application.OpenURL(a.imageUrl);
                            }
                        }

                        if (expandedLayout)
                            EditorGUILayout.BeginHorizontal();
                        else
                            EditorGUILayout.BeginVertical();

                        EditorGUILayout.LabelField(a.name, descriptionStyle,
                            GUILayout.Width(expandedLayout
                                ? position.width - MAX_ALL_INFORMATION_WIDTH + AVATAR_DESCRIPTION_FIELD_WIDTH
                                : AVATAR_DESCRIPTION_FIELD_WIDTH));
                        EditorGUILayout.LabelField("Release Status: " + a.releaseStatus,
                            GUILayout.Width(AVATAR_RELEASE_STATUS_FIELD_WIDTH));

                        string oppositeReleaseStatus = a.releaseStatus == "public" ? "private" : "public";
                        if (GUILayout.Button("Make " + oppositeReleaseStatus,
                            GUILayout.Width(SET_AVATAR_STATUS_BUTTON_WIDTH)))
                        {
                            a.releaseStatus = oppositeReleaseStatus;

                            a.SaveReleaseStatus((c) =>
                                {
                                    ApiAvatar savedBP = (ApiAvatar) c.Model;

                                    if (justUpdatedAvatars == null) justUpdatedAvatars = new List<ApiAvatar>();
                                    justUpdatedAvatars.Add(savedBP);

                                },
                                (c) =>
                                {
                                    Debug.LogError(c.Error);
                                    EditorUtility.DisplayDialog("Avatar Updated",
                                        "Failed to change avatar release status", "OK");
                                });
                        }

                        if (GUILayout.Button("Copy ID", GUILayout.Width(COPY_AVATAR_ID_BUTTON_WIDTH)))
                        {
                            TextEditor te = new TextEditor();
                            te.text = a.id;
                            te.SelectAll();
                            te.Copy();
                        }

                        if (GUILayout.Button("Delete", GUILayout.Width(DELETE_AVATAR_BUTTON_WIDTH)))
                        {
                            if (EditorUtility.DisplayDialog("Delete " + a.name + "?",
                                "Are you sure you want to delete " + a.name + "? This cannot be undone.", "Delete",
                                "Cancel"))
                            {
                                foreach (VRC.Core.PipelineManager pm in FindObjectsOfType<VRC.Core.PipelineManager>()
                                    .Where(pm => pm.blueprintId == a.id))
                                {
                                    pm.blueprintId = "";
                                    pm.completedSDKPipeline = false;

                                    UnityEditor.EditorUtility.SetDirty(pm);
                                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pm.gameObject.scene);
                                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(pm.gameObject.scene);
                                }

                                API.Delete<ApiAvatar>(a.id);
                                uploadedAvatars.RemoveAll(avatar => avatar.id == a.id);
                                if (ImageCache.ContainsKey(a.id))
                                    ImageCache.Remove(a.id);

                                if (justDeletedContents == null) justDeletedContents = new List<string>();
                                justDeletedContents.Add(a.id);
                                updatedContent = true;
                            }
                        }

                        if (expandedLayout)
                            EditorGUILayout.EndHorizontal();
                        else
                            EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Space();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            if (!expandedLayout)
            {
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            if ((updatedContent) && (null != window)) window.Reset();

            return true;
        }
        else
        {
            return false;
        }
    }

    void ShowContent()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();

        if (uploadedWorlds == null || uploadedAvatars == null)
        {
            if (uploadedWorlds == null)
                uploadedWorlds = new List<ApiWorld>();
            if (uploadedAvatars == null)
                uploadedAvatars = new List<ApiAvatar>();

            EditorCoroutine.Start(FetchUploadedData());
        }

        if( fetchingWorlds != null || fetchingAvatars != null )
        {
            GUILayout.BeginVertical(boxGuiStyle, GUILayout.Width(SdkWindowWidth));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fetching Records", titleGuiStyle);
            EditorGUILayout.Space();
            GUILayout.EndVertical();
        }
        else
        {
            GUILayout.BeginVertical(boxGuiStyle, GUILayout.Width(SdkWindowWidth));
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Fetch updated records from the VRChat server");
            if( GUILayout.Button("Fetch") )
            {
                ClearContent();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            GUILayout.EndVertical();
        }

        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        OnGUIUserInfo();
    }
}
