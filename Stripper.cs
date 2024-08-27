using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace Stripper {

    public class Main : BasePlugin, IPluginConfig<MainConfig> {
        
        public override string ModuleName => "Stripper";
        public override string ModuleVersion => "6.6.6";
        public override string ModuleAuthor => "eboyfriends";
        public required MainConfig Config { get; set; }

        public override void Load(bool hotReload)
        {
            Logger.LogInformation("We are loading Stripper!");
            
            RegisterEventHandler<EventItemPickup>(OnItemPickup, HookMode.Post);
            RegisterEventHandler<EventItemPurchase>(OnItemPurchase, HookMode.Post);
            RegisterEventHandler<EventRoundStart>(OnRoundStart, HookMode.Post);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Post);

            RegisterListener<Listeners.OnEntitySpawned>(OnEntitySpawned);
        }

        public override void Unload(bool hotReload)
        {
            Logger.LogInformation("We are unloading ChangeFov!");

            DeregisterEventHandler<EventItemPickup>(OnItemPickup, HookMode.Post);
            DeregisterEventHandler<EventItemPurchase>(OnItemPurchase, HookMode.Post);
            DeregisterEventHandler<EventRoundStart>(OnRoundStart, HookMode.Post);
            DeregisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Post);
        }

        public void OnConfigParsed(MainConfig config) {
            if (config == null) {
                Logger.LogError("Parsed config is null in OnConfigParsed method");
                return;
            }
            
            Config = config;
            Logger.LogInformation("Config successfully parsed and set");
        }
        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info) {
            List<CCSPlayerController> players = Utilities.GetPlayers();
            if (players.Count <= 0) return HookResult.Continue;

            foreach (CCSPlayerController player in players) {
                if (player.Team == CsTeam.Spectator || player.Team == CsTeam.None) continue;

                if (!HasItem(player, "weapon_awp")) {
                    player.GiveNamedItem("weapon_awp");
                }

                if (HasItem(player, "weapon_awp")) {
                    RefillAmmo(player);
                }
            }

            return HookResult.Continue;
        }
        private HookResult OnItemPickup(EventItemPickup @event, GameEventInfo info) {
            if (@event.Userid == null || !@event.Userid.IsValid) {
                return HookResult.Continue;
            }

            CCSPlayerController Player = @event.Userid;
            if (Player.Connected != PlayerConnectedState.PlayerConnected || !Player.PlayerPawn.IsValid) {
                return HookResult.Continue;
            }

            var Weapon = WeaponData.Weapons.FirstOrDefault(w => w.DefIndex == @event.Defindex);
            if (Weapon == null || Weapon.WeaponName == null) return HookResult.Continue;

            if (!Config.Allowed.Any(allowedItem => Weapon.WeaponName.Contains(allowedItem, StringComparison.OrdinalIgnoreCase))) {
                return HookResult.Handled;
            }

            return HookResult.Continue;
        }
        private HookResult OnItemPurchase(EventItemPurchase @event, GameEventInfo info) {
            if (@event.Userid == null || !@event.Userid.IsValid) {
                return HookResult.Continue;
            }

            CCSPlayerController Player = @event.Userid;
            if (Player.Connected != PlayerConnectedState.PlayerConnected || !Player.PlayerPawn.IsValid) {
                return HookResult.Continue;
            }
            
            var Weapon = WeaponData.Weapons.FirstOrDefault(w => w.WeaponName == @event.Weapon);
            if (Weapon == null || Weapon.WeaponName == null) return HookResult.Continue;
            
            if (!Config.Allowed.Any(allowedItem => Weapon.WeaponName.Contains(allowedItem, StringComparison.OrdinalIgnoreCase))) {
                return HookResult.Handled;
            }

            return HookResult.Continue;
        }
        
        private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info) {
            if (@event.Attacker == null || @event.Userid == null) {
                return HookResult.Continue;
            }

            CCSPlayerController Player = @event.Userid;
            CCSPlayerController Attacker = @event.Attacker;
            
            CCSPlayerPawn? PlayerPawn = Player.PlayerPawn.Value;
            CCSPlayerPawn? AttackerPawn = Attacker.PlayerPawn.Value;

            if (Config != null && Config.RestoreHpOnKnife && @event.Weapon.ToLower().StartsWith("knife") && AttackerPawn != null) {
                AttackerPawn.Health = 100;
                AttackerPawn.MaxHealth = 100;
                
                Utilities.SetStateChanged(AttackerPawn, "CBaseEntity", "m_iHealth");
            }

            return HookResult.Continue;
        }
        public static void RefillAmmo(CCSPlayerController Player) {
            if(Player.PlayerPawn.Value == null || Player.PlayerPawn.Value.WeaponServices == null) return;

            foreach (var Weapon in Player.PlayerPawn.Value.WeaponServices.MyWeapons)
            {
                if (Weapon is { IsValid: true, Value.IsValid: true })
                {
                    Weapon.Value.Clip1 = 5;
                    Weapon.Value.ReserveAmmo[0] = 15;

                    Schema.SetSchemaValue<int>(Weapon.Value.Handle, "CBasePlayerWeapon", "m_iClip1", Weapon.Value.Clip1);
                    Schema.SetSchemaValue<int>(Weapon.Value.Handle, "CBasePlayerWeapon", "m_iClip2", Weapon.Value.Clip2);
                    Schema.SetSchemaValue<int>(Weapon.Value.Handle, "CBasePlayerWeapon", "m_pReserveAmmo", Weapon.Value.ReserveAmmo[0]);
                }
            }
        }
        private void OnEntitySpawned(CEntityInstance entity) {
            if (entity == null || entity.DesignerName == null) {
                return;
            }

            if (!Config.Allowed.Any(allowedItem => entity.DesignerName.Contains(allowedItem, StringComparison.OrdinalIgnoreCase)) && entity.DesignerName.StartsWith("weapon_")) {
                entity.Remove();
            }
        }
        private static bool HasItem(CCSPlayerController player, string itemName) {
            var weapons = player?.PlayerPawn?.Value?.WeaponServices?.MyWeapons;
            if (weapons != null) return weapons.Any(weapon => weapon.IsValid && weapon?.Value?.DesignerName == itemName);
            return false;
        }
    }

    public class WeaponData {
        public string? WeaponName { get; init; }
        public string? Name { get; init; }
        public int Price { get; init; }
        public long DefIndex { get; init; }
        public static List<WeaponData> Weapons = new List<WeaponData> {
            new()
            {
                DefIndex = (long)ItemDefinition.M4A4,
                Name = "M4A1",
                WeaponName = "weapon_m4a1",
                Price = 3100,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.M4A1_S,
                Name = "M4A1-S",
                WeaponName = "weapon_m4a1_silencer",
                Price = 2900,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.FAMAS,
                Name = "Famas",
                WeaponName = "weapon_famas",
                Price = 2050,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.AUG,
                Name = "AUG",
                WeaponName = "weapon_aug",
                Price = 3300,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.AK_47,
                Name = "AK-47",
                WeaponName = "weapon_ak47",
                Price = 2700,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.GALIL_AR,
                Name = "Galil",
                WeaponName = "weapon_galilar",
                Price = 1800,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.SG_553,
                Name = "Sg553",
                WeaponName = "weapon_sg556",
                Price = 3000,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.SCAR_20,
                Name = "Scar-20",
                WeaponName = "weapon_scar20",
                Price = 5000,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.AWP,
                Name = "AWP",
                WeaponName = "weapon_awp",
                Price = 4750,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.SSG_08,
                Name = "SSG08",
                WeaponName = "weapon_ssg08",
                Price = 1700,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.G3SG1,
                Name = "G3SG1",
                WeaponName = "weapon_g3sg1",
                Price = 5000,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.MP9,
                Name = "MP9",
                WeaponName = "weapon_mp9",
                Price = 1250,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.MP7,
                Name = "MP7",
                WeaponName = "weapon_mp7",
                Price = 1500,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.MP5_SD,
                Name = "MP5-SD",
                WeaponName = "weapon_mp5sd",
                Price = 1500,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.UMP_45,
                Name = "UMP-45",
                WeaponName = "weapon_ump45",
                Price = 1200,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.P90,
                Name = "P-90",
                WeaponName = "weapon_p90",
                Price = 2350,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.PP_BIZON,
                Name = "PP-19 Bizon",
                WeaponName = "weapon_bizon",
                Price = 1400,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.MAC_10,
                Name = "MAC-10",
                WeaponName = "weapon_mac10",
                Price = 1050,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.USP_S,
                Name = "USP-S",
                WeaponName = "weapon_usp_silencer",
                Price = 200,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.P2000,
                Name = "P2000",
                WeaponName = "weapon_hkp2000",
                Price = 200,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.GLOCK_18,
                Name = "Glock-18",
                WeaponName = "weapon_glock",
                Price = 200,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.DUAL_BERETTAS,
                Name = "Dual berettas",
                WeaponName = "weapon_elite",
                Price = 300,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.P250,
                Name = "P250",
                WeaponName = "weapon_p250",
                Price = 300,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.FIVE_SEVEN,
                Name = "Five-SeveN",
                WeaponName = "weapon_fiveseven",
                Price = 500,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.CZ75_AUTO,
                Name = "CZ75-Auto",
                WeaponName = "weapon_cz75a",
                Price = 500,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.TEC_9,
                Name = "Tec-9",
                WeaponName = "weapon_tec9",
                Price = 500,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.R8_REVOLVER,
                Name = "Revolver R8",
                WeaponName = "weapon_revolver",
                Price = 600,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.DESERT_EAGLE,
                Name = "Desert Eagle",
                WeaponName = "weapon_deagle",
                Price = 700,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.NOVA,
                Name = "Nova",
                WeaponName = "weapon_nova",
                Price = 1050,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.XM1014,
                Name = "XM1014",
                WeaponName = "weapon_xm1014",
                Price = 2000,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.MAG_7,
                Name = "MAG-7",
                WeaponName = "weapon_mag7",
                Price = 1300,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.SAWED_OFF,
                Name = "Sawed-off",
                WeaponName = "weapon_sawedoff",
                Price = 1100,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.M249,
                Name = "M429",
                WeaponName = "weapon_m249",
                Price = 5200,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.NEGEV,
                Name = "Negev",
                WeaponName = "weapon_negev",
                Price = 1700,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.ZEUS_X27,
                Name = "Zeus x27",
                WeaponName = "weapon_taser",
                Price = 200,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.HIGH_EXPLOSIVE_GRENADE,
                Name = "High Explosive Grenade",
                WeaponName = "weapon_hegrenade",
                Price = 300,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.MOLOTOV,
                Name = "Molotov",
                WeaponName = "weapon_molotov",
                Price = 400,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.INCENDIARY_GRENADE,
                Name = "Incendiary Grenade",
                WeaponName = "weapon_incgrenade",
                Price = 600,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.SMOKE_GRENADE,
                Name = "Smoke Grenade",
                WeaponName = "weapon_smokegrenade",
                Price = 300,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.FLASHBANG,
                Name = "Flashbang",
                WeaponName = "weapon_flashbang",
                Price = 200,
            },
            new()
            {
                DefIndex = (long)ItemDefinition.DECOY_GRENADE,
                Name = "Decoy Grenade",
                WeaponName = "weapon_decoy",
                Price = 50
            }
        };
    }

}