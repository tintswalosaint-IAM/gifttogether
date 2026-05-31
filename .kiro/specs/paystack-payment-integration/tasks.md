# Implementation Plan: Paystack Payment Integration

## Overview

Integrate Paystack as the payment processor for NEO. The backend (ASP.NET Core) gains a `PaymentController`, a typed `PaystackService`, a `PendingPayment` entity, and EF Core migrations. The mobile app (MAUI Blazor Hybrid) gains a `PaymentService`, four new Razor pages (BankSetup, PaymentSummary, PaymentProcessing, PaymentSuccess), and a deep link handler that catches the Paystack callback URI.

Backend tasks must be sequenced before mobile tasks. Within the backend, data model → service → controller. Within mobile, DTOs/service → screens → deep link wiring.

---

## Tasks

### Backend

- [ ] 1. Validate Paystack configuration at startup
  - In `GiftTogether/Program.cs`, after `builder.Build()`, read `builder.Configuration["Paystack:SecretKey"]`; if null or whitespace, throw `InvalidOperationException("Paystack:SecretKey is required")` to refuse startup
  - Add placeholder keys to `GiftTogether/appsettings.json` under `"Paystack": { "SecretKey": "", "WebhookSecret": "", "PlatformSubaccountCode": "" }` so the config shape is documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [ ] 2. Add `PaystackSubaccountCode` to the `User` model and run EF migration
  - Add `public string? PaystackSubaccountCode { get; set; }` to the `User` entity in `GiftTogether/Models/`
  - Run `dotnet ef migrations add AddPaystackSubaccountCode` and verify the generated migration file is correct
  - _Requirements: 2.2, 13.4_

- [ ] 3. Create the `PendingPayment` entity and EF migration
  - Create `GiftTogether/Models/PendingPayment.cs` with fields: `Id` (int), `Reference` (string, required), `GiftGoalId` (int FK), `ContributionAmount` (decimal), `GrossAmount` (decimal), `ContributorName` (string?), `ContributorMessage` (string?), `Status` (enum: `Pending = 0, Confirmed = 1, Failed = 2`), `CreatedAt` (DateTime UTC)
  - Add `DbSet<PendingPayment> PendingPayments` to `AppDbContext`; add unique index on `Reference` in `OnModelCreating`
  - Add `public int? PendingPaymentId { get; set; }` FK and navigation property to the existing `Contribution` entity
  - Run `dotnet ef migrations add AddPendingPayments` and verify the migration
  - _Requirements: 13.1, 13.2, 13.3_

- [ ] 4. Create `PaystackService`
  - Create `GiftTogether/Services/PaystackService.cs` as a typed `HttpClient` service
  - Constructor accepts `HttpClient` (base address `https://api.paystack.co/`, `Authorization: Bearer {SecretKey}` default header) and `IConfiguration`
  - Implement `CreateSubaccountAsync(string bankCode, string accountNumber, string businessName)` → POST `/subaccount` with `{ settlement_bank, account_number, business_name, percentage_charge: 0 }`; return the `subaccount_code` string from the response
  - Implement `UpdateSubaccountAsync(string subaccountCode, string bankCode, string accountNumber, string businessName)` → PUT `/subaccount/{subaccountCode}`; return the updated `subaccount_code`
  - Implement `GetBanksAsync()` → GET `/bank?currency=ZAR`; return `IEnumerable<PaystackBank>` (record with `Name` and `Code` fields)
  - Implement `InitializeTransactionAsync(InitializeTransactionRequest req)` → POST `/transaction/initialize`; return `InitializeTransactionResponse` (record with `AuthorizationUrl`, `AccessCode`, `Reference`)
  - Implement `VerifyTransactionAsync(string reference)` → GET `/transaction/verify/{reference}`; return `VerifyTransactionResponse` (record with `Status` string and `Amount` decimal in kobo)
  - Throw descriptive exceptions for non-success Paystack responses, including the Paystack error message from the response body
  - _Requirements: 1.5, 2.1, 2.4, 3.1, 5.5, 6.1_

