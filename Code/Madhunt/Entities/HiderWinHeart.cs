using System.Reflection;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Madhunt {
    //TODO Rework this to use static hooks (+ add unlocking collectables)
    [CustomEntity("Madhunt/HiderWinHeart")]
    public class HiderWinHeart : HeartGem {
        private static readonly FieldInfo DASH_ATTACK_TIMER_FIELD = typeof(Player).GetField("dashAttackTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        private On.Celeste.HeartGem.hook_OnPlayer onPlayerHook;
        private On.Celeste.HeartGem.hook_Collect collectHook;
        private On.Celeste.HeartGem.hook_EndCutscene endCutsceneHook;
        private On.Celeste.HeartGem.hook_IsCompleteArea completeAreaHook;
        private On.Celeste.HeartGem.hook_RegisterAsCollected registerCollectedHook;

        public HiderWinHeart(EntityData data, Vector2 offset) : base(data.Position + offset) {
            IsFake = false;
            On.Celeste.HeartGem.OnPlayer += onPlayerHook = (orig, heart, player) => {
                if(heart == this && MadhuntModule.CurrentRound?.PlayerRole != PlayerRole.HIDER && player.DashAttacking) {
                    player.StateMachine.State = Player.StNormal;
                    DASH_ATTACK_TIMER_FIELD.SetValue(player, 0f);
                }
                orig(heart, player);
            };
            On.Celeste.HeartGem.Collect += collectHook = (orig, heart, player) => {
                if(heart != this || MadhuntModule.CurrentRound?.PlayerRole == PlayerRole.HIDER) orig(heart, player);
            };
            On.Celeste.HeartGem.EndCutscene += endCutsceneHook = (orig, heart) => {
                orig(heart);
                if(heart == this) MadhuntModule.EndRound(PlayerRole.HIDER);  
            };
            On.Celeste.HeartGem.IsCompleteArea += completeAreaHook = (orig, heart, b) => {
                if(heart != this) return orig(heart, b);
                return false;
            };
            On.Celeste.HeartGem.RegisterAsCollected += registerCollectedHook = (orig, heart, level, poem) => {
                if(heart != this) orig(heart, level, poem);
            };
        }

        public override void Removed(Scene scene) {
            base.Removed(scene);
            On.Celeste.HeartGem.OnPlayer -= onPlayerHook;
            On.Celeste.HeartGem.Collect -= collectHook;
            On.Celeste.HeartGem.EndCutscene -= endCutsceneHook;
            On.Celeste.HeartGem.IsCompleteArea -= completeAreaHook;
            On.Celeste.HeartGem.RegisterAsCollected -= registerCollectedHook;
        }
    }
}