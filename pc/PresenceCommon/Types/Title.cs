using System.Runtime.InteropServices;
using System.Text;

namespace PresenceCommon.Types
{
    public class Title
    {
        public uint Magic { get; }
        public int Index { get; }
        public string TitleID { get; }
        public string TitleName { get; }

        [StructLayout(LayoutKind.Sequential, Size = 146)]
        private struct TitlePacket
        {
            [MarshalAs(UnmanagedType.U4)]
            public uint magic;
            [MarshalAs(UnmanagedType.U4)]
            public int index;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public byte[] titleid;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] title;
        }

        public Title(byte[] bytes)
        {
            TitlePacket title = DataHandler.ByteArrayToStructure<TitlePacket>(bytes);
            Magic = title.magic;
            Index = title.index;
            TitleID = Encoding.UTF8.GetString(title.titleid, 0, title.titleid.Length).Split('\0')[0];
            TitleName = Encoding.UTF8.GetString(title.title, 0, title.title.Length).Split('\0')[0];
        }
    }
}
