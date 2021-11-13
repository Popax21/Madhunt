using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.CelesteNet.Client.Entities;
using Celeste.Mod.CelesteNet.DataTypes;

using MonoMod.RuntimeDetour;

namespace Celeste.Mod.Madhunt {
    public class Manager : GameComponent {
        private class RoundState {
            public RoundSettings settings;
            public PlayerState playerState;
            public bool initialSpawn, othersJoined, isWinner;
        }

        private Everest.Events.Level.LoadLevelHandler levelLoadHook;
        private Everest.Events.Level.ExitHandler exitHook;
        private On.Celeste.Level.hook_LoadNewPlayer playerLoadHook;

        private CelesteNetClientModule module;
        private Action<Object> initHook, disposeHook;
        private Hook ghostPlayerCollisionHook, ghostNameRenderHook, ghostEmoteRenderHook;

        private RoundState roundState = null;
        private Level arenaLoadLevel = null;
        private float startTimer = 0f;
        //NOTE: DON'T USE QUEUES, OR EVEREST DECIDES TO DIE
        private List<Action> updateQueue = new List<Action>(), prevUpdateQueue = new List<Action>();

        public Manager(Game game) : base(game) {
            //Get the Celeste.NET module
            if((module = (CelesteNetClientModule) Everest.Modules.FirstOrDefault(m => m is CelesteNetClientModule)) == null) throw new Exception("CelesteNET not loaded!");

            //Install hooks
            Everest.Events.Level.OnLoadLevel += levelLoadHook = (lvl, intro, fromLoader) => {
                //Disable save and quit when in a round
                if(InRound) lvl.SaveQuitDisabled = true;
            };
            Everest.Events.Level.OnExit += exitHook = (lvl, exit, mode, session, snow) => {
                if(arenaLoadLevel == null) State = null;
            };
            On.Celeste.Level.LoadNewPlayer += playerLoadHook = (orig, pos, smode) => {
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
            };

            CelesteNetClientContext.OnInit += initHook = c => {
                CelesteNetClientContext ctx = (CelesteNetClientContext) c;

                //Register handlers
                ctx.Client.Data.RegisterHandler<DataMadhuntStart>((con, data) => MainThreadHelper.Do(() => {
                    //Check if the version is compatible
                    if(data.MajorVersion != Module.Instance.Metadata.Version.Major || data.MinorVersion != Module.Instance.Metadata.Version.Minor) {
                        Logger.Log(LogLevel.Warn, Module.Name, $"Ignoring start packet with incompatible version {data.MajorVersion}.{data.MinorVersion} vs installed {Module.Instance.Metadata.Version}");
                        return;
                    }

                    //Check if we should start
                    Session ses = (Celeste.Scene as Level)?.Session;
                    if(InRound || ses == null || ses.Area != data.RoundSettings.lobbyArea || ses.Level != data.RoundSettings.lobbyLevel) return;
                    
                    //Check if the zone ID matches
                    if(data.StartZoneID.HasValue && data.StartZoneID != Celeste.Scene.Tracker.GetEntity<Player>()?.CollideFirst<StartZone>()?.ID) return;

                    //Start the madhunt
                    StartInternal(data.RoundSettings, PlayerState.HIDER);
                }));
                ctx.Client.Data.RegisterHandler<DataMadhuntStateUpdate>((con, data) => {
                    if(roundState != null && data.RoundState?.roundID == roundState.settings.RoundID) roundState.othersJoined = true;
                    MainThreadHelper.Do(() => CheckRoundEnd(roundState?.othersJoined ?? false));
                });
                ctx.Client.Data.RegisterHandler<DataPlayerInfo>((con, data) => MainThreadHelper.Do(() => CheckRoundEnd()));
            };
            CelesteNetClientContext.OnDispose += disposeHook = _ => StopRound();
            
            ghostPlayerCollisionHook = new Hook(typeof(Ghost).GetMethod(nameof(Ghost.OnPlayer)), (Action<Action<Ghost, Player>, Ghost, Player>) ((orig, ghost, player) => {
                //Check if we collided with a seeker ghost as a hider
                DataMadhuntStateUpdate ghostState = GetGhostState(ghost.PlayerInfo);
                if(InRound && ghostState?.RoundState?.roundID == roundState.settings.RoundID && State == PlayerState.HIDER && ghostState?.RoundState?.state == PlayerState.SEEKER) {
                    //Check the start timer
                    if(startTimer > 0) return;

                    //Turn the player into a seeker
                    player.Die(ghost.Speed, evenIfInvincible: true).DeathAction = () => {
                        State = PlayerState.SEEKER;
                        CheckRoundEnd(false, ended => { if(!ended) RespawnInArena(); });
                    };
                } else orig(ghost, player);
            }));

            ghostNameRenderHook = new Hook(typeof(GhostNameTag).GetMethod(nameof(GhostNameTag.Render)), (Action<Action<GhostNameTag>, GhostNameTag>) ((orig, nameTag) => {
                if(!InRound || !(nameTag.Tracking is Ghost ghost) || State == GetGhostState(ghost.PlayerInfo)?.RoundState?.state) orig(nameTag);
            }));
        }

