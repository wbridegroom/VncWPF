using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace VncWpf.Rfb
{
    public class RfbProtocol
    {
        public const string PROTOCOL_VERSION = "003.008";

        private readonly ushort[,] colorMap = new ushort[256, 3];

        private readonly BinaryReader reader;
        private readonly BinaryWriter writer;
        private readonly Encoding encoding;
        private readonly List<VncEncodings> supportedVncEncodings;

        public ushort[,] ColorMap
        {
            get { return colorMap; }
        }

        public RfbProtocol(BinaryReader reader, BinaryWriter writer, Encoding encoding)
        {
            this.reader = reader;
            this.writer = writer;
            this.encoding = encoding;
            supportedVncEncodings = new List<VncEncodings>
            {
                VncEncodings.Hex,
                VncEncodings.CopyRect,
                VncEncodings.Raw,
                VncEncodings.DesktopSize
            };
        }

        public string ReadVersion()
        {
            var numArray = reader.ReadBytes(12);
            var str = "003.";
            switch (numArray[10])
            {
                case 49:
                    str = "004.001";
                    break;
                case 51:
                case 54:
                    str = str + "003";
                    break;
                case 55:
                    str = str + "007";
                    break;
                case 56:
                    str = str + "008";
                    break;
            }
            return str;
        }

        public void WriteVersion(string version)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append("RFB ").Append(version).Append("\n");
            writer.Write(encoding.GetBytes(stringBuilder.ToString()));
            writer.Flush();
        }

        public byte[] ReadChallenge()
        {
            return reader.ReadBytes(16);
        }

        public void SendChanllengeResponse(byte[] response)
        {
            writer.Write(response, 0, response.Length);
            writer.Flush();
        }

        public List<SecurityTypes> ReadSecurityTypes()
        {
            var list = new List<SecurityTypes>();
            int num = reader.ReadByte();
            for (var index = 0; index < num; ++index)
            {
                var securityTypes = (SecurityTypes) reader.ReadByte();
                list.Add(securityTypes);
            }
            return list;
        }

        public void WriteSecurityType(SecurityTypes securityType)
        {
            writer.Write((byte) securityType);
            writer.Flush();
        }

        public bool ReadSecurityResult()
        {
            return (int) reader.ReadUInt32() == 0;
        }

        public void WriteInitialization(bool shared)
        {
            if (shared) writer.Write((byte) 1);
            else writer.Write((byte) 0);

            writer.Flush();
        }

        public FrameBuffer ReadInitialization()
        {
            return new FrameBuffer(reader.ReadUInt16(), reader.ReadUInt16(),
                PixelFormat.GetPixelFormat(reader.ReadBytes(16)),
                encoding.GetString(reader.ReadBytes((int) reader.ReadUInt32())));
        }

        public void WriteSetPixelFormat(PixelFormat pixelFormat)
        {
            writer.Write((byte) 0);
            writer.Write(new byte[3], 0, 3);
            writer.Write(PixelFormat.GetBytes(pixelFormat));
            writer.Flush();
        }

        public void WriteSetEncoding()
        {
            writer.Write((byte) 2);
            writer.Write((byte) 0);
            writer.Write((ushort) supportedVncEncodings.Count);
            foreach (var num in supportedVncEncodings.Select(obj => (int) obj))
            {
                writer.Write(num);
            }
            writer.Flush();
        }

        public void WriteFramebufferUpdateRequest(Int32Rect rect, bool incremental)
        {
            writer.Write((byte) 3);
            writer.Write(incremental ? (byte) 1 : (byte) 0);
            writer.Write((ushort) rect.X);
            writer.Write((ushort) rect.Y);
            writer.Write((ushort) rect.Width);
            writer.Write((ushort) rect.Height);
            writer.Flush();
        }

        public void WriteKeyEvent(uint key, bool pressed)
        {
            writer.Write((byte) 4);
            writer.Write(pressed ? (byte) 1 : (byte) 0);
            writer.Write(new byte[2], 0, 2);
            writer.Write(key);
            writer.Flush();
        }

        public void WritePointerEvent(byte buttonMask, Point point)
        {
            writer.Write((byte) 5);
            writer.Write(buttonMask);
            writer.Write((ushort) point.X);
            writer.Write((ushort) point.Y);
            writer.Flush();
        }

        public void WriteClientCutText(string text)
        {
            writer.Write((byte) 6);
            writer.Write(new byte[3], 0, 3);
            writer.Write((uint) text.Length);
            writer.Write(encoding.GetBytes(text));
            writer.Flush();
        }

        public ServerMessageTypes ReadServerMessageType()
        {
            return (ServerMessageTypes) reader.ReadByte();
        }

        public int ReadNumRects()
        {
            reader.ReadByte();
            return reader.ReadUInt16();
        }

        public Int32Rect ReadRectSize()
        {
            return new Int32Rect
            {
                X = reader.ReadUInt16(),
                Y = reader.ReadUInt16(),
                Width = reader.ReadUInt16(),
                Height = reader.ReadUInt16()
            };
        }

        public int[] ReadRectPixels(FrameBuffer buffer, Int32Rect rect)
        {
            var numArray = new int[0];
            switch ((VncEncodings) reader.ReadUInt32())
            {
                case VncEncodings.DesktopSize:
                    buffer.ResizeDesktop(rect.Width, rect.Height);
                    break;
                case VncEncodings.Raw:
                    numArray = ReadRawPixels(buffer, rect);
                    break;
                case VncEncodings.CopyRect:
                    numArray = ReadCopyRectPixels(rect);
                    break;
                case VncEncodings.Hex:
                    numArray = ReadHextilePixels(buffer, rect);
                    break;
            }
            return numArray;
        }

        public void ReadColorMap()
        {
            reader.ReadByte();
            var num2 = reader.ReadUInt16();
            var num3 = reader.ReadUInt16();
            var num4 = 0;
            while (num4 < num3)
            {
                colorMap[num2, 0] = (byte) (reader.ReadUInt16()*byte.MaxValue/ushort.MaxValue);
                colorMap[num2, 1] = (byte) (reader.ReadUInt16()*byte.MaxValue/ushort.MaxValue);
                colorMap[num2, 2] = (byte) (reader.ReadUInt16()*byte.MaxValue/ushort.MaxValue);
                ++num4;
                ++num2;
            }
        }

        public string ReadServerCutText()
        {
            reader.ReadBytes(3);
            return encoding.GetString(reader.ReadBytes((int) reader.ReadUInt32()));
        }

        private int[] ReadRawPixels(FrameBuffer buffer, Int32Rect rect)
        {
            return ReadPixels(buffer, rect);
        }

        private int[] ReadCopyRectPixels(Int32Rect rect)
        {
            var numArray = new int[rect.Width*rect.Height];
            reader.ReadInt16();
            reader.ReadInt16();
            return numArray;
        }

        private int[] ReadHextilePixels(FrameBuffer buffer, Int32Rect rect)
        {
            var num1 = 0;
            var num2 = 0;
            var numArray = new int[rect.Width*rect.Height];
            var y = 0;
            while (y < rect.Height)
            {
                var x = 0;
                while (x < rect.Width)
                {
                    var height1 = rect.Height - y < 16 ? rect.Height - y : 16;
                    var width1 = rect.Width - x < 16 ? rect.Width - x : 16;
                    var num3 = reader.ReadByte();
                    var flag1 = (num3 & 1) != 0;
                    var flag2 = (num3 & 2) != 0;
                    var flag3 = (num3 & 4) != 0;
                    var flag4 = (num3 & 8) != 0;
                    var flag5 = (num3 & 16) != 0;
                    if (flag1)
                    {
                        var num4 = 0;
                        var num5 = 0;
                        var int32Rect = new Int32Rect(x, y, width1, height1);
                        if (int32Rect != rect)
                        {
                            num4 = y*rect.Width + x;
                            num5 = rect.Width - width1;
                        }
                        for (var index1 = 0; index1 < int32Rect.Height; ++index1)
                        {
                            for (var index2 = 0; index2 < int32Rect.Width; ++index2)
                                numArray[num4++] = ReadPixel(buffer);
                            num4 += num5;
                        }
                    }
                    else
                    {
                        if (flag2) num1 = ReadPixel(buffer);
                        var num4 = 0;
                        var num5 = 0;
                        var int32Rect1 = new Int32Rect(x, y, width1, height1);
                        if (int32Rect1 != rect)
                        {
                            num4 = y*rect.Width + x;
                            num5 = rect.Width - width1;
                        }
                        for (var index1 = 0; index1 < int32Rect1.Height; ++index1)
                        {
                            for (var index2 = 0; index2 < int32Rect1.Width; ++index2)
                            {
                                numArray[num4++] = num1;
                            }
                            num4 += num5;
                        }
                        if (flag3) num2 = ReadPixel(buffer);
                        if (flag4)
                        {
                            int num6 = reader.ReadByte();
                            for (var index1 = 0; index1 < num6; ++index1)
                            {
                                if (flag5) num2 = ReadPixel(buffer);
                                int num7 = reader.ReadByte();
                                var num8 = num7 >> 4 & 15;
                                var num9 = num7 & 15;
                                int num10 = reader.ReadByte();
                                var width2 = (num10 >> 4 & 15) + 1;
                                var height2 = (num10 & 15) + 1;
                                var int32Rect2 = new Int32Rect(x + num8, y + num9, width2, height2);
                                var num11 = 0;
                                var num12 = 0;
                                if (int32Rect2 != rect)
                                {
                                    num11 = int32Rect2.Y*rect.Width + int32Rect2.X;
                                    num12 = rect.Width - int32Rect2.Width;
                                }
                                for (var index2 = 0; index2 < int32Rect2.Height; ++index2)
                                {
                                    for (var index3 = 0; index3 < int32Rect2.Width; ++index3)
                                        numArray[num11++] = num2;
                                    num11 += num12;
                                }
                            }
                        }
                    }
                    x += 16;
                }
                y += 16;
            }
            return numArray;
        }

        private int[] ReadPixels(FrameBuffer buffer, Int32Rect rect)
        {
            var length = rect.Width*rect.Height;
            var numArray = new int[length];
            for (var index = 0; index < length; ++index)
            {
                numArray[index] = ReadPixel(buffer);
            }
            return numArray;
        }

        private int ReadPixel(FrameBuffer buffer)
        {
            switch (buffer.PixelFormat.Bpp)
            {
                case 8:
                    return Read8BppPixel();
                case 16:
                    return Read16BppPixel(buffer.PixelFormat);
                case 32:
                    return Read32BppPixel(buffer.PixelFormat);
                default:
                    throw new Exception("Unknown Pixel Format: " + buffer.PixelFormat.Bpp);
            }
        }

        private int Read8BppPixel()
        {
            var num = reader.ReadByte();
            return GetRgbValue((byte)colorMap[num, 2], (byte)colorMap[num, 1], (byte)colorMap[num, 0]);
        }

        private int Read16BppPixel(PixelFormat pf)
        {
            var numArray = reader.ReadBytes(2);
            var num = (ushort) (numArray[0] & byte.MaxValue | numArray[1] << 8);
            return GetRgbValue((byte)((num >> pf.RedShift & pf.RedMax) << 3),
                (byte) ((num >> pf.GreenShift & pf.GreenMax) << 2), (byte) ((num >> pf.BlueShift & pf.BlueMax) << 3));
        }

        private int Read32BppPixel(PixelFormat pf)
        {
            var numArray = reader.ReadBytes(4);
            var num = (uint) (numArray[0] & byte.MaxValue | numArray[1] << 8 | numArray[2] << 16 | numArray[3] << 24);
            return GetRgbValue((byte)(num >> pf.RedShift & (ulong)pf.RedMax),
                (byte) (num >> pf.GreenShift & (ulong) pf.GreenMax), (byte) (num >> pf.BlueShift & (ulong) pf.BlueMax));
        }

        private static int GetRgbValue(byte red, byte green, byte blue)
        {
            return blue & byte.MaxValue | green << 8 | red << 16 | -16777216;
        }
    }

    public enum VncEncodings
    {
        Cursor = -239,
        DesktopSize = -223,
        Raw = 0,
        CopyRect = 1,
        Rre = 2,
        Hex = 5,
        Zrle = 16,
    }

    public enum ServerMessageTypes : byte
    {
        FrameBufferUpdate = 0,
        SetColorMap = (byte)1,
        Bell = (byte)2,
        ServerCutText = (byte)3,
        Unknown = (byte)8,
    }

    public enum SecurityTypes : byte
    {
        None = (byte)1,
        VNC = (byte)2,
        Tight = (byte)16,
        Ultra = (byte)17,
        TLS = (byte)18,
    }

    public enum ClientMessageTypes : byte
    {
        SetPixelFormat = 0,
        SetEncodings = (byte)2,
        FrameBufferUpdateRequest = (byte)3,
        KeyEvent = (byte)4,
        PointEvent = (byte)5,
        ClientCutText = (byte)6,
    }

    public enum HextileEncoding : byte
    {
        Raw = (byte)1,
        BackgoundSpecified = (byte)2,
        ForegroundSpecified = (byte)4,
        AnySubrects = (byte)8,
        SubrectsColored = (byte)10,
    }
}
