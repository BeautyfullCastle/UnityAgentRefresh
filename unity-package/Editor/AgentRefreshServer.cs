// MIT License
// Copyright (c) 2025 BeautyfullCastle
// https://github.com/BeautyfullCastle/UnityAgentRefresh

using System;
using System.Collections.Generic;
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

        // Log Buffering
        private struct LogEntry
        {
            public string type;
            public string message;
            public string stackTrace;
        }

        private static List<LogEntry> _logBuffer = new List<LogEntry>();
        private static readonly object _logLock = new object();
        private const int MaxLogEntries = 500;

        // Refresh Tracking
        private static readonly object _refreshLock = new object();
        private static bool _refreshPending;
        private static bool _refreshCompleted;
        private static List<LogEntry> _refreshErrors;

        static AgentRefreshServer()
        {
            StartServer();
            Application.logMessageReceived += OnLogReceived;
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
            Application.logMessageReceived -= OnLogReceived;
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

        private static void OnLogReceived(string message, string stackTrace, LogType type)
        {
            var entry = new LogEntry
            {
                type = type.ToString(),
                message = message,
                stackTrace = stackTrace
            };

            lock (_logLock)
            {
                _logBuffer.Add(entry);
                if (_logBuffer.Count > MaxLogEntries)
                {
                    _logBuffer.RemoveAt(0);
                }
            }

            if ((type == LogType.Error || type == LogType.Exception) && _refreshPending)
            {
                lock (_refreshLock)
                {
                    if (_refreshPending && _refreshErrors != null)
                    {
                        _refreshErrors.Add(entry);
                    }
                }
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
                string path = request.Url.AbsolutePath;
                string method = request.HttpMethod;

                if (path == "/refresh" && method == "POST")
                {
                    HandleRefresh(response);
                }
                else if (path == "/status" && method == "GET")
                {
                    HandleStatus(response);
                }
                else if (path == "/logs" && method == "GET")
                {
                    int count = 50;
                    var countStr = request.QueryString["count"];
                    if (!string.IsNullOrEmpty(countStr)) int.TryParse(countStr, out count);
                    HandleGetLogs(response, count);
                }
                else if (path == "/errors" && method == "GET")
                {
                    HandleGetLogs(response, 100, "Error");
                }
                else if (path == "/clear" && method == "POST")
                {
                    HandleClearLogs(response);
                }
                else
                {
                    SendResponse(response, 404, "{\"success\": false, \"message\": \"Not Found. Available endpoints: POST /refresh, GET /status, GET /logs, GET /errors, POST /clear\"}");
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

            lock (_refreshLock)
            {
                _refreshPending = true;
                _refreshCompleted = false;
                _refreshErrors = new List<LogEntry>();
            }
            
            var focusSwitcher = FocusSwitcherFactory.Create();
            focusSwitcher.PrepareForRefresh();
            
            // Queue the refresh to run on the main thread
            EditorApplication.delayCall += () => {
                focusSwitcher.ExecuteRefreshWithFocus();
                // Use nested delayCall to mark completion after the refresh finishes
                EditorApplication.delayCall += () => {
                    lock (_refreshLock)
                    {
                        _refreshCompleted = true;
                    }
                };
            };

            // Wait for completion (with timeout)
            int timeoutMs = 30000;
            int intervalMs = 100;
            int elapsedMs = 0;

            while (elapsedMs < timeoutMs)
            {
                lock (_refreshLock)
                {
                    if (_refreshCompleted) break;
                }
                Thread.Sleep(intervalMs);
                elapsedMs += intervalMs;
            }

            // Build response
            var sb = new StringBuilder();
            List<LogEntry> errors;
            lock (_refreshLock)
            {
                errors = _refreshErrors;
                _refreshPending = false;
                _refreshErrors = null;
            }

            bool hasErrors = errors != null && errors.Count > 0;
            sb.Append("{");
            sb.Append("\"success\": true,");
            sb.Append($"\"message\": \"{(elapsedMs >= timeoutMs ? "Refresh timed out" : "Refresh completed")}\",");
            sb.Append($"\"hasErrors\": {(hasErrors ? "true" : "false")}");

            if (hasErrors)
            {
                sb.Append($", \"errorCount\": {errors.Count},");
                sb.Append("\"errors\": [");
                for (int i = 0; i < errors.Count; i++)
                {
                    sb.Append("{");
                    sb.Append($"\"type\": \"{EscapeJson(errors[i].type)}\",");
                    sb.Append($"\"message\": \"{EscapeJson(errors[i].message)}\"");
                    sb.Append("}");
                    if (i < errors.Count - 1) sb.Append(",");
                }
                sb.Append("]");
            }
            sb.Append("}");

            SendResponse(response, 200, sb.ToString());
        }

        private static void HandleStatus(HttpListenerResponse response)
        {
            int logCount;
            int errorCount = 0;
            
            lock (_logLock)
            {
                logCount = _logBuffer.Count;
                foreach (var log in _logBuffer)
                {
                    if (log.type == "Error" || log.type == "Exception")
                        errorCount++;
                }
            }

            SendResponse(response, 200, $"{{\"running\": true, \"port\": {Port}, \"bufferedLogs\": {logCount}, \"errors\": {errorCount}}}");
        }

        private static void HandleGetLogs(HttpListenerResponse response, int count, string filterType = null)
        {
            List<LogEntry> logs;
            lock (_logLock)
            {
                logs = new List<LogEntry>(_logBuffer);
            }

            if (!string.IsNullOrEmpty(filterType))
            {
                logs = logs.FindAll(l => 
                    l.type.Equals(filterType, StringComparison.OrdinalIgnoreCase) || 
                    (filterType.Equals("Error", StringComparison.OrdinalIgnoreCase) && l.type.Equals("Exception", StringComparison.OrdinalIgnoreCase))
                );
            }

            if (logs.Count > count)
            {
                logs = logs.GetRange(logs.Count - count, count);
            }

            var sb = new StringBuilder();
            sb.Append("{\"logs\": [");
            for (int i = 0; i < logs.Count; i++)
            {
                sb.Append("{");
                sb.Append($"\"type\": \"{EscapeJson(logs[i].type)}\",");
                sb.Append($"\"message\": \"{EscapeJson(logs[i].message)}\"");
                sb.Append("}");
                if (i < logs.Count - 1) sb.Append(",");
            }
            sb.Append($"], \"count\": {logs.Count}}}");

            SendResponse(response, 200, sb.ToString());
        }

        private static void HandleClearLogs(HttpListenerResponse response)
        {
            lock (_logLock)
            {
                _logBuffer.Clear();
            }
            SendResponse(response, 200, "{\"success\": true, \"message\": \"Log buffer cleared\"}");
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
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
