# Implementation Plan: GiftTogether Mobile UI Design System

## Overview

Implement the GiftTogether mobile app UI redesign using a token-driven CSS architecture, shared Razor components, and premium micro-interactions across all five screens. The implementation follows the design document exactly: CSS files are created first (tokens → base → components → animations → dark → app.css entry point), then Inter font loading, then shared Razor components, then each page redesign, and finally page-transition wiring in MainLayout. Property-based tests using CsCheck validate the 17 correctness properties defined in the design.

All code is C# / Razor / CSS targeting .NET MAUI Blazor Hybrid (net9.0-android, net9.0-ios, net9.0-maccatalyst, net9.0-windows).

## Tasks

- [x] 1. Create CSS design token foundation
  - Create `GiftTogether.Mobile/wwwroot/css/tokens.css` with all `:root` custom properties from the design: complete color palette (`--color-primary` through `--color-whatsapp`), typographic scale (`--text-xs` through `--text-4xl`, all weight and line-height tokens), spacing tokens (`--space-1` through `--space-16` on a 4px base unit), border-radius tokens (`--radius-sm` through `--radius-full`), transition tokens (`--transition-fast`, `--transition-base`, `--transition-slow`, `--transition-spring`), and elevation tokens (`--shadow-sm`, `--shadow-md`, `--shadow-lg`, `--shadow-xl`)
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

- [x] 2. Create base, components, animations, and dark CSS files
  - [x] 2.1 Create `GiftTogether.Mobile/wwwroot/css/base.css` with CSS reset (`*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0 }`), `html`/`body` styles using Inter font stack with system-ui fallback, `rem`-based font sizes, and typography utility classes
    - _Requirements: 1.8, 9.4_

  - [x] 2.2 Create `GiftTogether.Mobile/wwwroot/css/components.css` with all shared component styles: `.top-bar` (sticky, grid layout, safe-area padding-top), `.sheet` / `.sheet-backdrop` (bottom sheet), `.toast`, `.skeleton--registry-card` / `.skeleton--goal-card`, `.progress-track` / `.progress-fill` / `.progress-fill--animated`, `.goal-card`, `.registry-card`, `.empty-state`, `.btn` variants, `.form-group` with focus ring, `.badge`, `.share-row`, `.error-state`, `.fab`, and all other component classes from the design
    - All interactive elements must have `min-height: 44px` and `min-width: 44px`
    - All fixed/sticky elements must include `env(safe-area-inset-*)` padding
    - Focus ring: `outline: 2px solid var(--color-primary); outline-offset: 2px` on `:focus-visible`
    - _Requirements: 1.1–1.6, 7.5, 9.1, 9.3, 9.5_

  - [x] 2.3 Create `GiftTogether.Mobile/wwwroot/css/animations.css` with all `@keyframes` definitions: `shimmer` (skeleton, 1.5s, gradient sweep), `slideInRight` / `slideInLeft` (page transitions, 300ms), `spin` (button loading spinner), `confettiFall` (GuestRegistry success), `slideUp` (toast enter), `fadeIn` / `fadeOut` (form mode switch); and `.page--enter-forward` / `.page--enter-back` animation utility classes; and press-feedback active states (`transform: scale(0.96); opacity: 0.85`)
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 8.4_

  - [x] 2.4 Create `GiftTogether.Mobile/wwwroot/css/dark.css` with `@media (prefers-color-scheme: dark)` block overriding all color tokens with dark-mode values from the design, and `@media (prefers-reduced-motion: reduce)` block setting `animation-duration: 0.01ms !important` and `transition-duration: 0.01ms !important` on `*, *::before, *::after`
    - _Requirements: 1.7, 7.7_

  - [x] 2.5 Replace the content of `GiftTogether.Mobile/wwwroot/css/app.css` with the five `@import` statements in order: `tokens.css`, `base.css`, `components.css`, `animations.css`, `dark.css`; remove all existing ad-hoc CSS from this file
    - _Requirements: 1.1_

  - [ ]* 2.6 Write property test: spacing token formula (Property 1)
    - **Property 1: Spacing Token Formula** — parse `tokens.css` and assert that for every N in [1, 16], `--space-N` equals `N * 0.25rem`
    - **Validates: Requirements 1.3**

  - [ ]* 2.7 Write property test: all font-size declarations use rem units (Property 16)
    - **Property 16: All Font Size Declarations Use rem Units** — parse all CSS files and assert that every `font-size` value uses `rem` units, not `px`, `pt`, or `em`
    - **Validates: Requirements 9.4**

