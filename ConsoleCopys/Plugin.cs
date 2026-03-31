using Console;
using MelonLoader;

[assembly: MelonInfo(typeof(Plugin), PluginInfo.Name, PluginInfo.Version, PluginInfo.GUID)]
[assembly: MelonGame()]
namespace Console
{
    public class Plugin : MelonMod
    {
        public override void OnInitializeMelon()
        {
            base.OnInitializeMelon();
            Console.LoadConsole();
        }
    }
}