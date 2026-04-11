namespace ObjLoader.Services.Rendering.Passes;

internal sealed class ApiObjectRenderPass : IRenderPass
{
    public void Render(in RenderPassContext context)
    {
        if (context.ApiObjectRenderer == null || context.DrawManagerAdapter == null) return;

        if (!context.IsWireframe) context.DeviceContext.RSSetState(context.Resources.RasterizerState);
        var viewProj = context.View * context.Proj;
        context.ApiObjectRenderer.RenderApiObjects(context.DeviceContext, context.DrawManagerAdapter, viewProj, new System.Numerics.Vector4(context.CamPos, 1.0f), null, null, false, 0, false);

        context.ApiObjectRenderer.RenderBillboards(context.DeviceContext, context.DrawManagerAdapter, viewProj, new System.Numerics.Vector4(context.CamPos, 1.0f), null, null, false, 0, false);
    }
}