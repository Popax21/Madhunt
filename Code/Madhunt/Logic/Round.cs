using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Monocle;
using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.CelesteNet.Client.Entities;

namespace Celeste.Mod.Madhunt {
    public class MadhuntRound {
        public const float RESPAWN_INVINCIBILITY = 1f, TRANSITION_INVINCIBILITY = 0.25f;
        private static Random SEED_RNG = new Random();

#region Hooks

        private static Hook ghostPlayerCollisionHook, ghostNameRenderHook;
        
        internal static void Init() {
            On.Celeste.Level.LoadNewPlayer += PlayerLoadHook;
            On.Celeste.Level.Reload += ReloadHook;
            On.Celeste.Level.EnforceBounds += EnforceBoundsHook;
            On.Celeste.Player.Die += DieHook;
            On.Celeste.Holdable.Pickup += PickupHook;
            
            ghostPlayerCollisionHook = new Hook(typeof(Ghost).GetMethod(nameof(Ghost.OnPlayer)), typeof(MadhuntRound).GetMethod(nameof(GhostPlayerCollisionHook), BindingFlags.NonPublic | BindingFlags.Static));
            ghostNameRenderHook = new Hook(typeof(GhostNameTag).GetMethod(nameof(GhostNameTag.Render)), typeof(MadhuntRound).GetMethod(nameof(GhostNameTagRenderHook), BindingFlags.NonPublic | BindingFlags.Static));
        }

        internal static void Uninit() {
            On.Celeste.Level.LoadNewPlayer -= PlayerLoadHook;
            On.Celeste.Level.Reload -= ReloadHook;
            On.Celeste.Level.EnforceBounds -= EnforceBoundsHook;
            On.Celeste.Player.Die -= DieHook;
            On.Celeste.Holdable.Pickup -= PickupHook;
            
            ghostPlayerCollisionHook.Dispose();
            ghostPlayerCollisionHook = null;
            ghostNameRenderHook.Dispose();
            ghostNameRenderHook = null;
        }

        private static Player PlayerLoadHook(On.Celeste.Level.orig_LoadNewPlayer orig, Vector2 pos, PlayerSpriteMode smode) {
            MadhuntRound round = MadhuntModule.CurrentRound;
            if(round == null) return orig(pos, smode);

            //Set the invincibility timer
            round.invincTimer = Calc.Max(round.invincTimer, RESPAWN_INVINCIBILITY);

            //Change the sprite mode
            Player player = orig(pos, round.PlayerRole switch {
                PlayerRole.HIDER => PlayerSpriteMode.Madeline,
                PlayerRole.SEEKER => PlayerSpriteMode.MadelineAsBadeline,
                _ => smode
            });

            if(round.IsActive && !round.spawnedInArena) {
                round.spawnedInArena = true;
                Level arenaLevel = round.arenaLevel;

                //Set respawn point
                arenaLevel.Entities.UpdateLists();
                Vector2? spawnPos = ((round.PlayerRole == PlayerRole.HIDER) ? 
                    arenaLevel.Tracker.GetEntities<HiderSpawnPoint>().FirstOrDefault(s => ((HiderSpawnPoint) s).SpawnIndex == round.Settings.spawnIndex)?.Position : 
                    arenaLevel.Tracker.GetEntities<SeekerSpawnPoint>().FirstOrDefault(s => ((SeekerSpawnPoint) s).SpawnIndex == round.Settings.spawnIndex)?.Position 
                );
                if(!spawnPos.HasValue) Logger.Log(LogLevel.Warn, MadhuntModule.Name, $"Couldn't find spawnpoint with index {round.Settings.spawnIndex} for Madhunt round {round.Settings.RoundID}!");
                arenaLevel.Session.RespawnPoint = player.Position = spawnPos ?? arenaLevel.DefaultSpawnPoint;
            }

            return player;
        }

