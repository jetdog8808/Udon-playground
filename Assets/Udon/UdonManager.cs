using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon.ClientBindings;
using VRC.Udon.ClientBindings.Interfaces;
using VRC.Udon.Common.Interfaces;

namespace VRC.Udon
{
    [AddComponentMenu("")]
    [ExecuteInEditMode]
    public class UdonManager : MonoBehaviour, IUdonClientInterface
    {
        private static UdonManager _instance;
        private static bool _isUdonEnabled = true;
        public UdonBehaviour currentlyExecuting;

        public static UdonManager Instance
        {
            get
            {
                if(_instance != null)
                {
                    return _instance;
                }

                GameObject udonManagerGameObject = new GameObject("UdonManager");
                if(Application.isPlaying)
                {
                    DontDestroyOnLoad(udonManagerGameObject);
                }

                _instance = udonManagerGameObject.AddComponent<UdonManager>();
                return _instance;
            }
        }

        private IUdonClientInterface _udonClientInterface;

        private IUdonClientInterface UdonClientInterface
        {
            get
            {
                if(_udonClientInterface != null)
                {
                    return _udonClientInterface;
                }

                _udonClientInterface = new UdonClientInterface();

                #if !VRC_CLIENT
                _udonClientInterface.RegisterWrapperModule(new ExternVRCInstantiate());
                #endif

                return _udonClientInterface;
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            if(_isUdonEnabled)
            {
                return;
            }

            VRC.Core.Logger.LogWarning("Udon is disabled globally, Udon components will be removed from the scene.");
            GameObject[] sceneRootGameObjects = scene.GetRootGameObjects();
            List<UdonBehaviour> udonBehavioursWorkingList = new List<UdonBehaviour>();
            foreach(GameObject rootGameObject in sceneRootGameObjects)
            {
                rootGameObject.GetComponentsInChildren(true, udonBehavioursWorkingList);
                foreach(UdonBehaviour udonBehaviour in udonBehavioursWorkingList)
                {
                    Destroy(udonBehaviour);
                }
            }
        }

        public void Awake()
        {
            if(_instance == null)
            {
                _instance = this;
            }

            DebugLogging = Application.isEditor;

            if(this == Instance)
            {
                return;
            }

            if(Application.isPlaying)
            {
                Destroy(this);
            }
            else
            {
                DestroyImmediate(this);
            }
            
            PrimitiveType[] primitiveTypes = (PrimitiveType[]) Enum.GetValues(typeof(PrimitiveType));
            foreach (PrimitiveType primitiveType in primitiveTypes)
            {
                GameObject go = GameObject.CreatePrimitive(primitiveType);
                Mesh primitiveMesh = go.GetComponent<MeshFilter>().sharedMesh;
                Destroy(go);
                Blacklist(primitiveMesh);
            }
        }

        [PublicAPI]
        public static void SetUdonEnabled(bool isEnabled)
        {
            _isUdonEnabled = isEnabled;
        }

        public IUdonVM ConstructUdonVM()
        {
            return !_isUdonEnabled ? null : UdonClientInterface.ConstructUdonVM();
        }

        public bool IsBlacklisted<T>(T objectToCheck)
        {
            return UdonClientInterface.IsBlacklisted(objectToCheck);
        }

        public void Blacklist(UnityEngine.Object objectToBlacklist)
        {
            UdonClientInterface.Blacklist(objectToBlacklist);
        }

        public void Blacklist(IEnumerable<UnityEngine.Object> objectsToBlacklist)
        {
            UdonClientInterface.Blacklist(objectsToBlacklist);
        }

        public bool IsBlacklisted(UnityEngine.Object objectToCheck)
        {
            return UdonClientInterface.IsBlacklisted(objectToCheck);
        }

        public void ClearBlacklist()
        {
            UdonClientInterface.ClearBlacklist();
        }

        public bool IsTypeSafe(Type type)
        {
            return UdonClientInterface.IsTypeSafe(type);
        }

        public IUdonWrapper GetWrapper()
        {
            return UdonClientInterface.GetWrapper();
        }

        public void RegisterWrapperModule(IUdonWrapperModule wrapperModule)
        {
            UdonClientInterface.RegisterWrapperModule(wrapperModule);
        }

        public bool DebugLogging
        {
            get => UdonClientInterface.DebugLogging;
            set => UdonClientInterface.DebugLogging = value;
        }
    }
}
