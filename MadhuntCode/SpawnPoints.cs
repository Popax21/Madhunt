using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod.Entities;

namespace Celeste.Mod.Madhunt {
    [Tracked]
    [CustomEntity("Madhunt/HiderSpawnPoint")]
    public class HiderSpawnPoint : Entity {
        public HiderSpawnPoint(EntityData data, Vector2 off) : base(data.Position + off) => SpawnIndex = (byte) data.Int("spawnIndex");
        public byte SpawnIndex { get; }
    }
    
    [Tracked]

    [CustomEntity("Madhunt/SeekerSpawnPoint")]
    public class SeekerSpawnPoint : Entity {
        public SeekerSpawnPoint(EntityData data, Vector2 off) : base(data.Position + off) => SpawnIndex = (byte) data.Int("spawnIndex");
        public byte SpawnIndex { get; }
    }
}