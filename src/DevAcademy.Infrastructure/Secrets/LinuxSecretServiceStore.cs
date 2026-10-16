using System.Runtime.InteropServices;
using DevAcademy.AI;

namespace DevAcademy.Infrastructure.Secrets;

/// <summary>
/// Linux Secret Service through libsecret (secret_password_storev_sync / lookupv_sync / clearv_sync with a GHashTable of
/// attributes, avoiding C varargs). If libsecret cannot be loaded the store is unavailable: there is NO plaintext fallback.
/// UNVERIFIED: compiled but never executed on Linux by the author (64-bit layout assumed for SecretSchema).
/// </summary>
public sealed class LinuxSecretServiceStore : ISecretStore
{
    private static readonly string[] SecretLibs = ["libsecret-1.so.0", "libsecret-1.so"];
    private static readonly string[] GLibs = ["libglib-2.0.so.0", "libglib-2.0.so"];

    private readonly string _service;
    private readonly Lazy<Loaded?> _lib;
    private string? _reason;

    public LinuxSecretServiceStore(string service = "DevAcademy")
    {
        _service = service;
        _lib = new Lazy<Loaded?>(Load);
    }

    public bool IsAvailable => OperatingSystem.IsLinux() && _lib.Value is not null;
    public string? UnavailableReason => IsAvailable ? null : _reason ?? "libsecret requires Linux.";

    public string? Get(string providerId)
    {
        var l = Require();
        var attrs = Attributes(l, providerId);
        try
        {
            var err = IntPtr.Zero;
            var p = l.Lookup(l.Schema, attrs, IntPtr.Zero, ref err);
            ThrowIfError(err, l, "read");
            if (p == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUTF8(p); }
            finally { l.Free(p); }
        }
        finally { l.Unref(attrs); }
    }

    public void Set(string providerId, string secret)
    {
        var l = Require();
        var attrs = Attributes(l, providerId);
        try
        {
            var err = IntPtr.Zero;
            l.Store(l.Schema, attrs, "default", $"{_service} ({providerId})", secret, IntPtr.Zero, ref err);
            ThrowIfError(err, l, "write");
        }
        finally { l.Unref(attrs); }
    }

    public void Delete(string providerId)
    {
        var l = Require();
        var attrs = Attributes(l, providerId);
        try
        {
            var err = IntPtr.Zero;
            l.Clear(l.Schema, attrs, IntPtr.Zero, ref err);
            ThrowIfError(err, l, "delete");
        }
        finally { l.Unref(attrs); }
    }

    private Loaded Require() =>
        (OperatingSystem.IsLinux() ? _lib.Value : null) ?? throw new SecretStoreException(UnavailableReason!);

    private IntPtr Attributes(Loaded l, string providerId)
    {
        // The table owns copies of the strings (g_strdup) and frees them with g_free.
        var table = l.HashNew(l.StrHash, l.StrEqual, l.GFree, l.GFree);
        l.HashInsert(table, l.Dup("service"), l.Dup(_service));
        l.HashInsert(table, l.Dup("provider"), l.Dup(providerId));
        return table;
    }

