using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;

using Monocle;
using Celeste.Mod.Entities;
using Celeste.Mod.Procedurline;

namespace Celeste.Mod.Madhunt {
    [Tracked]
    [CustomEntity("Madhunt/TransitionBooster")]
    public class TransitionBooster : CustomBooster {
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
        
        public static readonly Color COLOR = Calc.HexToColor("#521382");

        public TransitionBooster(Vector2 pos) : base(pos, COLOR) {}
        public TransitionBooster(EntityData data, Vector2 offset) : this(data.Position + offset) {
            TargetDashDir = (Direction) data.Int("targetDir");
            TargetArea = data.Attr("targetArea").ParseAreaKey();
            TargetLevel = data.Attr("targetLevel");
            TargetID = data.Int("targetID");
        }

        protected override BoostType? OnBoost(Player player) => BoostType.RED_BOOST;

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