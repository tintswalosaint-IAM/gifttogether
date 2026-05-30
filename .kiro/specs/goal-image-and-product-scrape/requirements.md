# Requirements Document

## Introduction

This feature adds two capabilities to the "Add gift goals" form, which appears in Step 2 of the `CreateRegistry` wizard and in the "Add a gift goal" bottom sheet in `RegistryDetail`:

1. **Goal image upload** — the user can pick an image from their device gallery or camera and upload it as the gift goal's cover image. The backend already exposes `POST /api/registries/{id}/goals/{goalId}/upload-image` (multipart/form-data, field name `image`). Because the goal must exist before an image can be uploaded, the upload happens immediately after the goal is created.

2. **Product link auto-fill** — when the user enters a product URL in the "Product link" field, the app calls a new backend scraping endpoint (`POST /api/scrape`) that fetches the product page and returns `{ title, price, imageUrl }`. The app then non-destructively populates the gift name, target amount, and goal image preview from the scraped data.

The target e-commerce sites are South African retailers: Takealot, Woolworths, Checkers, Game, and Makro.

---

## Glossary

- **Goal_Form**: The UI form used to enter gift goal details, present in both `CreateRegistry` (Step 2) and the `RegistryDetail` add-goal bottom sheet.
- **Image_Picker**: The MAUI `MediaPicker` abstraction that lets the user select a photo from the device gallery or capture one with the camera.
- **Image_Preview**: A thumbnail displayed inside the Goal_Form showing the currently selected or scraped goal image before the goal is saved.
- **Upload_Service**: The client-side service method that sends a picked image file to `POST /api/registries/{id}/goals/{goalId}/upload-image`.
- **Scrape_Endpoint**: The new backend endpoint `POST /api/scrape` that accepts a URL and returns scraped product metadata.
- **Scrape_Result**: The JSON object returned by the Scrape_Endpoint: `{ title: string | null, price: decimal | null, imageUrl: string | null }`.
- **Auto_Fill**: The non-destructive action of populating empty Goal_Form fields with values from a Scrape_Result.
- **Product_URL**: A URL entered by the user in the "Product link" field, pointing to a product page on a supported retailer site.
- **Supported_Retailers**: Takealot, Woolworths, Checkers, Game, and Makro.

---

## Requirements

### Requirement 1: Goal Image Picker

**User Story:** As a registry owner, I want to pick an image from my device for a gift goal, so that contributors can visually identify what I am wishing for.

#### Acceptance Criteria

1. THE Goal_Form SHALL display an image picker control that allows the user to select a photo from the device gallery or capture a new photo with the camera.
2. WHEN the user selects or captures a photo, THE Image_Preview SHALL display a thumbnail of the selected image within the Goal_Form before the goal is saved.
3. WHEN the user selects or captures a photo, THE Goal_Form SHALL store the selected file in memory so it can be uploaded after the goal is created.
4. WHEN the user has selected an image and then selects a different image, THE Image_Preview SHALL update to show the most recently selected image.
5. WHEN the user has selected an image and the goal is successfully created, THE Upload_Service SHALL upload the selected image to `POST /api/registries/{registryId}/goals/{goalId}/upload-image` using multipart/form-data with the field name `image`.
6. IF the image upload fails after goal creation, THEN THE Goal_Form SHALL display an error message indicating that the goal was saved but the image could not be uploaded, and SHALL retain the goal in the list.
7. THE Goal_Form SHALL accept image files of type JPEG, PNG, WebP, or GIF only.
8. THE Goal_Form SHALL reject image files larger than 5 MB and SHALL display an error message stating the size limit.

---

### Requirement 2: Goal Image Preview from Scraped URL

**User Story:** As a registry owner, I want the goal image to be pre-filled from a scraped product page, so that I do not have to manually find and upload a product photo.

#### Acceptance Criteria

1. WHEN a Scrape_Result contains a non-null `imageUrl`, THE Goal_Form SHALL display the scraped image URL as the Image_Preview.
2. WHEN the user has already selected a local image via the Image_Picker and a Scrape_Result is then received, THE Goal_Form SHALL NOT replace the locally selected image with the scraped image URL.
3. WHEN the user clears the Image_Preview after it was populated by a Scrape_Result, THE Goal_Form SHALL remove the image association from the goal.

---

### Requirement 3: Product Link Auto-Fill

**User Story:** As a registry owner, I want to paste a product URL and have the gift name, price, and image filled in automatically, so that I can add goals quickly without manual data entry.

#### Acceptance Criteria

