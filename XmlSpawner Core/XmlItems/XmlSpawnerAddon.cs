using System;
using System.IO;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.XmlSpawner2
{
	public class XmlSpawnerAddon : BaseAddon
	{
		public override bool ShareHue => false;

		[CommandProperty(AccessLevel.GameMaster)]
		public virtual int PartialVisibility
		{
			get
			{
				var nvisible = 0;
				// figure out what percentage of components is visible and return that value
				// go through the components
				for (var i = 0; i < Components.Count; i++)
					if (Components[i].Visible)
						nvisible++;

				return (int)(100.0 * nvisible / Components.Count + 0.5);
			}
			set
			{
				if (Components == null || Components.Count < 1) return;

				// assign visibility to the components based upon the percentage value
				var nvisible = value * (Components.Count - 1) / 100;

				// go through the components and assign visibility to the specified percentage
				// starting at the beginning of the component list
				for (var i = 0; i < Components.Count; i++) Components[i].Visible = i < nvisible;
			}
		}

		// create an addon with the static components described in the multi.txt file
		public static XmlSpawnerAddon ReadMultiFile(string filename, out string status_str)
		{
			status_str = null;

			if (filename == null) return null;

			XmlSpawnerAddon newaddon = null;

			// look for the file in the default spawner locations
			var dirname = XmlSpawner.LocateFile(filename);

			if (File.Exists(dirname))
			{
				var ncomponents = 0;

				newaddon = new XmlSpawnerAddon();

				try
				{
					ncomponents = LoadAddonFromMulti(newaddon, dirname, out status_str);
				}
				catch
				{
					newaddon.Delete();
					status_str = "Bad Multi file : " + filename;
					return null;
				}

				if (ncomponents == 0)
				{
					newaddon.Delete();
					status_str += " : " + filename;
					return null;
				}
			}
			else
				status_str = "No such file : " + filename;

			return newaddon;
		}

		// adds components from a multi.txt file to an existing addon
		public static int LoadAddonFromMulti(XmlSpawnerAddon newaddon, string filename, out string status_str)
		{
			status_str = null;

			if (filename == null)
			{
				status_str = "Invalid filename";
				return 0;
			}

			if (newaddon == null)
			{
				status_str = "Invalid addon";
				return 0;
			}

			var badformat = false;
			var ncomponents = 0;

			if (File.Exists(filename))
				using (var sr = new StreamReader(filename))
				{
					string line;
					var linenumber = 0;

					// Read and process lines from the file until the end of the file is reached.
					// Individual lines have the format of
					// itemid x y z visible [hue] ; attachment[,args]
					// where visible is a 0/1 and hue can be optionally specified for individual itemid entries.
					while ((line = sr.ReadLine()) != null)
					{
						linenumber++;

						// process the line
						if (line.Length == 0) continue;

						// first parse out the component specification from any optional attachment specifications

						var specs = line.Split(';');

						// the component spec will always be first

						if (specs == null || specs.Length < 1) continue;

						var args = specs[0].Trim().Split(' ');

						AddonComponent newcomponent = null;

						if (args != null && args.Length >= 5)
						{
							var itemid = -1;
							var x = 0;
							var y = 0;
							var z = 0;
							var visible = 0;
							var hue = -1;

							try
							{
								itemid = Int32.Parse(args[0]);
								x = Int32.Parse(args[1]);
								y = Int32.Parse(args[2]);
								z = Int32.Parse(args[3]);
								visible = Int32.Parse(args[4]);

								// handle the optional fields that are not part of the original multi.txt specification
								if (args.Length > 5) hue = Int32.Parse(args[5]);
							}
							catch { badformat = true; }

							if (itemid < 0 || badformat)
							{
								status_str = String.Format("Error line {0}", linenumber);
								break;
							}

							// create the new component
							newcomponent = new AddonComponent(itemid);

							// set the properties according to the specification
							newcomponent.Visible = visible == 1;

							if (hue >= 0)
								newcomponent.Hue = hue;

							// add it to the addon
							newaddon.AddComponent(newcomponent, x, y, z);

							ncomponents++;
						}

						// if a valid component was added, then check to see if any additional attachment specifications need to be processed
						if (newcomponent != null && specs.Length > 1)
							for (var j = 1; j < specs.Length; j++)
							{
								if (specs[j] == null) continue;

								var attachstring = specs[j].Trim();

								Type type = null;
								try
								{
									type = SpawnerType.GetType(BaseXmlSpawner.ParseObjectType(attachstring));
								}
								catch { }

								// if so then create it
								if (type != null && type.IsSubclassOf(typeof(XmlAttachment)))
								{
									var newo = XmlSpawner.CreateObject(type, attachstring, false, true);
									if (newo is XmlAttachment)
										// add the attachment to the target
										XmlAttach.AttachTo(newcomponent, (XmlAttachment)newo);
								}
							}
					}

					sr.Close();
				}
			else
				status_str = "No such file : " + filename;

			if (badformat)
				return 0;
			else
				return ncomponents;
		}

		public override BaseAddonDeed Deed => null;

		public XmlSpawnerAddon()
		{
		}

		public XmlSpawnerAddon(Serial serial)
			: base(serial)
		{
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);
			writer.Write(1); // Version
			// version 1
			writer.Write(PartialVisibility);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);
			var version = reader.ReadInt();

			switch (version)
			{
				case 1:
					PartialVisibility = reader.ReadInt();
					break;
			}
		}
	}
}
