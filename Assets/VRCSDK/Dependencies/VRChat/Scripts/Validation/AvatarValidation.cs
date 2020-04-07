using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace VRCSDK2.Validation
{
    public static class AvatarValidation
    {
        private const int ENFORCE_AUDIO_SOURCE_GAMEOBJECTS_PER_FRAME = 50;
        private const int ENFORCE_STATIONS_GAMEOBJECTS_PER_FRAME = 50;

        private const int MAX_STATIONS_PER_AVATAR = 6;
        private const float MAX_STATION_ACTIVATE_DISTANCE = 0f;
        private const float MAX_STATION_LOCATION_DISTANCE = 2f;
        private const float MAX_STATION_COLLIDER_DIMENSION = 2f;

        public static readonly string[] ComponentTypeWhiteList = new string[] {
            "UnityEngine.Transform",
            "UnityEngine.Animator",
            "VRC.Core.PipelineManager",
#if !VRC_CLIENT
            "VRC.Core.PipelineSaver",
#endif
            "VRCSDK2.VRC_AvatarDescriptor",
            "VRCSDK2.VRC_AvatarVariations",
            "VRC.SDK3.Components.VRCAvatarDescriptor",
            "VRC.SDK3.Components.VRCAvatarVariations",
            "NetworkMetadata",
            "RootMotion.FinalIK.IKExecutionOrder",
            "RootMotion.FinalIK.VRIK",
            "RootMotion.FinalIK.FullBodyBipedIK",
            "RootMotion.FinalIK.LimbIK",
            "RootMotion.FinalIK.AimIK",
            "RootMotion.FinalIK.BipedIK",
            "RootMotion.FinalIK.GrounderIK",
            "RootMotion.FinalIK.GrounderFBBIK",
            "RootMotion.FinalIK.GrounderVRIK",
            "RootMotion.FinalIK.GrounderQuadruped",
            "RootMotion.FinalIK.TwistRelaxer",
            "RootMotion.FinalIK.ShoulderRotator",
            "RootMotion.FinalIK.FBBIKArmBending",
            "RootMotion.FinalIK.FBBIKHeadEffector",
            "RootMotion.FinalIK.FABRIK",
            "RootMotion.FinalIK.FABRIKChain",
            "RootMotion.FinalIK.FABRIKRoot",
            "RootMotion.FinalIK.CCDIK",
            "RootMotion.FinalIK.RotationLimit",
            "RootMotion.FinalIK.RotationLimitHinge",
            "RootMotion.FinalIK.RotationLimitPolygonal",
            "RootMotion.FinalIK.RotationLimitSpline",
            "UnityEngine.SkinnedMeshRenderer",
            "LimbIK", // our limbik based on Unity ik
            "AvatarAnimation",
            "LoadingAvatarTextureAnimation",
            "UnityEngine.MeshFilter",
            "UnityEngine.MeshRenderer",
            "UnityEngine.Animation",
            "UnityEngine.ParticleSystem",
            "UnityEngine.ParticleSystemRenderer",
#if UNITY_STANDALONE
            "DynamicBone",
            "DynamicBoneCollider",
#endif
            "UnityEngine.TrailRenderer",
#if UNITY_STANDALONE
            "UnityEngine.Cloth",
            "UnityEngine.Light",
            "UnityEngine.BoxCollider",
            "UnityEngine.SphereCollider",
            "UnityEngine.CapsuleCollider",
            "UnityEngine.Rigidbody",
            "UnityEngine.Joint",
            "UnityEngine.Animations.AimConstraint",
            "UnityEngine.Animations.LookAtConstraint",
            "UnityEngine.Animations.ParentConstraint",
            "UnityEngine.Animations.PositionConstraint",
            "UnityEngine.Animations.RotationConstraint",
            "UnityEngine.Animations.ScaleConstraint",
            "UnityEngine.Camera",
#endif
            "UnityEngine.FlareLayer",
            "UnityEngine.GUILayer",
#if UNITY_STANDALONE
            "UnityEngine.AudioSource",
            "ONSPAudioSource",
            "VRCSDK2.VRC_SpatialAudioSource",
            "VRC.SDK3.Components.VRCSpatialAudioSource",
#endif
            "AvatarCustomAudioLimiter",
            "UnityEngine.LineRenderer",
            "VRCSDK2.VRC_IKFollower",
            "VRC.SDK3.Components.VRCIKFollower",
            "VRC_IKFollowerInternal",
            "RealisticEyeMovements.EyeAndHeadAnimator",
            "RealisticEyeMovements.LookTargetController",
            "AvatarAudioSourceFilter",
            "VRCSDK2.VRC_Station",
            "VRC.SDK3.Components.VRCStation",
            "VRC_StationInternal",
            "VRC.AvatarPerformanceComponentSettings",
        };

        public static readonly string[] ShaderWhiteList = new string[]
        {
            "VRChat/Mobile/Standard Lite",
            "VRChat/Mobile/Diffuse",
            "VRChat/Mobile/Bumped Diffuse",
            "VRChat/Mobile/Bumped Mapped Specular",
            "VRChat/Mobile/Toon Lit",
            "VRChat/Mobile/MatCap Lit",

            "VRChat/Mobile/Particles/Additive",
            "VRChat/Mobile/Particles/Multiply",
        };

        public static bool ps_limiter_enabled = false;
        public static int ps_max_particles = 50000;
        public static int ps_max_systems = 200;
        public static int ps_max_emission = 5000;
        public static int ps_max_total_emission = 40000;
        public static int ps_mesh_particle_divider = 50;
        public static int ps_mesh_particle_poly_limit = 50000;
        public static int ps_collision_penalty_high = 120;
        public static int ps_collision_penalty_med = 60;
        public static int ps_collision_penalty_low = 10;
        public static int ps_trails_penalty = 10;
        public static int ps_max_particle_force = 0; // can not be disabled

        private static int _enforceAudioSourcesFrameNumber = 0;
        private static int _enforceAudioSourcesProcessedThisFrame = 0;

        private static int _enforceAvatarStationsFrameNumber = 0;
        private static int _enforceAvatarStationsProcessedThisFrame = 0;

        public static void RemoveIllegalComponents(GameObject target, bool retry = true)
        {
            ValidationUtils.RemoveIllegalComponents(target, ValidationUtils.WhitelistedTypes("avatar", ComponentTypeWhiteList), retry);
        }

        public static IEnumerable<Component> FindIllegalComponents(GameObject target)
        {
            return ValidationUtils.FindIllegalComponents(target, ValidationUtils.WhitelistedTypes("avatar", ComponentTypeWhiteList));
        }

        public static List<AudioSource> EnforceAudioSourceLimits(GameObject currentAvatar)
        {
            List<AudioSource> found = new List<AudioSource>();
            IEnumerator enforcer = EnforceAudioSourceLimitsEnumerator(currentAvatar, (a) => found.Add(a));
            while (enforcer.MoveNext())
            {
                _enforceAudioSourcesProcessedThisFrame = 0;
            }

            return found;
        }

        static void ProcessSpatialAudioSources(AudioSource audioSource)
        {
#if VRC_SDK_VRCSDK2
            VRC_SpatialAudioSource vrcSpatialAudioSource2 = audioSource.gameObject.GetComponent<VRC_SpatialAudioSource>();
            if (vrcSpatialAudioSource2 == null)
            {
                // user has not yet added VRC_SpatialAudioSource (or ONSP)
                // so set up some defaults
                vrcSpatialAudioSource2 = audioSource.gameObject.AddComponent<VRC_SpatialAudioSource>();
                vrcSpatialAudioSource2.Gain = AudioManagerSettings.AvatarAudioMaxGain;
                vrcSpatialAudioSource2.Far = AudioManagerSettings.AvatarAudioMaxRange;
                vrcSpatialAudioSource2.Near = 0f;
                vrcSpatialAudioSource2.VolumetricRadius = 0f;
                vrcSpatialAudioSource2.EnableSpatialization = true;
                vrcSpatialAudioSource2.enabled = true;
                audioSource.spatialize = true;
                audioSource.priority = Mathf.Clamp(audioSource.priority, 200, 255);
                audioSource.bypassEffects = false;
                audioSource.bypassListenerEffects = false;
                audioSource.spatialBlend = 1f;
                audioSource.spread = 0;

                // user is allowed to change, but for now put a safe default
                audioSource.maxDistance = AudioManagerSettings.AvatarAudioMaxRange;
                audioSource.minDistance = audioSource.maxDistance / 500f;
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            }
#elif VRC_SDK_VRCSDK3
            VRC.SDK3.Components.VRCSpatialAudioSource vrcSpatialAudioSource2 = audioSource.gameObject.GetComponent<VRC.SDK3.Components.VRCSpatialAudioSource>();
            if (vrcSpatialAudioSource2 == null)
            {
                // user has not yet added VRC_SpatialAudioSource (or ONSP)
                // so set up some defaults
                vrcSpatialAudioSource2 = audioSource.gameObject.AddComponent<VRC.SDK3.Components.VRCSpatialAudioSource>();
                vrcSpatialAudioSource2.Gain = AudioManagerSettings.AvatarAudioMaxGain;
                vrcSpatialAudioSource2.Far = AudioManagerSettings.AvatarAudioMaxRange;
                vrcSpatialAudioSource2.Near = 0f;
                vrcSpatialAudioSource2.VolumetricRadius = 0f;
                vrcSpatialAudioSource2.EnableSpatialization = true;
                vrcSpatialAudioSource2.enabled = true;
                audioSource.spatialize = true;
                audioSource.priority = Mathf.Clamp(audioSource.priority, 200, 255);
                audioSource.bypassEffects = false;
                audioSource.bypassListenerEffects = false;
                audioSource.spatialBlend = 1f;
                audioSource.spread = 0;

                // user is allowed to change, but for now put a safe default
                audioSource.maxDistance = AudioManagerSettings.AvatarAudioMaxRange;
                audioSource.minDistance = audioSource.maxDistance / 500f;
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            }
#endif
        }

        private static IEnumerator EnforceAudioSourceLimitsEnumerator(GameObject currentAvatar, System.Action<AudioSource> onFound)
        {
            if (currentAvatar == null)
            {
                yield break;
            }

            Queue<GameObject> children = new Queue<GameObject>();
            if (currentAvatar != null)
            {
                children.Enqueue(currentAvatar.gameObject);
            }

            while (children.Count > 0)
            {
                if (Time.frameCount > _enforceAudioSourcesFrameNumber)
                {
                    _enforceAudioSourcesFrameNumber = Time.frameCount;
                    _enforceAudioSourcesProcessedThisFrame = 0;
                }

                if (_enforceAudioSourcesProcessedThisFrame > ENFORCE_AUDIO_SOURCE_GAMEOBJECTS_PER_FRAME)
                {
                    yield return null;
                }

                Profiler.BeginSample("EnforceAudioSourceLimitsEnumerator");
                _enforceAudioSourcesProcessedThisFrame++;

                GameObject child = children.Dequeue();
                if (child == null)
                {
                    Profiler.EndSample();
                    continue;
                }

                int childCount = child.transform.childCount;
                for (int idx = 0; idx < childCount; ++idx)
                {
                    children.Enqueue(child.transform.GetChild(idx).gameObject);
                }

#if VRC_CLIENT
                if (child.GetComponent<USpeaker>() != null)
                {
                    Profiler.EndSample();
                    continue;
                }
#endif

                AudioSource[] sources = child.transform.GetComponents<AudioSource>();
                if (sources != null && sources.Length > 0)
                {
                    AudioSource audioSource = sources[0];
                    if (audioSource == null)
                    {
                        Profiler.EndSample();
                        continue;
                    }


#if VRC_CLIENT
                    audioSource.outputAudioMixerGroup = VRCAudioManager.GetAvatarGroup();
                    audioSource.priority = Mathf.Clamp(audioSource.priority, 200, 255);
#else
                        ProcessSpatialAudioSources( audioSource );
#endif //!VRC_CLIENT

                    onFound(audioSource);

                    if (sources.Length > 1)
                    {
                        Debug.LogError("Disabling extra AudioSources on GameObject(" + child.name + "). Only one is allowed per GameObject.");
                        for (int i = 1; i < sources.Length; i++)
                        {
                            if (sources[i] == null)
                            {
                                Profiler.EndSample();
                                continue;
                            }

#if VRC_CLIENT
                            sources[i].enabled = false;
                            sources[i].clip = null;
#else
                            ValidationUtils.RemoveComponent(sources[i]);
#endif //!VRC_CLIENT
                        }
                    }
                }

                Profiler.EndSample();
            }
        }

        public static void EnforceRealtimeParticleSystemLimits(Dictionary<ParticleSystem, int> particleSystems, bool includeDisabled = false, bool stopSystems = true)
        {
            float totalEmission = 0;
            ParticleSystem ps = null;
            int max = 0;
            int em_penalty = 1;
            ParticleSystem.EmissionModule em;
            float emission = 0;
            ParticleSystem.Burst[] bursts;

            foreach (KeyValuePair<ParticleSystem, int> kp in particleSystems)
            {
                if (kp.Key == null)
                    continue;

                if (!kp.Key.isPlaying && !includeDisabled)
                    continue;

                ps = kp.Key;
                max = kp.Value;
                em_penalty = 1;
                if (ps.collision.enabled)
                {
                    // particle force is always restricted (not dependent on ps_limiter_enabled)
                    var restrictedCollision = ps.collision;
                    restrictedCollision.colliderForce = ps_max_particle_force;

                    if (ps_limiter_enabled)
                    {
                        switch (ps.collision.quality)
                        {
                            case ParticleSystemCollisionQuality.High:
                                max = max / ps_collision_penalty_high;
                                em_penalty += 3;
                                break;
                            case ParticleSystemCollisionQuality.Medium:
                                max = max / ps_collision_penalty_med;
                                em_penalty += 2;
                                break;
                            case ParticleSystemCollisionQuality.Low:
                                max = max / ps_collision_penalty_low;
                                em_penalty += 2;
                                break;
                        }
                    }
                }
                if (ps_limiter_enabled && ps.trails.enabled)
                {
                    max = max / ps_trails_penalty;
                    em_penalty += 3;
                }
                if (ps_limiter_enabled && ps.emission.enabled)
                {
                    em = ps.emission;
                    emission = 0;
                    emission += GetCurveMax(em.rateOverTime);
                    emission += GetCurveMax(em.rateOverDistance);

                    bursts = new ParticleSystem.Burst[em.burstCount];
                    em.GetBursts(bursts);
                    for (int i = 0; i < bursts.Length; i++)
                    {
                        float adjMax = bursts[i].repeatInterval > 1 ? bursts[i].maxCount : bursts[i].maxCount * bursts[i].repeatInterval;
                        if (adjMax > ps_max_emission)
                            bursts[i].maxCount = (short)Mathf.Clamp(adjMax, 0, ps_max_emission);
                    }
                    em.SetBursts(bursts);

                    emission *= em_penalty;
                    totalEmission += emission;
                    if ((emission > ps_max_emission || totalEmission > ps_max_total_emission) && stopSystems)
                    {
                        kp.Key.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        // Debug.LogWarning("Particle system named " + kp.Key.gameObject.name + " breached particle emission limits, it has been stopped");
                    }
                }
                if (ps_limiter_enabled && ps.main.maxParticles > Mathf.Clamp(max, 1, kp.Value))
                {
                    ParticleSystem.MainModule psm = ps.main;
                    psm.maxParticles = Mathf.Clamp(psm.maxParticles, 1, max);
                    if (stopSystems)
                        kp.Key.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    Debug.LogWarning("Particle system named " + kp.Key.gameObject.name + " breached particle limits, it has been limited");
                }
            }
        }

        public static List<VRC.SDKBase.VRCStation> EnforceAvatarStationLimits(GameObject currentAvatar)
        {
            List<VRC.SDKBase.VRCStation> found = new List<VRC.SDKBase.VRCStation>();
            IEnumerator enforcer = EnforceAvatarStationLimitsEnumerator(currentAvatar, (a) => found.Add(a));
            while (enforcer.MoveNext())
            {
                _enforceAvatarStationsProcessedThisFrame = 0;
            }

            return found;
        }

        public static IEnumerator EnforceAvatarStationLimitsEnumerator(GameObject currentAvatar, System.Action<VRC.SDKBase.VRCStation> onFound)
        {
            Queue<GameObject> children = new Queue<GameObject>();
            children.Enqueue(currentAvatar.gameObject);

            int stationCount = 0;
            uint objectsProcessedThisFrame = 0;
            while (children.Count > 0)
            {
                if (Time.frameCount > _enforceAvatarStationsFrameNumber)
                {
                    _enforceAvatarStationsFrameNumber = Time.frameCount;
                    _enforceAvatarStationsProcessedThisFrame = 0;
                }

                if (_enforceAvatarStationsProcessedThisFrame > ENFORCE_STATIONS_GAMEOBJECTS_PER_FRAME)
                {
                    yield return null;
                }

                Profiler.BeginSample("EnforceAvatarStationLimitsEnumerator");
                _enforceAvatarStationsProcessedThisFrame++;

                GameObject child = children.Dequeue();
                if (child == null)
                {
                    Profiler.EndSample();
                    continue;
                }

                int childCount = child.transform.childCount;
                for (int idx = 0; idx < childCount; ++idx)
                {
                    children.Enqueue(child.transform.GetChild(idx).gameObject);
                }

                VRC.SDKBase.VRCStation[] stations = child.transform.GetComponents<VRC.SDKBase.VRCStation>();
                if (stations != null && stations.Length > 0)
                {
                    foreach (VRC.SDKBase.VRCStation station in stations)
                    {
                        if (station == null)
                        {
                            Profiler.EndSample();
                            continue;
                        }

#if VRC_CLIENT
                        VRC_StationInternal stationInternal = station.transform.GetComponent<VRC_StationInternal>();
#endif
                        if (stationCount < MAX_STATIONS_PER_AVATAR)
                        {
                            bool markedForDestruction = false;
                            // keep this station, but limit it
                            if (station.disableStationExit)
                            {
                                Debug.LogError("[" + currentAvatar.name + "]==> Stations on avatars cannot disable station exit. Re-enabled.");
                                station.disableStationExit = false;
                            }

                            if (station.stationEnterPlayerLocation != null)
                            {
                                if (Vector3.Distance(station.stationEnterPlayerLocation.position, station.transform.position) > MAX_STATION_LOCATION_DISTANCE)
                                {
#if VRC_CLIENT
                                    markedForDestruction = true;
                                    Debug.LogError("[" + currentAvatar.name + "]==> Station enter location is too far from station (max dist=" + MAX_STATION_LOCATION_DISTANCE + "). Station disabled.");
#else
                                    Debug.LogError("Station enter location is too far from station (max dist="+MAX_STATION_LOCATION_DISTANCE+"). Station will be disabled at runtime.");
#endif
                                }
                                if (Vector3.Distance(station.stationExitPlayerLocation.position, station.transform.position) > MAX_STATION_LOCATION_DISTANCE)
                                {
#if VRC_CLIENT
                                    markedForDestruction = true;
                                    Debug.LogError("[" + currentAvatar.name + "]==> Station exit location is too far from station (max dist=" + MAX_STATION_LOCATION_DISTANCE + "). Station disabled.");
#else
                                    Debug.LogError("Station exit location is too far from station (max dist="+MAX_STATION_LOCATION_DISTANCE+"). Station will be disabled at runtime.");
#endif
                                }

                                if (markedForDestruction)
                                {
#if VRC_CLIENT
                                    ValidationUtils.RemoveComponent(station);
                                    if (stationInternal != null)
                                    {
                                        ValidationUtils.RemoveComponent(stationInternal);
                                    }
#endif
                                }
                                else
                                {
                                    if (onFound != null)
                                    {
                                        onFound(station);
                                    }
                                }

                            }
                        }
                        else
                        {
#if VRC_CLIENT
                            Debug.LogError("[" + currentAvatar.name + "]==> Removing station over limit of " + MAX_STATIONS_PER_AVATAR);
                            ValidationUtils.RemoveComponent(station);
                            if (stationInternal != null)
                            {
                                ValidationUtils.RemoveComponent(stationInternal);
                            }

#else
                            Debug.LogError("Too many stations on avatar("+ currentAvatar.name +"). Maximum allowed="+MAX_STATIONS_PER_AVATAR+". Extra stations will be removed at runtime.");
#endif
                        }

                        stationCount++;
                    }
                }
                Profiler.EndSample();

                if (objectsProcessedThisFrame < ENFORCE_STATIONS_GAMEOBJECTS_PER_FRAME)
                {
                    continue;
                }

                objectsProcessedThisFrame = 0;
                yield return null;
            }
        }

        public static void RemoveCameras(GameObject currentAvatar, bool localPlayer, bool friend)
        {
            if (!localPlayer && currentAvatar != null)
            {
                foreach (Camera camera in currentAvatar.GetComponentsInChildren<Camera>(true))
                {
                    if (camera == null || camera.gameObject == null)
                        continue;

                    Debug.LogWarning("Removing camera from " + camera.gameObject.name);

                    if (friend && camera.targetTexture != null)
                    {
                        camera.enabled = false;
                    }
                    else
                    {

                        camera.enabled = false;
                        if (camera.targetTexture != null)
                            camera.targetTexture = new RenderTexture(16, 16, 24);
                        ValidationUtils.RemoveComponent(camera);
                    }
                }
            }
        }

        public static void StripAnimations(GameObject currentAvatar)
        {
            foreach (Animator anim in currentAvatar.GetComponentsInChildren<Animator>(true))
            {
                if (anim == null)
                    continue;
                StripRuntimeAnimatorController(anim.runtimeAnimatorController);
            }
            foreach (VRC.SDKBase.VRCStation station in currentAvatar.GetComponentsInChildren<VRC.SDKBase.VRCStation>(true))
            {
                if (station == null)
                    continue;
                StripRuntimeAnimatorController(station.animatorController);
            }
        }

        private static void StripRuntimeAnimatorController(RuntimeAnimatorController rc)
        {
            if (rc == null || rc.animationClips == null)
                return;
            foreach (AnimationClip clip in rc.animationClips)
            {
                if (clip == null)
                    continue;
                clip.events = null;
            }
        }

        public static void RemoveExtraAnimationComponents(GameObject currentAvatar)
        {
            if (currentAvatar == null)
                return;

            // remove Animator comps
            {
                Animator mainAnimator = currentAvatar.GetComponent<Animator>();
                bool removeMainAnimator = false;
                if (mainAnimator != null)
                {
                    if (!mainAnimator.isHuman || mainAnimator.avatar == null || !mainAnimator.avatar.isValid)
                    {
                        removeMainAnimator = true;
                    }
                }

                foreach (Animator anim in currentAvatar.GetComponentsInChildren<Animator>(true))
                {
                    if (anim == null || anim.gameObject == null)
                        continue;

                    // exclude the main avatar animator
                    if (anim == mainAnimator)
                    {
                        if (!removeMainAnimator)
                        {
                            continue;
                        }
                    }

                    Debug.LogWarning("Removing Animator comp from " + anim.gameObject.name);

                    anim.enabled = false;
                    ValidationUtils.RemoveComponent(anim);
                }
            }

            ValidationUtils.RemoveComponentsOfType<Animation>(currentAvatar);
        }

        private static Color32 GetTrustLevelColor(VRC.Core.APIUser user)
        {
#if VRC_CLIENT
            Color32 color = new Color32(255, 255, 255, 255);
            if (user == null)
            {
                return color;
            }

            if (user == VRC.Core.APIUser.CurrentUser)
            {
                color = VRCInputManager.showSocialRank ? VRCPlayer.GetColorForSocialRank(user) : VRCPlayer.GetDefaultNameplateColor(user, user.hasVIPAccess);
            }
            else
            {
                color = VRCPlayer.ShouldShowSocialRank(user) ? VRCPlayer.GetColorForSocialRank(user) : VRCPlayer.GetDefaultNameplateColor(user, user.hasVIPAccess);
            }
            return color;
#else
            // we are in sdk, this is not meaningful anyway
            return (Color32)Color.grey;
#endif
        }

        private static Material CreateFallbackMaterial(Material originalMaterial, VRC.Core.APIUser user)
        {
#if VRC_CLIENT
            Material fallbackMaterial;
            Color trustCol = user != null ? (Color)GetTrustLevelColor(user) : Color.white;
            string displayName = user != null ? user.displayName : "localUser";

            if (originalMaterial == null || originalMaterial.shader == null)
            {
                fallbackMaterial = VRC.Core.AssetManagement.CreateMatCap(trustCol * 0.8f + new Color(0.2f, 0.2f, 0.2f));
                fallbackMaterial.name = string.Format("MC_{0}_{1}", fallbackMaterial.shader.name, displayName);
            }
            else
            {
                var safeShader = VRC.Core.AssetManagement.GetSafeShader(originalMaterial.shader.name);
                if (safeShader == null)
                {
                    fallbackMaterial = VRC.Core.AssetManagement.CreateSafeFallbackMaterial(originalMaterial, trustCol * 0.8f + new Color(0.2f, 0.2f, 0.2f));
                    fallbackMaterial.name = string.Format("FB_{0}_{1}_{2}", fallbackMaterial.shader.name, displayName, originalMaterial.name);
                }
                else
                {
                    //Debug.Log("<color=cyan>*** using safe internal fallback for shader:"+ safeShader.name + "</color>");
                    fallbackMaterial = new Material(safeShader);
                    if (safeShader.name == "Standard" || safeShader.name == "Standard (Specular setup)")
                    {
                        VRC.Core.AssetManagement.SetupBlendMode(fallbackMaterial);
                    }

                    fallbackMaterial.CopyPropertiesFromMaterial(originalMaterial);
                    fallbackMaterial.name = string.Format("INT_{0}_{1}_{2}", fallbackMaterial.shader.name, displayName, originalMaterial.name);
                }
            }

            return fallbackMaterial;
#else
            // we are in sdk, this is not meaningful anyway
            return new Material(Shader.Find("Standard"));
#endif
        }

        public static void SetupShaderReplace(VRC.Core.APIUser user, GameObject currentAvatar, HashSet<Renderer> avatarRenderers)
        {
            avatarRenderers.Clear();
            avatarRenderers.UnionWith(currentAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            avatarRenderers.UnionWith(currentAvatar.GetComponentsInChildren<MeshRenderer>(true));
        }

        public static void ReplaceShaders(VRC.Core.APIUser user, IEnumerable<Renderer> avatarRenderers, FallbackMaterialCache fallbackMaterialCache, bool debug = true)
        {
            foreach (Renderer avatarRenderer in avatarRenderers)
            {
                //TODO 2018.4 LTS: Replace this with avatarRenderer.GetSharedMaterials(List<Material> sharedMaterials);
                if (avatarRenderer == null) continue;
                Material[] avatarRendererSharedMaterials = avatarRenderer.sharedMaterials;
                for (int i = 0; i < avatarRendererSharedMaterials.Length; ++i)
                {
                    Material currentMaterial = avatarRendererSharedMaterials[i];
                    if (currentMaterial == null)
                    {
                        continue;
                    }

                    Material fallbackMaterial;
                    if (fallbackMaterialCache.HasFallbackMaterial(currentMaterial))
                    {
                        // material is in our swap list, so its already a fallback
                        fallbackMaterial = fallbackMaterialCache.GetFallBackMaterial(currentMaterial);

                        if (debug)
                        {
                            Debug.Log(string.Format("<color=cyan>*** Using existing fallback: '{0}' </color>", fallbackMaterial.shader.name));
                        }
                    }
                    else
                    {
                        // The current material is not in our safe list so create a fallback.
                        fallbackMaterial = CreateFallbackMaterial(currentMaterial, user);

                        // Map the current material to the fallback and the fallback to itself.
                        fallbackMaterialCache.AddFallbackMaterial(currentMaterial, fallbackMaterial);

                        fallbackMaterialCache.AddFallbackMaterial(fallbackMaterial, fallbackMaterial);

                        if (debug)
                        {
                            Debug.Log(string.Format("<color=cyan>*** Creating new fallback: '{0}' </color>", fallbackMaterial.shader.name));
                        }
                    }

                    avatarRendererSharedMaterials[i] = fallbackMaterial;
                }

                avatarRenderer.sharedMaterials = avatarRendererSharedMaterials;
            }
        }

        public static void ReplaceShadersRealtime(VRC.Core.APIUser user, IEnumerable<Renderer> avatarRenderers, FallbackMaterialCache fallbackMaterialCache, bool debug = false)
        {
            ReplaceShaders(user, avatarRenderers, fallbackMaterialCache, debug);
        }

        public static void SetupParticleLimits()
        {
            ps_limiter_enabled = VRC.Core.RemoteConfig.GetBool("ps_limiter_enabled", ps_limiter_enabled);
            ps_max_particles = VRC.Core.RemoteConfig.GetInt("ps_max_particles", ps_max_particles);
            ps_max_systems = VRC.Core.RemoteConfig.GetInt("ps_max_systems", ps_max_systems);
            ps_max_emission = VRC.Core.RemoteConfig.GetInt("ps_max_emission", ps_max_emission);
            ps_max_total_emission = VRC.Core.RemoteConfig.GetInt("ps_max_total_emission", ps_max_total_emission);
            ps_mesh_particle_divider = VRC.Core.RemoteConfig.GetInt("ps_mesh_particle_divider", ps_mesh_particle_divider);
            ps_mesh_particle_poly_limit = VRC.Core.RemoteConfig.GetInt("ps_mesh_particle_poly_limit", ps_mesh_particle_poly_limit);
            ps_collision_penalty_high = VRC.Core.RemoteConfig.GetInt("ps_collision_penalty_high", ps_collision_penalty_high);
            ps_collision_penalty_med = VRC.Core.RemoteConfig.GetInt("ps_collision_penalty_med", ps_collision_penalty_med);
            ps_collision_penalty_low = VRC.Core.RemoteConfig.GetInt("ps_collision_penalty_low", ps_collision_penalty_low);
            ps_trails_penalty = VRC.Core.RemoteConfig.GetInt("ps_trails_penalty", ps_trails_penalty);

            ps_limiter_enabled = VRC.Core.LocalConfig.GetList("betas").Contains("particle_system_limiter") || ps_limiter_enabled;
            ps_max_particles = VRC.Core.LocalConfig.GetInt("ps_max_particles", ps_max_particles);
            ps_max_systems = VRC.Core.LocalConfig.GetInt("ps_max_systems", ps_max_systems);
            ps_max_emission = VRC.Core.LocalConfig.GetInt("ps_max_emission", ps_max_emission);
            ps_max_total_emission = VRC.Core.LocalConfig.GetInt("ps_max_total_emission", ps_max_total_emission);
            ps_mesh_particle_divider = VRC.Core.LocalConfig.GetInt("ps_mesh_particle_divider", ps_mesh_particle_divider);
            ps_mesh_particle_poly_limit = VRC.Core.LocalConfig.GetInt("ps_mesh_particle_poly_limit", ps_mesh_particle_poly_limit);
            ps_collision_penalty_high = VRC.Core.LocalConfig.GetInt("ps_collision_penalty_high", ps_collision_penalty_high);
            ps_collision_penalty_med = VRC.Core.LocalConfig.GetInt("ps_collision_penalty_med", ps_collision_penalty_med);
            ps_collision_penalty_low = VRC.Core.LocalConfig.GetInt("ps_collision_penalty_low", ps_collision_penalty_low);
            ps_trails_penalty = VRC.Core.LocalConfig.GetInt("ps_trails_penalty", ps_trails_penalty);
        }

        public static Dictionary<ParticleSystem, int> EnforceParticleSystemLimits(GameObject currentAvatar)
        {
            Dictionary<ParticleSystem, int> particleSystems = new Dictionary<ParticleSystem, int>();

            foreach (ParticleSystem ps in currentAvatar.transform.GetComponentsInChildren<ParticleSystem>(true))
            {
                int realtime_max = ps_max_particles;

                // always limit collision force
                var collision = ps.collision;
                if (collision.colliderForce > ps_max_particle_force)
                {
                    collision.colliderForce = ps_max_particle_force;
                    Debug.LogError("Collision force is restricted on avatars, particle system named " + ps.gameObject.name + " collision force restricted to " + ps_max_particle_force);
                }

                if (ps_limiter_enabled)
                {
                    if (particleSystems.Count > ps_max_systems)
                    {
                        Debug.LogError("Too many particle systems, #" + particleSystems.Count + " named " + ps.gameObject.name + " deleted");
                        ValidationUtils.RemoveComponent(ps);
                        continue;
                    }
                    else
                    {
                        var main = ps.main;
                        var emission = ps.emission;

                        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                        if (renderer != null)
                        {
                            if (renderer.renderMode == ParticleSystemRenderMode.Mesh)
                            {
                                Mesh[] meshes = new Mesh[0];
                                int highestPoly = 0;
                                renderer.GetMeshes(meshes);
                                if (meshes.Length == 0 && renderer.mesh != null)
                                {
                                    meshes = new Mesh[] { renderer.mesh };
                                }
                                // Debug.Log(meshes.Length + " meshes possible emmited meshes from " + ps.gameObject.name);
                                foreach (Mesh m in meshes)
                                {
                                    if (m.isReadable)
                                    {
                                        if (m.triangles.Length / 3 > highestPoly)
                                        {
                                            highestPoly = m.triangles.Length / 3;
                                        }
                                    }
                                    else
                                    {
                                        if (1000 > highestPoly)
                                        {
                                            highestPoly = int.MaxValue;
                                        }
                                    }
                                }
                                if (highestPoly > 0)
                                {
                                    highestPoly = Mathf.Clamp(highestPoly / ps_mesh_particle_divider, 1, highestPoly);
                                    realtime_max = Mathf.FloorToInt((float)realtime_max / highestPoly);

                                    if (highestPoly > ps_mesh_particle_poly_limit)
                                    {
                                        Debug.LogError("Particle system named " + ps.gameObject.name + " breached polygon limits, it has been deleted");
                                        ValidationUtils.RemoveComponent(ps);
                                        continue;
                                    }
                                }
                            }
                        }


                        ParticleSystem.MinMaxCurve rate = emission.rateOverTime;

                        if (rate.mode == ParticleSystemCurveMode.Constant)
                        {
                            rate.constant = Mathf.Clamp(rate.constant, 0, ps_max_emission);
                        }
                        else if (rate.mode == ParticleSystemCurveMode.TwoConstants)
                        {
                            rate.constantMax = Mathf.Clamp(rate.constantMax, 0, ps_max_emission);
                        }
                        else
                        {
                            rate.curveMultiplier = Mathf.Clamp(rate.curveMultiplier, 0, ps_max_emission);
                        }

                        emission.rateOverTime = rate;
                        rate = emission.rateOverDistance;

                        if (rate.mode == ParticleSystemCurveMode.Constant)
                        {
                            rate.constant = Mathf.Clamp(rate.constant, 0, ps_max_emission);
                        }
                        else if (rate.mode == ParticleSystemCurveMode.TwoConstants)
                        {
                            rate.constantMax = Mathf.Clamp(rate.constantMax, 0, ps_max_emission);
                        }
                        else
                        {
                            rate.curveMultiplier = Mathf.Clamp(rate.curveMultiplier, 0, ps_max_emission);
                        }

                        emission.rateOverDistance = rate;

                        //Disable collision with PlayerLocal layer
                        collision.collidesWith &= ~(1 << 10);
                    }
                }

                particleSystems.Add(ps, realtime_max);
            }

            EnforceRealtimeParticleSystemLimits(particleSystems, true, false);

            return particleSystems;
        }

        public static bool ClearLegacyAnimations(GameObject currentAvatar)
        {
            bool hasLegacyAnims = false;
            foreach (var ani in currentAvatar.GetComponentsInChildren<Animation>(true))
            {
                if (ani.clip != null)
                    if (ani.clip.legacy)
                    {
                        Debug.LogWarningFormat("Legacy animation found named '{0}' on '{1}', removing", ani.clip.name, ani.gameObject.name);
                        ani.clip = null;
                        hasLegacyAnims = true;
                    }
                foreach (AnimationState anistate in ani)
                    if (anistate.clip.legacy)
                    {
                        Debug.LogWarningFormat("Legacy animation found named '{0}' on '{1}', removing", anistate.clip.name, ani.gameObject.name);
                        ani.RemoveClip(anistate.clip);
                        hasLegacyAnims = true;
                    }
            }
            return hasLegacyAnims;
        }

        private static float GetCurveMax(ParticleSystem.MinMaxCurve minMaxCurve)
        {
            switch (minMaxCurve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return minMaxCurve.constant;
                case ParticleSystemCurveMode.TwoConstants:
                    return minMaxCurve.constantMax;
                default:
                    return minMaxCurve.curveMultiplier;
            }
        }

        public static bool AreAnyParticleSystemsPlaying(Dictionary<ParticleSystem, int> particleSystems)
        {
            foreach (KeyValuePair<ParticleSystem, int> kp in particleSystems)
            {
                if (kp.Key != null && kp.Key.isPlaying)
                    return true;
            }

            return false;
        }

        public static void StopAllParticleSystems(Dictionary<ParticleSystem, int> particleSystems)
        {
            foreach (KeyValuePair<ParticleSystem, int> kp in particleSystems)
            {
                if (kp.Key != null && kp.Key.isPlaying)
                {
                    kp.Key.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        public static IEnumerable<Shader> FindIllegalShaders(GameObject target)
        {
            return ShaderValidation.FindIllegalShaders(target, ShaderWhiteList);
        }

        /// <summary>
        /// NOTE: intended to be called from 'VRCAvatarManager.SafetyCheckAndComponentScan'
        /// but temporarily disabled (until we enable texture streaming)
        /// </summary>  
        public static void ReportTexturesWithoutMipMapStreaming(VRC.Core.ApiAvatar avatar, GameObject target)
        {
            var badTextures = new List<Texture2D>();
            foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
            {
                foreach (Material m in r.sharedMaterials)
                {
                    foreach (int i in m.GetTexturePropertyNameIDs())
                    {
                        Texture2D t = m.GetTexture(i) as Texture2D;
                        if (!t)
                            continue;
                        if ((t.mipmapCount > 0) && !t.streamingMipmaps)
                            badTextures.Add(t);
                    }
                }
            }

            if (badTextures.Count > 0)
            {
                string warning = "[" + avatar.name + "]==> One or more avatar textures have non-streaming mipmaps: ";
                foreach (Texture2D t in badTextures)
                {
                    warning += "'" + t.name + "', ";
                }
                warning = warning.Remove(warning.LastIndexOf(","));
                Debug.LogWarning(warning + ".");
            }

        }

    }
}
