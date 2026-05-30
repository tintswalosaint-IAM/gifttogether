using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GiftTogether.Services;

public class PaystackService
{
    private readonly HttpClient _http;
    private readonly string _secretKey;
    private readonly string _webhookSecret;

    // NEO flat fee per transaction (ZAR)
    public const decimal NeoFee = 2.00m;
    // Paystack fee: 2.9% + R1
    private const decimal PaystackPct = 0.029m;
    private const decimal PaystackFixed = 1.00m;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PaystackService(HttpClient http, IConfiguration config)
    {
        _secretKey = config["Paystack:SecretKey"]
            ?? throw new InvalidOperationException("Paystack:SecretKey is not configured.");
        _webhookSecret = config["Paystack:WebhookSecret"] ?? "";

        _http = http;
        _http.BaseAddress = new Uri("https://api.paystack.co/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _secretKey);
    }

    /// <summary>
    /// Calculates the gross amount to charge the contributor so the creator
    /// receives the full contribution amount after all fees.
    /// Formula: ceil((contributionAmount + NeoFee + PaystackFixed) / (1 - PaystackPct) * 100) / 100
    /// </summary>
    public static decimal CalculateGrossAmount(decimal contributionAmount)
    {
        var raw = (contributionAmount + NeoFee + PaystackFixed) / (1 - PaystackPct);
        return Math.Ceiling(raw * 100) / 100;
    }

    /// <summary>
    /// Returns the full itemised fee breakdown for display in the UI.
    /// This is the single source of truth — the frontend must display these values only.
    /// </summary>
    public static FeeBreakdown CalculateFeeBreakdown(decimal contributionAmount)
    {
        var grossAmount = CalculateGrossAmount(contributionAmount);
        var paystackFee = grossAmount - contributionAmount - NeoFee;
        return new FeeBreakdown(
            ContributionAmount: contributionAmount,
            NeoServiceFee: NeoFee,
            PaystackProcessingFee: Math.Round(paystackFee, 2),
            TotalCharged: grossAmount
        );
    }

    /// <summary>
    /// Initializes a Paystack transaction.
    /// Returns (accessCode, authorizationUrl, reference) on success.
    /// </summary>
    public async Task<(string AccessCode, string AuthorizationUrl)> InitializeTransactionAsync(
        string reference,
        decimal grossAmountZar,
        string email,
        string callbackUrl,
        string? subaccountCode,
        decimal contributionAmountZar)
    {
        // Paystack amounts are in kobo (cents × 100)
        var amountKobo = (int)(grossAmountZar * 100);
        var splitAmountKobo = (int)(contributionAmountZar * 100);

        var body = new Dictionary<string, object>
        {
            ["reference"] = reference,
            ["amount"] = amountKobo,
            ["email"] = email,
            ["currency"] = "ZAR",
            ["callback_url"] = callbackUrl
        };

        // If creator has a subaccount, split the payment
        if (!string.IsNullOrEmpty(subaccountCode))
        {
            body["subaccount"] = subaccountCode;
            body["bearer"] = "subaccount";
            body["transaction_charge"] = splitAmountKobo; // creator gets this amount
        }

        var res = await _http.PostAsJsonAsync("transaction/initialize", body, JsonOpts);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        if (!res.IsSuccessStatusCode || !json.GetProperty("status").GetBoolean())
        {
            var msg = json.TryGetProperty("message", out var m) ? m.GetString() : "Paystack error";
            throw new Exception(msg ?? "Failed to initialize Paystack transaction.");
        }

        var data = json.GetProperty("data");
        return (
            data.GetProperty("access_code").GetString()!,
            data.GetProperty("authorization_url").GetString()!
        );
    }

    /// <summary>
    /// Verifies a transaction by reference. Returns the transaction data if successful.
    /// Throws if the transaction is not found or not successful.
    /// </summary>
    public async Task<PaystackTransactionData> VerifyTransactionAsync(string reference)
    {
        var res = await _http.GetAsync($"transaction/verify/{Uri.EscapeDataString(reference)}");
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        if (!res.IsSuccessStatusCode || !json.GetProperty("status").GetBoolean())
        {
            var msg = json.TryGetProperty("message", out var m) ? m.GetString() : "Paystack error";
            throw new Exception(msg ?? "Failed to verify Paystack transaction.");
        }

        var data = json.GetProperty("data");
        return new PaystackTransactionData(
            Reference: data.GetProperty("reference").GetString()!,
            Status: data.GetProperty("status").GetString()!,
            AmountKobo: data.GetProperty("amount").GetInt32(),
            CustomerEmail: data.TryGetProperty("customer", out var cust)
                ? cust.TryGetProperty("email", out var em) ? em.GetString() ?? "" : ""
                : ""
        );
    }

    /// <summary>
    /// Verifies the HMAC-SHA512 signature on a Paystack webhook request.
    /// </summary>
    public bool VerifyWebhookSignature(string rawBody, string signature)
    {
        if (string.IsNullOrEmpty(_webhookSecret)) return false;
        var key = Encoding.UTF8.GetBytes(_webhookSecret);
        var body = Encoding.UTF8.GetBytes(rawBody);
        var hash = HMACSHA512.HashData(key, body);
        var computed = Convert.ToHexString(hash).ToLowerInvariant();
        return computed == signature.ToLowerInvariant();
    }

    /// <summary>
    /// Fetches the list of South African banks from Paystack.
    /// </summary>
    public async Task<List<PaystackBank>> GetBanksAsync()
    {
        var res = await _http.GetAsync("bank?currency=ZAR&perPage=100");
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        if (!res.IsSuccessStatusCode || !json.GetProperty("status").GetBoolean())
            throw new Exception("Failed to fetch bank list from Paystack.");

        var banks = new List<PaystackBank>();
        foreach (var b in json.GetProperty("data").EnumerateArray())
        {
            banks.Add(new PaystackBank(
                Name: b.GetProperty("name").GetString()!,
                Code: b.GetProperty("code").GetString()!
            ));
        }
        return banks;
    }

    /// <summary>
    /// Creates or updates a Paystack subaccount for a creator.
    /// Returns the subaccount_code.
    /// </summary>
    public async Task<string> CreateSubaccountAsync(
        string businessName, string bankCode, string accountNumber, string? existingCode)
    {
        if (!string.IsNullOrEmpty(existingCode))
        {
            // Update existing subaccount
            var updateBody = new { settlement_bank = bankCode, account_number = accountNumber };
            var updateRes = await _http.PutAsJsonAsync($"subaccount/{existingCode}", updateBody, JsonOpts);
            var updateJson = await updateRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
            if (!updateRes.IsSuccessStatusCode || !updateJson.GetProperty("status").GetBoolean())
            {
                var msg = updateJson.TryGetProperty("message", out var m) ? m.GetString() : "Paystack error";
                throw new Exception(msg ?? "Failed to update Paystack subaccount.");
            }
            return existingCode;
        }

        var body = new
        {
            business_name = businessName,
            settlement_bank = bankCode,
            account_number = accountNumber,
            percentage_charge = 0
        };
        var res = await _http.PostAsJsonAsync("subaccount", body, JsonOpts);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        if (!res.IsSuccessStatusCode || !json.GetProperty("status").GetBoolean())
        {
            var msg = json.TryGetProperty("message", out var m) ? m.GetString() : "Paystack error";
            throw new Exception(msg ?? "Failed to create Paystack subaccount.");
        }

        return json.GetProperty("data").GetProperty("subaccount_code").GetString()!;
    }
}

public record PaystackTransactionData(
    string Reference,
    string Status,
    int AmountKobo,
    string CustomerEmail);

public record PaystackBank(string Name, string Code);

public record FeeBreakdown(
    decimal ContributionAmount,
    decimal NeoServiceFee,
    decimal PaystackProcessingFee,
    decimal TotalCharged);
