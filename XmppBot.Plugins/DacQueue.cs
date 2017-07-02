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
        private readonly Dictionary<string, List<string>> _roomQueues = new Dictionary<string, List<string>>();
        private DateTime _acquireTime;

        public override string EvaluateEx(ParsedLine line)
        {
            if (!line.IsCommand) return string.Empty;

            switch (line.Command.ToLower())
            {
                case "dibs":
                case "+":
                    return RequestDac(line.User.Mention, line.Room);

                case "release":
                case "cancel":
                case "-":
                    return RescindDac(line.User.Mention, line.Room);

                case "steal":
                case "$":
                    return StealDac(line.User.Mention, line.Room);

                case "status":
                case "?":
                    return DacStatus(line.Room);

                default:
                    return DacHelp();
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

                if (hadDAC)
                {
                    if (q.Count > 0)
                    {
                        _acquireTime = DateTime.Now;
                        return $"{user} releases the DAC... @{q[0]} the DAC is yours (gladdrive)";
                    }

                    return $"{user} releases the DAC. The DAC is currently free (gladdrive)";
                }

                return $"{user} rescinds dibs on the DAC";
            }

            return $"{user} is not queued for the DAC";
        }

        private string StealDac(string user, string room)
        {
            var q = GetQueue(room);

            if (q.Count == 0)
            {
                return RequestDac(user, room);
            }

            var owner = q[0];

            if (user == owner)
            {
                return $"{user} inexplicably attempts to steal the DAC from {user}";
            }

            q.Remove(user);
            q.Remove(owner);
            q.Insert(0, user);

            if (q.Count > 1)
            {
                return $"{user} stole the DAC from @{owner} and jumped the line in front of @{q[1]}! srsly? (saddrive)";
            }

            return $"{user} stole the DAC from @{owner}! (swiper)";
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

            sb.AppendLine("DACbot at your service! I know the following commands:");
            sb.AppendLine("!dibs (or !+) : call dibs on the DAC");
            sb.AppendLine("!release (or !-) : give up the DAC or rescind a dibs");
            sb.AppendLine("!steal (or !$) : take the DAC from the current owner");
            sb.AppendLine("!status (or !?) : get the current queue status");
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
