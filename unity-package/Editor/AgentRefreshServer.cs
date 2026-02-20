// MIT License
// Copyright (c) 2025 BeautyfullCastle
// https://github.com/BeautyfullCastle/UnityAgentRefresh

using System;
using System.Net;
using System.Text;
using System.Threading;
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
            
            var focusSwitcher = FocusSwitcherFactory.Create();
            focusSwitcher.PrepareForRefresh();
            
            // Queue the refresh to run on the main thread
            // EditorApplication.delayCall is thread-safe to assign from background threads
            EditorApplication.delayCall += focusSwitcher.ExecuteRefreshWithFocus;

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
    }
}
