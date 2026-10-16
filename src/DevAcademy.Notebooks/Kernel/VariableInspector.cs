using System.Text.Json.Nodes;

namespace DevAcademy.Notebooks.Kernel;

/// <summary>One row of the variable inspector.</summary>
public sealed record VariableInfo(string Name, string Type, string Summary, string? Shape);

/// <summary>
/// The variable inspector is an ordinary silent <c>execute_request</c>: a small introspection snippet is defined in the kernel (under a name
/// starting with an underscore, which the inspector itself hides) and called through <c>user_expressions</c>, whose result comes back as
/// JSON in the execute_reply. Only primitives, containers and array-likes (numpy/pandas/torch shapes) are summarised; modules, functions
/// and classes are skipped.
/// </summary>
public static class VariableInspector
{
    public const string Expression = "_devacademy_vars()";

    public const string Setup = """
        def _devacademy_vars():
            import types, reprlib
            _r = reprlib.Repr(); _r.maxstring = 60; _r.maxother = 60; _r.maxlist = 6; _r.maxtuple = 6; _r.maxdict = 4; _r.maxset = 6; _r.maxlevel = 2
            _prims = (bool, int, float, complex, str, bytes, type(None))
            _conts = (list, tuple, dict, set, frozenset)
            _skip = (types.ModuleType, types.FunctionType, types.BuiltinFunctionType, types.MethodType, type)
            rows = []
            for name, v in list(get_ipython().user_ns.items()):
                if name.startswith('_') or name in ('In', 'Out', 'exit', 'quit', 'get_ipython') or isinstance(v, _skip):
                    continue
                t = type(v)
                shape = None
                try:
                    if isinstance(v, _prims):
                        summary = _r.repr(v)
                    elif isinstance(v, _conts):
                        summary = _r.repr(v)
                        shape = 'len ' + str(len(v))
                    elif t.__module__.split('.')[0] in ('numpy', 'pandas', 'torch', 'tensorflow', 'polars') and hasattr(v, 'shape'):
                        shape = 'x'.join(str(d) for d in tuple(v.shape))
                        summary = 'dtype ' + str(getattr(v, 'dtype', '')) if hasattr(v, 'dtype') else ''
                    else:
                        continue
                except Exception:
                    summary = '<ошибка repr>'
                rows.append({'name': name, 'type': t.__name__, 'summary': summary, 'shape': shape})
            rows.sort(key=lambda r: r['name'])
            return _DevAcademyJson(rows[:500])

        class _DevAcademyJson:
            def __init__(self, data): self.data = data
            def _repr_json_(self): return self.data
            def __repr__(self): return 'variables'
        """;

    public static ExecuteOptions Options { get; } = new()
    {
        Silent = true,
        StoreHistory = false,
        UserExpressions = new JsonObject { ["vars"] = Expression },
    };

    /// <summary>Reads the table back from the execute_reply of a request made with <see cref="Setup"/> and <see cref="Options"/>.</summary>
    public static IReadOnlyList<VariableInfo> Parse(ExecuteResult result)
    {
        var rows = new List<VariableInfo>();
        if (result.UserExpressions?["vars"] is not JsonObject expr || expr["status"]?.GetValue<string>() != "ok") return rows;
        if (expr["data"]?["application/json"] is not JsonArray array) return rows;
        foreach (var item in array.OfType<JsonObject>())
            rows.Add(new VariableInfo(
                item["name"]?.GetValue<string>() ?? "",
                item["type"]?.GetValue<string>() ?? "",
                item["summary"]?.GetValue<string>() ?? "",
                item["shape"]?.GetValue<string>()));
        return rows;
    }

    /// <summary>Runs the inspector against a kernel.</summary>
    public static async Task<IReadOnlyList<VariableInfo>> QueryAsync(IKernelClient kernel, CancellationToken ct) =>
        Parse(await kernel.ExecuteAsync(Setup, Options, null, ct).ConfigureAwait(false));
}
