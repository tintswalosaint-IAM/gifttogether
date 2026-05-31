using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GiftTogether.Mobile.Services;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Name, string Email, string Password, string? GuestMessage);
public record AuthResponse(int UserId, string Name, string Email, string Token);

public record CreatorInfo(string Name, string? ProfileImageUrl, string? GuestMessage);

public record RegistryResponse(
    int Id, string Name, string Description, string Slug,
    string? HeroBackgroundColor, string? HeroImageUrl,
    CreatorInfo Creator, List<GiftGoalResponse> GiftGoals);

public record GiftGoalResponse(
    int Id, string Name, string Description,
    decimal TargetAmount, decimal TotalRaised,
    string? ImageUrl, string? ProductLink,
    List<ContributionResponse> Contributions);

public record ContributionResponse(
    int Id, string ContributorName, string Message,
    decimal Amount, DateTime CreatedAt);

public record CreateRegistryRequest(string Name, string Description);
public record CreateGiftGoalRequest(string Name, string Description, decimal TargetAmount, string? ImageUrl, string? ProductLink);
public record CreateContributionRequest(string ContributorName, string Message, decimal Amount);
public record ScrapeResult(string? Title, decimal? Price, string? ImageUrl);

public record PaymentInitResponse(
    string Reference,
    string AccessCode,
    string AuthorizationUrl,
    decimal GrossAmount,
    decimal ContributionAmount,
    decimal NeoFee);

public record PaymentVerifyResponse(
    bool Confirmed,
    string? ContributorName,
    decimal Amount,
    string? Reference,
    decimal TotalRaised);

public record UserProfileResponse(
    int Id, string Name, string Email,
    string? ProfileImageUrl, string? GuestMessage,
    bool HasBankAccount);

public record PaystackBankResponse(string Name, string Code);

// ── API Service ───────────────────────────────────────────────────────────────

public class ApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _auth;

    // Switch between local dev and deployed URL:
    // - Windows app / emulator: http://localhost:5150
    // - Real Android device on same WiFi: http://10.191.130.84:5150
    // - Deployed on Render: https://your-app.onrender.com
#if ANDROID
    public const string BaseUrl = "http://10.191.130.84:5150";
#else
    public const string BaseUrl = "http://localhost:5150";
