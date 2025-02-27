namespace Server.Items
{
	public abstract class BaseRewardScroll : Item
	{
		public override double DefaultWeight => 0.0;

		public BaseRewardScroll() : base(0x2D51) { }

		public BaseRewardScroll(Serial serial) : base(serial) { }

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

	public class RewardScrollDeed : Item
	{
		[Constructable]
		public RewardScrollDeed() : this(1)
		{
			ItemID = 5360;
			Movable = true;
			Hue = 1165;
			Name = "Reward Scroll Deed";
		}

		public override void OnDoubleClick(Mobile from)
		{
			from.AddToBackpack(new XmlRewardScroll());
			Delete();
		}

		[Constructable]
		public RewardScrollDeed(int amount)
		{
		}

		public RewardScrollDeed(Serial serial) : base(serial)
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

			var version = reader.ReadInt();
		}
	}

	public class XmlRewardScroll : BaseRewardScroll
	{
		[Constructable]
		public XmlRewardScroll()
		{
			Stackable = true;
			Name = "Reward Scroll";
			Hue = 1165;
			LootType = LootType.Blessed;
		}

		public XmlRewardScroll(Serial serial) : base(serial) { }

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);
			writer.Write((int)0);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);
			int version = reader.ReadInt();
		}
	}
}
