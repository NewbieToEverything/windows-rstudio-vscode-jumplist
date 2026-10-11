using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.RegularExpressions;

class JumpListTool
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHGetPropertyStoreFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        int flags,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    class CShellLink { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, Guid("77F10CF0-3DB5-4966-B520-B7C54FD35ED6")]
    class CDestinationList { }

    [ComImport, Guid("2D3468C1-36A7-43B6-AC24-D3F02FD9607A")]
    class CEnumerableObjectCollection { }

    [ComImport, Guid("6332DEBF-87B5-4670-90C0-5E57B408A49E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ICustomDestinationList
    {
        void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
        void BeginList(out uint pcMinSlots, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out IObjectArray ppv);
        void AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string pszCategory, [MarshalAs(UnmanagedType.Interface)] IObjectArray poa);
        void AppendKnownCategory(int category);
        void AddUserTasks([MarshalAs(UnmanagedType.Interface)] IObjectArray poa);
        void CommitList();
        void GetRemovedDestinations([MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out IObjectArray ppv);
        void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
        void AbortList();
    }

    [ComImport, Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IObjectArray
    {
        void GetCount(out uint pcObjects);
        void GetAt(uint uiIndex, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
    }

    [ComImport, Guid("5632B1A4-E38A-400A-928A-D4CD63230295"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IObjectCollection
    {
        void GetCount(out uint pcObjects);
        void GetAt(uint uiIndex, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void AddObject([MarshalAs(UnmanagedType.Interface)] object puc);
        void AddFromArray([MarshalAs(UnmanagedType.Interface)] IObjectArray poaSource);
        void RemoveObjectAt(uint uiIndex);
        void Clear();
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(int sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROPERTYKEY { public Guid fmtid; public uint pid; }

    [StructLayout(LayoutKind.Explicit)]
    struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr p;
    }

    static readonly Guid IID_IObjectArray = new Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9");
    static readonly Guid IID_IShellItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    static readonly Guid IID_IPropertyStore = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    static readonly Guid PKEY_AppUserModel_ID = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    static readonly Guid PKEY_Title = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9");
    static readonly string DockerIcon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "docker.ico");

    const ushort VT_LPWSTR = 31;
    const int GPS_READWRITE = 2;

    static bool SetLinkTitle(IShellLinkW link, string title)
    {
        try
        {
            IPropertyStore store = (IPropertyStore)link;
            PROPERTYKEY key = new PROPERTYKEY();
            key.fmtid = PKEY_Title;
            key.pid = 2;
            PROPVARIANT pv = new PROPVARIANT();
            pv.vt = VT_LPWSTR;
            pv.p = Marshal.StringToCoTaskMemUni(title);
            store.SetValue(ref key, ref pv);
            store.Commit();
            Marshal.FreeCoTaskMem(pv.p);
            return true;
        }
        catch { return false; }
    }

    // 创建一条 shell link：指定 exe / 参数 / 工作目录 / 图标 / 显示名(title) / tooltip(description)
    static IShellLinkW MakeLink(string exe, string runArgs, string workingDir, string iconPath, string title, string desc)
    {
        var link = (IShellLinkW)Activator.CreateInstance(typeof(CShellLink));
        link.SetPath(exe);
        link.SetArguments(runArgs ?? "");
        link.SetWorkingDirectory(workingDir);
        link.SetIconLocation(iconPath, 0);
        string d = desc ?? "";
        if (d.Length > 260) d = d.Substring(0, 260);
        try { link.SetDescription(d); } catch {}
        SetLinkTitle(link, title);
        return link;
    }

    // 可选的尾部参数：--new-window <exe> <args> <title> <desc>
    // 用 sentinel 标记，解析出「通用新窗口条目」的规格（若提供）
    static bool ParseNewWindow(string[] args, out string nwExe, out string nwArgs, out string nwTitle, out string nwDesc, out string nwIcon)
    {
        nwExe = nwArgs = nwTitle = nwDesc = nwIcon = null;
        int idx = Array.IndexOf(args, "--newwin-entry");
        if (idx < 0) return false;
        if (idx + 4 >= args.Length) return false;
        nwExe = args[idx + 1];
        nwArgs = args[idx + 2];
        nwTitle = args[idx + 3];
        nwDesc = args[idx + 4];
        // 可选第 6 个参数：图标来源（缺省则用 exe 自身图标）
        if (idx + 5 < args.Length) nwIcon = args[idx + 5];
        if (nwIcon == "(none)") nwIcon = null;
        // (none) 表示不传启动参数（空字符串会被 PowerShell splat 丢弃）
        if (nwArgs == "(none)") nwArgs = "";
        // IShellLink 会破坏参数中的引号，带空格的绝对路径会被截断
        // （见 issue: wscript 收到 "D:\my" 而非完整路径）。
        // 故新窗口条目一律用「相对脚本名 + WorkingDirectory」，路径中不含空格。
        return true;
    }

    // 该条目在普通文件列表中的结束位置（sentinel 之后不再算入）
    static int FileArgEnd(string[] args, int startIndex)
    {
        int idx = Array.IndexOf(args, "--newwin-entry");
        return idx >= 0 ? idx : args.Length;
    }

    static void Main(string[] args)
    {
        try
        {
            if (args.Length < 1) { Usage(); return; }
            switch (args[0].ToLowerInvariant())
            {
                case "create": CmdCreate(args, false); break;
                case "createtasks": CmdCreate(args, true); break;
                case "createlinks": CmdCreateLinks(args); break;
                case "delete": CmdDelete(args); break;
                case "setaumid": CmdSetAumid(args); break;
                case "readaumid": CmdReadAumid(args); break;
                default: Usage(); break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.GetType().Name + ": " + ex.Message);
            Environment.Exit(1);
        }
    }

    static void Usage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  JumpListTool create <aumid> <category> <file1> [file2] ...");
        Console.WriteLine("  JumpListTool createtasks <aumid> <file1> [file2] ... [--new-window <exe> <args> <title> <desc>]");
        Console.WriteLine("  JumpListTool createlinks <aumid> <exe> <target1> [target2] ... [--new-window <exe> <args> <title> <desc>]");
        Console.WriteLine("  JumpListTool delete <aumid>");
        Console.WriteLine("  JumpListTool setaumid <lnk> <aumid>");
        Console.WriteLine("  JumpListTool readaumid <lnk>");
    }

    static void CmdCreate(string[] args, bool asTasks)
    {
        int startIndex = asTasks ? 2 : 3;
        if (args.Length < startIndex + 1) { Usage(); return; }

        string aumid = args[1];
        string category = asTasks ? null : args[2];

        var destList = (ICustomDestinationList)Activator.CreateInstance(typeof(CDestinationList));
        destList.SetAppID(aumid);

        IObjectArray removed;
        uint minSlots;
        destList.BeginList(out minSlots, IID_IObjectArray, out removed);

        var collection = (IObjectCollection)Activator.CreateInstance(typeof(CEnumerableObjectCollection));
        int added = 0;
        int endIndex = FileArgEnd(args, startIndex);
        for (int i = startIndex; i < endIndex; i++)
        {
            string target = args[i];
            if (!File.Exists(target) && !Directory.Exists(target)) { Console.WriteLine("Missing: " + target); continue; }

            string title = Path.GetFileNameWithoutExtension(Path.GetFileName(target.TrimEnd('\\', '/')));
            if (string.IsNullOrEmpty(title)) title = target;
            collection.AddObject(MakeLink(
                target, "",
                Directory.Exists(target) ? target : Path.GetDirectoryName(target),
                target, title, target));
            added++;
        }

        // 通用「新窗口」条目（不指向任何文件）
        string nwExe, nwArgs, nwTitle, nwDesc, nwIcon;
        if (ParseNewWindow(args, out nwExe, out nwArgs, out nwTitle, out nwDesc, out nwIcon))
        {
            // 工作目录用工具自身所在目录（launcher 等辅助脚本都在这里），
            // 这样条目里只需写相对脚本名，避开 IShellLink 对带空格路径的引号截断。
            string nwDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string nwIconPath = string.IsNullOrEmpty(nwIcon) ? nwExe : nwIcon;
            collection.AddObject(MakeLink(nwExe, nwArgs, nwDir, nwIconPath, nwTitle, nwDesc));
            added++;
        }

        if (asTasks)
        {
            destList.AddUserTasks((IObjectArray)(object)collection);
            destList.CommitList();
            Console.WriteLine("OK: tasks created for AUMID=" + aumid + " with " + added + " items");
        }
        else
        {
            destList.AppendCategory(category, (IObjectArray)(object)collection);
            destList.CommitList();
            Console.WriteLine("OK: category '" + category + "' created for AUMID=" + aumid + " with " + added + " items");
        }
    }

    static void CmdCreateLinks(string[] args)
    {
        if (args.Length < 4) { Usage(); return; }
        string aumid = args[1];
        string exe = args[2];

        var destList = (ICustomDestinationList)Activator.CreateInstance(typeof(CDestinationList));
        destList.SetAppID(aumid);

        IObjectArray removed;
        uint minSlots;
        destList.BeginList(out minSlots, IID_IObjectArray, out removed);

        var collection = (IObjectCollection)Activator.CreateInstance(typeof(CEnumerableObjectCollection));
        int added = 0;
        string scriptDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        for (int i = 3; i < FileArgEnd(args, 3); i++)
        {
            string target = args[i];
            bool isDir = Directory.Exists(target);
            if (!isDir && !File.Exists(target)) { Console.WriteLine("Missing: " + target); continue; }

            string runExe;
            string runArgs;
            if (target.EndsWith(".vsremote", StringComparison.OrdinalIgnoreCase))
            {
                runExe = "wscript.exe";
                runArgs = "\"launch_remote.vbs\" \"" + target + "\"";
            }
            else
            {
                runExe = exe;
                runArgs = "\"" + target + "\"";
            }

            var link = (IShellLinkW)Activator.CreateInstance(typeof(CShellLink));
            link.SetPath(runExe);
            link.SetArguments(runArgs);
            string iconPath = (target.EndsWith(".vsremote", StringComparison.OrdinalIgnoreCase) && File.Exists(DockerIcon)) ? DockerIcon : exe;
            link.SetIconLocation(iconPath, 0);
            link.SetWorkingDirectory(target.EndsWith(".vsremote", StringComparison.OrdinalIgnoreCase) ? scriptDir : (isDir ? target : Path.GetDirectoryName(target)));
            string desc = GetProjectPath(target);
            if (string.IsNullOrEmpty(desc)) desc = target;
            if (desc.Length > 260) desc = desc.Substring(0, 260);
            try { link.SetDescription(desc); } catch {}
            string title = Path.GetFileNameWithoutExtension(Path.GetFileName(target.TrimEnd('\\', '/')));
            if (string.IsNullOrEmpty(title)) title = desc;
            SetLinkTitle(link, title);
            collection.AddObject(link);
            added++;
        }

        // 通用「新窗口」条目（不指向任何文件）
        string nwExe2, nwArgs2, nwTitle2, nwDesc2, nwIcon2;
        if (ParseNewWindow(args, out nwExe2, out nwArgs2, out nwTitle2, out nwDesc2, out nwIcon2))
        {
            collection.AddObject(MakeLink(nwExe2, nwArgs2, Path.GetDirectoryName(nwExe2),
                string.IsNullOrEmpty(nwIcon2) ? nwExe2 : nwIcon2, nwTitle2, nwDesc2));
            added++;
        }

        destList.AddUserTasks((IObjectArray)(object)collection);
        destList.CommitList();
        Console.WriteLine("OK: task links created for AUMID=" + aumid + " with " + added + " items");
    }

    static string GetProjectPath(string target) {
        if (target.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase)) {
            try {
                string json = File.ReadAllText(target);
                var m = Regex.Match(json, "\"path\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success) return m.Groups[1].Value.Replace("\\\\", "\\");
            } catch {}
        } else if (target.EndsWith(".vsremote", StringComparison.OrdinalIgnoreCase)) {
            try { return File.ReadAllText(target).Trim(); } catch {}
        }
        return target;
    }

    static void CmdDelete(string[] args)
    {
        if (args.Length < 2) { Usage(); return; }
        var destList = (ICustomDestinationList)Activator.CreateInstance(typeof(CDestinationList));
        destList.DeleteList(args[1]);
        Console.WriteLine("OK: deleted list for " + args[1]);
    }

    static void CmdSetAumid(string[] args)
    {
        if (args.Length < 3) { Usage(); return; }
        string lnk = args[1];
        string aumid = args[2];
        IPropertyStore store;
        SHGetPropertyStoreFromParsingName(lnk, IntPtr.Zero, GPS_READWRITE, IID_IPropertyStore, out store);

        PROPERTYKEY key = new PROPERTYKEY();
        key.fmtid = PKEY_AppUserModel_ID;
        key.pid = 5;

        PROPVARIANT pv = new PROPVARIANT();
        pv.vt = VT_LPWSTR;
        pv.p = Marshal.StringToCoTaskMemUni(aumid);

        store.SetValue(ref key, ref pv);
        store.Commit();
        Marshal.FreeCoTaskMem(pv.p);
        Console.WriteLine("OK: set AUMID=" + aumid + " on " + Path.GetFileName(lnk));
    }

    static void CmdReadAumid(string[] args)
    {
        if (args.Length < 2) { Usage(); return; }
        IPropertyStore store;
        SHGetPropertyStoreFromParsingName(args[1], IntPtr.Zero, 0, IID_IPropertyStore, out store);
        PROPERTYKEY key = new PROPERTYKEY();
        key.fmtid = PKEY_AppUserModel_ID;
        key.pid = 5;
        PROPVARIANT pv;
        store.GetValue(ref key, out pv);
        if (pv.vt == VT_LPWSTR && pv.p != IntPtr.Zero)
            Console.WriteLine(Marshal.PtrToStringUni(pv.p));
        else
            Console.WriteLine("(none, vt=" + pv.vt + ")");
    }
}
