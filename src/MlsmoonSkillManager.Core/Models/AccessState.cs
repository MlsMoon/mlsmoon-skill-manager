namespace MlsmoonSkillManager.Core.Models;

public enum AccessState
{
    Unknown = 0,
    Checking,
    Accessible,
    NoPermission,
    GhMissing,
    GhNotLoggedIn,
    OffNetwork,
    Unreachable,
    NeedsAuth
}

public sealed class GhAccountStatus
{
    public bool GhInstalled { get; init; }
    public bool LoggedIn { get; init; }
    public string Login { get; init; } = "";
    public string Detail { get; init; } = "";

    public AccessState AsAccessState()
    {
        if (!GhInstalled)
        {
            return AccessState.GhMissing;
        }

        return LoggedIn ? AccessState.Accessible : AccessState.GhNotLoggedIn;
    }
}

public sealed class RepoAccess
{
    public AccessState State { get; init; } = AccessState.Unknown;
    public string OwnerRepo { get; init; } = "";
    public string Visibility { get; init; } = "";
    public string Message { get; init; } = "";
    public bool CanInstall => State == AccessState.Accessible;
}
