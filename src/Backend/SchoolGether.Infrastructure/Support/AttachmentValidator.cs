using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SchoolGether.Application.Support;

namespace SchoolGether.Infrastructure.Support;

internal static class AttachmentValidator
{
    internal const long MaximumBytes = 5 * 1024 * 1024;
    internal static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150 || name.Any(char.IsControl) ||
            name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || name.EndsWith('.') || name.EndsWith(' '))
            throw Invalid();
    }

    internal static async Task<string> ValidateAsync(Stream stream, string name, CancellationToken ct)
    {
        var header = new byte[16];
        var count = await stream.ReadAsync(header, ct); stream.Position = 0;
        var extension = Path.GetExtension(name).ToLowerInvariant();
        switch (extension)
        {
            case ".txt":
                try
                {
                    using var reader = new StreamReader(stream, new UTF8Encoding(false, true), false, 4096, leaveOpen: true);
                    var buffer = new char[4096]; int read;
                    while ((read = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
                        if (buffer.AsSpan(0, read).Contains('\0')) throw Invalid();
                }
                catch (DecoderFallbackException) { throw Invalid(); }
                return "text/plain";
            case ".png" when count >= 16 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && header.AsSpan(12, 4).SequenceEqual("IHDR"u8):
                if (stream.Length < 45 || !await EndsWithAsync(stream, new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }, ct)) throw Invalid();
                return "image/png";
            case ".jpg" or ".jpeg" when count >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255:
                if (!await EndsWithAsync(stream, new byte[] { 255, 217 }, ct)) throw Invalid();
                return "image/jpeg";
            case ".webp" when count >= 16 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8):
                if (BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)) + 8L != stream.Length ||
                    !(header.AsSpan(12, 4).SequenceEqual("VP8 "u8) || header.AsSpan(12, 4).SequenceEqual("VP8L"u8) || header.AsSpan(12, 4).SequenceEqual("VP8X"u8))) throw Invalid();
                return "image/webp";
            case ".pdf" when count >= 8 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8):
                stream.Position = Math.Max(0, stream.Length - 1024);
                var tail = new byte[stream.Length - stream.Position]; await stream.ReadExactlyAsync(tail, ct);
                if (!Encoding.ASCII.GetString(tail).TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal)) throw Invalid();
                return "application/pdf";
            case ".docx": case ".xlsx": case ".pptx":
                await ValidateOfficeAsync(stream, extension, ct);
                return "application/vnd.openxmlformats-officedocument." + (extension == ".docx" ? "wordprocessingml.document" : extension == ".xlsx" ? "spreadsheetml.sheet" : "presentationml.presentation");
            default: throw Invalid();
        }
    }
    private static async Task<bool> EndsWithAsync(Stream stream, byte[] tail, CancellationToken ct)
    {
        if (stream.Length < tail.Length) return false;
        stream.Position = stream.Length - tail.Length;
        var actual = new byte[tail.Length]; await stream.ReadExactlyAsync(actual, ct);
        return actual.SequenceEqual(tail);
    }
    private static async Task ValidateOfficeAsync(Stream stream, string extension, CancellationToken ct)
    {
        try
        {
            ValidateZipFlags(stream);
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count is 0 or > 2000) throw Invalid();
            long total = 0;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                var name = entry.FullName;
                if (!names.Add(name) || name.StartsWith('/') || name.Contains('\\') || name.Contains(':') ||
                    name.Split('/').Any(x => x == "..") || name.Any(char.IsControl) || name.Contains("vba", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) || (total += entry.Length) > 100 * 1024 * 1024)
                    throw Invalid();
            }
            var main = extension == ".docx" ? "word/document.xml" : extension == ".xlsx" ? "xl/workbook.xml" : "ppt/presentation.xml";
            var kind = extension == ".docx" ? "wordprocessingml.document" : extension == ".xlsx" ? "spreadsheetml.sheet" : "presentationml.presentation";
            var types = await ReadXmlAsync(archive.GetEntry("[Content_Types].xml"), ct);
            XNamespace contentNs = "http://schemas.openxmlformats.org/package/2006/content-types";
            if (types.Root?.Name != contentNs + "Types" || types.Descendants().Attributes().Any(x => x.Value.Contains("macro", StringComparison.OrdinalIgnoreCase)) ||
                !types.Root.Elements(contentNs + "Override").Any(x => (string?)x.Attribute("PartName") == "/" + main &&
                    (string?)x.Attribute("ContentType") == "application/vnd.openxmlformats-officedocument." + kind + ".main+xml")) throw Invalid();
            var rels = await ReadXmlAsync(archive.GetEntry("_rels/.rels"), ct);
            XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
            if (rels.Root?.Name != relNs + "Relationships" || !rels.Root.Elements(relNs + "Relationship").Any(x =>
                    (string?)x.Attribute("Type") == "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" &&
                    (string?)x.Attribute("Target") == main && (string?)x.Attribute("TargetMode") != "External")) throw Invalid();
            var document = await ReadXmlAsync(archive.GetEntry(main), ct);
            var root = extension == ".docx" ? "document" : extension == ".xlsx" ? "workbook" : "presentation";
            var ns = extension == ".docx" ? "wordprocessingml" : extension == ".xlsx" ? "spreadsheetml" : "presentationml";
            if (document.Root?.Name != XName.Get(root, "http://schemas.openxmlformats.org/" + ns + "/2006/main")) throw Invalid();
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or EndOfStreamException or OverflowException or NotSupportedException)
        { throw Invalid(); }
    }
    private static async Task<XDocument> ReadXmlAsync(ZipArchiveEntry? entry, CancellationToken ct)
    {
        if (entry is null || entry.Length > 512 * 1024) throw Invalid();
        await using var input = entry.Open();
        using var buffer = new MemoryStream();
        var bytes = new byte[8192]; int count;
        while ((count = await input.ReadAsync(bytes, ct)) > 0)
        {
            if (buffer.Length + count > 512 * 1024) throw Invalid();
            buffer.Write(bytes, 0, count);
        }
        buffer.Position = 0;
        using var reader = XmlReader.Create(buffer, new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 512 * 1024 });
        return await XDocument.LoadAsync(reader, LoadOptions.None, ct);
    }
    private static void ValidateZipFlags(Stream stream)
    {
        // Lê o diretório central sem extrair: ZipArchive não expõe a marca de cifra.
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var end = -1L;
        for (var position = stream.Length - 22; position >= Math.Max(0, stream.Length - 65557); position--)
        {
            stream.Position = position;
            if (reader.ReadUInt32() == 0x06054b50) { end = position; break; }
        }
        if (end < 0) throw Invalid();
        stream.Position = end + 4;
        if (reader.ReadUInt16() != 0 || reader.ReadUInt16() != 0) throw Invalid();
        var diskCount = reader.ReadUInt16(); var count = reader.ReadUInt16();
        var length = reader.ReadUInt32(); var offset = reader.ReadUInt32();
        if (count is 0 or > 2000 || diskCount != count || (long)offset + length != end) throw Invalid();
        stream.Position = offset;
        for (var i = 0; i < count; i++)
        {
            var start = stream.Position;
            if (reader.ReadUInt32() != 0x02014b50) throw Invalid();
            stream.Position = start + 8;
            if ((reader.ReadUInt16() & 0x41) != 0) throw Invalid();
            stream.Position = start + 28;
            var name = reader.ReadUInt16(); var extra = reader.ReadUInt16(); var comment = reader.ReadUInt16();
            var next = start + 46 + name + extra + comment;
            if (next > end) throw Invalid();
            stream.Position = start + 42;
            var localOffset = reader.ReadUInt32();
            if ((long)localOffset + 30 > offset) throw Invalid();
            stream.Position = localOffset;
            if (reader.ReadUInt32() != 0x04034b50) throw Invalid();
            stream.Position = localOffset + 6;
            if ((reader.ReadUInt16() & 0x41) != 0) throw Invalid();
            stream.Position = next;
        }
        if (stream.Position != end) throw Invalid();
    }
    private static SupportException Invalid() => new("InvalidSupportAttachment");
}
