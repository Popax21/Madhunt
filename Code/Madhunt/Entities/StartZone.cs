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

    [Tracked]
    [CustomEntity("Madhunt/StartZoneIndicator")]
    public class StartZoneIndicator : Entity {
        private const float COLOR_LERP_TIME = 0.3f;

        private StartZone startZone;
        private float width, height;
        private float colLerp = 0f;

        public StartZoneIndicator(EntityData data, Vector2 offset) : base(data.Position + offset) {
            Depth = 5000;
            width = data.Width;
            height = data.Height;
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);
            startZone = scene.CollideFirst<StartZone>(Position, Position + new Vector2(width, height));
        }

        public override void Render() {
            base.Render();

            //Draw zone bounds
            Player player = Scene.Tracker.GetEntity<Player>();
            if(player != null && startZone != null) {
                bool inZone = player.CollideCheck(startZone);
                if(inZone && colLerp < 1) colLerp += Engine.DeltaTime / COLOR_LERP_TIME;
                else if(!inZone && colLerp > 0) colLerp -= Engine.DeltaTime / COLOR_LERP_TIME;
                colLerp = Calc.Clamp(colLerp, 0, 1);
            }
            Draw.Rect(Position, width, height, Color.Lerp(Color.White, Color.LightGreen, colLerp) * 0.45f);
        }

        public int ID { get; }
    }
}