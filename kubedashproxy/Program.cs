using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

if (args.Intersect(new[] { "-?", "--help" }).Any())
{
    Console.Error.WriteLine("Usage: kubedashproxy [options]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Options:");
    foreach (var prop in typeof(KubeDashProxyConfig).GetProperties())
    {
        var attr = (DescriptionAttribute?)Attribute.GetCustomAttribute(prop, typeof(DescriptionAttribute));
        if (attr == null) continue;
        var val = prop.Name + "=...";
        Console.Error.WriteLine($"  --{val,-30} {attr.Description}");
    }
    Environment.Exit(-1);
    return;
}

var builder = WebApplication.CreateSlimBuilder();
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddInMemoryCollection(new Dictionary<string, string?>()
    {
        { "Logging:LogLevel:Default", "Information" },
        { "Logging:LogLevel:Microsoft.Hosting.Lifetime", "Warning" },
        { "Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics", "Warning" },
        { "Logging:LogLevel:Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager", "Warning" },
        { "Logging:LogLevel:Microsoft.AspNetCore.Routing.EndpointMiddleware", "Warning" },
        { "Logging:LogLevel:Yarp.ReverseProxy.Forwarder.HttpForwarder", "Warning" },
    })
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("KUBEDASH_")
    .AddCommandLine(args);
var config = builder.Configuration.Get<KubeDashProxyConfig>() ?? new();
if (config.PortForwardListenPort == 0) config.PortForwardListenPort = NextFreeTcpPort();
if (config.ListenUrl == null) config.ListenUrl = $"http://localhost:{NextFreeTcpPort()}";
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
app.Services.GetRequiredService<ILogger<Program>>().LogInformation("Listening on {url}", url);

// Open the browser
Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

await app.WaitForShutdownAsync();

static int NextFreeTcpPort()
{
    using var l = new TcpListener(IPAddress.Loopback, 0);
    l.Start();
    int port = ((IPEndPoint)l.LocalEndpoint).Port;
    l.Stop();
    return port;
}