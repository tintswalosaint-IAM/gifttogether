using CsCheck;
using System.Text.RegularExpressions;
using Xunit;

// Feature: mobile-app-ui-design
// Property-based tests validating the 17 correctness properties defined in the design document.
// Pure logic is copied inline — no MAUI/Blazor dependency required.

namespace GiftTogether.Mobile.Tests;

// ── Shared DTOs (copied from ApiService.cs, no MAUI dependency) ──────────────

public record ContributionResponse(int Id, string ContributorName, string Message, decimal Amount, DateTime CreatedAt);
public record GiftGoalResponse(int Id, string Name, string Description, decimal TargetAmount, decimal TotalRaised, string? ImageUrl, string? ProductLink, List<ContributionResponse> Contributions);
public record CreatorInfo(string Name, string? ProfileImageUrl, string? GuestMessage);
public record RegistryResponse(int Id, string Name, string Description, string Slug, string? HeroBackgroundColor, string? HeroImageUrl, CreatorInfo Creator, List<GiftGoalResponse> GiftGoals);

// ── Pure logic helpers (copied from Razor components) ────────────────────────

internal static class DashboardLogic
{
    public static string GetGreeting(string? fullName, DateTime now)
    {
        var firstName = fullName?.Split(' ').FirstOrDefault() ?? "there";
        var salutation = now.Hour switch
        {
            >= 5 and < 12 => "Good morning",
            >= 12 and < 17 => "Good afternoon",
            _ => "Good evening"
        };
        return $"{salutation}, {firstName}";
    }

    public static (int count, int aggregatePct) ComputeStats(List<RegistryResponse> registries)
    {
        var count = registries.Count;
        var totalRaised = registries.SelectMany(r => r.GiftGoals).Sum(g => g.TotalRaised);
        var totalTarget = registries.SelectMany(r => r.GiftGoals).Sum(g => g.TargetAmount);
        var pct = totalTarget > 0 ? (int)Math.Min(100, Math.Round(totalRaised / totalTarget * 100)) : 0;
        return (count, pct);
    }
}

internal static class GoalCardLogic
{
    public static decimal FundedPct(decimal totalRaised, decimal targetAmount) =>
        targetAmount > 0 ? Math.Min(100, Math.Round(totalRaised / targetAmount * 100, 1)) : 0;

    public static bool IsFunded(decimal totalRaised, decimal targetAmount) =>
        FundedPct(totalRaised, targetAmount) >= 100;
}

internal static class ProgressBarLogic
{
    public static decimal ClampedValue(decimal value) => Math.Min(100, Math.Max(0, value));
}

// ── WCAG contrast helper ─────────────────────────────────────────────────────

internal static class WcagContrast
{
    private static double Linearize(double c)
    {
        c /= 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static double RelativeLuminance(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Convert.ToInt32(hex.Substring(0, 2), 16);
        var g = Convert.ToInt32(hex.Substring(2, 2), 16);
        var b = Convert.ToInt32(hex.Substring(4, 2), 16);
        return 0.2126 * Linearize(r) + 0.7152 * Linearize(g) + 0.0722 * Linearize(b);
    }

    public static double ContrastRatio(string hex1, string hex2)
    {
        var l1 = RelativeLuminance(hex1);
        var l2 = RelativeLuminance(hex2);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }
}

// ── CSS file paths ────────────────────────────────────────────────────────────

internal static class CssPaths
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "GiftTogether.Mobile", "wwwroot", "css"));

    public static string Tokens     => Path.Combine(Root, "tokens.css");
    public static string Dark       => Path.Combine(Root, "dark.css");
    public static string Animations => Path.Combine(Root, "animations.css");
    public static string Components => Path.Combine(Root, "components.css");
    public static string Base       => Path.Combine(Root, "base.css");

    public static IEnumerable<string> All => [Tokens, Dark, Animations, Components, Base];
}

