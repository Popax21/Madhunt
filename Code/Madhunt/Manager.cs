using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.RuntimeDetour;
using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.CelesteNet.Client.Entities;
using Celeste.Mod.CelesteNet.DataTypes;
using System.Reflection;
using System.Collections.Concurrent;

namespace Celeste.Mod.Madhunt {
    //TODO Rewrite this to be modular and not a big blob of logic
    public class Manager : GameComponent {
        private class RoundState {
            public RoundSettings settings;
            public int playerSeed;
            public PlayerState playerState;
            public bool initialSpawn, othersJoined, isWinner;
        }

        private CelesteNetClientModule module;
        private Delegate initHook, disposeHook;
        private Hook ghostPlayerCollisionHook, ghostNameRenderHook;

        private RoundState roundState = null;
        private Level arenaLoadLevel = null;
        private float startDelayTimer = -1f, startTimer = 0f;
        private ConcurrentQueue<Action> updateQueue = new ConcurrentQueue<Action>();

        public Manager(Game game) : base(game) {
            //Get the Celeste.NET module
            if((module = (CelesteNetClientModule) Everest.Modules.FirstOrDefault(m => m is CelesteNetClientModule)) == null) throw new Exception("CelesteNET not loaded!");

            //Install Celeste hooks
            Everest.Events.Level.OnLoadLevel += LevelLoadHook;
            Everest.Events.Level.OnExit += ExitHook;
            On.Celeste.Level.LoadNewPlayer += PlayerLoadHook;
            On.Celeste.Level.EnforceBounds += EnforceBoundsHook;
            On.Celeste.Player.Die += DieHook;

            //Install CelesteNet context hooks
            EventInfo initEvt = typeof(CelesteNetClientContext).GetEvent("OnInit");
            if(initEvt.EventHandlerType.GenericTypeArguments[0] == typeof(CelesteNetClientContext)) {
                initEvt.AddEventHandler(null, initHook = (Action<CelesteNetClientContext>) (_ => module.Context.Client.Data.RegisterHandlersIn(this)));
            } else {
                initEvt.AddEventHandler(null, initHook = (Action<object>) (_ => module.Context.Client.Data.RegisterHandlersIn(this)));
            }

            EventInfo disposeEvt = typeof(CelesteNetClientContext).GetEvent("OnDispose");
            if(disposeEvt.EventHandlerType.GenericTypeArguments[0] == typeof(CelesteNetClientContext)) {
                disposeEvt.AddEventHandler(null, disposeHook = (Action<CelesteNetClientContext>) (_ => StopRound()));
            } else {
                disposeEvt.AddEventHandler(null, disposeHook = (Action<object>) (_ => StopRound()));
            }
            
            //Install CelesteNet ghost hooks
            ghostPlayerCollisionHook = new Hook(typeof(Ghost).GetMethod(nameof(Ghost.OnPlayer)), GetType().GetMethod(nameof(GhostPlayerCollisionHook)));
            ghostNameRenderHook = new Hook(typeof(GhostNameTag).GetMethod(nameof(GhostNameTag.Render)), GetType().GetMethod(nameof(GhostNameTagRenderHook)));
        }

        protected override void Dispose(bool disposing) {
            //Remove hooks
            Everest.Events.Level.OnLoadLevel -= LevelLoadHook;
            Everest.Events.Level.OnExit -= ExitHook;
            On.Celeste.Level.LoadNewPlayer -= PlayerLoadHook;
            On.Celeste.Level.EnforceBounds -= EnforceBoundsHook;
            On.Celeste.Player.Die -= DieHook;

            if(initHook != null) typeof(CelesteNetClientContext).GetEvent("OnInit").RemoveEventHandler(null, initHook);
            initHook = null;

            if(disposeHook != null) typeof(CelesteNetClientContext).GetEvent("OnDispose").RemoveEventHandler(null, disposeHook);
            disposeHook = null;

            if(ghostPlayerCollisionHook != null) ghostPlayerCollisionHook.Dispose();
            ghostPlayerCollisionHook = null;

            if(ghostNameRenderHook != null) ghostNameRenderHook.Dispose();
            ghostNameRenderHook = null;

            base.Dispose(disposing);
        }