- [x] 3. Load Inter font in index.html and register in MauiProgram.cs
  - Add Google Fonts CDN preconnect and stylesheet links for Inter (weights 400, 500, 600, 700, 800 with `display=swap`) to `GiftTogether.Mobile/wwwroot/index.html` `<head>`, before the existing `app.css` link
  - Add JS interop helpers to `index.html`: `window.attachParallax`, `window.trapFocus`, and `window.confetti` (or inline confetti CSS approach per design)
  - Download Inter `.ttf` files (Regular, Medium, SemiBold, Bold) to `GiftTogether.Mobile/Resources/Fonts/` and register all four in `MauiProgram.cs` `ConfigureFonts` block, replacing the existing OpenSans registration
  - _Requirements: 1.8_

- [x] 4. Create shared Razor components in `Components/Shared/`
  - [x] 4.1 Create `TopBar.razor` with parameters: `Title` (string), `ShowBack` (bool), `OnBack` (EventCallback), `RightContent` (RenderFragment?); render `<header class="top-bar" role="banner">` with a 3-column grid layout; left slot shows back chevron SVG button (44×44, `aria-label="Go back"`) when `ShowBack` is true, or brand mark when false; center shows `<h1 class="top-bar-title">` when Title is non-empty; right slot renders `RightContent`
    - _Requirements: 3.10, 4.9, 5.8, 6.9, 9.1, 9.3_

  - [x] 4.2 Create `BottomSheet.razor` with parameters: `IsOpen` (bool), `OnClose` (EventCallback), `Title` (string), `ChildContent` (RenderFragment?); render backdrop div with `role="dialog" aria-modal="true"` and spring-animated sheet panel; include drag handle, title `<h2>`, and scrollable body; call `JS.InvokeVoidAsync("trapFocus", sheetId)` in `OnAfterRenderAsync` when `IsOpen` becomes true
    - _Requirements: 4.5, 4.7, 5.4, 6.5, 7.2, 7.3, 8.3, 9.1_

  - [x] 4.3 Create `Toast.razor` with a public `Show(string message, string type = "success")` method; manage `_visible`, `_entering`, `_message`, `_type` state; render `role="status" aria-live="polite"` div with enter/exit CSS classes; auto-dismiss after 2800ms visible + 200ms fade-out; include icon (✓ / ✕ / ℹ) alongside message text
    - _Requirements: 7.4, 8.5, 9.6_

  - [x] 4.4 Create `SkeletonLoader.razor` with parameters: `Variant` (string: "registry-card" | "goal-card" | "hero" | "text"), `Count` (int, default 3); render `Count` skeleton items with `aria-hidden="true"`; registry-card variant renders title/meta/bar lines; goal-card variant renders image block + body lines; all skeleton elements use the `shimmer` animation class
    - _Requirements: 3.8, 8.1, 8.4_

  - [x] 4.5 Create `ProgressBar.razor` with parameters: `Value` (decimal, 0–100), `Height` (string, default "10px"), `Animated` (bool, default true); render `<div class="progress-track" role="progressbar" aria-valuenow="..." aria-valuemin="0" aria-valuemax="100" aria-label="...% funded">`; inner fill div gets `progress-fill--animated` class when `Animated` is true; clamp value to [0, 100]
    - _Requirements: 3.4, 7.6, 9.2_

  - [ ]* 4.6 Write property test: ProgressBar applies animated transition on any value (Property 12)
    - **Property 12: Progress Bar Applies Animated Transition on Any Value Change** — for any Value in [0, 100] with Animated=true, assert the rendered element has class `progress-fill--animated` and the CSS specifies `600ms cubic-bezier(0.0, 0, 0.2, 1)` width transition
    - **Validates: Requirements 7.6**

  - [x] 4.7 Create `GoalCard.razor` with parameters: `Goal` (GiftGoalResponse, required), `IsOwner` (bool), `OnContribute` (EventCallback<GiftGoalResponse>), `OnRemove` (EventCallback<GiftGoalResponse>); render `<article class="goal-card">` with image or placeholder, goal name `<h3>`, target amount badge, product link, description, `<ProgressBar>` component, raised/percentage amounts, contributor list (max 3 shown), and action area; funded goals (`TotalRaised >= TargetAmount`) show "Fully funded" badge with ✓ icon and `--color-success` styling; owner mode shows Remove button; guest mode shows Contribute button; funded guest cards get `goal-card--funded` class (reduced opacity)
    - _Requirements: 4.4, 6.3, 6.4, 9.3, 9.6_

  - [ ]* 4.8 Write property test: GoalCard renders all required fields for any goal (Property 8)
    - **Property 8: GoalCard Renders All Required Fields for Any Goal** — for any GiftGoalResponse (varying image presence, description, product link, funding level), assert the rendered GoalCard contains: image or placeholder, goal name, target amount badge, ProgressBar, raised amount, and correct action (Contribute button or Fully funded badge)
    - **Validates: Requirements 4.4, 6.3**

  - [ ]* 4.9 Write property test: funded goal cards replace Contribute button with badge (Property 7)
    - **Property 7: Funded Goal Cards Replace Contribute Button with Badge** — for any GiftGoalResponse where TotalRaised >= TargetAmount, assert the rendered GoalCard in guest mode shows a "Fully funded" badge with ✓ icon and `--color-success` class, and does NOT render a "Contribute" button; also assert `goal-card--funded` class is applied
    - **Validates: Requirements 3.9, 6.4**

  - [x] 4.10 Create `RegistryCard.razor` with parameters: `Registry` (RegistryResponse, required), `OnTap` (EventCallback); render `<article class="registry-card" role="button" tabindex="0" aria-label="...">` with 64×64 thumbnail (hero image or deterministic gradient from name hash), info column (name, goal count + raised amount, `<ProgressBar Height="6px">`), "✓ Fully funded" badge when `FundedPct >= 100`, and chevron SVG; implement `GetGradient(string name)` using `Math.Abs(name.GetHashCode()) % 360` for hue
    - _Requirements: 3.4, 3.5, 3.9, 9.3_

  - [ ]* 4.11 Write property test: RegistryCard renders all required fields for any registry (Property 6)
    - **Property 6: RegistryCard Renders All Required Fields for Any Registry** — for any RegistryResponse (varying HeroImageUrl, goal count, funding level), assert the rendered RegistryCard displays: registry name, goal count, total raised, total target, funding percentage, a ProgressBar, and either the hero thumbnail or a gradient placeholder
    - **Validates: Requirements 3.4, 3.5**

  - [x] 4.12 Create `EmptyState.razor` with parameters: `Icon` (string, default "🎁"), `Title` (string), `Body` (string), `ActionContent` (RenderFragment?); render `<div class="empty-state" role="status">` with illustration div, `<h2>` title, `<p>` body, and optional action slot
    - _Requirements: 3.3_

