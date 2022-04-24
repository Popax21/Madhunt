using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;

namespace Celeste.Mod.Madhunt {
    public class DataMadhuntRoundStart : DataType<DataMadhuntRoundStart> {
        static DataMadhuntRoundStart() => DataID = $"madhuntRoundStartV{MadhuntModule.PROTOCOL_VERSION}";
        
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
        static DataMadhuntRoundEnd() => DataID = $"madhuntRoundEndV{MadhuntModule.PROTOCOL_VERSION}";

        public DataPlayerInfo EndPlayer;
        public string RoundID;
        public PlayerRole? WinningRole;

        public override MetaType[] GenerateMeta(DataContext ctx) => new MetaType[] { new MetaPlayerUpdate(EndPlayer) };
        public override void FixupMeta(DataContext ctx) => EndPlayer = Get<MetaPlayerUpdate>(ctx).Player;

        protected override void Read(CelesteNetBinaryReader reader) {
            RoundID = reader.ReadNetString();
            if(reader.ReadBoolean()) WinningRole = (PlayerRole) reader.ReadByte();
        }
        
        protected override void Write(CelesteNetBinaryWriter writer) {
            writer.WriteNetString(RoundID);
            writer.Write(WinningRole.HasValue);
            if(WinningRole.HasValue) writer.Write((byte) WinningRole);
        }
    }

    public class DataMadhuntStateUpdate : DataType<DataMadhuntStateUpdate> {
        static DataMadhuntStateUpdate() => DataID = $"madhuntStateUpdateV{MadhuntModule.PROTOCOL_VERSION}";

        public DataPlayerInfo Player;
        public PlayerState? State;

        public override MetaType[] GenerateMeta(DataContext ctx) => new MetaType[] { new MetaPlayerPublicState(Player), new MetaBoundRef(DataType<DataPlayerInfo>.DataID, Player?.ID ?? uint.MaxValue, true) };

        public override void FixupMeta(DataContext ctx) {
            Player = Get<MetaPlayerPublicState>(ctx);
            Get<MetaBoundRef>(ctx).ID = Player?.ID ?? uint.MaxValue;
        }

        protected override void Read(CelesteNetBinaryReader reader) {
            if(reader.ReadBoolean()) {
                State = new PlayerState() {
                    roundID = reader.ReadNetString(), 
                    seed = reader.ReadInt32(),
                    role = (PlayerRole) reader.ReadByte()
                };
            } else State = null;
        }

        protected override void Write(CelesteNetBinaryWriter writer) {
            writer.Write(State != null);
            if(State != null) {
                writer.WriteNetString(State.Value.roundID);
                writer.Write(State.Value.seed);
                writer.Write((byte) State.Value.role);
            }
        }
    }
}