using CounterStrikeSharp.API.Core;

namespace Stripper {
    public class MainConfig : BasePluginConfig {
        public List<string> Allowed { get; set; } = new List<string> 
        {
            "knife",
            "awp",
            "taser",
            "grenade",
            "flashbang",
            "decoy"
        };
    }
}

