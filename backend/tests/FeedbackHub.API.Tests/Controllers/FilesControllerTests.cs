using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Files;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class FilesControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<HttpClient> CreateAuthedClientAsync()
    {
        HttpClient client = fixture.CreateTestClient();
        Account caller;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: $"files-caller-{Guid.NewGuid():N}", email: $"files-caller-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller, organization.Id));
        return client;
    }

    private static MultipartFormDataContent CreateUploadContent(byte[] bytes, string fileName = "note.txt", string contentType = "text/plain")
    {
        MultipartFormDataContent content = new();
        ByteArrayContent fileContent = new(bytes);
        fileContent.Headers.ContentType = new(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Upload_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.PostAsync("/api/files", CreateUploadContent("hello"u8.ToArray()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_Valid_Returns201WithCorrectSize()
    {
        HttpClient client = await CreateAuthedClientAsync();
        byte[] bytes = "hello world"u8.ToArray();

        HttpResponseMessage response = await client.PostAsync("/api/files", CreateUploadContent(bytes));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        FileDto? dto = await response.Content.ReadJsonAsync<FileDto>();
        Assert.Equal(bytes.Length, dto!.Size);
    }

    [Fact]
    public async Task Upload_EmptyFile_ReturnsBadRequest()
    {
        HttpClient client = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.PostAsync("/api/files", CreateUploadContent([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ExceedsMaxFileSize_ReturnsBadRequest()
    {
        // StorageSettings:MaxFileSizeBytes is 10485760 (10 MB) in the test host's appsettings.json.
        HttpClient client = await CreateAuthedClientAsync();
        byte[] bytes = new byte[10_485_761];

        HttpResponseMessage response = await client.PostAsync("/api/files", CreateUploadContent(bytes));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        HttpClient client = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.GetAsync($"/api/files/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsPagedFiles()
    {
        HttpClient client = await CreateAuthedClientAsync();
        await client.PostAsync("/api/files", CreateUploadContent("file one"u8.ToArray(), "one.txt"));
        await client.PostAsync("/api/files", CreateUploadContent("file two"u8.ToArray(), "two.txt"));

        HttpResponseMessage response = await client.GetAsync("/api/files?pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PagedResult<FileDto>? result = await response.Content.ReadJsonAsync<PagedResult<FileDto>>();
        Assert.True(result!.TotalCount >= 2);
    }

    [Fact]
    public async Task Download_ReturnsUploadedBytes()
    {
        HttpClient client = await CreateAuthedClientAsync();
        byte[] bytes = "downloadable content"u8.ToArray();
        HttpResponseMessage uploadResponse = await client.PostAsync("/api/files", CreateUploadContent(bytes, "downloadable.txt"));
        FileDto? uploaded = await uploadResponse.Content.ReadJsonAsync<FileDto>();

        HttpResponseMessage response = await client.GetAsync($"/api/files/{uploaded!.Id}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        byte[] downloaded = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(bytes, downloaded);
    }

    [Fact]
    public async Task Delete_ThenGetById_ReturnsNotFound()
    {
        HttpClient client = await CreateAuthedClientAsync();
        HttpResponseMessage uploadResponse = await client.PostAsync("/api/files", CreateUploadContent("to be deleted"u8.ToArray(), "delete-me.txt"));
        FileDto? uploaded = await uploadResponse.Content.ReadJsonAsync<FileDto>();

        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/files/{uploaded!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        HttpResponseMessage getResponse = await client.GetAsync($"/api/files/{uploaded.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetById_BelongsToOtherOrganization_ReturnsNotFound()
    {
        HttpClient owner = await CreateAuthedClientAsync();
        HttpResponseMessage uploadResponse = await owner.PostAsync(
            "/api/files", CreateUploadContent("other org content"u8.ToArray(), "other-org.txt"));
        FileDto? uploaded = await uploadResponse.Content.ReadJsonAsync<FileDto>();

        HttpClient outsider = await CreateAuthedClientAsync();
        HttpResponseMessage response = await outsider.GetAsync($"/api/files/{uploaded!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ScopedToActiveOrganization_ExcludesOtherOrganizationsFiles()
    {
        HttpClient owner = await CreateAuthedClientAsync();
        await owner.PostAsync("/api/files", CreateUploadContent("other org content"u8.ToArray(), "other-org.txt"));

        HttpClient outsider = await CreateAuthedClientAsync();
        HttpResponseMessage response = await outsider.GetAsync("/api/files?pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PagedResult<FileDto>? result = await response.Content.ReadJsonAsync<PagedResult<FileDto>>();
        Assert.DoesNotContain(result!.Items, f => f.FileName == "other-org.txt");
    }

    // Organization gate

    [Theory]
    [InlineData("GET", "/api/files")]
    [InlineData("GET", "/api/files/00000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/files/00000000-0000-0000-0000-000000000001/download")]
    [InlineData("DELETE", "/api/files/00000000-0000-0000-0000-000000000001")]
    public async Task AnyEndpoint_Unauthenticated_ReturnsUnauthorized(string method, string path)
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // AC-298: the personal context has no organization to reach files in.
    [Theory]
    [InlineData("POST", "/api/files")]
    [InlineData("GET", "/api/files")]
    [InlineData("GET", "/api/files/00000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/files/00000000-0000-0000-0000-000000000001/download")]
    [InlineData("DELETE", "/api/files/00000000-0000-0000-0000-000000000001")]
    public async Task AnyEndpoint_PersonalContext_Returns403AccessDenied(string method, string path)
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(
            fixture, SystemRoleKind.Owner, personalContext: true);
        HttpRequestMessage request = new(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = CreateUploadContent("hello"u8.ToArray());
        }

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Delete_MemberWithoutFilesManage_Returns403AccessDenied()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(
            fixture, SystemRoleKind.Member);

        HttpResponseMessage response = await client.DeleteAsync($"/api/files/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, body.GetProperty("code").GetString());
    }

    // AC-301: the policy itself answers a suspended organization, before the middleware net.
    [Fact]
    public async Task GetAll_SuspendedOrganizationMember_Returns403SuspendedWithName()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(
            fixture, SystemRoleKind.Owner, organizationSuspended: true);

        HttpResponseMessage response = await client.GetAsync("/api/files");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(403, body.GetProperty("status").GetInt32());
        Assert.Equal(ApplicationMessages.OrganizationSuspended, body.GetProperty("code").GetString());
        Assert.Equal(AuthTestHelper.GateOrganizationName, body.GetProperty("organizationName").GetString());
    }
}