        public override void Update(GameTime gameTime) {
            //Update start timer
            if(startTimer > 0) startTimer -= (float) gameTime.ElapsedGameTime.TotalSeconds;
            
            //Update start delay timer
            if(roundState != null && !InRound) {
                startDelayTimer += Engine.RawDeltaTime;
                Engine.TimeRate = (float) Math.Exp(-6f * startDelayTimer);

                if(startDelayTimer > 0.75f) {
                    //Check if anyone else is in the same round
                    if(!GetGhostStates().Any(ghostState => ghostState.RoundState != null && ghostState.RoundState.Value.roundID == roundState.settings.RoundID)) {
                        Engine.Scene?.Tracker?.GetEntity<Player>()?.Die(Vector2.Zero, true, false);
                        roundState = null;
                        return;
                    }

                    //Determine and set the player state
                    PlayerState state;
                    if(roundState.settings.initialSeekers > 0) {
                        state = (GetGhostStates().Count(ghostState => {
                            if(ghostState.RoundState == null || ghostState.RoundState.Value.roundID != roundState.settings.RoundID) return false;
                            int ghostSeed = ghostState.RoundState.Value.seed;
                            return ghostSeed > roundState.playerSeed || (ghostSeed == roundState.playerSeed && ghostState.Player.ID > (module?.Context?.Client?.PlayerInfo?.ID ?? 0));
                        }) < roundState.settings.initialSeekers) ? PlayerState.SEEKER : PlayerState.HIDER;
                    } else {
                        state = (GetGhostStates().Count(ghostState => {
                            if(ghostState.RoundState == null || ghostState.RoundState.Value.roundID != roundState.settings.RoundID) return false;
                            int ghostSeed = ghostState.RoundState.Value.seed;
                            return ghostSeed > roundState.playerSeed || (ghostSeed == roundState.playerSeed && ghostState.Player.ID > (module?.Context?.Client?.PlayerInfo?.ID ?? 0));
                        }) < -roundState.settings.initialSeekers) ? PlayerState.HIDER : PlayerState.SEEKER;
                    }
                    Logger.Log(Module.Name, $"Determined state {state} for Madhunt {roundState.settings.RoundID}");
                    State = state;

                    //Change the spawnpoint and respawn player
                    RespawnInArena();
                }
            }

            //Clear update queue
            ConcurrentQueue<Action> queue = updateQueue;
            updateQueue = new ConcurrentQueue<Action>();
            foreach(Action act in queue) act();
            
            base.Update(gameTime);
        }

        public bool StartRound(RoundSettings settings, int? startZoneID=null) {
            if(module?.Context == null || !module.Client.IsReady || InRound) return false;

            //Send start packet
            module.Context.Client.Send<DataMadhuntStart>(new DataMadhuntStart() {
                MajorVersion = Module.Instance.Metadata.Version.Major,
                MinorVersion = Module.Instance.Metadata.Version.Minor,
                StartPlayer = module.Context.Client.PlayerInfo,
                RoundSettings = settings,
                StartZoneID = startZoneID
            });

            StartInternal(settings);
            return true;
        }

        private void StartInternal(RoundSettings settings) {
            if(roundState != null) return;
            
            //Create round state
            roundState = new RoundState() { settings = settings, playerSeed = Calc.Random.Next(int.MinValue, int.MaxValue), initialSpawn = true, othersJoined = false, isWinner = false };
            State = PlayerState.SEEDWAIT;
            startDelayTimer = 0;
            Logger.Log(Module.Name, $"Starting Madhunt {roundState.settings.RoundID} with seed {roundState.playerSeed}");
        }

        public void StopRound() {
            if(!InRound) return;
            Logger.Log(Module.Name, $"Stopping Madhunt {roundState.settings.RoundID} in state {roundState.playerState}{(roundState.isWinner ? " as winner" : string.Empty)}");

            //Set state
            RoundState state = roundState;
            State = null;

            //Respawn the player in the lobby
            RespawnInLobby(state);
        }

        public void TriggerGlobalWin(PlayerState winningState) {
            if(!InRound) return;
            Logger.Log(Module.Name, $"Triggering global win for state {winningState} in Madhunt {roundState.settings.RoundID}");
            module?.Client?.SendAndHandle(new DataMadhuntRoundEnd() {
                EndPlayer = module.Client.PlayerInfo,
                RoundID = roundState.settings.RoundID,
                WinningState = winningState
            });
        }

        private void CheckRoundEnd(bool endIfEmpty = true, Action<bool> callback = null) {
            if(!InRound) return;

            //Check if there are any hider or seeker ghosts
            if(
                !(endIfEmpty || GetGhostStates().Any(state => state.RoundState?.roundID == roundState.settings.RoundID)) || 
                (
                    (State == PlayerState.HIDER || GetGhostStates().Any(state => state.RoundState?.roundID == roundState.settings.RoundID && state.RoundState?.state == PlayerState.HIDER)) &&
                    (State == PlayerState.SEEKER || GetGhostStates().Any(state => state.RoundState?.roundID == roundState.settings.RoundID && state.RoundState?.state == PlayerState.SEEKER))
                )
            ) {
                roundState.isWinner = false;
                callback?.Invoke(false);
                return;
            }

            //If we're still a hider, continue the round
            if(State == PlayerState.HIDER) {
                roundState.isWinner = true;
                callback?.Invoke(false);
                return;
            }

            //End the round
            StopRound();
            callback?.Invoke(true);
        }

