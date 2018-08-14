using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VncWpf.Commands;
using VncWpf.Rfb;

namespace VncWpf
{
    public delegate void ViewerDisconnected(EventArgs args, string message);

    public class VncViewModel : INotifyPropertyChanged
    {
        private const string CONNECTION_FAILED_MESSAGE = "VNC Connection Failed:";

        private bool ctrlAltPressed;

        public event ViewerDisconnected ViewerDisconnected;

        public static List<StretchMode> STRETCH_MODES = new List<StretchMode>
        {
            new StretchMode {Name = "None", Mode = Stretch.None},
            new StretchMode {Name = "Fill", Mode = Stretch.Fill},
            new StretchMode {Name = "Maintain Aspect Ratio", Mode = Stretch.Uniform}
        };

        private WriteableBitmap desktopBitmap;
        public WriteableBitmap DesktopBitmap
        {
            get { return desktopBitmap; }
            set { desktopBitmap = value; Notify(); }
        }

        private Image desktop;
        public Image Desktop
        {
            get => desktop;
            set { desktop = value; Notify(); }
        }

        private string hostname;
        public string Hostname
        {
            get => hostname;
            set { hostname = value; Notify();}
        }

        private int port;
        public int Port
        {
            get => port;
            set { port = value; Notify();}
        }

        private string password;
        public string Password
        {
            get => password;
            set { password = value; Notify(); }
        }

        private bool isConnected;
        public bool IsConnected
        {
            get => isConnected;
            set { isConnected = value; Notify(); }
        }

        private bool viewOnly;
        public bool ViewOnly
        {
            get => viewOnly;
            set { viewOnly = value; Notify(); }
        }

        private bool isSharedConnection;
        public bool IsSharedConnection
        {
            get => isSharedConnection;
            set { isSharedConnection = value; Notify(); }
        }

        private string focusedElement;
        public string FocusedElement
        {
            get => focusedElement;
            set { focusedElement = value; Notify(); }
        }

        private double height;
        public double Height
        {
            get => height;
            set { height = value; Notify(); }
        }

        private double width;
        public double Width
        {
            get => width;
            set { width = value; Notify(); }
        }

        private double frameHeight;
        public double FrameHeight
        {
            get => frameHeight;
            set { frameHeight = value; Notify(); }
        }

        private double frameWidth;
        public double FrameWidth
        {
            get => frameWidth;
            set { frameWidth = value; Notify(); }
        }

        private StretchMode selectedStretchMode;
        public StretchMode SelectedStretchMode
        {
            get => selectedStretchMode;
            set 
            {
                selectedStretchMode = value; 
                Notify();
                SetStretchMode(value.Mode);
            }
        }

        public RfbClient Client { get; private set; }

        public ICommand Connect { get; set; }
        public ICommand Disconnect { get; set; }
        public ICommand SendCtrlAltDel { get; set; }

        public Dispatcher Dispatcher { get; set; }

        public VncViewModel()
        {
            Port = 5900;
            Client = new RfbClient(this);
            Client.UpdateDesktop += UpdateDesktop;
            Client.ServerTextCut += ServerCutText;
            Client.DesktopSizeChanged += DesktopSizeChanged;
            Client.ConnectionClosed += ConnectionClosed;

            Connect = new ConnectCommand(this);
            Disconnect = new DisconnectCommand(this);
            SendCtrlAltDel = new SendCtrlAltDelCommand(this);

            selectedStretchMode = STRETCH_MODES[0];
            Notify("SelectedStretchMode");
        }

        private void UpdateDesktop(List<EncodedRect> encodedRects)
        {
            foreach (var encodedRect in encodedRects)
            {
                DesktopBitmap.WritePixels(encodedRect.Rect, encodedRect.Pixels, encodedRect.Rect.Width * 4, 0);
            }

            try
            {
                Client.SendUpdateRequest(true);
            }
            catch (Exception ex)
            {
//                Logger.Error(ex, CONNECTION_FAILED_MESSAGE);
                ConnectionClosed(ex.StackTrace);
            }
        }

        private static void ServerCutText(string text)
        {
            Clipboard.SetText(text);
        }

        private void DesktopSizeChanged(Int32Rect rect)
        {
            Height = rect.Height;
            Width = rect.Width;
            DesktopBitmap = new WriteableBitmap(rect.Width, rect.Height, 96.0, 96.0, PixelFormats.Pbgra32, null);
            Desktop.Source = DesktopBitmap;
            Client.SendUpdateRequest(false);

            if (ViewOnly) return;

            FocusedElement = "VncFrame";
        }

        public void ConnectionClosed(string message)
        {
            IsConnected = false;
            Dispatcher.Invoke(() => Desktop.Source = null);
            Client.Disconnect();

            ViewerDisconnected?.Invoke(null, "Connection Lost");
        }

