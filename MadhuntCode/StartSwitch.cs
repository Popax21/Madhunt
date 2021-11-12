using System.Linq;
using System.Reflection;

using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod.Entities;

namespace Celeste.Mod.Madhunt {
    [CustomEntity("Madhunt/StartSwitch")]
    public class StartSwitch : DashSwitch {
        private static readonly FieldInfo PRESSED_FIELD = typeof(DashSwitch).GetField("pressed", BindingFlags.NonPublic | BindingFlags.Instance);
        private bool pressed = false;
        private float startCooldown = 0.2f;

        public StartSwitch(EntityData data, Vector2 offset) : base(data.Position + offset, (DashSwitch.Sides) data.Int("side"), false, false, new EntityID(data.Level.Name, data.ID), "mirror") {
            SwitchID = data.Int("switchID");

            OnDashCollide = (player, dir) => {
                DashCollisionResults res = OnDashed(player, dir);
                if(!pressed && (bool) PRESSED_FIELD.GetValue(this)) pressed = true;
                return res;
            };
        }

        public override void Update() {
            if(pressed && startCooldown > 0f) {
                startCooldown -= Engine.DeltaTime;
                if(startCooldown <= 0f) {
                    //Choose a random arena option
                    ArenaOption[] opts = Scene.Tracker.GetEntities<ArenaOption>().Cast<ArenaOption>().Where(o => o.SwitchID == SwitchID).ToArray();
                    ArenaOption opt = Calc.Random.Choose(opts);

                    //Start the manhunt
                    if(opt == null || !Module.MadhuntManager.StartRound(opt.GenerateRoundSettings(), CollideFirst<StartZone>(Position)?.ID)) Scene.Tracker.GetEntity<Player>().Die(Vector2.Zero, true);
                }
            }
            base.Update();
        }

        public int SwitchID { get; }
    }

    [Tracked]
    [CustomEntity("Madhunt/ArenaOption")]
    public class ArenaOption : Entity {
        private EntityData data;

        public ArenaOption(EntityData data, Vector2 offset) {
            this.data = data;
            SwitchID = data.Int("switchID");
        }

        private AreaKey ParseArea(string str) {
            string sid = str;
            AreaMode mode = AreaMode.Normal;

            int hashTagIndex = str.LastIndexOf('#');
            if(hashTagIndex >= 0 && hashTagIndex == str.Length-2) {
                switch(char.ToLower(str[hashTagIndex+1])) {
                    case 'a': str = str.Substring(hashTagIndex); mode = AreaMode.Normal; break;
                    case 'b': str = str.Substring(hashTagIndex); mode = AreaMode.BSide; break;
                    case 'c': str = str.Substring(hashTagIndex); mode = AreaMode.CSide; break;
                }
            }

            return new AreaKey() { SID = sid, Mode = mode };
        }

        public RoundSettings GenerateRoundSettings() {
            Session ses = SceneAs<Level>()?.Session;

            int spawnIndex = data.Int("spawnIndex");
            if(spawnIndex < 0) spawnIndex = Calc.Random.Range(0, -spawnIndex);

            return new RoundSettings() {
                lobbyArea = ses.Area,
                lobbyLevel = ses.Level,
                lobbySpawnPoint = ses.RespawnPoint ?? Vector2.Zero,
                arenaArea = (data.Attr("arenaArea").Length > 0) ? ParseArea(data.Attr("arenaArea")) : ses.Area,
                spawnLevel = data.Attr("spawnLevel"),
                spawnIndex = (byte) spawnIndex
            };
        }

        public int SwitchID { get; }
    }
}