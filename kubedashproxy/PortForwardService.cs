using Esatto.Win32.Processes;
using System.Diagnostics;

class PortForwardService(ILogger<PortForwardService> logger, KubeDashProxyConfig config, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var job = new Job();
        var ports = $"{config.PortForwardListenPort}:{config.TargetServicePort}";
        logger.LogInformation("Starting kubectl port-forward on {port}", ports);
        var proc = Process.Start(new ProcessStartInfo()
        {
            FileName = config.KubectlPath,
            ArgumentList = { "-n", config.Namespace, "port-forward", config.TargetService, ports },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not find kubectl");
        job.AddProcess(proc);

        using var _1 = stoppingToken.Register(() =>
        {
            logger.LogInformation("Stopping kubectl port-forward");
            proc.Kill();
        });

        proc.StandardInput.Close();
        var watchStandardOut = OnMessage(proc.StandardOutput, (m, l) => logger.LogInformation(m, l));
        var watchStandardError = OnMessage(proc.StandardError, (m, l) => logger.LogError(m, l));
        await proc.WaitForExitAsync();
        await watchStandardOut;
        await watchStandardError;
        if (proc.ExitCode != 0)
        {
            logger.LogError("kubectl port-forward exited with code {exitCode}", proc.ExitCode);
        }
        else
        {
            logger.LogInformation("kubectl port-forward exited");
        }

        lifetime.StopApplication();
    }

    private async Task OnMessage(StreamReader stream, Action<string, string> log)
    {
        try
        {
            while (true)
            {
                var line = await stream.ReadLineAsync().ConfigureAwait(false);
                if (line == null) break;
                log("kubectl port-forward: {out}", line);
            }
        }
        catch (ObjectDisposedException) { /* nop */ }
        catch (OperationCanceledException) { /* nop */ }
    }
}
