using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using log4net;
using XmppBot.Common;

namespace XmppBot.Plugins
{
    public class Dibs
    {
        public string User { get; }
        public DateTime Time { get; private set; }
        public TimeSpan Duration => DateTime.Now - Time;
        public bool Pending { get; set; }

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
            string proxy = null;

            if (line.Args.Length == 1)
            {
                var other = line.Args[0];

                if (other[0] == '@')
                {
                    proxy = user;
                    user = other.Substring(1);
                }
            }

            log.Debug($"User = {user}");

            switch (line.Command.ToLower())
            {
                case "dibs":
                case "+":
                    return RequestBaton(proxy, user, line.Room);

                case "release":
                case "cancel":
                case "-":
                    return RescindBaton(proxy, user, line.Room);

                case "redibs":
                case "-+":
                    return RejoinQueue(proxy, user, line.Room);

                case "steal":
                case "$":
                    return StealBaton(proxy, user, line.Room);

                case "lock":
                case "@+":
                    return LockQueue(proxy, user, line.Room);

                case "unlock":
                case "@-":
                    return UnlockQueue(proxy, user, line.Room);

                case "status":
                case "?":
                    return QueueStatus(line.Room);

                default:
                    return QueueHelp();
            }
        }

        public override string Name => "Queue";

        private string RequestBaton(string proxy, string user, string room)
        {
            var locked = GetLock(room);

            if (locked)
            {
                return QueueStatus(room);
            }

            var q = GetQueue(room);
            var dibs = q.Find(d => d.User == user);

            if (dibs != null)
            {
                if (q[0].User == user)
                {
                    if (dibs.Pending)
                    {
                        dibs.Pending = false;
                        return $"{user} claims the {_batonName}";
                    }

                    return $"{user} already has the {_batonName} (pokerface)";
                }

                return $"{user} is already queued for the {_batonName}";
            }
            
            q.Add(new Dibs(user));

            if (q.Count == 1)
            {
                var emoticon = RandomPositiveEmoticon();
                return proxy == null
                    ? $"{user} the {_batonName} is yours! {emoticon}"
                    : $"{proxy} hands the {_batonName} to @{user}! {emoticon}";
            }

            return proxy == null
                ? $"{user} calls (dibs) on the {_batonName} after {q[q.Count - 2].User}... fyi @{q[0].User}"
                : $"{proxy} calls (dibs) on the {_batonName} for @{user} after {q[q.Count-2].User}... fyi @{q[0].User}";
        }

        private string RescindBaton(string proxy, string user, string room)
        {
            var locked = GetLock(room);

            if (locked)
            {
                return QueueStatus(room);
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

                        sb.AppendLine(proxy == null
                            ? $"{user} releases the {_batonName} after {dibs.Duration}."
                            : $"{proxy} releases the {_batonName} for @{user} after {dibs.Duration}.");

                        if (q.Count > 1)
                        { 
                            sb.AppendLine($"@{q[0].User} the {_batonName} is yours if you call dibs again within 60 seconds! {emoticon}");
                            CheckForPending(room);
                        }
                        else
                        {
                            sb.AppendLine($"@{q[0].User} the {_batonName} is yours! {emoticon}");
                        }

                        return sb.ToString();
                    }

                    return proxy == null
                        ? $"{user} releases the {_batonName} after {dibs.Duration}."
                        : $"{proxy} releases the {_batonName} for @{user} after {dibs.Duration}.";
                }

