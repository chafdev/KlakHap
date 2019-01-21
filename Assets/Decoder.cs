using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Klak.Hap
{
    internal sealed class Decoder : IDisposable
    {
        #region Initialization/finalization

        public Decoder(StreamReader stream, int width, int height, int videoType)
        {
            _stream = stream;

            // Plugin initialization
            _plugin = KlakHap_CreateDecoder(width, height, videoType);
            _id = ++_instantiationCount;
            KlakHap_AssignDecoder(_id, _plugin);

            // By default, start from the first frame.
            _time = 0;

            // Decoder thread startup
            _resume = new AutoResetEvent(true);
            _thread = new Thread(DecoderThread);
            _thread.Start();
        }

        public void Dispose()
        {
            if (_thread != null)
            {
                _time = -1;
                _resume.Set();
                _thread.Join();
                _thread = null;
            }

            if (_plugin != IntPtr.Zero)
            {
                KlakHap_AssignDecoder(_id, IntPtr.Zero);
                KlakHap_DestroyDecoder(_plugin);
                _plugin = IntPtr.Zero;
            }
        }

        #endregion

        #region Public members

        public uint CallbackID { get { return _id; } }

        public void UpdateTime(float time)
        {
            _time = time;
            _resume.Set();
        }

        #endregion

        #region Private members

        static uint _instantiationCount;

        IntPtr _plugin;
        uint _id;

        Thread _thread;
        AutoResetEvent _resume;

        StreamReader _stream;
        float _time;

        #endregion

        #region Thread function

        void DecoderThread()
        {
            while (true)
            {
                _resume.WaitOne();

                if (_time < 0) break;

                var buffer = _stream.Advance(_time);
                if (buffer == null) continue;

                KlakHap_DecodeFrame(_plugin, buffer.PluginPointer);
            }
        }

        #endregion

        #region Native plugin entry points

        [DllImport("KlakHap")]
        internal static extern IntPtr KlakHap_CreateDecoder(int width, int height, int typeID);

        [DllImport("KlakHap")]
        internal static extern void KlakHap_DestroyDecoder(IntPtr decoder);

        [DllImport("KlakHap")]
        internal static extern void KlakHap_AssignDecoder(uint id, IntPtr decoder);

        [DllImport("KlakHap")]
        internal static extern void KlakHap_DecodeFrame(IntPtr decoder, IntPtr input);

        #endregion
    }
}
