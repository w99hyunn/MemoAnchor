public sealed class ReconstructionOptions
{
    public const string HTTP_CLIENT_NAME = "MemoAnchorReconstruction";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8765";
    public string WorkingDirectory { get; set; } = "/home/ubuntu/MemoAnchor/Reconstruction";
    public string PythonExecutable { get; set; } = ".venv/bin/python";
    public string ServerScriptPath { get; set; } = "server.py";
    public int IdleTimeoutSeconds { get; set; } = 60;
    public int StartupTimeoutSeconds { get; set; } = 20;
    public long MaxUploadBytes { get; set; } = 5L * 1024L * 1024L * 1024L;
    public int TimeoutMinutes { get; set; } = 30;
}
