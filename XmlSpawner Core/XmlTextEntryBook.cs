#define BOOKTEXTENTRY

using System;
using System.Text;
using Server.Network;

namespace Server.Items
{
	public delegate void XmlTextEntryBookCallback(Mobile from, object[] args, string response);

	public class XmlTextEntryBook : BaseEntryBook
	{
		public XmlTextEntryBookCallback m_bookcallback;
		public object[] m_args;

		public XmlTextEntryBook(int itemID, string title, string author, int pageCount, bool writable,
			XmlTextEntryBookCallback callback, object[] args) : base(itemID, title, author, pageCount, writable)
		{
			m_args = args;
			m_bookcallback = callback;
		}

		public XmlTextEntryBook(Serial serial) : base(serial)
		{
		}

		public void FillTextEntryBook(string text)
		{
			var pagenum = 0;
			var pages = Pages;
			var current = 0;

			// break up the text into single line length pieces
			while (text != null && current < text.Length)
			{
				var lineCount = 8;
				var lines = new string[lineCount];

				// place the line on the page
				for (var i = 0; i < lineCount; i++)
					if (current < text.Length)
					{
						// make each line 25 chars long
						var length = text.Length - current;
						if (length > 20) length = 20;
						lines[i] = text.Substring(current, length);
						current += length;
					}
					else
						// fill up the remaining lines
						lines[i] = String.Empty;

				if (pagenum >= PagesCount)
					return;
				Pages[pagenum].Lines = lines;
				pagenum++;
			}

			// empty the remaining contents
			for (var j = pagenum; j < PagesCount; j++)
				if (Pages[j].Lines.Length > 0)
					for (var i = 0; i < Pages[j].Lines.Length; i++)
						Pages[j].Lines[i] = String.Empty;
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

			Delete();
		}
	}

	// -------------------------------------------------------------
	// modifed from Beta-36 distribution version of BaseBook from basebook.cs
	// adds a hook to allow processing of book text on content change
	// -------------------------------------------------------------
	public class BaseEntryBook : Item
	{
		private string m_Title;
		private string m_Author;
		private BookPageInfo[] m_Pages;
		private bool m_Writable;

