// MIT License
// Copyright (c) 2025 BeautyfullCastle
// https://github.com/BeautyfullCastle/UnityAgentRefresh

#if UNITY_EDITOR_OSX
using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AgentRefreshServer
{
    /// <summary>
    /// macOS-specific focus switcher using osascript.
    /// Activates Unity for AssetDatabase.Refresh execution,
    /// then restores focus to the original application.
    /// </summary>
    public class MacFocusSwitcher : IFocusSwitcher
    {
        private string _originalApp;

        /// <summary>
        /// Get current frontmost application and activate Unity.
        /// </summary>
        public void PrepareForRefresh()
        {
            try
            {
                // 1. Get current frontmost application
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "osascript";
                    process.StartInfo.Arguments = "-e \"tell application \\\"System Events\\\" to get name of first process whose frontmost is true\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    _originalApp = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                }

                // 2. Focus Unity
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "osascript";
                    process.StartInfo.Arguments = "-e \"tell application \\\"Unity\\\" to activate\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AgentRefreshServer] PrepareForRefresh failed: {e.Message}");
            }
        }

        public void ExecuteRefreshWithFocus()
        {
            // This method is already called on the main thread via EditorApplication.delayCall
            try
            {
                // Use delayCall to let the focus settle before refreshing
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        Debug.Log("[AgentRefreshServer] AssetDatabase.Refresh executed");

                        // Return focus after refresh
                        EditorApplication.delayCall += () =>
                        {
                            if (!string.IsNullOrEmpty(_originalApp))
                            {
                                using (var process = new Process())
                                {
                                    process.StartInfo.FileName = "osascript";
                                    process.StartInfo.Arguments = $"-e \"tell application \\\"{_originalApp}\\\" to activate\"";
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
    }
}
#endif
