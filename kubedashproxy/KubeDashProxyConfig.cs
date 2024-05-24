using System.ComponentModel;

public class KubeDashProxyConfig
{
    [Description("Path to kubectl executable")]
    public string KubectlPath { get; set; } = "kubectl";

    [Description("Kubernetes service and service-account namespace")]
    public string Namespace { get; set; } = "kubernetes-dashboard";

    [Description("Upstream service or pod name")]
    public string TargetService { get; set; } = "service/kubernetes-dashboard-kong-proxy";

    // String to work around https://stackoverflow.com/questions/78530829
    [Description("Service port to which traffic is sent")]
    public string TargetServicePort { get; set; } = "443";

    [Description("Protocol to send to upstream")]
    public string TargetServiceScheme { get; set; } = "https";

    [Description("Service account to use for token generation")]
    public string ServiceAccount { get; set; } = "kubernetes-dashboard";

    [Description("Port to use for kubectl port-forward")]
    public int PortForwardListenPort { get; set; } = 0;

    [Description("URL to listen on")]
    public string ListenUrl { get; set; } = null!;

    [Description("Suppress Launching web browser")]
    public bool DoNotLaunchBrowser { get; set; } = false;
}
