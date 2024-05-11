﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace YakuzaGame.Files.Msg
{
    public class UnknownPropertyException : Exception
    {
        public UnknownPropertyException(string message) : base(message)
        {

        }
    }

    public class PauseMsgProperty : MsgProperty
    {
        public override bool IsEndProperty => false;

        public short Duration
        {
            get => (short)((_unknownInfo1[0] << 8) + _unknownInfo1[1]);
            set
            {
                var bytes = BitConverter.GetBytes(value);
                var reversed = bytes.Reverse();
                Array.Copy(reversed.ToArray(), 0, _unknownInfo1, 0, 2);
            }
        }

        public PauseMsgProperty() : base()
        {
            _header = new byte[] { 0x02, 0x09 };
            _unknownInfo1 = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            Duration = 10;
            Position = 0;
            _unknownInfo2 = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        }

        public PauseMsgProperty(byte[] data) : base(data)
        {

        }
    }

    public class ColorStartMsgProperty : MsgProperty
    {
        public override bool IsEndProperty => false;

        public short Color
        {
            get => (short)((_unknownInfo1[0] << 8) + _unknownInfo1[1]);
            set
            {
                var bytes = BitConverter.GetBytes(value);
                var reversed = bytes.Reverse();
                Array.Copy(reversed.ToArray(), 0, _unknownInfo1, 0, 2);
            }
        }

        public short TagLength
        {
            get => (short)((_unknownInfo2[2] << 8) + _unknownInfo2[3]);
            set
            {
                var bytes = BitConverter.GetBytes(value);
                var reversed = bytes.Reverse();
                Array.Copy(reversed.ToArray(), 0, _unknownInfo2, 2, 2);
            }
        }

        public byte Alpha
        {
            get => _unknownInfo2[4];
            set => _unknownInfo2[4] = value;
        }

        public byte R
        {
            get => _unknownInfo2[5];
            set => _unknownInfo2[5] = value;
        }

        public byte G
        {
            get => _unknownInfo2[6];
            set => _unknownInfo2[6] = value;
        }

        public byte B
        {
            get => _unknownInfo2[7];
            set => _unknownInfo2[7] = value;
        }

        public ColorStartMsgProperty() : base()
        {
            _header = new byte[] { 0x02, 0x07 };
            _unknownInfo1 = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            _unknownInfo2 = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            Color = 0;
            Position = 0;
            TagLength = 0;
            Alpha = 0;
            R = 0;
            G = 0;
            B = 0;
        }

        public ColorStartMsgProperty(byte[] data) : base(data)
        {
        }
    }

    public class ColorEndMsgProperty : MsgProperty
    {
        public override bool IsEndProperty => false;

        public ColorEndMsgProperty() : base()
        {
            _header = new byte[] { 0x02, 0x08 };
            _unknownInfo1 = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            Position = 0;
            _unknownInfo2 = new byte[] { 0x00, 0x00, 0x00, 0x0F, 0x00, 0x00, 0x00, 0x00 };
        }

        public ColorEndMsgProperty(byte[] data) : base(data)
        {
        }
    }

    public class SignMsgProperty : MsgProperty
    {
        public override bool IsEndProperty => false;

        public short Code
        {
            get => (short)((_unknownInfo2[0] << 8) + _unknownInfo2[1]);
            set
            {
                var bytes = BitConverter.GetBytes(value);
                var reversed = bytes.Reverse();
                Array.Copy(reversed.ToArray(), 0, _unknownInfo2, 0, 2);
            }
        }

        public short TagLength
        {
            get => (short)((_unknownInfo2[2] << 8) + _unknownInfo2[3]);
            set
            {
                var bytes = BitConverter.GetBytes(value);
                var reversed = bytes.Reverse();
                Array.Copy(reversed.ToArray(), 0, _unknownInfo2, 2, 2);
            }
        }

        public SignMsgProperty() : base()
        {
            _header = new byte[] { 0x02, 0x0F };
            _unknownInfo1 = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            _unknownInfo2 = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            Code = 0;
            Position = 0;
            TagLength = 0;
        }

        public SignMsgProperty(byte[] data) : base(data)
        {
        }
    }

    public class ParenthesisStartMsgProperty : MsgProperty
    {
        public override bool IsEndProperty => false;

        public ParenthesisStartMsgProperty() : base()
        {
            _header = new byte[] { 0x02, 0x0E };
            _unknownInfo1 = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            Position = 0;
            _unknownInfo2 = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        }

        public ParenthesisStartMsgProperty(byte[] data) : base(data)
        {
        }
    }

    public class ParenthesisEndMsgProperty : MsgProperty
    {
        public override bool IsEndProperty => false;

        public ParenthesisEndMsgProperty() : base()
        {
            _header = new byte[] { 0x02, 0x0E };
            _unknownInfo1 = new byte[] { 0x00, 0x01, 0x00, 0x00 };
            Position = 0;
            _unknownInfo2 = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        }

        public ParenthesisEndMsgProperty(byte[] data) : base(data)
        {
        }
    }


    public class MsgProperty : IComparable<MsgProperty>
    {
        protected byte[] _header;
        protected byte[] _unknownInfo1;
        public short Position { get; set; }
        protected byte[] _unknownInfo2;
        public int Order { get; set; }
        public virtual bool IsEndProperty { get; set; }

        protected MsgProperty()
        {

        }

        public MsgProperty(byte[] data)
        {
            _header = new byte[2];
            _unknownInfo1 = new byte[4];
            _unknownInfo2 = new byte[8];
            Array.Copy(data, 0, _header, 0, 2);
            Array.Copy(data, 2, _unknownInfo1, 0, 4);
            Position = (short)((data[6] << 8) + data[7]);
            Array.Copy(data, 8, _unknownInfo2, 0, 8);
        }

        public virtual byte[] ToByteArray()
        {
            var result = new byte[16];
            Array.Copy(_header, 0, result, 0, 2);
            Array.Copy(_unknownInfo1, 0, result, 2, 4);
            var bytes = BitConverter.GetBytes(Position);
            var reversed = bytes.Reverse();
            Array.Copy(reversed.ToArray(), 0, result, 6, 2);

            Array.Copy(_unknownInfo2, 0, result, 8, 8);
            return result;
        }

        public int CompareTo(MsgProperty other)
        {
            if (this.Order >= 0 && other.Order >= 0)
            {
                return Order.CompareTo(other.Order);
            }

            if (this.Order < 0 && other.Order < 0)
            {
                return Position.CompareTo(other.Position);
            }

            if (this.Order >= 0 && other.Order < 0)
            {
                if (this.Position != other.Position)
                {
                    return Position.CompareTo(other.Position);
                }

                if (Position == 0)
                {
                    return -1;
                }
                else
                {
                    return 1;
                }

            }

            if (this.Order < 0 && other.Order >= 0)
            {
                if (this.Position != other.Position)
                {
                    return Position.CompareTo(other.Position);
                }

                if (Position == 0)
                {
                    return 1;
                }
                else
                {
                    return -1;
                }

            }

            return Position.CompareTo(other.Position);
        }
    }

    public static class MsgPropertyFactory
    {
        public static MsgProperty GetProperty(byte[] data)
        {
            if (data[0] == 0x02 && data[1] == 0x09)
            {
                return new PauseMsgProperty(data);
            }

            if (data[0] == 0x02 && data[1] == 0x07)
            {
                return new ColorStartMsgProperty(data);
            }

            if (data[0] == 0x02 && data[1] == 0x08)
            {
                return new ColorEndMsgProperty(data);
            }

            if (data[0] == 0x02 && data[1] == 0x0E)
            {
                if (data[3] == 0)
                {
                    return new ParenthesisStartMsgProperty(data);
                }

                if (data[3] == 1)
                {
                    return new ParenthesisEndMsgProperty(data);
                }

                return new MsgProperty(data);
            }

            if (data[0] == 0x02 && data[1] == 0x0F)
            {
                return new SignMsgProperty(data);
            }

            return new MsgProperty(data);
        }
    }

    public class MsgProperties : IEnumerable<MsgProperty>
    {
        private readonly List<MsgProperty> Properties;

        public MsgProperties()
        {
            Properties = new List<MsgProperty>();
        }

        public MsgProperties(byte[] data) : this()
        {
            var propertyCount = data.Length / 16;

            for (var i = 0; i < propertyCount; i++)
            {
                var current = new byte[16];
                Array.Copy(data, i * 16, current, 0, 16);
                var property = MsgPropertyFactory.GetProperty(current);
                Properties.Add(property);
            }
        }

        public byte[] ToByteArray()
        {
            Properties.Sort();

            var result = new byte[16 * Properties.Count];

            for (var i = 0; i < Properties.Count; i++)
            {
                Array.Copy(Properties[i].ToByteArray(), 0, result, i * 16, 16);
            }

            return result;
        }

        public IEnumerator<MsgProperty> GetEnumerator()
        {
            return Properties.GetEnumerator();
        }

        public void Add(MsgProperty property)
        {
            Properties.Add(property);
        }

        public void AddRange(IEnumerable<MsgProperty> properties)
        {
            Properties.AddRange(properties);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count => Properties.Count;
    }
}
