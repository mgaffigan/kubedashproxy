using Esatto.Win32.Processes;
using System.Diagnostics;

class PortForwardService(ILogger<PortForwardService> logger, KubeDashProxyConfig config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var job = new Job();
        var proc = Process.Start(new ProcessStartInfo()
        {
            FileName = config.KubectlPath,
            ArgumentList = { "-n", config.Namespace, "port-forward", config.TargetService, $"{config.PortForwardListenPort}:{config.TargetServicePort}" },
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
        await proc.WaitForExitAsync(stoppingToken);
        await watchStandardOut;
        await watchStandardError;
    }

    private async Task OnMessage(StreamReader stream, Action<string, string> log)
    {
        try
        {
            while (true)
            {
                var line = await stream.ReadLineAsync();
                if (line == null) break;
                log("kubectl port-forward: {out}", line);
            }
        }
        catch (ObjectDisposedException) { /* nop */ }
        catch (OperationCanceledException) { /* nop */ }
    }
}
