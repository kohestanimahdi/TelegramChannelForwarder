using System.Collections.Generic;

namespace TelegramForwarder.Services
{
    public class TelegramSetting
    {
        public string ApiHash { get; set; }
        public int ApiId { get; set; }
        public string ForwadTo { get; set; }
        public int DelayPerMessage { get; set; }
        public int DelayPerRound { get; set; }

        public List<string> ForwardFrom { get; set; }
        public List<int> ForwardFromIds { get; set; }
    }
}
