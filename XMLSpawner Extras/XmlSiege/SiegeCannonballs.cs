using System;
using Server;
using Server.Targeting;
using Server.Network;
using System.Collections;
using Server.ContextMenus;
using Server.Engines.XmlSpawner2;

namespace Server.Items
{
	public abstract class SiegeCannonball : BaseSiegeProjectile
	{
		public SiegeCannonball()
			: this(1)
		{
		}

		public SiegeCannonball(int amount)
			: base(amount, 0xE74)
		{
		}

		public SiegeCannonball(Serial serial)
			: base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0); // version
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			int version = reader.ReadInt();
		}
	}

	public class LightSiegeCannonball : SiegeCannonball
	{
		[Constructable]
		public LightSiegeCannonball()
			: this(1)
		{
		}

		[Constructable]
		public LightSiegeCannonball(int amount)
			: base(amount)
		{
			Range = 17;
			Area = 0;
			AccuracyBonus = 0;
			PhysicalDamage = 80;
			FireDamage = 0;
			FiringSpeed = 35;
			Name = "Light Cannonball";
		}

		public LightSiegeCannonball(Serial serial)
			: base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0); // version
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			int version = reader.ReadInt();
		}
	}

	public class IronSiegeCannonball : SiegeCannonball
	{
		[Constructable]
		public IronSiegeCannonball()
			: this(1)
		{
		}

		[Constructable]
		public IronSiegeCannonball(int amount)
			: base(amount)
		{
			Range = 15;
			Area = 0;
			AccuracyBonus = 0;
			PhysicalDamage = 100;
			FireDamage = 0;
			FiringSpeed = 25;
			Name = "Iron Cannonball";
		}

		public IronSiegeCannonball(Serial serial)
			: base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0); // version
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			int version = reader.ReadInt();
		}
	}

	public class ExplodingSiegeCannonball : SiegeCannonball
	{
		[Constructable]
		public ExplodingSiegeCannonball()
			: this(1)
		{
		}

		[Constructable]
		public ExplodingSiegeCannonball(int amount)
			: base(amount)
		{
			Range = 11;
			Area = 1;
			AccuracyBonus = -10;
			PhysicalDamage = 10;
			FireDamage = 40;
			FiringSpeed = 20;
			Hue = 46;
			Name = "Exploding Cannonball";
		}

		public ExplodingSiegeCannonball(Serial serial)
			: base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0); // version
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			int version = reader.ReadInt();
		}
	}

	public class FierySiegeCannonball : SiegeCannonball
	{
		public override int AnimationID { get { return 0x36D4; } }
		public override int AnimationHue { get { return 0; } }

		[Constructable]
		public FierySiegeCannonball()
			: this(1)
		{
		}

		[Constructable]
		public FierySiegeCannonball(int amount)
			: base(amount)
		{
			Range = 8;
			Area = 2;
			AccuracyBonus = -20;
			PhysicalDamage = 0;
			FireDamage = 30;
			FiringSpeed = 10;
			Hue = 33;
			Name = "Fiery Cannonball";
		}

		public FierySiegeCannonball(Serial serial)
			: base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0); // version
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			int version = reader.ReadInt();
		}
/*
		public override Item Dupe(int amount)
		{
			FieryCannonball s = new FieryCannonball(amount);

			return this.Dupe(s, amount);
		}
 * */
	}

	public class SiegeGrapeShot : SiegeCannonball
	{
		// only does damage to mobiles
		public override double StructureDamageMultiplier { get { return 0.0; } } //  damage multiplier for structures

		[Constructable]
		public SiegeGrapeShot()
			: this(1)
		{
		}

		[Constructable]
		public SiegeGrapeShot(int amount)
			: base(amount)
		{
			Range = 17;
			Area = 1;
			AccuracyBonus = 0;
			PhysicalDamage = 20;
			FireDamage = 0;
			FiringSpeed = 35;
			Name = "Grape Shot";
		}

		public SiegeGrapeShot(Serial serial)
			: base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0); // version
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			int version = reader.ReadInt();
		}
	}
}
