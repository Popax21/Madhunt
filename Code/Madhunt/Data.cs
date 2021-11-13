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

        public string RoundID => $"{arenaArea.SID}#{arenaArea.Mode}#{spawnLevel}#{spawnIndex}#{Module.Instance.Metadata.Version.Major}.{Module.Instance.Metadata.Version.Minor}";
    }

    public enum PlayerState {
        HIDER, SEEKER
    }
    
    public class DataMadhuntStart : DataType<DataMadhuntStart> {
        static DataMadhuntStart() => DataType<DataMadhuntStart>.DataID = "madhuntStart";
        
        public int MajorVersion, MinorVersion;
        public DataPlayerInfo StartPlayer;
        public RoundSettings RoundSettings;
        public int? StartZoneID;
        public override MetaType[] GenerateMeta(DataContext ctx) => new MetaType[] { new MetaPlayerUpdate(StartPlayer) };

        public override void FixupMeta(DataContext ctx) => StartPlayer = Get<MetaPlayerUpdate>(ctx).Player;

        public override void Read(CelesteNetBinaryReader reader) {
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

            StartZoneID = reader.ReadBoolean() ? (int?) reader.ReadInt32() : null;
        }

        public override void Write(CelesteNetBinaryWriter writer) {
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

            writer.Write(StartZoneID.HasValue);
            if(StartZoneID.HasValue) writer.Write(StartZoneID.Value);
        }
    }

    public class DataMadhuntStateUpdate : DataType<DataMadhuntStateUpdate> {
        static DataMadhuntStateUpdate() => DataType<DataMadhuntStateUpdate>.DataID = "madhuntStateUpdate";

        public DataPlayerInfo Player;
        public (string roundID, PlayerState state)? RoundState;

        public override MetaType[] GenerateMeta(DataContext ctx) => new MetaType[] { new MetaPlayerPrivateState(Player), new MetaBoundRef(DataType<DataPlayerInfo>.DataID, Player?.ID ?? uint.MaxValue, true) };

        public override void FixupMeta(DataContext ctx) {
            Player = Get<MetaPlayerPrivateState>(ctx);
            Get<MetaBoundRef>(ctx).ID = Player?.ID ?? uint.MaxValue;
        }

        public override void Read(CelesteNetBinaryReader reader) {
            if(reader.ReadBoolean()) RoundState = (reader.ReadNetString(), (PlayerState) reader.ReadByte());
            else RoundState = null;
        }

        public override void Write(CelesteNetBinaryWriter writer) {
            writer.Write(RoundState != null);
            if(RoundState != null) {
                writer.WriteNetString(RoundState.Value.roundID);
                writer.Write((byte) RoundState.Value.state);
            }
        }
    }
}