// MIT License
// Copyright (c) 2025 BeautyfullCastle
// https://github.com/BeautyfullCastle/UnityAgentRefresh

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace AgentRefreshServer
{
    /// <summary>
    /// Lightweight HTTP server for external agent automation.
    /// Allows triggering AssetDatabase.Refresh() via HTTP POST.
    /// </summary>
    [InitializeOnLoad]
    public static class AgentRefreshServer
    {
        private const int Port = 7788;
        private const string Prefix = "http://localhost:7788/";
        
        private static HttpListener _listener;
        private static Thread _listenerThread;
        private static bool _isRunning;

        static AgentRefreshServer()
        {
            StartServer();
            EditorApplication.quitting += StopServer;
            AssemblyReloadEvents.beforeAssemblyReload += StopServer;
        }

        private static void StartServer()
        {
            if (_isRunning) return;
            
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(Prefix);
                _listener.Start();
                _isRunning = true;
                
                _listenerThread = new Thread(ListenLoop) { IsBackground = true };
                _listenerThread.Start();
                
                Debug.Log($"[AgentRefreshServer] Started on {Prefix}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AgentRefreshServer] Failed to start: {e.Message}");
            }
        }

        private static void StopServer()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            try
            {
                if (_listener != null && _listener.IsListening)
                {
                    _listener.Stop();
                    _listener.Close();
                }
                
                if (_listenerThread != null && _listenerThread.IsAlive)
                {
                    _listenerThread.Abort();
                }
                
                Debug.Log("[AgentRefreshServer] Stopped");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AgentRefreshServer] Error during shutdown: {e.Message}");
            }
        }

        private static void ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem((state) => HandleRequest((HttpListenerContext)state), context);
                }
                catch (HttpListenerException)
                {
                    // Expected on shutdown
                }
                catch (Exception e)
                {
                    if (_isRunning)
                    {
                        Debug.LogError($"[AgentRefreshServer] Error in listen loop: {e.Message}");
                    }
                }
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                if (request.Url.AbsolutePath == "/refresh" && request.HttpMethod == "POST")
                {
                    HandleRefresh(response);
                }
                else if (request.Url.AbsolutePath == "/status" && request.HttpMethod == "GET")
                {
                    HandleStatus(response);
                }
                else
                {
                    SendResponse(response, 404, "{\"success\": false, \"message\": \"Not Found\"}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AgentRefreshServer] Error handling request: {e.Message}");
                SendResponse(response, 500, "{\"success\": false, \"message\": \"Internal Server Error\"}");
            }
            finally
            {
                response.Close();
            }
        }

        private static void HandleRefresh(HttpListenerResponse response)
        {
            Debug.Log("[AgentRefreshServer] Refresh triggered via HTTP POST");
            
            // CRITICAL: Force Unity to foreground BEFORE registering delayCall
            // delayCall only executes when Unity has OS-level focus
#if UNITY_EDITOR_WIN
            WindowFocusManager.ForceUnityToForeground();
#endif
            
            // Queue the refresh to run on the main thread
            // EditorApplication.delayCall is thread-safe to assign from background threads
            EditorApplication.delayCall += WindowFocusManager.RefreshWithFocusSwitch;

            SendResponse(response, 200, "{\"success\": true, \"message\": \"Refresh triggered\"}");
        }

        private static void HandleStatus(HttpListenerResponse response)
        {
            SendResponse(response, 200, $"{{\"running\": true, \"port\": {Port}}}");
        }

        private static void SendResponse(HttpListenerResponse response, int statusCode, string content)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Cross-platform window focus manager for triggering Unity refresh
        /// while preserving the user's active window context.
        /// </summary>
        private static class WindowFocusManager
        {
            public static void RefreshWithFocusSwitch()
            {
#if UNITY_EDITOR_WIN
                WindowsRefreshWithFocus();
#elif UNITY_EDITOR_OSX
                MacRefreshWithFocus();
#else
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
#endif
            }

#if UNITY_EDITOR_WIN
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
            private static IntPtr _originalForegroundWindow = IntPtr.Zero;

            /// <summary>
            /// Force Unity to foreground from background thread using AttachThreadInput trick.
            /// This bypasses Windows' SetForegroundWindow restrictions.
            /// Must be called BEFORE EditorApplication.delayCall registration.
            /// </summary>
            public static void ForceUnityToForeground()
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

            private static void WindowsRefreshWithFocus()
            {
                // This method is already called on the main thread via EditorApplication.delayCall
                // Unity should already be in foreground due to ForceUnityToForeground() call
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
#endif

#if UNITY_EDITOR_OSX
            private static void MacRefreshWithFocus()
            {
                // This method is already called on the main thread via EditorApplication.delayCall
                try
                {
                    // 1. Get current frontmost application
                    string originalApp = "";
                    using (var process = new System.Diagnostics.Process())
                    {
                        process.StartInfo.FileName = "osascript";
                        process.StartInfo.Arguments = "-e \"tell application \\\"System Events\\\" to get name of first process whose frontmost is true\"";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.CreateNoWindow = true;
                        process.Start();
                        originalApp = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();
                    }
                    
                    // 2. Focus Unity
                    using (var process = new System.Diagnostics.Process())
                    {
                        process.StartInfo.FileName = "osascript";
                        process.StartInfo.Arguments = "-e \"tell application \\\"Unity\\\" to activate\"";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.Start();
                        process.WaitForExit();
                    }
                    
                    // 3. Use delayCall to let the focus settle before refreshing
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                            Debug.Log("[AgentRefreshServer] AssetDatabase.Refresh executed");
                            
                            // Return focus after refresh
                            EditorApplication.delayCall += () =>
                            {
                                if (!string.IsNullOrEmpty(originalApp))
                                {
                                    using (var process = new System.Diagnostics.Process())
                                    {
                                        process.StartInfo.FileName = "osascript";
                                        process.StartInfo.Arguments = $"-e \"tell application \\\"{originalApp}\\\" to activate\"";
                                        process.StartInfo.UseShellExecute = false;
                                        process.StartInfo.CreateNoWindow = true;
                                        process.Start();
                                    }
                                }
                            };
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[AgentRefreshServer] Failed during refresh: {e.Message}");
                        }
                    };
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AgentRefreshServer] Focus switch failed, refreshing anyway: {e.Message}");
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                }
            }
#endif
        }
    }
}