- [ ] 5. Checkpoint — Ensure all shared components compile and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Redesign LoginPage
  - Rewrite `GiftTogether.Mobile/Components/Pages/LoginPage.razor` to use the new design system:
    - Full-screen gradient background (`--color-primary` → `--color-primary-dark` at 160°) with `::before` radial glow pseudo-element
    - Brand section in upper 35%: SVG gift icon (56px), wordmark (Inter 800, `--text-3xl`), tagline
    - Auth card in lower 65% with `--radius-xl`, `--shadow-lg`; card mounts with spring animation (`translateY(40px)` → `translateY(0)`, 300ms)
    - Mode switching (login ↔ register) uses `.form-mode--exit` / `.form-mode--enter` CSS fade classes (150ms each)
    - Input focus: `border-color` → `--color-primary` + `box-shadow: 0 0 0 3px var(--color-primary-light)` via `--transition-fast`
    - Loading state: spinner inside button + `disabled` attribute
    - Error banner: slides down from `translateY(-8px)`, `--color-danger-bg` background, dismissible with ✕ button (44×44)
    - Field-level validation errors: `⚠` icon + message in `--text-xs` `--color-danger-text` below each invalid input
    - All inputs and buttons meet 44×44pt touch target minimum
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9_

  - [ ]* 6.1 Write property test: form validation highlights all empty required fields (Property 3)
    - **Property 3: Form Validation Highlights All Empty Required Fields** — for any non-empty subset of required fields left empty on LoginPage or CreateRegistry, assert each empty field receives `--color-danger` border and an inline error message, and no empty required field is silently ignored
    - **Validates: Requirements 2.6, 5.7**

