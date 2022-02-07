using Server.Mobiles;

/*
SimpleTileTrap
Written by Alari

 based off SimpleSwitch. (did a search and replace on simpleswitch->simpletiletrap
and then modified the code as appropriate.)

For this tile trap, 0 is the state when the player moves off or is not standing, and 1 is what is triggered when the player moves directly over the tile trap.
*/


namespace Server.Items
{
	public class SimpleTileTrap : Item
	{
		private int m_SwitchSound = 939;
		private Item m_TargetItem0 = null;
		private string m_TargetProperty0 = null;
		private Item m_TargetItem1 = null;
		private string m_TargetProperty1 = null;

		[Constructable]
		public SimpleTileTrap() : base(7107)
		{
			Name = "A tile trap";
			Movable = false;
			Visible = false;
		}

		public SimpleTileTrap(Serial serial) : base(serial)
		{
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int SwitchSound
		{
			get => m_SwitchSound;
			set
			{
				m_SwitchSound = value;
				InvalidateProperties();
			}
		}


		[CommandProperty(AccessLevel.GameMaster)]
		public Item Target0Item
		{
			get => m_TargetItem0;
			set
			{
				m_TargetItem0 = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string Target0Property
		{
			get => m_TargetProperty0;
			set
			{
				m_TargetProperty0 = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string Target0ItemName
		{
			get
			{
				if (m_TargetItem0 != null && !m_TargetItem0.Deleted) return m_TargetItem0.Name;
				else return null;
			}
		}


		[CommandProperty(AccessLevel.GameMaster)]
		public Item Target1Item
		{
			get => m_TargetItem1;
			set
			{
				m_TargetItem1 = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string Target1Property
		{
			get => m_TargetProperty1;
			set
			{
				m_TargetProperty1 = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string Target1ItemName
		{
			get
			{
				if (m_TargetItem1 != null && !m_TargetItem1.Deleted) return m_TargetItem1.Name;
				else return null;
			}
		}


		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0); // version 

			writer.Write(m_SwitchSound);
			writer.Write(m_TargetItem0);
			writer.Write(m_TargetProperty0);
			writer.Write(m_TargetItem1);
			writer.Write(m_TargetProperty1);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
			switch (version)
			{
				case 0:
				{
					m_SwitchSound = reader.ReadInt();
					m_TargetItem0 = reader.ReadItem();
					m_TargetProperty0 = reader.ReadString();
					m_TargetItem1 = reader.ReadItem();
					m_TargetProperty1 = reader.ReadString();
				}
					break;
			}
		}

		public bool CheckRange(Point3D loc, Point3D oldLoc, int range)
		{
			return CheckRange(loc, range) && !CheckRange(oldLoc, range);
		}

		public bool CheckRange(Point3D loc, int range)
		{
			return Z + 8 >= loc.Z && loc.Z + 16 > Z
			                      && Utility.InRange(GetWorldLocation(), loc, range);
		}


		public override bool HandlesOnMovement => true; // Tell the core that we implement OnMovement

		public override void OnMovement(Mobile m, Point3D oldLocation)
		{
			base.OnMovement(m, oldLocation);

			if (m.Location == oldLocation)
				return;


			if (m.Player && m.AccessLevel == AccessLevel.Player)
			{
				if (CheckRange(m.Location, oldLocation, 0))
					OnEnter(m);
				else if (oldLocation == Location)
					OnExit(m);
			}
		}

		public virtual void OnEnter(Mobile m)
		{
			string status_str;
			m.PlaySound(SwitchSound);
			BaseXmlSpawner.ApplyObjectStringProperties(null, m_TargetProperty1, m_TargetItem1, m, this, out status_str);
		}

		public virtual void OnExit(Mobile m)
		{
			string status_str;
			BaseXmlSpawner.ApplyObjectStringProperties(null, m_TargetProperty0, m_TargetItem0, m, this, out status_str);
		}
	}
}
