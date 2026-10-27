using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Services.Notifications;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class NotificationsControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<(HttpClient Client, Account Account)> CreateAuthedClientAsync()
    {
        HttpClient client = fixture.CreateTestClient();
        Account caller;
        await using (AppDbContext context = CreateDbContext())
        {
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"caller-{Guid.NewGuid():N}", email: $"caller-{Guid.NewGuid():N}@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller, null));
        return (client, caller);
    }

    [Fact]
    public async Task GetAll_NotAuthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_NoNotifications_ReturnsEmptyPagedResult()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PagedResult<NotificationDto>? result = await response.Content.ReadJsonAsync<PagedResult<NotificationDto>>();
        Assert.Empty(result!.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Theory]
    [InlineData("", 2)]
    [InlineData("?readState=Unread", 1)]
    [InlineData("?readState=read", 1)]
    public async Task GetAll_FiltersByReadState(string query, int expected)
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        await using (AppDbContext context = CreateDbContext())
        {
            Notification read = Notification.Create(new NotificationParams(caller.Id, NotificationTypes.OrganizationMemberAdded));
            read.MarkRead();
            context.Notifications.AddRange(
                read, Notification.Create(new NotificationParams(caller.Id, NotificationTypes.OrganizationMemberAdded)));
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.GetAsync($"/api/notifications{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PagedResult<NotificationDto>? result = await response.Content.ReadJsonAsync<PagedResult<NotificationDto>>();
        Assert.Equal(expected, result!.TotalCount);
    }

    [Fact]
    public async Task GetAll_UnknownReadState_ReturnsBadRequest()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.GetAsync("/api/notifications?readState=archived");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MarkAsRead_OwnedNotification_MarksReadAndDropsFromUnreadCount()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Guid notificationId;
        await using (AppDbContext context = CreateDbContext())
        {
            Notification notification = Notification.Create(
                new NotificationParams(caller.Id, NotificationTypes.OrganizationMemberAdded));
            context.Notifications.Add(notification);
            await context.SaveChangesAsync();
            notificationId = notification.Id;
        }

        HttpResponseMessage response = await client.PatchAsync($"/api/notifications/{notificationId}/read", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        HttpResponseMessage countResponse = await client.GetAsync("/api/notifications/unread-count");
        UnreadCountDto? count = await countResponse.Content.ReadJsonAsync<UnreadCountDto>();
        Assert.Equal(0, count!.Count);
    }

    [Fact]
    public async Task MarkAsRead_NotOwnedByCaller_ReturnsNotFound()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        Guid notificationId;
        await using (AppDbContext context = CreateDbContext())
        {
            Account other = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"other-{Guid.NewGuid():N}", email: $"other-{Guid.NewGuid():N}@example.com");
            Notification notification = Notification.Create(
                new NotificationParams(other.Id, NotificationTypes.OrganizationMemberAdded));
            context.Notifications.Add(notification);
            await context.SaveChangesAsync();
            notificationId = notification.Id;
        }

        HttpResponseMessage response = await client.PatchAsync($"/api/notifications/{notificationId}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MarkAllAsRead_MarksEveryUnreadNotificationForCaller()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        await using (AppDbContext context = CreateDbContext())
        {
            context.Notifications.Add(Notification.Create(
                new NotificationParams(caller.Id, NotificationTypes.OrganizationMemberAdded)));
            context.Notifications.Add(Notification.Create(
                new NotificationParams(caller.Id, NotificationTypes.OrganizationMemberAdded)));
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.PatchAsync("/api/notifications/read-all", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        HttpResponseMessage countResponse = await client.GetAsync("/api/notifications/unread-count");
        UnreadCountDto? count = await countResponse.Content.ReadJsonAsync<UnreadCountDto>();
        Assert.Equal(0, count!.Count);
    }
}
