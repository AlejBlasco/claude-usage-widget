using System.Runtime.InteropServices;
using System.Windows;

namespace ClaudeMeter.Desktop.Windowing;

/// <summary>
/// Adaptador fino sobre <c>user32.dll</c> para obtener las áreas de trabajo
/// reales de los monitores conectados. Deliberadamente sin lógica de
/// decisión (esa vive en <see cref="WindowPositionResolver"/>, xUnit puro) —
/// esta clase, junto con <c>MainWindow</c>, queda fuera de la cobertura
/// automatizada de este ciclo (no hay forma determinista de testear
/// monitores reales, ver Definition of Done).
/// </summary>
internal static class Win32ScreenInfo
{
    /// <summary>Enumera todos los monitores conectados y devuelve el área de trabajo (excluye la barra de tareas) de cada uno.</summary>
    public static IReadOnlyList<Rect> GetAllWorkAreas()
    {
        var workAreas = new List<Rect>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref RectNative _, IntPtr _) =>
        {
            var info = new MonitorInfo { CbSize = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                var wa = info.RcWork;
                workAreas.Add(new Rect(wa.Left, wa.Top, wa.Right - wa.Left, wa.Bottom - wa.Top));
            }
            return true;
        }, IntPtr.Zero);

        return workAreas;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RectNative lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int CbSize;
        public RectNative RcMonitor;
        public RectNative RcWork;
        public int DwFlags;
    }
}
