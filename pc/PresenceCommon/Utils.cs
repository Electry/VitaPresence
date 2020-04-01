using DiscordRPC;
using PresenceCommon.Types;

namespace PresenceCommon
{
    public static class Utils
    {
        public static RichPresence CreateDiscordPresence(Title title, Timestamps time, string state = "")
        {
            RichPresence presence = new RichPresence()
            {
                State = state
            };

            Assets assets = new Assets {};

            if (title.Index == 0)
            {
                assets.LargeImageText = "LiveArea";
                //assets.LargeImageKey = $"0{0x0100000000001000:x}";
                presence.Details = "In the LiveArea";
            }
            else
            {
                assets.LargeImageText = title.TitleName;
                //assets.LargeImageKey = $"0{title.TitleID:x}";
                presence.Details = $"{title.TitleName}";
            }
            presence.Assets = assets;
            presence.Timestamps = time;

            return presence;
        }
    }
}
