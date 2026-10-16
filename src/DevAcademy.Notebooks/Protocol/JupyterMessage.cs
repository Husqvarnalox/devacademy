using System.Text.Json.Nodes;

namespace DevAcademy.Notebooks.Protocol;

/// <summary>A Jupyter message (protocol 5.3): routing identities, four JSON dictionaries and optional binary buffers.</summary>
public sealed class JupyterMessage
{
    public const string ProtocolVersion = "5.3";

    public IReadOnlyList<byte[]> Identities { get; init; } = [];
    public required JsonObject Header { get; init; }
    public JsonObject ParentHeader { get; init; } = new();
    public JsonObject Metadata { get; init; } = new();
    public JsonObject Content { get; init; } = new();
    public IReadOnlyList<byte[]> Buffers { get; init; } = [];

    public string MsgId => Header["msg_id"]?.GetValue<string>() ?? "";
    public string MsgType => Header["msg_type"]?.GetValue<string>() ?? "";
    public string Session => Header["session"]?.GetValue<string>() ?? "";
    public string ParentMsgId => ParentHeader["msg_id"]?.GetValue<string>() ?? "";

    public static JsonObject NewHeader(string msgType, string session, string username = "devacademy") => new()
    {
        ["msg_id"] = Guid.NewGuid().ToString("N"),
        ["session"] = session,
        ["username"] = username,
        ["date"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", System.Globalization.CultureInfo.InvariantCulture),
        ["msg_type"] = msgType,
        ["version"] = ProtocolVersion,
    };

    public static JupyterMessage Create(string msgType, string session, JsonObject content, JsonObject? parentHeader = null) => new()
    {
        Header = NewHeader(msgType, session),
        Content = content,
        ParentHeader = parentHeader ?? new JsonObject(),
    };

    // ---- the requests this client sends

    public static JupyterMessage KernelInfoRequest(string session) => Create("kernel_info_request", session, new JsonObject());

    public static JupyterMessage ExecuteRequest(string session, string code, bool silent = false, bool storeHistory = true,
        bool allowStdin = false, JsonObject? userExpressions = null) =>
        Create("execute_request", session, new JsonObject
        {
            ["code"] = code,
            ["silent"] = silent,
            ["store_history"] = storeHistory && !silent,
            ["user_expressions"] = userExpressions?.DeepClone() ?? new JsonObject(),
            ["allow_stdin"] = allowStdin,
            ["stop_on_error"] = true,
        });

    public static JupyterMessage ShutdownRequest(string session, bool restart) =>
        Create("shutdown_request", session, new JsonObject { ["restart"] = restart });

    public static JupyterMessage InterruptRequest(string session) => Create("interrupt_request", session, new JsonObject());

    public static JupyterMessage InputReply(string session, string value, JsonObject parentHeader, IReadOnlyList<byte[]> identities) =>
        new() { Header = NewHeader("input_reply", session), Content = new JsonObject { ["value"] = value }, ParentHeader = parentHeader, Identities = identities };
}
