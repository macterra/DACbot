﻿using agsXMPP;
using agsXMPP.protocol.client;
using agsXMPP.protocol.iq.roster;
using agsXMPP.protocol.x.muc;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace XmppBot.Common
{
    public class Bot
    {
        #region log4net
        private static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        private const int MaxRosterSize = 100;

        private static AggregateCatalog _catalog = null;
        private static DirectoryCatalog _directoryCatalog = null;
        private static XmppClientConnection _client = null;


        private XmppBotConfig _config;

        public Bot(XmppBotConfig config)
        {
            _config = config;
        }

        public void Stop()
        {
        }

        public void Start()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (o, args) =>
            {
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                return loadedAssemblies.FirstOrDefault(asm => asm.FullName == args.Name);
            };

            // use our running app and a directory for our MEF catalog
            _catalog = new AggregateCatalog();
            _catalog.Catalogs.Add(new AssemblyCatalog(Assembly.GetEntryAssembly()));
            
            // create a MEF directory catalog for the plugins directory
            string pluginsDirectory = Environment.CurrentDirectory + "\\plugins\\";
            if (!Directory.Exists(pluginsDirectory))
            {
                Directory.CreateDirectory(pluginsDirectory);
            }
            _directoryCatalog = new DirectoryCatalog(Environment.CurrentDirectory + "\\plugins\\");
            _catalog.Catalogs.Add(_directoryCatalog);


            _catalog.Changed += new EventHandler<ComposablePartCatalogChangeEventArgs>(_catalog_Changed);
            var pluginList = LoadPlugins();

            log.Info(pluginList);

            _client = new XmppClientConnection(_config.Server);

            //_client.ConnectServer = "talk.google.com"; //necessary if connecting to Google Talk
            _client.AutoResolveConnectServer = false;

            _client.OnLogin += new ObjectHandler(xmpp_OnLogin);
            _client.OnMessage += new MessageHandler(xmpp_OnMessage);
            _client.OnError += new ErrorHandler(_client_OnError);

            log.Info("Connecting...");
            _client.Resource = _config.Resource;
            _client.Open(_config.User, _config.Password);
            log.Info("Connected.");

            _client.OnRosterStart += new ObjectHandler(_client_OnRosterStart);
            _client.OnRosterItem += new XmppClientConnection.RosterHandler(_client_OnRosterItem);
        }

        #region Xmpp Events

        void xmpp_OnLogin(object sender)
        {
            MucManager mucManager = new MucManager(_client);

            string[] rooms = _config.Rooms.Split(',');

            foreach (string room in rooms)
            {
                Jid jid = new Jid(room + "@" + _config.ConferenceServer);
                mucManager.JoinRoom(jid, _config.RoomNick);
                log.InfoFormat($"Joining room {room} as @{_config.RoomNick}");
            }
        }

        private void xmpp_OnMessage(object sender, Message msg)
        {
            if (!string.IsNullOrEmpty(msg.Body))
            {
                log.InfoFormat("Message : {0} - from {1}", msg.Body, msg.From);

                IChatUser user = null;

                if (msg.Type == MessageType.groupchat)
                {
                    //msg.From.Resource = User Room Name
                    //msg.From.Bare = Room 'email'
                    //msg.From.User = Room id

                    user = _roster.Values.FirstOrDefault(u => u.Name == msg.From.Resource);
                }
                else
                {
                    _roster.TryGetValue(msg.From.Bare, out user);
                }

                // we can't find a user or this is the bot talking
                if (null == user || _config.RoomNick == user.Name)
                    return;

                ParsedLine line = new ParsedLine(msg.From.Bare, msg.Body.Trim(), msg.From.User, user, (BotMessageType) msg.Type);

                switch (line.Command)
                {
                    case "xhelp":
                        var helpText = new StringBuilder();
                        var plist = Plugins.ToList();
                        plist.Sort((c1, c2) => c1.Name.CompareTo(c2.Name));

                        foreach (var p in plist)
                        {
                            var helpLine = p.Help(line);
                            if (!String.IsNullOrWhiteSpace(helpLine))
                            {
                                helpText.AppendLine(p.Help(line));
                            }
                        }

                        helpText.AppendLine("-----------------------");
                        helpText.AppendLine("En/Dis-able a plugin: !disable|!enable <pluginname>");
                        helpText.AppendLine("List plugin names: !list");
                        SendMessage(msg.From, helpText.ToString(), msg.Type);

                        break;
                    case "close":
                        SendMessage(msg.From, "I'm a quitter...", msg.Type);
                        Environment.Exit(1);
                        return;

                    case "reload":
                        SendMessage(msg.From, LoadPlugins(), msg.Type);
                        break;

                    default:
                        Task.Factory.StartNew(() =>
                                              Parallel.ForEach(Plugins,
                                                  plugin => SendMessage(msg.From, plugin.Evaluate(line), msg.Type)
                                                  ));
                        break;
                }
            }
        }


        static void _client_OnError(object sender, Exception ex)
        {
            log.Error("Exception: " + ex);
        }

        static void _catalog_Changed(object sender, ComposablePartCatalogChangeEventArgs e)
        {
            if (null != _directoryCatalog)
                _directoryCatalog.Refresh();
        }

        #endregion

        #region Roster Management

        private static Dictionary<string, IChatUser> _roster = new Dictionary<string, IChatUser>(MaxRosterSize);

        static void _client_OnRosterStart(object sender)
        {
            _roster = new Dictionary<string, IChatUser>(MaxRosterSize);
        }

        static void _client_OnRosterItem(object sender, RosterItem item)
        {
            if (!_roster.ContainsKey(item.Jid.User))
            {
                _addRoster(new ChatUser(item));
            }
        }

        private static void _addRoster(IChatUser user)
        {
            if (!_roster.ContainsKey(user.Bare))
                _roster.Add(user.Bare, user);
        }

        #endregion

        #region Message Senders

        private void SendMessage(string text, string jid, BotMessageType messageType)
        {
            //msg.from or jid
            if (!jid.Contains("@"))
                jid = jid + "@" + _config.Server;

            SendMessage(new Jid(jid), text, (MessageType)messageType);
        }

        private void SendMessage(Jid to, string text, MessageType type)
        {
            if (text == null) return;

            _client.Send(new Message(to, type, text));
        }

        #endregion

        #region Plugin Management

        [ImportMany(AllowRecomposition = true)]
        public static IEnumerable<IXmppBotPlugin> Plugins { get; set; }

        private string LoadPlugins()
        {
            var container = new CompositionContainer(_catalog);
            Plugins = container.GetExportedValues<IXmppBotPlugin>();

            foreach (IXmppBotPlugin plugin in Plugins)
            {
                // wire up message send event
                plugin.SentMessage += new PluginMessageHandler(SendMessage);

                // wire up plugin init
                plugin.Initialize();
            }


            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Loaded the following plugins");
            foreach (var part in _catalog.Parts)
            {
                builder.AppendFormat("\t{0}\n", part.ToString());
            }

            return builder.ToString();
        }

        #endregion

    }
}