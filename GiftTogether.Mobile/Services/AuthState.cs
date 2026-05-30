namespace GiftTogether.Mobile.Services;

/// <summary>
/// Holds the current user session. Registered as a singleton so all
/// Blazor components share the same login state.
/// </summary>
public class AuthState
{
    public string? Token { get; private set; }
    public int UserId { get; private set; }
    public string? UserName { get; private set; }
    public string? UserEmail { get; private set; }

    public bool IsLoggedIn => Token is not null;

    public event Action? OnChange;

    public void SetUser(AuthResponse response)
    {
        Token = response.Token;
        UserId = response.UserId;
        UserName = response.Name;
        UserEmail = response.Email;

        // Persist across app restarts
        Preferences.Set("gt_token", Token);
        Preferences.Set("gt_user_id", UserId);
        Preferences.Set("gt_user_name", UserName);
        Preferences.Set("gt_user_email", UserEmail);

        OnChange?.Invoke();
    }

    public void LoadFromPreferences()
    {
        Token = Preferences.Get("gt_token", null as string);
        UserId = Preferences.Get("gt_user_id", 0);
        UserName = Preferences.Get("gt_user_name", null as string);
        UserEmail = Preferences.Get("gt_user_email", null as string);
    }

    public void Logout()
    {
        Token = null;
        UserId = 0;
        UserName = null;
        UserEmail = null;

        Preferences.Remove("gt_token");
        Preferences.Remove("gt_user_id");
        Preferences.Remove("gt_user_name");
        Preferences.Remove("gt_user_email");

        OnChange?.Invoke();
    }
}
