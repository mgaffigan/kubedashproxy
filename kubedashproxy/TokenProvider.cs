using Microsoft.IdentityModel.JsonWebTokens;
using System.Diagnostics;

class TokenProvider(KubeDashProxyConfig config, ILogger<TokenProvider> logger)
{
    private SemaphoreSlim mutex = new(1);

    private DateTime? TokenExpDate;
    private string? LastToken;

    public async Task<string> GetTokenAsync()
    {
        await mutex.WaitAsync();
        try
        {
            if (TokenExpDate > DateTime.UtcNow)
            {
                return LastToken!;
            }

            logger.LogInformation("Acquiring new token");
            var proc = Process.Start(new ProcessStartInfo()
            {
                FileName = config.KubectlPath,
                ArgumentList = { "-n", config.Namespace, "create", "token", config.ServiceAccount },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("Could not find kubectl");
            proc.StandardInput.Close();
            var token = await proc.StandardOutput.ReadToEndAsync();
            var error = await proc.StandardError.ReadToEndAsync();
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException(error);
            }

            var jwtHandler = new JsonWebTokenHandler();
            var parsed = jwtHandler.ReadJsonWebToken(token);
            var expDate = parsed.ValidTo.AddMinutes(-30);
            if (expDate < DateTime.UtcNow)
            {
                throw new InvalidOperationException("Token is already expired or will expire in less than 30 minutes");
            }

            LastToken = token;
            TokenExpDate = expDate;
            return token;
        }
        finally
        {
            mutex.Release();
        }
    }
}