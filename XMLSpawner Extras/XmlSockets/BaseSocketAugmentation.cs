using System;

namespace Server.Engines.XmlSpawner2
{
	// this is the interface for objects that can be used in sockets
	public interface IXmlSocketAugmentation
	{
		string OnIdentify(Mobile from);

		bool OnAugment(Mobile from, object target);

		bool CanAugment(Mobile from, object target);

		bool CanAugment(Mobile from, object target, int socketnumber);

		bool OnRecover(Mobile from, object target, int version);

		bool CanRecover(Mobile from, object target, int version);

		int RecoverableSockets(int version);

		bool ConsumeOnAugment(Mobile from);

		int SocketsRequired { get; }

		bool DestroyAfterUse { get; }

		int Version { get; }

		void Delete();

		string Name { get; set; }

		int Icon { get; }

		bool UseGumpArt { get; }

		int IconXOffset { get; }

		int IconYOffset { get; }

		int IconHue { get; }
	}

	public abstract class BaseSocketAugmentation : Item, IXmlSocketAugmentation
	{
		public override void GetProperties(ObjectPropertyList list)
		{
			base.GetProperties(list);

			string msg = null;

			if (SocketsRequired > 1) msg = String.Format("\nRequires {0} sockets", SocketsRequired);

			list.Add(1062613, OnIdentify(null) + msg);
		}

		public virtual string OnIdentify(Mobile from)
		{
			return null;
		}

		public virtual bool OnAugment(Mobile from, object target)
		{
			return true;
		}

		public virtual bool CanAugment(Mobile from, object target)
		{
			return true;
		}

		public virtual bool CanAugment(Mobile from, object target, int socketnumber)
		{
			return CanAugment(from, target);
		}

		public virtual bool OnRecover(Mobile from, object target, int version)
		{
			return false;
		}

		public virtual bool CanRecover(Mobile from, object target, int version)
		{
			return false;
		}

		public virtual int RecoverableSockets(int version)
		{
			return SocketsRequired;
		}

		public virtual bool ConsumeOnAugment(Mobile from)
		{
			return true;
		}

		public virtual bool DestroyAfterUse => true;

		public virtual int Icon => ItemID;

		public virtual int SocketsRequired => 1;

		public virtual int Version => 0;

		public virtual bool UseGumpArt => false;

		public virtual int IconXOffset => 0;

		public virtual int IconYOffset => 0;

		public virtual int IconHue => Hue > 0 ? Hue - 1 : Hue;


		public BaseSocketAugmentation(int itemid) : base(itemid)
		{
		}

		public BaseSocketAugmentation() : base()
		{
		}

		public BaseSocketAugmentation(Serial serial) : base(serial)
		{
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