- [ ] 5. Register `PaystackService` and memory cache in `Program.cs`
  - Add `builder.Services.AddHttpClient<PaystackService>(client => { client.BaseAddress = new Uri("https://api.paystack.co/"); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config["Paystack:SecretKey"]); });`
  - Add `builder.Services.AddMemoryCache()` for the bank list cache (if not already registered)
  - _Requirements: 1.5_

- [ ] 6. Create `PaymentController` — register-bank endpoint
  - Create `GiftTogether/Controllers/PaymentController.cs` with `[ApiController]`, `[Route("api/payments")]`
  - Inject `PaystackService`, `AppDbContext`, and `TokenService`
  - Implement `POST /register-bank`: require auth (401 if missing/invalid); validate `bankCode`, `accountNumber`, `accountHolderName` non-empty (400 if any empty); if user has existing `PaystackSubaccountCode` call `UpdateSubaccountAsync`, else call `CreateSubaccountAsync`; persist `subaccountCode` to user record; return 200 with `{ subaccountCode }`; return 502 on Paystack error without persisting
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_

- [ ] 7. Create `PaymentController` — bank list endpoint
  - Implement `GET /banks`: no auth required; inject `IMemoryCache`; check cache key `"paystack:banks"` — if hit return cached list; if miss call `PaystackService.GetBanksAsync()`, cache result for 24 hours, return the list; return 502 on Paystack error
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [ ] 8. Implement the gross-up fee calculation helper
  - Create a static method `GrossUpCalculator.Calculate(decimal contributionAmount)` in `GiftTogether/Services/GrossUpCalculator.cs`
  - Formula: `grossAmount = Math.Ceiling((contributionAmount + 2.00m + 1.00m) / (1 - 0.029m) * 100) / 100`
  - Method returns `(GrossAmount, ContributionAmount, NeoFee: 2.00m)`
  - Write a quick inline sanity check: `Calculate(200.00m).GrossAmount` must equal `208.80m` (per Req 4.2) — add as an xUnit fact test in `GiftTogether.Mobile.Tests` or a new `GiftTogether.Tests` project
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

- [ ] 9. Create `PaymentController` — initialize endpoint
  - Implement `POST /initialize`: no auth required; accept `{ goalId, contributionAmount, contributorName?, contributorMessage? }`; validate goal exists (404 if not) and `contributionAmount > 0` (400 if not); check creator has `PaystackSubaccountCode` (422 if not); call `GrossUpCalculator.Calculate`; generate `reference` = `$"NEO-{goalId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{RandomString(6)}"`; write `PendingPayment` row (Status=Pending); call `PaystackService.InitializeTransactionAsync` with `amount` in kobo, `currency: "ZAR"`, `subaccount`, `bearer: "subaccount"`, `transaction_charge` in kobo, `callback_url: "neo://payment/callback"`; on Paystack error mark PendingPayment as Failed and return 502; on success return 200 with `{ accessCode, authorizationUrl, reference }`
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9_

- [ ] 10. Create `PaymentController` — verify endpoint
  - Implement `POST /verify/{reference}`: no auth required; call `PaystackService.VerifyTransactionAsync(reference)`; if Paystack status is not `"success"` return 402; find `PendingPayment` by reference (404 if not found); if already `Confirmed` return 200 with existing contribution (idempotent); create `Contribution` record with `ContributorName`, `Message`, `Amount = PendingPayment.ContributionAmount`, `GiftGoalId`, `PendingPaymentId`; mark `PendingPayment.Status = Confirmed`; save; return 200 with `{ contribution, totalRaised }`
  - Wrap the confirm-and-save in a database transaction to prevent race conditions between webhook and verify calls
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8, 6.9_

