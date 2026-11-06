using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Control;

/// <summary>
/// Answers the requests that change the instance (ADR-0124 decision 7) - fault rules, seeding, reset, snapshots and
/// restores - by calling the <see cref="IControlTarget"/>, which publishes each change on the event stream.
/// </summary>
/// <param name="target">What the endpoint drives.</param>
/// <param name="snapshots">The snapshots the endpoint keeps.</param>
/// <param name="answers">Writes the answers.</param>
internal sealed class ControlStoreRequests(IControlTarget target, ControlSnapshotShelf snapshots, ControlAnswers answers)
{
    /// <summary><c>POST /v1/faults</c>: adds the <c>text/plain</c> body as one <c>--fault</c> item.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>201 {"fault_id"}</c>.</returns>
    public async Task<HttpAnswer> AddFaultAsync(ControlRequest request)
    {
        ControlRequestBody.RequireMediaType(request.Http, "text/plain");
        var item = Encoding.UTF8.GetString(await ControlRequestBody.ReadAsync(request.Http, request.CancellationToken)).TrimEnd('\r', '\n');
        if (item.Length == 0)
        {
            throw new ControlErrorException(ControlError.BadRequest("the body must be one --fault item"));
        }

        return answers.Json(request.Http, ControlError.Created, new ControlFaultIdJson(target.AddFault(item)), ControlJsonContext.Default.ControlFaultIdJson);
    }

    /// <summary><c>GET /v1/faults</c>: the rules in the order they are tried.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>{"faults": [...]}</c>.</returns>
    public Task<HttpAnswer> ListFaultsAsync(ControlRequest request)
    {
        var faults = new ControlFaultsJson([.. target.ListFaults().Select(fault => new ControlFaultJson(fault.FaultId, fault.Item, fault.Remaining))]);

        return Task.FromResult(answers.Json(request.Http, HttpStatus.Ok, faults, ControlJsonContext.Default.ControlFaultsJson));
    }

    /// <summary><c>DELETE /v1/faults/{id}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>, or <c>404</c> for an unknown ID.</returns>
    public Task<HttpAnswer> RemoveFaultAsync(ControlRequest request) =>
        Task.FromResult(target.RemoveFault(request.Parameters[0])
            ? answers.NoContent(request.Http)
            : throw NotFound($"no fault rule has ID {request.Parameters[0]}"));

    /// <summary><c>POST /v1/mail?to=&lt;address&gt;</c>: delivers the <c>message/rfc822</c> body as a seed.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>.</returns>
    public async Task<HttpAnswer> SeedMailAsync(ControlRequest request)
    {
        var recipient = request.Query.Text("to");
        if (string.IsNullOrEmpty(recipient))
        {
            throw new ControlErrorException(ControlError.BadRequest("query key to must name the recipient"));
        }

        ControlRequestBody.RequireMediaType(request.Http, "message/rfc822");
        var message = await ControlRequestBody.ReadAsync(request.Http, request.CancellationToken);
        await target.SeedMailAsync(recipient, message, request.CancellationToken);

        return answers.NoContent(request.Http);
    }

    /// <summary><c>PUT /v1/files/{path}</c>: writes the body as the file.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>.</returns>
    public async Task<HttpAnswer> WriteFileAsync(ControlRequest request)
    {
        var content = await ControlRequestBody.ReadAsync(request.Http, request.CancellationToken);
        await target.WriteFileAsync(FilePath(request), content, request.CancellationToken);

        return answers.NoContent(request.Http);
    }

    /// <summary><c>DELETE /v1/files/{path}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>, or <c>404</c> when no file is there.</returns>
    public async Task<HttpAnswer> DeleteFileAsync(ControlRequest request) =>
        await target.DeleteFileAsync(FilePath(request), request.CancellationToken)
            ? answers.NoContent(request.Http)
            : throw NotFound($"no file at {FilePath(request)}");

    /// <summary><c>POST /v1/messages</c>: publishes or enqueues the JSON body's message as a seed.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>.</returns>
    public async Task<HttpAnswer> PublishMessageAsync(ControlRequest request)
    {
        var body = await ReadJsonAsync(request, ControlJsonContext.Default.ControlMessageRequestJson);
        await target.PublishMessageAsync(ControlMessageReading.Read(body), request.CancellationToken);

        return answers.NoContent(request.Http);
    }

    /// <summary><c>POST /v1/accounts</c>: adds the JSON body's account.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>.</returns>
    public async Task<HttpAnswer> AddAccountAsync(ControlRequest request)
    {
        var body = await ReadJsonAsync(request, ControlJsonContext.Default.ControlAccountRequestJson);
        if (string.IsNullOrEmpty(body.User) || string.IsNullOrEmpty(body.Password))
        {
            throw new ControlErrorException(ControlError.BadRequest("give user and password, neither empty"));
        }

        await target.AddAccountAsync(body.User, body.Password, request.CancellationToken);

        return answers.NoContent(request.Http);
    }

    /// <summary><c>DELETE /v1/accounts/{user}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>, or <c>404</c> for an unknown account.</returns>
    public async Task<HttpAnswer> RemoveAccountAsync(ControlRequest request) =>
        await target.RemoveAccountAsync(request.Parameters[0], request.CancellationToken)
            ? answers.NoContent(request.Http)
            : throw NotFound($"no account is named {request.Parameters[0]}");

    /// <summary><c>POST /v1/reset</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>.</returns>
    public async Task<HttpAnswer> ResetAsync(ControlRequest request)
    {
        await target.ResetAsync(request.CancellationToken);

        return answers.NoContent(request.Http);
    }

    /// <summary><c>POST /v1/snapshots</c>: takes and keeps a snapshot.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>201 {"snapshot_id"}</c>.</returns>
    public async Task<HttpAnswer> SnapshotAsync(ControlRequest request)
    {
        var snapshotId = snapshots.Keep(await target.SnapshotAsync(request.CancellationToken));

        return answers.Json(request.Http, ControlError.Created, new ControlSnapshotIdJson(snapshotId), ControlJsonContext.Default.ControlSnapshotIdJson);
    }

    /// <summary><c>POST /v1/snapshots/{id}/restore</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>, or <c>404</c> for a snapshot not kept.</returns>
    public async Task<HttpAnswer> RestoreAsync(ControlRequest request)
    {
        var snapshot = snapshots.Find(request.Parameters[0]) ?? throw NotFound($"no snapshot has ID {request.Parameters[0]}");
        await target.RestoreAsync(snapshot, request.CancellationToken);

        return answers.NoContent(request.Http);
    }

    private static string FilePath(ControlRequest request) => "/" + request.Parameters[0];

    private static ControlErrorException NotFound(string message) => new(ControlError.NotFound(message));

    // The JSON body; its message names where it went wrong, never a value it held, so a password is never echoed.
    private static async Task<T> ReadJsonAsync<T>(ControlRequest request, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        ControlRequestBody.RequireMediaType(request.Http, "application/json");
        var body = await ControlRequestBody.ReadAsync(request.Http, request.CancellationToken);
        try
        {
            return JsonSerializer.Deserialize(body, typeInfo) ?? throw new JsonException(null, "$", null, null);
        }
        catch (JsonException failure)
        {
            throw new ControlErrorException(ControlError.BadRequest($"the body is not the JSON object this request takes (at {failure.Path})"));
        }
    }
}
