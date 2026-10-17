using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// Draws the CRT shader over the whole camera image, after everything else.
// CRTSettings adds this pass to every game camera from code each frame, so no
// renderer asset or scene needs to be set up for the filter to show.
public class CRTRenderPass : ScriptableRenderPass
{
    readonly Material material;

    public CRTRenderPass(Material material)
    {
        this.material = material;
        renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        // We read the picture and write it back, so URP must render into a texture
        // we can read, not straight to the screen.
        requiresIntermediateTexture = true;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resources = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        // With stacked cameras, only filter once, on the last one.
        if (!cameraData.resolveFinalTarget) return;
        // Already on the screen: nothing we can read back from.
        if (resources.isActiveTargetBackBuffer) return;

        // A shader can't read and write the same texture, so copy the picture first,
        // then draw it back through the CRT shader.
        TextureHandle source = resources.activeColorTexture;
        TextureDesc desc = renderGraph.GetTextureDesc(source);
        desc.name = "CRT Copy";
        desc.clearBuffer = false;
        TextureHandle copy = renderGraph.CreateTexture(desc);

        renderGraph.AddBlitPass(source, copy, Vector2.one, Vector2.zero, passName: "CRT Copy");
        renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(copy, source, material, 0), "CRT Filter");
    }
}