- [ ] 11. Create `PaymentController` — webhook endpoint
  - Implement `POST /webhook`: read raw request body as string (disable body buffering with `[FromBody]` workaround or `Request.EnableBuffering()`); read `x-paystack-signature` header; compute `HMAC-SHA512(body, Paystack:WebhookSecret)`; if signature mismatch return 400; deserialize event body; if `event != "charge.success"` return 200 (acknowledge and ignore); extract `reference` from `data.reference`; run the same confirm logic as task 10 (create Contribution if not already confirmed); return 200 regardless of duplicate state
  - The webhook handler must NOT call `PaystackService.VerifyTransactionAsync` — trust the signed payload directly
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7_

- [ ] 12. Backend checkpoint — build, migrate, smoke test
  - Run `dotnet build` on `GiftTogether/`; fix any compile errors
  - Run `dotnet ef database update` to apply both migrations
  - Run any existing xUnit tests; fix regressions
  - Manually verify (curl or Swagger) that `GET /api/payments/banks` returns a bank list and `POST /api/payments/initialize` with an invalid goalId returns 404

---

### Mobile

- [ ] 13. Add payment DTOs and `PaymentService` to the mobile project
  - In `GiftTogether.Mobile/Services/`, add payment-specific records (or add to `ApiService.cs`):
    - `PaystackBank(string Name, string Code)`
    - `InitializePaymentResponse(string AuthorizationUrl, string AccessCode, string Reference)`
    - `VerifyPaymentResponse(string ContributorName, string Message, decimal Amount, decimal TotalRaised)`
  - Implement `GetBanksAsync()` → `GET /api/payments/banks`; returns `IEnumerable<PaystackBank>`
  - Implement `RegisterBankAsync(string bankCode, string accountNumber, string accountHolderName)` → `POST /api/payments/register-bank` (authenticated); throws on error
  - Implement `InitializePaymentAsync(int goalId, decimal amount, string? name, string? message)` → `POST /api/payments/initialize`; returns `InitializePaymentResponse`
  - Implement `VerifyPaymentAsync(string reference)` → `POST /api/payments/verify/{reference}`; returns `VerifyPaymentResponse`
  - _Requirements: 8.1, 8.9, 9.4, 10.3_

- [ ] 14. Create `BankSetupPage.razor`
  - Create `GiftTogether.Mobile/Components/Pages/BankSetupPage.razor` at route `/settings/bank`
  - Use `<TopBar ShowBack="true" OnBack="GoBack" Title="Bank account" />`
  - Bank name: `<select>` populated from `GetBanksAsync()` showing bank names; loading indicator in dropdown while fetching; store selected `BankCode`
  - Account number: `<input type="tel">` (numeric); inline validation error if empty on submit
  - Account holder name: `<input type="text">`; inline validation error if empty
  - "Save bank account" button: disabled while submitting; shows spinner while in progress; on success show Toast "Bank account saved"
  - On API error: display error message inline, do not navigate away
  - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9_

- [ ] 15. Wire "Contribute" button in `GuestRegistry.razor` to `PaymentSummaryPage`
  - The existing contribution bottom sheet in `GuestRegistry.razor` collects `contributorName`, `amount`, `message`
  - After the user fills in the form and taps "Pay", instead of calling the old contributions API, navigate to `/payment/summary?goalId={id}&amount={amount}&name={name}&message={message}` (or pass via a state service to avoid query-string encoding issues)
  - Keep the existing bottom sheet UI and validation unchanged; only replace the submit action
  - _Requirements: 9.1_

