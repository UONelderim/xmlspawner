using System;
using Server.Targeting;
using Server.Engines.XmlSpawner2;

namespace Server.Items
{
	public class SiegeRepairTool : Item
	{
		public const double
			RepairDestroyedResourcePenalty = 3; // additional resource  factor for repairing destroyed structures

		public const double RepairDestroyedTimePenalty = 3; // additional time factor for repairing destroyed structures
		public const int RepairRange = 2; // number of tiles away to search for repairable objects 

		private int m_UsesRemaining; // if set to less than zero, becomes unlimited uses
		private int m_HitPerRepair = 100; // number of hits repaired per use

		public virtual double BaseRepairTime => 9.0; // base time in seconds required to repair

		[CommandProperty(AccessLevel.GameMaster)]
		public int UsesRemaining
		{
			get => m_UsesRemaining;
			set
			{
				m_UsesRemaining = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitsPerRepair
		{
			get => m_HitPerRepair;
			set
			{
				m_HitPerRepair = value;
				InvalidateProperties();
			}
		}

		[Constructable]
		public SiegeRepairTool()
			: this(50)
		{
		}

		[Constructable]
		public SiegeRepairTool(int nuses)
			: base(0x13E4)
		{
			Name = "A Siege Repair Tool";
			Hue = 0;
			UsesRemaining = nuses;
		}

		public SiegeRepairTool(Serial serial)
			: base(serial)
		{
		}

		public override void GetProperties(ObjectPropertyList list)
		{
			base.GetProperties(list);

			if (m_UsesRemaining >= 0) list.Add(1060584, m_UsesRemaining.ToString()); // uses remaining: ~1_val~

			list.Add(1060662, "Hits per repair\t{0}", HitsPerRepair.ToString()); // ~1_val~: ~2_val
		}

		public override void OnDoubleClick(Mobile from)
		{
			if (UsesRemaining == 0)
			{
				from.SendMessage("This tool is now useless");
				return;
			}

			if (IsChildOf(from.Backpack) || Parent == from)
				@from.Target = new SiegeRepairTarget(this);
			else
				@from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
			writer.Write(m_UsesRemaining);
			writer.Write(m_HitPerRepair);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();

			switch (version)
			{
				case 0:
					m_UsesRemaining = reader.ReadInt();
					m_HitPerRepair = reader.ReadInt();
					break;
			}
		}

		public static void SiegeRepair_Callback(object state)
		{
			var args = (object[])state;

			var from = (Mobile)args[0];
			var a = (XmlSiege)args[1];
			var nhits = (int)args[2];

			if (a != null)
			{
				a.Hits += nhits;
				from.SendMessage("{0} hits repaired", nhits);
				a.BeingRepaired = false;
			}
		}

		private class SiegeRepairTarget : Target
		{
			private SiegeRepairTool m_tool;

			public SiegeRepairTarget(SiegeRepairTool tool)
				: base(2, true, TargetFlags.None)
			{
				m_tool = tool;
			}

			protected override void OnTarget(Mobile from, object targeted)
			{
				if (from == null || m_tool == null || from.Map == null) return;

				// find any xmlsiege attachment on the target
				var a = (XmlSiege)XmlAttach.FindAttachment(targeted, typeof(XmlSiege));

				// if it isnt on the target, but the target is an addon, then check the addon
				if (a == null && targeted is AddonComponent)
					a = (XmlSiege)XmlAttach.FindAttachment(((AddonComponent)targeted).Addon, typeof(XmlSiege));

				// if it still isnt found, the look for nearby targets
				if (a == null)
				{
					var loc = Point3D.Zero;
					if (targeted is IEntity)
						loc = ((IEntity)targeted).Location;
					else if (targeted is StaticTarget)
						loc = ((StaticTarget)targeted).Location;
					else if (targeted is LandTarget) loc = ((LandTarget)targeted).Location;

					if (loc != Point3D.Zero)
						foreach (var p in @from.Map.GetItemsInRange(loc, RepairRange))
						{
							a = (XmlSiege)XmlAttach.FindAttachment(p, typeof(XmlSiege));
							if (a != null) break;
						}
				}

				// repair the target
				if (a != null)
				{
					if (a.Hits >= a.HitsMax)
					{
						from.SendMessage("This does not require repair.");
						return;
					}

					if (a.BeingRepaired)
					{
						from.SendMessage("You must wait to repair again.");
						return;
					}

					var pack = from.Backpack;

					// does the player have it?
					if (pack != null)
					{
						var nhits = 0;


						double resourcepenalty = 1;

						// require more resources for repairing destroyed structures
						if (a.Hits == 0) resourcepenalty = RepairDestroyedResourcePenalty;

						// dont consume resources for staff
						if (from.AccessLevel > AccessLevel.Player) resourcepenalty = 0;

						var requirediron = (int)(a.Iron * resourcepenalty);
						var requiredstone = (int)(a.Stone * resourcepenalty);
						var requiredwood = (int)(a.Wood * resourcepenalty);

						var niron = pack.ConsumeUpTo(typeof(IronIngot), requirediron);
						var nstone = pack.ConsumeUpTo(typeof(BaseGranite), requiredstone);
						var nwood = pack.ConsumeUpTo(typeof(Board), requiredwood);

						if (niron == requirediron && nstone == requiredstone && nwood == requiredwood)
							nhits += m_tool.HitsPerRepair;


						if (nhits == 0)
						{
							from.SendMessage("Insufficient resources to complete the repair. Resources lost.");
							return;
						}

						from.PlaySound(0x2A); // play anvil sound
						from.SendMessage("You begin your repair");

						m_tool.UsesRemaining--;
						if (m_tool.UsesRemaining == 0)
						{
							from.SendLocalizedMessage(1044038); // You have worn out your tool!
							m_tool.Delete();
						}

						a.BeingRepaired = true;

						var smithskill = from.Skills[SkillName.Blacksmith].Value;
						var carpentryskill = from.Skills[SkillName.Carpentry].Value;

						double timepenalty = 1;
						if (a.Hits == 0)
							// repairing destroyed structures requires more time
							timepenalty = RepairDestroyedTimePenalty;

						// compute repair speed with modifiers
						var repairtime = TimeSpan.FromSeconds(m_tool.BaseRepairTime * timepenalty - from.Dex / 40.0 -
						                                      smithskill / 50.0 - carpentryskill / 50.0);

						// allow staff instant repair
						if (from.AccessLevel > AccessLevel.Player) repairtime = TimeSpan.Zero;

						// setup for the delayed repair
						Timer.DelayCall(repairtime, new TimerStateCallback(SiegeRepair_Callback),
							new object[] { from, a, nhits });
					}
				}
				else
				{
					from.SendMessage("Invalid target");
					return;
				}
			}
		}
	}
}
