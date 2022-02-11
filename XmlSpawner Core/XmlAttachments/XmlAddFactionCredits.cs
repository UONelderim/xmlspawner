using System;
using Server.Mobiles;

namespace Server.Engines.XmlSpawner2
{
	public class XmlAddFactionCredits : XmlAttachment
	{
		private int m_DataValue; // default data

		// a serial constructor is REQUIRED
		public XmlAddFactionCredits(ASerial serial)
			: base(serial)
		{
		}

		[Attachable]
		public XmlAddFactionCredits(int value)
		{
			Value = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int Value
		{
			get => m_DataValue;
			set => m_DataValue = value;
		}

		// These are the various ways in which the message attachment can be constructed.  
		// These can be called via the [addatt interface, via scripts, via the spawner ATTACH keyword.
		// Other overloads could be defined to handle other types of arguments
		public override bool HandlesOnKilled => true;

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
			// version 0
			writer.Write(m_DataValue);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
			// version 0
			m_DataValue = reader.ReadInt();
		}

		public override void OnAttach()
		{
			base.OnAttach();

			// apply the mod
			if (AttachedTo is PlayerMobile)
			{
				// for players just add it immediately
				var x = (XmlMobFactions)XmlAttach.FindAttachment(AttachedTo, typeof(XmlMobFactions));

				if (x != null)
				{
					x.Credits += Value;
					((Mobile)AttachedTo).SendMessage("Receive {0}", OnIdentify((Mobile)AttachedTo));
				}

				// and then remove the attachment
				Delete();
			}
			else if (AttachedTo is Item)
				// dont allow item attachments
				Delete();
		}

		public override void OnKilled(Mobile killed, Mobile killer)
		{
			base.OnKill(killed, killer);

			if (killer == null)
				return;

			// for players just add it immediately
			var x = (XmlMobFactions)XmlAttach.FindAttachment(AttachedTo, typeof(XmlMobFactions));

			if (x != null)
			{
				x.Credits += Value;
				killer.SendMessage("Receive {0}", OnIdentify(killer));
			}
		}

		public override string OnIdentify(Mobile from)
		{
			return String.Format("{0} Mob Faction Credits", Value);
		}
	}
}