- [x] 7. Redesign Dashboard
  - Rewrite `GiftTogether.Mobile/Components/Pages/Dashboard.razor` to use the new design system:
    - Use `<TopBar>` component with brand mark left and user-initials avatar button right (44×44 circular, `--color-primary-light` bg); avatar tap opens logout popover
    - Greeting + stats row: `GetGreeting(Auth.UserName, DateTime.Now)` left, stat pill right ("N registries · X% funded")
    - Loading state: `<SkeletonLoader Variant="registry-card" Count="3" />` instead of spinner
    - Empty state: `<EmptyState>` component with "No registries yet" title and "Create your first registry" CTA button
    - Registry list: `<RegistryCard>` components with `OnTap` navigating to `/registry/{id}` with slide-forward transition
    - FAB: `position: fixed`, bottom-right, 56×56, `--color-primary`, `--shadow-lg`, `bottom: calc(var(--space-6) + env(safe-area-inset-bottom))`
    - Error state: `<div class="error-state">` with message + "Try again" retry button
    - Implement `GetGreeting` and `ComputeStats` static helper methods per design spec
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9, 3.10, 8.1, 8.2_

  - [ ]* 7.1 Write property test: greeting function correctness (Property 4)
    - **Property 4: Greeting Function Correctness** — for any full name string and any hour 0–23, assert `GetGreeting` returns the first name (substring before first space) with "Good morning" for hours 5–11, "Good afternoon" for 12–16, "Good evening" for all others
    - **Validates: Requirements 3.1**

  - [ ]* 7.2 Write property test: aggregate stats computation (Property 5)
    - **Property 5: Aggregate Stats Computation** — for any list of RegistryResponse objects (including empty list, goals at various funding levels), assert `ComputeStats` returns count equal to list length and aggregate percentage equal to `floor(min(100, totalRaised / totalTarget × 100))` with 0% when totalTarget is zero
    - **Validates: Requirements 3.2**

- [x] 8. Redesign RegistryDetail
  - Rewrite `GiftTogether.Mobile/Components/Pages/RegistryDetail.razor` to use the new design system:
    - Use `<TopBar ShowBack="true" OnBack="GoBack" Title="@(_registry?.Name)" RightContent="...">` with "+ Goal" button in right slot
    - Full-bleed `<div class="registry-hero">` with `min-height: 220px`, hero image + dark gradient scrim overlay, owner avatar (80px, white border), "Created by" label, owner name, registry title (Inter 800, `--text-3xl`), personal message
    - Parallax: call `JS.InvokeVoidAsync("attachParallax", "hero-img", "scroll-container")` in `OnAfterRenderAsync`; apply `--hero-parallax-offset` CSS variable to hero image
    - Progress summary card: `margin-top: -24px`, `position: relative`, `z-index: 2`, `--radius-xl`, `--shadow-lg`; contains raised amount, `<ProgressBar>`, percentage + goal count, share buttons row
    - Loading state: `<SkeletonLoader Variant="goal-card" Count="2" />` + hero skeleton
    - Goal list: `<GoalCard IsOwner="true" OnRemove="ShowDeleteGoal">` for each goal
    - Empty goals state: `<EmptyState>` with "No gift goals yet" copy
    - Add-goal bottom sheet: `<BottomSheet IsOpen="_showAddGoal" OnClose="CloseModals" Title="Add a gift goal">` with form fields; on success call `_toast.Show("✓ Goal added!")` and close sheet without full reload
    - Delete-goal confirmation: `<BottomSheet>` with destructive confirm pattern (loading spinner in button, all other elements disabled during request)
    - Add `<Toast @ref="_toast" />` component reference
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10, 8.3_

