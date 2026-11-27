using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Control;

/// <summary>
/// Answers the requests that change the instance (ADR-0124 decision 7) - adding and removing fault rules, seeding,
/// deleting files, account changes (ADR-0132, ADR-0170), reset, snapshots and restores - and the fault rule listing,
/// by calling the <see cref="IControlTarget"/>. The target, not this class, publishes changes on the event stream.
/// </summary>
/// <param name="target">What the endpoint drives.</param>
/// <param name="snapshots">The snapshots the endpoint keeps.</param>
/// <param name="answers">Writes the answers.</param>
internal sealed class ControlStoreRequests(IControlTarget target, ControlSnapshotShelf snapshots, ControlAnswers answers)
{
    private const int MaxDatabases = 16;
    private const long MaxExpireMilliseconds = 31_536_000_000;

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
        await target.SeedMailAsync(recipient, target.WritesDeliveredTo ? WithDeliveredTo(recipient, message) : message, request.CancellationToken);

        return answers.NoContent(request.Http);
    }

    private static byte[] WithDeliveredTo(string recipient, ReadOnlyMemory<byte> message)
    {
        var line = Encoding.UTF8.GetBytes($"Delivered-To: {recipient}\r\n");
        var stamped = new byte[line.Length + message.Length];
        line.CopyTo(stamped, 0);
        message.CopyTo(stamped.AsMemory(line.Length));

        return stamped;
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

    /// <summary>
    /// <c>PUT /v1/keys/{database}/{key}</c>: sets the body's bytes as a string key, living <c>expire_ms</c>
    /// milliseconds when the query gives it (ADR-0158 decision 9).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>; <c>400</c> for a database outside 0 to 15 or a bad <c>expire_ms</c>.</returns>
    public async Task<HttpAnswer> SeedKeyAsync(ControlRequest request)
    {
        if (!int.TryParse(request.Parameters[0], NumberStyles.None, CultureInfo.InvariantCulture, out var database) || database >= MaxDatabases)
        {
            throw new ControlErrorException(ControlError.BadRequest($"the database must be a whole number from 0 to {MaxDatabases - 1}"));
        }

        var expireMilliseconds = request.Query.Number("expire_ms", 1, MaxExpireMilliseconds, 0);
        TimeSpan? expiry = expireMilliseconds == 0 ? null : TimeSpan.FromMilliseconds(expireMilliseconds);
        var value = await ControlRequestBody.ReadAsync(request.Http, request.CancellationToken);
        await target.SeedKeyAsync(database, Encoding.UTF8.GetBytes(request.Parameters[1]), value, expiry, request.CancellationToken);

        return answers.NoContent(request.Http);
    }

    /// <summary><c>POST /v1/directory</c>: adds the <c>text/plain</c> body's LDIF content records.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>.</returns>
    public async Task<HttpAnswer> SeedDirectoryAsync(ControlRequest request)
    {
        await target.SeedDirectoryAsync(await ReadTextAsync(request), request.CancellationToken);

        return answers.NoContent(request.Http);
    }

    /// <summary><c>PUT /v1/zones/{apex}</c>: adds the <c>text/plain</c> body as the apex's zone file.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>.</returns>
    public async Task<HttpAnswer> SeedZoneAsync(ControlRequest request)
    {
        await target.SeedZoneAsync(request.Parameters[0], await ReadTextAsync(request), request.CancellationToken);

        return answers.NoContent(request.Http);
    }

    /// <summary><c>POST /v1/accounts</c>: adds the JSON body's account.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>.</returns>
    public Task<HttpAnswer> AddAccountAsync(ControlRequest request) => AddAccountToAsync(request, target.AddAccountAsync, allowEmptyUser: true);

    /// <summary><c>DELETE /v1/accounts/{user}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>, or <c>404</c> for an unknown account.</returns>
    public Task<HttpAnswer> RemoveAccountAsync(ControlRequest request) =>
        RemoveAccountFromAsync(request, target.RemoveAccountAsync, "account");

    /// <summary><c>POST /v1/proxy-accounts</c>: adds the JSON body's proxy account (ADR-0170 decision 4).</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>; <c>409</c> when no proxy account is configured.</returns>
    public Task<HttpAnswer> AddProxyAccountAsync(ControlRequest request) => AddAccountToAsync(request, target.AddProxyAccountAsync);

    /// <summary><c>DELETE /v1/proxy-accounts/{user}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>; <c>404</c> for an unknown proxy account; <c>409</c> when none is configured.</returns>
    public Task<HttpAnswer> RemoveProxyAccountAsync(ControlRequest request) =>
        RemoveAccountFromAsync(request, target.RemoveProxyAccountAsync, "proxy account");

    /// <summary><c>POST /v1/tls-srp-accounts</c>: adds the JSON body's TLS-SRP account (ADR-0170 decision 4).</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>; <c>409</c> when TLS-SRP is not configured.</returns>
    public Task<HttpAnswer> AddTlsSrpAccountAsync(ControlRequest request) => AddAccountToAsync(request, target.AddTlsSrpAccountAsync);

    /// <summary><c>DELETE /v1/tls-srp-accounts/{user}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>; <c>404</c> for an unknown TLS-SRP account; <c>409</c> when TLS-SRP is not configured.</returns>
    public Task<HttpAnswer> RemoveTlsSrpAccountAsync(ControlRequest request) =>
        RemoveAccountFromAsync(request, target.RemoveTlsSrpAccountAsync, "TLS-SRP account");

    /// <summary><c>PUT /v1/accounts/{user}</c>: gives the account the JSON body's password (ADR-0132 amendment 1).</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>; <c>404</c> for an unknown account; <c>400</c> for an empty password.</returns>
    public Task<HttpAnswer> SetAccountPasswordAsync(ControlRequest request) =>
        SetPasswordOfAsync(request, target.SetAccountPasswordAsync, "account");

    /// <summary><c>PUT /v1/proxy-accounts/{user}</c>: gives the proxy account the JSON body's password.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>; <c>404</c> for an unknown proxy account; <c>400</c> for an empty password; <c>409</c> when none is configured.</returns>
    public Task<HttpAnswer> SetProxyAccountPasswordAsync(ControlRequest request) =>
        SetPasswordOfAsync(request, target.SetProxyAccountPasswordAsync, "proxy account");

    /// <summary><c>PUT /v1/tls-srp-accounts/{user}</c>: gives the TLS-SRP account the JSON body's password.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>; <c>404</c> for an unknown TLS-SRP account; <c>400</c> for an empty password; <c>409</c> when TLS-SRP is not configured.</returns>
    public Task<HttpAnswer> SetTlsSrpAccountPasswordAsync(ControlRequest request) =>
        SetPasswordOfAsync(request, target.SetTlsSrpAccountPasswordAsync, "TLS-SRP account");

    private async Task<HttpAnswer> SetPasswordOfAsync(ControlRequest request, Func<string, string, CancellationToken, Task<bool>> setPassword, string kind)
    {
        var body = await ReadJsonAsync(request, ControlJsonContext.Default.ControlPasswordRequestJson);
        if (string.IsNullOrEmpty(body.Password))
        {
            throw new ControlErrorException(ControlError.BadRequest("give a password, not empty"));
        }

        return await setPassword(request.Parameters[0], body.Password, request.CancellationToken)
            ? answers.NoContent(request.Http)
            : throw NotFound($"no {kind} is named {request.Parameters[0]}");
    }

    /// <summary><c>PUT /v1/bearer-token</c>: gives the Bearer-token (empty-name) account the JSON body's password (ADR-0132 amendment 2).</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>; <c>404</c> when there is no Bearer-token account; <c>400</c> for an empty password.</returns>
    public async Task<HttpAnswer> SetBearerTokenAsync(ControlRequest request)
    {
        var body = await ReadJsonAsync(request, ControlJsonContext.Default.ControlPasswordRequestJson);
        if (string.IsNullOrEmpty(body.Password))
        {
            throw new ControlErrorException(ControlError.BadRequest("give a password, not empty"));
        }

        return await target.SetAccountPasswordAsync(string.Empty, body.Password, request.CancellationToken)
            ? answers.NoContent(request.Http)
            : throw NotFound(NoBearerTokenMessage);
    }

    /// <summary><c>DELETE /v1/bearer-token</c>: removes the Bearer-token (empty-name) account (ADR-0132 amendment 2).</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>204</c>, or <c>404</c> when there is no Bearer-token account.</returns>
    public async Task<HttpAnswer> RemoveBearerTokenAsync(ControlRequest request) =>
        await target.RemoveAccountAsync(string.Empty, request.CancellationToken)
            ? answers.NoContent(request.Http)
            : throw NotFound(NoBearerTokenMessage);

    private const string NoBearerTokenMessage = "no Bearer-token account is set";

    private async Task<HttpAnswer> AddAccountToAsync(ControlRequest request, Func<string, string, CancellationToken, Task> add, bool allowEmptyUser = false)
    {
        var body = await ReadJsonAsync(request, ControlJsonContext.Default.ControlAccountRequestJson);
        var user = body.User;
        if (user is null || (!allowEmptyUser && user.Length == 0) || string.IsNullOrEmpty(body.Password))
        {
            throw new ControlErrorException(ControlError.BadRequest("give user and password, neither empty"));
        }

        await add(user, body.Password, request.CancellationToken);

        return answers.NoContent(request.Http);
    }

    private async Task<HttpAnswer> RemoveAccountFromAsync(ControlRequest request, Func<string, CancellationToken, Task<bool>> remove, string kind) =>
        await remove(request.Parameters[0], request.CancellationToken)
            ? answers.NoContent(request.Http)
            : throw NotFound($"no {kind} is named {request.Parameters[0]}");

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

    private static async Task<string> ReadTextAsync(ControlRequest request)
    {
        ControlRequestBody.RequireMediaType(request.Http, "text/plain");

        return Encoding.UTF8.GetString(await ControlRequestBody.ReadAsync(request.Http, request.CancellationToken));
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
