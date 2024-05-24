public class KubeDashProxyConfig
{
    public string KubectlPath { get; set; } = "kubectl";
    public string Namespace { get; set; } = "kubernetes-dashboard";
    public string TargetService { get; set; } = "service/kubernetes-dashboard-kong-proxy";
    public int TargetServicePort { get; set; } = 443;
    public string TargetServiceScheme { get; set; } = "https";
    public string ServiceAccountName { get; set; } = "kubernetes-dashboard";
    public int PortForwardListenPort { get; set; } = 34943;
    public string ListenUrl { get; set; } = "http://localhost:34980";
}
