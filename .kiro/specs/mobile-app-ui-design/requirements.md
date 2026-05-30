# Requirements Document

## Introduction

GiftTogether is a mobile gift registry platform built with .NET MAUI Blazor Hybrid. Users create and manage gift registries (wedding, baby shower, birthday, etc.), add gift goals with images and target amounts, and share registries with guests who can contribute. The app currently has functional pages but lacks a premium visual identity.

This feature defines the UI/UX design system and visual redesign for the GiftTogether mobile app. The benchmark is world-class consumer apps — Airbnb, Apple, Stripe, Linear, and Notion — characterized by clean typography, generous whitespace, smooth micro-interactions, purposeful color, and a seamless end-to-end experience. The redesign covers all five existing pages: LoginPage, Dashboard, CreateRegistry, RegistryDetail, and GuestRegistry.

---

## Glossary

- **Design_System**: The shared set of CSS custom properties, typography scales, spacing tokens, color palette, and component styles that govern the visual language of the app.
- **App**: The GiftTogether .NET MAUI Blazor Hybrid mobile application.
- **LoginPage**: The Razor component at `/` (unauthenticated) handling login and registration flows.
- **Dashboard**: The authenticated home screen listing the user's registries.
- **RegistryDetail**: The owner's management view for a single registry and its gift goals.
- **CreateRegistry**: The multi-step flow for creating a new registry and adding gift goals.
- **GuestRegistry**: The public-facing registry view shared with contributors.
- **Top_Bar**: The sticky navigation header present on all authenticated screens.
- **Bottom_Sheet**: A modal panel that slides up from the bottom of the screen.
- **Hero_Section**: The full-bleed banner at the top of RegistryDetail and GuestRegistry.
- **Goal_Card**: The card component representing a single gift goal.
- **Registry_Card**: The card component representing a registry in the Dashboard list.
- **Progress_Bar**: The animated bar showing funding progress toward a goal or registry total.
- **Contribution_Modal**: The Bottom_Sheet form through which guests submit contributions.
- **Empty_State**: The illustrated placeholder shown when a list has no items.
- **Skeleton_Loader**: An animated placeholder that mimics content layout during data loading.
- **Toast**: A transient, non-blocking notification that appears and auto-dismisses.
- **Safe_Area**: The device-specific inset region accounting for notches, status bars, and home indicators.
- **Color_Token**: A named CSS custom property representing a semantic color value.
- **Spacing_Token**: A named CSS custom property representing a spacing unit.
- **Transition_Token**: A named CSS custom property representing an animation duration and easing curve.

---

## Requirements

### Requirement 1: Design System Foundation

**User Story:** As a developer, I want a single source-of-truth design system, so that all screens share a consistent visual language and future changes can be made in one place.

#### Acceptance Criteria

1. THE Design_System SHALL define a complete color palette using CSS custom properties, including at minimum: `--color-primary`, `--color-primary-dark`, `--color-primary-light`, `--color-surface`, `--color-surface-raised`, `--color-background`, `--color-text-primary`, `--color-text-secondary`, `--color-text-disabled`, `--color-border`, `--color-success`, `--color-danger`, `--color-warning`.
2. THE Design_System SHALL define a typographic scale using CSS custom properties for font sizes (`--text-xs` through `--text-4xl`), font weights (`--weight-regular`, `--weight-medium`, `--weight-semibold`, `--weight-bold`, `--weight-extrabold`), and line heights (`--leading-tight`, `--leading-normal`, `--leading-relaxed`).
3. THE Design_System SHALL define spacing tokens (`--space-1` through `--space-16`) based on a 4px base unit.
4. THE Design_System SHALL define border-radius tokens (`--radius-sm`, `--radius-md`, `--radius-lg`, `--radius-xl`, `--radius-full`).
5. THE Design_System SHALL define transition tokens (`--transition-fast`, `--transition-base`, `--transition-slow`) with explicit duration and easing values.
6. THE Design_System SHALL define elevation tokens (`--shadow-sm`, `--shadow-md`, `--shadow-lg`) using layered box-shadow values.
7. WHERE the device is in dark mode, THE Design_System SHALL apply an alternative set of Color_Tokens via a `prefers-color-scheme: dark` media query, maintaining WCAG AA contrast ratios (minimum 4.5:1 for body text, 3:1 for large text).
8. THE Design_System SHALL load a premium sans-serif typeface (Inter or equivalent) as the primary font, with system-ui as the fallback stack.