        public void SetStretchMode(Stretch stretch)
        {
            Desktop.Stretch = stretch;
            switch (stretch)
            {
                case Stretch.Fill:
                    Height = FrameHeight;
                    Width = FrameWidth;
                    break;
                case Stretch.Uniform:
                    Height = FrameHeight;
                    Width = FrameWidth;
                    break;
                default:
                    if (stretch == Stretch.None && Desktop.Source != null)
                    {
                        Height = Desktop.Source.Height;
                        Width = Desktop.Source.Width;
                    }
                    break;
            }
            if (viewOnly) return;

            FocusedElement = "VncFrame";
        }

        public void ManageKeyDownAndKeyUp(KeyEventArgs e, bool isDown)
        {
            if (viewOnly) return;

            var modifiers = e.KeyboardDevice.Modifiers;
            var flag = modifiers == ModifierKeys.Shift;
            if (modifiers == (ModifierKeys.Alt | ModifierKeys.Control)) ctrlAltPressed = true;
            else if (modifiers == ModifierKeys.None && ctrlAltPressed)
            {
                Client.KeyboardEvent(65513U, false);
                Client.KeyboardEvent(65507U, false);
                ctrlAltPressed = false;
            }

            var key = (uint)KeyInterop.VirtualKeyFromKey(e.Key);
            try
            {
                if (key >= 65U && key <= 90U)
                {
                    if (!flag) key += 32U;
                    Client.KeyboardEvent(key, isDown);
                    e.Handled = true;
                }
                else if (key >= 48U && key <= 57U)
                {
                    if (flag) key = GetFlagKey(e.Key, key);
                    Client.KeyboardEvent(key, isDown);
                    e.Handled = true;
                }
                else
                {
                    key = GetKey(e.Key, key, flag);
                    Client.KeyboardEvent(key, isDown);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
//                Logger.Error(ex, CONNECTION_FAILED_MESSAGE);
                ConnectionClosed("Error: " + ex.Message);
            }
        }

        public void MouseEvent(MouseButtonState leftButton, MouseButtonState middleButton, MouseButtonState rightButton, int scrollDelta, Point point)
        {
            if (Client != null && !viewOnly)
            {
                byte buttonMask = 0;
                if (leftButton == MouseButtonState.Pressed) ++buttonMask;
                if (middleButton == MouseButtonState.Pressed) buttonMask += 2;
                if (rightButton == MouseButtonState.Pressed) buttonMask += 4;
                if (scrollDelta > 0) buttonMask += 8;
                if (scrollDelta < 0) buttonMask += 16;

                try
                {
                    Client.MouseEvent(buttonMask, TranslatePoint(point));
                }
                catch (Exception ex)
                {
                    ConnectionClosed("Error: " + ex.Message);
                }
            }
            if (viewOnly) return;

            FocusedElement = "VncFrame";
        }

        private Point TranslatePoint(Point point)
        {
            var actualWidth = Desktop.ActualWidth;
            var actualHeight = Desktop.ActualHeight;
            var pixelWidth = DesktopBitmap.PixelWidth;
            var pixelHeight = DesktopBitmap.PixelHeight;
            return new Point(point.X * pixelWidth / actualWidth, point.Y * pixelHeight / actualHeight);
        }

        #region System Hooks

        private struct KbHookStruct
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public int dwExtraInfo;
        }
        private delegate IntPtr HookHandlerDelegate(int nCode, IntPtr wParam, ref KbHookStruct lParam);
        private static HookHandlerDelegate callbackPtr;
        private static IntPtr hookPtr = IntPtr.Zero;
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookHandlerDelegate callbackPtr, IntPtr hInstance, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, ref KbHookStruct lParam);

        public void DisableSystemKeys()
        {
            if (callbackPtr == null) callbackPtr = KeyboardHookHandler;
            if (!(hookPtr == IntPtr.Zero)) return;
            var hinstance = Marshal.GetHINSTANCE(Application.Current.GetType().Module);
            hookPtr = SetWindowsHookEx(13, callbackPtr, hinstance, 0U);
        }

        public void EnableSystemKeys()
        {
            if (!(hookPtr != IntPtr.Zero)) return;
            UnhookWindowsHookEx(hookPtr);
            callbackPtr = null;
            hookPtr = IntPtr.Zero;
        }