- [ ] 16. Create `PaymentSummaryPage.razor`
  - Create `GiftTogether.Mobile/Components/Pages/PaymentSummaryPage.razor` at route `/payment/summary`
  - Accept parameters: `GoalId` (int), `ContributionAmount` (decimal), `ContributorName` (string?), `Message` (string?)
  - Display: goal name (loaded from existing API), contribution amount, service fee (R2.00, labelled "Service fee"), total = `ContributionAmount + 2.00`
  - "Pay R{total:F2}" primary button; disabled + spinner while initializing
  - On tap: call `InitializePaymentAsync`; on success open `AuthorizationUrl` via `Launcher.OpenAsync(new Uri(authorizationUrl))`; store `reference` in a singleton/static field or `ISessionStorage` for the deep link handler to retrieve
  - On API error: display error inline and re-enable button
  - Trust note: "Payments are secured by Paystack" below the button
  - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7_

- [ ] 17. Register the `neo://payment/callback` deep link URI scheme
  - **Android**: in `GiftTogether.Mobile/Platforms/Android/AndroidManifest.xml`, add an `<intent-filter>` with `<data android:scheme="neo" android:host="payment" android:pathPrefix="/callback" />` to `MainActivity`
  - **iOS**: in `GiftTogether.Mobile/Platforms/iOS/Info.plist`, add a `CFBundleURLTypes` entry with scheme `neo`
  - In `GiftTogether.Mobile/App.xaml.cs` (or `MauiProgram.cs`), override `OnAppLinkRequestReceived(Uri uri)` (MAUI's built-in deep link entry point); extract `reference` from `uri.Query`; if present, navigate to `/payment/processing?reference={reference}`; if absent, navigate to `/` and show error "Payment reference missing"
  - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_

- [ ] 18. Create `PaymentProcessingPage.razor`
  - Create `GiftTogether.Mobile/Components/Pages/PaymentProcessingPage.razor` at route `/payment/processing`
  - Accept `reference` query parameter
  - Render animated checklist: "Secure payment" → "Verifying details" → "Almost there…" appearing with 600ms delay between each item (CSS `animation-delay` or `Task.Delay`)
  - In `OnInitializedAsync`: `await Task.Delay(1500)` (minimum display time); simultaneously call `VerifyPaymentAsync(reference)` in the background; wait for whichever finishes last (use `Task.WhenAll` with the delay)
  - On success: navigate to `/payment/success?totalRaised={totalRaised}&goalName={goalName}`
  - On failure: replace checklist with error state showing failure reason and "Try again" button that re-calls verify
  - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6_

- [ ] 19. Create `PaymentSuccessPage.razor`
  - Create `GiftTogether.Mobile/Components/Pages/PaymentSuccessPage.razor` at route `/payment/success`
  - Accept `TotalRaised` (decimal) and `GoalName` (string) query parameters
  - Display: "You're part of this! 💛" heading; `<ProgressBar>` showing updated progress (requires knowing `TargetAmount` — load from registry API or pass as query param); "Share this collection" button opening native share sheet; "Back to collection" button navigating to `/r/{registrySlug}` and triggering a goal list refresh
  - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_

- [ ] 20. Final checkpoint — end-to-end smoke test
  - Run `dotnet build` on both projects; fix any compile errors
  - Run all existing tests; fix regressions
  - Manually trace the full flow: create a test goal → open GuestRegistry → tap Contribute → fill in amount → reach PaymentSummaryPage → verify fee breakdown displays correctly → confirm the Paystack URL opens on tap

---

## Notes

- Tasks 1–12 (backend) must be completed before tasks 13–19 (mobile)
- Task 8 (gross-up calculator) should be unit tested before task 9 uses it in the controller
- The webhook endpoint (task 11) and verify endpoint (task 10) share confirmation logic — extract it into a private `ConfirmPaymentAsync(string reference)` method on the controller or a separate `PaymentConfirmationService` to avoid duplication
- The `reference` passed from `PaymentSummaryPage` to the deep link handler needs a reliable handoff mechanism — a registered singleton `IPaymentSession` service (holding the last reference in memory) is simpler than query strings across an external browser round-trip
- Paystack test keys are available in the Paystack dashboard under Settings → API Keys; use test mode for all development and staging work
