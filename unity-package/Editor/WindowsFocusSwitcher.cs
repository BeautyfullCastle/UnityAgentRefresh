// MIT License
// Copyright (c) 2025 BeautyfullCastle
// https://github.com/BeautyfullCastle/UnityAgentRefresh

#if UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace AgentRefreshServer
{
    /// <summary>
    /// Windows-specific focus switcher using Win32 P/Invoke.
    /// Forces Unity to foreground for AssetDatabase.Refresh execution,
    /// then restores focus to the original window.
    /// </summary>
    public class WindowsFocusSwitcher : IFocusSwitcher
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        // Store original window to restore focus after refresh
        private IntPtr _originalForegroundWindow = IntPtr.Zero;

        /// <summary>
        /// Force Unity to foreground from background thread using AttachThreadInput trick.
        /// This bypasses Windows' SetForegroundWindow restrictions.
        /// Must be called BEFORE EditorApplication.delayCall registration.
        /// </summary>
        public void PrepareForRefresh()
        {
            try
            {
                // Save the original foreground window to restore later
                _originalForegroundWindow = GetForegroundWindow();

                // Get Unity's main window handle
                IntPtr unityWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                if (unityWindow == IntPtr.Zero)
                {
                    Debug.LogWarning("[AgentRefreshServer] Could not get Unity main window handle");
                    return;
                }

                // Get thread IDs
                IntPtr foregroundWindow = GetForegroundWindow();
                uint foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
                uint currentThreadId = GetCurrentThreadId();

                // Attach to foreground thread's input queue to gain focus permission
                if (foregroundThreadId != currentThreadId)
                {
                    AttachThreadInput(currentThreadId, foregroundThreadId, true);
                    try
                    {
                        BringWindowToTop(unityWindow);
                        ShowWindow(unityWindow, SW_RESTORE);
                        SetForegroundWindow(unityWindow);
                    }
                    finally
                    {
                        AttachThreadInput(currentThreadId, foregroundThreadId, false);
                    }
                }
                else
                {
                    SetForegroundWindow(unityWindow);
                }

                Debug.Log("[AgentRefreshServer] Forced Unity to foreground");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AgentRefreshServer] ForceUnityToForeground failed: {e.Message}");
            }
        }

        public void ExecuteRefreshWithFocus()
        {
            // This method is already called on the main thread via EditorApplication.delayCall
            // Unity should already be in foreground due to PrepareForRefresh() call
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                Debug.Log("[AgentRefreshServer] AssetDatabase.Refresh executed");

                // Return focus to original window after a short delay
                if (_originalForegroundWindow != IntPtr.Zero)
                {
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            SetForegroundWindow(_originalForegroundWindow);
                            _originalForegroundWindow = IntPtr.Zero;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[AgentRefreshServer] Failed to restore focus: {e.Message}");
                        }
                    };
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AgentRefreshServer] Failed during refresh: {e.Message}");
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
#endif