    private static void ThrowIfError(IntPtr gerror, Loaded l, string op)
    {
        if (gerror == IntPtr.Zero) return;
        // GError { GQuark domain; gint code; gchar *message; }
        var msg = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(gerror, 8));
        l.ErrorFree(gerror);
        throw new SecretStoreException($"libsecret {op} failed: {msg}");
    }

    private Loaded? Load()
    {
        if (!OperatingSystem.IsLinux()) { _reason = "libsecret requires Linux."; return null; }
        try
        {
            var secret = TryLoad(SecretLibs) ?? throw new DllNotFoundException("libsecret-1 not found");
            var glib = TryLoad(GLibs) ?? throw new DllNotFoundException("libglib-2.0 not found");
            return new Loaded(secret, glib);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            _reason = "libsecret (Secret Service) could not be loaded: " + e.Message + ". Install libsecret and a keyring daemon (GNOME Keyring / KWallet) to store API keys.";
            return null;
        }
    }

    private static IntPtr? TryLoad(string[] names)
    {
        foreach (var n in names)
            if (NativeLibrary.TryLoad(n, out var h)) return h;
        return null;
    }

    private delegate void StoreFn(IntPtr schema, IntPtr attrs, [MarshalAs(UnmanagedType.LPUTF8Str)] string collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label, [MarshalAs(UnmanagedType.LPUTF8Str)] string password, IntPtr cancellable, ref IntPtr error);
    private delegate IntPtr LookupFn(IntPtr schema, IntPtr attrs, IntPtr cancellable, ref IntPtr error);
    private delegate bool ClearFn(IntPtr schema, IntPtr attrs, IntPtr cancellable, ref IntPtr error);
    private delegate void FreeFn(IntPtr p);
    private delegate IntPtr HashNewFn(IntPtr hash, IntPtr equal, IntPtr keyDestroy, IntPtr valueDestroy);
    private delegate bool HashInsertFn(IntPtr table, IntPtr key, IntPtr value);
    private delegate IntPtr StrdupFn([MarshalAs(UnmanagedType.LPUTF8Str)] string s);

    private sealed class Loaded
    {
        public readonly StoreFn Store;
        public readonly LookupFn Lookup;
        public readonly ClearFn Clear;
        public readonly FreeFn Free, Unref, ErrorFree;
        public readonly HashNewFn HashNew;
        public readonly HashInsertFn HashInsert;
        public readonly StrdupFn Dup;
        public readonly IntPtr StrHash, StrEqual, GFree;
        public readonly IntPtr Schema;

        public Loaded(IntPtr secret, IntPtr glib)
        {
            Store = Marshal.GetDelegateForFunctionPointer<StoreFn>(NativeLibrary.GetExport(secret, "secret_password_storev_sync"));
            Lookup = Marshal.GetDelegateForFunctionPointer<LookupFn>(NativeLibrary.GetExport(secret, "secret_password_lookupv_sync"));
            Clear = Marshal.GetDelegateForFunctionPointer<ClearFn>(NativeLibrary.GetExport(secret, "secret_password_clearv_sync"));
            Free = Marshal.GetDelegateForFunctionPointer<FreeFn>(NativeLibrary.GetExport(secret, "secret_password_free"));
            Unref = Marshal.GetDelegateForFunctionPointer<FreeFn>(NativeLibrary.GetExport(glib, "g_hash_table_unref"));
            ErrorFree = Marshal.GetDelegateForFunctionPointer<FreeFn>(NativeLibrary.GetExport(glib, "g_error_free"));
            HashNew = Marshal.GetDelegateForFunctionPointer<HashNewFn>(NativeLibrary.GetExport(glib, "g_hash_table_new_full"));
            HashInsert = Marshal.GetDelegateForFunctionPointer<HashInsertFn>(NativeLibrary.GetExport(glib, "g_hash_table_insert"));
            Dup = Marshal.GetDelegateForFunctionPointer<StrdupFn>(NativeLibrary.GetExport(glib, "g_strdup"));
            StrHash = NativeLibrary.GetExport(glib, "g_str_hash");
            StrEqual = NativeLibrary.GetExport(glib, "g_str_equal");
            GFree = NativeLibrary.GetExport(glib, "g_free");
            Schema = BuildSchema();
        }

        /// <summary>
        /// struct SecretSchema { const gchar *name; SecretSchemaFlags flags; SecretSchemaAttribute attributes[32]; reserved... }
        /// with SecretSchemaAttribute { const gchar *name; SecretSchemaAttributeType type; } (16 bytes on 64-bit).
        /// </summary>
        private static IntPtr BuildSchema()
        {
            const int size = 1024;
            var p = Marshal.AllocHGlobal(size);
            for (var i = 0; i < size; i += 8) Marshal.WriteInt64(p, i, 0);
            Marshal.WriteIntPtr(p, 0, Marshal.StringToCoTaskMemUTF8("app.devacademy.ApiKey"));
            Marshal.WriteInt32(p, 8, 0); // SECRET_SCHEMA_NONE
            Marshal.WriteIntPtr(p, 16, Marshal.StringToCoTaskMemUTF8("service"));
            Marshal.WriteInt32(p, 24, 0); // SECRET_SCHEMA_ATTRIBUTE_STRING
            Marshal.WriteIntPtr(p, 32, Marshal.StringToCoTaskMemUTF8("provider"));
            Marshal.WriteInt32(p, 40, 0);
            return p; // lives for the process lifetime
        }
    }
}
