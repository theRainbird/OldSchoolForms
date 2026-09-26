using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Rendering;
using OldSchoolForms.Ui.Resources;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SkiaSharp;
using Svg.Skia;
using Key = Silk.NET.Input.Key;

namespace OldSchoolForms.Ui.Platform;

/// <summary>
/// Provides the platform abstraction layer using Silk.NET for window management and input,
/// and SkiaSharp for 2D rendering. Supports multiple simultaneous windows, each with its own
/// renderer and font renderer.
/// </summary>
public static class Platform
{
    private static readonly Dictionary<uint, WindowContext> _contexts = new();
    private static Form? _focusedWindow;
    private static Point _lastMousePosition;
    private static uint _nextWindowId = 1;
    private static readonly Dictionary<(MessageBoxIcon icon, uint windowId, int size), IGraphicsImage> _iconImageCache = new();
    private static IKeyboard? _keyboard;

    // Held-key auto-repeat. GLFW surfaces key repeat only through the char callback, so
    // non-textual editing keys (Backspace, Delete, arrows, Home/End) never re-fire KeyDown
    // while held. We synthesize the repeats here to match native text-editing behaviour.
    private const long KeyRepeatInitialDelayMs = 500;
    private const long KeyRepeatIntervalMs = 33;
    // Maximum seconds the render loop blocks in glfwWaitEventsTimeout when idle. The
    // thread sleeps until real input arrives; cross-thread invalidations (cursor blink,
    // animation timers) wake it earlier via PostEmptyEvent, so this only bounds the
    // worst-case wake-up for the rare case of a missed post.
    private const double IdleWaitSeconds = 1.0;
    private static Keys _repeatKey = Keys.None;
    private static ModifierKeys _repeatModifiers;
    private static long _repeatPressTicks;
    private static long _repeatLastTicks;
    private static Form? _repeatForm;

    private static readonly Dictionary<uint, List<Action>> _windowCleanupActions = new();
    private static readonly Dictionary<uint, ClickInfo> _lastClickInfo = new();

    private struct ClickInfo
    {
        public long Ticks;
        public int X;
        public int Y;
        public MouseButtons Button;
        public int Count;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GlfwGetWin32WindowDelegate(IntPtr window);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GlfwGetX11DisplayDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GlfwGetX11WindowDelegate(IntPtr window);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GlfwGetWaylandDisplayDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GlfwGetWaylandWindowDelegate(IntPtr window);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GlfwWaitEventsTimeoutDelegate(double timeout);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GlfwPostEmptyEventDelegate();

    private static readonly GlfwGetX11DisplayDelegate? _glfwGetX11Display = ResolveGlfwFunc<GlfwGetX11DisplayDelegate>("glfwGetX11Display");
    private static readonly GlfwGetX11WindowDelegate? _glfwGetX11Window = ResolveGlfwFunc<GlfwGetX11WindowDelegate>("glfwGetX11Window");
    private static readonly GlfwGetWaylandDisplayDelegate? _glfwGetWaylandDisplay = ResolveGlfwFunc<GlfwGetWaylandDisplayDelegate>("glfwGetWaylandDisplay");
    private static readonly GlfwGetWaylandWindowDelegate? _glfwGetWaylandWindow = ResolveGlfwFunc<GlfwGetWaylandWindowDelegate>("glfwGetWaylandWindow");

    // Resolved lazily rather than in the static initializer: Silk.NET loads the native
    // glfw library only when the first window/GL context is created, which happens
    // after this type's static initializer would otherwise run. Resolving on first use
    // (inside the render loop) guarantees the library is already mapped into the process.
    private static GlfwWaitEventsTimeoutDelegate? _glfwWaitEventsTimeout;
    private static GlfwPostEmptyEventDelegate? _glfwPostEmptyEvent;
    private static readonly object _glfwResolveLock = new();
    private static bool _waitEventsResolved;

    private static void EnsureWaitEventsResolved()
    {
        if (_waitEventsResolved)
            return;
        lock (_glfwResolveLock)
        {
            if (_waitEventsResolved)
                return;
            _glfwWaitEventsTimeout = ResolveGlfwFunc<GlfwWaitEventsTimeoutDelegate>("glfwWaitEventsTimeout");
            _glfwPostEmptyEvent = ResolveGlfwFunc<GlfwPostEmptyEventDelegate>("glfwPostEmptyEvent");
            _waitEventsResolved = true;
        }
    }

    // Managed id of the thread that owns the UI/render loop. Recorded by SetUiThread.
    // Cross-thread invalidations compare against this to decide whether to wake the loop.
    private static int _uiThreadId = -1;

    [DllImport("libc", EntryPoint = "dlopen", ExactSpelling = true)]
    private static extern IntPtr DlOpen(string? filename, int flags);

    [DllImport("libc", EntryPoint = "dlsym", ExactSpelling = true)]
    private static extern IntPtr DSym(IntPtr handle, string symbol);

    private const int RTLD_LAZY = 1;

    /// <summary>
    /// Called every frame before event processing. External components can hook here.
    /// </summary>
    public static Action? OnFrame { get; set; }

    /// <summary>
    /// Records the managed thread that owns the UI/render loop. Must be called from the
    /// thread that runs the application message loop before any window is created.
    /// Cross-thread invalidations use this to decide whether to wake the loop.
    /// </summary>
    public static void SetUiThread() => _uiThreadId = Environment.CurrentManagedThreadId;

    /// <summary>
    /// Gets whether the calling thread is the UI/render thread. Returns <c>true</c> when
    /// the UI thread has not been recorded yet, so pre-loop invalidations are treated as
    /// UI-thread work and never trigger a wake-up.
    /// </summary>
    public static bool IsOnUiThread => _uiThreadId < 0 || Environment.CurrentManagedThreadId == _uiThreadId;

    /// <summary>
    /// Posts an empty event to the windowing system to wake a render loop blocked in
    /// <c>glfwWaitEventsTimeout</c>. Used when invalidation originates off the UI thread
    /// (for example the cursor-blink timer or a control animation timer) so the pending
    /// dirty state is picked up promptly instead of after the wait timeout.
    /// </summary>
    public static void PostEmptyEvent()
    {
        try
        {
            EnsureWaitEventsResolved();
            _glfwPostEmptyEvent?.Invoke();
        }
        catch { }
    }

    /// <summary>
    /// Gets the WindowContext for the specified window ID, or null if not found.
    /// </summary>
    /// <param name="windowId">The window ID.</param>
    /// <returns>The WindowContext, or null if not found.</returns>
    internal static WindowContext? GetWindowContext(uint windowId)
    {
        return _contexts.TryGetValue(windowId, out var ctx) ? ctx : null;
    }

    /// <summary>
    /// Gets the WindowContext for the specified form, or null if not found.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The WindowContext, or null if not found.</returns>
    internal static WindowContext? GetWindowContext(Form form)
    {
        return _contexts.TryGetValue(form.WindowId, out var ctx) ? ctx : null;
    }