        private IntPtr KeyboardHookHandler(int nCode, IntPtr wParam, ref KbHookStruct lParam)
        {
            if (nCode != 0) return CallNextHookEx(hookPtr, nCode, wParam, ref lParam);
            if (lParam.vkCode == 9 && lParam.flags == 32)
            {
                Client.KeyboardEvent(65513U, true);
                Client.KeyboardEvent(65289U, true);
                Client.KeyboardEvent(65289U, false);
                return new IntPtr(1);
            }
            if (lParam.vkCode == 91 && lParam.flags == 1)
            {
                Client.KeyboardEvent(65515U, true);
                Client.KeyboardEvent(65515U, false);
                return new IntPtr(1);
            }
            if (lParam.vkCode != 92 || lParam.flags != 1) return CallNextHookEx(hookPtr, nCode, wParam, ref lParam);
            Client.KeyboardEvent(65516U, true);
            Client.KeyboardEvent(65516U, false);
            return new IntPtr(1);
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Protected Methods

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        protected void Notify([CallerMemberName]string propertyName = null)
        {
            if (PropertyChanged == null || propertyName == null) return;

            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Key Codes 

        private static uint GetFlagKey(Key flagKeyPressed, uint originalKeyValue)
        {
            var key = originalKeyValue;
            switch (flagKeyPressed)
            {
                case Key.D0:
                    key = 41U;
                    break;
                case Key.D1:
                    key = 33U;
                    break;
                case Key.D2:
                    key = 64U;
                    break;
                case Key.D3:
                    key = 35U;
                    break;
                case Key.D4:
                    key = 36U;
                    break;
                case Key.D5:
                    key = 37U;
                    break;
                case Key.D6:
                    key = 94U;
                    break;
                case Key.D7:
                    key = 38U;
                    break;
                case Key.D8:
                    key = 42U;
                    break;
                case Key.D9:
                    key = 40U;
                    break;
            }
            return key;
        }

        private uint GetKey(Key keyPressed, uint originalKeyValue, bool flag)
        {
            var key = originalKeyValue;
            switch (keyPressed)
            {
                case Key.LWin:
                    key = 65515U;
                    break;
                case Key.Back:
                    key = 65288U;
                    break;
                case Key.Tab:
                    key = 65289U;
                    break;
                case Key.Return:
                    key = 65293U;
                    break;
                case Key.Capital:
                    key = 65509U;
                    break;
                case Key.Escape:
                    key = 65307U;
                    break;
                case Key.Prior:
                    key = 65365U;
                    break;
                case Key.Next:
                    key = 65366U;
                    break;
                case Key.End:
                    key = 65367U;
                    break;
                case Key.Home:
                    key = 65360U;
                    break;
                case Key.Left:
                    key = 65361U;
                    break;
                case Key.Up:
                    key = 65362U;
                    break;
                case Key.Right:
                    key = 65363U;
                    break;
                case Key.Down:
                    key = 65364U;
                    break;
                case Key.Insert:
                    key = 65379U;
                    break;
                case Key.Delete:
                    key = ushort.MaxValue;
                    break;
                case Key.Apps:
                    key = 65518U;
                    break;
                case Key.NumPad0:
                    key = 65456U;
                    break;
                case Key.NumPad1:
                    key = 65457U;
                    break;
                case Key.NumPad2:
                    key = 65458U;
                    break;
                case Key.NumPad3:
                    key = 65459U;
                    break;
                case Key.NumPad4:
                    key = 65460U;
                    break;
                case Key.NumPad5:
                    key = 65461U;
                    break;
                case Key.NumPad6:
                    key = 65462U;
                    break;
                case Key.NumPad7:
                    key = 65463U;
                    break;
                case Key.NumPad8:
                    key = 65464U;
                    break;
                case Key.NumPad9:
                    key = 65465U;
                    break;
                case Key.Multiply:
                    key = 65450U;
                    break;
                case Key.Add:
                    key = 65451U;
                    break;
                case Key.Subtract:
                    key = 65453U;
                    break;
                case Key.Decimal:
                    key = 65454U;
                    break;
                case Key.Divide:
                    key = 65455U;
                    break;
                case Key.F1:
                case Key.F2:
                case Key.F3:
                case Key.F4:
                case Key.F5:
                case Key.F6:
                case Key.F7:
                case Key.F8:
                case Key.F9:
                case Key.F10:
                case Key.F11:
                case Key.F12:
                    key = (uint)(65470 + (keyPressed - 90));
                    break;
                case Key.NumLock:
                    key = 65407U;
                    break;
                case Key.LeftShift:
                    key = 65505U;
                    break;
                case Key.RightShift:
                    key = 65506U;
                    break;
                case Key.LeftCtrl:
                    key = 65507U;
                    break;
                case Key.RightCtrl:
                    key = 65508U;
                    break;
                case Key.LeftAlt:
                    key = 65513U;
                    break;
                case Key.RightAlt:
                    key = 65514U;
                    break;
                case Key.Oem1:
                    key = flag ? 58U : 59U;
                    break;
                case Key.OemPlus:
                    key = flag ? 43U : 61U;
                    break;
                case Key.OemComma:
                    key = flag ? 60U : 44U;
                    break;
                case Key.OemMinus:
                    key = flag ? 95U : 45U;
                    break;
                case Key.OemPeriod:
                    key = flag ? 62U : 46U;
                    break;
                case Key.Oem2:
                    key = flag ? 63U : 47U;
                    break;
                case Key.Oem3:
                    key = flag ? 126U : 96U;
                    break;
                case Key.Oem4:
                    key = flag ? 123U : 91U;
                    break;
                case Key.Oem5:
                    key = flag ? 124U : 92U;
                    break;
                case Key.Oem6:
                    key = flag ? 125U : 93U;
                    break;
                case Key.Oem7:
                    key = flag ? 34U : 39U;
                    break;
                case Key.System:
                    key = 65513U;
                    break;
            }
            return key;
        }

        #endregion
    }

    public class StretchMode
    {
        public string Name { get; set; }
        public Stretch Mode { get; set; }
    }
}