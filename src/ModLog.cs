namespace PeglinBalatroScore
{
    /// <summary>
    /// Informational logging that can be switched off in the config. Warnings and errors always go to the
    /// BepInEx log (use Plugin.Log directly), since they mean something went wrong.
    /// </summary>
    internal static class ModLog
    {
        public static void Info(string message)
        {
            if (Plugin.EnableLogging == null || Plugin.EnableLogging.Value)
            {
                Plugin.Log.LogInfo(message);
            }
        }
    }
}