    /// <summary>
    /// Gets the native platform window handle for the specified form.
    /// On Windows this is an HWND, on X11 an X11 Window ID, on Wayland a wl_surface pointer.
    /// </summary>
    /// <param name="form">The form whose native handle to retrieve.</param>
    /// <returns>The native window handle, or <see cref="IntPtr.Zero"/> if not available.</returns>
    public static nint GetNativeWindowHandle(Form form)
    {
        var ctx = GetWindowContext(form);
        if (ctx == null)
            return nint.Zero;

        // On Windows, IView.Handle returns the GLFW window pointer, not the Win32 HWND.
        // WebView2 requires a real HWND. glfw3.dll is already loaded by Silk.NET into the
        // process; retrieve its module handle and resolve glfwGetWin32Window via GetProcAddress.
        // Direct DllImport("glfw3") fails because Silk.NET's native resolver only handles
        // calls from Silk.NET assemblies, not from OldSchoolForms.Ui.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var glfwModule = GetModuleHandle("glfw3.dll");
                if (glfwModule != IntPtr.Zero)
                {
                    var pFunc = GetProcAddress(glfwModule, "glfwGetWin32Window");
                    if (pFunc != IntPtr.Zero)
                    {
                        var func = Marshal.GetDelegateForFunctionPointer<GlfwGetWin32WindowDelegate>(pFunc);
                        var hwnd = func(ctx.Window.Handle);
                        if (hwnd != IntPtr.Zero)
                            return hwnd;
                    }
                }
            }
            catch
            {
                // Fall through to fallback
            }
        }

