using System;
using System.Linq;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.Madhunt {
    public class Module : EverestModule {
        public const byte PROTOCOL_VERSION = 1;

        public static Module Instance { get; private set; }
        public static string Name => Instance.Metadata.Name;
        public Module() { Instance = this; }

        private TransitionComponent transitionComp;
        private Manager manager;
        private Hook unlockedAreasHook;

        public override void Load() {
            unlockedAreasHook = new Hook(typeof(LevelSetStats).GetProperty("UnlockedAreas").GetGetMethod(), (Func<Func<LevelSetStats, int>, LevelSetStats, int>) ((orig, stats) => {
                if(stats.Name == Name) return stats.MaxArea;
                return orig(stats);
            }));

            Celeste.Instance.Components.Add(transitionComp = new TransitionComponent(Celeste.Instance));
            Celeste.Instance.Components.Add(manager = new Manager(Celeste.Instance));
            manager.OnVerifyRoundStart += VerifyRoundStart;
        }

        public override void Unload() {
            Celeste.Instance.Components.Remove(transitionComp);
            Celeste.Instance.Components.Remove(manager);
            transitionComp.Dispose();
            manager.Dispose();
            manager = null;
            unlockedAreasHook.Dispose();
        }

        private void VerifyRoundStart(DataMadhuntRoundStart data, ref bool isValid) {
            //Check if we're in the lobby room
            Level lvl = Celeste.Scene as Level;
            Session ses = lvl?.Session;
            if(ses == null || ses.Area != data.RoundSettings.lobbyArea || ses.Level != data.RoundSettings.lobbyLevel) return;

            //Check if the zone ID matches
            if(data.StartZoneID.HasValue && !(Celeste.Scene.Tracker.GetEntity<Player>()?.CollideAll<StartZone>().Cast<StartZone>().Any(sz => data.StartZoneID == sz.ID) ?? false)) return;

            //Check if there's an arena option which produces these settings
            foreach(ArenaOption option in lvl.Tracker.GetEntities<ArenaOption>()) {
                if(option.Settings.RoundID == data.RoundSettings.RoundID) {
                    //The start packet is valid
                    isValid |= true;
                    return;
                }
            }

        }

        public static Manager MadhuntManager => Instance.manager;
    }
}