using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopCalendar.Services;

public static class WindowBackdropService
{
    private const int WcaAccentPolicy = 19;

    public static void EnableAcrylic(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        if (!SetAccent(handle, AccentState.EnableAcrylicBlurBehind, alpha: 0x28))
        {
            SetAccent(handle, AccentState.EnableBlurBehind, alpha: 0x16);
        }
    }

    private static bool SetAccent(nint handle, AccentState state, byte alpha)
    {
        var accent = new AccentPolicy
        {
            AccentState = state,
            AccentFlags = 2,
            GradientColor = ToAbgr(alpha, red: 0x4B, green: 0x5D, blue: 0x6A),
            AnimationId = 0
        };

        var accentSize = Marshal.SizeOf<AccentPolicy>();
        var accentPtr = Marshal.AllocHGlobal(accentSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                Data = accentPtr,
                SizeOfData = accentSize
            };

            return SetWindowCompositionAttribute(handle, ref data) != 0;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }

    private static int ToAbgr(byte alpha, byte red, byte green, byte blue)
    {
        return alpha << 24 | blue << 16 | green << 8 | red;
    }

    private enum AccentState
    {
        EnableBlurBehind = 3,
        EnableAcrylicBlurBehind = 4
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public nint Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowCompositionAttribute(nint hwnd, ref WindowCompositionAttributeData data);
}
