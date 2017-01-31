using System;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.HDPipeline;

using UnityObject = UnityEngine.Object;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    [Flags]
    public enum ClearFlag
    {
        ClearNone  = 0,
        ClearColor = 1,
        ClearDepth = 2
    }

    [Flags]
    public enum StencilBits
    {
        None = 0,  
        SSS  = 1,  // BaseLitGUI.MaterialClass.SSS
        Hair = 2,  // BaseLitGUI.MaterialClass.Hair
        All  = 255 // 0xff
    }

    public class Utilities
    {
        public const RendererConfiguration kRendererConfigurationBakedLighting = RendererConfiguration.PerObjectLightProbe | RendererConfiguration.PerObjectLightmaps | RendererConfiguration.PerObjectLightProbeProxyVolume;


        // Render Target Management.
        public const ClearFlag kClearAll = ClearFlag.ClearDepth | ClearFlag.ClearColor;

        public static void SetRenderTarget(ScriptableRenderContext renderContext, RenderTargetIdentifier buffer, ClearFlag clearFlag, Color clearColor, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown)
        {
            var cmd = new CommandBuffer();
            cmd.name = "";
            cmd.SetRenderTarget(buffer, miplevel, cubemapFace);
            if (clearFlag != ClearFlag.ClearNone)
                cmd.ClearRenderTarget((clearFlag & ClearFlag.ClearDepth) != 0, (clearFlag & ClearFlag.ClearColor) != 0, clearColor);
            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Dispose();
        }

        public static void SetRenderTarget(ScriptableRenderContext renderContext, RenderTargetIdentifier buffer, ClearFlag clearFlag = ClearFlag.ClearNone, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown)
        {
            SetRenderTarget(renderContext, buffer, clearFlag, Color.black, miplevel, cubemapFace);
        }

        public static void SetRenderTarget(ScriptableRenderContext renderContext, RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthBuffer, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown)
        {
            SetRenderTarget(renderContext, colorBuffer, depthBuffer, ClearFlag.ClearNone, Color.black, miplevel, cubemapFace);
        }

        public static void SetRenderTarget(ScriptableRenderContext renderContext, RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthBuffer, ClearFlag clearFlag, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown)
        {
            SetRenderTarget(renderContext, colorBuffer, depthBuffer, clearFlag, Color.black, miplevel, cubemapFace);
        }

        public static void SetRenderTarget(ScriptableRenderContext renderContext, RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthBuffer, ClearFlag clearFlag, Color clearColor, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown)
        {
            var cmd = new CommandBuffer();
            cmd.name = "";
            cmd.SetRenderTarget(colorBuffer, depthBuffer, miplevel, cubemapFace);
            if (clearFlag != ClearFlag.ClearNone)
                cmd.ClearRenderTarget((clearFlag & ClearFlag.ClearDepth) != 0, (clearFlag & ClearFlag.ClearColor) != 0, clearColor);
            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Dispose();
        }

        public static void SetRenderTarget(ScriptableRenderContext renderContext, RenderTargetIdentifier[] colorBuffers, RenderTargetIdentifier depthBuffer)
        {
            SetRenderTarget(renderContext, colorBuffers, depthBuffer, ClearFlag.ClearNone, Color.black);
        }

        public static void SetRenderTarget(ScriptableRenderContext renderContext, RenderTargetIdentifier[] colorBuffers, RenderTargetIdentifier depthBuffer, ClearFlag clearFlag = ClearFlag.ClearNone)
        {
            SetRenderTarget(renderContext, colorBuffers, depthBuffer, clearFlag, Color.black);
        }

        public static void SetRenderTarget(ScriptableRenderContext renderContext, RenderTargetIdentifier[] colorBuffers, RenderTargetIdentifier depthBuffer, ClearFlag clearFlag, Color clearColor)
        {
            var cmd = new CommandBuffer();
            cmd.name = "";
            cmd.SetRenderTarget(colorBuffers, depthBuffer);
            if (clearFlag != ClearFlag.ClearNone)
                cmd.ClearRenderTarget((clearFlag & ClearFlag.ClearDepth) != 0, (clearFlag & ClearFlag.ClearColor) != 0, clearColor);
            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Dispose();
        }

        // Miscellanous
        public static Material CreateEngineMaterial(string shaderPath)
        {
            var mat = new Material(Shader.Find(shaderPath))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return mat;
        }

        public static void Destroy(UnityObject obj)
        {
            if (obj != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                    UnityObject.Destroy(obj);
                else
                    UnityObject.DestroyImmediate(obj);
#else
                UnityObject.Destroy(obj);
#endif
                obj = null;
            }
        }

        public static void SafeRelease(ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }
        }

        public class ProfilingSample
            : IDisposable
        {
            bool        disposed = false;
            ScriptableRenderContext  renderContext;
            string      name;

            public ProfilingSample(string _name, ScriptableRenderContext _renderloop)
            {
                renderContext = _renderloop;
                name = _name;

                CommandBuffer cmd = new CommandBuffer();
                cmd.name = "";
                cmd.BeginSample(name);
                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Dispose();
            }

            ~ProfilingSample()
            {
                Dispose(false);
            }

            public void Dispose()
            {
                Dispose(true);
            }

            // Protected implementation of Dispose pattern.
            protected virtual void Dispose(bool disposing)
            {
                if (disposed)
                    return;

                if (disposing)
                {
                    CommandBuffer cmd = new CommandBuffer();
                    cmd.name = "";
                    cmd.EndSample(name);
                    renderContext.ExecuteCommandBuffer(cmd);
                    cmd.Dispose();
                }

                disposed = true;
            }
        }

        public static Matrix4x4 GetViewProjectionMatrix(Matrix4x4 worldToViewMatrix, Matrix4x4 projectionMatrix)
        {
            // The actual projection matrix used in shaders is actually massaged a bit to work across all platforms
            // (different Z value ranges etc.)
            var gpuProj = GL.GetGPUProjectionMatrix(projectionMatrix, false);
            var gpuVP = gpuProj *  worldToViewMatrix * Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f)); // Need to scale -1.0 on Z to match what is being done in the camera.wolrdToCameraMatrix API.

            return gpuVP;
        }

        public static HDCamera GetHDCamera(Camera camera)
        {
            HDCamera hdCamera = new HDCamera();
            hdCamera.camera = camera;
            hdCamera.screenSize = new Vector4(camera.pixelWidth, camera.pixelHeight, 1.0f / camera.pixelWidth, 1.0f / camera.pixelHeight);

            // The actual projection matrix used in shaders is actually massaged a bit to work across all platforms
            // (different Z value ranges etc.)
            var gpuProj = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            var gpuVP = gpuProj * camera.worldToCameraMatrix;

            hdCamera.viewProjectionMatrix = gpuVP;
            hdCamera.invViewProjectionMatrix = gpuVP.inverse;

            return hdCamera;
        }

        public static void SetupMaterialHDCamera(HDCamera hdCamera, Material material)
        {
            material.SetVector("_ScreenSize", hdCamera.screenSize);
            material.SetMatrix("_ViewProjMatrix", hdCamera.viewProjectionMatrix);
            material.SetMatrix("_InvViewProjMatrix", hdCamera.invViewProjectionMatrix);
        }

        // TEMP: These functions should be implemented C++ side, for now do it in C#
        public static void SetMatrixCS(CommandBuffer cmd, ComputeShader shadercs, string name, Matrix4x4 mat)
        {
            var data = new float[16];

            for (int c = 0; c < 4; c++)
                for (int r = 0; r < 4; r++)
                    data[4 * c + r] = mat[r, c];

            cmd.SetComputeFloatParams(shadercs, name, data);
        }

        public static void SetMatrixArrayCS(CommandBuffer cmd, ComputeShader shadercs, string name, Matrix4x4[] matArray)
        {
            int numMatrices = matArray.Length;
            var data = new float[numMatrices * 16];

            for (int n = 0; n < numMatrices; n++)
                for (int c = 0; c < 4; c++)
                    for (int r = 0; r < 4; r++)
                        data[16 * n + 4 * c + r] = matArray[n][r, c];

            cmd.SetComputeFloatParams(shadercs, name, data);
        }

        public static void SetVectorArrayCS(CommandBuffer cmd, ComputeShader shadercs, string name, Vector4[] vecArray)
        {
            int numVectors = vecArray.Length;
            var data = new float[numVectors * 4];

            for (int n = 0; n < numVectors; n++)
                for (int i = 0; i < 4; i++)
                    data[4 * n + i] = vecArray[n][i];

            cmd.SetComputeFloatParams(shadercs, name, data);
        }

        public static void SetKeyword(Material m, string keyword, bool state)
        {
            if (state)
                m.EnableKeyword(keyword);
            else
                m.DisableKeyword(keyword);
        }

        public static void SelectKeyword(Material material, string keyword1, string keyword2, bool enableFirst)
        {
            material.EnableKeyword (enableFirst ? keyword1 : keyword2);
            material.DisableKeyword(enableFirst ? keyword2 : keyword1);
        }

        public static void SelectKeyword(Material material, string[] keywords, int enabledKeywordIndex)
        {
            material.EnableKeyword(keywords[enabledKeywordIndex]);

            for (int i = 0; i < keywords.Length; i++)
            {
                if (i != enabledKeywordIndex)
                {
                    material.DisableKeyword(keywords[i]);
                }
            }
        }

        public static HDRenderPipeline GetHDRenderPipeline()
        {
            HDRenderPipeline renderContext = GraphicsSettings.renderPipelineAsset as HDRenderPipeline;
            if (renderContext == null)
            {
                Debug.LogWarning("SkyParameters component can only be used with HDRenderPipeline custom RenderPipeline.");
                return null;
            }

            return renderContext;
        }

        static Mesh m_ScreenSpaceTriangle = null;

        static Mesh GetScreenSpaceTriangle()
        {
            // If the assembly has been reloaded, the pointer will become NULL.
            if (!m_ScreenSpaceTriangle)
            {
                m_ScreenSpaceTriangle = new Mesh
                {
                    // Note: neither the vertex nor the index data is actually used if the vertex shader computes vertices
                    // using 'SV_VertexID'. However, there is currently no way to bind NULL vertex or index buffers.
                    vertices  = new[] { new Vector3(-1, -1, 1), new Vector3(3, -1, 1), new Vector3(-1, 3, 1) },
                    triangles = new[] { 0, 1, 2 }
                };
            }

            return m_ScreenSpaceTriangle;
        }

        // Draws a full screen triangle as a faster alternative to drawing a full-screen quad.
        public static void DrawFullscreen(CommandBuffer commandBuffer, Material material, HDCamera camera,
                                          RenderTargetIdentifier colorBuffer,
                                          MaterialPropertyBlock properties = null, int shaderPassID = 0)
        {
            SetupMaterialHDCamera(camera, material);
            commandBuffer.SetRenderTarget(colorBuffer);
            commandBuffer.DrawMesh(GetScreenSpaceTriangle(), Matrix4x4.identity, material, 0, shaderPassID, properties);
        }

        // Draws a full screen triangle as a faster alternative to drawing a full-screen quad.
        public static void DrawFullscreen(CommandBuffer commandBuffer, Material material, HDCamera camera,
                                          RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthStencilBuffer,
                                          MaterialPropertyBlock properties = null, int shaderPassID = 0)
        {
            SetupMaterialHDCamera(camera, material);
            commandBuffer.SetRenderTarget(colorBuffer, depthStencilBuffer);
            commandBuffer.DrawMesh(GetScreenSpaceTriangle(), Matrix4x4.identity, material, 0, shaderPassID, properties);
        }

        // Draws a full screen triangle as a faster alternative to drawing a full-screen quad.
        public static void DrawFullscreen(CommandBuffer commandBuffer, Material material, HDCamera camera,
                                          RenderTargetIdentifier[] colorBuffers, RenderTargetIdentifier depthStencilBuffer,
                                          MaterialPropertyBlock properties = null, int shaderPassID = 0)
        {
            SetupMaterialHDCamera(camera, material);
            commandBuffer.SetRenderTarget(colorBuffers, depthStencilBuffer);
            commandBuffer.DrawMesh(GetScreenSpaceTriangle(), Matrix4x4.identity, material, 0, shaderPassID, properties);
        }

        // Draws a full screen triangle as a faster alternative to drawing a full-screen quad.
        // Important: the first RenderTarget must be created with 0 depth bits!
        public static void DrawFullscreen(CommandBuffer commandBuffer, Material material, HDCamera camera,
                                          RenderTargetIdentifier[] colorBuffers,
                                          MaterialPropertyBlock properties = null, int shaderPassID = 0)
        {
            // It is currently not possible to have MRT without also setting a depth target.
            // To work around this deficiency of the CommandBuffer.SetRenderTarget() API,
            // we pass the first color target as the depth target. If it has 0 depth bits,
            // no depth target ends up being bound.
            DrawFullscreen(commandBuffer, material, camera, colorBuffers, colorBuffers[0], properties, shaderPassID);
        }
    }
}
