using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

using XmppBot.Common;

namespace XmppBot.Plugins
{
    public class Dibs
    {
        public string User { get; }
        public DateTime Time { get; private set; }
        public TimeSpan Duration => DateTime.Now - Time;

        public Dibs(string user)
        {
            User = user;
            Time = DateTime.Now;
        }

        public void Reset()
        {
            Time = DateTime.Now;
        }
    }

    [Export(typeof(IXmppBotPlugin))]
    public class DacQueue : XmppBotPluginBase, IXmppBotPlugin
    {
        private readonly Dictionary<string, List<Dibs>> _roomQueues = new Dictionary<string, List<Dibs>>();

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
            var dibs = q.Find(d => d.User == user);

            if (dibs != null)
            {
                return q[0].User == user 
                    ? $"{user} already has the DAC" 
                    : $"{user} is already queued for the DAC";
            }
            
            q.Add(new Dibs(user));

            if (q.Count == 1)
            {
                return $"{user} the DAC is yours!";
            }

            return $"{user} calls (dibs) on the DAC after {q[q.Count-2].User}";
        }

        private string RescindDac(string user, string room)
        {
            var q = GetQueue(room);
            var dibs = q.Find(d => d.User == user);

            if (dibs != null)
            {
                bool hadDac = q[0].User == user;

                q.Remove(dibs);

                if (hadDac)
                {
                    if (q.Count > 0)
                    {
                        q[0].Reset();
                        return $"{user} releases the DAC... @{q[0].User} the DAC is yours (gladdrive)";
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

            if (user == owner.User)
            {
                return $"{user} inexplicably attempts to steal the DAC from {user}";
            }

            q.Remove(owner);

            var dibs = q.Find(d => d.User == user);

            if (dibs != null)
            {
                q.Remove(dibs);
                dibs.Reset();
                q.Insert(0, dibs);
            }
            else
            {
                q.Insert(0, new Dibs(user));
            }

            if (q.Count > 1)
            {
                return $"{user} stole the DAC from @{owner.User} and jumped the line in front of @{q[1].User}! srsly? (saddrive)";
            }

            return $"{user} stole the DAC from @{owner.User}! (swiper)";
        }

        private string DacStatus(string room)
        {
            var q = GetQueue(room);

            if (q.Count == 0)
            {
                return "The DAC queue is empty";
            }

            var sb = new StringBuilder();
            var dibs = q[0];
            var duration = dibs.Duration;

            if (duration > TimeSpan.FromMinutes(10))
            {
                sb.Append($"{dibs.User} has been hogging the DAC for {duration}");
            }
            else
            {
                sb.Append($"{dibs.User} has had the DAC for {duration}");
            }

            for (int i = 1; i < q.Count; i++)
            {
                sb.AppendLine();
                sb.Append($"... followed by {q[i].User}, waiting for {q[i].Duration}");
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

        private List<Dibs> GetQueue(string room)
        {
            if (!_roomQueues.ContainsKey(room))
            {
                _roomQueues[room] = new List<Dibs>();
            }

            return _roomQueues[room];
        }
    }
}
