using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using VRCSDK2.Validation.Performance.Stats;

namespace VRCSDK2.Validation.Performance.Scanners
{
    #if VRC_CLIENT
    [CreateAssetMenu(
        fileName = "New TrailRendererPerformanceScanner",
        menuName = "VRC Scriptable Objects/Performance/Avatar/Scanners/TrailRendererPerformanceScanner"
    )]
    #endif
    public sealed class TrailRendererPerformanceScanner : AbstractPerformanceScanner
    {
        public override IEnumerator RunPerformanceScanEnumerator(GameObject avatarObject, AvatarPerformanceStats perfStats, AvatarPerformance.IgnoreDelegate shouldIgnoreComponent)
        {
            // Trail Renderers
            List<TrailRenderer> trailRendererBuffer = new List<TrailRenderer>();
            yield return ScanAvatarForComponentsOfType(avatarObject, trailRendererBuffer);
            if(shouldIgnoreComponent != null)
            {
                trailRendererBuffer.RemoveAll(c => shouldIgnoreComponent(c));
            }

            int numTrailRenderers = trailRendererBuffer.Count;
            perfStats.trailRendererCount += numTrailRenderers;
            perfStats.materialCount += numTrailRenderers;
        }
    }
}
