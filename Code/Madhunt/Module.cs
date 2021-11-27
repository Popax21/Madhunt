using System;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.Madhunt {
    public class Module : EverestModule {
        public static Module Instance { get; private set; }
        public static string Name => Instance.Metadata.Name;
        public Module() { Instance = this; }

        private Manager manager;
        private Hook unlockedAreasHook;

        public override void Load() {
            Celeste.Instance.Components.Add(manager = new Manager(Celeste.Instance));
            unlockedAreasHook = new Hook(typeof(LevelSetStats).GetProperty("UnlockedAreas").GetGetMethod(), (Func<Func<LevelSetStats, int>, LevelSetStats, int>) ((orig, stats) => {
                if(stats.Name == Name) return stats.MaxArea;
                return orig(stats);
            }));
        }

        public override void Unload() {
            Celeste.Instance.Components.Remove(manager);
            manager.Dispose();
            manager = null;
            unlockedAreasHook.Dispose();
        }

        public static Manager MadhuntManager => Instance.manager;
    }
}