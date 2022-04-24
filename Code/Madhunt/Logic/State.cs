using Microsoft.Xna.Framework;

namespace Celeste.Mod.Madhunt {
    public struct RoundSettings {
        public AreaKey lobbyArea;
        public string lobbyLevel;
        public Vector2 lobbySpawnPoint;

        public AreaKey arenaArea;
        public string spawnLevel;
        public byte spawnIndex;

        public int initialSeekers;
        public bool tagMode, goldenMode;
        public bool hideNames;

        public string RoundID => $"{arenaArea.SID}#{arenaArea.Mode}#{spawnLevel}#{MadhuntModule.Instance.Metadata.Version.Major}.{MadhuntModule.Instance.Metadata.Version.Minor}";
    }

    public enum PlayerRole {
        SEEDWAIT, HIDER, SEEKER
    }

    public struct PlayerState {
        public string roundID;
        public int seed;
        public PlayerRole role;
    }
}