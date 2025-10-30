using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

static class Remote
{
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint a, bool i, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr a, uint s, uint t, uint p);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool WriteProcessMemory(IntPtr h, IntPtr a, byte[] b, uint s, out UIntPtr w);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GetProcAddress(IntPtr h, string n);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GetModuleHandle(string n);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateRemoteThread(IntPtr h, IntPtr a, uint st, IntPtr start, IntPtr param, uint f, IntPtr id);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr h);

    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint f, int pid);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool Module32First(IntPtr s, ref MODULEENTRY32 m);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool Module32Next(IntPtr s, ref MODULEENTRY32 m);

    const uint PROCESS_ALL_ACCESS = 0x1F0FFF, MEM_COMMIT = 0x1000, MEM_RESERVE = 0x2000, PAGE_READWRITE = 0x04;
    const uint TH32CS_SNAPMODULE = 0x8 | 0x10, WAIT_OBJECT_0 = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MODULEENTRY32
    {
        public uint dwSize, th32ModuleID, th32ProcessID, GlblcntUsage, ProccntUsage;
        public IntPtr modBaseAddr; public uint modBaseSize; public IntPtr hModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExePath;
    }

    static IntPtr GetRemoteModuleBase(int pid, string name)
    {
        var snap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, pid);
        var me = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>() };
        if (snap == IntPtr.Zero) return IntPtr.Zero;
        try
        {
            if (!Module32First(snap, ref me)) return IntPtr.Zero;
            do
            {
                if (string.Equals(me.szModule, name, StringComparison.OrdinalIgnoreCase))
                    return me.modBaseAddr;
            } while (Module32Next(snap, ref me));
            return IntPtr.Zero;
        }
        finally { CloseHandle(snap); }
    }

    public static (IntPtr hProc, IntPtr remoteBase) EnsureInjected(Process p, string dllPath, string moduleName)
    {
        var hProc = OpenProcess(PROCESS_ALL_ACCESS, false, p.Id);
        if (hProc == IntPtr.Zero) throw new Exception("OpenProcess failed.");

        var baseAddr = GetRemoteModuleBase(p.Id, moduleName);
        if (baseAddr != IntPtr.Zero) return (hProc, baseAddr);

        // inject via LoadLibraryA
        var buf = Encoding.ASCII.GetBytes(dllPath + "\0");
        var remoteStr = VirtualAllocEx(hProc, IntPtr.Zero, (uint)buf.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
        if (remoteStr == IntPtr.Zero) throw new Exception("VirtualAllocEx failed.");
        if (!WriteProcessMemory(hProc, remoteStr, buf, (uint)buf.Length, out _)) throw new Exception("WriteProcessMemory failed.");
        var pLoadLib = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
        var thr = CreateRemoteThread(hProc, IntPtr.Zero, 0, pLoadLib, remoteStr, 0, IntPtr.Zero);
        if (thr == IntPtr.Zero) throw new Exception("CreateRemoteThread(LoadLibraryA) failed.");
        WaitForSingleObject(thr, 10_000);
        CloseHandle(thr);

        baseAddr = GetRemoteModuleBase(p.Id, moduleName);
        if (baseAddr == IntPtr.Zero) throw new Exception("Module base not found.");
        return (hProc, baseAddr);
    }

    public static IntPtr GetRemoteExportByRva(IntPtr remoteBase, string localDllPath, string exportName)
    {
        var hLocal = NativeLibrary.Load(localDllPath);
        try
        {
            if (!NativeLibrary.TryGetExport(hLocal, exportName, out var localFunc))
                throw new EntryPointNotFoundException(exportName);
            long rva = localFunc.ToInt64() - hLocal.ToInt64();
            return new IntPtr(remoteBase.ToInt64() + rva);
        }
        finally { NativeLibrary.Free(hLocal); }
    }

    public static void CallRemoteBool(IntPtr hProc, IntPtr remoteThreadProc, bool value)
    {
        // pass boolean as pointer: null = false, non-null = true
        var param = value ? new IntPtr(1) : IntPtr.Zero;
        var thr = CreateRemoteThread(hProc, IntPtr.Zero, 0, remoteThreadProc, param, 0, IntPtr.Zero);
        if (thr == IntPtr.Zero) throw new Exception("CreateRemoteThread failed.");
        WaitForSingleObject(thr, 10_000);
        CloseHandle(thr);
    }
}