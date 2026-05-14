using System;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.Plugins;

namespace DustAdvisor.Hdt
{
    public sealed class Plugin : IPlugin
    {
        public string Name => "Dust Advisor";
        public string Description => "Recommends which collected cards are safe to disenchant.";
        public string ButtonText => "Open";
        public string Author => "personal";
        public Version Version => new Version(0, 1, 0);
        public MenuItem MenuItem { get; private set; }

        public void OnLoad()
        {
            MenuItem = new MenuItem { Header = "Dust Advisor" };
            MenuItem.Click += (s, e) => { /* wired in a later task */ };
        }

        public void OnUnload() { }
        public void OnButtonPress() { /* wired in a later task */ }
        public void OnUpdate() { }
    }
}
