// MIT License
// Copyright (c) 2025 BeautyfullCastle
// https://github.com/BeautyfullCastle/UnityAgentRefresh

namespace AgentRefreshServer
{
    /// <summary>
    /// Factory for creating platform-specific IFocusSwitcher instances.
    /// </summary>
    public static class FocusSwitcherFactory
    {
        private static IFocusSwitcher _instance;

        public static IFocusSwitcher Create()
        {
            if (_instance != null) return _instance;

#if UNITY_EDITOR_WIN
            _instance = new WindowsFocusSwitcher();
#elif UNITY_EDITOR_OSX
            _instance = new MacFocusSwitcher();
#else
            _instance = new DefaultFocusSwitcher();
#endif
            return _instance;
        }
    }
}
