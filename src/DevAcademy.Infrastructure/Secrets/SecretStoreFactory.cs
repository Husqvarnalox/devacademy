using DevAcademy.AI;

namespace DevAcademy.Infrastructure.Secrets;

/// <summary>An always-unavailable store (unsupported platform). Never stores anything: no plaintext fallback.</summary>
public sealed class UnavailableSecretStore(string reason) : ISecretStore
{
    public bool IsAvailable => false;
    public string? UnavailableReason => reason;
    public string? Get(string providerId) => throw new SecretStoreException(reason);
    public void Set(string providerId, string secret) => throw new SecretStoreException(reason);
    public void Delete(string providerId) => throw new SecretStoreException(reason);
}

public static class SecretStoreFactory
{
    public static ISecretStore CreateForCurrentOs() =>
        OperatingSystem.IsMacOS() ? new MacKeychainSecretStore()
        : OperatingSystem.IsWindows() ? new WindowsCredentialSecretStore()
        : OperatingSystem.IsLinux() ? new LinuxSecretServiceStore()
        : new UnavailableSecretStore("No OS secret store is supported on this platform.");
}