---

### Requirement 2: Login and Registration Screen

**User Story:** As a new or returning user, I want a visually striking and frictionless login/registration experience, so that I feel confident in the app from the first moment.

#### Acceptance Criteria

1. THE LoginPage SHALL display a full-screen gradient background using `--color-primary` and `--color-primary-dark` with a subtle animated or static decorative element (e.g., soft radial glow or abstract shape).
2. THE LoginPage SHALL display the GiftTogether brand mark — an icon and wordmark — centered in the upper portion of the screen with a tagline.
3. THE LoginPage SHALL present the login and registration forms inside a floating card with `--radius-xl` corners and `--shadow-lg` elevation, positioned in the lower two-thirds of the screen.
4. WHEN the user taps a form input, THE LoginPage SHALL animate the input border to `--color-primary` with a smooth transition using `--transition-fast`.
5. WHEN the user switches between login and registration modes, THE LoginPage SHALL animate the form card content with a fade or slide transition rather than an instant swap.
6. WHEN the user submits a form with missing required fields, THE LoginPage SHALL display inline field-level error messages directly beneath the relevant input, styled with `--color-danger`.
7. WHEN the user taps the primary action button, THE LoginPage SHALL display a loading spinner inside the button and disable the button for the duration of the request.
8. IF an authentication error occurs, THEN THE LoginPage SHALL display an error banner at the top of the form card with `--color-danger` background, dismissible by the user.
9. THE LoginPage SHALL render all interactive elements with a minimum touch target size of 44×44 points.

---

### Requirement 3: Dashboard Screen

**User Story:** As an authenticated user, I want a clean, scannable dashboard, so that I can quickly see all my registries and their funding status at a glance.

#### Acceptance Criteria

1. THE Dashboard SHALL display a personalized greeting using the user's first name and a contextual time-of-day salutation (e.g., "Good morning, Jane").
2. THE Dashboard SHALL display a summary stat row showing the total number of registries and the aggregate funding percentage across all registries.
3. WHEN the user has no registries, THE Dashboard SHALL display an illustrated Empty_State with a headline, supporting copy, and a prominent "Create your first registry" call-to-action button.
4. WHEN the user has registries, THE Dashboard SHALL render each registry as a Registry_Card containing: registry name, goal count, total raised amount, total target amount, funding percentage, and an animated Progress_Bar.
5. THE Registry_Card SHALL display a hero thumbnail image if one is available, or a gradient placeholder derived from the registry name if no image exists.
6. WHEN the user taps a Registry_Card, THE Dashboard SHALL navigate to RegistryDetail with a slide-in transition.
7. THE Dashboard SHALL display a floating action button (FAB) or a prominent header button to create a new registry.
8. WHILE data is loading, THE Dashboard SHALL display Skeleton_Loaders in the shape of Registry_Cards rather than a generic spinner.
9. WHEN a registry reaches 100% funding, THE Registry_Card SHALL display a "Fully funded" badge using `--color-success`.
10. THE Dashboard Top_Bar SHALL display the GiftTogether brand mark on the left and a user avatar or initials button on the right that opens a profile/logout menu.

---

### Requirement 4: Registry Detail Screen (Owner View)

**User Story:** As a registry owner, I want a rich, immersive detail view, so that I can manage my registry and feel proud sharing it with guests.

#### Acceptance Criteria

