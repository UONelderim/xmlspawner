using System;
using Server.Mobiles;

namespace Server.Engines.XmlSpawner2
{
	public class XmlSpawnTime : XmlAttachment
	{
		private TimeSpan m_MinDelay = TimeSpan.MinValue;

		private TimeSpan m_MaxDelay = TimeSpan.MinValue;

		// a serial constructor is REQUIRED
		public XmlSpawnTime(ASerial serial)
			: base(serial)
		{
		}

		[Attachable]
		public XmlSpawnTime(double mindelay, double maxdelay)
		{
			MinDelay = TimeSpan.FromMinutes(mindelay);
			MaxDelay = TimeSpan.FromMinutes(maxdelay);
		}

		[Attachable]
		public XmlSpawnTime()
		{
			// min/maxdelay values will be taken from the spawner 
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan MinDelay
		{
			get
			{
				var spawner = MySpawner;

				// try to get the min/maxdelay based on spawner values if not specified on the attachment.
				if (spawner != null && m_MinDelay < TimeSpan.Zero) return spawner.MinDelay;
				return m_MinDelay;
			}
			set => m_MinDelay = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan MaxDelay
		{
			get
			{
				var spawner = MySpawner;

				// try to get the min/maxdelay based on spawner values if not specified on the attachment.
				if (spawner != null && m_MaxDelay < TimeSpan.Zero) return spawner.MaxDelay;

				return m_MaxDelay;
			}
			set => m_MaxDelay = value;
		}

		public override bool HandlesOnKilled => true;

		private XmlSpawner MySpawner
		{
			get
			{
				// figure out the spawner that spawned the object
				if (AttachedTo is Item)
					return ((Item)AttachedTo).Spawner as XmlSpawner;
				else if (AttachedTo is Mobile) return ((Mobile)AttachedTo).Spawner as XmlSpawner;

				return null;
			}
		}

		// These are the various ways in which the message attachment can be constructed.  
		// These can be called via the [addatt interface, via scripts, via the spawner ATTACH keyword.
		// Other overloads could be defined to handle other types of arguments
		public static void ResetXmlSpawnTime(Mobile killed)
		{
			if (killed == null)
				return;

			// set the spawner's NextSpawn time based on min/maxdelay
			var spawner = killed.Spawner as XmlSpawner;

			if (spawner != null)
			{
				var mind = (int)spawner.MinDelay.TotalSeconds;
				var maxd = (int)spawner.MaxDelay.TotalSeconds;

				if (mind >= 0 && maxd >= 0) spawner.NextSpawn = TimeSpan.FromSeconds(Utility.RandomMinMax(mind, maxd));
			}
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
			// version 0
			writer.Write(m_MinDelay);
			writer.Write(m_MaxDelay);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
			switch (version)
			{
				case 0:
					m_MinDelay = reader.ReadTimeSpan();
					m_MaxDelay = reader.ReadTimeSpan();
					break;
			}
		}

		public override void OnKilled(Mobile killed, Mobile killer)
		{
			base.OnKilled(killed, killer);

			if (killed == null)
				return;

			// set the spawner's NextSpawn time based on min/maxdelay
			var spawner = MySpawner;

			var mind = (int)MinDelay.TotalSeconds;
			var maxd = (int)MaxDelay.TotalSeconds;

			if (spawner != null && mind >= 0 && maxd >= 0)
				spawner.NextSpawn = TimeSpan.FromSeconds(Utility.RandomMinMax(mind, maxd));
		}
	}
}
