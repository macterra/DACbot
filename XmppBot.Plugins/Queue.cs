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
        private readonly Dictionary<string, bool> _roomLocks = new Dictionary<string, bool>();
        private string _botName;
        private string _batonName;
        private string[] _positiveEmoticons;
        private string[] _negativeEmoticons;

        public override void Initialize(XmppBotConfig config)
        {
            _botName = config.RoomNick;
            _batonName = config.BatonName;

            var positiveEmoticons = config.PositiveEmoticons;
            var negativeEmoticons = config.NegativeEmoticons;

            _positiveEmoticons = positiveEmoticons.Split(',');
            _negativeEmoticons = negativeEmoticons.Split(',');

            log.Debug($"My name is {_botName}");
            log.Debug($"I manage the queue for the {_batonName}");
            log.Debug($"Positive emoticons: {positiveEmoticons}");
            log.Debug($"Negative emoticons: {negativeEmoticons}");
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

            var args = String.Join(",", line.Args);

            log.Debug($"Queue EvaluateEx Command={line.Command} Args={args}");

            var user = line.User.Mention;

            if (line.Args.Length == 1)
            {
                var other = line.Args[0];

                if (other[0] == '@')
                {
                    user = other.Substring(1);
                }
            }

            log.Debug($"User = {user}");

            switch (line.Command.ToLower())
            {
                case "dibs":
                case "+":
                    return RequestBaton(user, line);

                case "release":
                case "cancel":
                case "-":
                    return RescindBaton(user, line);

                case "redibs":
                case "-+":
                    return RejoinQueue(user, line);

                case "steal":
                case "$":
                    return StealBaton(user, line);

                case "status":
                case "?":
                    return QueueStatus(line);

                case "lock":
                case "@+":
                    return LockQueue(user, line);

                case "unlock":
                case "@-":
                    return UnlockQueue(user, line);

                default:
                    return QueueHelp();
            }
        }

        public override string Name => "Queue";

        private string RequestBaton(string user, ParsedLine line)
        {
            var proxy = line.User.Mention;
            var room = line.Room;

            var locked = GetLock(room);

            if (locked)
            {
                return QueueStatus(line);
            }

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
                var emoticon = RandomPositiveEmoticon();
                return $"{user} the {_batonName} is yours! {emoticon}";
            }

            return $"{user} calls (dibs) on the {_batonName} after {q[q.Count-2].User}... fyi @{q[0].User}";
        }

        private string RescindBaton(string user, ParsedLine line)
        {
            var proxy = line.User.Mention;
            var room = line.Room;
            var locked = GetLock(room);

            if (locked)
            {
                return QueueStatus(line);
            }

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
                        var emoticon = RandomPositiveEmoticon();

                        sb.AppendLine($"{user} releases the {_batonName} after {dibs.Duration}.");
                        sb.AppendLine($"@{q[0].User} the {_batonName} is yours! {emoticon}");

                        return sb.ToString();
                    }

                    return $"{user} releases the {_batonName} after {dibs.Duration}.";
                }

                return $"{user} rescinds dibs on the {_batonName}";
            }

            return $"{user} is not queued for the {_batonName}";
        }

        private string RejoinQueue(string user, ParsedLine line)
        {
            var proxy = line.User.Mention;
            var room = line.Room;
            var locked = GetLock(room);

            if (locked)
            {
                return QueueStatus(line);
            }

            var q = GetQueue(room);
            var dibs = q.Find(d => d.User == user);

            if (dibs != null)
            {
                if (q.Count > 1)
                {
                    var sb = new StringBuilder();

                    sb.AppendLine(RescindBaton(user, line));
                    sb.AppendLine(RequestBaton(user, line));

                    return sb.ToString();
                }

                return $"{user} is giving up the {_batonName} for {user} (pokerface)";
            }

            return RequestBaton(user, line);
        }

        private string StealBaton(string user, ParsedLine line)
        {
            var proxy = line.User.Mention;
            var room = line.Room;
            var locked = GetLock(room);

            if (locked)
            {
                return QueueStatus(line);
            }

            var q = GetQueue(room);

            if (q.Count == 0)
            {
                return RequestBaton(user, line);
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

            var emoticon = RandomNegativeEmoticon();

            if (q.Count > 1)
            {
                return $"{user} stole the {_batonName} from @{owner.User} and jumped the line in front of @{q[1].User}! srsly? {emoticon}";
            }

            return $"{user} stole the {_batonName} from @{owner.User}! {emoticon}";
        }

        private string LockQueue(string user, ParsedLine line)
        {
            var proxy = line.User.Mention;
            var room = line.Room;
            var locked = GetLock(room);

            if (locked)
            {
                return QueueStatus(line);
            }

            var q = GetQueue(room);
            
            q.Clear();
            RequestBaton(user, line);
            SetLock(room, true);

            var emoticon = RandomNegativeEmoticon();
            return $"@all {user} has locked {_batonName}! {emoticon}";
        }

        private string UnlockQueue(string user, ParsedLine line)
        {
            var proxy = line.User.Mention;
            var room = line.Room;
            var locked = GetLock(room);
            var q = GetQueue(room);

            if (locked)
            {
                q.Clear();
                SetLock(room, false);

                var emoticon = RandomPositiveEmoticon();
                return $"@all {user} has unlocked the {_batonName}! {emoticon}";
            }

            return $"{_batonName} is not locked. (pokerface)";
        }

        private string QueueStatus(ParsedLine line)
        {
            var proxy = line.User.Mention;
            var room = line.Room;
            var q = GetQueue(room);
            var locked = GetLock(room);

            if (q.Count == 0)
            {
                return $"The {_batonName} queue is empty";
            }

            if (locked)
            {
                var owner = q[0];
                return $"{owner.User} has had the {_batonName} locked for {owner.Duration}";
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

        private string QueueHelp()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"{_botName} at your service! I know the following commands:");
            sb.AppendLine($"!dibs (or !+) : call dibs on the {_batonName}");
            sb.AppendLine($"!release (or !-) : give up the {_batonName} or rescind a dibs");
            sb.AppendLine($"!redibs (or !-+) : combined release and dibs as a courtesy");
            sb.AppendLine($"!steal (or !$) : take the {_batonName} from the current owner");
            sb.AppendLine($"!lock (or !@+) : lock the {_batonName} preventing anyone else from calling dibs");
            sb.AppendLine($"!unlock (or !@-) : unlock the {_batonName}");
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

        private bool GetLock(string room)
        {
            if (!_roomLocks.ContainsKey(room))
            {
                _roomLocks[room] = false;
            }

            return _roomLocks[room];
        }

        private void SetLock(string room, bool isLocked)
        {
            _roomLocks[room] = isLocked;
        }

        private string RandomEmoticon(string[] emoticons)
        {
            var rng = new Random();
            var idx = rng.Next(emoticons.Length - 1);
            return $"({emoticons[idx]})";
        }

        private string RandomNegativeEmoticon()
        {
            return RandomEmoticon(_negativeEmoticons);
        }

        private string RandomPositiveEmoticon()
        {
            return RandomEmoticon(_positiveEmoticons);
        }
    }
}
