public sealed class ReconstructionProcessHandler : DelegatingHandler
{
    private readonly ReconstructionProcessManager processManager;

    public ReconstructionProcessHandler(ReconstructionProcessManager processManager)
    {
        this.processManager = processManager;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await processManager.EnsureStartedAsync(cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
