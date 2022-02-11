using System;
using System.IO;
using System.Collections;
using Server.Items;
using Server.Mobiles;
using Server.Commands;

namespace Server.Engines.XmlSpawner2
{
	public class WriteMulti
	{
		private class TileEntry
		{
			public int ID;
			public int X;
			public int Y;
			public int Z;

			public TileEntry(int id, int x, int y, int z)
			{
				ID = id;
				X = x;
				Y = y;
				Z = z;
			}
		}

		public static void Initialize()
		{
			CommandSystem.Register("WriteMulti", XmlSpawner.DiskAccessLevel,
				new CommandEventHandler(WriteMulti_OnCommand));
		}

		[Usage("WriteMulti <MultiFile> [zmin zmax][-noitems][-nostatics][-nomultis][-noaddons][-invisible]")]
		[Description(
			"Creates a multi text file from the objects within the targeted area.  The min/max z range can also be specified.")]
		public static void WriteMulti_OnCommand(CommandEventArgs e)
		{
			if (e == null || e.Mobile == null) return;

			if (e.Mobile.AccessLevel < XmlSpawner.DiskAccessLevel)
			{
				e.Mobile.SendMessage("You do not have rights to perform this command.");
				return;
			}

			if (e.Arguments != null && e.Arguments.Length < 1)
			{
				e.Mobile.SendMessage(
					"Usage:  {0} <MultiFile> [zmin zmax][-noitems][-nostatics][-nomultis][-noaddons][-invisible]",
					e.Command);
				return;
			}

			var filename = e.Arguments[0].ToString();

			var zmin = Int32.MinValue;
			var zmax = Int32.MinValue;
			var includeitems = true;
			var includestatics = true;
			var includemultis = true;
			var includeaddons = true;
			var includeinvisible = false;

			if (e.Arguments.Length > 1)
			{
				var index = 1;
				while (index < e.Arguments.Length)
					if (e.Arguments[index] == "-noitems")
					{
						includeitems = false;
						index++;
					}
					else if (e.Arguments[index] == "-nostatics")
					{
						includestatics = false;
						index++;
					}
					else if (e.Arguments[index] == "-nomultis")
					{
						includemultis = false;
						index++;
					}
					else if (e.Arguments[index] == "-noaddons")
					{
						includeaddons = false;
						index++;
					}
					else if (e.Arguments[index] == "-invisible")
					{
						includeinvisible = true;
						index++;
					}
					else
						try
						{
							zmin = Int32.Parse(e.Arguments[index++]);
							zmax = Int32.Parse(e.Arguments[index++]);
						}
						catch
						{
							e.Mobile.SendMessage("{0} : Invalid zmin zmax arguments", e.Command);
							return;
						}
			}

			string dirname;
			if (Directory.Exists(XmlSpawner.XmlSpawnDir) && filename != null && !filename.StartsWith("/") &&
			    !filename.StartsWith("\\"))
				// put it in the defaults directory if it exists
				dirname = String.Format("{0}/{1}", XmlSpawner.XmlSpawnDir, filename);
			else
				// otherwise just put it in the main installation dir
				dirname = filename;

			// check to see if the file already exists and can be written to by the owner
			if (File.Exists(dirname))
				// check the file
				try
				{
					var op = new StreamReader(dirname, false);

					if (op == null)
					{
						e.Mobile.SendMessage("Cannot access file {0}", dirname);
						return;
					}

					var line = op.ReadLine();

					op.Close();

					// check the first line
					if (line != null && line.Length > 0)
					{
						var args = line.Split(" ".ToCharArray(), 3);
						if (args == null || args.Length < 3)
						{
							e.Mobile.SendMessage("Cannot overwrite file {0} : not owner", dirname);
							return;
						}

						if (args[2] != e.Mobile.Name)
						{
							e.Mobile.SendMessage("Cannot overwrite file {0} : not owner", dirname);
							return;
						}
					}
					else
					{
						e.Mobile.SendMessage("Cannot overwrite file {0} : not owner", dirname);
						return;
					}
				}
				catch
				{
					e.Mobile.SendMessage("Cannot overwrite file {0}", dirname);
					return;
				}

			DefineMultiArea(e.Mobile, dirname, zmin, zmax, includeitems, includestatics, includemultis,
				includeinvisible, includeaddons);
		}

