namespace Celeste.Mod.Madhunt {
    public class Module : EverestModule {
        public static Module Instance { get; private set; }
        public static string Name => Instance.Metadata.Name;
        public Module() { Instance = this; }

        private Manager manager;

        public override void Load() {
            Celeste.Instance.Components.Add(manager = new Manager(Celeste.Instance));
        }
        public override void Unload() {
            Celeste.Instance.Components.Remove(manager);
            manager.Dispose();
            manager = null;
        }

        public static Manager MadhuntManager => Instance.manager;
    }
}