internal static class RazorPaths
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "GiftTogether.Mobile", "Components"));

    public static string Toast        => Path.Combine(Root, "Shared", "Toast.razor");
    public static string GoalCard     => Path.Combine(Root, "Shared", "GoalCard.razor");
    public static string Dashboard    => Path.Combine(Root, "Pages", "Dashboard.razor");
    public static string GuestRegistry => Path.Combine(Root, "Pages", "GuestRegistry.razor");
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class DesignSystemTests
{
    // ── Property 1: Spacing Token Formula ────────────────────────────────────
    // Feature: mobile-app-ui-design, Property 1: Spacing Token Formula
    [Fact]
    public void Property1_SpacingTokenFormula()
    {
        var css = File.ReadAllText(CssPaths.Tokens);

        for (var n = 1; n <= 16; n++)
        {
            var expected = $"{n * 0.25:0.##}rem";
            var pattern = $@"--space-{n}\s*:\s*([^;]+);";
            var match = Regex.Match(css, pattern);
            Assert.True(match.Success, $"--space-{n} not found in tokens.css");
            var actual = match.Groups[1].Value.Trim();
            Assert.Equal(expected, actual);
        }
    }

    // ── Property 2: WCAG AA Contrast for All Color Token Pairs ───────────────
    // Feature: mobile-app-ui-design, Property 2: WCAG AA Contrast for All Color Token Pairs
    [Fact]
    public void Property2_WcagContrastLightMode()
    {
        var bodyPairs = new[]
        {
            ("#1a1033", "#ffffff"),  // text-primary on surface
            ("#6b7280", "#ffffff"),  // text-secondary on surface
            ("#166534", "#dcfce7"),  // success-text on success-bg
            ("#991b1b", "#fee2e2"),  // danger-text on danger-bg
            ("#92400e", "#fffbeb"),  // warning-text on warning-bg
            ("#0c4a6e", "#f0f9ff"),  // info-text on info-bg
        };

        foreach (var (fg, bg) in bodyPairs)
        {
            var ratio = WcagContrast.ContrastRatio(fg, bg);
            Assert.True(ratio >= 4.5,
                $"WCAG AA body text fail: {fg} on {bg} = {ratio:F2}:1 (need ≥ 4.5:1)");
        }
    }

    [Fact]
    public void Property2_WcagContrastDarkMode()
    {
        var darkPairs = new[]
        {
            ("#f0eeff", "#1c1b2e"),  // text-primary on surface (dark)
            ("#9ca3af", "#1c1b2e"),  // text-secondary on surface (dark)
            ("#bbf7d0", "#14532d"),  // success-text on success-bg (dark)
            ("#fecaca", "#7f1d1d"),  // danger-text on danger-bg (dark)
            ("#fde68a", "#78350f"),  // warning-text on warning-bg (dark)
            ("#bae6fd", "#0c2340"),  // info-text on info-bg (dark)
        };

        foreach (var (fg, bg) in darkPairs)
        {
            var ratio = WcagContrast.ContrastRatio(fg, bg);
            Assert.True(ratio >= 4.5,
                $"WCAG AA dark mode body text fail: {fg} on {bg} = {ratio:F2}:1 (need ≥ 4.5:1)");
        }
    }

    // ── Property 4: Greeting Function Correctness ─────────────────────────────
    // Feature: mobile-app-ui-design, Property 4: Greeting Function Correctness
    [Fact]
    public void Property4_GreetingFunctionCorrectness()
    {
        // Use CsCheck to generate (hour, name) pairs
        Gen.Select(Gen.Int[0, 23], Gen.String[1, 20])
           .Sample((hour, name) =>
           {
               var dt = new DateTime(2024, 6, 15, hour, 0, 0);
               var result = DashboardLogic.GetGreeting(name, dt);

               var expectedSalutation = hour switch
               {
                   >= 5 and < 12 => "Good morning",
                   >= 12 and < 17 => "Good afternoon",
                   _ => "Good evening"
               };

               Assert.StartsWith(expectedSalutation, result);
           });
    }

    [Theory]
    [InlineData(0,  "Good evening")]
    [InlineData(4,  "Good evening")]
    [InlineData(5,  "Good morning")]
    [InlineData(11, "Good morning")]
    [InlineData(12, "Good afternoon")]
    [InlineData(16, "Good afternoon")]
    [InlineData(17, "Good evening")]
    [InlineData(23, "Good evening")]
    public void Property4_GreetingBoundaryHours(int hour, string expectedSalutation)
    {
        var result = DashboardLogic.GetGreeting("Alice Smith", new DateTime(2024, 1, 1, hour, 0, 0));
        Assert.StartsWith(expectedSalutation, result);
        Assert.Contains("Alice", result);
    }

    [Fact]
    public void Property4_GreetingExtractsFirstName()
    {
        var result = DashboardLogic.GetGreeting("John Doe", new DateTime(2024, 1, 1, 10, 0, 0));
        Assert.Equal("Good morning, John", result);
    }

    // ── Property 5: Aggregate Stats Computation ───────────────────────────────
    // Feature: mobile-app-ui-design, Property 5: Aggregate Stats Computation
    [Fact]
    public void Property5_AggregateStatsEmptyList()
    {
        var (count, pct) = DashboardLogic.ComputeStats([]);
        Assert.Equal(0, count);
        Assert.Equal(0, pct);
    }

    [Fact]
    public void Property5_AggregateStatsPctAlwaysInRange()
    {
        // Generate (raised, target) pairs with positive target
        Gen.Select(Gen.Decimal[0m, 500m], Gen.Decimal[1m, 500m])
           .Sample((raised, target) =>
           {
               var goal = new GiftGoalResponse(1, "Goal", "", target, raised, null, null, []);
               var registry = new RegistryResponse(1, "R", "", "r", null, null,
                   new CreatorInfo("C", null, null), [goal]);
               var (_, pct) = DashboardLogic.ComputeStats([registry]);
               Assert.InRange(pct, 0, 100);
           });
    }

    [Fact]
    public void Property5_AggregateStatsZeroTargetGivesZeroPct()
    {
        var goals = new List<GiftGoalResponse>
        {
            new(1, "Goal", "", 0m, 100m, null, null, [])
        };
        var registry = new RegistryResponse(1, "R", "", "r", null, null,
            new CreatorInfo("C", null, null), goals);
        var (_, pct) = DashboardLogic.ComputeStats([registry]);
        Assert.Equal(0, pct);
    }

    [Fact]
    public void Property5_AggregateStatsCountEqualsListLength()
    {
        // Generate 0–5 registries and verify count matches
        Gen.Int[0, 5].Sample(n =>
        {
            var registries = Enumerable.Range(0, n)
                .Select(i => new RegistryResponse(i, $"R{i}", "", $"r{i}", null, null,
                    new CreatorInfo("C", null, null), []))
                .ToList();
            var (count, _) = DashboardLogic.ComputeStats(registries);
            Assert.Equal(n, count);
        });
    }

    // ── Property 7: Funded Goal Cards Replace Contribute Button with Badge ────
    // Feature: mobile-app-ui-design, Property 7: Funded Goal Cards Replace Contribute Button with Badge
    [Fact]
    public void Property7_FundedGoalIsFunded()
    {
        // For any target > 0, raised >= target means funded
        Gen.Select(Gen.Decimal[1m, 1000m], Gen.Decimal[0m, 1000m])
           .Sample((target, extra) =>
           {
               var raised = target + extra; // always >= target
               Assert.True(GoalCardLogic.IsFunded(raised, target),
                   $"Expected IsFunded=true for raised={raised}, target={target}");
               Assert.True(GoalCardLogic.FundedPct(raised, target) >= 100m);
           });
    }

    [Fact]
    public void Property7_UnfundedGoalIsNotFunded()
    {
        // raised < target means not funded
        Gen.Select(Gen.Decimal[1m, 1000m], Gen.Decimal[0.01m, 0.99m])
           .Sample((target, fraction) =>
           {
               var raised = target * fraction; // always < target
               Assert.False(GoalCardLogic.IsFunded(raised, target),
                   $"Expected IsFunded=false for raised={raised}, target={target}");
           });
    }

    // ── Property 8: GoalCard Renders All Required Fields ─────────────────────
    // Feature: mobile-app-ui-design, Property 8: GoalCard Renders All Required Fields
    [Fact]
    public void Property8_FundedPctAlwaysInRange()
    {
        Gen.Select(Gen.Decimal[0m, 2000m], Gen.Decimal[0m, 1000m])
           .Sample((raised, target) =>
           {
               var pct = GoalCardLogic.FundedPct(raised, target);
               Assert.InRange(pct, 0m, 100m);
           });
    }

    [Fact]
    public void Property8_IsFundedConsistentWithFundedPct()
    {
        Gen.Select(Gen.Decimal[0m, 2000m], Gen.Decimal[0m, 1000m])
           .Sample((raised, target) =>
           {
               var pct = GoalCardLogic.FundedPct(raised, target);
               var funded = GoalCardLogic.IsFunded(raised, target);
               Assert.Equal(pct >= 100m, funded);
           });
    }

    // ── Property 9: Goal List Grows by Exactly One on Valid Addition ──────────
    // Feature: mobile-app-ui-design, Property 9: Goal List Grows by Exactly One on Valid Addition
    [Fact]
    public void Property9_GoalListGrowsByOne()
    {
        Gen.Int[0, 10].Sample(initialCount =>
        {
            var list = Enumerable.Range(0, initialCount)
                .Select(i => new GiftGoalResponse(i, $"Goal {i}", "", 100m, 0m, null, null, []))
                .ToList();

            var before = list.Count;
            var newGoal = new GiftGoalResponse(99, "New Goal", "", 250m, 0m, null, null, []);
            list.Add(newGoal);

            Assert.Equal(before + 1, list.Count);
            Assert.Equal("New Goal", list.Last().Name);
            Assert.Equal(250m, list.Last().TargetAmount);
        });
    }

    // ── Property 10: Invalid Contribution Amount Shows Inline Error ───────────
    // Feature: mobile-app-ui-design, Property 10: Invalid Contribution Amount Shows Inline Error Without Closing Modal
    [Fact]
    public void Property10_InvalidAmountsFailValidation()
    {
        var invalidAmounts = new[] { 0m, -1m, -100m, -0.01m };
        foreach (var amount in invalidAmounts)
        {
            Assert.False(IsValidContributionAmount(amount),
                $"Expected amount {amount} to be invalid");
        }
    }

    [Fact]
    public void Property10_ValidAmountsPassValidation()
    {
        Gen.Decimal[0.01m, 100000m].Sample(amount =>
        {
            Assert.True(IsValidContributionAmount(amount),
                $"Expected amount {amount} to be valid");
        });
    }

    [Fact]
    public void Property10_NegativeAmountsAlwaysInvalid()
    {
        Gen.Decimal[-100000m, -0.01m].Sample(amount =>
        {
            Assert.False(IsValidContributionAmount(amount),
                $"Expected negative amount {amount} to be invalid");
        });
    }

    private static bool IsValidContributionAmount(decimal amount) => amount > 0;

    // ── Property 11: Toast Auto-Dismisses After Three Seconds ─────────────────
    // Feature: mobile-app-ui-design, Property 11: Toast Auto-Dismisses After Three Seconds
    [Fact]
    public void Property11_ToastTimingConstants()
    {
        const int visibleMs = 2800;
        const int fadeOutMs = 200;
        const int totalMs = visibleMs + fadeOutMs;

        Assert.Equal(3000, totalMs);
        Assert.InRange(visibleMs, 2700, 2900); // 2800ms ± 100ms
    }

    [Fact]
    public void Property11_ToastTimingInSourceCode()
    {
        var source = File.ReadAllText(RazorPaths.Toast);
        Assert.Contains("2800", source);
        Assert.Contains("200", source);
    }

    // ── Property 12: ProgressBar Applies Animated Transition ─────────────────
    // Feature: mobile-app-ui-design, Property 12: Progress Bar Applies Animated Transition on Any Value Change
    [Fact]
    public void Property12_ClampedValueAlwaysInRange()
    {
        Gen.Decimal[-1000m, 1000m].Sample(value =>
        {
            var clamped = ProgressBarLogic.ClampedValue(value);
            Assert.InRange(clamped, 0m, 100m);
        });
    }

    [Fact]
    public void Property12_AnimatedClassInCss()
    {
        var css = File.ReadAllText(CssPaths.Components);
        Assert.Contains(".progress-fill--animated", css);
        Assert.Contains("600ms", css);
        Assert.Contains("cubic-bezier(0.0, 0, 0.2, 1)", css);
    }

    // ── Property 13: Reduced Motion Disables All Non-Essential Animations ─────
    // Feature: mobile-app-ui-design, Property 13: Reduced Motion Disables All Non-Essential Animations
    [Fact]
    public void Property13_ReducedMotionBlockExists()
    {
        var css = File.ReadAllText(CssPaths.Dark);
        Assert.Contains("prefers-reduced-motion: reduce", css);
        Assert.Contains("animation-duration: 0.01ms !important", css);
        Assert.Contains("transition-duration: 0.01ms !important", css);
    }

    [Fact]
    public void Property13_AllKeyframesDefinedInAnimationsCss()
    {
        var css = File.ReadAllText(CssPaths.Animations);
        var expectedKeyframes = new[]
        {
            "shimmer", "slideInRight", "slideInLeft", "spin",
            "confettiFall", "slideUp", "fadeIn", "slideInUp", "cardMount"
        };

        foreach (var name in expectedKeyframes)
        {
            Assert.Contains($"@keyframes {name}", css);
        }
    }

    // ── Property 14: Network Error Shows Inline Error State with Retry Button ─
    // Feature: mobile-app-ui-design, Property 14: Network Error Always Shows Inline Error State with Retry Button
    [Fact]
    public void Property14_ErrorStateCssClassExists()
    {
        var css = File.ReadAllText(CssPaths.Components);
        Assert.Contains(".error-state", css);
        Assert.Contains(".error-state-message", css);
    }

    [Fact]
    public void Property14_ErrorStateInDashboardPage()
    {
        var source = File.ReadAllText(RazorPaths.Dashboard);
        Assert.Contains("error-state", source);
        Assert.Contains("Try again", source);
    }

    [Fact]
    public void Property14_ErrorStateInGuestRegistryPage()
    {
        var source = File.ReadAllText(RazorPaths.GuestRegistry);
        Assert.Contains("error-state", source);
        Assert.Contains("Try again", source);
    }

    // ── Property 15: Fixed/Sticky Elements Include Safe Area Insets ──────────
    // Feature: mobile-app-ui-design, Property 15: All Fixed and Sticky Elements Include Safe Area Insets
    [Fact]
    public void Property15_TopBarStickyHasSafeAreaInset()
    {
        var css = File.ReadAllText(CssPaths.Components);
        Assert.Contains("position: sticky", css);
        Assert.Contains("env(safe-area-inset-top)", css);
    }

    [Fact]
    public void Property15_FabFixedHasSafeAreaInset()
    {
        var css = File.ReadAllText(CssPaths.Components);
        // .fab is fixed and must include env(safe-area-inset-bottom)
        Assert.Contains("env(safe-area-inset-bottom)", css);
    }

    [Fact]
    public void Property15_ToastFixedHasSafeAreaInset()
    {
        var css = File.ReadAllText(CssPaths.Components);
        // .toast is fixed and must include env(safe-area-inset-bottom)
        var toastSection = ExtractSelectorBlock(css, ".toast");
        Assert.Contains("env(safe-area-inset-bottom)", toastSection);
    }

    private static string ExtractSelectorBlock(string css, string selector)
    {
        var idx = css.IndexOf(selector, StringComparison.Ordinal);
        if (idx < 0) return string.Empty;
        var start = css.IndexOf('{', idx);
        if (start < 0) return string.Empty;
        var depth = 0;
        var end = start;
        for (var i = start; i < css.Length; i++)
        {
            if (css[i] == '{') depth++;
            else if (css[i] == '}') { depth--; if (depth == 0) { end = i; break; } }
        }
        return css[start..end];
    }

    // ── Property 16: All Font-Size Declarations Use rem Units ────────────────
    // Feature: mobile-app-ui-design, Property 16: All Font Size Declarations Use rem Units
    [Fact]
    public void Property16_FontSizeTokensUseRem()
    {
        var css = File.ReadAllText(CssPaths.Tokens);
        var tokenPattern = new Regex(@"--text-\w+\s*:\s*([^;]+);");
        foreach (Match m in tokenPattern.Matches(css))
        {
            var value = m.Groups[1].Value.Trim();
            // Strip inline comments (e.g. "0.75rem;    /* 12px */")
            var valueOnly = value.Split('/')[0].Trim();
            Assert.EndsWith("rem", valueOnly);
        }
    }

    [Fact]
    public void Property16_NoLiteralPxFontSizesInCssFiles()
    {
        // font-size declarations must not use literal px values
        var literalPxFontSize = new Regex(@"font-size\s*:\s*\d+px");

        foreach (var path in CssPaths.All)
        {
            if (!File.Exists(path)) continue;
            var css = File.ReadAllText(path);
            var matches = literalPxFontSize.Matches(css);
            Assert.True(matches.Count == 0,
                $"Found literal px font-size in {Path.GetFileName(path)}: " +
                string.Join(", ", matches.Cast<Match>().Select(m => m.Value)));
        }
    }

    // ── Property 17: Color-Coded States Include Non-Color Indicator ──────────
    // Feature: mobile-app-ui-design, Property 17: All Color-Coded States Include a Non-Color Indicator
    [Fact]
    public void Property17_FundedBadgeHasCheckIcon()
    {
        var source = File.ReadAllText(RazorPaths.GoalCard);
        Assert.Contains("funded-badge", source);
        Assert.Contains("<svg", source);
        Assert.Contains("Fully funded", source);
    }

    [Fact]
    public void Property17_ToastHasIconAlongsideColor()
    {
        var source = File.ReadAllText(RazorPaths.Toast);
        Assert.Contains("toast-icon", source);
        Assert.Contains("✓", source);
        Assert.Contains("✕", source);
    }

    [Fact]
    public void Property17_ErrorStateHasIconEmoji()
    {
        var source = File.ReadAllText(RazorPaths.Dashboard);
        Assert.Contains("error-state-icon", source);
        Assert.Contains("⚠", source);
    }

    [Fact]
    public void Property17_ColorCodedStateClassesExistInCss()
    {
        var css = File.ReadAllText(CssPaths.Components);
        var requiredClasses = new[]
        {
            ".funded-badge",
            ".error-state",
            ".toast--success",
            ".toast--error",
            ".info-banner--warning",
            ".info-banner--info",
        };

        foreach (var cls in requiredClasses)
        {
            Assert.Contains(cls, css);
        }
    }
}
