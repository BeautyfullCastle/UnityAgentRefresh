// MIT License
// Copyright (c) 2025 BeautyfullCastle
// https://github.com/BeautyfullCastle/UnityAgentRefresh

using UnityEditor;
using UnityEngine;

namespace AgentRefreshServer
{
    /// <summary>
    /// Fallback focus switcher for unsupported platforms.
    /// Simply executes AssetDatabase.Refresh without focus management.
    /// </summary>
    public class DefaultFocusSwitcher : IFocusSwitcher
    {
        public void PrepareForRefresh()
        {
            // No preparation needed for default implementation
        }

        public void ExecuteRefreshWithFocus()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log("[AgentRefreshServer] AssetDatabase.Refresh executed (default)");
        }
    }
}
