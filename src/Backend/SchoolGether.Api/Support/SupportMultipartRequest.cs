using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using SchoolGether.Application.Support;

namespace SchoolGether.Api.Support;

internal sealed class SupportMultipartRequest<T> : IDisposable
{
    internal const int BodyLimit = 16777216;
    internal T Request { get; private set; } = default!;
    internal List<IncomingAttachment> Files { get; } = [];
    internal static async Task<SupportMultipartRequest<T>> ReadAsync(HttpRequest request, CancellationToken ct)
    {
        var result = new SupportMultipartRequest<T>();
        try
        {
            if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var type) || !type.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase) ||
                !type.Boundary.HasValue || type.Boundary.Length > 128) throw new SupportException("InvalidSupportInput");
            if (request.ContentLength > BodyLimit) throw new SupportException("SupportRequestTooLarge");
            using var limited = new LimitedBody(request.Body);
            var reader = new MultipartReader(HeaderUtilities.RemoveQuotes(type.Boundary).Value!, limited) { HeadersLengthLimit = 8192, BodyLengthLimit = BodyLimit };
            var hasRequest = false;
            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(ct)) is not null)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) || disposition.DispositionType != "form-data")
                    throw new SupportException("InvalidSupportInput");
                var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;
                var fileName = HeaderUtilities.RemoveQuotes(disposition.FileNameStar.HasValue ? disposition.FileNameStar : disposition.FileName).Value;
                if (name == "request" && fileName is null && !hasRequest)
                {
                    using var data = await ReadPartAsync(section.Body, 32768, ct);
                    result.Request = await JsonSerializer.DeserializeAsync<T>(data, new JsonSerializerOptions(JsonSerializerDefaults.Web), ct) ?? throw new SupportException("InvalidSupportInput");
                    hasRequest = true;
                }
                else if (name == "files" && fileName is not null && result.Files.Count < 3)
                    result.Files.Add(new(fileName, await ReadPartAsync(section.Body, 5242880, ct)));
                else throw new SupportException("InvalidSupportInput");
            }
            if (!hasRequest) throw new SupportException("InvalidSupportInput");
            return result;
        }
        catch (Exception ex)
        {
            result.Dispose();
            if (ex is JsonException or InvalidDataException) throw new SupportException("InvalidSupportInput");
            throw;
        }
    }
    private static async Task<MemoryStream> ReadPartAsync(Stream source, int maximum, CancellationToken ct)
    {
        var data = new MemoryStream();
        try
        {
            var buffer = new byte[81920]; int count;
            while ((count = await source.ReadAsync(buffer, ct)) > 0)
            {
                if (data.Length + count > maximum) throw new SupportException("InvalidSupportAttachment");
                data.Write(buffer, 0, count);
            }
            data.Position = 0; return data;
        }
        catch { data.Dispose(); throw; }
    }
    public void Dispose() { foreach (var file in Files) file.Content.Dispose(); }
    private sealed class LimitedBody(Stream source) : Stream
    {
        private long read;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var count = await source.ReadAsync(buffer[..Math.Min(buffer.Length, (int)Math.Max(1, BodyLimit + 1 - read))], ct);
            if ((read += count) > BodyLimit) throw new SupportException("SupportRequestTooLarge");
            return count;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class SupportMultipartAttribute : Attribute, Microsoft.AspNetCore.Mvc.Filters.IResourceFilter
{
    public void OnResourceExecuting(Microsoft.AspNetCore.Mvc.Filters.ResourceExecutingContext context)
    {
        for (var index = context.ValueProviderFactories.Count - 1; index >= 0; index--)
            if (context.ValueProviderFactories[index] is Microsoft.AspNetCore.Mvc.ModelBinding.FormValueProviderFactory or
                Microsoft.AspNetCore.Mvc.ModelBinding.FormFileValueProviderFactory or Microsoft.AspNetCore.Mvc.ModelBinding.JQueryFormValueProviderFactory)
                context.ValueProviderFactories.RemoveAt(index);
    }
    public void OnResourceExecuted(Microsoft.AspNetCore.Mvc.Filters.ResourceExecutedContext context) { }
}
