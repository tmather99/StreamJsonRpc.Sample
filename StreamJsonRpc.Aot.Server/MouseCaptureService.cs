using System;
using SharpHook;
using SharpHook.Native;
using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Server;

public class MouseCaptureService : IDisposable
{
    private TaskPoolGlobalHook? _hook;
    private readonly Action<int, int, MouseAction> _onMouseEvent;
    private DateTime _lastMoveTime = DateTime.MinValue;
    private readonly TimeSpan _moveThrottle = TimeSpan.FromMilliseconds(50);
    private bool _isRunning;

    public MouseCaptureService(Action<int, int, MouseAction> onMouseEvent)
    {
        _onMouseEvent = onMouseEvent;
    }

    public async Task StartAsync()
    {
        if (_isRunning)
        {
            Console.WriteLine("Mouse capture already started");
            return;
        }

        Console.WriteLine("Starting global mouse capture...");

        _hook = new TaskPoolGlobalHook();

        // Subscribe to mouse events
        _hook.MouseMoved += OnMouseMoved;
        _hook.MousePressed += OnMousePressed;
        _hook.MouseReleased += OnMouseReleased;
        _hook.MouseWheel += OnMouseWheel;

        // Start the hook
        await _hook.RunAsync();
        _isRunning = true;

        Console.WriteLine("Global mouse capture started successfully");
    }

    private void OnMouseMoved(object? sender, MouseHookEventArgs e)
    {
        // Throttle move events to avoid flooding
        var now = DateTime.UtcNow;
        if (now - _lastMoveTime < _moveThrottle)
            return;

        _lastMoveTime = now;
        _onMouseEvent(e.Data.X, e.Data.Y, MouseAction.Move);
    }

    private void OnMousePressed(object? sender, MouseHookEventArgs e)
    {
        var action = (int)e.Data.Button switch
        {
            1 => MouseAction.LeftClick,
            2 => MouseAction.RightClick,
            3 => MouseAction.MiddleClick,
            _ => MouseAction.LeftClick
        };

        _onMouseEvent(e.Data.X, e.Data.Y, action);
    }
        
    private void OnMouseReleased(object? sender, MouseHookEventArgs e)
    {
        // Optional: track double-clicks by timing between releases
    }

    private void OnMouseWheel(object? sender, MouseWheelHookEventArgs e)
    {
        _onMouseEvent(e.Data.X, e.Data.Y, MouseAction.Scroll);
    }

    public void Stop()
    {
        if (_hook != null && _isRunning)
        {
            Console.WriteLine("Stopping global mouse capture...");

            _hook.MouseMoved -= OnMouseMoved;
            _hook.MousePressed -= OnMousePressed;
            _hook.MouseReleased -= OnMouseReleased;
            _hook.MouseWheel -= OnMouseWheel;

            _hook.Dispose();
            _hook = null;
            _isRunning = false;

            Console.WriteLine("Global mouse capture stopped");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}