1. WHEN the user finishes entering text in the "Product link" field (on blur or after a debounce of 800 ms), THE Goal_Form SHALL call the Scrape_Endpoint with the entered URL if the URL is non-empty and syntactically valid.
2. WHILE a scrape request is in progress, THE Goal_Form SHALL display a loading indicator adjacent to the "Product link" field.
3. WHEN a Scrape_Result is received and the gift name field is empty, THE Goal_Form SHALL populate the gift name field with `Scrape_Result.title`.
4. WHEN a Scrape_Result is received and the target amount field is zero or empty, THE Goal_Form SHALL populate the target amount field with `Scrape_Result.price`.
5. WHEN a Scrape_Result is received and the user has not already selected a local image, THE Goal_Form SHALL set the Image_Preview to `Scrape_Result.imageUrl` if it is non-null.
6. WHEN a Scrape_Result is received and a field already contains a user-entered value, THE Goal_Form SHALL NOT overwrite that field.
7. IF the Scrape_Endpoint returns an error or the URL is unreachable, THEN THE Goal_Form SHALL display a non-blocking inline notice (e.g. "Could not fetch product details") and SHALL leave all fields unchanged.
8. IF the entered URL does not match a Supported_Retailer domain, THEN THE Scrape_Endpoint SHALL return a 422 response with an error message identifying the unsupported domain.
9. THE Goal_Form SHALL NOT call the Scrape_Endpoint more than once per distinct URL value (debounced, not on every keystroke).

---

### Requirement 4: Backend Scrape Endpoint

**User Story:** As a mobile client, I want a backend endpoint to scrape product metadata, so that the app can auto-fill goal details without making cross-origin requests from the WebView.

#### Acceptance Criteria

1. THE Scrape_Endpoint SHALL accept `POST /api/scrape` with a JSON body `{ "url": "<Product_URL>" }`.
2. WHEN a valid Product_URL for a Supported_Retailer is provided, THE Scrape_Endpoint SHALL return HTTP 200 with a Scrape_Result containing at least one non-null field among `title`, `price`, and `imageUrl`.
3. THE Scrape_Endpoint SHALL extract the product title from the page's `<title>` tag or a retailer-specific CSS selector, whichever yields a cleaner result.
4. THE Scrape_Endpoint SHALL extract the product price as a decimal value in South African Rand (ZAR), stripping currency symbols and thousand separators.
5. THE Scrape_Endpoint SHALL extract the primary product image URL from the page's Open Graph `og:image` meta tag or a retailer-specific image selector.
6. IF the provided URL is not syntactically valid, THEN THE Scrape_Endpoint SHALL return HTTP 400 with `{ "error": "Invalid URL." }`.
7. IF the provided URL does not match a Supported_Retailer domain, THEN THE Scrape_Endpoint SHALL return HTTP 422 with `{ "error": "Unsupported retailer. Supported sites: Takealot, Woolworths, Checkers, Game, Makro." }`.
8. IF the HTTP request to the product page fails or times out (timeout: 10 seconds), THEN THE Scrape_Endpoint SHALL return HTTP 502 with `{ "error": "Could not reach the product page." }`.
9. IF the page is reachable but no product data can be extracted, THEN THE Scrape_Endpoint SHALL return HTTP 200 with a Scrape_Result where all fields are null.
10. THE Scrape_Endpoint SHALL require a valid Bearer token (authenticated users only) to prevent abuse.
11. THE Scrape_Endpoint SHALL set a `User-Agent` header on outbound HTTP requests to avoid being blocked by retailer bot-detection.

---

### Requirement 5: Image Upload API Client

**User Story:** As a mobile client, I want an API service method for uploading goal images, so that the Goal_Form can call it after a goal is created.

#### Acceptance Criteria

1. THE Upload_Service SHALL expose a method `UploadGoalImageAsync(registryId, goalId, fileStream, fileName, contentType)` that sends a multipart/form-data POST to `/api/registries/{registryId}/goals/{goalId}/upload-image`.
2. WHEN the upload succeeds, THE Upload_Service SHALL return the `url` string from the response body.
3. IF the server returns a non-success status code, THEN THE Upload_Service SHALL throw an exception with the server's error message.
4. THE Upload_Service SHALL include the authenticated Bearer token in the `Authorization` header of the upload request.

---

### Requirement 6: Scrape API Client

**User Story:** As a mobile client, I want an API service method for calling the scrape endpoint, so that the Goal_Form can trigger auto-fill without duplicating HTTP logic.

#### Acceptance Criteria

1. THE ApiService SHALL expose a method `ScrapeProductAsync(url)` that sends `POST /api/scrape` with `{ "url": url }` and returns a `ScrapeResult` record.
2. WHEN the server returns HTTP 200, THE ApiService SHALL deserialize the response into a `ScrapeResult` with nullable `Title`, `Price`, and `ImageUrl` fields.
3. IF the server returns HTTP 422 (unsupported retailer) or HTTP 400 (invalid URL), THEN THE ApiService SHALL throw an exception with the server's error message so the Goal_Form can display it.
4. IF the server returns HTTP 502 (scrape failure), THEN THE ApiService SHALL throw an exception with the server's error message.
