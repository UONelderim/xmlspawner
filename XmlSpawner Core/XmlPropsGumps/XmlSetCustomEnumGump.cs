using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Server.Network;
using Server.Commands;

namespace Server.Gumps
{
	public class XmlSetCustomEnumGump : XmlSetListOptionGump
	{
		private string[] m_Names;

		public XmlSetCustomEnumGump(PropertyInfo prop, Mobile mobile, object o, Stack<PropertiesGump.StackEntry> stack,
			int propspage, ArrayList list, string[] names) : base(prop, mobile, o, stack, propspage, list, names, null)
		{
			m_Names = names;
		}

		public override void OnResponse(NetState sender, RelayInfo relayInfo)
		{
			var index = relayInfo.ButtonID - 1;

			if (index >= 0 && index < m_Names.Length)
				try
				{
					var info = m_Property.PropertyType.GetMethod("Parse", new Type[] { typeof(string) });

					CommandLogging.LogChangeProperty(m_Mobile, m_Object, m_Property.Name, m_Names[index]);

					if (info != null)
						m_Property.SetValue(m_Object, info.Invoke(null, new object[] { m_Names[index] }), null);
					else if (m_Property.PropertyType == typeof(Enum) ||
					         m_Property.PropertyType.IsSubclassOf(typeof(Enum)))
						m_Property.SetValue(m_Object, Enum.Parse(m_Property.PropertyType, m_Names[index], false), null);
				}
				catch
				{
					m_Mobile.SendMessage("An exception was caught. The property may not have changed.");
				}

			m_Mobile.SendGump(new XmlPropertiesGump(m_Mobile, m_Object, m_Stack, m_List, m_Page));
		}
	}
}