                return proxy == null
                    ? $"{user} rescinds dibs on the {_batonName}"
                    : $"{proxy} rescinds dibs for @{user} on the {_batonName}";
            }

            return $"{user} is not queued for the {_batonName}";
        }

        private void CheckForPending(string room)
        {
            Timer timer = null;
            var q = GetQueue(room);
            var dibs = q[0];

            dibs.Pending = true;

            timer = new Timer((obj) =>
            {
                //log.Debug($"CheckForPending {dibs.Pending} on {dibs.User} after {dibs.Duration} in {room}");

                if (dibs.Pending)
                {
                    if (q.Count > 1)
                    {
                        q.Add(dibs);
                        var msg = RescindBaton(null, dibs.User, room);
                        SendMessage(msg, room, BotMessageType.groupchat);
                    }
                    else
                    {
                        SendMessage($"@{dibs.User} the {_batonName} is yours!", room, BotMessageType.groupchat);
                    }
                }

                timer.Dispose();
            }, null, 60000, Timeout.Infinite);
        }

        private string RejoinQueue(string proxy, string user, string room)
        {
            var locked = GetLock(room);

            if (locked)
            {
                return QueueStatus(room);
            }

            var q = GetQueue(room);
            var dibs = q.Find(d => d.User == user);

            if (dibs != null)
            {
                if (q.Count > 1)
                {
                    var sb = new StringBuilder();

                    sb.AppendLine(RescindBaton(proxy, user, room));
                    sb.AppendLine(RequestBaton(proxy, user, room));

                    return sb.ToString();
                }

                return $"{user} is giving up the {_batonName} for {user} (pokerface)";
            }

            return RequestBaton(proxy, user, room);
        }

        private string StealBaton(string proxy, string user, string room)
        {
            var locked = GetLock(room);

            if (locked)
            {
                return QueueStatus(room);
            }

            var q = GetQueue(room);

            if (q.Count == 0)
            {
                return RequestBaton(proxy, user, room);
            }

            var owner = q[0];

            if (user == owner.User)
            {
                return proxy == null
                    ? $"{user} inexplicably attempts to steal the {_batonName} from {user} (derp)"
                    : $"{proxy} inexplicably attempts to steal the {_batonName} from {user} for {user} (derp)";
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
                return proxy == null
                    ? $"{user} stole the {_batonName} from @{owner.User} and jumped the line in front of @{q[1].User}! srsly? {emoticon}"
                    : $"{proxy} stole the {_batonName} from @{owner.User} for @{user} ahead of @{q[1].User}! srsly? {emoticon}";
            }

            return proxy == null 
                ? $"{user} stole the {_batonName} from @{owner.User}! {emoticon}"
                : $"{proxy} stole the {_batonName} from @{owner.User} for @{user}! {emoticon}";
        }

        private string LockQueue(string proxy, string user, string room)
        {
            var locked = GetLock(room);

            if (locked)
            {
                return QueueStatus(room);
            }

            var q = GetQueue(room);
            
            q.Clear();
            RequestBaton(proxy, user, room);
            SetLock(room, true);

            var emoticon = RandomNegativeEmoticon();
            return proxy == null
                ? $"@all {user} has locked the {_batonName}! {emoticon}"
                : $"@all {proxy} has locked the {_batonName} for @{user}! {emoticon}";
        }

        private string UnlockQueue(string proxy, string user, string room)
        {
            var locked = GetLock(room);
            var q = GetQueue(room);

            if (locked)
            {
                var owner = q[0];
                q.Clear();
                SetLock(room, false);

                var emoticon = RandomPositiveEmoticon();

                return proxy == null
                    ? $"@all {user} has unlocked the {_batonName} after {owner.Duration}! {emoticon}"
                    : $"@all {proxy} has unlocked the {_batonName} after {owner.Duration}! {emoticon}";
            }

            return $"{_batonName} is not locked. (pokerface)";
        }

        private string QueueStatus(string room)
        {
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
            sb.AppendLine($"!dibs @mention: call dibs for @mention");
            sb.AppendLine($"!release (or !-) : give up the {_batonName} or rescind a dibs");
            sb.AppendLine($"!release @mention: rescind a dibs for @mention");
            sb.AppendLine($"!redibs (or !-+) : combined release and dibs as a courtesy");
            sb.AppendLine($"!steal (or !$) : take the {_batonName} from the current owner");
            sb.AppendLine($"!steal @mention: steal the {_batonName} for @mention");
            sb.AppendLine($"!lock (or !@+) : lock the {_batonName} preventing anyone else from calling dibs");
            sb.AppendLine($"!lock @mention: lock the {_batonName} for @mention");
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

        private string RandomEmoticon(string[] emoticons, double chance)
        {
            var rng = new Random();

            if (rng.NextDouble() < chance)
            {
                var idx = rng.Next(emoticons.Length - 1);
                return $"({emoticons[idx]})";
            }

            return "";
        }

        private string RandomNegativeEmoticon()
        {
            return RandomEmoticon(_negativeEmoticons, 0.8);
        }

        private string RandomPositiveEmoticon()
        {
            return RandomEmoticon(_positiveEmoticons, 0.2);
        }
    }
}
