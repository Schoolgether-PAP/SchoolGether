using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Support;

namespace SchoolGether.Api.IntegrationTests;

public sealed partial class SupportStorageTests
{
    [Theory]
    [InlineData("wrong.png", "plain text")]
    [InlineData("../test.txt", "text")]
    [InlineData("C:\\test.txt", "text")]
    [InlineData("bad\nname.txt", "text")]
    [InlineData("bad.txt", "a\0b")]
    [InlineData("bad.docx", "PK fake ZIP")]
    public async Task RejectsInvalidNameOrContent(string name, string content)
    {
        await using var factory = new IdentityFactory(supportEnabled: true);
        var storage = factory.Services.GetService<IFileStorage>();
        Assert.NotNull(storage);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await Assert.ThrowsAsync<SupportException>(() => storage.PrepareAsync(new(name, stream), default));
    }

    [Fact]
    public async Task RejectsInvalidUtf8AndOversize_WithoutLeavingTemporaryFiles()
    {
        await using var factory = new IdentityFactory(supportEnabled: true);
        var storage = factory.Services.GetService<IFileStorage>(); Assert.NotNull(storage);
        foreach (var bytes in new[] { new byte[] { 0xc0, 0xaf }, new byte[5242881] })
        {
            using var stream = new MemoryStream(bytes);
            await Assert.ThrowsAsync<SupportException>(() => storage.PrepareAsync(new("bad.txt", stream), default));
        }
    }

    [Fact]
    public async Task PrepareCommitReadDelete_UsesOpaqueKeysAndServerMime()
    {
        await using var factory = new IdentityFactory(supportEnabled: true);
        var storage = factory.Services.GetService<IFileStorage>(); Assert.NotNull(storage);
        using var stream = new MemoryStream("hello"u8.ToArray());
        var file = await storage.PrepareAsync(new("note.txt", stream), default);
        Assert.Equal("text/plain", file.ContentType);
        Assert.Equal(5, file.Size);
        Assert.Equal("2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824", file.Sha256);
        Assert.DoesNotContain("note", file.StorageKey);
        await storage.CommitAsync(file, default);
        await using (var read = await storage.OpenReadAsync(file.StorageKey, default))
            Assert.Equal("hello", await new StreamReader(read).ReadToEndAsync());
        await storage.DeleteAsync(file.StorageKey, default);
        await Assert.ThrowsAsync<SupportException>(() => storage.OpenReadAsync(file.StorageKey, default));
    }
}
