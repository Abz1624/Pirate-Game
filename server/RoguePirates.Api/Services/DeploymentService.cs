namespace RoguePirates.Api.Services;

public sealed class DeploymentService
{
    public string ActiveContentVersion { get; private set; } = "baseline_v1";

    public void Rollout(string contentVersion) => ActiveContentVersion = contentVersion;

    public void Rollback(string previousVersion) => ActiveContentVersion = previousVersion;
}