		public static void DefineMultiArea(Mobile m, string dirname, int zmin, int zmax, bool includeitems,
			bool includestatics,
			bool includemultis, bool includeinvisible, bool includeaddons)
		{
			var multiargs = new object[8];
			multiargs[0] = dirname;
			multiargs[1] = zmin;
			multiargs[2] = zmax;
			multiargs[3] = includeitems;
			multiargs[4] = includestatics;
			multiargs[5] = includemultis;
			multiargs[6] = includeinvisible;
			multiargs[7] = includeaddons;

			BoundingBoxPicker.Begin(m, new BoundingBoxCallback(DefineMultiArea_Callback), multiargs);
		}

		private static void DefineMultiArea_Callback(Mobile from, Map map, Point3D start, Point3D end, object state)
		{
			var multiargs = (object[])state;

			if (from != null && multiargs != null && map != null)
			{
				var dirname = (string)multiargs[0];
				var zmin = (int)multiargs[1];
				var zmax = (int)multiargs[2];
				var includeitems = (bool)multiargs[3];
				var includestatics = (bool)multiargs[4];
				var includemultis = (bool)multiargs[5];
				var includeinvisible = (bool)multiargs[6];
				var includeaddons = (bool)multiargs[7];

				var itemlist = new ArrayList();
				var staticlist = new ArrayList();
				var tilelist = new ArrayList();

				var sx = start.X > end.X ? end.X : start.X;
				var sy = start.Y > end.Y ? end.Y : start.Y;
				var ex = start.X < end.X ? end.X : start.X;
				var ey = start.Y < end.Y ? end.Y : start.Y;

				// find all of the world-placed items within the specified area
				if (includeitems)
				{
					// make the first pass for items only
					IPooledEnumerable eable = map.GetItemsInBounds(new Rectangle2D(sx, sy, ex - sx + 1, ey - sy + 1));

					foreach (Item item in eable)
						// is it within the bounding area
						if (item.Parent == null &&
						    (zmin == Int32.MinValue || item.Location.Z >= zmin && item.Location.Z <= zmax))
							// add the item
							if ((includeinvisible || item.Visible) && item.ItemID <= 16383)
								itemlist.Add(item);

					eable.Free();

					var searchrange = 100;

					// make the second expanded pass to pick up addon components and multi components
					eable = map.GetItemsInBounds(new Rectangle2D(sx - searchrange, sy - searchrange,
						ex - sy + searchrange * 2 + 1,
						ey - sy + searchrange * 2 + 1));

					foreach (Item item in eable)
						// is it within the bounding area
						if (item.Parent == null)
						{
							if (item is BaseAddon && includeaddons)
								// go through all of the addon components
								foreach (var c in ((BaseAddon)item).Components)
								{
									var x = c.X;
									var y = c.Y;
									var z = c.Z;

									if ((includeinvisible || item.Visible) && (item.ItemID <= 16383 || includemultis) &&
									    x >= sx && x <= ex && y >= sy && y <= ey &&
									    (zmin == Int32.MinValue || z >= zmin && z <= zmax)) itemlist.Add(c);
								}

							if (item is BaseMulti && includemultis)
							{
								// go through all of the multi components
								var mcl = ((BaseMulti)item).Components;
								if (mcl != null && mcl.List != null)
									for (var i = 0; i < mcl.List.Length; i++)
									{
										var t = mcl.List[i];

										var x = t.m_OffsetX + item.X;
										var y = t.m_OffsetY + item.Y;
										var z = t.m_OffsetZ + item.Z;
										var itemID = t.m_ItemID & 0x3FFF;

										if (x >= sx && x <= ex && y >= sy && y <= ey &&
										    (zmin == Int32.MinValue || z >= zmin && z <= zmax))
											tilelist.Add(new TileEntry(itemID, x, y, z));
									}
							}
						}

					eable.Free();
				}

				// find all of the static tiles within the specified area
				if (includestatics)
					// count the statics
					for (var x = sx; x < ex; x++)
					for (var y = sy; y < ey; y++)
					{
						var statics = map.Tiles.GetStaticTiles(x, y, false);

						for (var j = 0; j < statics.Length; j++)
							if (zmin == Int32.MinValue || statics[j].Z >= zmin && statics[j].Z <= zmax)
								staticlist.Add(new TileEntry(statics[j].ID & 0x3FFF, x, y, statics[j].Z));
					}

				var nstatics = staticlist.Count;
				var nitems = itemlist.Count;
				var ntiles = tilelist.Count;

				var ntotal = nitems + nstatics + ntiles;

				var ninvisible = 0;
				var nmultis = ntiles;
				var naddons = 0;

				foreach (Item item in itemlist)
				{
					var x = item.X - from.X;
					var y = item.Y - from.Y;
					var z = item.Z - from.Z;

					if (item.ItemID > 16383) nmultis++;
					if (!item.Visible) ninvisible++;
					if (item is BaseAddon || item is AddonComponent) naddons++;
				}

				try
				{
					// open the file, overwrite any previous contents
					var op = new StreamWriter(dirname, false);

					if (op != null)
					{
						// write the header
						op.WriteLine("1 version {0}", from.Name);
						op.WriteLine("{0} num components", ntotal);

						// write out the items
						foreach (Item item in itemlist)
						{
							var x = item.X - from.X;
							var y = item.Y - from.Y;
							var z = item.Z - from.Z;

							if (item.Hue > 0)
								// format is x y z visible hue
								op.WriteLine("{0} {1} {2} {3} {4} {5}", item.ItemID, x, y, z, item.Visible ? 1 : 0,
									item.Hue);
							else
								// format is x y z visible
								op.WriteLine("{0} {1} {2} {3} {4}", item.ItemID, x, y, z, item.Visible ? 1 : 0);
						}

						if (includestatics)
							foreach (TileEntry s in staticlist)
							{
								var x = s.X - @from.X;
								var y = s.Y - @from.Y;
								var z = s.Z - @from.Z;
								var ID = s.ID;
								op.WriteLine("{0} {1} {2} {3} {4}", ID, x, y, z, 1);
							}

						if (includemultis)
							foreach (TileEntry s in tilelist)
							{
								var x = s.X - @from.X;
								var y = s.Y - @from.Y;
								var z = s.Z - @from.Z;
								var ID = s.ID;
								op.WriteLine("{0} {1} {2} {3} {4}", ID, x, y, z, 1);
							}
					}

					op.Close();
				}
				catch
				{
					from.SendMessage("Error writing multi file {0}", dirname);
					return;
				}

				from.SendMessage(66, "WriteMulti results:");

				if (includeitems)
				{
					from.SendMessage(66, "Included {0} items", nitems);

					if (includemultis)
						@from.SendMessage("{0} multis", nmultis);
					else
						@from.SendMessage(33, "Ignored multis");

					if (includeinvisible)
						@from.SendMessage("{0} invisible", ninvisible);
					else
						@from.SendMessage(33, "Ignored invisible");

					if (includeaddons)
						@from.SendMessage("{0} addons", naddons);
					else
						@from.SendMessage(33, "Ignored addons");
				}
				else
					@from.SendMessage(33, "Ignored items");

				if (includestatics)
					@from.SendMessage(66, "Included {0} statics", nstatics);
				else
					@from.SendMessage(33, "Ignored statics");

				from.SendMessage(66, "Saved {0} components to {1}", ntotal, dirname);
			}
		}
	}
}
