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
        private DateTime _acquireTime;

        public override string EvaluateEx(ParsedLine line)
        {
            if (!line.IsCommand) return string.Empty;

            switch (line.Command.ToLower())
            {
                case "+":
                    return RequestDac(line.User.Mention, line.Room);

                case "-":
                    return RescindDac(line.User.Mention, line.Room);

                case "+!":
                    return StealDac(line.User.Mention, line.Room);

                case "?":
                    return DacStatus(line.Room);

                case "help":
                    return DacHelp();

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
                return q[0] == user 
                    ? $"{user} already has the DAC" 
                    : $"{user} is already queued for the DAC";
            }
            
            q.Add(user);

            if (q.Count == 1)
            {
                _acquireTime = DateTime.Now;
                return $"{user} the DAC is yours!";
            }

            return $"{user} calls (dibs) on the DAC after {q[q.Count-2]}";
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
                    _acquireTime = DateTime.Now;
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

        private string DacStatus(string room)
        {
            var q = GetQueue(room);

            if (q.Count == 0)
            {
                return "The DAC queue is empty";
            }

            var sb = new StringBuilder();
            var duration = DateTime.Now - _acquireTime;

            if (duration > TimeSpan.FromMinutes(10))
            {
                sb.Append($"{q[0]} has been hogging the DAC for {duration}");
            }
            else
            {
                sb.Append($"{q[0]} has had the DAC for {duration}");
            }

            for (int i = 1; i < q.Count; i++)
            {
                sb.AppendLine();
                sb.Append($"... followed by {q[i]}");
            }

            return sb.ToString();
        }

        private string DacHelp()
        {
            var sb = new StringBuilder();

            sb.AppendLine("I'm DACbot! I know the following commands:");
            sb.AppendLine("!+ : call dibs on the DAC");
            sb.AppendLine("!- : give up the DAC or rescind a dibs");
            sb.AppendLine("!? : get the current queue status");
            sb.AppendLine("!help : this message");

            return sb.ToString();
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
