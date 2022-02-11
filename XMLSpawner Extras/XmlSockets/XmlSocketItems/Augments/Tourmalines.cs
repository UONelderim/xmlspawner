using Server.Mobiles;
using Server.Engines.XmlSpawner2;

namespace Server.Items
{
	// --------------------------------------------------
	// Mythic Tourmaline
	// --------------------------------------------------

	public class MythicTourmaline : BaseSocketAugmentation, IMythicAugment
	{
		[Constructable]
		public MythicTourmaline() : base(0xF2D)
		{
			Name = "Mythic Tourmaline";
			Hue = 1161;
		}

		public MythicTourmaline(Serial serial) : base(serial)
		{
		}

		public override int SocketsRequired => 3;

		public override int Icon => 0x9a8;

		public override bool UseGumpArt => true;

		public override int IconXOffset => 15;

		public override int IconYOffset => 15;


		public override string OnIdentify(Mobile from)
		{
			return "Weapon: +25 Weapon speed\nShields,Armor: +15 Reflect physical\nCreature: +5 All resists";
		}

		public override bool OnAugment(Mobile from, object target)
		{
			if (target is BaseWeapon)
				((BaseWeapon)target).Attributes.WeaponSpeed += 25;
			else if (target is BaseArmor)
				((BaseArmor)target).Attributes.ReflectPhysical += 15;
			else if (target is BaseCreature)
			{
				var b = target as BaseCreature;

				b.PhysicalResistanceSeed += 5;
				b.FireResistSeed += 5;
				b.ColdResistSeed += 5;
				b.PoisonResistSeed += 5;
				b.EnergyResistSeed += 5;
			}
			else
				return false;

			return true;
		}


		public override bool CanAugment(Mobile from, object target)
		{
			return target is BaseWeapon || target is BaseArmor || target is BaseCreature;
		}

		public override bool OnRecover(Mobile from, object target, int version)
		{
			if (target is BaseWeapon)
				((BaseWeapon)target).Attributes.WeaponSpeed -= 25;
			else if (target is BaseArmor)
				((BaseArmor)target).Attributes.ReflectPhysical -= 15;
			else if (target is BaseCreature)
			{
				var b = target as BaseCreature;

				b.PhysicalResistanceSeed -= 5;
				b.FireResistSeed -= 5;
				b.ColdResistSeed -= 5;
				b.PoisonResistSeed -= 5;
				b.EnergyResistSeed -= 5;
			}
			else
				return false;

			return true;
		}


		public override bool CanRecover(Mobile from, object target, int version)
		{
			return true;
		}


		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
		}
	}

	// --------------------------------------------------
	// Legendary Tourmaline
	// --------------------------------------------------

	public class LegendaryTourmaline : BaseSocketAugmentation, ILegendaryAugment
	{
		[Constructable]
		public LegendaryTourmaline() : base(0xf26)
		{
			Name = "Legendary Tourmaline";
			Hue = 53;
		}

		public LegendaryTourmaline(Serial serial) : base(serial)
		{
		}

		public override int SocketsRequired => 2;

		public override int Icon => 0x9a8;

		public override bool UseGumpArt => true;

		public override int IconXOffset => 15;

		public override int IconYOffset => 15;


		public override string OnIdentify(Mobile from)
		{
			return "Weapon: +15 Weapon speed\nShields,Armor: +8 Reflect physical\nCreature: +3 All resists";
		}

		public override bool OnAugment(Mobile from, object target)
		{
			if (target is BaseWeapon)
				((BaseWeapon)target).Attributes.WeaponSpeed += 15;
			else if (target is BaseArmor)
				((BaseArmor)target).Attributes.ReflectPhysical += 8;
			else if (target is BaseCreature)
			{
				var b = target as BaseCreature;

				b.PhysicalResistanceSeed += 3;
				b.FireResistSeed += 3;
				b.ColdResistSeed += 3;
				b.PoisonResistSeed += 3;
				b.EnergyResistSeed += 3;
			}
			else
				return false;

			return true;
		}


		public override bool CanAugment(Mobile from, object target)
		{
			return target is BaseWeapon || target is BaseArmor || target is BaseCreature;
		}

		public override bool OnRecover(Mobile from, object target, int version)
		{
			if (target is BaseWeapon)
				((BaseWeapon)target).Attributes.WeaponSpeed -= 15;
			else if (target is BaseArmor)
				((BaseArmor)target).Attributes.ReflectPhysical -= 8;
			else if (target is BaseCreature)
			{
				var b = target as BaseCreature;

				b.PhysicalResistanceSeed -= 3;
				b.FireResistSeed -= 3;
				b.ColdResistSeed -= 3;
				b.PoisonResistSeed -= 3;
				b.EnergyResistSeed -= 3;
			}
			else
				return false;

			return true;
		}


		public override bool CanRecover(Mobile from, object target, int version)
		{
			return true;
		}


		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
		}
	}

	// --------------------------------------------------
	// Ancient Tourmaline
	// --------------------------------------------------

	public class AncientTourmaline : BaseSocketAugmentation, IAncientAugment
	{
		[Constructable]
		public AncientTourmaline() : base(0xf26)
		{
			Name = "Ancient Tourmaline";
			Hue = 56;
		}

		public AncientTourmaline(Serial serial) : base(serial)
		{
		}

		public override int SocketsRequired => 1;

		public override int Icon => 0x9a8;

		public override bool UseGumpArt => true;

		public override int IconXOffset => 15;

		public override int IconYOffset => 15;


		public override string OnIdentify(Mobile from)
		{
			return "Weapon: +5 Weapon speed\nShields,Armor: +3 Reflect physical\nCreature: +1 All resists";
		}

		public override bool OnAugment(Mobile from, object target)
		{
			if (target is BaseWeapon)
				((BaseWeapon)target).Attributes.WeaponSpeed += 5;
			else if (target is BaseArmor)
				((BaseArmor)target).Attributes.ReflectPhysical += 3;
			else if (target is BaseCreature)
			{
				var b = target as BaseCreature;

				b.PhysicalResistanceSeed += 1;
				b.FireResistSeed += 1;
				b.ColdResistSeed += 1;
				b.PoisonResistSeed += 1;
				b.EnergyResistSeed += 1;
			}
			else
				return false;

			return true;
		}


		public override bool CanAugment(Mobile from, object target)
		{
			return target is BaseWeapon || target is BaseArmor || target is BaseCreature;
		}

		public override bool OnRecover(Mobile from, object target, int version)
		{
			if (target is BaseWeapon)
				((BaseWeapon)target).Attributes.WeaponSpeed -= 5;
			else if (target is BaseArmor)
				((BaseArmor)target).Attributes.ReflectPhysical -= 3;
			else if (target is BaseCreature)
			{
				var b = target as BaseCreature;

				b.PhysicalResistanceSeed -= 1;
				b.FireResistSeed -= 1;
				b.ColdResistSeed -= 1;
				b.PoisonResistSeed -= 1;
				b.EnergyResistSeed -= 1;
			}
			else
				return false;

			return true;
		}


		public override bool CanRecover(Mobile from, object target, int version)
		{
			return true;
		}


		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
		}
	}
}
