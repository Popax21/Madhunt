using System.Linq;
using System.Reflection;

using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod.Entities;

namespace Celeste.Mod.Madhunt {
    [CustomEntity("Madhunt/StartSwitch")]
    public class StartSwitch : DashSwitch {
        private class NameText : Entity {
            private StartSwitch sSwitch;
            private string name;

            public NameText(StartSwitch sSwitch, string name) {
                AddTag(Tags.HUD);
                this.sSwitch = sSwitch;
                this.name = Dialog.Get(name);
            }

            public override void Render() {
                if (SceneAs<Level>().FrozenOrPaused)
                    return;

                //Draw switch name
                Player player = Scene.Tracker.GetEntity<Player>();
                if(player != null) {
                    Camera cam = SceneAs<Level>().Camera;
                    float alpha = Calc.Clamp(2f - Vector2.Distance(sSwitch.Position, Scene.Tracker.GetEntity<Player>().Position) / 64f, 0f, 1f);
                    switch(sSwitch.side) {
                        case Sides.Left: ActiveFont.Draw(name, (sSwitch.CenterRight + Vector2.UnitX*8f - cam.Position.Floor()) * 6f, new Vector2(0f, 0.5f), Vector2.One, Color.White * alpha); break;
                        case Sides.Right: ActiveFont.Draw(name, (sSwitch.CenterLeft - Vector2.UnitX*8f - cam.Position.Floor()) * 6f, new Vector2(1f, 0.5f), Vector2.One, Color.White * alpha); break;
                        case Sides.Up: ActiveFont.Draw(name, (sSwitch.BottomCenter + Vector2.UnitY*8f - cam.Position.Floor()) * 6f, new Vector2(0.5f, 0f), Vector2.One, Color.White * alpha); break;
                        case Sides.Down: ActiveFont.Draw(name, (sSwitch.TopCenter - Vector2.UnitY*8f - cam.Position.Floor()) * 6f, new Vector2(0.5f, 1f), Vector2.One, Color.White * alpha); break;
                    }
                }
            }
        }
        private static readonly FieldInfo PRESSED_FIELD = typeof(DashSwitch).GetField("pressed", BindingFlags.NonPublic | BindingFlags.Instance);
        private Sides side;
        private NameText nameText;
        private bool pressed = false;

        public StartSwitch(EntityData data, Vector2 offset) : base(data.Position + offset, (Sides) data.Int("side"), false, false, new EntityID(data.Level.Name, data.ID), "mirror") {
            side = (Sides) data.Int("side");
            SwitchID = data.Int("switchID");
            nameText = string.IsNullOrEmpty(data.Attr("name")) ? null : new NameText(this, data.Attr("name"));

            OnDashCollide = (player, dir) => {
                DashCollisionResults res = OnDashed(player, dir);
                if(!pressed && (bool) PRESSED_FIELD.GetValue(this)) pressed = true;
                return res;
            };
        }

        public override void Added(Scene scene) {
            base.Added(scene);
            Scene.Add(nameText);
        }

        public override void Removed(Scene scene) {
            Scene.Remove(nameText);
            base.Removed(scene);
        }

        public override void Update() {
            if(pressed) {
                //Choose a random arena option
                ArenaOption[] opts = Scene.Tracker.GetEntities<ArenaOption>().Cast<ArenaOption>().Where(o => o.SwitchID == SwitchID).ToArray();
                ArenaOption opt = (opts.Length > 0) ? Calc.Random.Choose(opts) : null;

                //Start the manhunt
                if(opt == null || !Module.MadhuntManager.StartRound(opt.GenerateRoundSettings(), CollideFirst<StartZone>()?.ID)) Scene.Tracker.GetEntity<Player>().Die(Vector2.Zero, true);
                pressed = false;
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
                spawnIndex = (byte) spawnIndex,
                initialSeekers = data.Int("initialSeekers", 1)
            };
        }

        public int SwitchID { get; }
    }
}