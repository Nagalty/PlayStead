namespace PlayStead.Core.Identity;

public enum IdentityResolutionState
{
    MatchConfirmed = 1,
    MatchProbable = 2,
    Ambiguous = 3,
    New = 4
}
