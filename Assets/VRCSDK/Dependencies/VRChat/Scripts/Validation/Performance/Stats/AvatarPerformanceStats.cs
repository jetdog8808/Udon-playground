using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRCSDK2.Validation.Performance.Stats
{
    public class AvatarPerformanceStats
    {
        private delegate int ComparePerformanceStatsDelegate(AvatarPerformanceStats stats, AvatarPerformanceStatsLevel statsLevel);

        #region Public Fields

        public string avatarName;

        public int polyCount;
        public Bounds aabb;
        public int skinnedMeshCount;
        public int meshCount;
        public int materialCount;
        public int animatorCount;
        public int boneCount;
        public int lightCount;
        public int particleSystemCount;
        public int particleTotalCount;
        public int particleMaxMeshPolyCount;
        public bool particleTrailsEnabled;
        public bool particleCollisionEnabled;
        public int trailRendererCount;
        public int lineRendererCount;
        public int dynamicBoneComponentCount;
        public int dynamicBoneSimulatedBoneCount;
        public int dynamicBoneColliderCount;
        public int dynamicBoneCollisionCheckCount; // number of collider simulated bones excluding the root multiplied by the number of colliders
        public int clothCount;
        public int clothMaxVertices;
        public int physicsColliderCount;
        public int physicsRigidbodyCount;
        public int audioSourceCount;

        #endregion

        #region Private Fields

        private readonly PerformanceRating[] _performanceRatingCache;
        private static readonly Dictionary<AvatarPerformanceCategory, string> _performanceCategoryDisplayNames = new Dictionary<AvatarPerformanceCategory, string>
        {
            {AvatarPerformanceCategory.PolyCount, "Polygons"},
            {AvatarPerformanceCategory.AABB, "Bounds"},
            {AvatarPerformanceCategory.SkinnedMeshCount, "Skinned Meshes"},
            {AvatarPerformanceCategory.MeshCount, "Meshes"},
            {AvatarPerformanceCategory.MaterialCount, "Material Slots"},
            {AvatarPerformanceCategory.AnimatorCount, "Animators"},
            {AvatarPerformanceCategory.BoneCount, "Bones"},
            {AvatarPerformanceCategory.LightCount, "Lights"},
            {AvatarPerformanceCategory.ParticleSystemCount, "Particle Systems"},
            {AvatarPerformanceCategory.ParticleTotalCount, "Total Max Particles"},
            {AvatarPerformanceCategory.ParticleMaxMeshPolyCount, "Mesh Particle Max Polygons"},
            {AvatarPerformanceCategory.ParticleTrailsEnabled, "Particle Trails Enabled"},
            {AvatarPerformanceCategory.ParticleCollisionEnabled, "Particle Collision Enabled"},
            {AvatarPerformanceCategory.TrailRendererCount, "Trail Renderers"},
            {AvatarPerformanceCategory.LineRendererCount, "Line Renderers"},
            {AvatarPerformanceCategory.DynamicBoneComponentCount, "Dynamic Bone Components"},
            {AvatarPerformanceCategory.DynamicBoneSimulatedBoneCount, "Dynamic Bone Transforms"},
            {AvatarPerformanceCategory.DynamicBoneColliderCount, "Dynamic Bone Colliders"},
            {AvatarPerformanceCategory.DynamicBoneCollisionCheckCount, "Dynamic Bone Collision Check Count"},
            {AvatarPerformanceCategory.ClothCount, "Cloths"},
            {AvatarPerformanceCategory.ClothMaxVertices, "Total Cloth Vertices"},
            {AvatarPerformanceCategory.PhysicsColliderCount, "Physics Colliders"},
            {AvatarPerformanceCategory.PhysicsRigidbodyCount, "Physics Rigidbodies"},
            {AvatarPerformanceCategory.AudioSourceCount, "Audio Sources"},
        };

        private static readonly Dictionary<PerformanceRating, string> _performanceRatingDisplayNames = new Dictionary<PerformanceRating, string>
        {
            {PerformanceRating.None, "None"},
            {PerformanceRating.Excellent, "Excellent"},
            {PerformanceRating.Good, "Good"},
            {PerformanceRating.Medium, "Medium"},
            {PerformanceRating.Poor, "Poor"},
            {PerformanceRating.VeryPoor, "VeryPoor"}
        };

        #endregion

        #region Initialization

        private static AvatarPerformanceStatsLevelSet _performanceStatsLevelSet = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if(_performanceStatsLevelSet != null)
            {
                return;
            }

            _performanceStatsLevelSet = Resources.Load<AvatarPerformanceStatsLevelSet>(GetPlatformPerformanceStatLevels());
        }

        private static string GetPlatformPerformanceStatLevels()
        {
            return VRC.ValidationHelpers.IsStandalonePlatform()
                ? "Validation/Performance/StatsLevels/Windows/AvatarPerformanceStatLevels_Windows"
                : "Validation/Performance/StatsLevels/Quest/AvatarPerformanceStatLevels_Quest";
        }

        #endregion

        #region Constructors

        public AvatarPerformanceStats()
        {
            _performanceRatingCache = new PerformanceRating[(int)AvatarPerformanceCategory.AvatarPerformanceCategoryCount];
        }

        #endregion

        #region Public Methods

        public void Reset()
        {
            avatarName = null;
            polyCount = 0;
            aabb = new Bounds(Vector3.zero, Vector3.zero);
            skinnedMeshCount = 0;
            meshCount = 0;
            materialCount = 0;
            animatorCount = 0;
            boneCount = 0;
            lightCount = 0;
            particleSystemCount = 0;
            particleTotalCount = 0;
            particleMaxMeshPolyCount = 0;
            particleTrailsEnabled = false;
            particleCollisionEnabled = false;
            trailRendererCount = 0;
            lineRendererCount = 0;
            dynamicBoneComponentCount = 0;
            dynamicBoneSimulatedBoneCount = 0;
            dynamicBoneColliderCount = 0;
            dynamicBoneCollisionCheckCount = 0;
            clothCount = 0;
            clothMaxVertices = 0;
            physicsColliderCount = 0;
            physicsRigidbodyCount = 0;
            audioSourceCount = 0;

            for(int i = 0; i < (int)AvatarPerformanceCategory.AvatarPerformanceCategoryCount; i++)
            {
                _performanceRatingCache[i] = PerformanceRating.None;
            }
        }

        public PerformanceRating GetPerformanceRatingForCategory(AvatarPerformanceCategory perfCategory)
        {
            if(_performanceRatingCache[(int)perfCategory] == PerformanceRating.None)
            {
                _performanceRatingCache[(int)perfCategory] = CalculatePerformanceRatingForCategory(perfCategory);
            }

            return _performanceRatingCache[(int)perfCategory];
        }

        public void CalculateAllPerformanceRatings()
        {
            for(int i = 0; i < _performanceRatingCache.Length; i++)
            {
                _performanceRatingCache[i] = PerformanceRating.None;
            }

            foreach(AvatarPerformanceCategory perfCategory in Enum.GetValues(typeof(AvatarPerformanceCategory)))
            {
                if(perfCategory == AvatarPerformanceCategory.None ||
                   perfCategory == AvatarPerformanceCategory.AvatarPerformanceCategoryCount)
                {
                    continue;
                }

                if(_performanceRatingCache[(int)perfCategory] == PerformanceRating.None)
                {
                    _performanceRatingCache[(int)perfCategory] = CalculatePerformanceRatingForCategory(perfCategory);
                }
            }
        }

        public static string GetPerformanceCategoryDisplayName(AvatarPerformanceCategory category)
        {
            return _performanceCategoryDisplayNames[category];
        }

        public static string GetPerformanceRatingDisplayName(PerformanceRating rating)
        {
            return _performanceRatingDisplayNames[rating];
        }

        public static AvatarPerformanceStatsLevel GetStatLevelForRating(PerformanceRating rating)
        {
            switch(rating)
            {
                case PerformanceRating.None:
                    return _performanceStatsLevelSet.excellent;

                case PerformanceRating.Excellent:
                    return _performanceStatsLevelSet.excellent;

                case PerformanceRating.Good:
                    return _performanceStatsLevelSet.good;

                case PerformanceRating.Medium:
                    return _performanceStatsLevelSet.medium;

                case PerformanceRating.Poor:
                    return _performanceStatsLevelSet.poor;

                case PerformanceRating.VeryPoor:
                    return _performanceStatsLevelSet.poor;

                default:
                    return _performanceStatsLevelSet.excellent;
            }
        }

        #endregion

        #region Private Methods

        private PerformanceRating CalculatePerformanceRatingForCategory(AvatarPerformanceCategory perfCategory)
        {
            switch(perfCategory)
            {
                case AvatarPerformanceCategory.Overall:
                {
                    PerformanceRating maxRating = PerformanceRating.None;

                    foreach(AvatarPerformanceCategory category in Enum.GetValues(typeof(AvatarPerformanceCategory)))
                    {
                        if(category == AvatarPerformanceCategory.None ||
                           category == AvatarPerformanceCategory.Overall ||
                           category == AvatarPerformanceCategory.AvatarPerformanceCategoryCount)
                        {
                            continue;
                        }

                        PerformanceRating rating = GetPerformanceRatingForCategory(category);
                        if(rating > maxRating)
                        {
                            maxRating = rating;
                        }
                    }

                    return maxRating;
                }

                case AvatarPerformanceCategory.PolyCount:
                    return CalculatePerformanceRating((x, y) => x.polyCount - y.polyCount);

                case AvatarPerformanceCategory.AABB:
                {
                    return CalculatePerformanceRating(
                        (x, y) =>
                            ApproxLessOrEqual(y.aabb.extents.x, 0.0f) || // -1 extents means "no AABB limit"
                            (
                                ApproxLessOrEqual(x.aabb.extents.x, y.aabb.extents.x) &&
                                ApproxLessOrEqual(x.aabb.extents.y, y.aabb.extents.y) &&
                                ApproxLessOrEqual(x.aabb.extents.z, y.aabb.extents.z))
                                ? -1
                                : 1
                    );
                }

                case AvatarPerformanceCategory.SkinnedMeshCount:
                    return CalculatePerformanceRating((x, y) => x.skinnedMeshCount - y.skinnedMeshCount);

                case AvatarPerformanceCategory.MeshCount:
                    return CalculatePerformanceRating((x, y) => x.meshCount - y.meshCount);

                case AvatarPerformanceCategory.MaterialCount:
                    return CalculatePerformanceRating((x, y) => x.materialCount - y.materialCount);

                case AvatarPerformanceCategory.AnimatorCount:
                    return CalculatePerformanceRating((x, y) => x.animatorCount - y.animatorCount);

                case AvatarPerformanceCategory.BoneCount:
                    return CalculatePerformanceRating((x, y) => x.boneCount - y.boneCount);

                case AvatarPerformanceCategory.LightCount:
                    return CalculatePerformanceRating((x, y) => x.lightCount - y.lightCount);

                case AvatarPerformanceCategory.ParticleSystemCount:
                    return CalculatePerformanceRating((x, y) => x.particleSystemCount - y.particleSystemCount);

                case AvatarPerformanceCategory.ParticleTotalCount:
                    return CalculatePerformanceRating((x, y) => x.particleTotalCount - y.particleTotalCount);

                case AvatarPerformanceCategory.ParticleMaxMeshPolyCount:
                    return CalculatePerformanceRating((x, y) => x.particleMaxMeshPolyCount - y.particleMaxMeshPolyCount);

                case AvatarPerformanceCategory.ParticleTrailsEnabled:
                    return CalculatePerformanceRating(
                        (x, y) =>
                        {
                            if(x.particleTrailsEnabled == y.particleTrailsEnabled)
                            {
                                return 0;
                            }

                            return x.particleTrailsEnabled ? 1 : -1;
                        });

                case AvatarPerformanceCategory.ParticleCollisionEnabled:
                    return CalculatePerformanceRating(
                        (x, y) =>
                        {
                            if(x.particleCollisionEnabled == y.particleCollisionEnabled)
                            {
                                return 0;
                            }

                            return x.particleCollisionEnabled ? 1 : -1;
                        });

                case AvatarPerformanceCategory.TrailRendererCount:
                    return CalculatePerformanceRating((x, y) => x.trailRendererCount - y.trailRendererCount);

                case AvatarPerformanceCategory.LineRendererCount:
                    return CalculatePerformanceRating((x, y) => x.lineRendererCount - y.lineRendererCount);

                case AvatarPerformanceCategory.DynamicBoneComponentCount:
                    return CalculatePerformanceRating((x, y) => x.dynamicBoneComponentCount - y.dynamicBoneComponentCount);

                case AvatarPerformanceCategory.DynamicBoneSimulatedBoneCount:
                    return CalculatePerformanceRating((x, y) => x.dynamicBoneSimulatedBoneCount - y.dynamicBoneSimulatedBoneCount);

                case AvatarPerformanceCategory.DynamicBoneColliderCount:
                    return CalculatePerformanceRating((x, y) => x.dynamicBoneColliderCount - y.dynamicBoneColliderCount);

                case AvatarPerformanceCategory.DynamicBoneCollisionCheckCount:
                    return CalculatePerformanceRating((x, y) => x.dynamicBoneCollisionCheckCount - y.dynamicBoneCollisionCheckCount);

                case AvatarPerformanceCategory.ClothCount:
                    return CalculatePerformanceRating((x, y) => x.clothCount - y.clothCount);

                case AvatarPerformanceCategory.ClothMaxVertices:
                    return CalculatePerformanceRating((x, y) => x.clothMaxVertices - y.clothMaxVertices);

                case AvatarPerformanceCategory.PhysicsColliderCount:
                    return CalculatePerformanceRating((x, y) => x.physicsColliderCount - y.physicsColliderCount);

                case AvatarPerformanceCategory.PhysicsRigidbodyCount:
                    return CalculatePerformanceRating((x, y) => x.physicsRigidbodyCount - y.physicsRigidbodyCount);

                case AvatarPerformanceCategory.AudioSourceCount:
                    return CalculatePerformanceRating((x, y) => x.audioSourceCount - y.audioSourceCount);

                default:
                    return PerformanceRating.None;
            }
        }

        private PerformanceRating CalculatePerformanceRating(ComparePerformanceStatsDelegate compareFn)
        {
            if(compareFn(this, _performanceStatsLevelSet.excellent) <= 0)
            {
                return PerformanceRating.Excellent;
            }

            if(compareFn(this, _performanceStatsLevelSet.good) <= 0)
            {
                return PerformanceRating.Good;
            }

            if(compareFn(this, _performanceStatsLevelSet.medium) <= 0)
            {
                return PerformanceRating.Medium;
            }

            if(compareFn(this, _performanceStatsLevelSet.poor) <= 0)
            {
                return PerformanceRating.Poor;
            }

            return PerformanceRating.VeryPoor;
        }

        private static bool ApproxLessOrEqual(float x1, float x2)
        {
            float r = x1 - x2;
            return r < 0.0f || Mathf.Approximately(r, 0.0f);
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendFormat("Avatar Name: {0}\n", avatarName);
            sb.AppendFormat("Overall Performance: {0}\n", GetPerformanceRatingForCategory(AvatarPerformanceCategory.Overall));
            sb.AppendFormat("Poly Count: {0}\n", polyCount);
            sb.AppendFormat("Bounds: {0}\n", aabb.ToString());
            sb.AppendFormat("Skinned Mesh Count: {0}\n", skinnedMeshCount);
            sb.AppendFormat("Mesh Count: {0}\n", meshCount);
            sb.AppendFormat("Material Count: {0}\n", materialCount);
            sb.AppendFormat("Animator Count: {0}\n", animatorCount);
            sb.AppendFormat("Bone Count: {0}\n", boneCount);
            sb.AppendFormat("Light Count: {0}\n", lightCount);
            sb.AppendFormat("Particle System Count: {0}\n", particleSystemCount);
            sb.AppendFormat("Particle Total Count: {0}\n", particleTotalCount);
            sb.AppendFormat("Particle Max Mesh Poly Count: {0}\n", particleMaxMeshPolyCount);
            sb.AppendFormat("Particle Trails Enabled: {0}\n", particleTrailsEnabled);
            sb.AppendFormat("Particle Collision Enabled: {0}\n", particleCollisionEnabled);
            sb.AppendFormat("Trail Renderer Count: {0}\n", trailRendererCount);
            sb.AppendFormat("Line Renderer Count: {0}\n", lineRendererCount);
            sb.AppendFormat("Dynamic Bone Component Count: {0}\n", dynamicBoneComponentCount);
            sb.AppendFormat("Dynamic Bone Simulated Bone Count: {0}\n", dynamicBoneSimulatedBoneCount);
            sb.AppendFormat("Dynamic Bone Collider Count: {0}\n", dynamicBoneColliderCount);
            sb.AppendFormat("Dynamic Bone Collision Check Count: {0}\n", dynamicBoneCollisionCheckCount);
            sb.AppendFormat("Cloth Count: {0}\n", clothCount);
            sb.AppendFormat("Cloth Max Vertices: {0}\n", clothMaxVertices);
            sb.AppendFormat("Physics Collider Count: {0}\n", physicsColliderCount);
            sb.AppendFormat("Physics Rigidbody Count: {0}\n", physicsRigidbodyCount);

            return sb.ToString();
        }

        #endregion
    }
}
