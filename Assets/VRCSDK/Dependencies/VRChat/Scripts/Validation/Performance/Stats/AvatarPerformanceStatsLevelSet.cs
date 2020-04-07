using UnityEngine;
using UnityEngine.Serialization;

namespace VRCSDK2.Validation.Performance.Stats
{
    public class AvatarPerformanceStatsLevelSet : ScriptableObject
    {
        [FormerlySerializedAs("veryGood")]
        public AvatarPerformanceStatsLevel excellent;
        public AvatarPerformanceStatsLevel good;
        public AvatarPerformanceStatsLevel medium;
        [FormerlySerializedAs("bad")]
        public AvatarPerformanceStatsLevel poor;
    }
}
