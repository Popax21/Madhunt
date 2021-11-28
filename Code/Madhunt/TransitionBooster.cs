using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Madhunt {
    [Tracked]
    [CustomEntity("Madhunt/TransitionBooster")]
    public class TransitionBooster : Booster {
        public enum Direction {
            LEFTDOWN, LEFT, LEFTUP, UP, RIGHTUP, RIGHT, RIGHTDOWN, DOWN
        }

        public static readonly IReadOnlyDictionary<Direction, Vector2> DIRECTIONS = new Dictionary<Direction, Vector2>() {
            [Direction.LEFTDOWN] = new Vector2(-1, 1),
            [Direction.LEFT] = new Vector2(-1, 0),
            [Direction.LEFTUP] = new Vector2(-1, -1),
            [Direction.UP] = new Vector2(0, -1),
            [Direction.RIGHTUP] = new Vector2(1, -1),
            [Direction.RIGHT] = new Vector2(1, 0),
            [Direction.RIGHTDOWN] = new Vector2(1, 1),
            [Direction.DOWN] = new Vector2(0, 1)
        };

        private static readonly Color ORIG_COLOR = Calc.HexToColor("9c1105"), NEW_COLOR = Calc.HexToColor("521382");
        
        private static Sprite RECOLORED_SPRITE = null;

        private static readonly FieldInfo PARTICLE_TYPE_FIELD = typeof(Booster).GetField("particleType", BindingFlags.NonPublic | BindingFlags.Instance);
        private static On.Celeste.Booster.hook_AppearParticles appearParticlesHook;
        private static int appearParticlesCounter = 0;
        
        public TransitionBooster(Vector2 pos) : base(pos, true) {
            //Recolor the sprite
            Sprite sprite = Components.Get<Sprite>();
            if(RECOLORED_SPRITE == null) RECOLORED_SPRITE = sprite.Recolor(ORIG_COLOR, NEW_COLOR);
            RECOLORED_SPRITE.CloneInto(sprite);

            //Recolor particles
            ParticleType appearParticles = P_RedAppear.Recolor(ORIG_COLOR, NEW_COLOR);
            if(appearParticlesCounter++ <= 0) On.Celeste.Booster.AppearParticles += appearParticlesHook = (orig, booster) => {
                if(booster is TransitionBooster) {
                    ParticleSystem particlesBG = SceneAs<Level>()?.ParticlesBG;
                    if(particlesBG == null) return;
                    for(int i = 0; i < 360; i += 30) {
                        particlesBG.Emit(appearParticles, 1, Center, Vector2.One * 2f, i * Calc.DegToRad);
                    }
                } else orig(booster);
            };
            PARTICLE_TYPE_FIELD.SetValue(this, P_BurstRed.Recolor(ORIG_COLOR, NEW_COLOR));
        }

        public TransitionBooster(EntityData data, Vector2 offset) : this(data.Position + offset) {
            TargetDashDir = (Direction) data.Int("targetDir");
            TargetArea = data.Attr("targetArea").ParseAreaKey();
            TargetLevel = data.Attr("targetLevel");
            TargetID = data.Int("targetID");
        }

        public override void Removed(Scene scene) {
            base.Removed(scene);
            if(--appearParticlesCounter <= 0) On.Celeste.Booster.AppearParticles -= appearParticlesHook;
        }

        public override void Update() {
            base.Update();
            Components.Get<Sprite>().FlipX = false;
        }

        public Direction? TargetDashDir { get; }
        public AreaKey TargetArea { get; }
        public string TargetLevel { get; }
        public int TargetID { get; }
    }

    [Tracked]
    [CustomEntity("Madhunt/TransitionBoosterTarget")]
    public class TransitionBoosterTarget : Entity {
        public TransitionBoosterTarget(EntityData data, Vector2 offset) : base(data.Position + offset) {
            TargetID = data.Int("targetID");
            BoosterDir = (TransitionBooster.Direction) data.Int("boosterDir");
        }

        public int TargetID { get; }
        public TransitionBooster.Direction BoosterDir { get; }
    }
}