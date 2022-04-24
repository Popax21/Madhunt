using System;
using Monocle;

namespace Celeste.Mod.Madhunt {
    public static class DebugCommands {
        [Command("mh_state", "Dumps Madhunt's internal state")]
        public static void DumpState() {
            Celeste.Commands.Log($"mod version: {MadhuntModule.Instance.Metadata.Version.Major}.{MadhuntModule.Instance.Metadata.Version.Minor}");
            Celeste.Commands.Log($"protocol version: v{MadhuntModule.PROTOCOL_VERSION}");

            //Dump round state
            Celeste.Commands.Log("round:");
            if(MadhuntModule.CurrentRound != null) {
                MadhuntRound round = MadhuntModule.CurrentRound;
                Celeste.Commands.Log($"  ID: {round.Settings.RoundID}");
                Celeste.Commands.Log($"  seed: {round.PlayerSeed}");
                Celeste.Commands.Log($"  role: {round.PlayerRole}");
                Celeste.Commands.Log($"  is winner: {round.isWinner}");
                Celeste.Commands.Log($"  skip end check: {round.skipEndCheck}");
            } else Celeste.Commands.Log("  no round");

            //Dump session state
            Celeste.Commands.Log("session:");
            if(MadhuntModule.Session != null) {
                MadhuntSession ses = MadhuntModule.Session;
                Celeste.Commands.Log($"  won last round: {ses.WonLastRound}");
            } else Celeste.Commands.Log("  no session");
        }

        [Command("mh_endround", "End the current Madhunt round | 0 - no winner, 1 - hiders win, 2 - seekers win ")]
        public static void EndRound(int winner) {
            if(MadhuntModule.CurrentRound == null) {
                Celeste.Commands.Log("not in a round!");
                return;
            }
            MadhuntModule.EndRound(winner switch {
                0 => null,
                1 => PlayerRole.HIDER,
                2 => PlayerRole.SEEKER,
                _ => throw new ArgumentException($"Invalid winner role {winner}!")
            });
        }
    }
}