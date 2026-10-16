using System.Runtime.InteropServices;
using System.Text;
using DevAcademy.AI;

namespace DevAcademy.Infrastructure.Secrets;

/// <summary>
/// macOS Keychain (generic password: service = "DevAcademy", account = provider id) through Security.framework P/Invoke.
/// Secrets never appear on a command line. VERIFIED on macOS (see MacKeychainSecretStoreTests).
/// </summary>
public sealed class MacKeychainSecretStore : ISecretStore
{
    public const string DefaultService = "DevAcademy";
    private const int ErrSecItemNotFound = -25300;
    private const int ErrSecDuplicateItem = -25299;
    private const uint Utf8 = 0x08000100; // kCFStringEncodingUTF8

    private readonly string _service;

    public MacKeychainSecretStore(string service = DefaultService) => _service = service;

    public bool IsAvailable => OperatingSystem.IsMacOS();
    public string? UnavailableReason => IsAvailable ? null : "macOS Keychain is only available on macOS.";

    public string? Get(string providerId)
    {
        EnsureAvailable();
        using var q = new CfDict();
        q.Add(Native.Const("kSecClass"), Native.Const("kSecClassGenericPassword"));
        q.Add(Native.Const("kSecAttrService"), q.Own(Native.Str(_service)));
        q.Add(Native.Const("kSecAttrAccount"), q.Own(Native.Str(providerId)));
        q.Add(Native.Const("kSecReturnData"), Native.Deref("kCFBooleanTrue"));
        q.Add(Native.Const("kSecMatchLimit"), Native.Const("kSecMatchLimitOne"));

        var status = Native.SecItemCopyMatching(q.Handle, out var result);
        if (status == ErrSecItemNotFound) return null;
        Check(status, "read");
        try
        {
            var len = (int)Native.CFDataGetLength(result);
            var bytes = new byte[len];
            Marshal.Copy(Native.CFDataGetBytePtr(result), bytes, 0, len);
            return Encoding.UTF8.GetString(bytes);
        }
        finally { Native.CFRelease(result); }
    }

    public void Set(string providerId, string secret)
    {
        EnsureAvailable();
        var bytes = Encoding.UTF8.GetBytes(secret);
        var data = Native.CFDataCreate(IntPtr.Zero, bytes, bytes.Length);
        try
        {
            using var add = new CfDict();
            add.Add(Native.Const("kSecClass"), Native.Const("kSecClassGenericPassword"));
            add.Add(Native.Const("kSecAttrService"), add.Own(Native.Str(_service)));
            add.Add(Native.Const("kSecAttrAccount"), add.Own(Native.Str(providerId)));
            add.Add(Native.Const("kSecValueData"), data);
            var status = Native.SecItemAdd(add.Handle, IntPtr.Zero);
            if (status != ErrSecDuplicateItem) { Check(status, "write"); return; }

            using var query = new CfDict();
            query.Add(Native.Const("kSecClass"), Native.Const("kSecClassGenericPassword"));
            query.Add(Native.Const("kSecAttrService"), query.Own(Native.Str(_service)));
            query.Add(Native.Const("kSecAttrAccount"), query.Own(Native.Str(providerId)));
            using var update = new CfDict();
            update.Add(Native.Const("kSecValueData"), data);
            Check(Native.SecItemUpdate(query.Handle, update.Handle), "update");
        }
        finally
        {
            Native.CFRelease(data);
            Array.Clear(bytes);
        }
    }

    public void Delete(string providerId)
    {
        EnsureAvailable();
        using var q = new CfDict();
        q.Add(Native.Const("kSecClass"), Native.Const("kSecClassGenericPassword"));
        q.Add(Native.Const("kSecAttrService"), q.Own(Native.Str(_service)));
        q.Add(Native.Const("kSecAttrAccount"), q.Own(Native.Str(providerId)));
        var status = Native.SecItemDelete(q.Handle);
        if (status != ErrSecItemNotFound) Check(status, "delete");
    }

    private void EnsureAvailable()
    {
        if (!IsAvailable) throw new SecretStoreException(UnavailableReason!);
    }

    private static void Check(int status, string op)
    {
        if (status != 0) throw new SecretStoreException($"macOS Keychain {op} failed (OSStatus {status}).");
    }

    private sealed class CfDict : IDisposable
    {
        private readonly List<IntPtr> _owned = [];
        private readonly List<IntPtr> _keys = [];
        private readonly List<IntPtr> _values = [];
        private IntPtr _handle;

        public IntPtr Own(IntPtr cf) { _owned.Add(cf); return cf; }
        public void Add(IntPtr key, IntPtr value) { _keys.Add(key); _values.Add(value); }

        public IntPtr Handle
        {
            get
            {
                if (_handle == IntPtr.Zero)
                    _handle = Native.CFDictionaryCreate(IntPtr.Zero, _keys.ToArray(), _values.ToArray(), _keys.Count,
                        Native.Address("kCFTypeDictionaryKeyCallBacks"), Native.Address("kCFTypeDictionaryValueCallBacks"));
                return _handle;
            }
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero) Native.CFRelease(_handle);
            foreach (var o in _owned) Native.CFRelease(o);
        }
    }

    private static class Native
    {
        private const string Security = "/System/Library/Frameworks/Security.framework/Security";
        private const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        private static readonly Lazy<IntPtr> CfLib = new(() => NativeLibrary.Load(CF));
        private static readonly Lazy<IntPtr> SecLib = new(() => NativeLibrary.Load(Security));

        /// <summary>Address of an exported data symbol (used for the callbacks structs).</summary>
        public static IntPtr Address(string symbol)
        {
            if (NativeLibrary.TryGetExport(CfLib.Value, symbol, out var p)) return p;
            return NativeLibrary.GetExport(SecLib.Value, symbol);
        }

        /// <summary>Value of an exported CFTypeRef constant (e.g. kSecClass).</summary>
        public static IntPtr Deref(string symbol) => Marshal.ReadIntPtr(Address(symbol));
        public static IntPtr Const(string symbol) => Deref(symbol);

        public static IntPtr Str(string s) => CFStringCreateWithCString(IntPtr.Zero, s, Utf8);

        [DllImport(CF)] public static extern IntPtr CFStringCreateWithCString(IntPtr alloc, [MarshalAs(UnmanagedType.LPUTF8Str)] string s, uint encoding);
        [DllImport(CF)] public static extern IntPtr CFDataCreate(IntPtr alloc, byte[] bytes, nint length);
        [DllImport(CF)] public static extern IntPtr CFDataGetBytePtr(IntPtr data);
        [DllImport(CF)] public static extern nint CFDataGetLength(IntPtr data);
        [DllImport(CF)] public static extern void CFRelease(IntPtr cf);
        [DllImport(CF)] public static extern IntPtr CFDictionaryCreate(IntPtr alloc, IntPtr[] keys, IntPtr[] values, nint count, IntPtr keyCallbacks, IntPtr valueCallbacks);
        [DllImport(Security)] public static extern int SecItemAdd(IntPtr attributes, IntPtr result);
        [DllImport(Security)] public static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);
        [DllImport(Security)] public static extern int SecItemUpdate(IntPtr query, IntPtr attributesToUpdate);
        [DllImport(Security)] public static extern int SecItemDelete(IntPtr query);
    }
}
