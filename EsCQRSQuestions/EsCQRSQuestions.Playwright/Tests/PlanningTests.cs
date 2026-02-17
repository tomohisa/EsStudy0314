using EsCQRSQuestions.Playwright.Base;
using Microsoft.Playwright;

namespace EsCQRSQuestions.Playwright.Tests;

[TestFixture]
public class PlanningTests : BaseTest
{
    private const string AdminPlanningUrl = "http://localhost:5260/planning";

    [SetUp]
    public async Task TestSetUp()
    {
        await Page!.GotoAsync(AdminPlanningUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Question Management" })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 20000 });
    }

    [Test]
    public async Task CreateGroup_ShouldCompleteWithoutErrorAlert()
    {
        string? alertMessage = null;
        Page!.Dialog += async (_, dialog) =>
        {
            alertMessage = dialog.Message;
            await dialog.AcceptAsync();
        };

        var groupName = $"pw-group-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        await Page.GetByRole(AriaRole.Button, new() { Name = "Create New Group" }).ClickAsync();
        await Page.Locator("#groupModal").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        await Page.Locator("#groupName").FillAsync(groupName);
        await SaveGroupWithRetry("#groupModal", () => alertMessage, () => alertMessage = null);

        var createdGroupButton = await WaitForGroupButton(groupName);
        Assert.That(await createdGroupButton.IsVisibleAsync(), Is.True, $"Created group '{groupName}' was not found in the list.");
    }

    private async Task SaveGroupWithRetry(string modalSelector, Func<string?> getAlert, Action clearAlert)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await Page!.Locator(modalSelector).GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
            var saveResult = await WaitForModalCloseOrAlert(modalSelector, () => getAlert() is not null);

            var alert = getAlert();
            if (string.IsNullOrEmpty(alert))
            {
                if (saveResult.ModalClosed)
                {
                    return;
                }

                if (attempt == 3)
                {
                    Assert.Fail("Save operation timed out; group modal did not close.");
                }

                await Task.Delay(1500);
                continue;
            }

            if (!IsTransientServerError(alert) || attempt == 3)
            {
                Assert.Fail($"Unexpected alert appeared: {alert}");
            }

            clearAlert();
            await Task.Delay(3000);
        }
    }

    private async Task<(bool ModalClosed, bool AlertRaised)> WaitForModalCloseOrAlert(
        string modalSelector,
        Func<bool> isAlertRaised)
    {
        for (var i = 0; i < 80; i++)
        {
            if (isAlertRaised())
            {
                return (false, true);
            }

            var isModalVisible = await Page!.Locator(modalSelector).IsVisibleAsync();
            if (!isModalVisible)
            {
                return (true, false);
            }

            await Task.Delay(250);
        }

        return (false, false);
    }

    private async Task<ILocator> WaitForGroupButton(string groupName)
    {
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            var button = Page!.GetByRole(AriaRole.Button, new() { Name = groupName }).First;
            if (await button.CountAsync() > 0)
            {
                return button;
            }

            await Page.ReloadAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(1500);
        }

        Assert.Fail($"Created group '{groupName}' was not visible after retries.");
        return Page!.GetByRole(AriaRole.Button, new() { Name = groupName }).First;
    }

    private static bool IsTransientServerError(string message) =>
        message.Contains("DbUpdateException", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("InternalServerError", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("\"status\":500", StringComparison.OrdinalIgnoreCase);
}
