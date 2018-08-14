using System.IO;
using System.Text;

namespace VncWpf.Rfb
{
    public class BigEndianReader : BinaryReader
    {
        private readonly byte[] buffer = new byte[4];

        public BigEndianReader(Stream stream) : base(stream) { }

        public BigEndianReader(Stream stream, Encoding encoding) : base(stream, encoding) { }

        public override ushort ReadUInt16()
        {
            ReadIntoBuffer(2);
            return (ushort) (buffer[1] | (uint) buffer[0] << 8);
        }

        public override short ReadInt16()
        {
            ReadIntoBuffer(2);
            return (short) (buffer[1] & byte.MaxValue | buffer[0] << 8);
        }

        public override uint ReadUInt32()
        {
            ReadIntoBuffer(4);
            return (uint) (buffer[3] & byte.MaxValue | buffer[2] << 8 | buffer[1] << 16 | buffer[0] << 24);
        }

        public override int ReadInt32()
        {
            ReadIntoBuffer(4);
            return buffer[3] | buffer[2] << 8 | buffer[1] << 16 | buffer[0] << 24;
        }

        private void ReadIntoBuffer(int size)
        {
            var offset = 0;
            do
            {
                var num = BaseStream.Read(buffer, offset, size - offset);
                if (num == 0) throw new IOException("Error reading bytes");

                offset += num;
            } while (offset < size);
        }
    }
}
