using System;

using Microsoft.Xna.Framework;
using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;

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

        public string RoundID => $"{arenaArea.SID}#{arenaArea.Mode}#{spawnLevel}#{Module.Instance.Metadata.Version.Major}.{Module.Instance.Metadata.Version.Minor}";
    }

    public enum PlayerState {
        SEEDWAIT, HIDER, SEEKER
    }
    
    public class DataMadhuntRoundStart : DataType<DataMadhuntRoundStart> {
        static DataMadhuntRoundStart() => DataID = $"madhuntRoundStartV{Module.PROTOCOL_VERSION}";
        
        public int MajorVersion, MinorVersion;
        public DataPlayerInfo StartPlayer;
        public RoundSettings RoundSettings;
        public int? StartZoneID;

        public override MetaType[] GenerateMeta(DataContext ctx) => new MetaType[] { new MetaPlayerUpdate(StartPlayer) };
        public override void FixupMeta(DataContext ctx) => StartPlayer = Get<MetaPlayerUpdate>(ctx).Player;

        protected override void Read(CelesteNetBinaryReader reader) {
            MajorVersion = reader.ReadInt32();
            MinorVersion = reader.ReadInt32();

            RoundSettings.lobbyArea.SID = reader.ReadNetString();
            RoundSettings.lobbyArea.Mode = (AreaMode) reader.ReadByte();
            RoundSettings.lobbyLevel = reader.ReadNetString();
            RoundSettings.lobbySpawnPoint = reader.ReadVector2();

            RoundSettings.arenaArea.SID = reader.ReadNetString();
            RoundSettings.arenaArea.Mode = (AreaMode) reader.ReadByte();
            RoundSettings.spawnLevel = reader.ReadNetString();
            RoundSettings.spawnIndex = reader.ReadByte();

            RoundSettings.initialSeekers = reader.ReadInt32();
            RoundSettings.tagMode = reader.ReadBoolean();
            RoundSettings.goldenMode = reader.ReadBoolean();
            RoundSettings.hideNames = reader.ReadBoolean();

            StartZoneID = reader.ReadBoolean() ? (int?) reader.ReadInt32() : null;
        }

        protected override void Write(CelesteNetBinaryWriter writer) {
            writer.Write(MajorVersion);
            writer.Write(MinorVersion);
            
            writer.WriteNetString(RoundSettings.lobbyArea.SID);
            writer.Write((byte) RoundSettings.lobbyArea.Mode);
            writer.WriteNetString(RoundSettings.lobbyLevel);
            writer.Write(RoundSettings.lobbySpawnPoint);

            writer.WriteNetString(RoundSettings.arenaArea.SID);
            writer.Write((byte) RoundSettings.arenaArea.Mode);
            writer.WriteNetString(RoundSettings.spawnLevel);
            writer.Write(RoundSettings.spawnIndex);

            writer.Write(RoundSettings.initialSeekers);
            writer.Write(RoundSettings.tagMode);
            writer.Write(RoundSettings.goldenMode);
            writer.Write(RoundSettings.hideNames);

            writer.Write(StartZoneID.HasValue);
            if(StartZoneID.HasValue) writer.Write(StartZoneID.Value);
        }
    }

    public class DataMadhuntRoundEnd : DataType<DataMadhuntRoundEnd> {
        static DataMadhuntRoundEnd() => DataID = $"madhuntRoundEndV{Module.PROTOCOL_VERSION}";

        public DataPlayerInfo EndPlayer;
        public string RoundID;
        public PlayerState WinningState;

        public override MetaType[] GenerateMeta(DataContext ctx) => new MetaType[] { new MetaPlayerUpdate(EndPlayer) };
        public override void FixupMeta(DataContext ctx) => EndPlayer = Get<MetaPlayerUpdate>(ctx).Player;

        protected override void Read(CelesteNetBinaryReader reader) {
            RoundID = reader.ReadNetString();
            WinningState = (PlayerState) reader.ReadByte();
        }
        
        protected override void Write(CelesteNetBinaryWriter writer) {
            writer.WriteNetString(RoundID);
            writer.Write((byte) WinningState);
        }
    }

    public class DataMadhuntStateUpdate : DataType<DataMadhuntStateUpdate> {
        public struct RoundState {
            public string roundID;
            public int seed;
            public PlayerState state;
        }

        static DataMadhuntStateUpdate() => DataID = $"madhuntStateUpdateV{Module.PROTOCOL_VERSION}";

        public DataPlayerInfo Player;
        public RoundState? State;

        public override MetaType[] GenerateMeta(DataContext ctx) => new MetaType[] { new MetaPlayerPublicState(Player), new MetaBoundRef(DataType<DataPlayerInfo>.DataID, Player?.ID ?? uint.MaxValue, true) };

        public override void FixupMeta(DataContext ctx) {
            Player = Get<MetaPlayerPublicState>(ctx);
            Get<MetaBoundRef>(ctx).ID = Player?.ID ?? uint.MaxValue;
        }

        protected override void Read(CelesteNetBinaryReader reader) {
            if(reader.ReadBoolean()) {
                State = new RoundState() {
                    roundID = reader.ReadNetString(), 
                    seed = reader.ReadInt32(),
                    state = (PlayerState) reader.ReadByte()
                };
            } else State = null;
        }

        protected override void Write(CelesteNetBinaryWriter writer) {
            writer.Write(State != null);
            if(State != null) {
                writer.WriteNetString(State.Value.roundID);
                writer.Write(State.Value.seed);
                writer.Write((byte) State.Value.state);
            }
        }
    }
}