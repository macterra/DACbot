using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

using XmppBot.Common;

namespace XmppBot.Plugins
{
    [Export(typeof(IXmppBotPlugin))]
    public class DacQueue : XmppBotPluginBase, IXmppBotPlugin
    {
        private Dictionary<string, List<string>> _roomQueues = new Dictionary<string, List<string>>();

        public override string EvaluateEx(ParsedLine line)
        {
            if (!line.IsCommand) return string.Empty;

            switch (line.Command.ToLower())
            {
                case "+":
                    return RequestDac(line.User.Mention, line.Room);

                case "-":
                    return RescindDac(line.User.Mention, line.Room);

                case "!":
                case "+!":
                    return StealDac(line.User.Mention, line.Room);

                case "?":
                    return $"DAC queue status TBD (dmzie) from {line.From}";

                default:
                    return $"unknown command {line.Command}";
            }
        }

        public override string Name => "DAC queue";

        private string RequestDac(string user, string room)
        {
            var q = GetQueue(room);

            if (q.Contains(user))
            {
                if (q[0] == user)
                {
                    return $"{user} already has the DAC";
                }

                return $"{user} is already queued for the DAC";
            }
            
            q.Add(user);

            return q.Count == 1 
                ? $"{user} the DAC is yours!" 
                : $"{user} calls (dibs) on the DAC after {q[q.Count-2]}";
        }

        private string RescindDac(string user, string room)
        {
            var q = GetQueue(room);

            if (q.Contains(user))
            {
                bool hadDAC = q[0] == user;

                q.Remove(user);

                if (hadDAC && q.Count > 0)
                {
                    return $"{user} rescinds the DAC... @{q[0]} the DAC is yours.";
                }

                return $"{user} rescinds the DAC";
            }

            return $"{user} is not queued for the DAC";
        }

        private string StealDac(string user, string room)
        {
            return $"{user} steals the DAC! in {room} (swiper)";
        }

        private List<string> GetQueue(string room)
        {
            if (!_roomQueues.ContainsKey(room))
            {
                _roomQueues[room] = new List<string>();
            }

            return _roomQueues[room];
        }
    }
}