        private static void ReloadHook(On.Celeste.Level.orig_Reload orig, Level level) {
            orig(level);
            if(MadhuntModule.CurrentRound == null) return;

            //Set the invincibility timer
            MadhuntModule.CurrentRound.invincTimer = Calc.Max(MadhuntModule.CurrentRound.invincTimer, RESPAWN_INVINCIBILITY);
        }

        private static void EnforceBoundsHook(On.Celeste.Level.orig_EnforceBounds orig, Level level, Player player) {
            orig(level, player);
            if(level.Transitioning || MadhuntModule.CurrentRound == null) return;

            //Stop the player from going over the top of the screen (to prevent cheeky hiding spots)    
            //Still allow for normal transitions though        
            if(player.Top < level.Bounds.Top && !level.Session.MapData.CanTransitionTo(level, player.TopCenter - Vector2.UnitY*12f)) {
                player.Top = level.Bounds.Top;
                player.OnBoundsV();
            }
        }

        private static PlayerDeadBody DieHook(On.Celeste.Player.orig_Die orig, Player player, Vector2 dir, bool evenIfInvincible, bool registerDeath) {
            PlayerDeadBody body = orig(player, dir, evenIfInvincible, registerDeath);

            //If a hider dies while in a golden mode round, make them a seeker
            MadhuntRound round = MadhuntModule.CurrentRound;
            if(body != null && round != null && round.Settings.goldenMode && round.PlayerRole == PlayerRole.HIDER) {
                Action oldDeathAct = body.DeathAction;
                body.DeathAction = () => {
                    oldDeathAct?.Invoke();

                    //Turn the player into a seeker
                    round = MadhuntModule.CurrentRound;
                    if(round == null) return;
                    round.PlayerRole = PlayerRole.SEEKER;
                };
            }
            return body;
        }

        private static bool PickupHook(On.Celeste.Holdable.orig_Pickup orig, Holdable holdable, Player player) {
            //Does the holdable belong to a ghost?
            MadhuntRound round = MadhuntModule.CurrentRound;
            if(round == null || !round.Settings.tagMode || !(holdable.Entity is Ghost ghost)) return orig(holdable, player);

            //Is the ghost in the same round and not in the same state?
            DataMadhuntStateUpdate ghostState = round.GetGhostState(ghost.PlayerInfo);
            if(ghostState?.State == null || ghostState.State.Value.role == round.PlayerRole) return orig(holdable, player);

            //Don't allow grabbing between players in different roles
            return false;
        }

        private static void GhostPlayerCollisionHook(Action<Ghost, Player> orig, Ghost ghost, Player player) {
            //Check if we collided with a seeker ghost as a hider
            MadhuntRound round = MadhuntModule.CurrentRound;
            if(round != null && round.Settings.tagMode && round.GetGhostState(ghost.PlayerInfo) is var ghostState && ghostState?.State?.roundID == round.Settings.RoundID) {
                if(round.PlayerRole == PlayerRole.HIDER && ghostState?.State?.role == PlayerRole.SEEKER) {
                    //Check for invincibiliy
                    if(round.invincTimer > 0 || ((Celeste.Scene as Level)?.Transitioning ?? false)) return;

                    //Turn the player into a seeker
                    player.Die(Vector2.Zero, evenIfInvincible: true).DeathAction = () => {
                        if(MadhuntModule.CurrentRound == null) return;
                        MadhuntModule.CurrentRound.PlayerRole = PlayerRole.SEEKER;
                    };
                } else if(round.PlayerRole == ghostState?.State?.role) {
                    //Only handle the collision if the ghost has the same role
                    //This prevents bouncing of seekers/hiders when interactions are enabled
                    orig(ghost, player);
                }
            } else orig(ghost, player);
        }
        
