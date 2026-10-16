using Microsoft.AspNetCore.Http;
using SchoolGether.Api.Support;
using SchoolGether.Application.Support;
using SchoolGether.Contracts.Support;

namespace SchoolGether.Api.IntegrationTests;

[CollectionDefinition("Multipart admission", DisableParallelization = true)]
public sealed class MultipartAdmissionCollection;

[Collection("Multipart admission")]
public sealed class SupportMultipartAdmissionTests
{
    [Fact]
    public async Task CreateAndSend_ShareAdmissionUntilBuffersAreDisposed()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var first = await Body(new CreateSupportTicketRequest(Guid.NewGuid(), "Pedido", "Technical", "Normal", "Ajuda"));
        using var second = await Body(new SendSupportMessageRequest(Guid.NewGuid(), 1, "Resposta", false));
        using var third = await Body(new CreateSupportTicketRequest(Guid.NewGuid(), "Outro", "Technical", "Normal", "Ajuda"));
        var one = SupportMultipartRequest<CreateSupportTicketRequest>.ReadAsync(Request(first), cancellation.Token);
        var two = SupportMultipartRequest<SendSupportMessageRequest>.ReadAsync(Request(second), cancellation.Token);
        var three = SupportMultipartRequest<CreateSupportTicketRequest>.ReadAsync(Request(third), cancellation.Token);
        try
        {
            Assert.True(first.Entered.Task.IsCompleted);
            Assert.True(second.Entered.Task.IsCompleted);
            Assert.False(third.Entered.Task.IsCompleted);
            first.Continue.TrySetResult();
            using var parsed = await one;
            Assert.Single(parsed.Files);
            Assert.False(third.Entered.Task.IsCompleted);
            parsed.Dispose();
            parsed.Dispose(); // Repeated disposal must not create another permit.
            await third.Entered.Task.WaitAsync(cancellation.Token);
            second.Continue.TrySetResult(); third.Continue.TrySetResult();
            using var parsedTwo = await two;
            using var parsedThree = await three;
            Assert.Single(parsedTwo.Files); Assert.Single(parsedThree.Files);
        }
        finally
        {
            cancellation.Cancel();
            await DisposeResults(one, two, three);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledWaiterDoesNotRead_AndReadFailureOrCancellationReleasesAdmission(bool cancelRead)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var firstCancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        using var waitingCancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        using var first = await Body(new { unsupported = true });
        using var second = await Body(new SendSupportMessageRequest(Guid.NewGuid(), 1, "Resposta", false));
        using var third = await Body(new CreateSupportTicketRequest(Guid.NewGuid(), "Pedido", "Technical", "Normal", "Ajuda"));
        var one = SupportMultipartRequest<CreateSupportTicketRequest>.ReadAsync(Request(first), firstCancellation.Token);
        var two = SupportMultipartRequest<SendSupportMessageRequest>.ReadAsync(Request(second), deadline.Token);
        var canceled = SupportMultipartRequest<CreateSupportTicketRequest>.ReadAsync(Request(third), waitingCancellation.Token);
        Task<SupportMultipartRequest<CreateSupportTicketRequest>>? next = null;
        try
        {
            Assert.False(third.Entered.Task.IsCompleted);
            waitingCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
            Assert.False(third.Entered.Task.IsCompleted);
            next = SupportMultipartRequest<CreateSupportTicketRequest>.ReadAsync(Request(third), deadline.Token);
            Assert.False(third.Entered.Task.IsCompleted);
            if (cancelRead)
            {
                firstCancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => one);
            }
            else
            {
                first.Continue.TrySetResult();
                await Assert.ThrowsAsync<SupportException>(() => one);
            }
            await third.Entered.Task.WaitAsync(deadline.Token);
            second.Continue.TrySetResult(); third.Continue.TrySetResult();
            using var parsedTwo = await two;
            using var parsedNext = await next;
            Assert.Single(parsedNext.Files);
        }
        finally
        {
            deadline.Cancel();
            await DisposeResults(one, two, canceled);
            if (next is not null) await DisposeResults(next);
        }
    }

    private static async Task<GatedBody> Body(object request)
    {
        using var content = SupportAttachmentTests.Multipart(request, ("note.txt", "ok"u8.ToArray()));
        return new(await content.ReadAsByteArrayAsync(), content.Headers.ContentType!.ToString());
    }
    private static HttpRequest Request(GatedBody body)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = body.ContentType;
        context.Request.Body = body;
        return context.Request;
    }
    private static async Task DisposeResults(params Task[] tasks)
    {
        foreach (var task in tasks)
        {
            try
            {
                await task;
                if (task is Task<SupportMultipartRequest<CreateSupportTicketRequest>> create) create.Result.Dispose();
                if (task is Task<SupportMultipartRequest<SendSupportMessageRequest>> send) send.Result.Dispose();
            }
            catch (Exception) { /* Drain every operation after cancellation, including expected malformed input. */ }
        }
    }
    private sealed class GatedBody(byte[] bytes, string contentType) : MemoryStream(bytes)
    {
        internal string ContentType { get; } = contentType;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            Entered.TrySetResult();
            await Continue.Task.WaitAsync(ct);
            return await base.ReadAsync(buffer, ct);
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
    }
}
