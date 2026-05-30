# Implementation Plan: Goal Image Upload & Product Link Scrape

## Overview

Implement two capabilities for the "Add gift goal" form in both `CreateRegistry` (Step 2) and the `RegistryDetail` add-goal bottom sheet:

1. A goal image picker that lets users select a photo from their device and uploads it after goal creation.
2. A product URL auto-fill that calls a new `POST /api/scrape` backend endpoint, extracts title, price, and image from supported South African retailer pages using `HtmlAgilityPack`, and non-destructively populates the form.

The backend is ASP.NET Core (.NET 8) and the mobile client is MAUI Blazor Hybrid (C#/Razor).

---

## Tasks

- [ ] 1. Add HtmlAgilityPack dependency to the backend project
  - Add `<PackageReference Include="HtmlAgilityPack" Version="1.11.67" />` to `GiftTogether/GiftTogether.csproj`
  - Verify the project still builds after the package is added
  - _Requirements: 4.1, 4.3, 4.4, 4.5_

- [x] 2. Create backend DTOs for the scrape endpoint
  - Create `GiftTogether/DTOs/ScrapeDtos.cs` with `ScrapeRequest(string Url)` and `ScrapeResultDto(string? Title, decimal? Price, string? ImageUrl)` records
  - Follow the namespace and record style already used in `GiftTogether/DTOs/RegistryDtos.cs`
  - _Requirements: 4.1, 4.2_

- [x] 3. Create `StrategyBase` with shared HTML-parsing helpers
  - Create `GiftTogether/Services/Scraping/StrategyBase.cs` implementing `IRetailerStrategy` (abstract)
  - Add `IRetailerStrategy` interface in the same file or a sibling file `IRetailerStrategy.cs`
  - Implement `OgImage`, `OgTitle`, and `PageTitle` static helpers using `HtmlDocument.DocumentNode.SelectSingleNode` XPath
  - Implement `ParseZarPrice(string? raw)` using the regex pipeline described in the design: strip `R` and whitespace, remove thousand separators, normalise decimal comma, then `decimal.TryParse` with `InvariantCulture`
  - _Requirements: 4.3, 4.4, 4.5_

  - [ ]* 3.1 Write property test for `ParseZarPrice` (Property 1)
    - **Property 1: ZAR price parsing is total and correct**
    - For any string representing a ZAR price (with/without `R`, with space/comma thousand separators, `.` or `,` decimal), `ParseZarPrice` SHALL return the correct decimal; for strings with no numeric value it SHALL return `null`
    - Use FsCheck generators to produce valid ZAR price strings and verify round-trip numeric equality
    - **Validates: Requirements 4.4**

  - [ ]* 3.2 Write unit tests for `ParseZarPrice` specific examples
    - `"R 1 299.99"` → `1299.99m`, `"R1,299.99"` → `1299.99m`, `"1299"` → `1299m`, `""` → `null`, `"abc"` → `null`
    - **Validates: Requirements 4.4**

- [x] 4. Implement the five retailer strategy classes
  - Create `GiftTogether/Services/Scraping/TakealotStrategy.cs` — `Matches` on `takealot.com`; title from `h1.pdp-title` → `<title>` strip suffix; price from `span.currency.plus` → `meta[property="product:price:amount"]`; image from `img.pdp-image` first `src` → `OgImage`
  - Create `GiftTogether/Services/Scraping/WoolworthsStrategy.cs` — `Matches` on `woolworths.co.za`; title from `h1.product-name` → `<title>` strip suffix; price from `strong.price` → `meta[property="product:price:amount"]`; image from `OgImage` → `img.product-image` first `src`
  - Create `GiftTogether/Services/Scraping/CheckersStrategy.cs` — `Matches` on `checkers.co.za`; title from `h1.pdp__name` → `<title>`; price from `div.pdp__price` text → `meta[property="product:price:amount"]`; image from `OgImage` → `img.pdp__image` first `src`
  - Create `GiftTogether/Services/Scraping/GameStrategy.cs` — `Matches` on `game.co.za`; title from `h1.product-name` → `<title>` strip suffix; price from `span.price-box__price` → `meta[property="product:price:amount"]`; image from `OgImage` → `img.product-image-photo` first `src`
  - Create `GiftTogether/Services/Scraping/MakroStrategy.cs` — `Matches` on `makro.co.za`; title from `h1.product-name` → `<title>` strip suffix; price from `span.price-box__price` → `meta[property="product:price:amount"]`; image from `OgImage` → `img.product-image-photo` first `src`
  - Each strategy's `Matches` method must handle both `www.` and bare domain variants (case-insensitive)
  - _Requirements: 4.2, 4.3, 4.4, 4.5_

  - [ ]* 4.1 Write unit tests for each retailer strategy using minimal HTML fixtures
    - For each of the five strategies: provide a minimal HTML string containing the primary selector values; assert that `Extract` returns the expected title, price, and imageUrl
    - Also test the OG-tag fallback path: provide HTML with only `og:image` and `og:title` meta tags; assert both are extracted
    - **Validates: Requirements 4.3, 4.4, 4.5**

- [x] 5. Create `ScraperService`
  - Create `GiftTogether/Services/ScraperService.cs`
  - Declare `public static readonly IReadOnlySet<string> AllowedHosts` containing all ten domain variants (with and without `www.`) using `StringComparer.OrdinalIgnoreCase`
  - Inject `HttpClient` (configured with 10 s timeout and a browser-like `User-Agent` header)
  - Instantiate the five strategy classes in a private `IReadOnlyList<IRetailerStrategy> _strategies`
  - Implement `ScrapeAsync(string url)`: fetch the page with `HttpClient.GetStringAsync`, load into `HtmlDocument`, iterate `_strategies` to find the matching one, call `Extract`, return `ScrapeResultDto`
  - _Requirements: 4.1, 4.2, 4.8, 4.11_

  - [ ]* 5.1 Write property test for `AllowedHosts` domain check (Property 2)
    - **Property 2: Allowlist domain check is exhaustive**
    - For any absolute URI string, `AllowedHosts.Contains(uri.Host)` SHALL return `true` iff the host is one of the five supported retailer domains (with or without `www.`), and `false` for all others
    - Use FsCheck to generate arbitrary hostnames and verify against the expected set
    - **Validates: Requirements 3.8, 4.7**

- [x] 6. Create `ScrapeController`
  - Create `GiftTogether/Controllers/ScrapeController.cs` with `[ApiController]`, `[Route("api/scrape")]`
  - Inject `ScraperService` and `TokenService`; validate the Bearer token (return 401 if missing/invalid)
  - `[HttpPost]` action: validate `req.Url` is non-empty and a valid absolute URI (400 on failure); check `ScraperService.AllowedHosts` (422 on failure); call `ScraperService.ScrapeAsync` wrapped in `try/catch` for `HttpRequestException` and `TaskCanceledException` (502 on failure); return 200 with `ScrapeResultDto`
  - All error responses use `{ "error": "..." }` shape consistent with existing controllers
  - _Requirements: 4.1, 4.6, 4.7, 4.8, 4.9, 4.10_

  - [ ]* 6.1 Write unit tests for `ScrapeController`
    - 400 on missing/empty URL, 400 on non-URI string, 422 on unsupported domain, 502 on `HttpRequestException`, 502 on `TaskCanceledException`, 200 with all-null fields when page is reachable but empty, 401 on missing token
    - Mock `ScraperService` using a test double
    - **Validates: Requirements 4.6, 4.7, 4.8, 4.9, 4.10**

- [x] 7. Register `ScraperService` and its `HttpClient` in `Program.cs`
  - In `GiftTogether/Program.cs`, add `builder.Services.AddHttpClient<ScraperService>()` with a `ConfigureHttpClient` delegate that sets `Timeout = TimeSpan.FromSeconds(10)` and adds a `User-Agent` header (e.g. `"Mozilla/5.0 (compatible; GiftTogether/1.0)"`)
  - Ensure `ScraperService` is registered as a scoped or transient service (whichever matches `AddHttpClient` default)
  - _Requirements: 4.8, 4.11_

- [ ] 8. Checkpoint — verify backend builds and controller is reachable
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Add `ScrapeResult` record and new methods to `ApiService`
  - In `GiftTogether.Mobile/Services/ApiService.cs`, add `public record ScrapeResult(string? Title, decimal? Price, string? ImageUrl);` alongside the other DTOs
  - Implement `ScrapeProductAsync(string url)`: call `SetAuthHeader()`, `PostAsJsonAsync("/api/scrape", new { url }, JsonOpts)`, then `ReadOrThrow<ScrapeResult>(res)`
  - Implement `UploadGoalImageAsync(int registryId, int goalId, Stream stream, string fileName, string contentType)`: call `SetAuthHeader()`, build `MultipartFormDataContent` with a `StreamContent` part named `"image"`, POST to `/api/registries/{registryId}/goals/{goalId}/upload-image`, throw on non-success, deserialise and return the `"url"` string from the JSON response
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 6.4_

  - [ ]* 9.1 Write property test for `ScrapeResult` JSON round-trip (Property 7)
    - **Property 7: Scrape API client round-trips ScrapeResult correctly**
    - For any combination of nullable `Title`, `Price`, and `ImageUrl` values serialised as a JSON response body, `ScrapeProductAsync` SHALL deserialise into a `ScrapeResult` whose fields exactly match the originals (including `null`)
    - Mock `HttpMessageHandler` to return the generated JSON; assert field equality
    - **Validates: Requirements 6.1, 6.2**

  - [ ]* 9.2 Write property test for error propagation (Property 8)
    - **Property 8: Scrape API client propagates error messages**
    - For any HTTP response with status 400, 422, or 502 and a JSON body `{ "error": "<message>" }`, `ScrapeProductAsync` SHALL throw an `Exception` whose `Message` equals the error field value
    - Use FsCheck to generate arbitrary error message strings and status codes from {400, 422, 502}
    - **Validates: Requirements 6.3, 6.4**

  - [ ]* 9.3 Write unit tests for `UploadGoalImageAsync`
    - Mock `HttpMessageHandler`; verify the request URL matches `/api/registries/{registryId}/goals/{goalId}/upload-image`, the multipart part is named `"image"`, the return value equals the `url` field in the response, and an exception is thrown on non-success status
    - **Validates: Requirements 5.1, 5.2, 5.3, 5.4**

  - [ ]* 9.4 Write property test for upload URL template (Property 6)
    - **Property 6: Upload request targets the correct endpoint**
    - For any `(registryId, goalId)` pair, `UploadGoalImageAsync` SHALL send a POST to exactly `/api/registries/{registryId}/goals/{goalId}/upload-image` with a multipart body containing a part named `image`
    - Use FsCheck to generate arbitrary positive integer pairs and verify the captured request URL
    - **Validates: Requirements 5.1**

- [x] 10. Update `CreateRegistry.razor` Step 2 with image picker, preview, scrape debounce, and auto-fill
  - Add image picker state fields: `_pickedImageStream`, `_pickedImageFileName`, `_pickedImageContentType`, `_imagePreviewSrc`
  - Add scrape state fields: `_scrapeCts`, `_scrapeLoading`, `_scrapeError`
  - Add the image picker UI block (preview thumbnail with clear button, or pick button) above the goal name field in Step 2
  - Replace the static `@bind="_goalLink"` input with an `@oninput="OnProductLinkInput"` handler and add the loading spinner and error message elements below the field
  - Implement `PickImageAsync()`: call `MediaPicker.Default.PickPhotoAsync`, validate MIME type against `{ image/jpeg, image/png, image/webp, image/gif }` (case-insensitive), validate size ≤ 5 MB, build a base64 data URL for `_imagePreviewSrc`, store stream/filename/contentType
  - Implement `ClearImage()`: dispose and null all image state fields
  - Implement `OnProductLinkInput(ChangeEventArgs e)`: cancel previous `_scrapeCts`, create new one, validate URL, `await Task.Delay(800, cts.Token)`, set `_scrapeLoading`, call `Api.ScrapeProductAsync`, call `ApplyAutoFill`, handle `TaskCanceledException` silently and other exceptions via `_scrapeError`
  - Implement `ApplyAutoFill(ScrapeResult result)`: non-destructively set `_goalName` if empty, `_goalAmount` if zero, `_imagePreviewSrc` if `_pickedImageStream == null` and result has an image URL
  - Update `AddGoalAsync()`: after `Api.AddGoalAsync` succeeds, if `_pickedImageStream != null` call `Api.UploadGoalImageAsync`; on upload failure show non-fatal error in `_goalNameError`; always call `ClearImage()` in a `finally` block; reset `_goalLink` and `_scrapeError` in `ResetGoalForm`
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.9_

  - [ ]* 10.1 Write property test for non-destructive auto-fill (Property 3)
    - **Property 3: Non-destructive auto-fill preserves existing values**
    - For any combination of pre-filled `(name, amount, imagePreviewSrc)` and any `ScrapeResult`, after `ApplyAutoFill`: a non-empty/non-zero field SHALL retain its original value; an empty/zero field SHALL be set to the `ScrapeResult` value if non-null
    - Extract `ApplyAutoFill` logic into a testable static/instance method; use FsCheck to generate arbitrary field combinations
    - **Validates: Requirements 2.2, 3.3, 3.4, 3.5, 3.6**

  - [ ]* 10.2 Write property test for image type validation (Property 4)
    - **Property 4: Image type validation is correct**
    - For any MIME type string, the validation SHALL accept iff it is one of `image/jpeg`, `image/png`, `image/webp`, `image/gif` (case-insensitive), and reject all others
    - Use FsCheck to generate arbitrary strings and verify acceptance matches the allowed set
    - **Validates: Requirements 1.7**

  - [ ]* 10.3 Write property test for image size validation (Property 5)
    - **Property 5: Image size validation is correct**
    - For any non-negative integer `size`, the validation SHALL accept iff `size <= 5 * 1024 * 1024` (5 242 880 bytes)
    - Use FsCheck to generate arbitrary non-negative integers and verify the boundary
    - **Validates: Requirements 1.8**

- [x] 11. Update `RegistryDetail.razor` add-goal bottom sheet with the same image picker and scrape features
  - Add the same image picker state fields and scrape state fields as in task 10 (scoped to the add-goal bottom sheet)
  - Add the image picker UI block inside the `<BottomSheet>` for "Add a gift goal", above the gift name field
  - Replace the static product link `<input>` with the `@oninput="OnProductLinkInput"` version including spinner and error message
  - Implement `PickImageAsync`, `ClearImage`, `OnProductLinkInput`, and `ApplyAutoFill` with identical logic to task 10
  - Update `AddGoalAsync` in `RegistryDetail.razor` to upload the image after goal creation and call `ClearImage()` in `finally`, matching the pattern from task 10
  - Reset image and scrape state in `ShowAddGoal()` (the method that opens the bottom sheet) and in `CloseModals()`
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.9_

  - [ ]* 11.1 Write unit tests for `ApplyAutoFill` specific examples (bUnit or plain xUnit)
    - Test each combination: name pre-filled / empty, amount pre-filled / zero, image picked / not picked — verify the correct fields are set or preserved
    - **Validates: Requirements 2.2, 3.3, 3.4, 3.5, 3.6**

- [ ] 12. Final checkpoint — ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

---

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation at natural integration boundaries
- Property tests validate universal correctness properties using FsCheck (NuGet: `FsCheck.Xunit`)
- Unit tests validate specific examples and edge cases
- The design document contains full implementation sketches for all methods — refer to it during implementation
