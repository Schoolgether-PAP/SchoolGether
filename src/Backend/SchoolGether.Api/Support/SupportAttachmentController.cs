using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Application.Support;

namespace SchoolGether.Api.Support;

[ApiController, Authorize, Route("api/v1/support/attachments")]
public sealed class SupportAttachmentController(SupportQueries queries) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var file = await queries.DownloadAsync(id, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: false);
    }
}