        private void RespawnInLobby(RoundState state=null) {
            state ??= roundState;

            Session ses = (Celeste.Scene as Level)?.Session;
            if(ses != null) {
                //Change session
                ses.Area = state.settings.lobbyArea;
                ses.Level = state.settings.lobbyLevel;
                ses.RespawnPoint = state.settings.lobbySpawnPoint;
                ses.Inventory.DreamDash = state.isWinner;
                Celeste.Scene = new LevelLoader(ses, ses.RespawnPoint);
            } else updateQueue.Enqueue(() => RespawnInLobby(state));
        }

        private void RespawnInArena() {
            Session ses = (Celeste.Scene as Level)?.Session;
            if(ses != null) {
                //Change session
                ses.Area = roundState.settings.arenaArea;
                ses.Level = roundState.settings.spawnLevel;
                ses.Inventory.DreamDash = true;
                LevelLoader loader = new LevelLoader(ses);
                arenaLoadLevel = loader.Level;
                Celeste.Scene = loader;
            } else updateQueue.Enqueue(() => RespawnInArena());
        }
        
        private DataMadhuntStateUpdate GetGhostState(DataPlayerInfo info) {
            DataMadhuntStateUpdate ghostState = null;
            if(!(module.Context?.Client?.Data?.TryGetBoundRef<DataPlayerInfo,DataMadhuntStateUpdate>(info.ID, out ghostState) ?? false)) return null;
            return ghostState;
        }
        private IEnumerable<DataMadhuntStateUpdate> GetGhostStates() => module?.Context?.Client?.Data?.GetRefs<DataPlayerInfo>().Where(i => !string.IsNullOrEmpty(i.DisplayName)).Select(i => GetGhostState(i)).Where(s => s != null) ?? Enumerable.Empty<DataMadhuntStateUpdate>();

        private void LevelLoadHook(Level lvl, Player.IntroTypes intro, bool fromLoader) {
            //Disable save and quit when in a round
            if(InRound) lvl.SaveQuitDisabled = true;
        }

        private void ExitHook(Level lvl, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            if(arenaLoadLevel == null) State = null;
        }

        private Player PlayerLoadHook(On.Celeste.Level.orig_LoadNewPlayer orig, Vector2 pos, PlayerSpriteMode smode) {
            try {
                Level arenaLevel = arenaLoadLevel;
                arenaLoadLevel = null;

                //Set the start timer
                startTimer = 5f;

                //Change the sprite mode
                Player player = orig(pos, State switch {
                        PlayerState.HIDER => PlayerSpriteMode.Madeline,
                        PlayerState.SEEKER => PlayerSpriteMode.MadelineAsBadeline,
                        _ => smode
                });

                if(InRound && arenaLevel != null) {
                    //Set respawn point
                    arenaLevel.Entities.UpdateLists();
                    Vector2? spawnPos = ((roundState.playerState == PlayerState.HIDER) ? 
                        arenaLevel.Tracker.GetEntities<HiderSpawnPoint>().FirstOrDefault(s => ((HiderSpawnPoint) s).SpawnIndex == roundState.settings.spawnIndex)?.Position : 
                        arenaLevel.Tracker.GetEntities<SeekerSpawnPoint>().FirstOrDefault(s => ((SeekerSpawnPoint) s).SpawnIndex == roundState.settings.spawnIndex)?.Position 
                    );
                    if(!spawnPos.HasValue) Logger.Log(LogLevel.Warn, Module.Name, $"Didn't find spawnpoint with index {roundState.settings.spawnIndex} for Madhunt round {roundState.settings.RoundID}!");
                    arenaLevel.Session.RespawnPoint = player.Position = spawnPos ?? arenaLevel.DefaultSpawnPoint;
                }

                return player;
            } catch(Exception) {
                State = null;
                arenaLoadLevel = null;
                throw;
            }
        }

        private void EnforceBoundsHook(On.Celeste.Level.orig_EnforceBounds orig, Level level, Player player) {
            orig(level, player);
            if(level.Transitioning || !InRound)
                return;
            
            if(player.Top < level.Bounds.Top && !level.Session.MapData.CanTransitionTo(level, player.TopCenter - Vector2.UnitY*12f)) {
                player.Top = level.Bounds.Top;
                player.OnBoundsV();
            }
        }