1. THE RegistryDetail SHALL display a full-bleed Hero_Section at the top of the screen, spanning the full device width and a minimum height of 220px, showing the registry hero image or a gradient fallback.
2. THE Hero_Section SHALL overlay the owner's avatar, name, and registry title on the hero image using a dark gradient scrim for legibility.
3. THE RegistryDetail SHALL display a floating progress summary card that overlaps the bottom edge of the Hero_Section by 24px, showing total raised, total target, and an animated Progress_Bar.
4. THE RegistryDetail SHALL display each gift goal as a Goal_Card with: goal image (or placeholder), goal name, description, target amount badge, animated Progress_Bar, raised amount, and contributor count.
5. WHEN the user taps "+ Goal" in the Top_Bar, THE RegistryDetail SHALL present the add-goal form as a Bottom_Sheet with a drag handle, sliding up with a spring animation.
6. WHEN the user adds a goal successfully, THE RegistryDetail SHALL dismiss the Bottom_Sheet and display a Toast notification confirming the addition, without a full page reload.
7. WHEN the user taps "Remove" on a Goal_Card, THE RegistryDetail SHALL present a confirmation Bottom_Sheet before executing the deletion.
8. THE RegistryDetail SHALL display share action buttons (Copy Link, WhatsApp) in the progress summary card, styled as secondary action buttons.
9. THE Top_Bar SHALL display a back chevron on the left and the registry name (truncated with ellipsis if needed) centered, with the "+ Goal" action on the right.
10. WHEN the user scrolls down, THE Hero_Section SHALL exhibit a parallax scroll effect or gracefully collapse, keeping the Top_Bar sticky.

---

### Requirement 5: Create Registry Flow

**User Story:** As a user, I want a guided, step-by-step registry creation experience, so that I can set up my registry without feeling overwhelmed.

#### Acceptance Criteria

1. THE CreateRegistry SHALL present the creation flow as a two-step wizard with a visual step indicator (e.g., segmented progress bar or numbered steps) at the top of the screen.
2. THE CreateRegistry SHALL display step 1 (registry details) with fields for registry name and description, and a "Continue" primary action button.
3. WHEN the user completes step 1 and taps "Continue", THE CreateRegistry SHALL animate the transition to step 2 (add gift goals) using a horizontal slide animation.
4. THE CreateRegistry SHALL display step 2 with fields for gift name, description, target amount, and product link, plus an "Add goal" button and a "Done" secondary action.
5. WHEN the user adds a goal in step 2, THE CreateRegistry SHALL append the new goal to an inline list below the form with a smooth insertion animation, without navigating away.
6. WHEN the user taps "Done" in step 2, THE CreateRegistry SHALL navigate to RegistryDetail for the newly created registry.
7. IF a required field is empty when the user taps a primary action, THEN THE CreateRegistry SHALL highlight the empty field with a `--color-danger` border and display an inline error message.
8. THE CreateRegistry Top_Bar SHALL display a back button on the left and the current step title centered.

---

### Requirement 6: Guest Registry Screen

**User Story:** As a guest contributor, I want a beautiful, trustworthy registry page, so that I feel confident and excited to contribute to the gift.

#### Acceptance Criteria

1. THE GuestRegistry SHALL display a full-bleed Hero_Section matching the visual treatment of RegistryDetail, including the creator's avatar, name, registry title, and personal message.
2. THE GuestRegistry SHALL display the overall funding progress card below the Hero_Section, showing total raised, total target, percentage funded, and goal count.
3. THE GuestRegistry SHALL display each Goal_Card with: image, name, description, product link, Progress_Bar, raised amount, and a "Contribute" button.
4. WHEN a goal is fully funded, THE GuestRegistry SHALL replace the "Contribute" button with a "Fully funded ✓" badge and visually distinguish the card (e.g., reduced opacity or a success overlay).
5. WHEN the user taps "Contribute" on a Goal_Card, THE GuestRegistry SHALL present the Contribution_Modal as a Bottom_Sheet with fields for contributor name (optional), amount (required), and message (optional).
6. WHEN the user submits a valid contribution, THE GuestRegistry SHALL display a success state inside the Contribution_Modal with a celebratory animation (e.g., confetti or checkmark animation) before allowing the user to close.
7. IF the user submits the contribution form with an invalid or missing amount, THEN THE GuestRegistry SHALL display an inline error message beneath the amount field without closing the modal.
8. THE GuestRegistry SHALL display a trust notice and test-mode banner using distinct, non-alarming visual treatments (amber and blue info banners respectively).
9. THE GuestRegistry Top_Bar SHALL display only the GiftTogether brand mark, with no authenticated navigation elements.
10. THE GuestRegistry SHALL display share action buttons (Copy Link, WhatsApp) in the progress card, accessible without scrolling past the hero.

