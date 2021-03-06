using System;
using Server.Targeting;
using Server.Commands;

namespace Server.Items
{
	public partial class ItemFlags
	{
		[Usage("Flag flagfield")]
		[Description("Gets the state of the specified SavedFlag on any item")]
		public static void GetFlag_OnCommand(CommandEventArgs e)
		{
			var flag = 0;
			var error = false;
			if (e.Arguments.Length > 0)
			{
				if (e.Arguments[0].StartsWith("0x"))
					try { flag = Convert.ToInt32(e.Arguments[0].Substring(2), 16); }
					catch { error = true; }
				else
					try { flag = Int32.Parse(e.Arguments[0]); }
					catch { error = true; }
			}

			if (!error)
				e.Mobile.Target = new GetFlagTarget(e, flag);
			else
				try
				{
					e.Mobile.SendMessage(33, "Flag: Bad flagfield argument");
				}
				catch { }
		}

		private class GetFlagTarget : Target
		{
			private CommandEventArgs m_e;
			private int m_flag;

			public GetFlagTarget(CommandEventArgs e, int flag) : base(30, false, TargetFlags.None)
			{
				m_e = e;
				m_flag = flag;
			}

			protected override void OnTarget(Mobile from, object targeted)
			{
				if (targeted is Item)
				{
					var state = ((Item)targeted).GetSavedFlag(m_flag);

					from.SendMessage("Flag (0x{0:X}) = {1}", m_flag, state);
				}
				else
					@from.SendMessage("Must target an Item");
			}
		}


		[Usage("Stealable [true/false]")]
		[Description("Sets/gets the stealable flag on any item")]
		public static void SetStealable_OnCommand(CommandEventArgs e)
		{
			var state = false;
			var error = false;
			if (e.Arguments.Length > 0)
				try { state = Boolean.Parse(e.Arguments[0]); }
				catch { error = true; }

			if (!error) e.Mobile.Target = new SetStealableTarget(e, state);
		}

		private class SetStealableTarget : Target
		{
			private CommandEventArgs m_e;
			private bool m_state;
			private bool set = false;

			public SetStealableTarget(CommandEventArgs e, bool state) : base(30, false, TargetFlags.None)
			{
				m_e = e;
				m_state = state;
				if (e.Arguments.Length > 0) set = true;
			}

			protected override void OnTarget(Mobile from, object targeted)
			{
				if (targeted is Item)
				{
					if (set) SetStealable((Item)targeted, m_state);

					var state = GetStealable((Item)targeted);

					from.SendMessage("Stealable = {0}", state);
				}
				else
					@from.SendMessage("Must target an Item");
			}
		}
	}
}