        private PlayerDeadBody DieHook(On.Celeste.Player.orig_Die orig, Player player, Vector2 dir, bool evenIfInvincible, bool registerDeath) {
            PlayerDeadBody body = orig(player, dir, evenIfInvincible, registerDeath);
            if(body != null && InRound && roundState.settings.goldenMode && State == PlayerState.HIDER) {
                Action oldDeathAct = body.DeathAction;
                body.DeathAction = () => {
                    oldDeathAct();
                    State = PlayerState.SEEKER;
                    CheckRoundEnd(false, ended => { if(!ended) RespawnInArena(); });
                };
            }
            return body;
        }

        private void GhostPlayerCollisionHook(Action<Ghost, Player> orig, Ghost ghost, Player player) {
            //Check if we collided with a seeker ghost as a hider
            DataMadhuntStateUpdate ghostState = GetGhostState(ghost.PlayerInfo);
            if(InRound && roundState.settings.tagMode && ghostState?.RoundState?.roundID == roundState.settings.RoundID) {
                if(State == PlayerState.HIDER && ghostState?.RoundState?.state == PlayerState.SEEKER) {
                    //Check the start timer
                    if(startTimer > 0) return;

                    //Turn the player into a seeker
                    player.Die(ghost.Speed, evenIfInvincible: true).DeathAction = () => {
                        State = PlayerState.SEEKER;
                        CheckRoundEnd(false, ended => { if(!ended) RespawnInArena(); });
                    };
                } else if (State == ghostState?.RoundState?.state)
                    orig(ghost, player);
            } else orig(ghost, player);
        }

        private void GhostNameTagRenderHook(Action<GhostNameTag> orig, GhostNameTag nameTag) {
            if(!InRound || !roundState.settings.hideNames || !(nameTag.Tracking is Ghost ghost) || State == GetGhostState(ghost.PlayerInfo)?.RoundState?.state) orig(nameTag);
        }

        public void Handle(CelesteNetConnection con, DataMadhuntStart data) {
            //Check if the version is compatible
            if(data.MajorVersion != Module.Instance.Metadata.Version.Major || data.MinorVersion != Module.Instance.Metadata.Version.Minor) {
                Logger.Log(LogLevel.Warn, Module.Name, $"Ignoring Madhunt start packet with incompatible version {data.MajorVersion}.{data.MinorVersion} vs installed {Module.Instance.Metadata.Version}");
                return;
            }

            updateQueue.Enqueue(() => {
                //Check if we should start
                Session ses = (Celeste.Scene as Level)?.Session;
                if(InRound || ses == null || ses.Area != data.RoundSettings.lobbyArea || ses.Level != data.RoundSettings.lobbyLevel) return;
                
                //Check if the zone ID matches
                if(data.StartZoneID.HasValue && data.StartZoneID != Celeste.Scene.Tracker.GetEntity<Player>()?.CollideFirst<StartZone>()?.ID) return;

                //Start the madhunt
                updateQueue.Enqueue(() => StartInternal(data.RoundSettings));
            });
        }
        
        public void Handle(CelesteNetConnection con, DataMadhuntRoundEnd data) {
            updateQueue.Enqueue(() => {
                if(roundState == null || data.RoundID != roundState.settings.RoundID) return;
                roundState.isWinner = (State == data.WinningState);
                StopRound();
            });
        }

        public void Handle(CelesteNetConnection con, DataMadhuntStateUpdate data) {
            updateQueue.Enqueue(() => {
                if(roundState != null && data.RoundState?.roundID == roundState.settings.RoundID) roundState.othersJoined = true;
                CheckRoundEnd(roundState?.othersJoined ?? false);
            });
        }
        
        public void Handle(CelesteNetConnection con, DataPlayerInfo data) => MainThreadHelper.Do(() => CheckRoundEnd());

        public bool InRound => roundState != null && roundState.playerState != PlayerState.SEEDWAIT;
        public PlayerState? State {
            get => roundState?.playerState;
            private set {
                //Change round state 
                if(value.HasValue) roundState.playerState = value.Value; 
                else roundState = null;

                //Send state update packet
                module?.Context?.Client?.Send<DataMadhuntStateUpdate>(new DataMadhuntStateUpdate() {
                    Player = module?.Context?.Client?.PlayerInfo,
                    RoundState = (roundState != null) ? ((string, int, PlayerState)?) (roundState.settings.RoundID, roundState.playerSeed, roundState.playerState) : null
                });
            }
        }
    }
}