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
        public override string EvaluateEx(ParsedLine line)
        {
            if (!line.IsCommand) return string.Empty;

            switch (line.Command.ToLower())
            {
                case "+":
                    return $"{line.User.Mention} calls (dibs) on the DAC";

                case "-":
                    return $"{line.User.Mention} rescinds the DAC";

                case "?":
                    return $"DAC queue status TBD (dmzie)";

                default:
                    return null;
            }
        }

        public override string Name
        {
            get { return "DAC queue"; }
        }
    }
}
