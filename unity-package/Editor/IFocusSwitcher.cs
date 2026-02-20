// MIT License
// Copyright (c) 2025 BeautyfullCastle
// https://github.com/BeautyfullCastle/UnityAgentRefresh

namespace AgentRefreshServer
{
    /// <summary>
    /// Strategy interface for OS-specific window focus management
    /// during Unity AssetDatabase refresh operations.
    /// </summary>
    public interface IFocusSwitcher
    {
        /// <summary>
        /// Called from background thread before queuing refresh.
        /// Use for operations that must happen before main thread execution.
        /// </summary>
        void PrepareForRefresh();

        /// <summary>
        /// Called on main thread via EditorApplication.delayCall.
        /// Executes AssetDatabase.Refresh with focus management.
        /// </summary>
        void ExecuteRefreshWithFocus();
    }
}
