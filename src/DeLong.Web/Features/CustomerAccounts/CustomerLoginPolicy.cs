using Microsoft.AspNetCore.Identity;

namespace DeLong.Web.Features.CustomerAccounts;

public static class CustomerLoginPolicy
{
    public const int MaxFailedAccessAttempts = 15;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(1);

    public static IdentityOptions CreateOptions(IdentityOptions source) => new()
    {
        ClaimsIdentity = source.ClaimsIdentity,
        User = source.User,
        Password = source.Password,
        SignIn = source.SignIn,
        Tokens = source.Tokens,
        Stores = source.Stores,
        Lockout = new LockoutOptions
        {
            AllowedForNewUsers = source.Lockout.AllowedForNewUsers,
            MaxFailedAccessAttempts = MaxFailedAccessAttempts,
            DefaultLockoutTimeSpan = LockoutDuration
        }
    };
}
