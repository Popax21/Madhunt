using System.Linq;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod.Entities;
using Celeste.Mod.Procedurline;

namespace Celeste.Mod.Madhunt {
    [Tracked]
    [CustomEntity("Madhunt/ArenaOption")]
    public class ArenaOption : Entity {
        private EntityData data;
        private HashSet<int> switchIDs;

        public ArenaOption(EntityData data, Vector2 offset) {
            this.data = data;
            this.switchIDs = new HashSet<int>(data.Attr("switchIDs").Split(new[]{','}).Select(s => int.Parse(s)));
        }

        public bool CanChooseOption(StartSwitch sSwitch) => switchIDs.Contains(sSwitch.SwitchID);

        public RoundSettings Settings {
            get {
                Session ses = SceneAs<Level>()?.Session;

                int spawnIndex = data.Int("spawnIndex");
                if(spawnIndex < 0) spawnIndex = Calc.Random.Range(0, -spawnIndex);

                return new RoundSettings() {
                    lobbyArea = ses.Area,
                    lobbyLevel = ses.Level,
                    lobbySpawnPoint = ses.RespawnPoint ?? Vector2.Zero,
                    arenaArea = (data.Attr("arenaArea").Length > 0) ? data.Attr("arenaArea").ParseAreaKey() : ses.Area,
                    spawnLevel = data.Attr("spawnLevel"),
                    spawnIndex = (byte) spawnIndex,
                    initialSeekers = data.Int("initialSeekers", 1),
                    tagMode = data.Bool("tagMode", true),
                    goldenMode = data.Bool("goldenMode", false),
                    hideNames = data.Bool("hideNames", true)
                };
            }
        }
    }
}