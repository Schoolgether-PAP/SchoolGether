using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Support;

namespace SchoolGether.Api.IntegrationTests;

public sealed class SupportAttachmentTests
{
    internal static MultipartFormDataContent Multipart(object request, params (string Name, byte[] Bytes)[] files)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"), "request");
        foreach (var file in files) content.Add(new ByteArrayContent(file.Bytes), "files", file.Name);
        return content;
    }

    [Fact]
    public async Task MultipartCreation_PrivateDownload_HasSafeHeadersAndNoStorageKey()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        using var body = Multipart(SupportAuthorizationTests.Create(), ("relatório.txt", "Olá"u8.ToArray()));
        using var response = await client.PostAsync(SupportAuthorizationTests.Route, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ticket = await response.Content.ReadFromJsonAsync<JsonElement>();
        var messages = await client.GetFromJsonAsync<JsonElement>($"{SupportAuthorizationTests.Route}/{ticket.GetProperty("id")}/messages");
        var attachment = messages.GetProperty("items")[0].GetProperty("attachments")[0];
        Assert.Equal(4, attachment.GetProperty("size").GetInt64());
        Assert.False(attachment.TryGetProperty("storageKey", out _));
        var route = "/api/v1/support/attachments/" + attachment.GetProperty("id");
        using var download = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("Olá", await download.Content.ReadAsStringAsync());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.True(download.Headers.CacheControl?.NoStore);
        Assert.True(download.Headers.CacheControl?.Private);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        await PlatformAdminTests.Authenticate(client, "admin-b@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode);
        await PlatformAdminTests.Authenticate(client, "student-a@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);
    }

    [Fact]
    public async Task MultipartUnknownFields_AreRejectedWithZeroFiles()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        using var body = Multipart(new { ClientId = Guid.NewGuid(), Subject = "Pedido", Category = "Technical", Priority = "Normal", Description = "Ajuda", InstitutionId = 2 });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(SupportAuthorizationTests.Route, body)).StatusCode);
    }

    [Fact]
    public async Task FourAttachments_AreRejectedWithoutPersistingTicket()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        using var body = Multipart(SupportAuthorizationTests.Create(), Enumerable.Range(0, 4).Select(x => ($"{x}.txt", "ok"u8.ToArray())).ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(SupportAuthorizationTests.Route, body)).StatusCode);
        var page = await client.GetFromJsonAsync<JsonElement>(SupportAuthorizationTests.Route);
        Assert.Equal(0, page.GetProperty("total").GetInt32());
    }
    [Fact]
    public async Task Download_RechecksInternalVisibilityColleaguesAndSuspension_AndHandlesMissingFiles()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/support/attachments/1")).StatusCode);
        await PlatformAdminTests.Authenticate(client, "admin-a@example.test");
        (await client.PostAsJsonAsync("/api/v1/identity/users", new SchoolGether.Contracts.Identity.CreateUserRequest(
            "colleague@example.test", IdentityFactory.Password, "Colega", "Teacher", null))).EnsureSuccessStatusCode();
        await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        using var body = Multipart(SupportAuthorizationTests.Create(), ("public.txt", "public"u8.ToArray()));
        var response = await client.PostAsync(SupportAuthorizationTests.Route, body); response.EnsureSuccessStatusCode();
        var ticket = await response.Content.ReadFromJsonAsync<JsonElement>();
        var messagesRoute = $"{SupportAuthorizationTests.Route}/{ticket.GetProperty("id")}/messages";
        var messages = await client.GetFromJsonAsync<JsonElement>(messagesRoute);
        var publicId = messages.GetProperty("items")[0].GetProperty("attachments")[0].GetProperty("id").GetInt32();
        await PlatformAdminTests.Authenticate(client);
        using var note = Multipart(new { ClientId = Guid.NewGuid(), RowVersion = ticket.GetProperty("rowVersion").GetInt64(), Text = "Nota privada", Internal = true }, ("internal.txt", "internal"u8.ToArray()));
        var noteResponse = await client.PostAsync(messagesRoute, note); noteResponse.EnsureSuccessStatusCode();
        var internalId = (await noteResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attachments")[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/support/attachments/{internalId}")).StatusCode);
        await PlatformAdminTests.Authenticate(client, "colleague@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/support/attachments/{publicId}")).StatusCode);
        foreach (var email in new[] { "teacher-a@example.test", "admin-a@example.test" })
        {
            await PlatformAdminTests.Authenticate(client, email);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/support/attachments/{internalId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/support/attachments/{publicId}")).StatusCode);
            Assert.Single((await client.GetFromJsonAsync<JsonElement>(messagesRoute)).GetProperty("items").EnumerateArray());
        }
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGether.Infrastructure.Persistence.SchoolGetherDbContext>();
        var attachment = await EntityFrameworkQueryableExtensions.SingleAsync(
            EntityFrameworkQueryableExtensions.IgnoreQueryFilters(db.SupportAttachments), x => x.Id == publicId);
        await factory.Services.GetRequiredService<IFileStorage>().DeleteAsync(attachment.StorageKey, default);
        using var missing = await client.GetAsync($"/api/v1/support/attachments/{publicId}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, missing.StatusCode);
        var error = await missing.Content.ReadAsStringAsync();
        Assert.DoesNotContain(attachment.StorageKey, error); Assert.DoesNotContain("SupportFileUnavailable", error);
        var institution = await EntityFrameworkQueryableExtensions.SingleAsync(db.Institutions, x => x.Id == factory.InstitutionA);
        institution.SetSuspension(true, "Teste de revogação"); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/support/attachments/{publicId}")).StatusCode);
    }

    [Fact]
    public async Task ThreeMaximumFiles_AreAccepted_ReplayDoesNotDuplicateQuota_AndChangedBytesConflict()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var bytes = Enumerable.Repeat((byte)'a', 5 * 1024 * 1024).ToArray(); var request = SupportAuthorizationTests.Create();
        async Task<HttpStatusCode> Send(byte[] first)
        {
            using var body = Multipart(request, ("a.txt", first), ("b.txt", bytes), ("c.txt", bytes));
            return (await client.PostAsync(SupportAuthorizationTests.Route, body)).StatusCode;
        }
        Assert.Equal(HttpStatusCode.Created, await Send(bytes));
        Assert.Equal(HttpStatusCode.OK, await Send(bytes));
        Assert.Equal(HttpStatusCode.Conflict, await Send("different"u8.ToArray()));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGether.Infrastructure.Persistence.SchoolGetherDbContext>();
        var usage = await EntityFrameworkQueryableExtensions.SingleAsync(
            EntityFrameworkQueryableExtensions.IgnoreQueryFilters(db.SupportStorageUsages));
        Assert.Equal(15 * 1024 * 1024, usage.UsedBytes);
        var root = factory.Services.GetRequiredService<SupportOptions>().FilesRoot;
        Assert.Equal(3, Directory.GetFiles(Path.Combine(root, "files")).Length);
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "temporary")));
    }

    [Fact]
    public async Task MultipartOverHttpLimit_IsRejectedBeforePersisting()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        using var body = Multipart(SupportAuthorizationTests.Create(), ("large.txt", new byte[16 * 1024 * 1024]));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync(SupportAuthorizationTests.Route, body)).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>(SupportAuthorizationTests.Route)).GetProperty("total").GetInt32());
    }}
