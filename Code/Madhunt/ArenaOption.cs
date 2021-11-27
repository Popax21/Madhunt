using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod.Entities;

namespace Celeste.Mod.Madhunt {
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
                initialSeekers = data.Int("initialSeekers", 1),
                tagMode = data.Bool("tagMode", true),
                goldenMode = data.Bool("goldenMode", false),
                hideNames = data.Bool("hideNames", true)
            };
        }

        public int SwitchID { get; }
    }
}