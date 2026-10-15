using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// What the level tools ask of the next Play (ARCHITECTURE §27): start the run at a checkpoint (Play From Here), or walk the level
    /// with the route runner (Run Route Runner). The editor leaves the request in its session state before entering Play; it is read
    /// once, before the first scene loads, and cleared. Editor only.
    /// </summary>
    public static class EditorPlayRequests
    {
        public const string PlayFromKey = "HotPatata.PlayFromCheckpoint";
        public const string RouteRunKey = "HotPatata.RunRouteRunner";

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Read()
        {
            int from = UnityEditor.SessionState.GetInt(PlayFromKey, -1);
            UnityEditor.SessionState.EraseInt(PlayFromKey);
            // a Play without a request starts at the start (the static may survive a Play without a domain reload)
            RunOptions.StartCheckpoint = Mathf.Max(0, from);
            if (UnityEditor.SessionState.GetBool(RouteRunKey, false))
            {
                UnityEditor.SessionState.EraseBool(RouteRunKey);
                var go = new GameObject("Route runner");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<RouteRunSession>();
            }
        }
#endif
    }
}
