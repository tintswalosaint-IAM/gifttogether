# Design Document — Paystack Payment Integration

## Overview

This document describes the technical design for integrating Paystack as the payment processor for the NEO gift registry platform. The integration enables contributors to make real card payments towards gift goals, with funds automatically split between the creator's bank account (via a Paystack subaccount) and the NEO platform account.

The feature spans two projects:

- **GiftTogether** (ASP.NET Core .NET 8 backend) — new `PaymentController`, `PaystackService`, `PendingPayment` entity, and EF Core migration.
- **GiftTogether.Mobile** (MAUI Blazor Hybrid) — new payment DTOs, `PaymentService`, and four new Razor pages covering the full contributor payment flow.

### Key Design Decisions

1. **Flat-amount split, not percentage**: Paystack's split is configured with `bearer: "subaccount"` and `transaction_charge` set to the contribution amount in kobo. This means the creator's subaccount is charged the contribution amount, and the remainder (after Paystack fees) flows to the NEO platform account. The gross-up formula ensures the creator always receives the exact intended amount.

2. **PendingPayment as correlation record**: A `PendingPayment` row is written to the database before the Paystack API call. This decouples the payment intent from Paystack's confirmation, allowing both the verify endpoint and the webhook handler to idempotently confirm the same payment without creating duplicate `Contribution` records.

3. **Webhook as safety net**: The verify endpoint is the primary confirmation path (called by the mobile app after returning from checkout). The webhook handler is a secondary path that catches cases where the user closes the app before verification completes. Both paths share the same confirmation logic.

4. **Deep link callback**: The mobile app registers the `neo://payment/callback` URI scheme. Paystack's `callback_url` points to this scheme. When Paystack redirects after checkout, the OS invokes the app, which extracts the `reference` query parameter and navigates to the processing screen.

5. **No JWT required for payment endpoints**: `POST /initialize`, `POST /verify/{reference}`, and `GET /banks` are unauthenticated so guest contributors can pay without an account. `POST /register-bank` requires authentication because it modifies the creator's user record.


## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  GiftTogether.Mobile (MAUI Blazor Hybrid)                           │
│                                                                     │
│  GuestRegistry.razor                                                │
│       │ contributor taps "Pay"                                      │
│       ▼                                                             │
│  PaymentSummaryPage.razor ──► POST /api/payments/initialize         │
│       │ receives authorization_url                                  │
│       ▼                                                             │
│  Browser / WebView (Paystack Hosted Checkout)                       │
│       │ Paystack redirects to neo://payment/callback?reference=...  │
│       ▼                                                             │
│  App.xaml.cs (deep link handler) ──► NavigationService             │
│       │                                                             │
│       ▼                                                             │
│  PaymentProcessingPage.razor ──► POST /api/payments/verify/{ref}   │
│       │ success                                                     │
│       ▼                                                             │
│  PaymentSuccessPage.razor                                           │
└─────────────────────────────────────────────────────────────────────┘
                          │ HTTP
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  GiftTogether (ASP.NET Core .NET 8)                                 │
│                                                                     │
│  PaymentController (api/payments)                                   │
│    POST /register-bank  ──► PaystackService.CreateSubaccountAsync   │
│    GET  /banks          ──► PaystackService.GetBanksAsync           │
│    POST /initialize     ──► PaystackService.InitializeTransactionAsync│
│    POST /verify/{ref}   ──► PaystackService.VerifyTransactionAsync  │
│    POST /webhook        ──► HMAC-SHA512 verify ──► confirm logic    │
│                                                                     │
│  PaystackService (typed HttpClient → api.paystack.co)               │
│  AppDbContext (SQLite via EF Core)                                  │
│    ├── Users (+ PaystackSubaccountCode column)                      │
│    ├── PendingPayments (new table)                                  │
│    └── Contributions (+ PendingPaymentId FK)                        │
└─────────────────────────────────────────────────────────────────────┘
                          │ HTTPS
                          ▼
                  api.paystack.co
```

### Request Flow: Payment Initialization

```
Mobile                    Backend                   Paystack
  │                          │                          │
  │ POST /initialize          │                          │
  │ {goalId, amount, name}   │                          │
  │─────────────────────────►│                          │
  │                          │ Write PendingPayment     │
  │                          │ (Status=Pending)         │
  │                          │                          │
  │                          │ POST /transaction/initialize
  │                          │─────────────────────────►│
  │                          │◄─────────────────────────│
  │                          │ {access_code,            │
  │                          │  authorization_url,      │
  │                          │  reference}              │
  │◄─────────────────────────│                          │
  │ {access_code,            │                          │
  │  authorization_url,      │                          │
  │  reference}              │                          │
```

### Request Flow: Payment Verification

```
Mobile                    Backend                   Paystack
  │                          │                          │
  │ POST /verify/{reference} │                          │
  │─────────────────────────►│                          │
  │                          │ GET /transaction/verify/{ref}
  │                          │─────────────────────────►│
  │                          │◄─────────────────────────│
  │                          │ {status: "success", ...} │
  │                          │                          │
  │                          │ Find PendingPayment      │
  │                          │ Create Contribution      │
  │                          │ Mark PendingPayment      │
  │                          │ as Confirmed             │
  │◄─────────────────────────│                          │
  │ {contribution, totalRaised}                         │
```

## Architecture

The system has three layers: the MAUI mobile app, the ASP.NET Core backend, and the Paystack API.

### Component Interaction

The mobile app calls the backend payment endpoints. The backend calls Paystack. Paystack sends webhooks back to the backend.

**Payment initialization flow:**
1. Mobile calls POST /api/payments/initialize`n2. Backend writes PendingPayment record (Status=Pending)
3. Backend calls Paystack POST /transaction/initialize`n4. Backend returns uthorization_url and eference to mobile
5. Mobile opens uthorization_url in browser

**Payment verification flow:**
1. Paystack redirects to 
eo://payment/callback?reference={ref}`n2. MAUI deep link handler navigates to PaymentProcessingPage
3. Mobile calls POST /api/payments/verify/{reference}`n4. Backend calls Paystack GET /transaction/verify/{reference}`n5. Backend creates Contribution, marks PendingPayment as Confirmed
6. Mobile navigates to PaymentSuccessPage

**Webhook flow (safety net):**
1. Paystack sends POST /api/payments/webhook with charge.success event
2. Backend verifies HMAC-SHA512 signature
3. Backend confirms PendingPayment if not already confirmed (idempotent)

## Components and Interfaces

### Backend: PaystackService

A typed HttpClient service registered in DI. All Paystack API communication goes through this class.

**Registration in Program.cs:**
```csharp
builder.Services.AddHttpClient<PaystackService>(client =>
{
    client.BaseAddress = new Uri("https://api.paystack.co/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["Paystack:SecretKey"]);
});
```
