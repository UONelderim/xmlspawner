using System;
using Server.Items;
using Server.Targeting;

namespace Server.Engines.XmlSpawner2
{
	public class HandSiegeAttack : XmlAttachment
	{
		private const double
			DamageScaleFactor = 0.5; // multiplier of weapon min/max damage used to calculate siege damage.

		private const double
			BaseWeaponDelay =
				9.0; // base delay in seconds between attacks.  Actual delay will be reduced by weapon speed.

		private Item m_AttackTarget = null; // target of the attack
		private Point3D m_currentloc;
		private int m_currentdirection;
		private Map m_currentmap;
		private Point3D m_targetloc;
		private Map m_targetmap;
		private int m_MaxDistance = 2; // max distance away from the target allowed

		private InternalTimer m_Timer;

		[CommandProperty(AccessLevel.GameMaster)]
		public Item AttackTarget
		{
			get => m_AttackTarget;
			set
			{
				m_AttackTarget = value;

				if (m_AttackTarget != null)
					// immediate attack unless already attacking
					DoTimer(TimeSpan.Zero, true);
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int MaxDistance
		{
			get => m_MaxDistance;
			set => m_MaxDistance = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public Point3D CurrentLoc
		{
			get => m_currentloc;
			set => m_currentloc = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public Map CurrentMap
		{
			get => m_currentmap;
			set => m_currentmap = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public Point3D TargetLoc
		{
			get => m_targetloc;
			set => m_targetloc = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public Map TargetMap
		{
			get => m_targetmap;
			set => m_targetmap = value;
		}

		// These are the various ways in which the message attachment can be constructed.  
		// These can be called via the [addatt interface, via scripts, via the spawner ATTACH keyword.
		// Other overloads could be defined to handle other types of arguments

		// a serial constructor is REQUIRED
		public HandSiegeAttack(ASerial serial)
			: base(serial)
		{
		}

		[Attachable]
		public HandSiegeAttack()
		{
		}

		public static void SelectTarget(Mobile from, Item weapon)
		{
			if (from == null || weapon == null) return;

			// does this weapon have a HandSiegeAttack attachment on it already?

			var a = (HandSiegeAttack)XmlAttach.FindAttachment(weapon, typeof(HandSiegeAttack));

			if (a == null || a.Deleted)
			{
				a = new HandSiegeAttack();
				XmlAttach.AttachTo(weapon, a);
			}

			from.Target = new HandSiegeTarget(weapon, a);
		}

		private class HandSiegeTarget : Target
		{
			private Item m_weapon;
			private HandSiegeAttack m_attachment;

			public HandSiegeTarget(Item weapon, HandSiegeAttack attachment)
				: base(30, true, TargetFlags.None)
			{
				m_weapon = weapon;
				m_attachment = attachment;
			}

			protected override void OnTarget(Mobile from, object targeted)
			{
				if (from == null || m_weapon == null || from.Map == null || m_attachment == null) return;

				if (targeted is StaticTarget)
				{
					var staticid = ((StaticTarget)targeted).ItemID;
					var staticx = ((StaticTarget)targeted).Location.X;
					var staticy = ((StaticTarget)targeted).Location.Y;

					Item multiitem = null;
					var tileloc = Point3D.Zero;

					// find the possible multi owner of the static tile
					foreach (var item in from.Map.GetItemsInRange(((StaticTarget)targeted).Location, 50))
						if (item is BaseMulti)
						{
							// search the component list for a match
							var mcl = ((BaseMulti)item).Components;
							var found = false;
							if (mcl != null && mcl.List != null)
								for (var i = 0; i < mcl.List.Length; i++)
								{
									var t = mcl.List[i];

									var x = t.m_OffsetX + item.X;
									var y = t.m_OffsetY + item.Y;
									var z = t.m_OffsetZ + item.Z;
									var itemID = t.m_ItemID & 0x3FFF;

									if (itemID == staticid && x == staticx && y == staticy)
									{
										found = true;
										tileloc = new Point3D(x, y, z);
										break;
									}
								}

							if (found)
							{
								multiitem = item;
								break;
							}
						}

					if (multiitem != null)
						//Console.WriteLine("attacking {0} at {1}:{2}", multiitem, tileloc, ((StaticTarget)targeted).Location);
						// may have to reconsider the use tileloc vs target loc
						//m_cannon.AttackTarget(from, multiitem, ((StaticTarget)targeted).Location);

						//m_weapon.AttackTarget(from, multiitem, multiitem.Map.GetPoint(targeted, true), m_checklos);


						m_attachment.BeginAttackTarget(@from, multiitem, multiitem.Map.GetPoint(targeted, true));
				}
				else if (targeted is AddonComponent)
				{
					// if the addon doesnt have an xmlsiege attachment, then attack the addon
					var a = (XmlSiege)XmlAttach.FindAttachment(targeted, typeof(XmlSiege));
					if (a == null || a.Deleted)
						m_attachment.BeginAttackTarget(@from, ((AddonComponent)targeted).Addon,
							((Item)targeted).Location);
					else
						m_attachment.BeginAttackTarget(@from, (Item)targeted, ((Item)targeted).Location);
				}
				else if (targeted is Item)
					m_attachment.BeginAttackTarget(@from, (Item)targeted, ((Item)targeted).Location);
			}
		}

		public void BeginAttackTarget(Mobile from, Item target, Point3D targetloc)
		{
			if (from == null || target == null) return;

			// check the target line of sight
			var adjustedloc = new Point3D(targetloc.X, targetloc.Y, targetloc.Z + target.ItemData.Height);
			var fromloc = new Point3D(from.Location.X, from.Location.Y, from.Location.Z + 14);

			if (!from.Map.LineOfSight(fromloc, adjustedloc))
			{
				from.SendMessage("Cannot see target.");
				return;
			}

			var distance = (int)XmlSiege.GetDistance(from.Location, targetloc);

			if (distance <= MaxDistance)
			{
				CurrentLoc = from.Location;
				CurrentMap = from.Map;
				TargetLoc = target.Location;
				TargetMap = target.Map;

				AttackTarget = target;
			}
			else
				@from.SendLocalizedMessage(500446); // That is too far away.
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
			// version 0
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
			// version 0
		}

		public override void OnAttach()
		{
			base.OnAttach();

			if (!(AttachedTo is Item)) Delete();
		}


		public override void OnDelete()
		{
			base.OnDelete();

			if (m_Timer != null)
				m_Timer.Stop();
		}

		public void DoTimer(TimeSpan delay, bool wait)
		{
			// is there a timer already running?  Then let it finish
			if (m_Timer != null && m_Timer.Running && wait) return;

			if (m_Timer != null)
				m_Timer.Stop();

			m_Timer = new InternalTimer(this, delay);
			m_Timer.Start();
		}

		// added the duration timer that begins on spawning
		private class InternalTimer : Timer
		{
			private HandSiegeAttack m_attachment;

			public InternalTimer(HandSiegeAttack attachment, TimeSpan delay)
				: base(delay)
			{
				Priority = TimerPriority.TwoFiftyMS;
				m_attachment = attachment;
			}

			protected override void OnTick()
			{
				if (m_attachment == null) return;

				var weapon = m_attachment.AttachedTo as Item;
				var target = m_attachment.AttackTarget;

				if (weapon == null || weapon.Deleted || target == null || target.Deleted)
				{
					Stop();
					return;
				}

				// the weapon must be equipped
				var attacker = weapon.Parent as Mobile;

				if (attacker == null || attacker.Deleted)
				{
					Stop();
					return;
				}

				// the attacker cannot be fighting

				if (attacker.Combatant != null)
				{
					attacker.SendMessage("Cannot siege while fighting.");
					Stop();
					return;
				}

				// get the location of the attacker

				var attackerloc = attacker.Location;
				var attackermap = attacker.Map;

				var targetloc = target.Location;
				var targetmap = target.Map;

				if (targetmap == null || targetmap == Map.Internal || attackermap == null ||
				    attackermap == Map.Internal || targetmap != attackermap)
				{
					// if the attacker or target has an invalid map, then stop
					Stop();
					return;
				}

				// compare it against previous locations.  If they have moved then break off the attack
				if (attackerloc != m_attachment.CurrentLoc || attackermap != m_attachment.CurrentMap)
				{
					Stop();
					return;
				}


				// attack the target
				// Animate( int action, int frameCount, int repeatCount, bool forward, bool repeat, int delay )
				var action = 26; // 1-H bash animation, 29=2-H mounted


				// get the layer
				switch (weapon.Layer)
				{
					case Layer.OneHanded:

						if (attacker.Mount == null)
							// unmounted animation
							action = 9;
						else
							action = 26;
						break;
					case Layer.TwoHanded:
						if (attacker.Mount == null)
							// unmounted animation
							action = 12;
						else
							action = 29;
						break;
				}

				// attack animation
				attacker.Animate(action, 7, 1, true, false, 0);

				var basedamage = 1;
				var basedelay = BaseWeaponDelay;

				if (weapon is BaseWeapon)
				{
					var b = (BaseWeapon)weapon;
					// calculate the siege damage based on the weapon min/max damage and the overall damage scale factor
					basedamage = (int)(Utility.RandomMinMax(b.MinDamage, b.MaxDamage) * DamageScaleFactor);
					// reduce the actual delay by the weapon speed
					basedelay -= b.Speed / 10;
				}

				if (basedelay < 1) basedelay = 1;
				if (basedamage < 1) basedamage = 1;

				// apply siege damage, all physical
				XmlSiege.Attack(attacker, target, basedamage, 0);

				// prepare for the next attack
				m_attachment.DoTimer(TimeSpan.FromSeconds(basedelay), false);
			}
		}
	}
}
