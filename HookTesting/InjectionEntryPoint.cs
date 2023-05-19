using EasyHook;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace HookTesting
{
    struct CustomVertex
    {
        public float x;
        public float y;
        public float z;
        public float rhw;
        public uint color;
    }

    public class InjectionEntryPoint : IEntryPoint
    {
        ServerInterface _server = null;

        Queue<string> _messageQueue = new Queue<string>();

        Device D3DDevice = null;
        IntPtr endScene_Address = IntPtr.Zero;
        IntPtr getCustomText_Address = IntPtr.Zero;

        public InjectionEntryPoint(
            RemoteHooking.IContext context,
            string channelName)
        {
            _server = RemoteHooking.IpcConnectClient<ServerInterface>(channelName);

            _server.Ping();
        }

        private static uint D3DCOLOR_XRGB(int r, int g, int b)
        {
            return (uint)((((0xff) & 0xff) << 24) | (((r) & 0xff) << 16) | (((g) & 0xff) << 8) | ((b) & 0xff));
        }

        public unsafe void Run(
            RemoteHooking.IContext context,
            string channelName)
        {
            _server.IsInstalled(RemoteHooking.GetCurrentProcessId());

            var d3ddevPattern = "48 89 85 ? ? ? ? 48 8D 05 ? ? ? ?";
            var d3ddevAddress = *(IntPtr*)PatternScanner.FindPattern(d3ddevPattern, 10, 14);
            _server.ReportMessage($"[+] D3DDevice Found: 0x{d3ddevAddress.ToInt64():X16}");

            D3DDevice = new Device(d3ddevAddress);

            endScene_Address = PatternScanner.GetVTableFuncAddress(d3ddevAddress, 0x2a);
            _server.ReportMessage($"[+] EndScene Found: 0x{endScene_Address.ToInt64():X16}");

            var endSceneHook = LocalHook.Create(
                endScene_Address,
                new EndScene_Delegate(EndScene_Hook),
                this);

            endSceneHook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });

            var getCustomTextPattern = "E8 ? ? ? ? 48 89 85 ? ? ? ? 8B 45 04 89 44 24 30 C7 44 24 ? ? ? ? ? 48 8D 45 28 48 89 44 24 ? 41 B9 ? ? ? ? 4C 8B 85 ? ? ? ? 33 D2 48 8B 0D ? ? ? ? FF 95 ? ? ? ? 48 8B 05 ? ? ? ?";
            var address = PatternScanner.FindPattern(getCustomTextPattern, 1, 5);

            getCustomText_Address = PatternScanner.GetAddressFromBytecode(address, 1, 5);

            _server.ReportMessage($"[+] GetCustomText Found: 0x{getCustomText_Address.ToInt64():X16}");

            var getCustomTextHook = LocalHook.Create(
                getCustomText_Address,
                new GetCustomText_Delegate(GetCustomText_Hook),
                this);
            
            getCustomTextHook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });

            _server.ReportMessage("hooks installed");

            v_buffer = new VertexBuffer(D3DDevice, 3 * sizeof(CustomVertex), Usage.None, VertexFormat.PositionRhw | VertexFormat.Diffuse, Pool.Managed);

            v_buffer.Lock(0, 0, LockFlags.None).WriteRange(new[]
            {
                new CustomVertex { x = 400.0f, y = 62.5f, z = 0.5f, rhw = 1.0f, color = D3DCOLOR_XRGB(0, 0, 255) },
                new CustomVertex { x = 650.0f, y = 500.0f, z = 0.5f, rhw = 1.0f, color = D3DCOLOR_XRGB(0, 255, 0) },
                new CustomVertex { x = 150.0f, y = 500.0f, z = 0.5f, rhw = 1.0f, color = D3DCOLOR_XRGB(255, 0, 0) }
            });
            v_buffer.Unlock();

            RemoteHooking.WakeUpProcess();

            try
            {
                while (true)
                {
                    Thread.Sleep(500);

                    string[] queued = null;

                    lock (_messageQueue)
                    {
                        queued = _messageQueue.ToArray();
                        _messageQueue.Clear();
                    }

                    if (queued != null && queued.Length > 0)
                    {
                        _server.ReportMessages(queued);
                    }
                    else
                    {
                        _server.Ping();
                    }
                }
            }
            catch
            {

            }

            endSceneHook.Dispose();

            LocalHook.Release();
        }

        VertexBuffer v_buffer = null;

        unsafe int EndScene_Hook(IntPtr device)
        {
            try
            {
                D3DDevice.VertexFormat = VertexFormat.PositionRhw | VertexFormat.Diffuse;

                D3DDevice.SetStreamSource(0, v_buffer, 0, sizeof(CustomVertex));

                D3DDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 1);
            } catch (Exception e) { _server.ReportException(e); }
            

            return Marshal.GetDelegateForFunctionPointer<EndScene_Delegate>(endScene_Address)(device);
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall,
                    SetLastError = true)]
        delegate int EndScene_Delegate(
                    IntPtr device);

        unsafe IntPtr GetCustomText_Hook()
        {
            var address = Marshal.GetDelegateForFunctionPointer<GetCustomText_Delegate>(getCustomText_Address)();
            var str = Marshal.PtrToStringAnsi(address);

            _server.ReportMessage("[~] GetCustomText Executed: 0x" + address.ToString("X16") + " - " + str);

            return Marshal.StringToCoTaskMemAnsi(str + " hooked");
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall,
                    SetLastError = true)]
        delegate IntPtr GetCustomText_Delegate();
    }
}
