using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Support;
using SchoolGether.Infrastructure.Persistence;
using SchoolGether.Infrastructure.Support;

namespace SchoolGether.Api.IntegrationTests;

public sealed partial class SupportStorageTests
{
    [Theory]
    [InlineData("docx")]
    [InlineData("xlsx")]
    [InlineData("pptx")]
    public async Task AcceptsBoundedOpenXmlPackages(string extension)
    {
        await using var factory = new IdentityFactory(supportEnabled: true);
        var storage = factory.Services.GetRequiredService<IFileStorage>();
        using var input = new MemoryStream(Office(extension));
        var prepared = await storage.PrepareAsync(new("document." + extension, input), default);
        Assert.StartsWith("application/vnd.openxmlformats-officedocument.", prepared.ContentType);
        await storage.DeleteAsync(prepared.StorageKey, default);
    }
    [Theory]
    [InlineData("override")]
    [InlineData("default")]
    public async Task AcceptsMacroFreeOfficePrinterSettings_WithDeclaredTypeAndRelationship(string declaration)
    {
        await using var factory = new IdentityFactory(supportEnabled: true);
        var storage = factory.Services.GetRequiredService<IFileStorage>();
        var bytes = OfficeWithPrinterSettings(declaration);
        using var input = new MemoryStream(bytes);
        var prepared = await storage.PrepareAsync(new("print-layout.docx", input), default);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", prepared.ContentType);
        await storage.CommitAsync(prepared, default);
        await using (var downloaded = await storage.OpenReadAsync(prepared.StorageKey, default))
        {
            using var actual = new MemoryStream();
            await downloaded.CopyToAsync(actual);
            Assert.Equal(bytes, actual.ToArray());
        }
        await storage.DeleteAsync(prepared.StorageKey, default);
    }

    [Theory]
    [InlineData("undeclared")]
    [InlineData("unrelated")]
    [InlineData("external")]
    [InlineData("wrongType")]
    [InlineData("macro")]
    [InlineData("vbaType")]
    [InlineData("vbaRelationship")]
    public async Task RejectsUndeclaredOrMacroBinaryParts_EvenWithPrinterSettings(string defect)
    {
        await using var factory = new IdentityFactory(supportEnabled: true);
        var storage = factory.Services.GetRequiredService<IFileStorage>();
        using var input = new MemoryStream(OfficeWithPrinterSettings("override", defect));
        await Assert.ThrowsAsync<SupportException>(() => storage.PrepareAsync(new("document.docx", input), default));
        var root = factory.Services.GetRequiredService<SupportOptions>().FilesRoot;
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
    }

