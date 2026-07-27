using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Options;

public sealed class ReconstructionProcessManager
{
    private readonly SemaphoreSlim startupLock = new(1, 1);
    private readonly ReconstructionOptions options;
    private readonly IWebHostEnvironment environment;
    private readonly ILogger<ReconstructionProcessManager> logger;
    private readonly HttpClient readinessClient;
    private Process? ownedProcess;

    public ReconstructionProcessManager(
        IOptions<ReconstructionOptions> options,
        IWebHostEnvironment environment,
        ILogger<ReconstructionProcessManager> logger)
    {
        this.options = options.Value;
        this.environment = environment;
        this.logger = logger;
        readinessClient = new HttpClient
        {
            BaseAddress = new Uri(this.options.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(1)
        };
    }

    public async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (await IsReadyAsync(cancellationToken))
        {
            return;
        }

        await startupLock.WaitAsync(cancellationToken);
        try
        {
            if (await IsReadyAsync(cancellationToken))
            {
                return;
            }

            if (ownedProcess is { HasExited: false })
            {
                try
                {
                    await WaitUntilReadyAsync(ownedProcess, cancellationToken);
                    return;
                }
                catch (HttpRequestException exception)
                {
                    logger.LogWarning(exception, "Replacing an unresponsive reconstruction process");
                    await WaitForFailedProcessExitAsync(ownedProcess, cancellationToken);
                    ownedProcess = null;
                }
            }

            ownedProcess?.Dispose();
            ownedProcess = StartProcess();
            await WaitUntilReadyAsync(ownedProcess, cancellationToken);
        }
        finally
        {
            startupLock.Release();
        }
    }

    private Process StartProcess()
    {
        string workingDirectory = ResolvePath(options.WorkingDirectory, environment.ContentRootPath);
        string serverScript = ResolvePath(options.ServerScriptPath, workingDirectory);
        string pythonExecutable = ResolveExecutable(options.PythonExecutable, workingDirectory);
        if (!Directory.Exists(workingDirectory))
        {
            throw new HttpRequestException($"Reconstruction directory not found: {workingDirectory}");
        }
        if (!File.Exists(serverScript))
        {
            throw new HttpRequestException($"Reconstruction server script not found: {serverScript}");
        }
        if (Path.IsPathRooted(pythonExecutable) && !File.Exists(pythonExecutable))
        {
            throw new HttpRequestException($"Reconstruction Python executable not found: {pythonExecutable}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(serverScript);
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add(new Uri(options.BaseUrl).Host);
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(new Uri(options.BaseUrl).Port.ToString());
        startInfo.ArgumentList.Add("--idle-timeout-seconds");
        startInfo.ArgumentList.Add(Math.Max(1, options.IdleTimeoutSeconds).ToString());
        startInfo.Environment["PYTHONUNBUFFERED"] = "1";

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, args) => LogProcessOutput(args.Data, false);
        process.ErrorDataReceived += (_, args) => LogProcessOutput(args.Data, true);
        try
        {
            if (!process.Start())
            {
                process.Dispose();
                throw new HttpRequestException("Could not start the reconstruction process.");
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            process.Dispose();
            throw new HttpRequestException("Could not start the reconstruction process.", exception);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        logger.LogInformation("Started reconstruction process {ProcessId} in {WorkingDirectory}", process.Id, workingDirectory);
        return process;
    }

    private async Task WaitUntilReadyAsync(Process process, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, options.StartupTimeoutSeconds));
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new HttpRequestException($"Reconstruction process exited during startup with code {process.ExitCode}.");
            }
            if (await IsReadyAsync(cancellationToken))
            {
                return;
            }

            await Task.Delay(200, cancellationToken);
        }

        try
        {
            process.Kill(true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
        }

        throw new HttpRequestException("Reconstruction process did not become ready before the startup timeout.");
    }

    private static async Task WaitForFailedProcessExitAsync(Process process, CancellationToken cancellationToken)
    {
        if (!process.HasExited)
        {
            try
            {
                process.Kill(true);
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
            {
            }
        }

        await process.WaitForExitAsync(cancellationToken);
        process.Dispose();
    }

    private async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await readinessClient.GetAsync(string.Empty, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private void LogProcessOutput(string? message, bool isError)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (isError)
        {
            logger.LogWarning("[Reconstruction] {Message}", message);
        }
        else
        {
            logger.LogInformation("[Reconstruction] {Message}", message);
        }
    }

    private static string ResolvePath(string path, string baseDirectory)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));
    }

    private static string ResolveExecutable(string executable, string workingDirectory)
    {
        if (Path.IsPathRooted(executable))
        {
            return Path.GetFullPath(executable);
        }

        return executable.Contains('/') || executable.Contains('\\')
            ? Path.GetFullPath(Path.Combine(workingDirectory, executable))
            : executable;
    }
}
