using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.RuntimeDetour;
using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.CelesteNet.Client.Entities;
using Celeste.Mod.CelesteNet.DataTypes;

namespace Celeste.Mod.Madhunt {
    //TODO Rewrite this to be modular and not a big blob of logic
    public class Manager : GameComponent {
        public const float START_DELAY = 1f, START_TIME_SLOWDOWN_FACTOR = 4f;
        public const float TRANSITION_INVINCIBILITY = 0.25f, RESPAWN_INVINCIBILITY = 1f;
        private static Random RANDOM = new Random();

        private class RoundState {
            public RoundSettings settings;
            public int playerSeed;
            public PlayerState playerState;
            public bool initialSpawn, skipEndCheck, isWinner;

            public int? oldDashes;
            public HashSet<string> oldFlags, oldLevelFlags;
            public HashSet<EntityID> oldDoNotLoad, oldKeys;
        }

        private CelesteNetClientModule module;
        private Delegate initHook, disposeHook;
        private Hook ghostPlayerCollisionHook, ghostNameRenderHook;

        private RoundState roundState = null;
        private Level arenaLoadLevel = null;
        private float startDelayTimer = 0f, invincTimer = 0f;
        private ConcurrentQueue<Action> updateQueue = new ConcurrentQueue<Action>();

        public Manager(Game game) : base(game) {
            //Get the CelesteNET module
            if((module = (CelesteNetClientModule) Everest.Modules.FirstOrDefault(m => m is CelesteNetClientModule)) == null) throw new Exception("CelesteNET not loaded!");

            //Install Celeste hooks
            Everest.Events.Level.OnLoadLevel += LevelLoadHook;
            Everest.Events.Level.OnExit += ExitHook;
            On.Celeste.Level.LoadNewPlayer += PlayerLoadHook;
            On.Celeste.Level.Reload += ReloadHook;
            On.Celeste.Level.EnforceBounds += EnforceBoundsHook;
            On.Celeste.Player.Die += DieHook;
            On.Celeste.Holdable.Pickup += PickupHook;

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
            
            //Install CelesteNet hooks
            ghostPlayerCollisionHook = new Hook(typeof(Ghost).GetMethod(nameof(Ghost.OnPlayer)), GetType().GetMethod(nameof(GhostPlayerCollisionHook), BindingFlags.NonPublic | BindingFlags.Instance), this);
            ghostNameRenderHook = new Hook(typeof(GhostNameTag).GetMethod(nameof(GhostNameTag.Render)), GetType().GetMethod(nameof(GhostNameTagRenderHook), BindingFlags.NonPublic | BindingFlags.Instance), this);
        }

        protected override void Dispose(bool disposing) {
            //Remove hooks
            Everest.Events.Level.OnLoadLevel -= LevelLoadHook;
            Everest.Events.Level.OnExit -= ExitHook;
            On.Celeste.Level.LoadNewPlayer -= PlayerLoadHook;
            On.Celeste.Level.Reload -= ReloadHook;
            On.Celeste.Level.EnforceBounds -= EnforceBoundsHook;
            On.Celeste.Player.Die -= DieHook;
            On.Celeste.Holdable.Pickup -= PickupHook;

            if(initHook != null) typeof(CelesteNetClientContext).GetEvent("OnInit").RemoveEventHandler(null, initHook);
            initHook = null;

            if(disposeHook != null) typeof(CelesteNetClientContext).GetEvent("OnDispose").RemoveEventHandler(null, disposeHook);
            disposeHook = null;

            ghostPlayerCollisionHook.Dispose();
            ghostNameRenderHook.Dispose();
            ghostPlayerCollisionHook = ghostNameRenderHook = null;

            base.Dispose(disposing);
        }

