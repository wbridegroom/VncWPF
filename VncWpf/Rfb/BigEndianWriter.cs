using System;
using System.IO;
using System.Text;

namespace VncWpf.Rfb
{
    public class BigEndianWriter : BinaryWriter
    {
        public BigEndianWriter(Stream stream) : base(stream) { }

        public BigEndianWriter(Stream stream, Encoding encoding) : base(stream, encoding) { }

        public override void Write(ushort value)
        {
            WriteToStream(BitConverter.GetBytes(value));
        }

        public override void Write(short value)
        {
            WriteToStream(BitConverter.GetBytes(value));
        }

        public override void Write(uint value)
        {
            WriteToStream(BitConverter.GetBytes(value));
        }

        public override void Write(int value)
        {
            WriteToStream(BitConverter.GetBytes(value));
        }

        public override void Write(ulong value)
        {
            WriteToStream(BitConverter.GetBytes(value));
        }

        public override void Write(long value)
        {
            WriteToStream(BitConverter.GetBytes(value));
        }

        private void WriteToStream(byte[] bytes)
        {
            Array.Reverse(bytes);
            Write(bytes);
        }
    }
}
