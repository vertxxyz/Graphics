using System;
using Unity.Collections;
using System.Collections.Generic;

namespace UnityEngine.Rendering.HighDefinition
{
    //Class representing lights in the context of a view.
    internal partial class HDVisibleLightEntities : IDisposable
    {
        static private HashSet<HDVisibleLightEntities> s_Instances = new HashSet<HDVisibleLightEntities>();

        public const int ArrayCapacity = 32;
        int m_Capacity = 0;
        int m_Size = 0;

        internal enum ProcessLightsCountSlots
        {
            ProcessedLights,
            DirectionalLights,
            PunctualLights,
            AreaLightCounts
        }

        [Flags]
        internal enum ShadowMapFlags
        {
            None = 0,
            WillRenderShadowMap = 1 << 0,
            WillRenderScreenSpaceShadow = 1 << 1,
            WillRenderRayTracedShadow = 1 << 2
        }

        NativeArray<int> m_ProcessVisibleLightCounts;

        #region visible lights SoA
        NativeArray<HDLightEntityData> m_VisibleEntities;
        NativeArray<LightBakingOutput> m_VisibleLightBakingOutput;
        NativeArray<LightShadows>      m_VisibleLightShadows;
        #endregion

        #region ProcessedLight data SoA
        //TODO: check perf and see if this needs to be a SoA
        NativeArray<int>             m_ProcessedVisibleLightIndices;
        NativeArray<HDLightType>     m_ProcessedLightTypes;
        NativeArray<LightCategory>   m_ProcessedLightCategories;
        NativeArray<GPULightType>    m_ProcessedGPULightType;
        NativeArray<LightVolumeType> m_ProcessedLightVolumeType;
        NativeArray<float>           m_ProcessedLightDistanceToCamera;
        NativeArray<float>           m_ProcessedLightDistanceFade;
        NativeArray<float>           m_ProcessedLightVolumetricDistanceFade;
        NativeArray<bool>            m_ProcessedLightIsBakedShadowMask;
        NativeArray<ShadowMapFlags>  m_ProcessedShadowMapFlags;
        NativeArray<uint>            m_SortKeys;

        private void ResizeArrays(int newCapacity)
        {
            m_Capacity = Math.Max(Math.Max(newCapacity, ArrayCapacity), m_Capacity * 2);
            m_VisibleEntities.ResizeArray(m_Capacity);
            m_VisibleLightBakingOutput.ResizeArray(m_Capacity);
            m_VisibleLightShadows.ResizeArray(m_Capacity);

            m_ProcessedVisibleLightIndices.ResizeArray(m_Capacity);
            m_ProcessedLightTypes.ResizeArray(m_Capacity);
            m_ProcessedLightCategories.ResizeArray(m_Capacity);
            m_ProcessedGPULightType.ResizeArray(m_Capacity);
            m_ProcessedLightVolumeType.ResizeArray(m_Capacity);
            m_ProcessedLightDistanceToCamera.ResizeArray(m_Capacity);
            m_ProcessedLightDistanceFade.ResizeArray(m_Capacity);
            m_ProcessedLightVolumetricDistanceFade.ResizeArray(m_Capacity);
            m_ProcessedLightIsBakedShadowMask.ResizeArray(m_Capacity);
            m_ProcessedShadowMapFlags.ResizeArray(m_Capacity);
            m_SortKeys.ResizeArray(m_Capacity);
        }

        private void DisposeArrays()
        {
            if (m_Capacity == 0)
                return;

            m_ProcessVisibleLightCounts.Dispose();

            m_VisibleEntities.Dispose();
            m_VisibleLightBakingOutput.Dispose();
            m_VisibleLightShadows.Dispose();

            m_ProcessedVisibleLightIndices.Dispose();
            m_ProcessedLightTypes.Dispose();
            m_ProcessedLightCategories.Dispose();
            m_ProcessedGPULightType.Dispose();
            m_ProcessedLightVolumeType.Dispose();
            m_ProcessedLightDistanceToCamera.Dispose();
            m_ProcessedLightDistanceFade.Dispose();
            m_ProcessedLightVolumetricDistanceFade.Dispose();
            m_ProcessedLightIsBakedShadowMask.Dispose();
            m_ProcessedShadowMapFlags.Dispose();
            m_SortKeys.Dispose();
            m_Capacity = 0;
            m_Size = 0;
        }