        public override void Update(GameTime gameTime) {
            //Update the invincibility timer
            if(invincTimer > 0) invincTimer -= (float) gameTime.ElapsedGameTime.TotalSeconds;
            if((Celeste.Scene as Level)?.Transitioning ?? false && invincTimer < TRANSITION_INVINCIBILITY) invincTimer = TRANSITION_INVINCIBILITY;
            
            //Update the start delay timer
            if(roundState != null && !InRound) {
                startDelayTimer += Engine.RawDeltaTime;
                Engine.TimeRate = (float) Math.Exp(-START_TIME_SLOWDOWN_FACTOR * startDelayTimer);

                if(startDelayTimer > START_DELAY) {
                    Engine.TimeRate = 1f;
                    if(!EndSeedWait()) {
                        Logger.Log(Module.Name, $"Couldn't end seed wait successfully for Madhunt {roundState.settings.RoundID}");
                        roundState = null;
                        Engine.Scene?.Tracker?.GetEntity<Player>()?.Die(Vector2.Zero, true, false);
                    }
                }
            }

            //Clear update queue
            ConcurrentQueue<Action> queue = updateQueue;
            updateQueue = new ConcurrentQueue<Action>();
            foreach(Action act in queue) act();
            
            base.Update(gameTime);
        }

        public bool SomeoneInRound(string roundID) => GetGhostStates().Any(s => s.State != null && s.State.Value.roundID == roundID);