        private static void GhostNameTagRenderHook(Action<GhostNameTag> orig, GhostNameTag nameTag) {
            //Don't render name tags of other roles (if disabled in the settings)
            MadhuntRound round = MadhuntModule.CurrentRound;
            if(round != null && round.Settings.hideNames && nameTag.Tracking is Ghost ghost) {
                PlayerState? ghostState = round.GetGhostState(ghost.PlayerInfo)?.State;
                if(ghostState?.role != round.State.role) return;
            }
            
            orig(nameTag);
        }
#endregion

        public readonly RoundSettings Settings;
        public readonly CelesteNetClient NetClient;

        private int playerSeed;
        private PlayerRole playerRole;
        private float invincTimer;

        private Level arenaLevel;
        private SessionSnapshot sesSnapshot;

        //Expose flags for debugging
        internal bool isWinner, skipEndCheck, spawnedInArena;

        internal MadhuntRound(CelesteNetClient client, RoundSettings settings) {
            Settings = settings;
            NetClient = client;

            //Determine seed
            //Query the random number generator a random number of times
            int numIter = Environment.TickCount % 16;
            do { playerSeed = SEED_RNG.Next(int.MinValue, int.MaxValue); } while(numIter-- > 0);

            //Register handlers
            NetClient.Data.RegisterHandlersIn(this);

            //Start seed wait
            Logger.Log(MadhuntModule.Name, $"Starting Madhunt round {Settings.RoundID} with seed {playerSeed}");
            skipEndCheck = true;
            PlayerRole = PlayerRole.SEEDWAIT;
        }

        internal bool EndSeedWait() {
            if(!InSeedWait) return false;

            //Check if anyone else is in the same round
            if(!GetGhostStates().Any()) return false;

            //Determine and set the player role
            if(Settings.initialSeekers > 0) {
                PlayerRole = (GetGhostStates().Count(ghostState => {
                    int ghostSeed = ghostState.State.Value.seed;
                    return ghostSeed > playerSeed || (ghostSeed == playerSeed && ghostState.Player.ID > NetClient.PlayerInfo.ID);
                }) < Settings.initialSeekers) ? PlayerRole.SEEKER : PlayerRole.HIDER;
            } else {
                PlayerRole = (GetGhostStates().Count(ghostState => {
                    int ghostSeed = ghostState.State.Value.seed;
                    return ghostSeed > playerSeed || (ghostSeed == playerSeed && ghostState.Player.ID > NetClient.PlayerInfo.ID);
                }) < -Settings.initialSeekers) ? PlayerRole.HIDER : PlayerRole.SEEKER;
            }

            Logger.Log(MadhuntModule.Name, $"Ended seed wait, determined role {PlayerRole} for Madhunt round {Settings.RoundID}");

            //Check if the round would immediatly end
            if(CheckRoundEnd()) return false;

            //Load into arena
            Session ses = (Celeste.Scene as Level)?.Session;
            if(ses == null) return false;
            sesSnapshot = new SessionSnapshot(ses);
            ses.Keys.Clear();
            ses.Inventory.DreamDash = true;
            if(MadhuntModule.Session != null) {
                MadhuntModule.Session.WonLastRound = false;
            }
            spawnedInArena = false;
            arenaLevel = LoadLevel(ses, Settings.arenaArea, Settings.spawnLevel);

            return true;
        }

        internal void Stop(bool isWinner=false, bool returnToLobby=true) {
            Logger.Log(MadhuntModule.Name, $"Stopping Madhunt round {Settings.RoundID} in role {PlayerRole}{(isWinner ? " as winner" : string.Empty)}");

            //Unregister handlers
            NetClient.Data.UnregisterHandlersIn(this);

            //Send player state packet to notify other clients of round exit
            NetClient.Send(new DataMadhuntStateUpdate() {
                Player = NetClient.PlayerInfo,
                State = null
            });

            //Return to lobby
            if(arenaLevel != null && returnToLobby) {
                Session ses = arenaLevel.Session;
                sesSnapshot.Apply(ses);
                ses.RespawnPoint = Settings.lobbySpawnPoint;
                ses.Inventory.DreamDash = isWinner;
                if(MadhuntModule.Session != null) {
                    MadhuntModule.Session.WonLastRound = isWinner;
                }
                LoadLevel(ses, Settings.lobbyArea, Settings.lobbyLevel);

                arenaLevel = null;
                sesSnapshot = null;
            }
        }


