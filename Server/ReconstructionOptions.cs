public sealed class ReconstructionOptions
{
    public const string HTTP_CLIENT_NAME = "MemoAnchorReconstruction";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8765";
    public long MaxUploadBytes { get; set; } = 2L * 1024L * 1024L * 1024L;
    public int TimeoutMinutes { get; set; } = 30;
}
