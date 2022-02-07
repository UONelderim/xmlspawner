using System;

namespace Server.Engines.XmlSpawner2
{
	public class MobFactionRegion : Region
	{
		private XmlMobFactions.GroupTypes m_FactionType;
		private int m_FactionLevel;
		private DateTime m_lastmsg;
		private Point3D m_EjectLocation;
		private Map m_EjectMap;


		public XmlMobFactions.GroupTypes FactionType
		{
			get => m_FactionType;
			set => m_FactionType = value;
		}

		public int FactionLevel
		{
			get => m_FactionLevel;
			set => m_FactionLevel = value;
		}

		public Point3D EjectLocation
		{
			get => m_EjectLocation;
			set => m_EjectLocation = value;
		}

		public Map EjectMap
		{
			get => m_EjectMap;
			set => m_EjectMap = value;
		}

		public MobFactionRegion(string name, Map map, int priority, params Rectangle3D[] area) : base(name, map,
			priority, area)
		{
		}

		public override bool OnMoveInto(Mobile m, Direction d, Point3D newLocation, Point3D oldLocation)
		{
			if (m.AccessLevel > AccessLevel.Player || Contains(oldLocation))
				return true;

			// do they have enough faction to enter?
			var a = (XmlMobFactions)XmlAttach.FindAttachment(m, typeof(XmlMobFactions));

			if (a == null) return false;

			var fac = a.GetFactionLevel(m_FactionType);

			if (fac < FactionLevel)
			{
				// throttle message display
				if (DateTime.Now - m_lastmsg > TimeSpan.FromSeconds(1))
				{
					m.SendMessage("Your {0} faction is too low to enter here", FactionType);
					m_lastmsg = DateTime.Now;
				}

				return false;
			}

			return true;
		}

		public void KickOut(Mobile m)
		{
			if (m == null || EjectMap == null) return;

			m.SendMessage("Your {0} faction is too low to enter here", FactionType);
			m.MoveToWorld(EjectLocation, EjectMap);
			Effects.SendLocationParticles(Items.EffectItem.Create(m.Location, m.Map, Items.EffectItem.DefaultDuration),
				0x3728, 10, 10, 2023);
		}

		public override void OnEnter(Mobile m)
		{
			if (m == null || m.AccessLevel > AccessLevel.Player)
				return;

			// do they have enough faction to enter?
			var a = (XmlMobFactions)XmlAttach.FindAttachment(m, typeof(XmlMobFactions));

			if (a == null)
			{
				// kick them out
				KickOut(m);
				return;
			}

			var fac = a.GetFactionLevel(m_FactionType);

			if (fac < FactionLevel) KickOut(m);
		}

		public override void OnExit(Mobile m)
		{
		}
	}
}