        protected override void Dispose(bool disposing) {
            //Remove hooks
            if(levelLoadHook != null) Everest.Events.Level.OnLoadLevel -= levelLoadHook;
            levelLoadHook = null;

            if(exitHook != null) Everest.Events.Level.OnExit -= exitHook;
            exitHook = null;

            if(playerLoadHook != null) On.Celeste.Level.LoadNewPlayer -= playerLoadHook;
            playerLoadHook = null;

            if(initHook != null) CelesteNetClientContext.OnInit -= initHook;
            initHook = null;

            if(disposeHook != null) CelesteNetClientContext.OnDispose -= disposeHook;
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
            
            //Clear update queue
            List<Action> queue = updateQueue;
            updateQueue = prevUpdateQueue;
            prevUpdateQueue = queue;
            foreach(Action act in queue) act();
            queue.Clear();
            
            base.Update(gameTime);
        }

        public bool StartRound(RoundSettings settings, int? startZoneID=null) {
            if(module?.Context == null || InRound) return false;

            //Send start packet
            module.Context.Client.Send<DataMadhuntStart>(new DataMadhuntStart() {
                MajorVersion = Module.Instance.Metadata.Version.Major,
                MinorVersion = Module.Instance.Metadata.Version.Minor,
                StartPlayer = module.Context.Client.PlayerInfo,
                RoundSettings = settings,
                StartZoneID = startZoneID
            });

            StartInternal(settings, PlayerState.SEEKER);
            return true;
        }

        private void StartInternal(RoundSettings settings, PlayerState state) {
            Logger.Log(Module.Name, $"Starting Madhunt {settings.RoundID} in state {state}");
            
            //Create round state and set state
            roundState = new RoundState() { settings = settings, initialSpawn = true, othersJoined = false, isWinner = false };
            State = state;

            //Change the spawnpoint and respawn player
            RespawnInArena();
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

        private void CheckRoundEnd(bool endIfEmpty = true, Action<bool> callback = null) {
            if(!InRound) return;

            //Check for other hider ghosts
            if(!(endIfEmpty || GetGhostStates().Any(state => state.RoundState?.roundID == roundState.settings.RoundID)) || GetGhostStates().Any(state => state.RoundState?.roundID == roundState.settings.RoundID && state.RoundState?.state == PlayerState.HIDER)) {
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
            } else updateQueue.Add(() => RespawnInLobby(state));
        }

        private void RespawnInArena() {
            Session ses = (Celeste.Scene as Level)?.Session;
            if(ses != null) {
                //Change session
                ses.Area = roundState.settings.arenaArea;
                ses.Level = roundState.settings.spawnLevel;
                ses.Inventory.DreamDash = ses.MapData.Data.Mode[(int) ses.Area.Mode].Inventory.DreamDash;
                LevelLoader loader = new LevelLoader(ses);
                arenaLoadLevel = loader.Level;
                Celeste.Scene = loader;
            } else updateQueue.Add(() => RespawnInArena());
        }
        
        private DataMadhuntStateUpdate GetGhostState(DataPlayerInfo info) {
            DataMadhuntStateUpdate ghostState = null;
            if(!(module.Context?.Client?.Data?.TryGetBoundRef<DataPlayerInfo,DataMadhuntStateUpdate>(info.ID, out ghostState) ?? false)) return null;
            return ghostState;
        }
        private IEnumerable<DataMadhuntStateUpdate> GetGhostStates() => module?.Context?.Client?.Data?.GetRefs<DataPlayerInfo>().Where(i => !string.IsNullOrEmpty(i.DisplayName)).Select(i => GetGhostState(i)).Where(s => s != null) ?? Enumerable.Empty<DataMadhuntStateUpdate>();

        public bool InRound => roundState != null;
        public PlayerState? State {
            get => roundState?.playerState;
            private set {
                //Change round state 
                if(value.HasValue) roundState.playerState = value.Value; 
                else roundState = null;

                //Send state update packet
                module?.Context?.Client?.Send<DataMadhuntStateUpdate>(new DataMadhuntStateUpdate() {
                    Player = module?.Context?.Client?.PlayerInfo,
                    RoundState = (roundState != null) ? ((string, PlayerState)?) (roundState.settings.RoundID, roundState.playerState) : null
                });
            }
        }
    }
}