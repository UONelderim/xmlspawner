using System;

namespace Server.Engines.XmlSpawner2
{
	public class XmlFactionEquip : XmlAttachment
	{
		private int m_DataValue; // default data
		private string m_GroupName;

		private XmlMobFactions.GroupTypes m_GroupType = XmlMobFactions.GroupTypes.End_Unused;

		// a serial constructor is REQUIRED
		public XmlFactionEquip(ASerial serial)
			: base(serial)
		{
		}

		[Attachable]
		public XmlFactionEquip(string factiontype, int minvalue)
		{
			MinValue = minvalue;
			FactionType = factiontype;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int MinValue
		{
			get => m_DataValue;
			set => m_DataValue = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string FactionType
		{
			get => m_GroupName;
			set
			{
				m_GroupName = value;
				try
				{
					m_GroupType =
						(XmlMobFactions.GroupTypes)Enum.Parse(typeof(XmlMobFactions.GroupTypes), m_GroupName, true);
				}
				catch
				{
				}
			}
		}

		// These are the various ways in which the message attachment can be constructed.
		// These can be called via the [addatt interface, via scripts, via the spawner ATTACH keyword.
		// Other overloads could be defined to handle other types of arguments
		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
			// version 0
			writer.Write(m_DataValue);
			writer.Write(m_GroupName);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
			// version 0
			m_DataValue = reader.ReadInt();
			FactionType = reader.ReadString();
		}

		public override bool CanEquip(Mobile from)
		{
			// check to see if the owners faction is sufficient
			var list = XmlAttach.FindAttachments(XmlAttach.MobileAttachments, from, typeof(XmlMobFactions), "Standard");
			if (list != null && list.Count > 0)
			{
				var faclevel = 0;

				var x = list[0] as XmlMobFactions;

				if (m_GroupType != XmlMobFactions.GroupTypes.End_Unused) faclevel = x.GetFactionLevel(m_GroupType);

				if (faclevel < MinValue && AttachedTo is Item && AttachedTo != null)
				{
					var item = AttachedTo as Item;
					var name = item.Name;
					if (name == null) name = item.ItemData.Name;
					from.SendMessage("Cannot equip {2}. Need {0} {1} faction.", MinValue, FactionType, name);
					return false;
				}
			}

			return true;
		}

		public override string OnIdentify(Mobile from)
		{
			return String.Format("Requires {0} {1} faction to equip", MinValue, FactionType);
		}
	}
}
