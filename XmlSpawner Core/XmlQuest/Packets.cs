using System.IO;

namespace Server.Network
{
	public sealed class ForcedContainerContent : Packet
	{
		public static bool Send(NetState ns, Item c)
		{
			return ns != null && Send(ns, Instantiate(ns, c));
		}

		public static ForcedContainerContent Instantiate(NetState ns, Item c)
		{
			return new ForcedContainerContent(ns.Mobile, c);
		}

		private ForcedContainerContent(Mobile beholder, Item beheld)
			: base(0x3C)
		{
			var items = beheld.Items;
			var count = items.Count;

			EnsureCapacity(5 + count * 19);

			var written = 0;

			m_Stream.Write((ushort)0);

			for (var i = 0; i < count; ++i)
			{
				var child = items[i];

				if (!child.Deleted)
				{
					m_Stream.Write(child.Serial);
					m_Stream.Write((ushort)child.ItemID);
					m_Stream.Write((sbyte)0); // itemID offset
					m_Stream.Write((ushort)child.Amount);
					m_Stream.Write((short)child.X);
					m_Stream.Write((short)child.Y);

					m_Stream.Write(beheld.Serial);
					m_Stream.Write((ushort)(child.QuestItem ? child.QuestItemHue : child.Hue));

					++written;
				}
			}

			m_Stream.Seek(3, SeekOrigin.Begin);
			m_Stream.Write((ushort)written);
		}
	}
}
