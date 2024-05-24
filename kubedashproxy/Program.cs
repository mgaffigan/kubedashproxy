﻿using System.Diagnostics;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateSlimBuilder();
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("KUBEDASH_")
    .AddCommandLine(args);
var config = builder.Configuration.Get<KubeDashProxyConfig>() ?? new();
var url = config.ListenUrl;
builder.WebHost.UseUrls(url);
builder.Services.AddSingleton(config);
builder.Services.AddHostedService<PortForwardService>();
builder.Services.AddSingleton<TokenProvider>();
builder.Services.AddReverseProxy()
    .LoadFromMemory([
        new RouteConfig()
        {
            RouteId = "kubedash",
            ClusterId = "kubedash",
            Match = new RouteMatch { Path = "/{**catch-all}" }
        }
    ], [
        new ClusterConfig()
        {
            ClusterId = "kubedash",
            Destinations = new Dictionary<string, DestinationConfig>()
            {
                { "kubedash", new DestinationConfig() { Address = $"{config.TargetServiceScheme}://localhost:{config.PortForwardListenPort}" } }
            },
            HttpClient = new HttpClientConfig() { DangerousAcceptAnyServerCertificate = true }
        }
    ])
    .AddTransforms(b =>
    {
        b.AddRequestTransform(async rc =>
        {
            var tokenProvider = rc.HttpContext.RequestServices.GetRequiredService<TokenProvider>();
            rc.ProxyRequest.Headers.Add("Authorization", "Bearer " + await tokenProvider.GetTokenAsync());
        });
    });

await using var app = builder.Build();
app.MapReverseProxy();
await app.StartAsync();

// Open the browser
Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

await app.WaitForShutdownAsync();