#endif

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ApiService(HttpClient http, AuthState auth)
    {
        _http = http;
        _auth = auth;
        _http.BaseAddress = new Uri(BaseUrl);
    }

    private void SetAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization = _auth.Token is not null
            ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _auth.Token)
            : null;
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    public async Task<AuthResponse> LoginAsync(string email, string password)
    {
        var res = await _http.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), JsonOpts);
        return await ReadOrThrow<AuthResponse>(res);
    }

    public async Task<AuthResponse> RegisterAsync(string name, string email, string password, string? guestMessage)
    {
        var res = await _http.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(name, email, password, guestMessage), JsonOpts);
        return await ReadOrThrow<AuthResponse>(res);
    }

    // ── Registries ────────────────────────────────────────────────────────────

    public async Task<List<RegistryResponse>> GetMyRegistriesAsync()
    {
        SetAuthHeader();
        var res = await _http.GetAsync("/api/registries");
        return await ReadOrThrow<List<RegistryResponse>>(res);
    }

    public async Task<RegistryResponse> CreateRegistryAsync(string name, string description)
    {
        SetAuthHeader();
        var res = await _http.PostAsJsonAsync("/api/registries",
            new CreateRegistryRequest(name, description), JsonOpts);
        return await ReadOrThrow<RegistryResponse>(res);
    }

    public async Task<RegistryResponse> GetRegistryBySlugAsync(string slug)
    {
        var res = await _http.GetAsync($"/api/registries/{slug}");
        return await ReadOrThrow<RegistryResponse>(res);
    }

    public async Task DeleteRegistryAsync(int id)
    {
        SetAuthHeader();
        var res = await _http.DeleteAsync($"/api/registries/{id}");
        if (!res.IsSuccessStatusCode) throw new Exception(await GetError(res));
    }

    // ── Goals ─────────────────────────────────────────────────────────────────

    public async Task<GiftGoalResponse> AddGoalAsync(int registryId, string name, string description, decimal amount, string? productLink)
    {
        SetAuthHeader();
        var res = await _http.PostAsJsonAsync($"/api/registries/{registryId}/goals",
            new CreateGiftGoalRequest(name, description, amount, null, productLink), JsonOpts);
        return await ReadOrThrow<GiftGoalResponse>(res);
    }

    public async Task DeleteGoalAsync(int registryId, int goalId)
    {
        SetAuthHeader();
        var res = await _http.DeleteAsync($"/api/registries/{registryId}/goals/{goalId}");
        if (!res.IsSuccessStatusCode) throw new Exception(await GetError(res));
    }

    public async Task<ScrapeResult> ScrapeProductAsync(string url)
    {
        SetAuthHeader();
        var res = await _http.PostAsJsonAsync("/api/scrape", new { url }, JsonOpts);
        return await ReadOrThrow<ScrapeResult>(res);
    }

    public async Task<string> UploadGoalImageAsync(
        int registryId,
        int goalId,
        Stream stream,
        string fileName,
        string contentType)
    {
        SetAuthHeader();
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "image", fileName);

        var res = await _http.PostAsync(
            $"/api/registries/{registryId}/goals/{goalId}/upload-image", content);

        if (!res.IsSuccessStatusCode)
            throw new Exception(await GetError(res));

        var doc = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(JsonOpts);
        return doc.GetProperty("url").GetString()!;
    }

    // ── Payments ──────────────────────────────────────────────────────────────

    public async Task<PaymentInitResponse> InitializePaymentAsync(
        int goalId, decimal amount, string? contributorName, string? message, string? email)
    {
        SetAuthHeader();
        var res = await _http.PostAsJsonAsync("/api/payments/initialize",
            new { goalId, contributionAmount = amount, contributorName, message, email }, JsonOpts);
        return await ReadOrThrow<PaymentInitResponse>(res);
    }

    public async Task<List<PaystackBankResponse>> GetBanksAsync()
    {
        var res = await _http.GetAsync("/api/payments/banks");
        return await ReadOrThrow<List<PaystackBankResponse>>(res);
    }

    public async Task RegisterBankAsync(string bankCode, string accountNumber, string accountHolderName)
    {
        SetAuthHeader();
        var res = await _http.PostAsJsonAsync("/api/payments/register-bank",
            new { bankCode, accountNumber, accountHolderName }, JsonOpts);
        if (!res.IsSuccessStatusCode) throw new Exception(await GetError(res));
    }

    public async Task<PaymentVerifyResponse> VerifyPaymentAsync(string reference)
    {
        var res = await _http.PostAsync($"/api/payments/verify/{Uri.EscapeDataString(reference)}", null);
        return await ReadOrThrow<PaymentVerifyResponse>(res);
    }

    // ── Profile ───────────────────────────────────────────────────────────────

    public async Task<UserProfileResponse> GetProfileAsync()
    {
        SetAuthHeader();
        var res = await _http.GetAsync("/api/auth/profile");
        return await ReadOrThrow<UserProfileResponse>(res);
    }

    public async Task UpdateGuestMessageAsync(string guestMessage)
    {
        SetAuthHeader();
        var res = await _http.PatchAsJsonAsync("/api/auth/profile",
            new { GuestMessage = guestMessage }, JsonOpts);
        if (!res.IsSuccessStatusCode) throw new Exception(await GetError(res));
    }

    // ── Contributions ─────────────────────────────────────────────────────────

    public async Task<ContributionResponse> ContributeAsync(int goalId, string contributorName, string message, decimal amount)
    {
        var res = await _http.PostAsJsonAsync($"/api/goals/{goalId}/contributions",
            new CreateContributionRequest(contributorName, message, amount), JsonOpts);
        return await ReadOrThrow<ContributionResponse>(res);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<T> ReadOrThrow<T>(HttpResponseMessage res)
    {
        if (res.IsSuccessStatusCode)
            return (await res.Content.ReadFromJsonAsync<T>(JsonOpts))!;

        var error = await GetError(res);
        throw new Exception(error);
    }

    private static async Task<string> GetError(HttpResponseMessage res)
    {
        try
        {
            var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
            if (doc.TryGetProperty("error", out var e)) return e.GetString() ?? res.ReasonPhrase ?? "Unknown error";
        }
        catch { }
        return $"Request failed ({(int)res.StatusCode})";
    }
}