        public bool StartRound(RoundSettings settings, int? startZoneID=null) {
            if(module?.Context == null || !module.Client.IsReady || roundState != null) return false;

            //Send start packet
            module.Context.Client.Send(new DataMadhuntRoundStart() {
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

            //Determine seed
            //Query the random number generator a random number of times, because it outputs total BS sometimes
            //Also create a new random number generator sometimes
            if(Environment.TickCount % 21 <= 3) RANDOM = new Random();
            int numIter = Environment.TickCount % 16;
            int seed;
            do { seed = RANDOM.Next(int.MinValue, int.MaxValue); } while(numIter-- > 0);

            Logger.Log(Module.Name, $"Starting Madhunt {settings.RoundID} with seed {seed}");
            
            //Create round state
            roundState = new RoundState() { settings = settings, playerSeed = seed, initialSpawn = true, skipEndCheck = true, isWinner = false };
            State = PlayerState.SEEDWAIT;
            startDelayTimer = 0;
        }

        public void StopRound() {
            if(!InRound) return;
            Logger.Log(Module.Name, $"Stopping Madhunt {roundState.settings.RoundID} in state {roundState.playerState}{(roundState.isWinner ? " as winner" : string.Empty)}");

            //Set state
            RoundState state = roundState;
            State = null;

            //Respawn the player in the lobby
            RespawnInLobby(true, state);
        }

        private bool EndSeedWait() {
            if(roundState == null || State != PlayerState.SEEDWAIT) return false;

            //Check if anyone else is in the same round
            if(!GetGhostStates().Any(ghostState => ghostState.State != null && ghostState.State.Value.roundID == roundState.settings.RoundID)) return false;

            //Determine and set the player state
            PlayerState state;
            if(roundState.settings.initialSeekers > 0) {
                state = (GetGhostStates().Count(ghostState => {
                    if(ghostState.State == null || ghostState.State.Value.roundID != roundState.settings.RoundID) return false;
                    int ghostSeed = ghostState.State.Value.seed;
                    return ghostSeed > roundState.playerSeed || (ghostSeed == roundState.playerSeed && ghostState.Player.ID > (module?.Context?.Client?.PlayerInfo?.ID ?? 0));
                }) < roundState.settings.initialSeekers) ? PlayerState.SEEKER : PlayerState.HIDER;
            } else {
                state = (GetGhostStates().Count(ghostState => {
                    if(ghostState.State == null || ghostState.State.Value.roundID != roundState.settings.RoundID) return false;
                    int ghostSeed = ghostState.State.Value.seed;
                    return ghostSeed > roundState.playerSeed || (ghostSeed == roundState.playerSeed && ghostState.Player.ID > (module?.Context?.Client?.PlayerInfo?.ID ?? 0));
                }) < -roundState.settings.initialSeekers) ? PlayerState.HIDER : PlayerState.SEEKER;
            }

            Logger.Log(Module.Name, $"Ended seed wait, determined state {state} for Madhunt {roundState.settings.RoundID}");
            State = state;
            if(CheckRoundEnd()) return false;

            //Change the spawnpoint and respawn player
            RespawnInArena(true);
            return true;
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

        private bool CheckRoundEnd() {
            if(!InRound) return true;

            //Check if there are both hiders and seekers left
            //Skip this check if the round just started
            if(
                (State == PlayerState.HIDER || GetGhostStates().Any(state => state.State?.roundID == roundState.settings.RoundID && state.State?.state == PlayerState.HIDER)) &&
                (State == PlayerState.SEEKER || GetGhostStates().Any(state => state.State?.roundID == roundState.settings.RoundID && state.State?.state == PlayerState.SEEKER))
            ) {
                //Check if we're the only remaining hider
                roundState.isWinner = State == PlayerState.HIDER && !GetGhostStates().Any(state => state.State?.roundID == roundState.settings.RoundID && state.State?.state == PlayerState.HIDER);

                roundState.skipEndCheck = false;
                return false;
            }
            if(roundState.skipEndCheck) return false;

            //End the round
            StopRound();
            return true;
        }

        private void UpdateFlags(Session ses) {
            ses.SetFlag("Madhunt_InRound", InRound && State != PlayerState.SEEDWAIT);
            ses.SetFlag("Madhunt_IsHider", InRound && State == PlayerState.HIDER);
            ses.SetFlag("Madhunt_IsSeeker", InRound && State == PlayerState.SEEKER);
        }

        private void RespawnInLobby(bool doReset, RoundState state=null) {
            state ??= roundState;

            Session ses = (Celeste.Scene as Level)?.Session;
            if(ses != null) {
                //Change session
                ses.Area = state.settings.lobbyArea;
                ses.Level = state.settings.lobbyLevel;
                ses.RespawnPoint = state.settings.lobbySpawnPoint;
                ses.Inventory.DreamDash = state.isWinner;

                if(doReset) {
                    ses.Dashes = state.oldDashes ?? ses.Dashes;
                    ses.Flags = state.oldFlags ?? ses.Flags;
                    ses.LevelFlags = state.oldLevelFlags ?? ses.LevelFlags;
                    ses.DoNotLoad = state.oldDoNotLoad ?? ses.DoNotLoad;
                    ses.Keys = state.oldKeys ?? ses.Keys;
                    if(Celeste.Scene.Tracker.GetEntity<Player>() is Player player) player.Leader.LoseFollowers();
                }
                UpdateFlags(ses);

                if(Celeste.Scene is Level level) level.Wipe?.Cancel();
                Celeste.Scene = new LevelLoader(ses, ses.RespawnPoint);
            } else updateQueue.Enqueue(() => RespawnInLobby(doReset, state));
        }

        private void RespawnInArena(bool doReset) {
            Session ses = (Celeste.Scene as Level)?.Session;
            if(ses != null) {
                //Change session
                ses.Area = roundState.settings.arenaArea;
                ses.Level = roundState.settings.spawnLevel;
                ses.Inventory.DreamDash = true;

                if(doReset) {
                    roundState.oldDashes = ses.Dashes;
                    roundState.oldFlags = new HashSet<string>(ses.Flags);
                    roundState.oldLevelFlags = new HashSet<string>(ses.LevelFlags);
                    roundState.oldDoNotLoad = new HashSet<EntityID>(ses.DoNotLoad);
                    roundState.oldKeys = new HashSet<EntityID>(ses.Keys);
                    ses.Keys.Clear();
                    if(Celeste.Scene.Tracker.GetEntity<Player>() is Player player) player.Leader.LoseFollowers();
                }
                UpdateFlags(ses);

                if(Celeste.Scene is Level level) level.Wipe?.Cancel();
                LevelLoader loader = new LevelLoader(ses);
                arenaLoadLevel = loader.Level;
                Celeste.Scene = loader;
            } else updateQueue.Enqueue(() => RespawnInArena(doReset));
        }

        private DataMadhuntStateUpdate GetGhostState(DataPlayerInfo info) {
            DataMadhuntStateUpdate ghostState = null;
            if(!(module.Context?.Client?.Data?.TryGetBoundRef<DataPlayerInfo,DataMadhuntStateUpdate>(info.ID, out ghostState) ?? false)) return null;
            return ghostState;
        }
        private IEnumerable<DataMadhuntStateUpdate> GetGhostStates() => module?.Context?.Client?.Data?.GetRefs<DataPlayerInfo>().Where(i => !string.IsNullOrEmpty(i.DisplayName)).Select(i => GetGhostState(i)).Where(s => s != null && s.Player != module?.Context?.Client?.PlayerInfo) ?? Enumerable.Empty<DataMadhuntStateUpdate>();

        private void LevelLoadHook(Level lvl, Player.IntroTypes intro, bool fromLoader) {
            //Disable save and quit when in a round
            if(InRound) lvl.SaveQuitDisabled = true;
        }

        private void ExitHook(Level lvl, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            //Exit the round (if we're in one)
            if(InRound) State = null;
        }

        private Player PlayerLoadHook(On.Celeste.Level.orig_LoadNewPlayer orig, Vector2 pos, PlayerSpriteMode smode) {
            try {
                Level arenaLevel = arenaLoadLevel;
                arenaLoadLevel = null;

                //Set the invincibility timer
                if(invincTimer < RESPAWN_INVINCIBILITY) invincTimer = RESPAWN_INVINCIBILITY;

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

        private void ReloadHook(On.Celeste.Level.orig_Reload orig, Level level) {
            orig(level);
            if(!InRound) return;

            //Set the invincibility timer
            if(invincTimer < RESPAWN_INVINCIBILITY) invincTimer = RESPAWN_INVINCIBILITY;
        }

        private void EnforceBoundsHook(On.Celeste.Level.orig_EnforceBounds orig, Level level, Player player) {
            orig(level, player);
            if(level.Transitioning || !InRound) return;

            //Stop the player from going over the top of the screen (to prevent cheeky hiding spots)    
            //Still allow for normal transitions though        
            if(player.Top < level.Bounds.Top && !level.Session.MapData.CanTransitionTo(level, player.TopCenter - Vector2.UnitY*12f)) {
                player.Top = level.Bounds.Top;
                player.OnBoundsV();
            }
        }

        private PlayerDeadBody DieHook(On.Celeste.Player.orig_Die orig, Player player, Vector2 dir, bool evenIfInvincible, bool registerDeath) {
            PlayerDeadBody body = orig(player, dir, evenIfInvincible, registerDeath);

            //If a hider dies while in a golden mode round, make them a seeker
            if(body != null && InRound && roundState.settings.goldenMode && State == PlayerState.HIDER) {
                Action oldDeathAct = body.DeathAction;
                body.DeathAction = () => {
                    oldDeathAct?.Invoke();

                    //Turn the player into a seeker
                    if(!InRound) return;
                    State = PlayerState.SEEKER;
                    if(!CheckRoundEnd()) RespawnInArena(false);
                };
            }
            return body;
        }

        private bool PickupHook(On.Celeste.Holdable.orig_Pickup orig, Holdable holdable, Player player) {
            //Does the holdable belong to a ghost?
            if(!InRound || !roundState.settings.tagMode || !(holdable.Entity is Ghost ghost)) return orig(holdable, player);

            //Is the ghost in the same round and not in the same state?
            DataMadhuntStateUpdate ghostState = GetGhostState(ghost.PlayerInfo);
            if(ghostState?.State == null || ghostState.State.Value.roundID != roundState.settings.RoundID || ghostState.State.Value.state == State) return orig(holdable, player);

            //Don't allow grabbing between players in different states
            return false;
        }

        private void GhostPlayerCollisionHook(Action<Ghost, Player> orig, Ghost ghost, Player player) {
            //Check if we collided with a seeker ghost as a hider
            DataMadhuntStateUpdate ghostState = GetGhostState(ghost.PlayerInfo);
            if(InRound && roundState.settings.tagMode && ghostState?.State?.roundID == roundState.settings.RoundID) {
                if(State == PlayerState.HIDER && ghostState?.State?.state == PlayerState.SEEKER) {
                    //Check for invincibiliy
                    if(invincTimer > 0 || ((Celeste.Scene as Level)?.Transitioning ?? false)) return;

                    //Turn the player into a seeker
                    player.Die(Vector2.Zero, evenIfInvincible: true).DeathAction = () => {
                        if(roundState == null) return;
                        State = PlayerState.SEEKER;
                        if(!CheckRoundEnd()) RespawnInArena(false);
                    };
                } else if(State == ghostState?.State?.state) {
                    //Only handle the collision if the ghost has the same role
                    //This prevents bouncing of seekers/hiders when interactions are enabled
                    orig(ghost, player);
                }
            } else orig(ghost, player);
        }

        private void GhostNameTagRenderHook(Action<GhostNameTag> orig, GhostNameTag nameTag) {
            //Don't render name tags of other roles (if disabled in the settings)
            if(InRound && roundState.settings.hideNames && 
                nameTag.Tracking is Ghost ghost && GetGhostState(ghost.PlayerInfo)?.State is var ghostState &&
                ghostState?.roundID == roundState.settings.RoundID && ghostState?.state != State
            ) return;

            orig(nameTag);
        }

        public void Handle(CelesteNetConnection con, DataMadhuntRoundStart data) {
            //Check if the version is compatible
            if(data.MajorVersion != Module.Instance.Metadata.Version.Major || data.MinorVersion != Module.Instance.Metadata.Version.Minor) {
                Logger.Log(LogLevel.Info, Module.Name, $"Ignoring Madhunt round start packet with incompatible version {data.MajorVersion}.{data.MinorVersion} vs installed {Module.Instance.Metadata.Version}");
                return;
            }

            updateQueue.Enqueue(() => {
                //Check if we should start
                //Do this by asking the verifiers if the packet is accepted
                //This prevents attackers from spamming round start packets
                if(roundState != null) return;

                bool validSettings = false;
                OnVerifyRoundStart?.Invoke(data, ref validSettings);
                if(!validSettings) {
                    Logger.Log(LogLevel.Info, Module.Name, $"Ignoring Madhunt round start packet because the verifiers didn't accept it");
                    return;
                }

                //Start the round
                updateQueue.Enqueue(() => StartInternal(data.RoundSettings));
            });
        }
        
        public void Handle(CelesteNetConnection con, DataMadhuntRoundEnd data) {
            updateQueue.Enqueue(() => {
                if(roundState == null || data.RoundID != roundState.settings.RoundID) return;

                //Stop the round
                roundState.isWinner = (State == data.WinningState);
                StopRound();
            });
        }

        public void Handle(CelesteNetConnection con, DataMadhuntStateUpdate data) {
            updateQueue.Enqueue(() => {
                //Check if the round ended
                CheckRoundEnd();
            });
        }
        
        public void Handle(CelesteNetConnection con, DataPlayerInfo data) {
            updateQueue.Enqueue(() => {
               //Check if the round ended
                CheckRoundEnd();
            });
        }

        public bool InRound => roundState != null && roundState.playerState != PlayerState.SEEDWAIT;
        public PlayerState? State {
            get => roundState?.playerState;
            private set {
                //Change round state 
                if(value.HasValue) roundState.playerState = value.Value; 
                else roundState = null;
                if(Celeste.Scene is Level lvl) UpdateFlags(lvl.Session);

                //Send state update packet
                module?.Context?.Client?.Send<DataMadhuntStateUpdate>(new DataMadhuntStateUpdate() {
                    Player = module?.Context?.Client?.PlayerInfo,
                    State = (roundState != null) ? new DataMadhuntStateUpdate.RoundState() {
                        roundID = roundState.settings.RoundID,
                        seed = roundState.playerSeed,
                        state = roundState.playerState
                    } : (DataMadhuntStateUpdate.RoundState?) null
                });
            }
        }

        public delegate void RoundVerifyer(DataMadhuntRoundStart startData, ref bool isValid);
        public event RoundVerifyer OnVerifyRoundStart;
    }
}