        internal bool CheckRoundEnd() {
            //Check if there are both hiders and seekers left
            //Skip this check if the round just started
            if(
                (PlayerRole == PlayerRole.HIDER || GetGhostStates().Any(state => state.State?.role == PlayerRole.HIDER)) &&
                (PlayerRole == PlayerRole.SEEKER || GetGhostStates().Any(state => state.State?.role == PlayerRole.SEEKER))
            ) {
                //Check if we're the only remaining hider
                isWinner = PlayerRole == PlayerRole.HIDER && !GetGhostStates().Any(state => state.State?.role == PlayerRole.HIDER);
                skipEndCheck = false;
                return false;
            }
            if(skipEndCheck) return false;

            //End the round
            Stop(isWinner);
            return true;
        }

        internal void UpdateInvincibility(float dt) {
            if(invincTimer > 0) invincTimer -= dt;
            if(((Celeste.Scene as Level)?.Transitioning ?? false)) invincTimer = Calc.Max(invincTimer, TRANSITION_INVINCIBILITY);
        }

        private Level LoadLevel(Session ses, AreaKey area, string levelName) {
            //Cancel screen wipe
            if(Celeste.Scene is Level scLevel) scLevel.Wipe?.Cancel();

            //Set session
            ses.Area = area;
            ses.Level = levelName;

            //Create level loader
            LevelLoader loader = new LevelLoader(ses);
            Celeste.Scene = loader;
            return loader.Level;
        }

        public void UpdateFlags(Session ses) {
            ses.SetFlag("Madhunt_InRound", IsActive);
            ses.SetFlag("Madhunt_IsHider", IsActive && PlayerRole == PlayerRole.HIDER);
            ses.SetFlag("Madhunt_IsSeeker", IsActive && PlayerRole == PlayerRole.SEEKER);
        }

        public DataMadhuntStateUpdate GetGhostState(DataPlayerInfo info) {
            //Check if the player info is valid
            if(string.IsNullOrEmpty(info.DisplayName)) return null;

            //Check ghost state
            if(!NetClient.Data.TryGetBoundRef<DataPlayerInfo, DataMadhuntStateUpdate>(info.ID, out DataMadhuntStateUpdate ghostState)) return null;
            if(ghostState.State?.roundID != Settings.RoundID) return null;
            return ghostState;
        }

        public IEnumerable<DataMadhuntStateUpdate> GetGhostStates() =>
            NetClient.Data.GetRefs<DataPlayerInfo>()
            .Where(i => i != NetClient.PlayerInfo)
            .Select(i => GetGhostState(i))
            .Where(s => s != null)
        ;

        public bool InSeedWait => playerRole == PlayerRole.SEEDWAIT;
        public bool IsActive => playerRole != PlayerRole.SEEDWAIT;

        public int PlayerSeed => playerSeed;
        public PlayerRole PlayerRole {
            get => playerRole;
            private set {
                //Change role
                playerRole = value; 
                if(Celeste.Scene is Level lvl) UpdateFlags(lvl.Session);

                //Send state update packet
                NetClient.Send<DataMadhuntStateUpdate>(new DataMadhuntStateUpdate() {
                    Player = NetClient.PlayerInfo,
                    State = State
                });

                //Do a round end check
                MadhuntModule.Instance.roundManager.CheckRoundEnd();
            }
        }

        public PlayerState State => new PlayerState() {
            roundID = Settings.RoundID,
            seed = playerSeed,
            role = playerRole
        };
    }
}