        // On Linux, resolve the real native handle via GLFW platform functions.
        // ctx.Window.Handle is the GLFW window pointer, not the X11 Window ID
        // or Wayland wl_surface pointer.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                if (IsWayland())
                {
                    if (_glfwGetWaylandWindow != null)
                    {
                        var wlSurface = _glfwGetWaylandWindow(ctx.Window.Handle);
                        if (wlSurface != IntPtr.Zero)
                            return wlSurface;
                    }
                }
                else
                {
                    if (_glfwGetX11Window != null)
                    {
                        var xid = _glfwGetX11Window(ctx.Window.Handle);
                        if (xid != IntPtr.Zero)
                            return xid;
                    }
                }
            }
            catch
            {
                // Fall through
            }
        }

        // Fallback: return the raw GLFW window pointer
        return ctx.Window.Handle;
    }

    // ResolveGlfwSetWindowAttrib and ResolveGlfwByProcMaps have been replaced
    // by the generic ResolveGlfwFunc<T> below.

    private static TDelegate? ResolveGlfwFunc<TDelegate>(string name) where TDelegate : class
    {
        IntPtr pFunc = IntPtr.Zero;
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var mod = GetModuleHandle("glfw3.dll");
                if (mod != IntPtr.Zero)
                    pFunc = GetProcAddress(mod, name);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var global = DlOpen(null, RTLD_LAZY);
                if (global != IntPtr.Zero)
                    pFunc = DSym(global, name);

                if (pFunc == IntPtr.Zero)
                    pFunc = ResolveGlfwSymbolByProcMaps(name);

                if (pFunc == IntPtr.Zero)
                {
                    string[] libNames = { "libglfw.so.3", "libglfw.so", "libglfw.so.3.4" };
                    foreach (var libName in libNames)
                    {
                        var lib = DlOpen(libName, RTLD_LAZY);
                        if (lib != IntPtr.Zero)
                        {
                            pFunc = DSym(lib, name);
                            if (pFunc != IntPtr.Zero)
                                break;
                        }
                    }
                }
            }
        }
        catch
        {
            return null;
        }

        if (pFunc != IntPtr.Zero)
            return Marshal.GetDelegateForFunctionPointer<TDelegate>(pFunc);
        return null;
    }

    private static IntPtr ResolveGlfwSymbolByProcMaps(string name)
    {
        try
        {
            var lines = File.ReadAllLines("/proc/self/maps");
            foreach (var line in lines)
            {
                if (line.Contains("libglfw", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 6)
                    {
                        var path = parts[5];
                        if (File.Exists(path))
                        {
                            var lib = DlOpen(path, RTLD_LAZY);
                            if (lib != IntPtr.Zero)
                            {
                                var sym = DSym(lib, name);
                                if (sym != IntPtr.Zero)
                                    return sym;
                            }
                        }
                    }
                }
            }
        }
        catch { }
        return IntPtr.Zero;
    }

    // ──────────────────────────────────────────────
    // X11 native functions (resolved via dlopen/dlsym)
    // ──────────────────────────────────────────────

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int XSetTransientForHintDelegate(IntPtr display, IntPtr window, IntPtr transientForWindow);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr XInternAtomDelegate(IntPtr display, IntPtr atomName, int onlyIfExists);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int XChangePropertyDelegate(IntPtr display, IntPtr window, IntPtr property, IntPtr type,
        int format, int mode, IntPtr data, int nelements);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int XDeletePropertyDelegate(IntPtr display, IntPtr window, IntPtr property);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int XFlushDelegate(IntPtr display);

    private static readonly XSetTransientForHintDelegate? _xSetTransientForHint = ResolveX11Func<XSetTransientForHintDelegate>("XSetTransientForHint");
    private static readonly XInternAtomDelegate? _xInternAtom = ResolveX11Func<XInternAtomDelegate>("XInternAtom");
    private static readonly XChangePropertyDelegate? _xChangeProperty = ResolveX11Func<XChangePropertyDelegate>("XChangeProperty");
    private static readonly XDeletePropertyDelegate? _xDeleteProperty = ResolveX11Func<XDeletePropertyDelegate>("XDeleteProperty");
    private static readonly XFlushDelegate? _xFlush = ResolveX11Func<XFlushDelegate>("XFlush");

    private const int PropModeReplace = 0;
    private const int NETWmStateModalAtomIndex = 0; // Index within _NET_WM_STATE

    private static TDelegate? ResolveX11Func<TDelegate>(string name) where TDelegate : class
    {
        IntPtr pFunc = IntPtr.Zero;
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string[] xlibNames = { "libX11.so.6", "libX11.so" };
                foreach (var libName in xlibNames)
                {
                    var lib = DlOpen(libName, RTLD_LAZY);
                    if (lib != IntPtr.Zero)
                    {
                        pFunc = DSym(lib, name);
                        if (pFunc != IntPtr.Zero)
                            break;
                    }
                }
            }
        }
        catch
        {
            return null;
        }
        if (pFunc != IntPtr.Zero)
            return Marshal.GetDelegateForFunctionPointer<TDelegate>(pFunc);
        return null;
    }

    // ──────────────────────────────────────────────
    // Windows native functions
    // ──────────────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool enable);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    private const int GWL_EXSTYLE = -20;
    private static readonly IntPtr WS_EX_DLGMODALFRAME = new IntPtr(0x00000001);

    // ──────────────────────────────────────────────
    // Modal relationship API
    // ──────────────────────────────────────────────

    /// <summary>
    /// Establishes a native modal relationship between a dialog and its owner.
    /// On Windows: disables the owner via EnableWindow + sets WS_EX_DLGMODALFRAME.
    /// On X11: sets XSetTransientForHint + _NET_WM_STATE_MODAL.
    /// On Wayland: no-op (overlay mode is used instead).
    /// </summary>
    internal static void EstablishModalRelationship(Form dialogForm, Form ownerForm, WindowContext dialogCtx, WindowContext ownerCtx)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var ownerHwnd = GetNativeWindowHandle(ownerForm);
                if (ownerHwnd != IntPtr.Zero)
                    EnableWindow(ownerHwnd, false);

                var dialogHwnd = GetNativeWindowHandle(dialogForm);
                if (dialogHwnd != IntPtr.Zero)
                {
                    var exStyle = GetWindowLongPtrW(dialogHwnd, GWL_EXSTYLE);
                    SetWindowLongPtrW(dialogHwnd, GWL_EXSTYLE, (IntPtr)((long)exStyle | (long)WS_EX_DLGMODALFRAME));
                }
            }
            catch { }
        }
        else if (IsX11())
        {
            try
            {
                if (_glfwGetX11Display == null || _glfwGetX11Window == null ||
                    _xSetTransientForHint == null || _xInternAtom == null ||
                    _xChangeProperty == null || _xFlush == null)
                    return;

                var display = _glfwGetX11Display();
                if (display == IntPtr.Zero)
                    return;

                var dialogXid = _glfwGetX11Window(dialogCtx.Window.Handle);
                var ownerXid = _glfwGetX11Window(ownerCtx.Window.Handle);
                if (dialogXid == IntPtr.Zero || ownerXid == IntPtr.Zero)
                    return;

                _xSetTransientForHint(display, dialogXid, ownerXid);

                // Set _NET_WM_STATE_MODAL
                var netWmStateAtom = InternAtom(display, "_NET_WM_STATE");
                var netWmStateModalAtom = InternAtom(display, "_NET_WM_STATE_MODAL");
                var atomTypeAtom = InternAtom(display, "ATOM");
                if (netWmStateAtom != IntPtr.Zero && netWmStateModalAtom != IntPtr.Zero && atomTypeAtom != IntPtr.Zero)
                {
                    // Allocate buffer for one Atom (8 bytes on 64-bit, format 32 = 4 bytes on wire)
                    var buffer = Marshal.AllocHGlobal(8);
                    try
                    {
                        Marshal.WriteIntPtr(buffer, netWmStateModalAtom);
                        _xChangeProperty(display, dialogXid, netWmStateAtom, atomTypeAtom,
                            32, PropModeReplace, buffer, 1);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }

                _xFlush(display);
            }
            catch { }
        }
    }

    /// <summary>
    /// Breaks a previously established modal relationship.
    /// Re-enables the owner on Windows, removes _NET_WM_STATE_MODAL on X11.
    /// </summary>
    internal static void BreakModalRelationship(Form dialogForm, Form ownerForm)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var ownerHwnd = GetNativeWindowHandle(ownerForm);
                if (ownerHwnd != IntPtr.Zero)
                    EnableWindow(ownerHwnd, true);

                var dialogHwnd = GetNativeWindowHandle(dialogForm);
                if (dialogHwnd != IntPtr.Zero)
                {
                    var exStyle = GetWindowLongPtrW(dialogHwnd, GWL_EXSTYLE);
                    SetWindowLongPtrW(dialogHwnd, GWL_EXSTYLE, (IntPtr)((long)exStyle & ~(long)WS_EX_DLGMODALFRAME));
                }
            }
            catch { }
        }
        else if (IsX11())
        {
            try
            {
                if (_glfwGetX11Display == null || _glfwGetX11Window == null ||
                    _xDeleteProperty == null || _xInternAtom == null ||
                    _xFlush == null)
                    return;

                var display = _glfwGetX11Display();
                if (display == IntPtr.Zero)
                    return;

                var dialogCtx = GetWindowContext(dialogForm);
                if (dialogCtx == null)
                    return;

                var dialogXid = _glfwGetX11Window(dialogCtx.Window.Handle);
                if (dialogXid == IntPtr.Zero)
                    return;

                var netWmStateAtom = InternAtom(display, "_NET_WM_STATE");
                if (netWmStateAtom != IntPtr.Zero)
                    _xDeleteProperty(display, dialogXid, netWmStateAtom);

                _xFlush(display);
            }
            catch { }
        }
    }

    private static IntPtr InternAtom(IntPtr display, string name)
    {
        if (_xInternAtom == null) return IntPtr.Zero;
        var ptr = Marshal.StringToCoTaskMemAnsi(name);
        try
        {
            return _xInternAtom(display, ptr, 0);
        }
        finally
        {
            Marshal.FreeCoTaskMem(ptr);
        }
    }

    // RegisterModal, UnregisterModal, SetModalFloating, CleanupModalMap replaced
    // by EstablishModalRelationship / BreakModalRelationship (above).
    // GLFW_FLOATING is no longer used — modality is handled via native platform
    // APIs (Windows: EnableWindow, X11: XSetTransientForHint + _NET_WM_STATE_MODAL,
    // Wayland: in-window overlay).

    /// <summary>
    /// Determines whether the current Linux session uses Wayland.
    /// </summary>
    internal static bool IsWayland()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return false;
        var session = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "";
        return session.Equals("wayland", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the current Linux session uses X11.
    /// </summary>
    internal static bool IsX11()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return false;
        var session = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "";
        return session.Equals("x11", StringComparison.OrdinalIgnoreCase) ||
               session.Equals("tty", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true when modal dialogs should use the in-window overlay approach
    /// instead of separate native windows. Required on Wayland where GLFW cannot
    /// programmatically manage focus or Z-order between windows.
    /// </summary>
    internal static bool ShouldUseOverlay()
    {
        // Wayland: GLFW's glfwFocusWindow is a no-op and GLFW_FLOATING only works
        // on wlroots compositors. The overlay approach works reliably everywhere.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && IsWayland())
            return true;
        return false;
    }

    /// <summary>
    /// Creates a new window for the specified form.
    /// The renderer is initialized asynchronously when the window loads.
    /// </summary>
    /// <param name="form">The form to create a window for.</param>
    /// <returns>A unique handle identifier for the window.</returns>
    /// <exception cref="InvalidOperationException">Thrown when window creation fails.</exception>
    public static IntPtr CreateWindow(Form form)
    {
        var options = WindowOptions.Default;
        options.Title = form.Text ?? string.Empty;
        options.Size = new Vector2D<int>(form.Width > 0 ? (int)(form.Width * form.Zoom) : 800, form.Height > 0 ? (int)(form.Height * form.Zoom) : 600);
        options.IsVisible = true;
        options.ShouldSwapAutomatically = false;
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));
        options.PreferredDepthBufferBits = 24;
        options.Samples = 0;

        if (form.FormBorderStyle == FormBorderStyle.FixedSingle ||
            form.FormBorderStyle == FormBorderStyle.None ||
            form.FormBorderStyle == FormBorderStyle.FixedDialog ||
            form.FormBorderStyle == FormBorderStyle.Fixed3D)
        {
            options.WindowBorder = WindowBorder.Fixed;
        }
        else
        {
            options.WindowBorder = WindowBorder.Resizable;
        }

        var window = Window.Create(options);

        uint windowId = _nextWindowId++;
        form.WindowId = windowId;

        var ctx = new WindowContext(window, windowId, form);
        _contexts[windowId] = ctx;
        _focusedWindow = form;

        var handle = new IntPtr(windowId);
        form.SetHandle(handle);

        window.Load += () =>
        {
            var input = window.CreateInput();
            var keyboard = input.Keyboards.FirstOrDefault();
            var mouse = input.Mice.FirstOrDefault();
            _keyboard = keyboard;

            var cleanup = new List<Action>();
            _windowCleanupActions[windowId] = cleanup;

            Action<Vector2D<int>> resize = size =>
            {
                if (size.X > 0 && size.Y > 0)
                {
                    form.SuspendLayout();
                    form.Width = (int)MathF.Ceiling(size.X / form.Zoom);
                    form.Height = (int)MathF.Ceiling(size.Y / form.Zoom);
                    form.ResumeLayout(true);
                    form.OnResize(EventArgs.Empty);
                    form.Invalidate();
                }
            };
            window.Resize += resize;
            cleanup.Add(() => window.Resize -= resize);

            if (keyboard != null)
            {
                Action<IKeyboard, Key, int> keyDown = (kb, key, keyCode) =>
                {
                    if (_focusedWindow == null) return;
                    var args = new KeyEventArgs
                    {
                        KeyCode = MapKeyCode(key),
                        Modifiers = MapModifierKeys(keyboard)
                    };
                    _focusedWindow.OnKeyDown(args);

                    if (IsAutoRepeatKey(args.KeyCode))
                    {
                        _repeatKey = args.KeyCode;
                        _repeatModifiers = args.Modifiers;
                        _repeatPressTicks = Environment.TickCount64;
                        _repeatLastTicks = _repeatPressTicks;
                        _repeatForm = _focusedWindow;
                    }
                    else
                    {
                        _repeatKey = Keys.None;
                        _repeatForm = null;
                    }
                };
                keyboard.KeyDown += keyDown;
                cleanup.Add(() => keyboard.KeyDown -= keyDown);

                Action<IKeyboard, Key, int> keyUp = (kb, key, keyCode) =>
                {
                    if (_focusedWindow == null) return;
                    var args = new KeyEventArgs
                    {
                        KeyCode = MapKeyCode(key),
                        Modifiers = MapModifierKeys(keyboard)
                    };
                    _focusedWindow.OnKeyUp(args);

                    if (args.KeyCode == _repeatKey)
                    {
                        _repeatKey = Keys.None;
                        _repeatForm = null;
                    }
                };
                keyboard.KeyUp += keyUp;
                cleanup.Add(() => keyboard.KeyUp -= keyUp);

                Action<IKeyboard, char> keyChar = (kb, ch) =>
                {
                    if (_focusedWindow == null) return;
                    _focusedWindow.OnTextInput(ch.ToString());
                };
                keyboard.KeyChar += keyChar;
                cleanup.Add(() => keyboard.KeyChar -= keyChar);
            }
            if (mouse != null)
            {
                Action<IMouse, MouseButton> mouseDown = (m, button) =>
                {
                    // Use 'm' (the IMouse parameter) not the captured variable — Silk.NET may
                    // recreate the IMouse object after focus transitions (Wayland/GLFW).
                    var pos = m.Position;
                    float zoom = form.Zoom;
                    var point = new Point((int)(pos.X / zoom), (int)(pos.Y / zoom));
                    var btn = MapMouseButton(button);
                    int clicks = 1;
                    long now = Environment.TickCount64;
                    if (_lastClickInfo.TryGetValue(windowId, out var last))
                    {
                        int dx = point.X - last.X;
                        int dy = point.Y - last.Y;
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        long dt = now - last.Ticks;
                        if (btn == last.Button && dt <= SystemInformation.DoubleClickTime && dist <= SystemInformation.DoubleClickSize)
                        {
                            clicks = last.Count + 1;
                        }
                    }
                    _lastClickInfo[windowId] = new ClickInfo { Ticks = now, X = point.X, Y = point.Y, Button = btn, Count = clicks };
                    var args = new MouseEventArgs(btn, clicks, point.X, point.Y, 0);
                    form.OnMouseDown(args);
                };
                mouse.MouseDown += mouseDown;
                cleanup.Add(() => mouse.MouseDown -= mouseDown);

                Action<IMouse, MouseButton> mouseUp = (m, button) =>
                {
                    var pos = m.Position;
                    float zoom = form.Zoom;
                    var point = new Point((int)(pos.X / zoom), (int)(pos.Y / zoom));
                    var btn = MapMouseButton(button);
                    var args = new MouseEventArgs(btn, 1, point.X, point.Y, 0);
                    form.OnMouseUp(args);
                };
                mouse.MouseUp += mouseUp;
                cleanup.Add(() => mouse.MouseUp -= mouseUp);

                Action<IMouse, System.Numerics.Vector2> mouseMove = (m, pos) =>
                {
                    float zoom = form.Zoom;
                    var point = new Point((int)(pos.X / zoom), (int)(pos.Y / zoom));
                    _lastMousePosition = point;
                    form.Cursor = null;
                    var args = new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0);
                    form.OnMouseMove(args);
                    ApplyFormCursor(form, m);
                };
                mouse.MouseMove += mouseMove;
                cleanup.Add(() => mouse.MouseMove -= mouseMove);

                Action<IMouse, ScrollWheel> scroll = (m, wheel) =>
                {
                    float zoom = form.Zoom;
                    var point = new Point((int)(m.Position.X / zoom), (int)(m.Position.Y / zoom));
                    var args = new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, wheel.Y);
                    form.OnMouseWheel(args);
                };
                mouse.Scroll += scroll;
                cleanup.Add(() => mouse.Scroll -= scroll);
            }

            Action closing = () => ctx.IsClosing = true;
            window.Closing += closing;
            cleanup.Add(() => window.Closing -= closing);

            Action<bool> focusChanged = focused =>
            {
                if (focused)
                {
                    // Always trigger a re-render when focus is regained (Alt+Tab back, etc.)
                    form.Invalidate();

                    // Force at least one render pass regardless of IsVisible state.
                    // After Alt+Tab the compositor may lag and IsVisible can still be false
                    // even though the window has focus. ForceRender ensures the window renders
                    // and is cleared after one successful render.
                    if (_contexts.TryGetValue(windowId, out var ctx))
                        ctx.ForceRender = true;

                    _focusedWindow = form;
                    form.Focused = true;
                    form.OnGotFocus(EventArgs.Empty);
                }
                else
                {
                    form.Focused = false;
                    form.OnLostFocus(EventArgs.Empty);
                    // Clear the global focused window on focus loss to prevent stale references.
                    // Without this, keyboard events may be routed to a window that no longer has focus.
                    if (_focusedWindow == form)
                        _focusedWindow = null;
                }
            };
            window.FocusChanged += focusChanged;
            cleanup.Add(() => window.FocusChanged -= focusChanged);

            Action<Vector2D<int>> move = pos =>
            {
                form.X = pos.X;
                form.Y = pos.Y;
            };
            window.Move += move;
            cleanup.Add(() => window.Move -= move);

            Action<WindowState> stateChanged = state =>
            {
                form.WindowState = state switch
                {
                    Silk.NET.Windowing.WindowState.Minimized => FormWindowState.Minimized,
                    Silk.NET.Windowing.WindowState.Maximized => FormWindowState.Maximized,
                    _ => FormWindowState.Normal
                };
                form.Invalidate();
            };
            window.StateChanged += stateChanged;
            cleanup.Add(() => window.StateChanged -= stateChanged);

            try { ctx.InitializeRenderer(); } catch { }
        };

        window.Initialize();

        return handle;
    }

    private static WindowContext? GetFormContext(Form form)
    {
        return _contexts.TryGetValue(form.WindowId, out var ctx) ? ctx : null;
    }

    /// <summary>
    /// Destroys the window with the specified handle and releases its rendering resources.
    /// Called when a form is programmatically closed (e.g., MessageBox button click).
    /// The GL context is still valid here, so we can do full cleanup.
    /// </summary>
    /// <param name="handle">The window handle identifier.</param>
    public static void DestroyWindow(IntPtr handle)
    {
        uint windowId = (uint)handle;
        if (!_contexts.TryGetValue(windowId, out var ctx))
            return;

        CleanupWindowOnClose(windowId, ctx, ctx.Form, glCleanup: true);

        try { ctx.Window.IsVisible = false; } catch { }
        try { ctx.Window.Close(); } catch { }
    }

    /// <summary>
    /// Performs cleanup for a closing window. When glCleanup is true, GL resources are
    /// cleaned up (safe for programmatic close). When false, only non-GL cleanup is done
    /// (safe for deferred cleanup from the Closing event).
    /// </summary>
    private static void CleanupWindowOnClose(uint windowId, WindowContext ctx, Form form, bool glCleanup)
    {
        if (_windowCleanupActions.TryGetValue(windowId, out var cleanup))
        {
            foreach (var action in cleanup)
                action();
            _windowCleanupActions.Remove(windowId);
        }

        ctx.IsClosing = true;
        _contexts.Remove(windowId);

        if (_focusedWindow == form)
        {
            _focusedWindow = _contexts.Count > 0
                ? _contexts.Values.FirstOrDefault()?.Form
                : null;
        }

        CleanupIconImages(windowId);

        if (glCleanup)
        {
            // Programmatic close: GL context is still valid, clean up GL resources
            try { ctx.Window.MakeCurrent(); } catch { }
        }
        else
        {
            // Deferred close (from Closing event): GL context may be invalid, skip GL calls
            try { ctx.Renderer?.MarkContextLost(); } catch { }
        }

        try { ctx.Dispose(); } catch { }

        form.OnFormClosing(new FormClosingEventArgs(CloseReason.UserClosing, false));
        Application.Instance.UnregisterForm(form);
        form.SetHandle(IntPtr.Zero);
    }

    /// <summary>
    /// Sets the title of the window with the specified handle.
    /// </summary>
    /// <param name="handle">The window handle identifier.</param>
    /// <param name="title">The new title.</param>
    public static void SetWindowTitle(IntPtr handle, string title)
    {
        uint windowId = (uint)handle;
        if (_contexts.TryGetValue(windowId, out var ctx))
        {
            ctx.Window.Title = title;
        }
    }

    /// <summary>
    /// Minimizes the window with the specified handle.
    /// </summary>
    /// <param name="handle">The window handle identifier.</param>
    public static void MinimizeWindow(IntPtr handle)
    {
        uint windowId = (uint)handle;
        if (_contexts.TryGetValue(windowId, out var ctx))
        {
            ctx.Window.WindowState = WindowState.Minimized;
        }
    }

    /// <summary>
    /// Maximizes the window with the specified handle.
    /// </summary>
    /// <param name="handle">The window handle identifier.</param>
    public static void MaximizeWindow(IntPtr handle)
    {
        uint windowId = (uint)handle;
        if (_contexts.TryGetValue(windowId, out var ctx))
        {
            ctx.Window.WindowState = WindowState.Maximized;
        }
    }

    /// <summary>
    /// Restores the window with the specified handle to its normal state.
    /// </summary>
    /// <param name="handle">The window handle identifier.</param>
    public static void RestoreWindow(IntPtr handle)
    {
        uint windowId = (uint)handle;
        if (_contexts.TryGetValue(windowId, out var ctx))
        {
            ctx.Window.WindowState = WindowState.Normal;
        }
    }

    /// <summary>
    /// Moves the window to the specified position.
    /// </summary>
    /// <param name="handle">The window handle identifier.</param>
    /// <param name="x">The new x-coordinate.</param>
    /// <param name="y">The new y-coordinate.</param>
    public static void MoveWindow(IntPtr handle, int x, int y)
    {
        uint windowId = (uint)handle;
        if (_contexts.TryGetValue(windowId, out var ctx))
        {
            ctx.Window.Position = new Vector2D<int>(x, y);
        }
    }

    /// <summary>
    /// Resizes the window to the specified dimensions.
    /// </summary>
    /// <param name="handle">The window handle identifier.</param>
    /// <param name="width">The new width.</param>
    /// <param name="height">The new height.</param>
    public static void ResizeWindow(IntPtr handle, int width, int height)
    {
        uint windowId = (uint)handle;
        if (_contexts.TryGetValue(windowId, out var ctx))
        {
            ctx.Window.Size = new Vector2D<int>(width, height);
        }
    }

    /// <summary>
    /// Gets the position of the window.
    /// </summary>
    /// <param name="handle">The window handle identifier.</param>
    /// <returns>A tuple containing the x and y coordinates.</returns>
    public static (int x, int y) GetWindowPosition(IntPtr handle)
    {
        uint windowId = (uint)handle;
        if (_contexts.TryGetValue(windowId, out var ctx))
        {
            var pos = ctx.Window.Position;
            return (pos.X, pos.Y);
        }
        return (0, 0);
    }

    /// <summary>
    /// Gets the size of the window.
    /// </summary>
    /// <param name="handle">The window handle identifier.</param>
    /// <returns>A tuple containing the width and height.</returns>
    public static (int width, int height) GetWindowSize(IntPtr handle)
    {
        uint windowId = (uint)handle;
        if (_contexts.TryGetValue(windowId, out var ctx))
        {
            var size = ctx.Window.Size;
            return (size.X, size.Y);
        }
        return (0, 0);
    }

    /// <summary>
    /// Gets the window state of the window.
    /// </summary>
    /// <param name="handle">The window handle identifier.</param>
    /// <returns>The current window state.</returns>
    public static FormWindowState GetWindowState(IntPtr handle)
    {
        uint windowId = (uint)handle;
        if (_contexts.TryGetValue(windowId, out var ctx))
        {
            return ctx.Window.WindowState switch
            {
                WindowState.Minimized => FormWindowState.Minimized,
                WindowState.Maximized => FormWindowState.Maximized,
                _ => FormWindowState.Normal
            };
        }
        return FormWindowState.Normal;
    }

    /// <summary>
    /// Brings the window to the front of the z-order.
    /// </summary>
    /// <param name="handle">The window handle identifier.</param>
    public static void BringToFront(IntPtr handle)
    {
        uint windowId = (uint)handle;
        if (_contexts.TryGetValue(windowId, out var ctx))
        {
            try { ctx.Window.Focus(); } catch { }
        }
    }

    /// <summary>
    /// Gets the currently focused window.
    /// </summary>
    public static Form? FocusedWindow => _focusedWindow;

    /// <summary>
    /// Measures the dimensions of text using the font renderer of the specified window.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="font">The font to use.</param>
    /// <returns>A tuple containing the width and height.</returns>
    public static (int width, int height) MeasureText(string text, Core.Font font)
    {
        return MeasureText(text, font, 1.0f);
    }

    /// <summary>
    /// Measures the dimensions of text using the font renderer of the specified window with zoom scaling.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="font">The font to use.</param>
    /// <param name="zoom">The zoom factor for scaling.</param>
    /// <returns>A tuple containing the width and height.</returns>
    public static (int width, int height) MeasureText(string text, Core.Font font, float zoom)
    {
        var ctx = _contexts.Values.FirstOrDefault(c => c.IsInitialized);
        if (ctx == null || ctx.FontRenderer == null)
            return (0, 0);
        return ctx.FontRenderer.MeasureText(text, font, zoom);
    }

    /// <summary>
    /// Sets whether the window has borders.
    /// </summary>
    /// <param name="handle">The window handle identifier.</param>
    /// <param name="bordered">Whether the window should have borders.</param>
    public static void SetBordered(IntPtr handle, bool bordered)
    {
        uint windowId = (uint)handle;
        if (_contexts.TryGetValue(windowId, out var ctx))
        {
            ctx.Window.WindowBorder = bordered ? WindowBorder.Fixed : WindowBorder.Hidden;
        }
    }

    /// <summary>
    /// Loads a message box icon as an IGraphicsImage for the specified window.
    /// Uses Svg.Skia for resolution-independent SVG icons with alpha transparency.
    /// The image is cached per (icon, windowId, size) pair.
    /// </summary>
    /// <param name="icon">The message box icon type to load.</param>
    /// <param name="windowId">The window ID to associate the icon with.</param>
    /// <param name="size">The desired icon size in pixels. Default is 48.</param>
    /// <returns>The IGraphicsImage, or null if the icon could not be loaded.</returns>
    public static IGraphicsImage? LoadMessageBoxIcon(MessageBoxIcon icon, uint windowId, int size = 48)
    {
        if (icon == MessageBoxIcon.None)
            return null;

        var key = (icon, windowId, size);
        if (_iconImageCache.TryGetValue(key, out var cached))
            return cached;

        string? resourceName = GetIconResourceName(icon);
        if (resourceName == null)
            return null;

        var image = LoadSvgResource(resourceName, size);
        if (image != null)
        {
            _iconImageCache[key] = image;
        }

        return image;
    }

    /// <summary>
    /// Loads an SVG resource from an embedded resource and renders it to an IGraphicsImage
    /// at the specified pixel size with full alpha transparency support.
    /// Uses Svg.Skia for pure C# SVG rasterization.
    /// </summary>
    /// <param name="resourceName">The manifest resource name of the SVG file.</param>
    /// <param name="size">The desired width/height in pixels.</param>
    /// <returns>The IGraphicsImage, or null if loading failed.</returns>
    public static IGraphicsImage? LoadSvgResource(string resourceName, int size)
    {
        try
        {
            var assembly = typeof(Platform).Assembly;
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;
            return SvgImage.FromSvgStream(stream, size);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads a raster image resource (PNG, JPEG, BMP, etc.) from an embedded resource.
    /// Supports alpha transparency for cutout images.
    /// </summary>
    /// <param name="resourceName">The manifest resource name of the image file.</param>
    /// <returns>The IGraphicsImage, or null if loading failed.</returns>
    public static IGraphicsImage? LoadRasterImageResource(string resourceName)
    {
        return RasterImage.FromResource(resourceName);
    }

    private static string? GetIconResourceName(MessageBoxIcon icon)
    {
        return icon switch
        {
            MessageBoxIcon.Information => "OldSchoolForms.Ui.Resources.Icons.SvgRepo.info.svg",
            MessageBoxIcon.Warning => "OldSchoolForms.Ui.Resources.Icons.FluentColor.ic_fluent_warning_48_color.svg",
            MessageBoxIcon.Error => "OldSchoolForms.Ui.Resources.Icons.FluentColor.ic_fluent_error_circle_48_color.svg",
            MessageBoxIcon.Question => "OldSchoolForms.Ui.Resources.Icons.FluentColor.ic_fluent_question_circle_48_color.svg",
            _ => null
        };
    }

    /// <summary>
    /// Cleans up icon images associated with the specified window ID.
    /// Called when a window is being destroyed.
    /// </summary>
    /// <param name="windowId">The window ID whose icon images should be cleaned up.</param>
    internal static void CleanupIconImages(uint windowId)
    {
        var keysToRemove = new List<(MessageBoxIcon, uint, int)>();
        foreach (var kvp in _iconImageCache)
        {
            if (kvp.Key.Item2 == windowId)
            {
                kvp.Value?.Dispose();
                keysToRemove.Add(kvp.Key);
            }
        }
        foreach (var key in keysToRemove)
        {
            _iconImageCache.Remove(key);
        }
    }

    /// <summary>
    /// Processes all pending events and renders all active windows.
    /// Called by the application's main loop.
    /// </summary>
    /// <param name="app">The application instance.</param>
    public static void ProcessEvents(Application app)
    {
        OnFrame?.Invoke();
        EnsureWaitEventsResolved();
        var contextsSnapshot = _contexts.Values.ToList();
        var pendingCleanup = new List<WindowContext>();

        foreach (var ctx in contextsSnapshot)
        {
            if (ctx.IsClosing)
            {
                if (_contexts.ContainsKey(ctx.WindowId))
                    pendingCleanup.Add(ctx);
                continue;
            }

            try
            {
                ctx.DoEvents();
            }
            catch { }
            if (ctx.IsClosing && _contexts.ContainsKey(ctx.WindowId))
                pendingCleanup.Add(ctx);
        }

        // Note: Old modal workarounds (skip DoEvents for owners, deferred focus redirect,
        // GLFW_FLOATING, _modalOwnerMap tracking) have been removed. Modal dialogs are now
        // handled via native platform APIs on X11/Windows (EnableWindow, XSetTransientForHint +
        // _NET_WM_STATE_MODAL) and via in-window overlay on Wayland (DialogOverlay). The WM/
        // compositor manages window Z-order and event routing for native mode; the overlay
        // intercepts all input for Wayland mode.

        foreach (var ctx in pendingCleanup)
        {
            CleanupWindowOnClose(ctx.WindowId, ctx, ctx.Form, glCleanup: false);
            // Actually close and hide the Silk.NET window (CleanupWindowOnClose only removes
            // from _contexts and disposes renderers, but does not destroy the native window)
            try { ctx.Window.IsVisible = false; } catch { }
            try { ctx.Window.Close(); } catch { }
        }

        // Synthesize auto-repeat for held editing/navigation keys (GLFW does not re-raise
        // KeyDown for these). Runs on the same thread that pumps DoEvents above.
        if (_repeatForm != null)
        {
            long repeatNow = Environment.TickCount64;
            if (_focusedWindow != _repeatForm)
            {
                _repeatKey = Keys.None;
                _repeatForm = null;
            }
            else if (repeatNow - _repeatPressTicks >= KeyRepeatInitialDelayMs &&
                     repeatNow - _repeatLastTicks >= KeyRepeatIntervalMs)
            {
                _repeatLastTicks = repeatNow;
                try
                {
                    _repeatForm.OnKeyDown(new KeyEventArgs
                    {
                        KeyCode = _repeatKey,
                        Modifiers = _repeatModifiers
                    });
                }
                catch { }
            }
        }

        // Render only windows that are dirty or explicitly forced. The same skip rules
        // are applied here as for the wait decision below, so a minimized or hidden
        // window that happens to be dirty cannot drive a zero-timeout busy loop.
        foreach (var ctx in _contexts.Values.ToList())
        {
            if (ctx.IsClosing || !ctx.IsInitialized || ctx.Renderer == null || ctx.FontRenderer == null)
                continue;

            var form = ctx.Form;

            // Skip minimized windows
            if (form.WindowState == FormWindowState.Minimized)
                continue;

            // Skip hidden windows — SwapBuffers can block indefinitely on some Linux
            // GPU drivers when the window is not on the current virtual desktop or
            // is fully obscured by other windows. GLFW does not fire any event to
            // notify us, so we must check IsVisible explicitly. ForceRender overrides
            // this check to ensure at least one render pass after focus transitions.
            if (!ctx.Window.IsVisible && !ctx.ForceRender)
                continue;

            // Dirty gate: skip the full clear + command-list rebuild + swap when nothing
            // changed since the last render. This is the primary idle-CPU optimization.
            if (!ctx.ForceRender && !form.RequiresRender)
                continue;

            try
            {
                ctx.Window.MakeCurrent();
                RenderForm(ctx);
                ctx.Window.SwapBuffers();
                // Clear ForceRender after a successful render pass.
                ctx.ForceRender = false;
            }
            catch
            {
                // Clear ForceRender even on error to avoid infinite forced renders.
                ctx.ForceRender = false;
            }
        }

        // Event-driven pacing instead of busy-polling at a fixed frame rate. Block until
        // the windowing system reports input. A held auto-repeat key shortens the wait so
        // synthesized repeats keep firing; a still-pending render (e.g. an invalidate
        // raised from within a render handler) forces an immediate next pass; otherwise the
        // thread sleeps until an event or the idle timeout. Cross-thread invalidations
        // (cursor blink, animation timers) wake the wait early via PostEmptyEvent.
        double waitSeconds;
        if (_repeatForm != null)
            waitSeconds = KeyRepeatIntervalMs / 1000.0;
        else if (HasPendingRender())
            waitSeconds = 0.0;
        else
            waitSeconds = IdleWaitSeconds;

        if (waitSeconds > 0.0)
        {
            if (_glfwWaitEventsTimeout != null)
            {
                try { _glfwWaitEventsTimeout(waitSeconds); }
                catch { Thread.Sleep((int)(waitSeconds * 1000)); }
            }
            else
            {
                Thread.Sleep((int)(waitSeconds * 1000));
            }
        }
        else if (_glfwWaitEventsTimeout == null && _contexts.Count > 0)
        {
            // No event-wait available and work is pending: yield briefly so the loop
            // keeps draining the event queue without spinning hot on the CPU.
            Thread.Sleep(1);
        }
    }

    /// <summary>
    /// Determines whether any renderable window still has pending work (dirty or forced),
    /// applying the same minimized/hidden skip rules used by the render pass.
    /// </summary>
    /// <returns><c>true</c> if a renderable window needs to be drawn; otherwise <c>false</c>.</returns>
    private static bool HasPendingRender()
    {
        foreach (var ctx in _contexts.Values)
        {
            if (ctx.IsClosing || !ctx.IsInitialized)
                continue;
            if (ctx.Form.WindowState == FormWindowState.Minimized)
                continue;
            if (!ctx.Window.IsVisible && !ctx.ForceRender)
                continue;
            if (ctx.ForceRender || ctx.Form.RequiresRender)
                return true;
        }
        return false;
    }


    private static void RenderForm(WindowContext ctx)
    {
        var form = ctx.Form;
        var renderer = ctx.Renderer!;
        var fontRenderer = ctx.FontRenderer!;

        form.ClearRenderFlag();
        // Pre-clear the dirty tree before painting. Controls that override Render
        // without calling base never clear their own dirty flag; without this pass
        // they would keep the form permanently dirty and drive a busy render loop.
        // Running before the paint pass preserves any Invalidate raised during render.
        form.ClearDirtyTree();
        renderer.Clear(form.BackColor);

        using var g = new Graphics();
        g.Zoom = form.Zoom;
        g.MeasureText = (text, font, zoom) => fontRenderer.MeasureText(text, font, zoom);
        form.Render(g);

        foreach (var cmd in g.GetCommands())
        {
            ExecuteDrawCommand(cmd, renderer, fontRenderer);
        }

        using var gOverlay = new Graphics();
        gOverlay.Zoom = form.Zoom;
        gOverlay.MeasureText = (text, font, zoom) => fontRenderer.MeasureText(text, font, zoom);
        form.RenderOverlay(gOverlay);

        foreach (var cmd in gOverlay.GetCommands())
        {
            ExecuteDrawCommand(cmd, renderer, fontRenderer);
        }

        renderer.Present();
    }

    private static void ExecuteDrawCommand(DrawCommand cmd, SkiaRenderer renderer, SkiaFontRenderer fontRenderer)
    {
        renderer.SetClipRect(GetCommandClip(cmd));

        switch (cmd.Type)
        {
            case DrawCommandType.FillRectangle:
                renderer.FillRectangle(cmd.Color, cmd.X, cmd.Y, cmd.Width, cmd.Height);
                break;
            case DrawCommandType.DrawRectangle:
                renderer.DrawRectangle(cmd.Color, cmd.X, cmd.Y, cmd.Width, cmd.Height, cmd.LineWidth);
                break;
            case DrawCommandType.DrawLine:
                renderer.DrawLine(cmd.Color, cmd.X, cmd.Y, cmd.X2, cmd.Y2, cmd.LineWidth, cmd.DashPattern);
                break;
            case DrawCommandType.DrawString:
                if (!string.IsNullOrEmpty(cmd.Text) && cmd.Font != null)
                {
                    fontRenderer.DrawText(cmd.Text, cmd.Font, cmd.Color, cmd.X, cmd.Y, renderer.Canvas, cmd.Zoom);
                }
                break;
            case DrawCommandType.FillTriangle:
                renderer.FillTriangle(cmd.Color, cmd.X, cmd.Y, cmd.X2, cmd.Y2, cmd.X3, cmd.Y3);
                break;
            case DrawCommandType.FillPie:
                renderer.FillPie(cmd.Color, cmd.X, cmd.Y, cmd.Width, cmd.Height, cmd.StartAngle, cmd.SweepAngle);
                break;
            case DrawCommandType.DrawArc:
                renderer.DrawArc(cmd.Color, cmd.X, cmd.Y, cmd.Width, cmd.Height, cmd.StartAngle, cmd.SweepAngle, cmd.LineWidth);
                break;
            case DrawCommandType.FillPolygon:
                if (cmd.Points != null)
                    renderer.FillPolygon(cmd.Color, cmd.Points);
                break;
            case DrawCommandType.DrawPolygon:
                if (cmd.Points != null)
                    renderer.DrawPolygon(cmd.Color, cmd.Points, cmd.LineWidth);
                break;
            case DrawCommandType.FillEllipse:
                renderer.FillEllipse(cmd.Color, cmd.X, cmd.Y, cmd.Width, cmd.Height);
                break;
            case DrawCommandType.DrawEllipse:
                renderer.DrawEllipse(cmd.Color, cmd.X, cmd.Y, cmd.Width, cmd.Height, cmd.LineWidth);
                break;
            case DrawCommandType.DrawImage:
                if (cmd.Image?.NativeImage != null)
                {
                    var skImage = cmd.Image.NativeImage;
                    var drawX = cmd.X;
                    var drawY = cmd.Y;
                    var drawW = cmd.Width;
                    var drawH = cmd.Height;

                    if (cmd.Image is SvgImage svgImage)
                    {
                        int targetW = Math.Max(1, (int)Math.Ceiling(cmd.Width));
                        int targetH = Math.Max(1, (int)Math.Ceiling(cmd.Height));
                        var rasterized = svgImage.GetRasterized(targetW, targetH);
                        if (rasterized != null)
                        {
                            skImage = rasterized;
                            drawX = MathF.Round(cmd.X);
                            drawY = MathF.Round(cmd.Y);
                            drawW = targetW;
                            drawH = targetH;
                        }
                    }

                    renderer.DrawImage(skImage, drawX, drawY, drawW, drawH);
                }
                break;
        }
    }

    private static Core.Rectangle? GetCommandClip(DrawCommand cmd)
    {
        if (!cmd.ClipBounds.HasValue) return null;

        if (IsStrokeCommand(cmd.Type))
        {
            int inflate = (int)Math.Ceiling(cmd.LineWidth / 2f);
            var r = cmd.ClipBounds.Value;
            return new Core.Rectangle(r.X - inflate, r.Y - inflate, r.Width + 2 * inflate, r.Height + 2 * inflate);
        }

        return cmd.ClipBounds;
    }

    private static bool IsStrokeCommand(DrawCommandType type) => type switch
    {
        DrawCommandType.DrawRectangle => true,
        DrawCommandType.DrawLine => true,
        DrawCommandType.DrawArc => true,
        DrawCommandType.DrawPolygon => true,
        DrawCommandType.DrawEllipse => true,
        _ => false
    };

    /// <summary>
    /// Determines whether a key should auto-repeat while held. Textual keys repeat via the
    /// char callback; this covers the non-textual editing/navigation keys that GLFW does not
    /// re-surface as <c>KeyDown</c>.
    /// </summary>
    /// <param name="key">The mapped key to test.</param>
    /// <returns><c>true</c> if the key should auto-repeat while held; otherwise <c>false</c>.</returns>
    private static bool IsAutoRepeatKey(Core.Keys key)
    {
        return key switch
        {
            Core.Keys.Back => true,
            Core.Keys.Delete => true,
            Core.Keys.Left => true,
            Core.Keys.Right => true,
            Core.Keys.Up => true,
            Core.Keys.Down => true,
            Core.Keys.Home => true,
            Core.Keys.End => true,
            _ => false
        };
    }

    private static Core.Keys MapKeyCode(Key key)
    {
        return key switch
        {
            Key.Enter => Core.Keys.Enter,
            Key.Escape => Core.Keys.Escape,
            Key.Backspace => Core.Keys.Back,
            Key.Tab => Core.Keys.Tab,
            Key.Space => Core.Keys.Space,
            Key.Left => Core.Keys.Left,
            Key.Right => Core.Keys.Right,
            Key.Up => Core.Keys.Up,
            Key.Down => Core.Keys.Down,
            Key.Home => Core.Keys.Home,
            Key.End => Core.Keys.End,
            Key.PageUp => Core.Keys.PageUp,
            Key.PageDown => Core.Keys.PageDown,
            Key.Insert => Core.Keys.Insert,
            Key.Delete => Core.Keys.Delete,
            Key.F1 => Core.Keys.F1,
            Key.F2 => Core.Keys.F2,
            Key.F3 => Core.Keys.F3,
            Key.F4 => Core.Keys.F4,
            Key.F5 => Core.Keys.F5,
            Key.F6 => Core.Keys.F6,
            Key.F7 => Core.Keys.F7,
            Key.F8 => Core.Keys.F8,
            Key.F9 => Core.Keys.F9,
            Key.F10 => Core.Keys.F10,
            Key.F11 => Core.Keys.F11,
            Key.F12 => Core.Keys.F12,
            Key.A => Core.Keys.A,
            Key.B => Core.Keys.B,
            Key.C => Core.Keys.C,
            Key.D => Core.Keys.D,
            Key.E => Core.Keys.E,
            Key.F => Core.Keys.F,
            Key.G => Core.Keys.G,
            Key.H => Core.Keys.H,
            Key.I => Core.Keys.I,
            Key.J => Core.Keys.J,
            Key.K => Core.Keys.K,
            Key.L => Core.Keys.L,
            Key.M => Core.Keys.M,
            Key.N => Core.Keys.N,
            Key.O => Core.Keys.O,
            Key.P => Core.Keys.P,
            Key.Q => Core.Keys.Q,
            Key.R => Core.Keys.R,
            Key.S => Core.Keys.S,
            Key.T => Core.Keys.T,
            Key.U => Core.Keys.U,
            Key.V => Core.Keys.V,
            Key.W => Core.Keys.W,
            Key.X => Core.Keys.X,
            Key.Y => Core.Keys.Y,
            Key.Z => Core.Keys.Z,
            Key.Number0 => Core.Keys.D0,
            Key.Number1 => Core.Keys.D1,
            Key.Number2 => Core.Keys.D2,
            Key.Number3 => Core.Keys.D3,
            Key.Number4 => Core.Keys.D4,
            Key.Number5 => Core.Keys.D5,
            Key.Number6 => Core.Keys.D6,
            Key.Number7 => Core.Keys.D7,
            Key.Number8 => Core.Keys.D8,
            Key.Number9 => Core.Keys.D9,
            Key.AltLeft or Key.AltRight => Core.Keys.Menu,
            _ => Core.Keys.None
        };
    }

    private static ModifierKeys MapModifierKeys(IKeyboard keyboard)
    {
        var modifiers = ModifierKeys.None;
        if (keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight))
            modifiers |= ModifierKeys.Shift;
        if (keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight))
            modifiers |= ModifierKeys.Control;
        if (keyboard.IsKeyPressed(Key.AltLeft) || keyboard.IsKeyPressed(Key.AltRight))
            modifiers |= ModifierKeys.Alt;
        return modifiers;
    }

    private static MouseButtons MapMouseButton(Silk.NET.Input.MouseButton button)
    {
        return button switch
        {
            Silk.NET.Input.MouseButton.Left => MouseButtons.Left,
            Silk.NET.Input.MouseButton.Right => MouseButtons.Right,
            Silk.NET.Input.MouseButton.Middle => MouseButtons.Middle,
            _ => MouseButtons.None
        };
    }

    private static void ApplyFormCursor(Form form, IMouse mouse)
    {
        var cursorType = form.Cursor;
        try
        {
            if (cursorType.HasValue)
            {
                var mapped = MapToStandardCursor(cursorType.Value);
                mouse.Cursor.Type = CursorType.Standard;
                mouse.Cursor.StandardCursor = mapped;
            }
            else
            {
                mouse.Cursor.Type = CursorType.Standard;
                mouse.Cursor.StandardCursor = StandardCursor.Default;
            }
        }
        catch
        {
            // Ignore cursor errors (some backends may not support certain cursor types)
        }
    }

    private static StandardCursor MapToStandardCursor(SystemCursorType cursor)
    {
        return cursor switch
        {
            SystemCursorType.Arrow => StandardCursor.Arrow,
            SystemCursorType.IBeam => StandardCursor.IBeam,
            SystemCursorType.Crosshair => StandardCursor.Crosshair,
            SystemCursorType.Hand => StandardCursor.Hand,
            SystemCursorType.SizeWE => StandardCursor.HResize,
            SystemCursorType.SizeNS => StandardCursor.VResize,
            SystemCursorType.SizeAll => StandardCursor.ResizeAll,
            SystemCursorType.NotAllowed => StandardCursor.NotAllowed,
            _ => StandardCursor.Default
        };
    }

    /// <summary>
    /// Gets the text content from the system clipboard.
    /// </summary>
    /// <returns>The clipboard text, or null if empty or not text.</returns>
    /// <summary>
    /// Gets the currently pressed modifier keys.
    /// </summary>
    /// <summary>
    /// Gets the currently pressed modifier keys.
    /// </summary>
    public static ModifierKeys GetCurrentModifiers()
    {
        if (_keyboard == null) return ModifierKeys.None;
        return MapModifierKeys(_keyboard);
    }

    /// <summary>
    /// Gets the text content from the system clipboard.
    /// </summary>
    /// <returns>The clipboard text, or null if empty or not text.</returns>
    public static string? GetClipboardText()
    {
        return _keyboard?.ClipboardText;
    }

    /// <summary>
    /// Sets the text content of the system clipboard.
    /// </summary>
    /// <param name="text">The text to set.</param>
    public static void SetClipboardText(string? text)
    {
        if (_keyboard != null && text != null)
        {
            _keyboard.ClipboardText = text;
        }
    }
}