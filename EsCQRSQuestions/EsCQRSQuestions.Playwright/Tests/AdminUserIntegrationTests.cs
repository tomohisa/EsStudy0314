using EsCQRSQuestions.Playwright.Base;
using Microsoft.Playwright;
using System.Net.Http.Json;
using System.Text.Json;
using System.Linq;

namespace EsCQRSQuestions.Playwright.Tests;

[TestFixture]
public class AdminUserIntegrationTests : BaseTest
{
    [Test]
    public async Task AdminAndUserFlow_ShouldWorkEndToEnd()
    {
        var adminPage = Page!;
        string? adminAlert = null;
        adminPage.Dialog += async (_, dialog) =>
        {
            adminAlert = dialog.Message;
            await dialog.AcceptAsync();
        };

        var runId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var groupName = $"e2e-group-{runId}";
        var questionText = $"e2e-question-{runId}";
        var optionA = "E2E Option A";
        var optionB = "E2E Option B";
        var participantName = $"tester-{runId}";
        var comment = $"e2e-comment-{runId}";

        await adminPage.GotoAsync($"{AdminBaseUrl}/planning");
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await adminPage.GetByRole(AriaRole.Heading, new() { Name = "Question Management" })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        await adminPage.GetByRole(AriaRole.Button, new() { Name = "Create New Group" }).ClickAsync();
        await adminPage.Locator("#groupModal").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await adminPage.Locator("#groupName").FillAsync(groupName);
        await SaveWithRetry(
            adminPage,
            "#groupModal",
            () => adminAlert,
            () => adminAlert = null,
            "creating group");

        var groupButton = await WaitForGroupButton(adminPage, groupName);
        await groupButton.ClickAsync();

        var addQuestionButton = adminPage.GetByRole(AriaRole.Button, new() { Name = "Add Question" });
        await addQuestionButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 20000 });

        var openLink = adminPage.GetByRole(AriaRole.Link, new() { Name = "Open Link" });
        await openLink.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        var href = await openLink.GetAttributeAsync("href");
        Assert.That(href, Is.Not.Null.And.Not.Empty, "Open Link href was empty.");
        var uniqueCode = href!.TrimEnd('/').Split('/').Last();
        Assert.That(uniqueCode, Is.Not.Empty, "Could not parse survey unique code from Open Link.");

        await addQuestionButton.ClickAsync();
        await adminPage.Locator("#questionModal").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await adminPage.Locator("#questionText").FillAsync(questionText);

        var optionInputs = adminPage.Locator("#questionModal input[placeholder='Option text']");
        await optionInputs.Nth(0).FillAsync(optionA);
        await optionInputs.Nth(1).FillAsync(optionB);
        await SaveWithRetry(
            adminPage,
            "#questionModal",
            () => adminAlert,
            () => adminAlert = null,
            "creating question");

        var questionRow = adminPage.Locator("tr", new PageLocatorOptions { HasText = questionText }).First;
        await questionRow.WaitForAsync(new LocatorWaitForOptions { Timeout = 20000 });
        await questionRow.GetByRole(AriaRole.Button, new() { Name = "Start Display" }).ClickAsync();
        Assert.That(adminAlert, Is.Null, $"Unexpected admin alert while starting display: {adminAlert}");

        var userPage = await Context!.NewPageAsync();
        await userPage.GotoAsync($"{UserBaseUrl}/questionair/{uniqueCode}");
        await userPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var observerPage = await Context.NewPageAsync();
        await observerPage.GotoAsync($"{UserBaseUrl}/questionair/{uniqueCode}");
        await observerPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await userPage.GetByText(questionText).WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await observerPage.GetByText(questionText).WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        var nameInput = userPage.Locator("#participantName");
        if (await nameInput.CountAsync() == 0)
        {
            var editNameButton = userPage.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("^(Change|Set name)$") });
            await editNameButton.First.ClickAsync();
            nameInput = userPage.Locator("#participantName");
        }

        await nameInput.FillAsync(participantName);
        await userPage.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await userPage.Locator(".option-row").Filter(new LocatorFilterOptions { HasText = optionA })
            .GetByRole(AriaRole.Button, new() { Name = "Submit Choice" })
            .ClickAsync();
        await userPage.Locator(".option-row").Filter(new LocatorFilterOptions { HasText = optionA })
            .GetByRole(AriaRole.Button, new() { Name = "Chosen" })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await observerPage.GetByRole(AriaRole.Heading, new() { Name = "Response Statistics" })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await WaitForAdminResponseCount(adminPage, questionText, expectedCount: 1);

        await userPage.Locator("#comment").FillAsync(comment);
        var submitCommentButton = userPage.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("^(Post Comment|Submit Comment)$") });
        await submitCommentButton.First.ClickAsync();
        await EnsureCommentSubmissionSucceeded(userPage);
        await WaitForApiComment(uniqueCode, comment);

        await WaitForAdminResponse(adminPage, questionRow, questionText, comment);
    }

    private static async Task<(bool ModalClosed, bool AlertRaised)> WaitForModalCloseOrAlert(
        IPage page,
        string modalSelector,
        Func<bool> isAlertRaised)
    {
        for (var i = 0; i < 80; i++)
        {
            if (isAlertRaised())
            {
                return (false, true);
            }

            var isModalVisible = await page.Locator(modalSelector).IsVisibleAsync();
            if (!isModalVisible)
            {
                return (true, false);
            }

            await Task.Delay(250);
        }

        return (false, false);
    }

    private static async Task SaveWithRetry(
        IPage page,
        string modalSelector,
        Func<string?> getAlert,
        Action clearAlert,
        string operationName)
    {
        const int maxAttempts = 10;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await page.Locator(modalSelector).GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
            var saveResult = await WaitForModalCloseOrAlert(page, modalSelector, () => getAlert() is not null);

            var alert = getAlert();
            if (string.IsNullOrEmpty(alert))
            {
                if (saveResult.ModalClosed)
                {
                    return;
                }

                if (attempt == maxAttempts)
                {
                    Assert.Fail($"Save operation timed out while {operationName}; modal did not close.");
                }

                await Task.Delay(1500 + (attempt * 500));
                continue;
            }

            if (!IsTransientServerError(alert) || attempt == maxAttempts)
            {
                Assert.Fail($"Unexpected admin alert while {operationName}: {alert}");
            }

            clearAlert();
            await Task.Delay(2000 * attempt);
        }
    }

    private static async Task<ILocator> WaitForGroupButton(IPage adminPage, string groupName)
    {
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            var button = adminPage.GetByRole(AriaRole.Button, new() { Name = groupName }).First;
            if (await button.CountAsync() > 0)
            {
                return button;
            }

            await adminPage.ReloadAsync();
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(1500);
        }

        Assert.Fail($"Created group '{groupName}' was not visible after retries.");
        return adminPage.GetByRole(AriaRole.Button, new() { Name = groupName }).First;
    }

    private static async Task WaitForAdminResponse(
        IPage adminPage,
        ILocator questionRow,
        string questionText,
        string comment)
    {
        for (var attempt = 1; attempt <= 30; attempt++)
        {
            await questionRow.GetByRole(AriaRole.Button, new() { Name = "View" }).ClickAsync();
            await adminPage.GetByRole(AriaRole.Heading, new() { Name = $"Question Details: {questionText}" })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

            var commentVisible = await adminPage.GetByText(comment).First.IsVisibleAsync();
            var responseRows = await adminPage.Locator("h4:has-text('Responses')").Locator("xpath=following::tbody/tr").CountAsync();
            if (commentVisible && responseRows > 0)
            {
                return;
            }

            await Task.Delay(2000);
        }

        var bodyText = await adminPage.Locator("body").InnerTextAsync();
        var snippet = bodyText.Length > 1200 ? bodyText[..1200] : bodyText;
        Assert.Fail($"Admin page did not show participant/comment response within timeout. Page snippet: {snippet}");
    }

    private static async Task EnsureCommentSubmissionSucceeded(IPage userPage)
    {
        for (var attempt = 1; attempt <= 15; attempt++)
        {
            var successVisible =
                await userPage.GetByText("Comment posted.").First.IsVisibleAsync()
                || await userPage.GetByText("コメントを送信しました。").First.IsVisibleAsync();
            if (successVisible)
            {
                return;
            }

            var knownError = await TryReadKnownCommentError(userPage);
            if (!string.IsNullOrEmpty(knownError))
            {
                Assert.Fail($"Comment submission failed on user page: {knownError}");
            }

            await Task.Delay(1000);
        }

        var fallbackError = await TryReadKnownCommentError(userPage);
        if (!string.IsNullOrEmpty(fallbackError))
        {
            Assert.Fail($"Comment submission failed on user page: {fallbackError}");
        }
    }

    private static async Task<string?> TryReadKnownCommentError(IPage userPage)
    {
        var patterns = new[]
        {
            "Error sending comment:",
            "Please register an option first, then send comment.",
            "Please enter a comment before sending.",
            "コメント送信エラー:",
            "先に選択肢を登録してからコメントを送信してください。",
            "コメントを入力してから送信してください。"
        };

        foreach (var pattern in patterns)
        {
            var locator = userPage.GetByText(pattern).First;
            if (await locator.IsVisibleAsync())
            {
                return (await locator.InnerTextAsync()).Trim();
            }
        }

        return null;
    }

    private static async Task WaitForApiComment(string uniqueCode, string comment)
    {
        using var http = new HttpClient();
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            var response = await http.GetAsync($"http://localhost:5349/api/questions/active/{uniqueCode}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("responses", out var responsesElement) &&
                    responsesElement.ValueKind == JsonValueKind.Array)
                {
                    var comments = responsesElement.EnumerateArray()
                        .Select(r => r.TryGetProperty("comment", out var c) ? c.GetString() : null)
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .ToList();
                    if (comments.Any(c => string.Equals(c, comment, StringComparison.Ordinal)))
                    {
                        return;
                    }
                }
            }

            await Task.Delay(1000);
        }

        Assert.Fail("API did not reflect submitted comment within timeout.");
    }

    private static async Task WaitForAdminResponseCount(IPage adminPage, string questionText, int expectedCount)
    {
        for (var attempt = 1; attempt <= 15; attempt++)
        {
            var row = adminPage.Locator("tr", new PageLocatorOptions { HasText = questionText }).First;
            var responseCell = row.Locator("td").Nth(4);
            var cellText = (await responseCell.InnerTextAsync()).Trim();
            if (cellText.StartsWith($"{expectedCount} ", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(1000);
        }

        Assert.Fail($"Admin response count did not update to {expectedCount} without opening details.");
    }

    private static bool IsTransientServerError(string message) =>
        message.Contains("DbUpdateException", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("InternalServerError", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("\"status\":500", StringComparison.OrdinalIgnoreCase);
}