---

### Requirement 7: Navigation and Transitions

**User Story:** As a user, I want smooth, native-feeling navigation transitions, so that the app feels polished and responsive.

#### Acceptance Criteria

1. WHEN the user navigates between screens, THE App SHALL apply a directional slide transition: forward navigation slides new content in from the right, back navigation slides content out to the right.
2. WHEN a Bottom_Sheet opens, THE App SHALL animate it sliding up from the bottom with a spring easing curve over 300ms.
3. WHEN a Bottom_Sheet closes, THE App SHALL animate it sliding down and out over 200ms.
4. WHEN a Toast appears, THE App SHALL animate it sliding in from the top or bottom edge and auto-dismiss after 3 seconds with a fade-out.
5. THE App SHALL apply a press feedback animation (scale down to 0.96 and opacity to 0.85) on all interactive elements within 100ms of a touch event.
6. WHEN a Progress_Bar value changes, THE App SHALL animate the width change over 600ms using an ease-out curve.
7. THE App SHALL respect the `prefers-reduced-motion` media query by disabling non-essential animations for users who have enabled reduced motion in their device settings.

---

### Requirement 8: Loading and Error States

**User Story:** As a user, I want clear feedback during loading and error conditions, so that I always know what the app is doing.

#### Acceptance Criteria

1. WHILE any screen is loading its initial data, THE App SHALL display Skeleton_Loaders that match the layout of the content being loaded, using a shimmer animation.
2. IF a network request fails, THEN THE App SHALL display an inline error state with a descriptive message and a "Try again" retry button, rather than leaving the screen blank.
3. WHEN a destructive action (delete registry, remove goal) is confirmed, THE App SHALL display a loading indicator inside the confirmation button and disable all other interactive elements in the Bottom_Sheet for the duration of the request.
4. THE Skeleton_Loader shimmer animation SHALL use a gradient sweep from `--color-border` to `--color-surface-raised` and back, cycling every 1.5 seconds.
5. WHEN an operation completes successfully, THE App SHALL display a Toast with a checkmark icon and a brief success message.

---

### Requirement 9: Accessibility and Safe Area Handling

**User Story:** As a user on any device, I want the app to render correctly and be usable regardless of screen size, notch, or accessibility settings.

#### Acceptance Criteria

1. THE App SHALL apply `env(safe-area-inset-top)`, `env(safe-area-inset-bottom)`, `env(safe-area-inset-left)`, and `env(safe-area-inset-right)` to all fixed and sticky elements to avoid content being obscured by device hardware.
2. THE App SHALL ensure all text meets WCAG AA contrast requirements: minimum 4.5:1 contrast ratio for body text and 3:1 for large text (18px+ or 14px+ bold) against their backgrounds.
3. THE App SHALL render all interactive elements (buttons, links, inputs) with a minimum touch target size of 44×44 points.
4. THE App SHALL support dynamic text sizing: all font sizes SHALL use `rem` units so that the layout scales proportionally when the user changes their device font size setting.
5. WHEN a form input receives focus, THE App SHALL display a visible focus ring using `--color-primary` with sufficient contrast against the input background.
6. THE App SHALL not rely solely on color to convey information; icons, labels, or patterns SHALL accompany color-coded states (e.g., error, success, warning).
