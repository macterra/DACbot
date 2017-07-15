using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using log4net;
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
    public class Queue : XmppBotPluginBase, IXmppBotPlugin
    {
        private static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Dictionary<string, List<Dibs>> _roomQueues = new Dictionary<string, List<Dibs>>();
        private string _botName;
        private string _batonName;

        public override void Initialize(XmppBotConfig config)
        {
            _botName = config.RoomNick;
            _batonName = config.BatonName;
        }

        private bool Mentions(string line)
        {
            return line.IndexOf(_botName, StringComparison.Ordinal) >= 0;
        }

        private string RespondToMention(ParsedLine line)
        {
            if (line.Raw.StartsWith("/me"))
            {
                return line.Raw.Replace(_botName, line.User.Mention);
            }

            return $"Hi @{line.User.Mention}";
        }

        public override string EvaluateEx(ParsedLine line)
        {
            // log.Debug($"Queue EvaluateEx {line.Raw}");

            if (Mentions(line.Raw))
            {
                return RespondToMention(line);
            }
            
            if (!line.IsCommand) return string.Empty;

            switch (line.Command.ToLower())
            {
                case "dibs":
                case "+":
                    return RequestBaton(line.User.Mention, line.Room);

                case "release":
                case "cancel":
                case "-":
                    return RescindBaton(line.User.Mention, line.Room);

                case "steal":
                case "$":
                    return StealBaton(line.User.Mention, line.Room);

                case "status":
                case "?":
                    return QueueStatus(line.Room);

                default:
                    return BatonHelp();
            }
        }

        public override string Name => "Queue";

        private string RequestBaton(string user, string room)
        {
            var q = GetQueue(room);
            var dibs = q.Find(d => d.User == user);

            if (dibs != null)
            {
                return q[0].User == user 
                    ? $"{user} already has the {_batonName} (pokerface)" 
                    : $"{user} is already queued for the {_batonName}";
            }
            
            q.Add(new Dibs(user));

            if (q.Count == 1)
            {
                return $"{user} the {_batonName} is yours!";
            }

            return $"{user} calls (dibs) on the {_batonName} after {q[q.Count-2].User}... fyi @{q[0].User}";
        }

        private string RescindBaton(string user, string room)
        {
            var q = GetQueue(room);
            var dibs = q.Find(d => d.User == user);

            if (dibs != null)
            {
                bool hadBaton = q[0].User == user;

                q.Remove(dibs);

                if (hadBaton)
                {
                    if (q.Count > 0)
                    {
                        q[0].Reset();

                        var sb = new StringBuilder();

                        sb.AppendLine($"{user} releases the {_batonName} after {dibs.Duration}.");
                        sb.AppendLine($"@{q[0].User} the {_batonName} is yours! (gladdrive)");

                        return sb.ToString();
                    }

                    return $"{user} releases the {_batonName} after {dibs.Duration}.";
                }

                return $"{user} rescinds dibs on the {_batonName}";
            }

            return $"{user} is not queued for the {_batonName}";
        }

        private string StealBaton(string user, string room)
        {
            var q = GetQueue(room);

            if (q.Count == 0)
            {
                return RequestBaton(user, room);
            }

            var owner = q[0];

            if (user == owner.User)
            {
                return $"{user} inexplicably attempts to steal the {_batonName} from {user} (derp)";
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

            var emoticon = RandomAngryEmoticon();

            if (q.Count > 1)
            {
                return $"{user} stole the {_batonName} from @{owner.User} and jumped the line in front of @{q[1].User}! srsly? {emoticon}";
            }

            return $"{user} stole the {_batonName} from @{owner.User}! {emoticon}";
        }

        private readonly string[] _angryEmoticons =
        {
            "(wat)",
            "(wtf)",
            "(orly)",
            "(huh)",
            "(lol)",
            "(haha)",
            "(rageguy)",
            "(mindblown)",
            "(swiper)",
            "(sadpanda)",
            "(grumpycat)",
            "(iseewhatyoudidthere)",
            "(cerealspit)",
            "(badass)"
        };

        private string RandomAngryEmoticon()
        {
            var rng = new Random();
            var idx = rng.Next(_angryEmoticons.Length - 1);
            return _angryEmoticons[idx];
        }

        private string QueueStatus(string room)
        {
            var q = GetQueue(room);

            if (q.Count == 0)
            {
                return $"The {_batonName} queue is empty";
            }

            var sb = new StringBuilder();
            var dibs = q[0];
            var duration = dibs.Duration;

            if (duration > TimeSpan.FromMinutes(10))
            {
                sb.Append($"{dibs.User} has been hogging the {_batonName} for {duration}");
            }
            else
            {
                sb.Append($"{dibs.User} has had the {_batonName} for {duration}");
            }

            for (int i = 1; i < q.Count; i++)
            {
                sb.AppendLine();
                sb.Append($"... followed by {q[i].User}, waiting for {q[i].Duration}");
            }

            return sb.ToString();
        }

        private string BatonHelp()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"{_botName} at your service! I know the following commands:");
            sb.AppendLine($"!dibs (or !+) : call dibs on the {_batonName}");
            sb.AppendLine($"!release (or !-) : give up the {_batonName} or rescind a dibs");
            sb.AppendLine($"!steal (or !$) : take the {_batonName} from the current owner");
            sb.AppendLine($"!status (or !?) : get the current queue status");
            sb.AppendLine($"!help : this message");

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
