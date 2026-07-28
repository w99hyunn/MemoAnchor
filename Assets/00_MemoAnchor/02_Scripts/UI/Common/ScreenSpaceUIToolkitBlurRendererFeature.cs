using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace MemoAnchor.UI
{
    public sealed class ScreenSpaceUIToolkitBlurRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Material blurMaterial;
        [SerializeField] private RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
        [SerializeField, Range(1, 4)] private int downsample = 2;
        [SerializeField, Range(1, 4)] private int iterations = 2;
        [SerializeField, Range(0.5f, 12f)] private float blurRadius = 2.5f;

        private BlurPass blurPass;
        private static int activeElementCount;

        public static Texture BlurTexture => BlurPass.OutputTexture;
        private static bool HasActiveElements => activeElementCount > 0;

        internal static void RegisterElement()
        {
            activeElementCount++;
        }

        internal static void UnregisterElement()
        {
            activeElementCount = Mathf.Max(0, activeElementCount - 1);
        }

        public override void Create()
        {
            blurPass = new BlurPass
            {
                renderPassEvent = passEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (!HasActiveElements || camera.cameraType != CameraType.Game ||
                camera.targetTexture != null || blurMaterial == null)
            {
                return;
            }

            blurPass.SetSettings(blurMaterial, downsample, iterations, blurRadius);
            renderer.EnqueuePass(blurPass);
        }

        protected override void Dispose(bool disposing)
        {
            blurPass.Dispose();
            blurPass = null;
        }

        private sealed class BlurPass : ScriptableRenderPass
        {
            private static readonly int BLUR_OFFSET_ID = Shader.PropertyToID("_BlurOffset");
            private static readonly int BLUR_TEXTURE_ID = Shader.PropertyToID("_ScreenSpaceUIToolkitBlurTexture");

            private Material material;
            private int downsample = 2;
            private int iterations = 2;
            private float blurRadius = 2.5f;
            private RTHandle blurATexture;
            private RTHandle blurBTexture;

            public static Texture OutputTexture { get; private set; }

            public BlurPass()
            {
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Color);
            }

            public void SetSettings(Material material, int downsample, int iterations, float blurRadius)
            {
                this.material = material;
                this.downsample = Mathf.Clamp(downsample, 1, 4);
                this.iterations = Mathf.Clamp(iterations, 1, 4);
                this.blurRadius = Mathf.Max(blurRadius, 0.5f);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                Camera camera = cameraData.camera;
                if (camera.cameraType != CameraType.Game || camera.targetTexture != null ||
                    material == null || resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                TextureHandle source = resourceData.activeColorTexture;
                if (!source.IsValid())
                    source = resourceData.cameraOpaqueTexture;
                if (!source.IsValid())
                    return;

                RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;
                descriptor.width = Mathf.Max(1, descriptor.width / downsample);
                descriptor.height = Mathf.Max(1, descriptor.height / downsample);

                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref blurATexture,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_ScreenSpaceUIToolkitBlurTextureA");
                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref blurBTexture,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_ScreenSpaceUIToolkitBlurTextureB");

                TextureHandle blurAHandle = renderGraph.ImportTexture(blurATexture);
                TextureHandle blurBHandle = renderGraph.ImportTexture(blurBTexture);
                if (!blurAHandle.IsValid() || !blurBHandle.IsValid())
                    return;

                OutputTexture = blurATexture.rt;
                Shader.SetGlobalTexture(BLUR_TEXTURE_ID, blurATexture);
                renderGraph.AddBlitPass(
                    CreateBlitParameters(source, blurAHandle, 0, 0f),
                    "ScreenSpace UIToolkit Blur Downsample");

                TextureHandle input = blurAHandle;
                TextureHandle output = blurBHandle;
                for (int i = 0; i < iterations; i++)
                {
                    float offset = blurRadius + i;
                    renderGraph.AddBlitPass(
                        CreateBlitParameters(input, output, 1, offset),
                        "ScreenSpace UIToolkit Blur Horizontal");
                    (input, output) = (output, input);

                    using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                               CreateBlitParameters(input, output, 2, offset),
                               "ScreenSpace UIToolkit Blur Vertical",
                               true))
                    {
                        if (i == iterations - 1)
                            builder.SetGlobalTextureAfterPass(output, BLUR_TEXTURE_ID);
                    }

                    (input, output) = (output, input);
                }
            }

            public void Dispose()
            {
                if (OutputTexture == blurATexture?.rt || OutputTexture == blurBTexture?.rt)
                    OutputTexture = null;

                blurATexture?.Release();
                blurBTexture?.Release();
                blurATexture = null;
                blurBTexture = null;
            }

            private RenderGraphUtils.BlitMaterialParameters CreateBlitParameters(
                TextureHandle source,
                TextureHandle destination,
                int passIndex,
                float offset)
            {
                var propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetFloat(BLUR_OFFSET_ID, offset);
                return new RenderGraphUtils.BlitMaterialParameters(
                    source,
                    destination,
                    material,
                    passIndex,
                    propertyBlock,
                    0,
                    0,
                    geometry: RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle);
            }
        }
    }
}