    private static byte[] OfficeWithPrinterSettings(string declaration, string? defect = null)
    {
        const string printerType = "application/vnd.openxmlformats-officedocument.wordprocessingml.printerSettings";
        var contentType = defect == "vbaType" ? "application/vnd.ms-office.vbaProject" : defect == "wrongType" ? "application/octet-stream" : printerType;
        var declared = defect == "undeclared" ? "" : declaration == "default"
            ? $"<Default Extension=\"bin\" ContentType=\"{contentType}\"/>"
            : $"<Override PartName=\"/word/printerSettings/printerSettings1.bin\" ContentType=\"{contentType}\"/>";
        var relationType = defect == "vbaRelationship" ? "http://schemas.microsoft.com/office/2006/relationships/vbaProject"
            : "http://schemas.openxmlformats.org/officeDocument/2006/relationships/printerSettings";
        using var data = new MemoryStream();
        using (var archive = new ZipArchive(data, ZipArchiveMode.Create, true))
        {
            void Add(string name, string value) { using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(value); }
            Add("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" + declared + "</Types>");
            Add("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"r1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Add("word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><w:body><w:sectPr><w:printerSettings r:id=\"printer1\"/></w:sectPr></w:body></w:document>");
            if (defect != "unrelated")
                Add("word/_rels/document.xml.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"printer1\" Type=\"{relationType}\" Target=\"printerSettings/printerSettings1.bin\"{(defect == "external" ? " TargetMode=\"External\"" : "")}/></Relationships>");
            // Synthetic DEVMODEW: no real device data or driver-specific payload.
            var settings = new byte[220];
            Encoding.Unicode.GetBytes("Synthetic test printer").CopyTo(settings, 0);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(settings.AsSpan(64), 0x0401);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(settings.AsSpan(68), 220);
            using (var printer = archive.CreateEntry("word/printerSettings/printerSettings1.bin").Open()) printer.Write(settings);
            if (defect == "macro") Add("word/vbaProject.bin", "macro");
        }
        return data.ToArray();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("macro")]
    [InlineData("../escape.xml")]
    [InlineData("/absolute.xml")]
    [InlineData("C:\\escape.xml")]
    [InlineData("encrypted")]
    [InlineData("localEncrypted")]
    [InlineData("many")]
    [InlineData("expanded")]
    [InlineData("largeXml")]
    [InlineData("dtd")]
    public async Task RejectsUnsafeOrUnstructuredOpenXml(string defect)
    {
        await using var factory = new IdentityFactory(supportEnabled: true);
        var storage = factory.Services.GetRequiredService<IFileStorage>();
        using var input = new MemoryStream(Office("docx", defect));
        await Assert.ThrowsAsync<SupportException>(() => storage.PrepareAsync(new("document.docx", input), default));
        var root = factory.Services.GetRequiredService<IConfiguration>()["Support:FilesRoot"]!;
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
    }
    private static byte[] Office(string extension, string? defect = null)
    {
        var main = extension == "docx" ? "word/document.xml" : extension == "xlsx" ? "xl/workbook.xml" : "ppt/presentation.xml";
        var kind = extension == "docx" ? "wordprocessingml.document" : extension == "xlsx" ? "spreadsheetml.sheet" : "presentationml.presentation";
        var root = extension == "docx" ? "document" : extension == "xlsx" ? "workbook" : "presentation";
        var ns = kind.Split('.')[0];
        using var data = new MemoryStream();
        using (var archive = new ZipArchive(data, ZipArchiveMode.Create, true))
        {
            void Add(string name, string value) { using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(value); }
            Add("[Content_Types].xml", $"<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/{main}\" ContentType=\"application/vnd.openxmlformats-officedocument.{kind}.main+xml\"/></Types>");
            Add("_rels/.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"r1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"{main}\"/></Relationships>");
            if (defect != "missing") Add(main, (defect == "dtd" ? "<!DOCTYPE document [<!ENTITY x SYSTEM 'file:///not-read'>]>" : "") + $"<{root} xmlns=\"http://schemas.openxmlformats.org/{ns}/2006/main\">{(defect == "largeXml" ? new string('x', 524289) : "")}</{root}>");
            if (defect == "macro") Add("word/vbaProject.bin", "macro");
            if (defect?.Contains("escape") == true || defect == "/absolute.xml") Add(defect, "unexpected");
            if (defect == "many") for (var i = 0; i < 2001; i++) Add($"extra/{i}.xml", "x");
        }
        var bytes = data.ToArray();
        if (defect == "localEncrypted") bytes[6] |= 1;
        if (defect is "encrypted" or "expanded")
            for (var index = 0; index < bytes.Length - 46; index++)
                if (bytes[index] == 0x50 && bytes[index + 1] == 0x4b && bytes[index + 2] == 1 && bytes[index + 3] == 2)
                {
                    if (defect == "encrypted") bytes[index + 8] |= 1;
                    else System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index + 24), 104857601);
                    break;
                }
        return bytes;
    }

    [Fact]
    public async Task CancellationAndReadFailure_RemovePreparationFiles()
    {
        await using var factory = new IdentityFactory(supportEnabled: true);
        var storage = factory.Services.GetRequiredService<IFileStorage>();
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        using var input = new MemoryStream("text"u8.ToArray());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.PrepareAsync(new("note.txt", input), canceled.Token));
        using var failing = new BrokenStream();
        Assert.Equal("SupportFileUnavailable", (await Assert.ThrowsAsync<SupportException>(() => storage.PrepareAsync(new("note.txt", failing), default))).Code);
        var root = factory.Services.GetRequiredService<IConfiguration>()["Support:FilesRoot"]!;
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
    }
    private sealed class BrokenStream : MemoryStream
    { public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => throw new IOException("Simulated disk/read failure"); }
    [Fact]
    public async Task PreparationSlots_AreSharedAcrossInstances_AndCancellationReleasesThem()
    {
        await using var factory = new IdentityFactory(supportEnabled: true);
        var options = factory.Services.GetRequiredService<SupportOptions>();
        var storage = new LocalFileStorage(options); var other = new LocalFileStorage(options);
        using var first = new BlockingStream(); using var second = new BlockingStream(); using var third = new BlockingStream();
        var one = storage.PrepareAsync(new("one.txt", first), default);
        var two = other.PrepareAsync(new("two.txt", second), default);
        await Task.WhenAll(first.Entered.Task, second.Entered.Task).WaitAsync(TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource();
        var waiting = other.PrepareAsync(new("three.txt", third), cancellation.Token);
        Assert.False(third.Entered.Task.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        first.Continue.TrySetResult(); second.Continue.TrySetResult();
        await storage.DeleteAsync((await one).StorageKey, default); await other.DeleteAsync((await two).StorageKey, default);
        using var data = new MemoryStream("ok"u8.ToArray());
        var file = await storage.PrepareAsync(new("last.txt", data), default);
        await storage.CommitAsync(file, default);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.OpenReadAsync(file.StorageKey, cancellation.Token));
        await storage.DeleteAsync(file.StorageKey, default);
    }
    private sealed class BlockingStream : MemoryStream
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal BlockingStream() : base("ok"u8.ToArray()) { }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            Entered.TrySetResult(); await Continue.Task.WaitAsync(ct); return await base.ReadAsync(buffer, ct);
        }
    }}
