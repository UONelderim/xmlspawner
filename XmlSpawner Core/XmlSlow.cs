using System;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Commands
{
	public class XmlSlow
	{
		public static void Initialize()
		{
			CommandSystem.Register("xmlslow", AccessLevel.GameMaster, OnCommand);
		}

		public static void OnCommand(CommandEventArgs e)
		{
			PlayerMobile pm = (PlayerMobile)e.Mobile;
			pm.CloseGump<XmlSlowGump>();
			pm.SendGump(new XmlSlowGump());
		}
	}

	public class XmlSlowGump : Gump
	{
		public XmlSlowGump() : base(10,10)
		{
			AddPage(0);
			AddBackground(0, 0, 330, 120, 5054);
			AddLabel(10,10, 0, "XmlSpawner Slow Spawners");
			AddLabel(10, 40, 0, "Enabled");
			AddCheck(70, 40, 0xD2, 0xD3, XmlSpawner.SpawnerTimer.LogSlow, 1);
			AddLabel(10, 70, 0, "Delay ms");
			AddBackground(70, 60, 80, 40, 0xDAC);
			AddTextEntry(90, 70, 50, 20, 0, 2, XmlSpawner.SpawnerTimer.SlowThreshold.TotalMilliseconds.ToString());
			AddButton(200, 70, 0xEF, 0xF0, 1, GumpButtonType.Reply, 0);
		}

		public override void OnResponse(NetState sender, RelayInfo info)
		{
			if (info.ButtonID == 1)
			{
				if (info.Switches.Length > 0)
				{
					XmlSpawner.SpawnerTimer.LogSlow = true;
					XmlSpawner.SpawnerTimer.LogTarget = sender.Mobile;
					sender.Mobile.SendMessage(62, "Slow spawners logging enabled");
				}
				else
				{
					XmlSpawner.SpawnerTimer.LogSlow = false;
					XmlSpawner.SpawnerTimer.LogTarget = null;
					sender.Mobile.SendMessage(32, "Slow spawners logging disabled");
				}

				Int32.TryParse(info.TextEntries[0].Text, out var ms);
				{
					XmlSpawner.SpawnerTimer.SlowThreshold = TimeSpan.FromMilliseconds(ms);
				}
			}
			sender.Mobile.SendGump(new XmlSlowGump());
		}
	}
}
