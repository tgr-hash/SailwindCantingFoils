using BepInEx;

namespace SailwindCantingFoils
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.sailwindmods.sailwindcantingfoils";
        public const string PluginName = "SailwindCantingFoils";
        public const string PluginVersion = "0.1.0";

        private void Awake()
        {
            Logger.LogInfo("SailwindCantingFoils loaded successfully");
        }
    }
}
