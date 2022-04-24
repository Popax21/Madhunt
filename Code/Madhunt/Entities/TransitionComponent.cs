using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MonoMod.Utils;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using MonoMod.RuntimeDetour;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Madhunt {
    public class TransitionComponent : GameComponent {
        private static readonly FieldInfo BOOST_DIR_FIELD = typeof(Player).GetField("gliderBoostDir", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private ILHook redDashCoroutineHook;
        private Level transitionLevel;
        private TransitionBooster transitionBooster, newTransitionBooster;
        private Vector2 newTransitionBoosterDir;

        public TransitionComponent(Game game) : base(game) {
            On.Celeste.Level.EnforceBounds += OnEnforceBounds;
            On.Celeste.Level.LoadNewPlayer += OnLoadNewPlayer;
            redDashCoroutineHook = new ILHook(typeof(Player).GetMethod("RedDashCoroutine", BindingFlags.NonPublic | BindingFlags.Instance).GetStateMachineTarget(), ILRedDashCoroutine);
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            On.Celeste.Level.EnforceBounds -= OnEnforceBounds;
            On.Celeste.Level.LoadNewPlayer -= OnLoadNewPlayer;
            redDashCoroutineHook.Dispose();
        }

        private void OnEnforceBounds(On.Celeste.Level.orig_EnforceBounds orig, Level level, Player player) {
            if(player.StateMachine.State == Player.StRedDash && player.LastBooster is TransitionBooster transBooster && transBooster.TargetDashDir.HasValue && (Vector2) BOOST_DIR_FIELD.GetValue(player) == TransitionBooster.DIRECTIONS[transBooster.TargetDashDir.Value]) {
                if(transBooster == newTransitionBooster)
                    return;

                if(
                    player.Right < level.Bounds.Left ||
                    level.Bounds.Right < player.Left ||
                    player.Bottom < level.Bounds.Top ||
                    level.Bounds.Bottom < player.Top
                ) {
                    //Transition to the the target room
                    Level origLevel = (Level) Celeste.Scene;
                    origLevel.Session.Area = transBooster.TargetArea;
                    origLevel.Session.Level = transBooster.TargetLevel;
                    origLevel.Session.RespawnPoint = null;

                    LevelLoader loader = new LevelLoader(origLevel.Session);
                    transitionLevel = loader.Level;
                    transitionBooster = transBooster;
                    Celeste.Scene = loader;
                }
            } else orig(level, player);
        }

        private Player OnLoadNewPlayer(On.Celeste.Level.orig_LoadNewPlayer orig, Vector2 position, PlayerSpriteMode spriteMode) {
            Player player = orig(position, spriteMode);

            if(transitionLevel != null) {
                transitionLevel.Entities.UpdateLists();
                
                //Get the booster target
                TransitionBoosterTarget target = transitionLevel.Tracker.GetEntities<TransitionBoosterTarget>().Cast<TransitionBoosterTarget>().Where(t => t.TargetID == transitionBooster.TargetID).FirstOrDefault();
                if(target != null) player.Add(new Coroutine(TransitionRoutine(transitionLevel, player, target)));
                else Logger.Log(LogLevel.Warn, MadhuntModule.Name, $"Transition target {transitionBooster.TargetID} doesn't exist in level {transitionLevel.Session.Level}!");
                
                transitionLevel = null;
                transitionBooster = null;
            }

            return player;
        }

        private IEnumerator TransitionRoutine(Level level, Player player, TransitionBoosterTarget target) {
            Vector2 boosterDir = TransitionBooster.DIRECTIONS[target.BoosterDir];
            Vector2 boosterPos = target.Position - 32f*boosterDir;

            //Set the player's position
            yield return null;
            player.StateMachine.State = Player.StDummy;
            player.Position = boosterPos;

            //Calculate the camera position
            Vector2 camPos;
            if(boosterDir.X > 0) camPos.X = target.Right;
            else if(boosterDir.X < 0) camPos.X = target.Left - level.Camera.Viewport.X;
            else camPos.X = target.CenterX - level.Camera.Viewport.X/2;

            if(boosterDir.Y > 0) camPos.Y = target.Bottom;
            else if(boosterDir.Y < 0) camPos.Y = target.Top - level.Camera.Viewport.Y;
            else camPos.Y = target.CenterY - level.Camera.Viewport.Y/2;

            //Wait until the wipe started
            level.Camera.Position = camPos;
            while(level.Wipe == null) {
                yield return null;
                level.Camera.Position = camPos;
            }

            //Wait a bit
            player.StateMachine.State = Player.StDummy;
            yield return 0.3f;

            //Make the player move in the transtion booster
            TransitionBooster booster = newTransitionBooster = new TransitionBooster(boosterPos);
            level.Add(booster);
            yield return null;

            player.CurrentBooster = player.LastBooster = booster;
            player.StateMachine.State = Player.StRedDash;
            booster.PlayerBoosted(player, newTransitionBoosterDir = boosterDir);
        }

        private void ILRedDashCoroutine(ILContext ctx) {
            ILCursor cursor = new ILCursor(ctx);

            //Hook accesses to "lastAim"
            while(cursor.TryGotoNext(MoveType.After, i => i.MatchLdfld(typeof(Player).GetField("lastAim", BindingFlags.NonPublic | BindingFlags.Instance)))) {
                cursor.Emit(OpCodes.Ldloc_1);
                cursor.EmitDelegate<Func<Vector2, Player, Vector2>>((lastAim, player) => {
                    if(player.LastBooster == newTransitionBooster) return newTransitionBoosterDir;
                    else return lastAim;
                });
            }
        }
    }
}
        