- [x] 9. Redesign CreateRegistry
  - Rewrite `GiftTogether.Mobile/Components/Pages/CreateRegistry.razor` to use the new design system:
    - Use `<TopBar ShowBack="true" OnBack="GoBack" Title="@(_step == 1 ? "Registry details" : "Add gift goals")">` 
    - Two-segment step indicator below top bar: active segment `--color-primary`, inactive `--color-border`, 4px height, `--radius-full`, animated width fill
    - Step 1: registry name + description fields, "Continue →" primary button; on submit validate required fields with `--color-danger` border + inline error messages
    - Step transition: step 1 slides out to `translateX(-100%)`, step 2 slides in from `translateX(100%)`, 300ms `cubic-bezier(0.4, 0, 0.2, 1)`; use `_step` int field (1 or 2) and CSS classes `.step--exit-left` / `.step--enter-right`
    - Step 2: gift name, description, target amount, product link fields; "Add goal" primary + "Done" outline buttons side by side; added goals append to inline list with `slideInUp` animation (200ms)
    - Field validation: `border-color: var(--color-danger)` + inline error on submit attempt; border resets to `--color-border` on input change
    - "Done" navigates to `/registry/{_registry.Id}`
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8_

  - [ ]* 9.1 Write property test: goal list grows by exactly one on valid addition (Property 9)
    - **Property 9: Goal List Grows by Exactly One on Valid Addition** — for any initial goal list and any valid goal input (non-empty name, positive target amount), after submitting the add-goal form, assert the inline list contains exactly one more item than before, and the new item displays the entered name and target amount
    - **Validates: Requirements 5.5**

- [x] 10. Redesign GuestRegistry
  - Rewrite `GiftTogether.Mobile/Components/Pages/GuestRegistry.razor` to use the new design system:
    - Use `<TopBar>` with brand mark only (no back button, no authenticated actions)
    - Full-bleed hero section matching RegistryDetail visual treatment (no parallax); includes creator avatar, name, registry title, personal message
    - Overall funding progress card below hero: total raised, total target, `<ProgressBar>`, percentage funded, goal count, share buttons row (accessible without scrolling past hero)
    - Trust banners: amber (`--color-warning-bg`, `--color-warning-text`, amber left border, ⚠ icon) and blue (`--color-info-bg`, `--color-info-text`, blue left border, 🧪 icon)
    - Goal list: `<GoalCard IsOwner="false" OnContribute="OpenContribute">` for each goal; funded goals show "Fully funded ✓" badge and `goal-card--funded` class
    - Contribution bottom sheet: `<BottomSheet IsOpen="_showContribute" OnClose="CloseContribute" Title='Contribute to "@_activeGoal?.Name"'>` with contributor name (optional), amount (required), message (optional) fields; inline error beneath amount field on invalid submit (sheet stays open)
    - Success state inside sheet: replace form content with centered 🎉 emoji, "Thank you!" heading, supporting copy, "Close" button, and CSS confetti animation (12 `<span>` elements with `--confetti-x` and `--confetti-delay` inline CSS variables)
    - Add `<Toast @ref="_toast" />` for copy-link feedback
    - Error state: `<div class="error-state">` with "Try again" button when registry load fails
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8, 6.9, 6.10, 8.2_

  - [ ]* 10.1 Write property test: invalid contribution amount shows inline error without closing modal (Property 10)
    - **Property 10: Invalid Contribution Amount Shows Inline Error Without Closing Modal** — for any invalid amount value (zero, negative, empty string, non-numeric), assert submitting the contribution form displays an inline error beneath the amount field and `_showContribute` remains true
    - **Validates: Requirements 6.7**

- [x] 11. Wire page transitions in MainLayout
  - Update `GiftTogether.Mobile/Components/Layout/MainLayout.razor` to inject `NavigationManager` and subscribe to `LocationChanged` event in `OnInitialized`
  - Track `_transitioning` (bool) and `_direction` ("forward" | "back") state fields
  - Apply `.page--enter-forward` or `.page--enter-back` CSS class to the `<div class="page">` wrapper based on direction when `_transitioning` is true; reset after 300ms via `Task.Delay(300)`
  - Implement `IDisposable` to unsubscribe from `LocationChanged` on dispose
  - _Requirements: 7.1_

