using System;
using System.Linq;

using MonoMod.RuntimeDetour;

namespace Celeste.Mod.Madhunt {
    public class MadhuntModule : EverestModule {
        public const byte PROTOCOL_VERSION = 2;

        public override Type SessionType => typeof(MadhuntSession);
        public static string Name => Instance.Metadata.Name;
        public static MadhuntModule Instance { get; private set; }
        public static MadhuntSession Session => (MadhuntSession) Instance?._Session;
        public MadhuntModule() { Instance = this; }

        private Hook unlockedAreasHook;
        internal TransitionComponent transitionComp;
        internal RoundManager roundManager;

        public override void Load() {
            //Initialize hooks
            MadhuntRound.Init();
            
            unlockedAreasHook = new Hook(typeof(LevelSetStats).GetProperty("UnlockedAreas").GetGetMethod(), (Func<Func<LevelSetStats, int>, LevelSetStats, int>) ((orig, stats) => {
                if(stats.Name == Name) return stats.MaxArea;
                return orig(stats);
            }));

            //Add components
            Celeste.Instance.Components.Add(transitionComp = new TransitionComponent(Celeste.Instance));
            Celeste.Instance.Components.Add(roundManager = new RoundManager(Celeste.Instance));

            //Initialize round verification
            OnVerifyRoundStart += DefaultVerifyer;
        }

        public override void Unload() {
            Celeste.Instance.Components.Remove(transitionComp);
            transitionComp.Dispose();

            Celeste.Instance.Components.Remove(roundManager);
            roundManager.Dispose();

            unlockedAreasHook.Dispose();
            MadhuntRound.Uninit();
        }

        public bool VerifyRoundStart(DataMadhuntRoundStart startPacket) {
            bool isValid = false;
            OnVerifyRoundStart?.Invoke(startPacket, ref isValid);
            return isValid;
        }

        private void DefaultVerifyer(DataMadhuntRoundStart startPacket, ref bool isValid) {
            //Check if we're in the lobby room
            Level lvl = Celeste.Scene as Level;
            Session ses = lvl?.Session;
            if(ses == null || ses.Area != startPacket.RoundSettings.lobbyArea || ses.Level != startPacket.RoundSettings.lobbyLevel) return;

            //Check if the zone ID matches
            if(startPacket.StartZoneID.HasValue && !(Celeste.Scene.Tracker.GetEntity<Player>()?.CollideAll<StartZone>().Cast<StartZone>().Any(sz => startPacket.StartZoneID == sz.ID) ?? false)) return;

            //Check if there's an arena option which produces these settings
            foreach(ArenaOption option in lvl.Tracker.GetEntities<ArenaOption>()) {
                if(option.Settings.RoundID == startPacket.RoundSettings.RoundID) {
                    //The start packet is valid
                    isValid |= true;
                    return;
                }
            }
        }

        public static bool StartRound(RoundSettings settings, int? startZoneID) => Instance.roundManager.StartRound(settings, startZoneID);
        public static void EndRound(PlayerRole? winnerRole = null) => Instance.roundManager.EndRound(winnerRole);
        public static bool IsRoundActive(string id) => Instance.roundManager.IsRoundActive(id);
        public static MadhuntRound CurrentRound => Instance.roundManager.CurrentRound;

        public delegate void RoundVerifyer(DataMadhuntRoundStart startPacket, ref bool isValid);
        public event RoundVerifyer OnVerifyRoundStart;
    }
}