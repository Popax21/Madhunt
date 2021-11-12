using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Madhunt {
    [Tracked]
    public class StartZone : Trigger {
        public StartZone(EntityData data, Vector2 offset) : base(data, offset) => ID = data.ID;
        public int ID { get; }
    }
}