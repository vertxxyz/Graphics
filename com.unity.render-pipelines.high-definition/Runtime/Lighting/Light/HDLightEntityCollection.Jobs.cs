using System;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.Rendering.HighDefinition
{
    internal partial class HDLightEntityCollection
    {
        #region Transform jobs
        JobHandle m_LightTransformDataJob;

        private struct LightCopyTransformDataJob : IJobParallelForTransform
        {
            [WriteOnly]
            public NativeArray<float3> lightPositions;

#if ENABLE_BURST_1_5_0_OR_NEWER
            [Unity.Burst.BurstCompile]
#endif
            public void Execute(int index, TransformAccess transform)
            {
                lightPositions[index] = (float3)transform.position;
            }
        }

        public void StartLightTransformDataJobs()
        {
            if (!m_LightPositions.IsCreated || !lightTransforms.isCreated)
                return;

            var lightTransformJob = new LightCopyTransformDataJob()
            {
                lightPositions = m_LightPositions
            };

            m_LightTransformDataJob = lightTransformJob.ScheduleReadOnly(lightTransforms, 64);
        }

        public void CompleteLightTransformDataJobs()
        {
            m_LightTransformDataJob.Complete();
        }

        #endregion
    }
}