- [ ] 12. Checkpoint — Ensure all tests pass and all pages render correctly
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 13. Create CsCheck property-based test project
  - Create a new xUnit test project `GiftTogether.Mobile.Tests/GiftTogether.Mobile.Tests.csproj` targeting `net9.0` with `PackageReference` for `CsCheck` (version `3.*`) and `xunit` / `xunit.runner.visualstudio`
  - Add a `<ProjectReference>` to `GiftTogether.Mobile.csproj`
  - Create `GiftTogether.Mobile.Tests/DesignSystemTests.cs` with the test class skeleton and `using CsCheck;` import
  - Tag format for all tests: `// Feature: mobile-app-ui-design, Property {N}: {property_text}`
  - _Requirements: 1.3, 1.7, 3.1, 3.2, 9.4_

- [ ] 14. Implement remaining property-based tests
  - [ ] 14.1 Write property test: WCAG AA contrast for all color token pairs (Property 2)
    - **Property 2: WCAG AA Contrast for All Color Token Pairs** — parse `tokens.css` and `dark.css` to extract all text/background color token pairs; for each pair compute relative luminance contrast ratio; assert ≥ 4.5:1 for body text pairs and ≥ 3:1 for large text pairs in both light and dark themes
    - **Validates: Requirements 1.7, 9.2**

  - [ ] 14.2 Write property test: Toast auto-dismisses after three seconds (Property 11)
    - **Property 11: Toast Auto-Dismisses After Three Seconds** — for any message string and type ("success", "error", "info"), call `Toast.Show()` and assert the component is visible for 2800ms ± 100ms then transitions to invisible, regardless of message content or length
    - **Validates: Requirements 7.4**

  - [ ] 14.3 Write property test: reduced motion disables all non-essential animations (Property 13)
    - **Property 13: Reduced Motion Disables All Non-Essential Animations** — parse `dark.css` and assert the `prefers-reduced-motion: reduce` block sets `animation-duration: 0.01ms !important` and `transition-duration: 0.01ms !important` on `*, *::before, *::after`; enumerate all `@keyframes` names in `animations.css` and assert each is covered by the reduced-motion override
    - **Validates: Requirements 7.7**

  - [ ] 14.4 Write property test: network error always shows inline error state with retry button (Property 14)
    - **Property 14: Network Error Always Shows Inline Error State with Retry Button** — for any HTTP error status code (400–599) or simulated connection timeout on any page's data-load method, assert the page renders an element with class `error-state` containing a non-empty error message and a button with text "Try again", and does not render only a spinner
    - **Validates: Requirements 8.2**

  - [ ] 14.5 Write property test: all fixed and sticky elements include safe area insets (Property 15)
    - **Property 15: All Fixed and Sticky Elements Include Safe Area Insets** — parse all CSS files and assert that every selector with `position: fixed` or `position: sticky` includes at least one `env(safe-area-inset-*)` value in its padding or margin declarations
    - **Validates: Requirements 9.1**

  - [ ] 14.6 Write property test: all color-coded states include a non-color indicator (Property 17)
    - **Property 17: All Color-Coded States Include a Non-Color Indicator** — for each color-coded state rendered in the app (error, success, warning, funded, loading), assert the rendered HTML includes at least one non-color indicator: an SVG icon, emoji, or text label alongside the color styling
    - **Validates: Requirements 9.6**

- [ ] 15. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP delivery
- Each task references specific requirements for traceability
- The CSS files must be created before any Razor component work begins (tasks 1–3 before task 4)
- Shared components must be created before page redesigns (task 4 before tasks 6–10)
- The test project (task 13) should be created before the remaining property tests (task 14), but individual property tests in tasks 2, 4, 6–10 can be written alongside their implementation tasks
- Property tests use CsCheck with minimum 100 iterations per property
- All 17 correctness properties from the design document are covered across tasks 2, 4, 6, 7, 9, 10, and 14
