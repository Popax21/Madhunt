using System;
using System.Linq;
using System.Reflection;
using System.Collections.Concurrent;

using Microsoft.Xna.Framework;
using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.CelesteNet.DataTypes;

namespace Celeste.Mod.Madhunt {
    public class RoundManager : GameComponent {
        public const float START_DELAY = 1f, START_TIME_SLOWDOWN_FACTOR = 4f;

        private CelesteNetClientModule netModule;
        private Delegate netInitHook, netDisposeHook;

        private ConcurrentQueue<Action> updateQueue = new ConcurrentQueue<Action>();

        private MadhuntRound curRound;
        private float startTimer;

        public RoundManager(Game game) : base(game) {
            //Install CelesteNet context hooks
            if((netModule = (CelesteNetClientModule) Everest.Modules.FirstOrDefault(m => m is CelesteNetClientModule)) == null) throw new Exception("CelesteNet not loaded???");

            EventInfo initEvt = typeof(CelesteNetClientContext).GetEvent("OnInit");
            if(initEvt.EventHandlerType.GenericTypeArguments[0] == typeof(CelesteNetClientContext)) {
                initEvt.AddEventHandler(null, netInitHook = (Action<CelesteNetClientContext>) (_ => NetClientInit(netModule.Context.Client)));
            } else {
                initEvt.AddEventHandler(null, netInitHook = (Action<object>) (_ => NetClientInit(netModule.Context.Client)));
            }

            EventInfo disposeEvt = typeof(CelesteNetClientContext).GetEvent("OnDispose");
            if(disposeEvt.EventHandlerType.GenericTypeArguments[0] == typeof(CelesteNetClientContext)) {
                disposeEvt.AddEventHandler(null, netDisposeHook = (Action<CelesteNetClientContext>) (_ => NetClientDisposed()));
            } else {
                disposeEvt.AddEventHandler(null, netDisposeHook = (Action<object>) (_ => NetClientDisposed()));
            }
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            //Remove hooks
            if(netDisposeHook != null) typeof(CelesteNetClientContext).GetEvent("OnDispose").RemoveEventHandler(null, netDisposeHook);
            netDisposeHook = null;
        }

        private void NetClientInit(CelesteNetClient client) {
            //Register handlers
            client.Data.RegisterHandlersIn(this);
        }

        private void NetClientDisposed() {
            //Stop current round (if we have one)
            curRound?.Stop();
        }

        public override void Update(GameTime gameTime) {
            //Update the invincibility timer
            curRound?.UpdateInvincibility((float) gameTime.ElapsedGameTime.TotalSeconds);
            
            //Update the start delay timer
            if(curRound?.InSeedWait ?? false) {
                startTimer += Celeste.RawDeltaTime;
                Celeste.TimeRate = (float) Math.Exp(-START_TIME_SLOWDOWN_FACTOR * startTimer);

                if(startTimer > START_DELAY) {
                    Celeste.TimeRate = 1f;
                    if(!curRound.EndSeedWait()) {
                        Logger.Log(MadhuntModule.Name, $"Couldn't end seed wait successfully for Madhunt round {curRound.Settings.RoundID}");
                        curRound = null;
                        Celeste.Scene?.Tracker?.GetEntity<Player>()?.Die(Vector2.Zero, true, false);
                    }
                }
            }

            //Flush update queue
            ConcurrentQueue<Action> queue = updateQueue;
            updateQueue = new ConcurrentQueue<Action>();
            foreach(Action act in queue) act();
            
            base.Update(gameTime);
        }

        public bool StartRound(RoundSettings settings, int? startZoneID) {
            //Get the CelesteNet client
            CelesteNetClient client = netModule.Context?.Client;
            if(client == null) return false;

            //Send start packet
            client.SendAndHandle(new DataMadhuntRoundStart() {
                MajorVersion = MadhuntModule.Instance.Metadata.Version.Major,
                MinorVersion = MadhuntModule.Instance.Metadata.Version.Minor,
                StartPlayer = client.PlayerInfo,
                RoundSettings = settings,
                StartZoneID = startZoneID
            });

            return true;
        }
    
        public void EndRound(PlayerRole? winnerRole) {
            if(curRound == null) return;
            curRound.Stop(curRound.PlayerRole == winnerRole);
        }

        public bool IsRoundActive(string id) =>
            netModule.Context?.Client?.Data?.GetRefs<DataPlayerInfo>()
            ?.Select(i => netModule.Context.Client.Data.TryGetBoundRef<DataPlayerInfo, DataMadhuntStateUpdate>(i, out var state) ? state : null)
            ?.Where(s => s?.State?.roundID == id)
            ?.Any()
        ?? false;

        internal void CheckRoundEnd() {
            if(curRound?.CheckRoundEnd() ?? false) curRound = null;
        }

        public void Handle(CelesteNetConnection con, DataMadhuntRoundStart data) {
            //Check if the version is compatible
            if(data.MajorVersion != MadhuntModule.Instance.Metadata.Version.Major || data.MinorVersion != MadhuntModule.Instance.Metadata.Version.Minor) {
                Logger.Log(LogLevel.Info, MadhuntModule.Name, $"Ignoring Madhunt round start packet with incompatible version {data.MajorVersion}.{data.MinorVersion} vs installed {MadhuntModule.Instance.Metadata.Version}");
                return;
            }

            updateQueue.Enqueue(() => {
                if(curRound != null) return;

                CelesteNetClient client = netModule.Context?.Client;
                if(client == null) return;

                //Check if we should start
                //Do this by asking the verifiers if the packet is accepted
                //This prevents attackers from spamming round start packets
                if(!MadhuntModule.Instance.VerifyRoundStart(data)) {
                    Logger.Log(LogLevel.Info, MadhuntModule.Name, $"Ignoring Madhunt round start packet because the verifiers didn't accept it");
                    return;
                }

                //Start the round
                curRound = new MadhuntRound(client, data.RoundSettings);
                startTimer = 0;
            });
        }
        
        public void Handle(CelesteNetConnection con, DataMadhuntRoundEnd data) {
            updateQueue.Enqueue(() => {
                if(curRound == null || curRound.Settings.RoundID != data.RoundID) return;

                //Stop the round
                curRound.Stop(curRound.PlayerRole == data.WinningRole);
            });
        }

        public void Handle(CelesteNetConnection con, DataMadhuntStateUpdate data) {
            updateQueue.Enqueue(() => CheckRoundEnd());
        }
        
        public void Handle(CelesteNetConnection con, DataPlayerInfo data) {
            updateQueue.Enqueue(() => CheckRoundEnd());
        }

        public MadhuntRound CurrentRound => curRound;
    }
}