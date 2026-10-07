using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DesktopCalendar.Services;

public sealed class DesktopHostService
{
    private const int GwlExStyle = -20;
    private const uint GwOwner = 4;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExAppWindow = 0x00040000L;
    private const int SwpNoZOrder = 0x0004;
    private const int SwpNoActivate = 0x0010;
    private const int SwpShowWindow = 0x0040;
    private const long MaxLogBytes = 64 * 1024;
    private static readonly nint HwndBottom = new(1);

    private nint _desktopHost;

    public bool Attach(Window window)
    {
        var helper = new WindowInteropHelper(window);
        var handle = helper.Handle;
        if (handle == nint.Zero)
        {
            return false;
        }

        var desktopHost = GetDesktopHostWindow();
        HideFromAltTab(handle);

        if (desktopHost == nint.Zero)
        {
            Position(window, handle);
            Log($"Attach fallback: handle=0x{handle:X}");
            return false;
        }

        _desktopHost = desktopHost;
        helper.Owner = desktopHost;
        Position(window, handle);

        var owner = GetWindow(handle, GwOwner);
        var attached = owner == desktopHost;
        Log($"Attach: handle=0x{handle:X} host=0x{desktopHost:X} owner=0x{owner:X} attached={attached}");
        return attached;
    }

    public void Repair(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        if (_desktopHost == nint.Zero || !IsWindow(_desktopHost) || GetWindow(handle, GwOwner) != _desktopHost)
        {
            Attach(window);
            return;
        }

        Position(window, handle);
    }

    // Handles WM_WINDOWPOSCHANGING: activation (for example by the context menu) would raise the
    // window above other apps, so every z-order change is redirected to the bottom.
    public static void KeepAtBottom(nint windowPosPointer)
    {
        var position = Marshal.PtrToStructure<WindowPos>(windowPosPointer);
        if ((position.Flags & SwpNoZOrder) != 0 || position.InsertAfter == HwndBottom)
        {
            return;
        }

        position.InsertAfter = HwndBottom;
        Marshal.StructureToPtr(position, windowPosPointer, false);
    }

    private static void Position(Window window, nint handle)
    {
        var dpi = VisualTreeHelper.GetDpi(window);
        SetWindowPos(
            handle,
            HwndBottom,
            (int)Math.Round(window.Left * dpi.DpiScaleX),
            (int)Math.Round(window.Top * dpi.DpiScaleY),
            (int)Math.Round(window.Width * dpi.DpiScaleX),
            (int)Math.Round(window.Height * dpi.DpiScaleY),
            SwpNoActivate | SwpShowWindow);
    }

    private static void HideFromAltTab(nint handle)
    {
        var style = GetWindowLongValue(handle, GwlExStyle);
        style &= ~WsExAppWindow;
        style |= WsExToolWindow;
        SetWindowLongValue(handle, GwlExStyle, style);
    }

    private static nint GetDesktopHostWindow()
    {
        var progman = FindWindow("Progman", "Program Manager");
        if (progman == nint.Zero)
        {
            progman = FindWindow("Progman", null);
        }

        return progman;
    }

    private static long GetWindowLongValue(nint handle, int index)
    {
        return nint.Size == 8
            ? GetWindowLongPtr64(handle, index).ToInt64()
            : GetWindowLong32(handle, index);
    }

    private static void SetWindowLongValue(nint handle, int index, long value)
    {
        if (nint.Size == 8)
        {
            SetWindowLongPtr64(handle, index, new nint(value));
        }
        else
        {
            SetWindowLong32(handle, index, unchecked((int)value));
        }
    }

    private static void Log(string message)
    {
        try
        {
            AppPaths.Ensure();
            var path = Path.Combine(AppPaths.AppDataRoot, "desktop-host.log");
            if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
            {
                File.Move(path, path + ".old", overwrite: true);
            }

            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Desktop attachment must not depend on diagnostic logging.
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        public nint Handle;
        public nint InsertAfter;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public int Flags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetWindow(nint hWnd, uint uCmd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, int flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);
}
