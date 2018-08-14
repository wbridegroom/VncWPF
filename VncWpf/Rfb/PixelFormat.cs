namespace VncWpf.Rfb
{
    public class PixelFormat
    {
        public int Bpp { get; set; }

        public int Depth { get; set; }

        public bool BigEndian { get; set; }

        public bool TrueColor { get; set; }

        public int RedMax { get; set; }

        public int GreenMax { get; set; }

        public int BlueMax { get; set; }

        public int RedShift { get; set; }

        public int GreenShift { get; set; }

        public int BlueShift { get; set; }

        public static PixelFormat GetPixelFormat(byte[] b)
        {
            return new PixelFormat()
            {
                Bpp         = b[0],
                Depth       = b[1],
                BigEndian   = b[2] != 0,
                TrueColor   = b[3] != 0,
                RedMax      = b[5] | b[4] << 8,
                GreenMax    = b[7] | b[6] << 8,
                BlueMax     = b[9] | b[8] << 8,
                RedShift    = b[10],
                GreenShift  = b[11],
                BlueShift   = b[12]
            };
        }

        public static byte[] GetBytes(PixelFormat pf)
        {
            return new []
            {
                (byte) pf.Bpp,
                (byte) pf.Depth,
                pf.BigEndian ? (byte) 1 : (byte) 0,
                pf.TrueColor ? (byte) 1 : (byte) 0,
                (byte) (pf.RedMax >> 8 & byte.MaxValue),
                (byte) (pf.RedMax & byte.MaxValue),
                (byte) (pf.GreenMax >> 8 & byte.MaxValue),
                (byte) (pf.GreenMax & byte.MaxValue),
                (byte) (pf.BlueMax >> 8 & byte.MaxValue),
                (byte) (pf.BlueMax & byte.MaxValue),
                (byte) pf.RedShift,
                (byte) pf.GreenShift,
                (byte) pf.BlueShift,
                (byte) 0,
                (byte) 0,
                (byte) 0
            };
        }
    }
}