        ~HDVisibleLightEntities()
        {
            Dispose();
        }

        #endregion

        public static HDVisibleLightEntities Get()
        {
            var instance = UnsafeGenericPool<HDVisibleLightEntities>.Get();
            s_Instances.Add(instance);
            return instance;
        }

        public static void Release(HDVisibleLightEntities obj)
        {
            UnsafeGenericPool<HDVisibleLightEntities>.Release(obj);
        }

        public void PrepareLightsForGPU(
            HDCamera camera,
            in CullingResults cullingResult,
            in AOVRequestData aovRequestData,
            in GlobalLightLoopSettings lightLoopSettings,
            DebugDisplaySettings debugDisplaySettings)
        {
            BuildVisibleLightEntities(cullingResult);
            FilterVisibleLightsByAOV(aovRequestData);
            StartProcessVisibleLightJob(camera, cullingResult.visibleLights, lightLoopSettings, debugDisplaySettings);
            CompleteProcessVisibleLightJob();
            FilterProcessedLightsByDebugFilter(debugDisplaySettings);
        }

        public static void Cleanup()
        {
            foreach (var obj in s_Instances)
            {
                obj.Dispose();
            }
            s_Instances.Clear();
        }

        public void Dispose()
        {
            DisposeArrays();
        }

        public void Reset()
        {
            m_Size = 0;
            //Track object if its recycled. This avoids memory leaks between pipeline sessions (during cleanup all the buffers can get freed).
            if (!s_Instances.Contains(this))
                s_Instances.Add(this);
        }

        #region Internal implementation

        private void BuildVisibleLightEntities(in CullingResults cullResults)
        {
            m_Size = 0;
            HDLightEntityCollection.instance.CompleteLightTransformDataJobs();
            using (new ProfilingScope(null, ProfilingSampler.Get(HDProfileId.BuildVisibleLightEntities)))
            {
                if (cullResults.visibleLights.Length == 0
                    || HDLightEntityCollection.instance == null
                    || HDLightEntityCollection.instance.lightCount == 0)
                    return;

                if (cullResults.visibleLights.Length > m_Capacity)
                {
                    ResizeArrays(cullResults.visibleLights.Length);
                }

                m_Size = cullResults.visibleLights.Length;

                //TODO: this should be accelerated by a c++ API
                for (int i = 0; i < cullResults.visibleLights.Length; ++i)
                {
                    Light light = cullResults.visibleLights[i].light;
                    m_VisibleEntities[i] = HDLightEntityCollection.instance.FindEntity(light);
                    m_VisibleLightBakingOutput[i] = light.bakingOutput;
                    m_VisibleLightShadows[i] = light.shadows;
                }
            }
        }

        private void FilterVisibleLightsByAOV(AOVRequestData aovRequest)
        {
            if (!aovRequest.hasLightFilter)
                return;

            for (int i = 0; i < m_Size; ++i)
            {
                var visibleEntity = m_VisibleEntities[i];
                var go = HDLightEntityCollection.instance.aovGameObjects[visibleEntity.dataIndex];
                if (go == null)
                    continue;

                if (!aovRequest.IsLightEnabled(go))
                    m_VisibleEntities[i] = HDLightEntityData.Invalid;
            }
        }

        private void FilterProcessedLightsByDebugFilter(DebugDisplaySettings debugDisplaySettings)
        {
            var debugLightFilter = debugDisplaySettings.GetDebugLightFilterMode();
            if (debugLightFilter == DebugLightFilterMode.None)
                return;

            for (int i = 0; i < m_ProcessVisibleLightCounts[0]; ++i)
            {
                HDLightEntityData entity = m_VisibleEntities[i];
                if (!entity.valid)
                    continue;

                if (!debugLightFilter.IsEnabledFor(m_ProcessedGPULightType[i], HDLightEntityCollection.instance.spotLightShapes[entity.dataIndex]))
                    m_VisibleEntities[i] = HDLightEntityData.Invalid;
            }
        }

        #endregion
    }
}
