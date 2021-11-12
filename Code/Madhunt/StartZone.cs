using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod.Entities;

namespace Celeste.Mod.Madhunt {
    [Tracked]
    [CustomEntity("Madhunt/StartZone")]
    public class StartZone : Trigger {
        public StartZone(EntityData data, Vector2 offset) : base(data, offset) => ID = data.ID;
        public int ID { get; }
    }
}