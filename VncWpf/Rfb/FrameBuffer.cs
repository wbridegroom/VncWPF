using System.Windows;

namespace VncWpf.Rfb
{
    public class FrameBuffer
    {
        public int Width { get; private set; }

        public int Height { get; private set; }

        public PixelFormat PixelFormat { get; private set; }

        public string Name { get; private set; }

        public Int32Rect Rect { get; private set; }

        public FrameBuffer(int width, int height, PixelFormat pixelFormat, string name)
        {
            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
            Name = name;
            Rect = new Int32Rect(0, 0, width, height);
        }

        public void ResizeDesktop(int newWidth, int newHeight)
        {
            Width = newWidth;
            Height = newHeight;
        }
    }
}