		[CommandProperty(AccessLevel.GameMaster)]
		public string Title
		{
			get => m_Title;
			set
			{
				m_Title = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string Author
		{
			get => m_Author;
			set
			{
				m_Author = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool Writable
		{
			get => m_Writable;
			set => m_Writable = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int PagesCount => m_Pages.Length;

		public BookPageInfo[] Pages => m_Pages;

		[Constructable]
		public BaseEntryBook(int itemID, string title, string author, int pageCount, bool writable) : base(itemID)
		{
			m_Title = title;
			m_Author = author;
			m_Pages = new BookPageInfo[pageCount];
			m_Writable = writable;

			for (var i = 0; i < m_Pages.Length; ++i)
				m_Pages[i] = new BookPageInfo();
		}

		public BaseEntryBook(Serial serial) : base(serial)
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
#if(BOOKTEXTENTRY)
		public override void OnDoubleClick(Mobile from)
		{
			from.Send(new EntryBookHeader(from, this));
			from.Send(new EntryBookPageDetails(this));
		}

		public static void Initialize()
		{
			// This will hijack the default packet handler for basebooks content change.  The header change handlers are left alone and basebook will still handle them.
			// This means that BaseEntryBooks will not support header changes (they are not intended to)

			//PacketHandlers.Register( 0x66,  0, true, new OnPacketReceive( ContentChange ) );
		}

		public static void ContentChange(NetState state, PacketReader pvSrc)
		{
			// need to deal with actual books
			var entryText = String.Empty;
			var from = state.Mobile;

			var serial = pvSrc.ReadSerial();

			var bookitem = World.FindItem(serial);

			// first try it as a normal basebook
			if (bookitem is BaseBook)
			{
				// do the base book content change
				BaseContentChange(bookitem as BaseBook, state, pvSrc);
				return;
			}

			// then try it as a text entry book
			var book = bookitem as BaseEntryBook;

			if (book == null) return;

			// get the number of available pages in the book
			int pageCount = pvSrc.ReadUInt16();

			if (pageCount > book.PagesCount)
				return;

			for (var i = 0; i < pageCount; ++i)
			{
				// get the current page number being read
				int index = pvSrc.ReadUInt16();

				if (index >= 1 && index <= book.PagesCount)
				{
					--index;

					int lineCount = pvSrc.ReadUInt16();

					if (lineCount <= 8)
					{
						var lines = new string[lineCount];

						for (var j = 0; j < lineCount; ++j)
							if ((lines[j] = pvSrc.ReadUTF8StringSafe()).Length >= 80)
								return;

						book.Pages[index].Lines = lines;
					}
					else
						return;
				}
				else
					return;
			}

			var sb = new StringBuilder();
			// add the book lines to the entry string
			for (var i = 0; i < book.PagesCount; ++i)
			for (var j = 0; j < book.Pages[i].Lines.Length; j++)
				sb.Append(book.Pages[i].Lines[j]);

			// send the book text off to be processed by invoking the callback if it is a textentry book
			var tebook = book as XmlTextEntryBook;
			if (tebook != null && tebook.m_bookcallback != null)
				tebook.m_bookcallback(state.Mobile, tebook.m_args, sb.ToString());
		}

		public static void BaseContentChange(BaseBook book, NetState state, PacketReader pvSrc)
		{
			var from = state.Mobile;

			if (book == null || !book.Writable || !from.InRange(book.GetWorldLocation(), 1))
				return;

			int pageCount = pvSrc.ReadUInt16();

			if (pageCount > book.PagesCount)
				return;

			for (var i = 0; i < pageCount; ++i)
			{
				int index = pvSrc.ReadUInt16();

				if (index >= 1 && index <= book.PagesCount)
				{
					--index;

					int lineCount = pvSrc.ReadUInt16();

					if (lineCount <= 8)
					{
						var lines = new string[lineCount];

						for (var j = 0; j < lineCount; ++j)
							if ((lines[j] = pvSrc.ReadUTF8StringSafe()).Length >= 80)
								return;

						book.Pages[index].Lines = lines;
					}
					else
						return;
				}
				else
					return;
			}
		}
#endif
	}

#if(BOOKTEXTENTRY)
	public sealed class EntryBookPageDetails : Packet
	{
		public EntryBookPageDetails(BaseEntryBook book) : base(0x66)
		{
			EnsureCapacity(256);

			m_Stream.Write((int)book.Serial);
			m_Stream.Write((ushort)book.PagesCount);

			for (var i = 0; i < book.PagesCount; ++i)
			{
				var page = book.Pages[i];

				m_Stream.Write((ushort)(i + 1));
				m_Stream.Write((ushort)page.Lines.Length);

				for (var j = 0; j < page.Lines.Length; ++j)
				{
					var buffer = Utility.UTF8.GetBytes(page.Lines[j]);

					m_Stream.Write(buffer, 0, buffer.Length);
					m_Stream.Write((byte)0);
				}
			}
		}
	}

	public sealed class EntryBookHeader : Packet
	{
		public EntryBookHeader(Mobile from, BaseEntryBook book) : base(0xD4)
		{
			var title = book.Title == null ? "" : book.Title;
			var author = book.Author == null ? "" : book.Author;

			var titleBuffer = Utility.UTF8.GetBytes(title);
			var authorBuffer = Utility.UTF8.GetBytes(author);

			EnsureCapacity(15 + titleBuffer.Length + authorBuffer.Length);

			m_Stream.Write((int)book.Serial);
			m_Stream.Write((bool)true);
			m_Stream.Write((bool)book.Writable && from.InRange(book.GetWorldLocation(), 1));
			m_Stream.Write((ushort)book.PagesCount);

			m_Stream.Write((ushort)(titleBuffer.Length + 1));
			m_Stream.Write(titleBuffer, 0, titleBuffer.Length);
			m_Stream.Write((byte)0); // terminate

			m_Stream.Write((ushort)(authorBuffer.Length + 1));
			m_Stream.Write(authorBuffer, 0, authorBuffer.Length);
			m_Stream.Write((byte)0); // terminate
		}
	}
#endif
}
