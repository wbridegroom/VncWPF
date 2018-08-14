using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace VncWpf.Rfb
{
    public delegate void UpdateDesktop(List<EncodedRect> encodedRects);
    public delegate void DesktopSizeChanged(Int32Rect rect);
    public delegate void ConnectionClosed(string message);
    public delegate void ServerTextCut(string text);

    public class RfbClient
    {
        private TcpClient tcp;
        private NetworkStream stream;
        private BinaryReader reader;
        private BinaryWriter writer;
        private RfbProtocol protocol;
        private readonly VncViewModel viewModel;
        private Task updateTask;
        private ManualResetEvent done;

        public FrameBuffer Buffer { get; private set; }

        public event UpdateDesktop UpdateDesktop;
        public event DesktopSizeChanged DesktopSizeChanged;
        public event ServerTextCut ServerTextCut;
        public event ConnectionClosed ConnectionClosed;

        public RfbClient(VncViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public FrameBuffer Connect()
        {
            tcp = new TcpClient { NoDelay = true };
            tcp.Connect(viewModel.Hostname, viewModel.Port);
            stream = tcp.GetStream();
            reader = new BigEndianReader(stream);
            writer = new BigEndianWriter(stream);
            protocol = new RfbProtocol(reader, writer, Encoding.ASCII);
            
            var version = protocol.ReadVersion();
            if (version == "003.008")
            {
                protocol.WriteVersion(version);
                var list = protocol.ReadSecurityTypes();
                if (list.Contains(SecurityTypes.None))
                {
                    protocol.WriteSecurityType(SecurityTypes.None);
                    if (version == "003.008" && !protocol.ReadSecurityResult())
                    {
                        Close();
                        return null;
                    }
                }
                else if (list.Contains(SecurityTypes.VNC))
                {
                    if (!AuthenticateVnc())
                    {
                        Close();
                        return null;
                    }
                }
                else
                {
                    Close();
                    return null;
                }
                viewModel.IsConnected = true;
                protocol.WriteInitialization(viewModel.IsSharedConnection);
                Buffer = protocol.ReadInitialization();
                protocol.WriteSetPixelFormat(Buffer.PixelFormat);
                protocol.WriteSetEncoding();
                return Buffer;
            }
            Close();
            return null;
        }

        public void Disconnect()
        {
            done.Set();
            updateTask.Wait(500);
            Close();
        }

        private bool AuthenticateVnc()
        {
            if (string.IsNullOrEmpty(viewModel.Password)) return false;
            protocol.WriteSecurityType(SecurityTypes.VNC);
            protocol.SendChanllengeResponse(EncryptChallenge(viewModel.Password, protocol.ReadChallenge()));
            return protocol.ReadSecurityResult();
        }

        private static byte[] EncryptChallenge(string password, byte[] challenge)
        {
            var numArray = new byte[8];
            Encoding.ASCII.GetBytes(password, 0, password.Length >= 8 ? 8 : password.Length, numArray, 0);
            for (var index = 0; index < 8; ++index)
            {
                numArray[index] = (byte) ((numArray[index] & 1) << 7 | 
                                          (numArray[index] & 2) << 5 | 
                                          (numArray[index] & 4) << 3 |
                                          (numArray[index] & 8) << 1 | 
                                          (numArray[index] & 16) >> 1 | 
                                          (numArray[index] & 32) >> 3 |
                                          (numArray[index] & 64) >> 5 | 
                                          (numArray[index] & 128) >> 7);
            }
            var des = new DESCryptoServiceProvider {Padding = PaddingMode.None, Mode = CipherMode.ECB};
            var encryptor = des.CreateEncryptor(numArray, null);
            var outputBuffer = new byte[16];
            encryptor.TransformBlock(challenge, 0, challenge.Length, outputBuffer, 0);
            return outputBuffer;
        }

        public void BeginUpdates()
        {
            done = new ManualResetEvent(false);
            updateTask = Task.Factory.StartNew(UpdateThread);
        }

        private bool CheckDone()
        {
            return done.WaitOne(0, false);
        }

        private void UpdateThread()
        {
            SendUpdateRequest(false);
            while (!CheckDone())
            {
                if (!tcp.Connected) break;
                try
                {
                    switch (protocol.ReadServerMessageType())
                    {
                        case ServerMessageTypes.FrameBufferUpdate:
                            var num = protocol.ReadNumRects();
                            var encodedRects = new List<EncodedRect>();
                            for (var index = 0; index < num; ++index)
                            {
                                var encodedRect = new EncodedRect {Rect = protocol.ReadRectSize()};
                                var numArray = protocol.ReadRectPixels(Buffer, encodedRect.Rect);
                                if (numArray.Length == 0)
                                {
                                    if (DesktopSizeChanged != null)
                                    {
                                        viewModel.Dispatcher.Invoke(() => DesktopSizeChanged(encodedRect.Rect));
                                    }
                                    break;
                                }
                                encodedRect.Pixels = numArray;
                                encodedRects.Add(encodedRect);
                            }
                            if (UpdateDesktop != null)
                            {
                                viewModel.Dispatcher.Invoke(() => UpdateDesktop(encodedRects));
                            }
                            continue;
                        case ServerMessageTypes.SetColorMap:
                            protocol.ReadColorMap();
                            continue;
                        case ServerMessageTypes.Bell:
                            SystemSounds.Beep.Play();
                            continue;
                        case ServerMessageTypes.ServerCutText:
                            var text = protocol.ReadServerCutText();
                            if (ServerTextCut != null) ServerTextCut(text);
                            continue;
                        default:
                            continue;
                    }
                }
                catch (Exception ex)
                {
                    if (ConnectionClosed == null) continue;
                    var message = "Error: " + ex.Message;
                    if (ex.InnerException != null) message = message + " Inner Message: " + ex.InnerException.Message;                    
                    ConnectionClosed(message);
                }
            }
        }

        public void SendUpdateRequest(bool incremental)
        {
            try
            {
                protocol.WriteFramebufferUpdateRequest(Buffer.Rect, incremental);
            }
            catch (Exception)
            {
                Close();
            }
        }

        public void ClientCutText(string text)
        {
            protocol.WriteClientCutText(text);
        }

        public void MouseEvent(byte buttonMask, Point point)
        {
            protocol.WritePointerEvent(buttonMask, point);
        }

        public void KeyboardEvent(uint key, bool pressed)
        {
            protocol.WriteKeyEvent(key, pressed);
        }

        public void Close()
        {
            viewModel.IsConnected = false;
            reader?.Close();
            writer?.Close();
            stream?.Close();
            tcp?.Close();
        }
    }
}
