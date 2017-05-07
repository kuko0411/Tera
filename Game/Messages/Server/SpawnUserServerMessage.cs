﻿namespace Tera.Game.Messages
{
    public class SpawnUserServerMessage : ParsedMessage
    {
        internal SpawnUserServerMessage(TeraMessageReader reader)
            : base(reader)
        {
            reader.Skip(8);
            var nameOffset = reader.ReadUInt16();
            reader.Skip(14);
            ServerId = reader.ReadUInt32();
            PlayerId = reader.ReadUInt32();
            Id = reader.ReadEntityId();
            Position = reader.ReadVector3f();
            Heading = reader.ReadAngle();
            reader.Skip(4);
            RaceGenderClass = new RaceGenderClass(reader.ReadInt32());
            reader.Skip(11);
            Dead = (reader.ReadByte() & 1) == 0;
            reader.Skip(121);
            Level = reader.ReadInt16();
            reader.BaseStream.Position = nameOffset - 4;
            Name = reader.ReadTeraString();
            GuildName = reader.ReadTeraString();
            //Debug.WriteLine(Name + ":" + BitConverter.ToString(BitConverter.GetBytes(Id.Id))+ ":"+ ServerId.ToString()+" "+ BitConverter.ToString(BitConverter.GetBytes(PlayerId))+" "+Dead);
        }

        public int Level { get; }
        public bool Dead { get; set; }
        public Angle Heading { get; set; }
        public Vector3f Position { get; set; }
        public EntityId Id { get; }
        public uint ServerId { get; }
        public uint PlayerId { get; }
        public string Name { get; }
        public string GuildName { get; }
        public RaceGenderClass RaceGenderClass { get; }
    }
}