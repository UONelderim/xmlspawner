//#define TRACE
//#define RESTRICTCONSTRUCTABLE

using System;
using System.Data;
using System.IO;
using System.Collections.Generic;
using Server.Items;
using Server.Network;
using Server.Targeting;
using System.Reflection;
using Server.Commands;
using Server.Commands.Generic;
using CPA = Server.CommandPropertyAttribute;
using System.Xml;
using System.Text;
using Server.Accounting;
using Server.ContextMenus;
using Server.Engines.XmlSpawner2;

/*
** XmlSpawner2
** version 4.00
** updated 10/7/13
** Blaze
**
** Originally modified by:
** ArteGordon
** Modification of the original XmlSpawner written by bobsmart
*/

namespace Server.Mobiles
{
	public class XmlSpawner : Item, ISpawner
	{
		#region Type declarations

		public enum TODModeType { Realtime, Gametime }

		public enum SpawnPositionType
		{
			Random, RowFill, ColFill, Perimeter, Player, Waypoint, RelXY, DeltaLocation, Location, Wet, Tiles, NoTiles,
			ItemID, NoItemID
		}

		public class SpawnPositionInfo
		{
			public SpawnPositionType positionType;
			public Mobile trigMob;
			public string[] positionArgs;

			public SpawnPositionInfo(SpawnPositionType positiontype, Mobile trigmob, string[] positionargs)
			{
				positionType = positiontype;
				trigMob = trigmob;
				positionArgs = positionargs;
			}
		}

		public class MovementInfo
		{
			public Mobile trigMob;
			public Point3D trigLocation = Point3D.Zero;

			public MovementInfo(Mobile m)
			{
				trigMob = m;
				if (m != null)
					trigLocation = m.Location;
			}
		}

		#endregion

		#region Constant declarations

		public const byte
			MaxLoops = 10; //maximum number of recursive calls from spawner to itself. this is to prevent stack overflow from xmlspawner scripting

		public const string Version = "4.00";

		//private const double SpawnIdleTime = 72.0;              // time in hours after which idle spawns will be relocated. A value < 0 disables this feature. This does not work properly under RunUO 2.0
		private const int ShowBoundsItemId = 14089; // 14089 Fire Column // 3555 Campfire // 8708 Skull Pole
		private const string SpawnDataSetName = "Spawns";
		private const string SpawnTablePointName = "Points";
		private const int SpawnFitSize = 16; // Normal wall/door height for a mobile is 20 to walk through
		private static int BaseItemId = 0x1F1C; // Purple Magic Crystal
		private static int ShowItemId = 0x3E57; // ships mast

		private static int
			defaultTriggerSound = 0x1F4; // click and sparkle sound by default  (0x1F4) , click sound (0x3A4)

		public static string
			XmlSpawnDir = "XmlSpawner"; // default directory for saving/loading .xml files with [xmlload [xmlsave

		public static string XmlMultiDir = "XmlMultis";

		private static string
			XmlConfigsDir = "XmlSpawnerConfigs"; // default directory for loading .xml config files with LoadConfig

		private const int
			MaxSmartSectorListSize =
				1024; // maximum sector list size for use in smart spawning. This gives a 512x512 tile range.

		private static string defwaypointname = null; // default waypoint name will get assigned in Initialize
		private const string XmlTableName = "Properties";
		private const string XmlDataSetName = "XmlSpawner";
		public static AccessLevel
			DiskAccessLevel =
				AccessLevel
					.Administrator; // minimum access level required by commands that can access the disk such as XmlLoad, XmlSave, and the Save function of XmlEdit
#if(RESTRICTCONSTRUCTABLE)
		public static AccessLevel ConstructableAccessLevel =
 AccessLevel.GameMaster; // only allow spawning of objects that have Constructable access restrictions at this level or lower. Must define RESTRICTCONSTRUCTABLE to enable this.
#endif
		private static int
			MaxMoveCheck = 10; // limit number of players that can be checked for triggering in a single OnMovement tick

		#endregion

		#region Static variable declarations

		// specifies the level at which smartspawning will be triggered.  Players with AccessLevel above this will not trigger smartspawning unless unhidden.
		public static AccessLevel SmartSpawnAccessLevel = AccessLevel.Player;

		// define the default values used in making spawners
		private static TimeSpan defMinDelay = TimeSpan.FromMinutes(5);
		private static TimeSpan defMaxDelay = TimeSpan.FromMinutes(10);
		private static TimeSpan defMinRefractory = TimeSpan.FromMinutes(0);
		private static TimeSpan defMaxRefractory = TimeSpan.FromMinutes(0);
		private static TimeSpan defTODStart = TimeSpan.FromMinutes(0);
		private static TimeSpan defTODEnd = TimeSpan.FromMinutes(0);
		private static TimeSpan defDuration = TimeSpan.FromMinutes(0);
		private static TimeSpan defDespawnTime = TimeSpan.FromHours(0);
		private static bool defIsGroup = false;
		private static int defTeam = 0;
		private static int defProximityTriggerSound = defaultTriggerSound;
		private static int defAmount = 1;
		private static bool defRelativeHome = true;
		private static int defSpawnRange = 5;
		private static int defHomeRange = 5;
		private static double defTriggerProbability = 1;
		private static int defProximityRange = -1;
		private static int defKillReset = 1;
		private static TODModeType defTODMode = TODModeType.Realtime;

		private static Timer m_GlobalSectorTimer;
		private static bool SmartSpawningSystemEnabled = false;

		private static WarnTimer2 m_WarnTimer;

		// hash table for optimizing HoldSmartSpawning method invocation
		private static Dictionary<Type, PropertyInfo> holdSmartSpawningHash;

		public static int seccount;

		// sector hashtable for each map
		private static Dictionary<Sector, List<XmlSpawner>>[] GlobalSectorTable =
			new Dictionary<Sector, List<XmlSpawner>>[6];

		#endregion

		#region Variable declarations

		private string m_Name = String.Empty;
		private string m_UniqueId = String.Empty;
		private bool m_PlayerCreated = false;
		private bool m_HomeRangeIsRelative = false;
		private int m_Team;

		private int m_HomeRange;

		// added a amount parameter for stacked item spawns
		private int m_StackAmount;

		// this is actually redundant with the width height spec for spawning area
		// just an easier way of specifying it
		private int m_SpawnRange;
		private int m_Count;
		private TimeSpan m_MinDelay;

		private TimeSpan m_MaxDelay;

		// added a duration parameter for time-limited spawns
		private TimeSpan m_Duration;
		public List<SpawnObject> m_SpawnObjects = new List<SpawnObject>(); // List of objects to spawn
		private DateTime m_End;
		private DateTime m_RefractEnd;
		private DateTime m_DurEnd;
		private SpawnerTimer m_Timer;
		private InternalTimer m_DurTimer;
		private InternalTimer3 m_RefractoryTimer;
		private bool m_Running;
		private bool m_Group;
		private int m_X;
		private int m_Y;
		private int m_Width;
		private int m_Height;
		private WayPoint m_WayPoint;

		private Static m_ShowContainerStatic;
		private bool m_proximityActivated;
		private bool m_refractActivated;
		private bool m_durActivated;
		private TimeSpan m_TODStart;

		private TimeSpan m_TODEnd;

		// time after proximity activation when the spawn cannot be reactivated
		private TimeSpan m_MinRefractory;
		private TimeSpan m_MaxRefractory;
		private string m_ItemTriggerName;
		private string m_NoItemTriggerName;
		private Item m_ObjectPropertyItem;
		private string m_ObjectPropertyName;
		public string status_str;

		public int m_killcount;

		// added proximity range sensor
		private int m_ProximityRange;

		// sound played when a proximity triggered spawner is tripped by a player
		// set this to zero if you dont want to hear anything
		private int m_ProximityTriggerSound;
		private string m_ProximityTriggerMessage;
		private string m_SpeechTrigger;
		private bool m_speechTriggerActivated;
		private string m_MobPropertyName;
		private string m_MobTriggerName;
		private string m_PlayerPropertyName;
		private double m_TriggerProbability = defTriggerProbability;
		private Mobile m_mob_who_triggered;
		private Item m_SetPropertyItem;

		private bool m_skipped = false;

		private int
			m_KillReset =
				defKillReset; // number of spawn ticks that pass without kills before killcount gets reset to zero

		private int m_spawncheck = 0;
		private TODModeType m_TODMode = TODModeType.Realtime;
		private string m_GumpState;
		private bool m_ExternalTriggering;
		private bool m_ExternalTrigger;
		private int m_SequentialSpawning = -1; // off by default
		private DateTime m_SeqEnd;
		private Region m_Region; // 2004.02.08 :: Omega Red
		private string m_RegionName = String.Empty; // 2004.02.08 :: Omega Red
		private AccessLevel m_TriggerAccessLevel = AccessLevel.Player;

		public List<XmlTextEntryBook> m_TextEntryBook;
		private XmlSpawnerGump m_SpawnerGump;

		private bool m_AllowGhostTriggering = false;
		private bool m_AllowNPCTriggering = false;
		private string m_ConfigFile;
		private bool m_OnHold = false;
		private bool m_HoldSequence = false;
		private bool m_SpawnOnTrigger = false;

		private DateTime m_FirstModified;
		private DateTime m_LastModified;

		private List<MovementInfo> m_MovementList;
		private MovementTimer m_MovementTimer;
		internal List<BaseXmlSpawner.KeywordTag> m_KeywordTagList = new List<BaseXmlSpawner.KeywordTag>();
		private string m_FirstModifiedBy = null;
		private string m_LastModifiedBy = null;

		public List<XmlSpawner> RecentSpawnerSearchList = null;
		public List<Item> RecentItemSearchList = null;
		public List<Mobile> RecentMobileSearchList = null;
		private TimeSpan m_DespawnTime;

		private string m_SkillTrigger;
		private bool m_skillTriggerActivated;
		private SkillName m_skill_that_triggered;
		private bool m_FreeRun = false; // override for all other triggering modes
		private SkillName m_SkillTriggerName;
		private double m_SkillTriggerMin;
		private double m_SkillTriggerMax;
		private int m_SkillTriggerSuccess;
		private Map currentmap;

		public bool m_IsInactivated = false;
		private bool m_SmartSpawning = false;
		private SectorTimer m_SectorTimer;

		private List<Static> m_ShowBoundsItems = new List<Static>();

		public List<BaseXmlSpawner.TypeInfo>
			PropertyInfoList = null; // used to optimize property info lookup used by set and get property methods.

		private Dictionary<string, List<Item>> spawnPositionWayTable = null; // used to optimize #waypoint lookup

		private bool inrespawn = false;

		private List<Sector> sectorList = null;

		private bool m_DisableGlobalAutoReset;

		private Point3D mostRecentSpawnPosition = Point3D.Zero;

		private int m_MovingPlayerCount = 0;
		private int m_FastestPlayerSpeed = 0;

		private bool m_DebugThis = false;

		private TimerPriority m_BasePriority = TimerPriority.OneSecond;

		#endregion

		#region Property Overrides

		// does not decay
		public override bool Decays => false;

		// is not counted in the normal item count
		public override bool IsVirtualItem => true;

		#endregion

		#region Properties

		public TimerPriority BasePriority
		{
			get => m_BasePriority;
			set => m_BasePriority = value;
		}

		public bool DebugThis
		{
			get => m_DebugThis;
			set => m_DebugThis = value;
		}

		public int MovingPlayerCount
		{
			get => m_MovingPlayerCount;
			set => m_MovingPlayerCount = value;
		}

		public int FastestPlayerSpeed
		{
			get => m_FastestPlayerSpeed;
			set => m_FastestPlayerSpeed = value;
		}

		public int NearbyPlayerCount
		{
			get
			{
				var count = 0;
				if (ProximityRange >= 0)
					foreach (var m in GetMobilesInRange(ProximityRange))
						if (m != null && m.Player)
							count++;
				return count;
			}
		}

		public Point3D MostRecentSpawnPosition
		{
			get => mostRecentSpawnPosition;
			set => mostRecentSpawnPosition = value;
		}

		public TimeSpan GameTOD
		{
			get
			{
				int hours;
				int minutes;

				Clock.GetTime(Map, Location.X, Location.Y, out hours, out minutes);
				return new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, hours, minutes, 0)
					.TimeOfDay;
			}
		}

		public TimeSpan RealTOD => DateTime.Now.TimeOfDay;

		public int RealDay => DateTime.Now.Day;

		public int RealMonth => DateTime.Now.Month;

		public DayOfWeek RealDayOfWeek => DateTime.Now.DayOfWeek;

		public MoonPhase MoonPhase => Clock.GetMoonPhase(Map, Location.X, Location.Y);

		public XmlSpawnerGump SpawnerGump
		{
			get => m_SpawnerGump;
			set => m_SpawnerGump = value;
		}

		public bool DisableGlobalAutoReset
		{
			get => m_DisableGlobalAutoReset;
			set => m_DisableGlobalAutoReset = value;
		}

		public bool DoDefrag
		{
			get => false;
			set
			{
				if (value == true) Defrag(true);
			}
		}

		private bool sectorIsActive = false;
		private bool UseSectorActivate = false;

		public bool SingleSector => UseSectorActivate;

		/*
		public override void OnSectorDeactivate()
		{
			sectorIsActive = false;
			base.OnSectorDeactivate();
		}

		public override void OnSectorActivate()
		{
			sectorIsActive = true;

			base.OnSectorActivate();

			// perform the smart respawning
			if(SmartSpawning && IsInactivated && UseSectorActivate)
			{
				SmartRespawn();
			}

		}
		*/

		public bool InActivationRange(Sector s1, Sector s2)
		{
			// check to see if the sectors are within +- 2 of one another
			if (s1 == null || s2 == null) return false;

			return Math.Abs(s1.X - s2.X) < 3 && Math.Abs(s1.Y - s2.Y) < 3;
		}

		public bool HasDamagedOrDistantSpawns
		{
			get
			{
				var ssec = Map.GetSector(Location);
				// go through the spawn lists
				foreach (var so in m_SpawnObjects)
					for (var x = 0; x < so.SpawnedObjects.Count; x++)
					{
						var o = so.SpawnedObjects[x];

						if (o is BaseCreature)
						{
							var b = (BaseCreature)o;

							// if the mob is damaged or outside of smartspawning detection range then return true
							if (b.Hits < b.HitsMax || b.Mana < b.ManaMax || b.Stam < b.StamMax || b.Map != Map)
								return true;

							// if the spawn moves into a sector that is not activatable from a sector on the sector list then dont smartspawn
							if (b.Map != null && b.Map != Map.Internal)
							{
								var bsec = b.Map.GetSector(b.Location);

								if (UseSectorActivate)
								{
									// is it in activatable range of the sector the spawner is in
									if (!InActivationRange(bsec, ssec)) return true;
								}
								else
								{
									var outofsec = true;

									if (sectorList != null)
										foreach (var s in sectorList)
											// is the creatures sector within activation range of any of the sectors in the list
											if (InActivationRange(bsec, s))
											{
												outofsec = false;
												break;
											}

									if (outofsec) return true;
								}
							}
						}
					}

				return false;
			}
		}

		private static int totalSectorsMonitored = 0;


		public bool HasActiveSectors
		{
			get
			{
				if (!SmartSpawning || Map == null || Map == Map.Internal) return false;

				// is this a region spawner?
				if (m_Region != null)
				{
					// confirm that players with the proper access level are present
					foreach (var m in m_Region.AllPlayers)
						if (m != null && (m.AccessLevel <= SmartSpawnAccessLevel || !m.Hidden))
							return true;
					return false;
				}

				// is this a single sector spawner?
				if (UseSectorActivate) return sectorIsActive;

				// if there is no sector list made for this spawner then create one.
				if (sectorList == null)
				{
					var loc = Location;
					sectorList = new List<Sector>();

					// is this container held?
					if (Parent != null)
					{
						if (RootParent is Mobile)
							loc = ((Mobile)RootParent).Location;
						else if (RootParent is Item)
							loc = ((Item)RootParent).Location;
					}

					// find the max detection range by examining both spawnrange 
					// note, sectors will activate when within +-2 sectors
					var bufferzone = 2 * Map.SectorSize;
					var x1 = m_X - bufferzone;
					var width = m_Width + 2 * bufferzone;
					var y1 = m_Y - bufferzone;
					var height = m_Height + 2 * bufferzone;

					// go through all of the sectors within the SpawnRange of the spawner to see if any are active
					for (var x = x1; x <= x1 + width; x += Map.SectorSize)
					for (var y = y1; y <= y1 + height; y += Map.SectorSize)
					{
						var s = Map.GetSector(new Point3D(x, y, loc.Z));

						if (s == null) continue;

						// dont add any redundant sectors
						var duplicate = false;
						foreach (var olds in sectorList)
							if (olds == s)
							{
								duplicate = true;
								break;
							}

						if (!duplicate)
						{
							sectorList.Add(s);

							if (GlobalSectorTable[Map.MapID] == null)
								GlobalSectorTable[Map.MapID] = new Dictionary<Sector, List<XmlSpawner>>();

							// add this sector and the spawner associated with it to the global sector table
							List<XmlSpawner> spawnerlist;
							if (GlobalSectorTable[Map.MapID].TryGetValue(s, out spawnerlist)) //.Contains(s))
							{
								//List<XmlSpawner> spawnerlist = GlobalSectorTable[Map.MapID][s];
								if (spawnerlist == null)
								{
									//GlobalSectorTable[Map.MapID].Remove(s);
									spawnerlist = new List<XmlSpawner>();
									//GlobalSectorTable[Map.MapID].Add(s, spawnerlist);
									GlobalSectorTable[Map.MapID][s] = spawnerlist;
								}

								if (!spawnerlist.Contains(this)) spawnerlist.Add(this);
							}
							else
							{
								spawnerlist = new List<XmlSpawner>();
								spawnerlist.Add(this);
								// add a new entry to the table
								GlobalSectorTable[Map.MapID][s] = spawnerlist;
							}

							totalSectorsMonitored++;

							// add some sanity checking here
							if (sectorList.Count > MaxSmartSectorListSize)
							{
								SmartSpawning = false;

								// log it
								try
								{
									Console.WriteLine("SmartSpawning disabled at {0} {1} : Range too large.", loc, Map);

									using (var op = new StreamWriter("badspawn.log", true))
									{
										op.WriteLine("{0} SmartSpawning disabled at {1} {2} : Range too large.",
											DateTime.Now, loc, Map);
										op.WriteLine();
									}
								}
								catch
								{
								}

								return true;
							}
						}
					}

					// if the spawner is in a single sector, then we can use the OnSectorActivation method to test for activity
					// note, sectors will activate when within +-2 sectors
					/*
										if((sectorList.Count == 1) && (Location.X >= m_X) && (Location.X <= m_X + m_Width) &&
											(Location.Y >= m_Y) && (Location.Y <= m_Y + m_Height))
										{

											UseSectorActivate = true;
										}
										else
										{
											UseSectorActivate = false;
										}
										*/
					UseSectorActivate = false;
				}

				_TraceStart(2);
				// go through the sectorlist and see if any of the sectors are active

				foreach (var s in sectorList)
				{
					if (s != null && s.Active && s.Players != null)
					{
						// confirm that players with the proper access level are present
						foreach (var m in s.Players)
							if (m != null && (m.AccessLevel <= SmartSpawnAccessLevel || !m.Hidden))
								return true;
						_TraceEnd(2);
					}

					seccount++;
				}

				_TraceEnd(2);
				return false;
			}
		}

		public int SecCount => seccount;


		public bool IsInactivated
		{
			get => m_IsInactivated;
			set => m_IsInactivated = value;
		}


		public int ActiveSectorCount
		{
			get
			{
				if (sectorList != null) return sectorList.Count;
				return 0;
			}
		}

		public DateTime FirstModified => m_FirstModified;

		public DateTime LastModified => m_LastModified;

		public bool PlayerCreated
		{
			get => m_PlayerCreated;
			set => m_PlayerCreated = value;
		}

		public bool OnHold
		{
			get
			{
				if (m_OnHold) return true;

				// determine whether there are any keywordtags with the hold flag
				if (m_KeywordTagList == null || m_KeywordTagList.Count == 0) return false;

				foreach (var sot in m_KeywordTagList)
					// check for any keyword tag with the holdspawn flag
					if (sot != null && !sot.Deleted && (sot.Flags & BaseXmlSpawner.KeywordFlags.HoldSpawn) != 0)
						return true;
				// no hold flags were set
				return false;
			}
			set => m_OnHold = value;
		}

		public string AddSpawn
		{
			get => null;
			set
			{
				if (!String.IsNullOrEmpty(value))
				{
					var str = value.Trim();
					var typestr = BaseXmlSpawner.ParseObjectType(str);

					var type = SpawnerType.GetType(typestr);

					if (type != null)
						m_SpawnObjects.Add(new SpawnObject(str, 1));
					else
					{
						// check for special keywords
						if (typestr != null && (BaseXmlSpawner.IsTypeOrItemKeyword(typestr) ||
						                        typestr.IndexOf("{") != -1 || typestr.StartsWith("*") ||
						                        typestr.StartsWith("#")))
							m_SpawnObjects.Add(new SpawnObject(str, 1));
						else
							status_str = String.Format("{0} is not a valid type name.", str);
					}

					InvalidateProperties();
				}
			}
		}

		public Skill TriggerSkill
		{
			get
			{
				if (TriggerMob != null && (int)m_skill_that_triggered >= 0)
					return TriggerMob.Skills[m_skill_that_triggered];
				else
					return null;
			}
			set
			{
				if (value != null)
					m_skill_that_triggered = value.SkillName;
				else
					m_skill_that_triggered = XmlSpawnerSkillCheck.RegisteredSkill.Invalid;
			}
		}

		public string UniqueId => m_UniqueId;

		// does not perform a defrag, so less accurate but can be used while looping through world object enums
		public int SafeCurrentCount => SafeTotalSpawnedObjects;


		public bool FreeRun
		{
			get => m_FreeRun;
			set => m_FreeRun = value;
		}

		public bool CanFreeSpawn
		{
			get
			{
				// allow free spawning if proximity sensing is off and if all of the potential free-spawning triggers are disabled
				if (Running && m_ProximityRange == -1 &&
				    (m_ObjectPropertyName == null || m_ObjectPropertyName.Length == 0) &&
				    (m_MobPropertyName == null || m_MobPropertyName.Length == 0 ||
				     m_MobTriggerName == null || m_MobTriggerName.Length == 0) &&
				    !m_ExternalTriggering)
					return true;
				else
					return false;
			}
		}


		public SpawnObject[] SpawnObjects
		{
			get => m_SpawnObjects.ToArray();
			set
			{
				if (value != null && value.Length > 0)
				{
					foreach (var so in value)
					{
						if (so == null) continue;
						var AlreadyInList = false;

						// Check if the new array has an existing spawn object
						foreach (var TheSpawn in m_SpawnObjects)
							if (TheSpawn.TypeName.ToUpper() == so.TypeName.ToUpper())
							{
								AlreadyInList = true;
								break;
							}

						// Does this item need to be added
						if (!AlreadyInList)
							// This is a new spawn object so add it to the array (deep copy)
							m_SpawnObjects.Add(new SpawnObject(so.TypeName, so.ActualMaxCount, so.SubGroup,
								so.SequentialResetTime, so.SequentialResetTo, so.KillsNeeded,
								so.RestrictKillsToSubgroup, so.ClearOnAdvance, so.MinDelay, so.MaxDelay,
								so.SpawnsPerTick, so.PackRange));
					}

					if (SpawnObjects.Length < 1)
						Stop();

					InvalidateProperties();
				}
			}
		}

		public bool HoldSequence
		{
			get
			{
				// check to see if any keyword tags have the holdsequence flag set, or whether the spawner holdsequence flag is set
				if (m_HoldSequence) return true;

				// determine whether there are any keywordtags with the hold flag
				if (m_KeywordTagList == null || m_KeywordTagList.Count == 0) return false;

				foreach (var sot in m_KeywordTagList)
					// check for any keyword tag with the holdsequence flag
					if (sot != null && !sot.Deleted && (sot.Flags & BaseXmlSpawner.KeywordFlags.HoldSequence) != 0)
						return true;

				// no hold flags were set
				return false;
			}
			set => m_HoldSequence = value;
		}

		public bool CanSpawn
		{
			get
			{
				if (OnHold) return false;


				if (m_Group == true)
				{
					if (TotalSpawnedObjects <= 0)
						return true;
					else
						return false;
				}
				else
				{
					if (IsFull)
						return false;
					else
						return true;
				}
			}
		}

		// test for a full spawner
		public bool IsFull
		{
			get
			{
				var nobj = TotalSpawnedObjects;

				return nobj >= m_Count || nobj >= TotalSpawnObjectCount;
			}
		}

		// this can be used in loops over world objects since it will not defrag and potentially modify the world object lists
		public int SafeTotalSpawnedObjects
		{
			get
			{
				if (m_SpawnObjects == null) return 0;

				var count = 0;

				foreach (var so in m_SpawnObjects)
					count += so.SpawnedObjects.Count;

				return count;
			}
		}

		public int TotalSpawnedObjects
		{
			get
			{
				if (m_SpawnObjects == null) return 0;

				// defrag so that accurately reflects currently active spawns
				Defrag(true);

				var count = 0;

				foreach (var so in m_SpawnObjects)
					count += so.SpawnedObjects.Count;

				return count;
			}
		}

		public bool isEmpty()
		{
			if (m_SpawnObjects == null) return true;

			foreach (var so in m_SpawnObjects)
				if (so.SpawnedObjects != null && so.SpawnedObjects.Count > 0)
					if (so.SpawnedObjects[0] is Mobile)
						return false;
			return true;
		}

		public int TotalSpawnObjectCount
		{
			get
			{
				var count = 0;

				foreach (var so in m_SpawnObjects)
					count += so.MaxCount;

				return count;
			}
		}

		#endregion

		#region Command Properties

		[CommandProperty(AccessLevel.GameMaster)]
		public bool GumpReset
		{
			set
			{
				if (value == true) m_SpawnerGump = null;
			}
		}

		public Region SpawnRegion
		{
			get => m_Region;
			set
			{
				// force a re-update of the smart spawning sector list the next time it is accessed
				ResetSectorList();

				m_Region = value;

				if (m_Region != null)
					m_RegionName = m_Region.Name;
				else
					m_RegionName = null;
			}
		}

		// 2004.02.08 :: Omega Red
		[CommandProperty(AccessLevel.GameMaster)]
		public string RegionName
		{
			get => m_RegionName;
			set
			{
				// force a re-update of the smart spawning sector list the next time it is accessed
				ResetSectorList();

				m_RegionName = value;

				if (String.IsNullOrEmpty(value))
				{
					m_Region = null;
					return;
				}

				if (Region.Regions.Count == 0) // after world load, before region load
					return;

				foreach (var region in Region.Regions)
					if (String.Compare(region.Name, m_RegionName, true) == 0)
					{
						m_Region = region;
						m_RegionName = region.Name;
						//InvalidateProperties();
						return;
					}

				status_str = "invalid region: " + value;
				m_Region = null;
			}
		}


		[CommandProperty(AccessLevel.GameMaster)]
		public Point3D X1_Y1
		{
			get => new Point3D(m_X, m_Y, Z);
			set
			{
				// X1 and Y1 will initiate region specification
				m_Width = 0;
				m_Height = 0;
				m_X = value.X;
				m_Y = value.Y;

				// reset the sector list
				ResetSectorList();

				m_SpawnRange = 0;

				// Check if the spawner is showing its bounds
				if (ShowBounds == true)
				{
					ShowBounds = false;
					ShowBounds = true;
				}
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public Point3D X2_Y2
		{
			get => new Point3D(m_X + m_Width, m_Y + m_Height, Z);
			set
			{
				int X2;
				int Y2;

				var OriginalX2 = m_X + m_Width;
				var OriginalY2 = m_Y + m_Height;

				// reset the sector list
				ResetSectorList();

				// now determine based upon the entered coordinate values what the lower left corner is
				// lower left will be the min x and min y
				// upper right will be max x max y
				if (value.X < OriginalX2)
				{
					// ok, this is the proper x value for the lower left
					m_X = value.X;
					X2 = OriginalX2;
				}
				else
				{
					m_X = OriginalX2;
					X2 = value.X;
				}

				if (value.Y < OriginalY2)
				{
					// ok, this is the proper y value for the lower left
					m_Y = value.Y;
					Y2 = OriginalY2;
				}
				else
				{
					m_Y = OriginalY2;
					Y2 = value.Y;
				}

				m_Width = X2 - m_X;
				m_Height = Y2 - m_Y;

				if (m_Width == m_Height)
					m_SpawnRange = m_Width / 2;
				else
					m_SpawnRange = -1;

				if (m_HomeRangeIsRelative == false)
				{
					var NewHomeRange = m_Width > m_Height ? m_Height : m_Width;
					m_HomeRange = NewHomeRange > 0 ? NewHomeRange : 0;
				}

				//original test was for less than 1, changed it to less than zero (zero is a valid width, its the default in fact)
				// Stop the spawner if the width or height is less than 1
				if (m_Width < 0 || m_Height < 0)
					Running = false;

				InvalidateProperties();

				// Check if the spawner is showing its bounds
				if (ShowBounds == true)
				{
					ShowBounds = false;
					ShowBounds = true;
				}
			}
		}

		// added the spawnrange property.  It sets both the XY and width/height parameters automatically.
		// also doesnt mess with homerange like XY does
		[CommandProperty(AccessLevel.GameMaster)]
		public int SpawnRange
		{
			get => m_SpawnRange;
			set
			{
				if (value < 0) return;

				// reset the sector list
				ResetSectorList();

				m_SpawnRange = value;
				m_Width = m_SpawnRange * 2;
				m_Height = m_SpawnRange * 2;

				// dont set the bounding box locations if the initial location is 0,0 since this occurs when the item is just being made
				// because m_X and m_Y are restored on loading, it creates problems with OnLocationChange which has to avoid applying translational
				// adjustments to newly placed spawners (because the actual m_X and m_Y is associated with the original location, not the 0,0 location)
				// basically, before placement, dont set m_X or m_Y to anything that needs to be adjusted later on

				if (Location.X == 0 && Location.Y == 0) return;

				m_X = Location.X - m_SpawnRange;
				m_Y = Location.Y - m_SpawnRange;

				//InvalidateProperties();

				// Check if the spawner is showing its bounds
				if (ShowBounds == true)
				{
					ShowBounds = false;
					ShowBounds = true;
				}
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool ShowBounds
		{
			get => m_ShowBoundsItems != null && m_ShowBoundsItems.Count > 0;
			set
			{
				if (value == true && ShowBounds == false)
				{
					if (m_ShowBoundsItems == null) m_ShowBoundsItems = new List<Static>();

					// Boundary lines
					var ValidX1 = m_X;
					var ValidX2 = m_X + m_Width;
					var ValidY1 = m_Y;
					var ValidY2 = m_Y + m_Height;

					for (var x = 0; x <= m_Width; x++)
					{
						var NewX = m_X + x;
						for (var y = 0; y <= m_Height; y++)
						{
							var NewY = m_Y + y;

							if (NewX == ValidX1 || NewX == ValidX2 || NewX == ValidY1 || NewX == ValidY2 ||
							    NewY == ValidX1 || NewY == ValidX2 || NewY == ValidY1 || NewY == ValidY2)
							{
								// Add an object to show the spawn area
								var s = new Static(ShowBoundsItemId);
								s.Visible = false;
								s.MoveToWorld(new Point3D(NewX, NewY, Z), Map);
								m_ShowBoundsItems.Add(s);
							}
						}
					}
				}

				if (value == false && m_ShowBoundsItems != null)
				{
					// Remove all of the items from the array
					foreach (var s in m_ShowBoundsItems)
						s.Delete();

					m_ShowBoundsItems.Clear();
				}
			}
		}

		/*
		[CommandProperty(AccessLevel.GameMaster)]
		public override string Name
		{
			get { return m_Name; }
			set
			{
				m_Name = value;
				InvalidateProperties();
			}
		}
		*/

		[CommandProperty(AccessLevel.GameMaster)]
		public int MaxCount
		{
			get => m_Count;
			set
			{
				m_Count = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int CurrentCount => TotalSpawnedObjects;

		[CommandProperty(AccessLevel.GameMaster)]
		public WayPoint WayPoint
		{
			get => m_WayPoint;
			set => m_WayPoint = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool ExternalTriggering
		{
			get => m_ExternalTriggering;
			set => m_ExternalTriggering = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool ExtTrigState
		{
			get => m_ExternalTrigger;
			set => m_ExternalTrigger = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool Running
		{
			get => m_Running;
			set
			{
				// Don't start the spawner unless the height and width are valid
				if (value == true && m_Width >= 0 && m_Height >= 0)
					Start();
				else
					Stop();

				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HomeRange
		{
			get => m_HomeRange;
			set
			{
				m_HomeRange = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool HomeRangeIsRelative
		{
			get => m_HomeRangeIsRelative;
			set => m_HomeRangeIsRelative = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int Team
		{
			get => m_Team;
			set
			{
				m_Team = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int StackAmount
		{
			get => m_StackAmount;
			set => m_StackAmount = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan MinDelay
		{
			get => m_MinDelay;
			set
			{
				m_MinDelay = value;
				// reset the spawn timer
				DoTimer();
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan MaxDelay
		{
			get => m_MaxDelay;
			set
			{
				m_MaxDelay = value;
				// reset the spawn timer
				DoTimer();
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int KillCount
		{
			get => m_killcount;
			set => m_killcount = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int KillReset
		{
			get => m_KillReset;
			set => m_KillReset = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public double TriggerProbability
		{
			get => m_TriggerProbability;
			set => m_TriggerProbability = value;
		}

		//added refractory period support
		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan RefractMin
		{
			get => m_MinRefractory;
			set => m_MinRefractory = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan RefractMax
		{
			get => m_MaxRefractory;
			set => m_MaxRefractory = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan RefractoryOver
		{
			get
			{
				if (m_refractActivated)
					return m_RefractEnd - DateTime.Now;
				else
					return TimeSpan.FromSeconds(0);
			}
			set => DoTimer3(value);
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string TriggerOnCarried
		{
			get => m_ItemTriggerName;
			set => m_ItemTriggerName = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string NoTriggerOnCarried
		{
			get => m_NoItemTriggerName;
			set => m_NoItemTriggerName = value;
		}


		[CommandProperty(AccessLevel.GameMaster)]
		public string TriggerObjectProp
		{
			get => m_ObjectPropertyName;
			set => m_ObjectPropertyName = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string TriggerObjectName
		{
			get
			{
				if (m_ObjectPropertyItem == null || m_ObjectPropertyItem.Deleted) return null;
				return m_ObjectPropertyItem.Name;
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public Item TriggerObject
		{
			get => m_ObjectPropertyItem;
			set => m_ObjectPropertyItem = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string SetItemName
		{
			get
			{
				if (m_SetPropertyItem == null || m_SetPropertyItem.Deleted) return null;
				return m_SetPropertyItem.Name;
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public Item SetItem
		{
			get => m_SetPropertyItem;
			set => m_SetPropertyItem = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string MobTriggerProp
		{
			get => m_MobPropertyName;
			set => m_MobPropertyName = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string MobTriggerName
		{
			get => m_MobTriggerName;
			set => m_MobTriggerName = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public Mobile MobTriggerId
		{
			get
			{
				if (m_MobTriggerName == null) return null;

				// try to parse out the type information if it has also been saved
				var typeargs = m_MobTriggerName.Split(",".ToCharArray(), 2);
				string typestr = null;
				var namestr = m_MobTriggerName;

				if (typeargs.Length > 1)
				{
					namestr = typeargs[0];
					typestr = typeargs[1];
				}

				return BaseXmlSpawner.FindMobileByName(this, namestr, typestr);
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string PlayerTriggerProp
		{
			get => m_PlayerPropertyName;
			set => m_PlayerPropertyName = value;
		}

		// time of day activation
		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan TODStart
		{
			get => m_TODStart;
			set => m_TODStart = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan TODEnd
		{
			get => m_TODEnd;
			set => m_TODEnd = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan TOD
		{
			get
			{
				if (m_TODMode == TODModeType.Gametime)
				{
					int hours;
					int minutes;
					Clock.GetTime(Map, Location.X, Location.Y, out hours, out minutes);
					return new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, hours, minutes, 0)
						.TimeOfDay;
				}
				else
					return DateTime.Now.TimeOfDay;
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TODModeType TODMode
		{
			get => m_TODMode;
			set => m_TODMode = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool TODInRange
		{
			get
			{
				if (m_TODStart == m_TODEnd) return true;
				DateTime now;
				DateTime TOD_start;
				DateTime TOD_end;
				DateTime day_start;

				if (m_TODMode == TODModeType.Gametime)
				{
					int hours;
					int minutes;
					Clock.GetTime(Map, Location.X, Location.Y, out hours, out minutes);
					now = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, hours, minutes, 0);
				}
				else
					// calculate the time window
					now = DateTime.Now;

				day_start = new DateTime(now.Year, now.Month, now.Day);
				// calculate the starting TOD window by adding the TODStart to day_start
				TOD_start = day_start + m_TODStart;
				TOD_end = day_start + m_TODEnd;


				// handle the case when TODstart is before midnight and end is after

				if (TOD_start > TOD_end)
				{
					if (now > TOD_start || now < TOD_end)
						return true;
					else
						return false;
				}
				else
				{
					if (now > TOD_start && now < TOD_end)
						return true;
					else
						return false;
				}
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan DespawnTime
		{
			get => m_DespawnTime;
			set => m_DespawnTime = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan Duration
		{
			get => m_Duration;
			set
			{
				m_Duration = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan DurationOver
		{
			get
			{
				if (m_durActivated)
					return m_DurEnd - DateTime.Now;
				else
					return TimeSpan.FromSeconds(0);
			}
			set => DoTimer2(value);
		}

		// proximity range parameter
		[CommandProperty(AccessLevel.GameMaster)]
		public int ProximityRange
		{
			get => m_ProximityRange;
			set
			{
				m_ProximityRange = value;
				InvalidateProperties();
			}
		}


		// proximity range activated?
		[CommandProperty(AccessLevel.GameMaster)]
		public bool ProximityActivated
		{
			get => m_proximityActivated;
			set
			{
				if (AllowTriggering) ActivateTrigger();

				m_proximityActivated = value;
			}
		}

		// proximity trigger sound parameter
		[CommandProperty(AccessLevel.GameMaster)]
		public int ProximitySound
		{
			get => m_ProximityTriggerSound;
			set => m_ProximityTriggerSound = value;
		}

		// proximity trigger message parameter
		[CommandProperty(AccessLevel.GameMaster)]
		public string ProximityMsg
		{
			get => m_ProximityTriggerMessage;
			set => m_ProximityTriggerMessage = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string SpeechTrigger
		{
			get => m_SpeechTrigger;
			set => m_SpeechTrigger = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string SkillTrigger
		{
			get => m_SkillTrigger;
			set
			{
				var news = XmlSpawnerSkillCheck.RegisteredSkill.Invalid;
				double minval = -1;
				double maxval = -1;
				var successval = 3; // either success or failure will trigger

				if (value != null)
				{
					// try parsing the skill trigger string for min and maxval
					// the string can take the form "skill,[+/-][,minval,maxval]"

					var arglist = BaseXmlSpawner.ParseString(value, 4, ",");

					if (arglist.Length == 2 || arglist.Length == 4)
					{
						if (arglist[1] == "+")
							successval = 1; // trigger on success only
						else if (arglist[1] == "-") successval = 2; // trigger on failure only
					}

					if (arglist.Length == 3)
					{
						successval = 3;
						try
						{
							minval = Double.Parse(arglist[1]);
							maxval = Double.Parse(arglist[2]);
						}
						catch { }
					}
					else if (arglist.Length == 4)
						try
						{
							minval = Double.Parse(arglist[2]);
							maxval = Double.Parse(arglist[3]);
						}
						catch { }

					try
					{
						news = (SkillName)Enum.Parse(typeof(SkillName), arglist[0], true);
					}
					catch { }
				}

				// unregister the previous skill if it was assigned
				if (m_SkillTrigger != null)
					XmlSpawnerSkillCheck.UnRegisterSkillTrigger(this, m_SkillTriggerName, Map, false);

				// if the skill trigger was valid then register it
				if (news != XmlSpawnerSkillCheck.RegisteredSkill.Invalid)
				{
					XmlSpawnerSkillCheck.RegisterSkillTrigger(this, news, Map);
					m_SkillTrigger = value;
					m_SkillTriggerName = news;
					m_SkillTriggerMin = minval;
					m_SkillTriggerMax = maxval;
					m_SkillTriggerSuccess = successval;
				}
				else
				{
					m_SkillTrigger = null;
					m_SkillTriggerName = XmlSpawnerSkillCheck.RegisteredSkill.Invalid;
					m_SkillTriggerMin = -1;
					m_SkillTriggerMax = -1;
				}
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan NextSpawn
		{
			get
			{
				if (m_Running)
					return m_End - DateTime.Now;
				else
					return TimeSpan.FromSeconds(0);
			}
			set
			{
				Start();
				DoTimer(value);
				//InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool SpawnOnTrigger
		{
			get => m_SpawnOnTrigger;
			set => m_SpawnOnTrigger = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool Group
		{
			get => m_Group;
			set
			{
				m_Group = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string GumpState
		{
			get => m_GumpState;
			set => m_GumpState = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int SequentialSpawn
		{
			get => m_SequentialSpawning;
			set => m_SequentialSpawning = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan NextSeqReset
		{
			get
			{
				if (m_Running && m_SeqEnd - DateTime.Now > TimeSpan.Zero)
					return m_SeqEnd - DateTime.Now;
				else
					return TimeSpan.FromSeconds(0);
			}
			set => m_SeqEnd = DateTime.Now + value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public AccessLevel TriggerAccessLevel
		{
			get => m_TriggerAccessLevel;
			set => m_TriggerAccessLevel = value;
		}


		[CommandProperty(AccessLevel.GameMaster)]
		public bool DoRespawn
		{
			get => false;
			set
			{
				// need to determine whether this is being set by the spawner during processing of a respawn entry
				// if so then dont do it, otherwise you will infinitely recurse and crash with a stack overflow
				if (value == true && !inrespawn) Respawn();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool DoReset
		{
			get => false;
			set
			{
				if (value == true) Reset();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool AllowGhostTrig
		{
			get => m_AllowGhostTriggering;
			set => m_AllowGhostTriggering = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool AllowNPCTrig
		{
			get => m_AllowNPCTriggering;
			set => m_AllowNPCTriggering = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string ConfigFile
		{
			get => m_ConfigFile;
			set => m_ConfigFile = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool LoadConfig
		{
			get => false;
			set
			{
				if (value == true) LoadXmlConfig(ConfigFile);
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public Mobile TriggerMob
		{
			get => m_mob_who_triggered;
			set => m_mob_who_triggered = value;
		}


		[CommandProperty(AccessLevel.GameMaster)]
		public string FirstModifiedBy
		{
			get => m_FirstModifiedBy;
			set
			{
				m_FirstModifiedBy = value;
				m_FirstModified = DateTime.Now;
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string LastModifiedBy
		{
			get => m_LastModifiedBy;
			set
			{
				m_LastModifiedBy = value;
				m_LastModified = DateTime.Now;
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool SmartSpawning
		{
			get => m_SmartSpawning;
			set
			{
				m_SmartSpawning = value;

				if (m_SmartSpawning)
				{
					// if any spawner is smartspawning, then the smartspawning system is enabled
					SmartSpawningSystemEnabled = true;
					// check to see if the global sector timer is running
					if (m_GlobalSectorTimer == null || !m_GlobalSectorTimer.Running)
						// start the global smartspawning timer
						DoGlobalSectorTimer(TimeSpan.FromSeconds(1));
				}

				//IsInactivated = false; 
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool IsEmpty => isEmpty();

		#endregion

		#region ISpawner interface support

		public bool UnlinkOnTaming => true;
		public Point3D HomeLocation => Location;
		public int Range => HomeRange;

		public virtual void GetSpawnProperties(ISpawnable spawn, ObjectPropertyList list) { }
		public virtual void GetSpawnContextEntries(ISpawnable spawn, Mobile m, List<ContextMenuEntry> list) { }

		public void Remove(ISpawnable spawn)
		{
			if (m_SpawnObjects == null) return;

			foreach (var so in m_SpawnObjects)
				for (var i = 0; i < so.SpawnedObjects.Count; ++i)
					if (so.SpawnedObjects[i] == spawn)
					{
						so.SpawnedObjects.Remove(spawn);
						if (SequentialSpawn >= 0 && so.RestrictKillsToSubgroup)
						{
							if (so.SubGroup == SequentialSpawn)
								m_killcount++;
						}
						else
							m_killcount++;

						return;
					}
		}

		public void RestoreISpawner()
		{
			// restore the Spawner assignments to all spawned objects
			if (m_SpawnObjects == null) return;

			foreach (var so in m_SpawnObjects)
				for (var i = 0; i < so.SpawnedObjects.Count; ++i)
				{
					var o = so.SpawnedObjects[i];
					if (o is Item)
						((Item)o).Spawner = this;
					else if (o is Mobile) ((Mobile)o).Spawner = this;
				}
		}

		#endregion

		#region Method Overrides

		public override void OnAfterDuped(Item newItem)
		{
			// automatically turn off duped spawners
			((XmlSpawner)newItem).Running = false;
		}

		public override void OnMapChange()
		{
			base.OnMapChange();

			// unregister the skill trigger on the previous map
			XmlSpawnerSkillCheck.UnRegisterSkillTrigger(this, m_SkillTriggerName, currentmap, false);

			// register the skill trigger on the new current map
			XmlSpawnerSkillCheck.RegisterSkillTrigger(this, m_SkillTriggerName, Map);

			currentmap = Map;

			// reset the sector list for smart spawning
			ResetSectorList();
		}

		public override void OnDoubleClick(Mobile from)
		{
			if (from == null || from.Deleted || from.AccessLevel < AccessLevel.GameMaster ||
			    m_SpawnerGump != null && SomeOneHasGumpOpen) return;

			// flag the first person to open the spawner as the placer
			if (FirstModifiedBy == null) FirstModifiedBy = from.Name;

			LastModifiedBy = from.Name;

			// clear any text entry books that might still be around
			DeleteTextEntryBook();

			var x = 0;
			var y = 0;
			// read the text entries for default values
			var acct = from.Account as Account;
			if (acct != null)
			{
				var defs = XmlSpawnerDefaults.GetDefaults(acct.ToString(), from.Name);
				if (defs != null)
				{
					x = defs.SpawnerGumpX;
					y = defs.SpawnerGumpY;
				}
			}

			var g = new XmlSpawnerGump(this, x, y, 0, 0, 0);
			from.SendGump(g);
		}

		public override void GetProperties(ObjectPropertyList list)
		{
			base.GetProperties(list);

			if (m_Running)
				list.Add(1060742); // active
			else
				list.Add(1060743); // inactive

			// add whitespace to the beginning to avoid any problem with names that begin with # and are interpreted as cliloc ids
			list.Add(1042971, " " + Name); // ~1_val~
			list.Add(1060656, m_Count.ToString()); // amount to make: ~1_val~
			list.Add(1061169, m_HomeRange.ToString()); // range ~1_val~

			var nlist_items = 6;

			if (m_Group)
			{
				list.Add(1060658 + 6 - nlist_items, "group\t{0}", m_Group); // ~1_val~: ~2_val~
				nlist_items--;
			}

			if (m_Team != 0)
			{
				list.Add(1060658 + 6 - nlist_items, "team\t{0}", m_Team); // ~1_val~: ~2_val~
				nlist_items--;
			}

			list.Add(1060658 + 6 - nlist_items, "speed\t{0} to {1}", m_MinDelay, m_MaxDelay); // ~1_val~: ~2_val~
			nlist_items--;

			// display the duration parameter in the prop gump if it is non-zero
			if (m_Duration > TimeSpan.FromMinutes(0))
			{
				list.Add(1060658 + 6 - nlist_items, "Duration\t{0}", m_Duration);
				nlist_items--;
			}

			// display the proximity range parameter in the prop gump if it is active
			if (m_ProximityRange != -1)
			{
				list.Add(1060658 + 6 - nlist_items, "ProximityRange\t{0}", m_ProximityRange);
				nlist_items--;
			}

			if (m_SpawnObjects != null)
				for (var i = 0; i < nlist_items && i < m_SpawnObjects.Count; ++i)
				{
					var typename = m_SpawnObjects[i].TypeName;
					if (typename != null && typename.Length > 20) typename = typename.Substring(0, 20);

					list.Add(1060658 + (6 - nlist_items) + i, " {0}\t{1}", typename,
						m_SpawnObjects[i].SpawnedObjects.Count);
				}

			// ARTEGORDONMOD
			// mod to display attachment properties
			XmlAttach.AddAttachmentProperties(this, list);
		}

		public override void OnSingleClick(Mobile from)
		{
			LabelTo(from, "XmlSpawner");
			LabelTo(from, Name + (m_Running == true ? " [On]" : " [Off]"));
		}


		public override void OnDelete()
		{
			base.OnDelete();

			if (ShowBounds == true)
				ShowBounds = false;

			RemoveSpawnObjects();

			// remove any text entry books that might still be attached to the spawner
			DeleteTextEntryBook();

			if (m_Timer != null)
				m_Timer.Stop();

			if (m_DurTimer != null)
				m_DurTimer.Stop();

			if (m_RefractoryTimer != null)
				m_RefractoryTimer.Stop();

			// if statics were added for marking container held spawners, delete them
			if (m_ShowContainerStatic != null && !m_ShowContainerStatic.Deleted)
				m_ShowContainerStatic.Delete();

			// unregister all triggerskills that might have been added
			XmlSpawnerSkillCheck.UnRegisterSkillTrigger(this, SkillName.Alchemy, Map, true);
		}

		private static bool IgnoreLocationChange = false;

		public override void OnLocationChange(Point3D oldLocation)
		{
			if (IgnoreLocationChange)
			{
				IgnoreLocationChange = false;
				return;
			}


			// calculate the positional shift
			if (oldLocation.X > 0 && oldLocation.Y > 0)
			{
				var diffx = X - oldLocation.X;
				var diffy = Y - oldLocation.Y;
				m_X += diffx;
				m_Y += diffy;
			}
			else
			{
				// Keep the original dimensions the same (Width, Height),
				// just recalculate the new top left corner
				m_X = X - m_Width / 2;
				m_Y = Y - m_Height / 2;
			}

			// reset the sector list for smart spawning
			ResetSectorList();

			// Check if the spawner is showing its bounds
			if (ShowBounds == true)
			{
				ShowBounds = false;
				ShowBounds = true;
			}
		}

		#endregion

		#region Gump support

		public bool SomeOneHasGumpOpen
		{
			get
			{
				// go through all online mobiles and see if any have xmlspawner gumps open
				var states = NetState.Instances;

				for (var i = 0; i < states.Count; ++i)
				{
					var m = ((NetState)states[i]).Mobile;

					if (m != null && m.HasGump(typeof(XmlSpawnerGump))) return true;
				}

				return false;
			}
		}


		public static void SpawnerGumpCallback(Mobile from, object invoker, string response)
		{
			// assign the response to the gumpstate
			if (invoker is XmlSpawner)
			{
				var xs = (XmlSpawner)invoker;
				xs.GumpState = response;
			}
		}

		public void DeleteTextEntryBook()
		{
			if (m_TextEntryBook != null)
			{
				foreach (var s in m_TextEntryBook)
					s.Delete();

				m_TextEntryBook = null;
			}
		}

		#endregion

		#region Utility Methods

		private static bool IsConstructable(ConstructorInfo ctor)
		{
			return ctor.IsDefined(typeof(ConstructableAttribute), false);
		}

		public static int ConvertToInt(string value)
		{
			if (value.StartsWith("0x"))
				return Convert.ToInt32(value.Substring(2), 16);
			else
				return Convert.ToInt32(value);
		}

		public static void ExecuteAction(object attachedto, Mobile trigmob, string action)
		{
			var loc = Point3D.Zero;
			Map map = null;
			if (attachedto is IEntity)
			{
				loc = ((IEntity)attachedto).Location;
				map = ((IEntity)attachedto).Map;
			}

			if (action == null || action.Length <= 0 || attachedto == null || map == null) return;

			string status_str = null;
			var TheSpawn = new SpawnObject(null, 0);

			TheSpawn.TypeName = action;
			var substitutedtypeName = BaseXmlSpawner.ApplySubstitution(null, attachedto, trigmob, action);
			var typeName = BaseXmlSpawner.ParseObjectType(substitutedtypeName);

			if (BaseXmlSpawner.IsTypeOrItemKeyword(typeName))
				BaseXmlSpawner.SpawnTypeKeyword(attachedto, TheSpawn, typeName, substitutedtypeName, true, trigmob, loc,
					map, out status_str);
			else
			{
				// its a regular type descriptor so find out what it is
				var type = SpawnerType.GetType(typeName);
				try
				{
					var arglist = BaseXmlSpawner.ParseString(substitutedtypeName, 3, "/");
					var o = CreateObject(type, arglist[0]);

					if (o == null)
						status_str = "invalid type specification: " + arglist[0];
					else if (o is Mobile)
					{
						var m = (Mobile)o;
						if (m is BaseCreature)
						{
							var c = (BaseCreature)m;
							c.Home = loc; // Spawners location is the home point
						}

						m.Location = loc;
						m.Map = map;

						BaseXmlSpawner.ApplyObjectStringProperties(null, substitutedtypeName, m, trigmob, attachedto,
							out status_str);
					}
					else if (o is Item)
					{
						var item = (Item)o;
						BaseXmlSpawner.AddSpawnItem(null, attachedto, TheSpawn, item, loc, map, trigmob, false,
							substitutedtypeName, out status_str);
					}
				}
				catch { }
			}
		}

		private static void RemoveFromSectorTable(Sector s, XmlSpawner spawner)
		{
			if (s == null || s.Owner == null || s.Owner == Map.Internal ||
			    GlobalSectorTable[s.Owner.MapID] == null) return;

			// find the sector
			List<XmlSpawner> spawnerlist;
			if (GlobalSectorTable[s.Owner.MapID].TryGetValue(s, out spawnerlist) && spawnerlist != null)
				//List<XmlSpawner> spawnerlist = GlobalSectorTable[s.Owner.MapID][s];
				if (spawnerlist.Contains(spawner))
					spawnerlist.Remove(spawner);
		}

		private void ResetSectorList()
		{
			// remove the global sector entries
			if (sectorList != null)
				foreach (var s in sectorList)
					RemoveFromSectorTable(s, this);
			sectorList = null;
			UseSectorActivate = false;

			//IsInactivated = false;

			// force an update of the sector list
			var sectorrefresh = HasActiveSectors;
		}

		public void LoadXmlConfig(string filename)
		{
			if (filename == null || filename.Length <= 0) return;
			// Check if the file exists
			if (File.Exists(filename) == true)
			{
				FileStream fs = null;
				try
				{
					fs = File.Open(filename, FileMode.Open, FileAccess.Read);
				}
				catch { }

				if (fs == null)
				{
					status_str = String.Format("Unable to open {0} for loading", filename);
					return;
				}

				// Create the data set
				var ds = new DataSet(XmlDataSetName);

				// Read in the file
				var fileerror = false;
				try
				{
					ds.ReadXml(fs);
				}
				catch { fileerror = true; }

				// close the file
				fs.Close();
				if (fileerror)
				{
					Console.WriteLine("XmlSpawner: Error in XML config file '{0}'", filename);
					return;
				}

				// Check that at least a single table was loaded
				if (ds.Tables != null && ds.Tables.Count > 0)
					if (ds.Tables[XmlTableName] != null && ds.Tables[XmlTableName].Rows.Count > 0)
						foreach (DataRow dr in ds.Tables[XmlTableName].Rows)
						{
							bool valid_entry;
							string strEntry = null;
							var boolEntry = true;
							double doubleEntry = 0;
							var intEntry = 0;

							valid_entry = true;
							try { strEntry = (string)dr["Name"]; }
							catch { valid_entry = false; }

							if (valid_entry) Name = strEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["X"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_X = intEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["Y"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_Y = intEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["Width"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_Width = intEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["Height"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_Height = intEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["CentreX"]); }
							catch { valid_entry = false; }

							if (valid_entry) X = intEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["CentreY"]); }
							catch { valid_entry = false; }

							if (valid_entry) Y = intEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["CentreZ"]); }
							catch { valid_entry = false; }

							if (valid_entry) Z = intEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["SequentialSpawning"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_SequentialSpawning = intEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["ProximityRange"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_ProximityRange = intEntry;

							valid_entry = true;
							try { strEntry = (string)dr["ProximityTriggerMessage"]; }
							catch { valid_entry = false; }

							if (valid_entry) m_ProximityTriggerMessage = strEntry;

							valid_entry = true;
							try { strEntry = (string)dr["SpeechTrigger"]; }
							catch { valid_entry = false; }

							if (valid_entry) m_SpeechTrigger = strEntry;

							valid_entry = true;
							try { strEntry = (string)dr["SkillTrigger"]; }
							catch { valid_entry = false; }

							if (valid_entry) m_SkillTrigger = strEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["ProximityTriggerSound"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_ProximityTriggerSound = intEntry;

							valid_entry = true;
							try { strEntry = (string)dr["ItemTriggerName"]; }
							catch { valid_entry = false; }

							if (valid_entry) m_ItemTriggerName = strEntry;

							valid_entry = true;
							try { strEntry = (string)dr["NoItemTriggerName"]; }
							catch { valid_entry = false; }

							if (valid_entry) m_NoItemTriggerName = strEntry;

							// check for the delayinsec entry
							var delayinsec = false;
							try { delayinsec = Boolean.Parse((string)dr["DelayInSec"]); }
							catch { }

							valid_entry = true;
							try { doubleEntry = Double.Parse((string)dr["MinDelay"]); }
							catch { valid_entry = false; }

							if (valid_entry)
							{
								if (delayinsec) m_MinDelay = TimeSpan.FromSeconds(doubleEntry);
								else m_MinDelay = TimeSpan.FromMinutes(doubleEntry);
							}

							valid_entry = true;
							try { doubleEntry = Double.Parse((string)dr["MaxDelay"]); }
							catch { valid_entry = false; }

							if (valid_entry)
							{
								if (delayinsec) m_MaxDelay = TimeSpan.FromSeconds(doubleEntry);
								else m_MaxDelay = TimeSpan.FromMinutes(doubleEntry);
							}

							valid_entry = true;
							try { doubleEntry = Double.Parse((string)dr["Duration"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_Duration = TimeSpan.FromMinutes(doubleEntry);

							valid_entry = true;
							try { doubleEntry = Double.Parse((string)dr["DespawnTime"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_DespawnTime = TimeSpan.FromHours(doubleEntry);

							valid_entry = true;
							try { doubleEntry = Double.Parse((string)dr["MinRefractory"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_MinRefractory = TimeSpan.FromMinutes(doubleEntry);

							valid_entry = true;
							try { doubleEntry = Double.Parse((string)dr["MaxRefractory"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_MaxRefractory = TimeSpan.FromMinutes(doubleEntry);

							valid_entry = true;
							try { doubleEntry = Double.Parse((string)dr["TODStart"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_TODStart = TimeSpan.FromMinutes(doubleEntry);

							valid_entry = true;
							try { doubleEntry = Double.Parse((string)dr["TODEnd"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_TODEnd = TimeSpan.FromMinutes(doubleEntry);

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["TODMode"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_TODMode = (TODModeType)intEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["Amount"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_StackAmount = intEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["MaxCount"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_Count = intEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["Range"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_HomeRange = intEntry;

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["Team"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_Team = intEntry;

							valid_entry = true;
							try { strEntry = (string)dr["WayPoint"]; }
							catch { valid_entry = false; }

							if (valid_entry) m_WayPoint = GetWaypoint(strEntry);

							valid_entry = true;
							try { intEntry = Int32.Parse((string)dr["KillReset"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_KillReset = intEntry;

							valid_entry = true;
							try { doubleEntry = Double.Parse((string)dr["TriggerProbability"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_TriggerProbability = doubleEntry;

							valid_entry = true;
							try { boolEntry = Boolean.Parse((string)dr["ExternalTriggering"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_ExternalTriggering = boolEntry;

							valid_entry = true;
							try { boolEntry = Boolean.Parse((string)dr["IsGroup"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_Group = boolEntry;

							valid_entry = true;
							try { boolEntry = Boolean.Parse((string)dr["IsHomeRangeRelative"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_HomeRangeIsRelative = boolEntry;

							valid_entry = true;
							try { boolEntry = Boolean.Parse((string)dr["AllowGhostTriggering"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_AllowGhostTriggering = boolEntry;

							valid_entry = true;
							try { boolEntry = Boolean.Parse((string)dr["AllowNPCTriggering"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_AllowNPCTriggering = boolEntry;

							valid_entry = true;
							try { boolEntry = Boolean.Parse((string)dr["SpawnOnTrigger"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_SpawnOnTrigger = boolEntry;

							valid_entry = true;
							try { boolEntry = Boolean.Parse((string)dr["SmartSpawning"]); }
							catch { valid_entry = false; }

							if (valid_entry) m_SmartSpawning = boolEntry;

							valid_entry = true;
							try { strEntry = (string)dr["RegionName"]; }
							catch { valid_entry = false; }

							if (valid_entry) RegionName = strEntry;

							valid_entry = true;
							try { strEntry = (string)dr["PlayerPropertyName"]; }
							catch { valid_entry = false; }

							if (valid_entry) m_PlayerPropertyName = strEntry;

							valid_entry = true;
							try { strEntry = (string)dr["MobPropertyName"]; }
							catch { valid_entry = false; }

							if (valid_entry) m_MobPropertyName = strEntry;

							valid_entry = true;
							try { strEntry = (string)dr["MobTriggerName"]; }
							catch { valid_entry = false; }

							if (valid_entry) m_MobTriggerName = strEntry;

							valid_entry = true;
							try { strEntry = (string)dr["ObjectPropertyName"]; }
							catch { valid_entry = false; }

							if (valid_entry) m_ObjectPropertyName = strEntry;

							valid_entry = true;
							try { strEntry = (string)dr["ObjectPropertyItemName"]; }
							catch { valid_entry = false; }

							if (valid_entry)
							{
								var typeargs = strEntry.Split(",".ToCharArray(), 2);
								string typestr = null;
								var namestr = strEntry;

								if (typeargs.Length > 1)
								{
									namestr = typeargs[0];
									typestr = typeargs[1];
								}

								m_ObjectPropertyItem = BaseXmlSpawner.FindItemByName(this, namestr, typestr);
							}

							valid_entry = true;
							try { strEntry = (string)dr["SetPropertyItemName"]; }
							catch { valid_entry = false; }

							if (valid_entry)
							{
								var typeargs = strEntry.Split(",".ToCharArray(), 2);
								string typestr = null;
								var namestr = strEntry;

								if (typeargs.Length > 1)
								{
									namestr = typeargs[0];
									typestr = typeargs[1];
								}

								m_SetPropertyItem = BaseXmlSpawner.FindItemByName(this, namestr, typestr);
							}

							valid_entry = true;
							try { strEntry = (string)dr["Name"]; }
							catch { valid_entry = false; }

							if (valid_entry) Name = strEntry;

							valid_entry = true;
							try { strEntry = (string)dr["Map"]; }
							catch { valid_entry = false; }

							if (valid_entry)
								// Convert the xml map value to a real map object
								try
								{
									Map = Map.Parse(strEntry);
								}
								catch { }

							// try loading the new spawn specifications first
							var Spawns = new SpawnObject[0];
							var havenew = true;
							valid_entry = true;
							try { Spawns = SpawnObject.LoadSpawnObjectsFromString2((string)dr["Objects2"]); }
							catch { havenew = false; }

							if (!havenew)
								// try loading the new spawn specifications
								try { Spawns = SpawnObject.LoadSpawnObjectsFromString((string)dr["Objects"]); }
								catch { valid_entry = false; }
							// can only have one of these defined

							if (valid_entry)
							{
								// clear existing spawns
								RemoveSpawnObjects();

								// Create the new array of spawned objects
								m_SpawnObjects = new List<SpawnObject>();

								// Assign the list of objects to spawn
								SpawnObjects = Spawns;
							}
						}
			}
		}

		public void ReportStatus()
		{
			if (PropertyInfoList != null)
			{
				Console.WriteLine("PropertyInfoList: {0}", PropertyInfoList.Count);
				foreach (var to in PropertyInfoList)
				{
					Console.WriteLine("\t{0}", to.t);
					foreach (var p in to.plist) Console.WriteLine("\t\t{0}", p);
				}
			}

			ShowTagList(this);
			var count = 0;
			Console.WriteLine("Registered Skills");
			Console.WriteLine("Felucca");
			for (var i = 0; i < XmlSpawnerSkillCheck.RegisteredSkill.MaxSkills + 1; i++)
			{
				if (XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Felucca).Count > 0)
					Console.WriteLine("\t{0} : {1}", (SkillName)i,
						XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Felucca).Count);

				count += XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Felucca).Count;
			}

			Console.WriteLine("Trammel");
			for (var i = 0; i < XmlSpawnerSkillCheck.RegisteredSkill.MaxSkills + 1; i++)
			{
				if (XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Trammel).Count > 0)
					Console.WriteLine("\t{0} : {1}", (SkillName)i,
						XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Trammel).Count);

				count += XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Trammel).Count;
			}

			Console.WriteLine("Ilshenar");
			for (var i = 0; i < XmlSpawnerSkillCheck.RegisteredSkill.MaxSkills + 1; i++)
			{
				if (XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Ilshenar).Count > 0)
					Console.WriteLine("\t{0} : {1}", (SkillName)i,
						XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Ilshenar).Count);

				count += XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Ilshenar).Count;
			}

			Console.WriteLine("Malas");
			for (var i = 0; i < XmlSpawnerSkillCheck.RegisteredSkill.MaxSkills + 1; i++)
			{
				if (XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Malas).Count > 0)
					Console.WriteLine("\t{0} : {1}", (SkillName)i,
						XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Malas).Count);

				count += XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Malas).Count;
			}

			Console.WriteLine("Tokuno");
			for (var i = 0; i < XmlSpawnerSkillCheck.RegisteredSkill.MaxSkills + 1; i++)
			{
				if (XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Tokuno).Count > 0)
					Console.WriteLine("\t{0} : {1}", (SkillName)i,
						XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Tokuno).Count);

				count += XmlSpawnerSkillCheck.RegisteredSkill.TriggerList((SkillName)i, Map.Tokuno).Count;
			}

			Console.WriteLine("Total = {0}", count);
		}

		#endregion

		#region Trace support

#if(TRACE)
string setname1 = _traceName[1] = "XmlFind";
string setname2 = _traceName[2] = "HasSector";

string setname4 = _traceName[4] = "AttachSpeech";
string setname5 = _traceName[5] = "HasHold";


string setname8 = _traceName[8] = "OnTick";
string setname9 = _traceName[9] = "Defrag";
string setname10 = _traceName[10] = "Respawn";
string setname11 = _traceName[11] = "SetProp";
string setname12 = _traceName[12] = "AttachMovement";
string setname13 = _traceName[13] = "ActiveSector";

string setname15 = _traceName[15] = "DistroTick";
string setname16 = _traceName[16] = "GetScaledFaction";
string setname17 = _traceName[17] = "FactionOnKill";
string setname18 = _traceName[18] = "CheckAcquire";



private const int MaxTraces = 20;
private static DateTime[] _traceStart = new DateTime[MaxTraces];
public static TimeSpan[] _traceTotal = new TimeSpan[MaxTraces];
public static string[] _traceName = new string[MaxTraces];
public static int[] _traceCount = new int[MaxTraces];
private static DateTime _traceStartTime = DateTime.Now;
private static double _startProcessTime = 0;

public static void _TraceStart(int index)
{
	   if(index < MaxTraces){
			_traceStart[index] = DateTime.Now;
			//_traceStart[index] =  Process.GetCurrentProcess().UserProcessorTime;
		}
}
public static void _TraceEnd(int index)
{
	   if(index < MaxTraces){
				XmlSpawner._traceTotal[index] = XmlSpawner._traceTotal[index].Add(DateTime.Now - _traceStart[index]);
				//XmlSpawner._traceTotal[index] = XmlSpawner._traceTotal[index].Add(Process.GetCurrentProcess().UserProcessorTime - _traceStart[index]);
				XmlSpawner._traceCount[index]++;
	   }
}
#else
		public static void _TraceStart(int index) { }
		public static void _TraceEnd(int index) { }
#endif

		#endregion

		#region Trigger Methods

		private bool ValidPlayerTrig(Mobile m)
		{
			if (m == null || m.Deleted) return false;
			return (m.Player || m_AllowNPCTriggering) && m.AccessLevel <= TriggerAccessLevel &&
			       (!m.Body.IsGhost && !m_AllowGhostTriggering || m.Body.IsGhost && m_AllowGhostTriggering);
		}

		private bool AllowTriggering => m_Running && !m_refractActivated && TODInRange && CanSpawn;

		private void ActivateTrigger()
		{
			// reset the timer
			DoTimer();

			// start the refractory timer to set proximity activated to false, thus enabling another activation
			if (m_MaxRefractory > TimeSpan.FromMinutes(0))
			{
				var minSeconds = (int)m_MinRefractory.TotalSeconds;
				var maxSeconds = (int)m_MaxRefractory.TotalSeconds;

				DoTimer3(TimeSpan.FromSeconds(Utility.RandomMinMax(minSeconds, maxSeconds)));
			}

			// if the spawnontrigger flag is set, then spawn immediately
			if (m_SpawnOnTrigger)
			{
				NextSpawn = TimeSpan.Zero;
				ResetNextSpawnTimes();
			}

			// reset speech triggering if it was set
			m_speechTriggerActivated = false;

			// reset skill triggering if it was set
			m_skillTriggerActivated = false;

			// reset external triggering if it was set
			//this.m_ExternalTrigger = false;
		}

		public void CheckTriggers(Mobile m, Skill s, bool hasproximity)
		{
			// only proximity trigger when no spawns have already been triggered
			if (AllowTriggering && !m_proximityActivated)
			{
				var needs_item_trigger = false;
				var needs_speech_trigger = false;
				var needs_skill_trigger = false;
				var needs_object_trigger = false;
				var needs_mob_trigger = false;
				var needs_player_trigger = false;
				var needs_noitem_trigger = false;
				var has_object_trigger = false;
				var has_mob_trigger = false;
				var has_player_trigger = false;
				var has_item_trigger = false;
				var has_noitem_trigger =
					true; // assume the player doesnt have the trigger-blocking item until it is found by search

				m_skipped = false;

				// test for the various triggering options in the order of increasing computational demand.  No point checking a high demand test
				// if a low demand one has already failed.

				// check for external triggering
				if (m_ExternalTriggering && !m_ExternalTrigger) return;

				// if speech triggering is set then test for successful activation
				if (m_SpeechTrigger != null && m_SpeechTrigger.Length > 0) needs_speech_trigger = true;
				// check to see if we have to continue
				if (needs_speech_trigger && !m_speechTriggerActivated) return;

				// if skill triggering is set then test for successful activation
				if (m_SkillTrigger != null && m_SkillTrigger.Length > 0) needs_skill_trigger = true;
				// check to see if we have to continue
				if (needs_skill_trigger && !m_skillTriggerActivated) return;

				// if item property triggering is set then test for the property value
				//
				if (m_ObjectPropertyName != null && m_ObjectPropertyName.Length > 0)
				{
					needs_object_trigger = true;
					string status_str;

					if (BaseXmlSpawner.TestItemProperty(this, m_ObjectPropertyItem, m_ObjectPropertyName, null,
						    out status_str))
						has_object_trigger = true;
					else
						has_object_trigger = false;
					if (status_str != null && status_str.Length > 0) this.status_str = status_str;
				}

				// check to see if we have to continue
				if (needs_object_trigger && !has_object_trigger) return;

				// if player property triggering is set then look for the mob and test properties
				if (m_PlayerPropertyName != null && m_PlayerPropertyName.Length > 0)
				{
					needs_player_trigger = true;
					string status_str;

					if (BaseXmlSpawner.TestMobProperty(this, m, m_PlayerPropertyName, null, out status_str))
						has_player_trigger = true;
					else
						has_player_trigger = false;
					if (status_str != null && status_str.Length > 0) this.status_str = status_str;
				}

				// check to see if we have to continue
				if (needs_player_trigger && !has_player_trigger) return;

				// if mob property triggering is set then look for the mob and test properties
				if (m_MobPropertyName != null && m_MobPropertyName.Length > 0 &&
				    m_MobTriggerName != null && m_MobTriggerName.Length > 0)
				{
					needs_mob_trigger = true;

					string status_str;

					if (BaseXmlSpawner.TestMobProperty(this, MobTriggerId, m_MobPropertyName, null, out status_str))
						has_mob_trigger = true;
					else
						has_mob_trigger = false;

					if (status_str != null && status_str.Length > 0) this.status_str = status_str;
				}

				// check to see if we have to continue
				if (needs_mob_trigger && !has_mob_trigger) return;

				// if player-carried item triggering is set then test for the presence of an item on the player an in their pack
				if (m_ItemTriggerName != null && m_ItemTriggerName.Length > 0)
				{
					//enable_triggering = false;
					needs_item_trigger = true;

					has_item_trigger = BaseXmlSpawner.CheckForCarried(m, m_ItemTriggerName);
				}

				// check to see if we have to continue
				if (needs_item_trigger && !has_item_trigger) return;

				// if player-carried noitem triggering is set then test for the presence of an item in the players pack that should block triggering
				if (m_NoItemTriggerName != null && m_NoItemTriggerName.Length > 0)
				{
					needs_noitem_trigger = true;

					has_noitem_trigger = BaseXmlSpawner.CheckForNotCarried(m, m_NoItemTriggerName);
				}

				// check to see if we have to continue
				if (needs_noitem_trigger && !has_noitem_trigger) return;

				// if this was called without being proximity triggered then check to see that the non-movement triggers were enabled.
				if (!hasproximity && !needs_object_trigger && !needs_mob_trigger && !m_ExternalTriggering) return;

				// all of the necessary trigger conditions have been met so go ahead and trigger
				// after you make the probability check

				if (Utility.RandomDouble() < m_TriggerProbability)
				{
					// play a sound indicating the spawner has been triggered
					if (m_ProximityTriggerSound > 0 && m != null && !m.Deleted)
						m.PlaySound(m_ProximityTriggerSound);

					// display the trigger message
					if (m_ProximityTriggerMessage != null && m_ProximityTriggerMessage.Length > 0 && m != null &&
					    !m.Deleted)
						m.PublicOverheadMessage(MessageType.Regular, 0x3B2, false, m_ProximityTriggerMessage);

					// enable spawning at the next ontick
					// this will also start the refractory timer and send the triggering indicators
					ProximityActivated = true;

					// keep track of who triggered this
					m_mob_who_triggered = m;

					// keep track of the skill that triggered this
					if (s != null)
						m_skill_that_triggered = s.SkillName;
					else
						m_skill_that_triggered = XmlSpawnerSkillCheck.RegisteredSkill.Invalid;
				}
				else
				{
					m_skipped = true;
					// reset speech triggering if it was set

					m_speechTriggerActivated = false;

					// reset skill triggering if it was set
					m_skillTriggerActivated = false;
					// reset external triggering if it was set
					//this.m_ExternalTrigger = false;
				}
			}
		}

		public bool HandlesOnSkillUse => m_Running && m_SkillTrigger != null && m_SkillTrigger.Length > 0;

		// this is the handler for skill use
		public void OnSkillUse(Mobile m, Skill skill, bool success)
		{
			if (m_Running && m_ProximityRange >= 0 && ValidPlayerTrig(m) && CanSpawn && !m_refractActivated &&
			    TODInRange)
			{
				if (!Utility.InRange(m.Location, Location, m_ProximityRange))
					return;

				m_skillTriggerActivated = false;

				// check the skill trigger conditions, Skillname[+/-][,min,max]
				if (m_SkillTrigger != null && skill.SkillName == m_SkillTriggerName &&
				    (m_SkillTriggerMin < 0 || skill.Value >= m_SkillTriggerMin) &&
				    (m_SkillTriggerMax < 0 || skill.Value <= m_SkillTriggerMax) &&
				    (m_SkillTriggerSuccess == 3 || m_SkillTriggerSuccess == 1 && success ||
				     m_SkillTriggerSuccess == 2 && !success))
				{
					// have a skill trigger so flag it and test it
					m_skillTriggerActivated = true;

					CheckTriggers(m, skill, true);
				}
			}
		}


		public override bool HandlesOnSpeech => m_Running && m_SpeechTrigger != null && m_SpeechTrigger.Length > 0;

		public override void OnSpeech(SpeechEventArgs e)
		{
			if ( /*!e.Handled && */m_Running && m_ProximityRange >= 0 && ValidPlayerTrig(e.Mobile) && CanSpawn &&
			                       !m_refractActivated && TODInRange)
			{
				m_speechTriggerActivated = false;

				if (!Utility.InRange(e.Mobile.Location, Location, m_ProximityRange))
					return;

				if (m_SpeechTrigger != null && e.Speech.ToLower().IndexOf(m_SpeechTrigger.ToLower()) >= 0)
				{
					e.Handled = true;

					// found the speech trigger so flag it for testing in the onmovement handler where the other proximity features are tested
					m_speechTriggerActivated = true;

					CheckTriggers(e.Mobile, null, true);
				}
			}
		}


		public override bool HandlesOnMovement => m_Running && m_ProximityRange >= 0;


		public void AddToMovementList(Mobile m)
		{
			// go through the list and check for redundancy
			if (m_MovementList == null) m_MovementList = new List<MovementInfo>();

			// check to see if the movement timer is running
			if (m_MovementTimer == null || !m_MovementTimer.Running) DoMovementTimer(TimeSpan.FromSeconds(1));

			var add = true;

			foreach (var moveinfo in m_MovementList)
			{
				var mtrig = moveinfo.trigMob;
				if (mtrig == m)
				{
					add = false;
					break;
				}
			}

			// wasnt on the list so add it
			if (add)
			{
				// is the list at max throttling length?
				if (m_MovementList.Count > MaxMoveCheck)
					// replace a random entry in the current list with this one
					m_MovementList[Utility.Random(m_MovementList.Count)] = new MovementInfo(m);
				else
					m_MovementList.Add(new MovementInfo(m));
			}
		}

		public void DoMovementTimer(TimeSpan delay)
		{
			if (m_MovementTimer != null)
				m_MovementTimer.Stop();

			m_MovementTimer = new MovementTimer(this, delay);

			m_MovementTimer.Start();
		}

		private class MovementTimer : Timer
		{
			private XmlSpawner m_Spawner;

			public MovementTimer(XmlSpawner spawner, TimeSpan delay)
				: base(delay)
			{
				Priority = TimerPriority.OneSecond;
				m_Spawner = spawner;
			}

			protected override void OnTick()
			{
				// check everyone on the movement list then clear the list

				if (m_Spawner != null && !m_Spawner.Deleted)
				{
					if (m_Spawner.m_Running && !m_Spawner.m_proximityActivated && !m_Spawner.m_refractActivated &&
					    m_Spawner.TODInRange && m_Spawner.CanSpawn)
					{
						var count = 0;
						var maxspeed = 0;
						var speed = 0;
						foreach (var moveinfo in m_Spawner.m_MovementList)
						{
							var m = moveinfo.trigMob;
							if (m == null) continue;

							// additional throttling in here by limiting number of mobs that can be checked in a single ontick
							count++;
							if (count > MaxMoveCheck) break;

							speed = (int)GetDistance(m.Location, moveinfo.trigLocation);
							if (speed > maxspeed) maxspeed = speed;
							m_Spawner.CheckTriggers(m, null, true);
						}

						m_Spawner.MovingPlayerCount = m_Spawner.m_MovementList.Count;
						m_Spawner.FastestPlayerSpeed = maxspeed;
					}

					m_Spawner.m_MovementList.Clear();
				}
			}
		}

		public static double GetDistance(Point3D p1, Point3D p2)
		{
			var xDelta = p1.X - p2.X;
			var yDelta = p1.Y - p2.Y;

			return Math.Sqrt(xDelta * xDelta + yDelta * yDelta);
		}

		public override void OnMovement(Mobile m, Point3D oldLocation)
		{
			if (m_Running && m_ProximityRange >= 0 && ValidPlayerTrig(m) && CanSpawn)
			{
				// check to see if player is within range of the spawner
				if (Parent == null && Utility.InRange(m.Location, Location, m_ProximityRange))
					// add some throttling code here.
					// add the player to a list that gets cleared every few seconds, checking for redundancy then trigger off of the list instead of off of
					// the actual movement stream

					AddToMovementList(m);

				// check the triggers in the OnTick for the list handler instead of here
				else
				{
					// clear any speech triggering
					m_speechTriggerActivated = false;

					// clear any skill triggering
					m_skillTriggerActivated = false;
				}
			}

			base.OnMovement(m, oldLocation);
		}

		#endregion

		#region Initialization

		public static bool AssignSettings(string argname, string value)
		{
			switch (argname)
			{
				case "XmlSpawnDir":
					XmlSpawnDir = value;
					break;
				case "DiskAccessLevel":
					DiskAccessLevel = (AccessLevel)Enum.Parse(typeof(AccessLevel), value, true);
					break;
				case "SmartSpawnAccessLevel":
					SmartSpawnAccessLevel = (AccessLevel)Enum.Parse(typeof(AccessLevel), value, true);
					break;
				case "XmlConfigsDir":
					XmlConfigsDir = value;
					break;
				case "defaultTriggerSound":
					defaultTriggerSound = ConvertToInt(value);
					defProximityTriggerSound = defaultTriggerSound;
					break;
				case "BaseItemId":
					BaseItemId = ConvertToInt(value);
					break;
				case "ShowItemId":
					ShowItemId = ConvertToInt(value);
					break;
				case "MaxMoveCheck":
					MaxMoveCheck = ConvertToInt(value);
					break;
				case "defMinDelay":
					defMinDelay = TimeSpan.FromMinutes(ConvertToInt(value));
					break;
				case "defMaxDelay":
					defMaxDelay = TimeSpan.FromMinutes(ConvertToInt(value));
					break;
				case "defRelativeHome":
					defRelativeHome = Boolean.Parse(value);
					break;
				case "defSpawnRange":
					defSpawnRange = ConvertToInt(value);
					break;
				case "defHomeRange":
					defHomeRange = ConvertToInt(value);
					break;
				case "JournalNotifyColor":
					XmlQuestHolder.JournalNotifyColor = ConvertToInt(value);
					break;
				case "JournalEchoColor":
					XmlQuestHolder.JournalEchoColor = ConvertToInt(value);
					break;
				case "BlockKeyword":
				{
					// parse the keyword list and remove them from the keyword hashtables
					var keywordlist = value.Split(',');

					if (keywordlist != null && keywordlist.Length > 0)
						for (var i = 0; i < keywordlist.Length; i++)
							BaseXmlSpawner.RemoveKeyword(keywordlist[i]);
					break;
				}
				case "BlockCommand":
				case "ChangeCommand":
					// delay processing of these settings until after all commands have been registered in their Initialize methods
					Timer.DelayCall(TimeSpan.Zero, new TimerStateCallback(DelayedAssignSettings),
						new object[] { argname, value });
					break;
				default:
					return false;
			}

			return true;
		}

		private static void DelayedAssignSettings(object state)
		{
			var args = (object[])state;
			var argname = (string)args[0];
			var value = (string)args[1];
			switch (argname)
			{
				case "BlockCommand":
				{
					// delay processing of this until after all commands have been registered in their Initialize methods
					// parse the command list and remove them from the command hashtables
					// the syntax is "commandname, commandname, etc."
					var keywordlist = value.Split(',');

					if (keywordlist != null && keywordlist.Length > 0)
						for (var i = 0; i < keywordlist.Length; i++)
						{
							var commandname = keywordlist[i].Trim().ToLower();
							try
							{
								CommandSystem.Entries.Remove(commandname);
							}
							catch
							{
								Console.WriteLine("{0}: invalid command {1}", argname, commandname);
							}
						}

					break;
				}
				case "ChangeCommand":
				{
					// delay processing of this until after all commands have been registered in their Initialize methods
					// parse the command list and rehash them into the command hashtables
					// the syntax is "oldname:newname[:accesslevel], oldname:newname[:accesslevel], etc."
					var keywordlist = value.Split(',');

					if (keywordlist != null && keywordlist.Length > 0)
						for (var i = 0; i < keywordlist.Length; i++)
						{
							var namelist = keywordlist[i].Split(':');
							if (namelist != null && namelist.Length > 1)
							{
								var oldname = namelist[0].Trim().ToLower();
								var newname = namelist[1].Trim();

								if (newname.Length == 0) newname = oldname;

								var access = AccessLevel.Player;
								var validaccess = false;
								if (namelist.Length > 2)
									// get the new accesslevel
									try
									{
										access = (AccessLevel)Enum.Parse(typeof(AccessLevel), namelist[2].Trim(), true);
										validaccess = true;
									}
									catch
									{
										Console.WriteLine("{0}: invalid accesslevel {1} for {2}", argname, namelist[2],
											newname);
									}

								// find the command entry for the old name
								CommandEntry e = null;
								try
								{
									e = CommandSystem.Entries[oldname];
								}
								catch
								{
									Console.WriteLine("{0}: invalid command {1}", argname, oldname);
								}

								if (e != null)
								{
									if (!validaccess)
										// use the old accesslevel
										access = e.AccessLevel;
									// remove the old command entry
									CommandSystem.Entries.Remove(oldname);
									// register the new command using the old handler
									CommandSystem.Register(newname, access, e.Handler);
								}

								// also look in the targetcommands list and adjust name and accesslevel there
								foreach (var b in TargetCommands.AllCommands)
									if (b.Commands != null)
										for (var j = 0; j < b.Commands.Length; j++)
										{
											var commandname = b.Commands[j];
											if (commandname.ToLower() == oldname)
											{
												// modify the basecommand with the new name and access
												b.Commands[j] = newname;
												if (validaccess) b.AccessLevel = access;

												// re-register it in the implementors hashtable
												var impls = BaseCommandImplementor.Implementors;

												for (var k = 0; k < impls.Count; ++k)
												{
													var impl = impls[k];

													if ((b.Supports & impl.SupportRequirement) != 0)
													{
														try
														{
															impl.Commands.Remove(commandname);
														}
														catch { }

														impl.Register(b);
													}
												}

												break;
											}
										}
							}
						}

					break;
				}
			}
		}

		public delegate bool AssignSettingsHandler(string argname, string value);

		// load in settings from the xmlspawner2.cfg file in the Data directory
		public static void LoadSettings(AssignSettingsHandler settingshandler, string section)
		{
			// Check if the file exists
			var path = Path.Combine(Core.BaseDirectory, "Data/xmlspawner.cfg");

			if (!File.Exists(path)) return;

			Console.WriteLine("Loading {0} configuration", section);
			using (var ip = new StreamReader(path))
			{
				string line;
				string currentsection = null;
				var nsettings = 0;

				while ((line = ip.ReadLine()) != null)
				{
					line = line.Trim();

					// skip comments
					if (line.Length == 0 || line.StartsWith("#"))
						continue;

					if (line.StartsWith("["))
					{
						// parse the section name
						var args = line.Split("[]".ToCharArray(), 3);
						if (args.Length > 2) currentsection = args[1].Trim();
					}

					// only process the matching classname section
					if (currentsection != section)
						continue;

					var split = line.Split('=');

					if (split.Length >= 2)
					{
						var argname = split[0].Trim();
						var value = split[1].Trim();

						if (argname.Length == 0 || value.Length == 0)
							continue;

						try
						{
							if (settingshandler(argname, value))
								nsettings++;
							else
								Console.WriteLine("'{0}' setting is invalid in section [{1}]", argname, currentsection);
						}
						catch (Exception e)
						{
							Console.WriteLine("Config error '{0}'='{1}'", argname, value);
							Console.WriteLine("Error: {0}", e.Message);
						}
					}
				}

				if (nsettings > 0) Console.WriteLine("{0} settings processed", nsettings);
			}
		}

		public static void Initialize()
		{
			LoadSettings(new AssignSettingsHandler(AssignSettings), "XmlSpawner");

			// initialize the default waypoint name
			var tmpwaypoint = new WayPoint();
			if (tmpwaypoint != null)
			{
				defwaypointname = tmpwaypoint.Name;
				tmpwaypoint.Delete();
			}
			// 2004.02.08 :: Omega Red
			// initialize m_Region fields after world load (now, regions are loaded)
			// Now this gets handled in OnTick

			var count = 0;
			var regional = 0;
			//int timercount=0;

			foreach (var item in World.Items.Values)
				if (item is XmlSpawner)
				{
					count++;
					var spawner = (XmlSpawner)item;

					if (spawner.RegionName != null && spawner.RegionName != String.Empty)
					{
						spawner.RegionName = spawner.RegionName; // invoke set(RegionName)
						regional++;
					}

					// check for smart spawning and restart timers after deser if needed
					// note, HasActiveSectors will recalculate the sector list and UseSectorActivate property
					var recalc_sectors = spawner.HasActiveSectors;
					/*
										if(spawner.SmartSpawning && spawner.IsInactivated && !spawner.UseSectorActivate)
										{
											spawner.DoSectorTimer(TimeSpan.FromSeconds(1));
											timercount++;
										}
					*/
					// add in the totalitem mod to keep them from adding to container counts
					//spawner.TotalItems = -1;
					//spawner.UpdateTotal(spawner, TotalType.Items, -1);

					spawner.RestoreISpawner();
				}

			// start the global smartspawning timer
			if (SmartSpawningSystemEnabled) DoGlobalSectorTimer(TimeSpan.FromSeconds(1));

			// standard commands
			CommandSystem.Register("XmlSpawnerShowAll", AccessLevel.Administrator,
				new CommandEventHandler(ShowSpawnPoints_OnCommand));
			CommandSystem.Register("XmlSpawnerHideAll", AccessLevel.Administrator,
				new CommandEventHandler(HideSpawnPoints_OnCommand));
			CommandSystem.Register("XmlSpawnerWipe", AccessLevel.Administrator,
				new CommandEventHandler(Wipe_OnCommand));
			CommandSystem.Register("XmlSpawnerWipeAll", AccessLevel.Administrator,
				new CommandEventHandler(WipeAll_OnCommand));
			CommandSystem.Register("XmlSpawnerLoad", DiskAccessLevel, new CommandEventHandler(Load_OnCommand));
			CommandSystem.Register("XmlSpawnerSave", DiskAccessLevel, new CommandEventHandler(Save_OnCommand));
			CommandSystem.Register("XmlSpawnerSaveAll", DiskAccessLevel, new CommandEventHandler(SaveAll_OnCommand));
			//added respawn commands
			CommandSystem.Register("XmlSpawnerRespawn", AccessLevel.Seer, new CommandEventHandler(Respawn_OnCommand));
			CommandSystem.Register("XmlSpawnerRespawnAll", AccessLevel.Seer,
				new CommandEventHandler(RespawnAll_OnCommand));

			// ok, I'm lazy. I dont like all that typing, so these are two aliases for the longer commands
			CommandSystem.Register("XmlShow", AccessLevel.Administrator,
				new CommandEventHandler(ShowSpawnPoints_OnCommand));
			CommandSystem.Register("XmlHide", AccessLevel.Administrator,
				new CommandEventHandler(HideSpawnPoints_OnCommand));

			CommandSystem.Register("XmlHome", AccessLevel.GameMaster, new CommandEventHandler(XmlHome_OnCommand));
			CommandSystem.Register("XmlUnLoad", DiskAccessLevel, new CommandEventHandler(UnLoad_OnCommand));
			CommandSystem.Register("XmlSpawnerUnLoad", DiskAccessLevel, new CommandEventHandler(UnLoad_OnCommand));
			CommandSystem.Register("XmlLoad", DiskAccessLevel, new CommandEventHandler(Load_OnCommand));
			CommandSystem.Register("XmlLoadHere", DiskAccessLevel, new CommandEventHandler(LoadHere_OnCommand));
			CommandSystem.Register("XmlNewLoad", DiskAccessLevel, new CommandEventHandler(NewLoad_OnCommand));
			CommandSystem.Register("XmlNewLoadHere", DiskAccessLevel, new CommandEventHandler(NewLoadHere_OnCommand));
			CommandSystem.Register("XmlSave", DiskAccessLevel, new CommandEventHandler(Save_OnCommand));
			CommandSystem.Register("XmlSaveAll", DiskAccessLevel, new CommandEventHandler(SaveAll_OnCommand));
			CommandSystem.Register("XmlSaveOld", DiskAccessLevel, new CommandEventHandler(SaveOld_OnCommand));
			CommandSystem.Register("XmlImportSpawners", DiskAccessLevel,
				new CommandEventHandler(XmlImportSpawners_OnCommand));
			CommandSystem.Register("XmlImportMSF", DiskAccessLevel, new CommandEventHandler(XmlImportMSF_OnCommand));
			CommandSystem.Register("XmlImportMap", DiskAccessLevel, new CommandEventHandler(XmlImportMap_OnCommand));
			CommandSystem.Register("XmlDefaults", AccessLevel.Administrator,
				new CommandEventHandler(XmlDefaults_OnCommand));
			CommandSystem.Register("XmlGet", AccessLevel.GameMaster, new CommandEventHandler(XmlGetValue_OnCommand));
			TargetCommands.Register(new XmlSetCommand());
			//CommandSystem.Register( "XmlSet", AccessLevel.GameMaster, new CommandEventHandler( XmlSetValue_OnCommand ) );
			CommandSystem.Register("OptimalSmartSpawning", AccessLevel.Administrator,
				new CommandEventHandler(OptimalSmartSpawning_OnCommand));
			CommandSystem.Register("SmartStat", AccessLevel.GameMaster, new CommandEventHandler(SmartStat_OnCommand));
			CommandSystem.Register("XmlGo", AccessLevel.GameMaster, new CommandEventHandler(SpawnEditorGo_OnCommand));
			//CommandSystem.Register( "TagList", AccessLevel.Administrator, new CommandEventHandler( ShowTagList_OnCommand ) );
			TargetCommands.Register(new XmlSaveSingle());

#if(TRACE)
			CommandSystem.Register( "XmlMake", AccessLevel.Administrator, new CommandEventHandler( XmlMake_OnCommand ) );
			CommandSystem.Register( "XmlTrace", AccessLevel.Administrator, new CommandEventHandler( XmlTrace_OnCommand ) );
			CommandSystem.Register( "XmlResetTrace", AccessLevel.Administrator, new CommandEventHandler( XmlResetTrace_OnCommand ) );
#endif
		}

		#endregion

		#region Commands

		[Usage("XmlGet property")]
		[Description("Returns value of the property on the targeted object.")]
		public static void XmlGetValue_OnCommand(CommandEventArgs e)
		{
			e.Mobile.Target = new GetValueTarget(e);
		}

		private class GetValueTarget : Target
		{
			private CommandEventArgs m_e;

			public GetValueTarget(CommandEventArgs e)
				: base(30, false, TargetFlags.None)
			{
				m_e = e;
			}

			protected override void OnTarget(Mobile from, object targeted)
			{
				var pname = m_e.GetString(0);
				Type ptype;
				var result = BaseXmlSpawner.GetPropertyValue(null, targeted, pname, out ptype);

				// see if it was successful
				if (ptype == null) return;
				from.SendMessage("{0}", result);
			}
		}

		public class XmlSetCommand : BaseCommand
		{
			public XmlSetCommand()
			{
				AccessLevel = AccessLevel.Administrator;
				Supports = CommandSupport.All;
				Commands = new string[] { "XmlSet" };
				ObjectTypes = ObjectTypes.Both;
				Usage = "XmlSet <propertyName> <value>";
				Description =
					"Sets a property value by name of a targeted object. Provides access to all public properties.";
			}

			public override void Execute(CommandEventArgs e, object obj)
			{
				if (e.Length >= 2)
				{
					var result = BaseXmlSpawner.SetPropertyValue(null, obj, e.GetString(0), e.GetString(1));

					if (result == "Property has been set.")
						AddResponse(result);
					else
						LogFailure(result);
				}
				else
					LogFailure("Format: XmlSet <propertyName> <value>");
			}
		}


		[Usage("TagList property")]
		[Description("Lists the keyword taglist for a spawner")]
		public static void ShowTagList_OnCommand(CommandEventArgs e)
		{
			e.Mobile.Target = new TagListTarget(e);
		}

		private class TagListTarget : Target
		{
			private CommandEventArgs m_e;

			public TagListTarget(CommandEventArgs e)
				: base(30, false, TargetFlags.None)
			{
				m_e = e;
			}

			protected override void OnTarget(Mobile from, object targeted)
			{
				if (targeted is XmlSpawner)
					((XmlSpawner)targeted).ShowTagList((XmlSpawner)targeted);
			}
		}

		public void ShowTagList(XmlSpawner spawner)
		{
			var count = 0;
			Console.WriteLine("{0} tags", spawner.m_KeywordTagList.Count);
			foreach (var tag in spawner.m_KeywordTagList)
			{
				count++;
				Console.WriteLine("tag {0} : {1}", count, BaseXmlSpawner.TagInfo(tag));
			}
		}


		// added in targeting for the [xmlhome command
		private class XmlHomeTarget : Target
		{
			private CommandEventArgs m_e;

			public XmlHomeTarget(CommandEventArgs e)
				: base(30, false, TargetFlags.None)
			{
				m_e = e;
			}

			protected override void OnTarget(Mobile from, object targeted)
			{
				XmlSpawner spawner = null;

				if (targeted is XmlSpawner)
					if (m_e.GetString(0) == "status")
					{
						((XmlSpawner)targeted).ReportStatus();
						return;
					}


				if (targeted is Mobile)
					spawner = ((Mobile)targeted).Spawner as XmlSpawner;
				else if (targeted is Item) spawner = ((Item)targeted).Spawner as XmlSpawner;

				if (spawner == null)
				{
					from.SendMessage("Unable to find spawner for this object");
					return;
				}

				// check to make sure it is still on the spawner
				foreach (var so in spawner.m_SpawnObjects)
					for (var x = 0; x < so.SpawnedObjects.Count; x++)
					{
						var o = so.SpawnedObjects[x];

						if (o == targeted)
						{
							@from.SendMessage("{0}, {1}, {2}", spawner.X, spawner.Y, spawner.Z);

							if (m_e.GetString(0) == "go")
							{
								// make sure the spawner is not in a container.
								if (spawner.Parent == null)
								{
									@from.Location = new Point3D(spawner.Location);
									@from.Map = spawner.Map;
								}
								else
									@from.SendMessage("Spawner is in a container");
							}
							else if (m_e.GetString(0) == "send")
							{
								// make sure the spawner is not in a container.
								if (spawner.Parent == null)
								{
									if (o is Item)
									{
										((Item)o).Location = new Point3D(spawner.Location);
										((Item)o).Map = spawner.Map;
									}

									if (o is Mobile)
									{
										((Mobile)o).Location = new Point3D(spawner.Location);
										((Mobile)o).Map = spawner.Map;
									}
								}
								else
									@from.SendMessage("Spawner is in a container");
							}
							else if (m_e.GetString(0) == "gump") spawner.OnDoubleClick(@from);

							return;
						}
					}
			}
		}

		[Usage("XmlHome [go][gump][send]")]
		[Description(
			"Returns the coordinates of the spawner for the targeted object. Args: 'go' teleports to spawner, 'gump' opens spawner gump, 'send' sends mob home")]
		public static void XmlHome_OnCommand(CommandEventArgs e)
		{
			e.Mobile.Target = new XmlHomeTarget(e);
		}

		private static void XmlSaveDefaults(string filePath, Mobile m)
		{
			if (filePath == null || filePath.Length < 1) return;

			using (var op = new StreamWriter(filePath))
			{
				if (op == null) m.SendMessage("unable to open file {0}", filePath);
				var xml = new XmlTextWriter(op);

				xml.Formatting = Formatting.Indented;
				xml.IndentChar = '\t';
				xml.Indentation = 1;

				xml.WriteStartDocument(true);

				xml.WriteStartElement("XmlDefaults");

				xml.WriteStartElement("defProximityRange");
				xml.WriteString(defProximityRange.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defTriggerProbability");
				xml.WriteString(defTriggerProbability.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defProximityTriggerSound");
				xml.WriteString(defProximityTriggerSound.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defMinRefractory");
				xml.WriteString(defMinRefractory.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defMaxRefractory");
				xml.WriteString(defMaxRefractory.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defTODStart");
				xml.WriteString(defTODStart.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defTODEnd");
				xml.WriteString(defTODEnd.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defStackAmount");
				xml.WriteString(defAmount.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defDuration");
				xml.WriteString(defDuration.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defIsGroup");
				xml.WriteString(defIsGroup.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defTeam");
				xml.WriteString(defTeam.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defRelativeHome");
				xml.WriteString(defRelativeHome.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defSpawnRange");
				xml.WriteString(defSpawnRange.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defHomeRange");
				xml.WriteString(defHomeRange.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defMinDelay");
				xml.WriteString(defMinDelay.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defMaxDelay");
				xml.WriteString(defMaxDelay.ToString());
				xml.WriteEndElement();
				xml.WriteStartElement("defTODMode");
				xml.WriteString(defTODMode.ToString());
				xml.WriteEndElement();

				xml.WriteEndElement();

				xml.Close();
			}

			m.SendMessage("defaults saved to {0}", filePath);
		}

		public static void XmlLoadDefaults(string filePath, Mobile m)
		{
			if (m == null || m.Deleted) return;
			if (filePath != null && filePath.Length >= 1)
			{
				if (File.Exists(filePath))
				{
					var doc = new XmlDocument();
					doc.Load(filePath);

					var root = doc["XmlDefaults"];
					LoadDefaults(root);
					m.SendMessage("defaults loaded successfully from {0}", filePath);
				}
				else
					m.SendMessage("File {0} does not exist.", filePath);
			}
		}

		private static void LoadDefaults(XmlElement node)
		{
			try { defProximityRange = Int32.Parse(node["defProximityRange"].InnerText); }
			catch { }

			try { defTriggerProbability = Double.Parse(node["defTriggerProbability"].InnerText); }
			catch { }

			try { defProximityTriggerSound = Int32.Parse(node["defProximityTriggerSound"].InnerText); }
			catch { }

			try { defMinRefractory = TimeSpan.Parse(node["defMinRefractory"].InnerText); }
			catch { }

			try { defMaxRefractory = TimeSpan.Parse(node["defMaxRefractory"].InnerText); }
			catch { }

			try { defTODStart = TimeSpan.Parse(node["defTODStart"].InnerText); }
			catch { }

			try { defTODEnd = TimeSpan.Parse(node["defTODEnd"].InnerText); }
			catch { }

			try { defAmount = Int32.Parse(node["defStackAmount"].InnerText); }
			catch { }

			try { defDuration = TimeSpan.Parse(node["defDuration"].InnerText); }
			catch { }

			try { defIsGroup = Boolean.Parse(node["defIsGroup"].InnerText); }
			catch { }

			try { defTeam = Int32.Parse(node["defTeam"].InnerText); }
			catch { }

			try { defRelativeHome = Boolean.Parse(node["defRelativeHome"].InnerText); }
			catch { }

			try { defSpawnRange = Int32.Parse(node["defSpawnRange"].InnerText); }
			catch { }

			try { defHomeRange = Int32.Parse(node["defHomeRange"].InnerText); }
			catch { }

			try { defMinDelay = TimeSpan.Parse(node["defMinDelay"].InnerText); }
			catch { }

			try { defMaxDelay = TimeSpan.Parse(node["defMaxDelay"].InnerText); }
			catch { }

			var todmode = 0;
			try { todmode = Int32.Parse(node["defTODMode"].InnerText); }
			catch { }

			switch (todmode)
			{
				case (int)TODModeType.Realtime:
					defTODMode = TODModeType.Realtime;
					break;
				case (int)TODModeType.Gametime:
					defTODMode = TODModeType.Gametime;
					break;
			}
		}

		[Usage("XmlDefaults [defaultpropertyname value]")]
		[Description("Returns or changes the default settings of the spawner.")]
		public static void XmlDefaults_OnCommand(CommandEventArgs e)
		{
			var m = e.Mobile;
			if (m == null || m.Deleted) return;
			if (e.Arguments.Length >= 1)
			{
				// leave open the possibility of just requesting display of a single property
				if (e.Arguments.Length == 2)
				{
					if (e.Arguments[0].ToLower() == "save")
						XmlSaveDefaults(e.Arguments[1], m);
					else if (e.Arguments[0].ToLower() == "load")
						XmlLoadDefaults(e.Arguments[1], m);
					else
						// try to set the property
					if (e.Arguments[0].ToLower() == "maxdelay")
						try
						{
							defMaxDelay = TimeSpan.FromMinutes(Convert.ToDouble(e.Arguments[1]));
							m.SendMessage("MaxDelay = {0}", defMaxDelay);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "mindelay")
						try
						{
							defMinDelay = TimeSpan.FromMinutes(Convert.ToDouble(e.Arguments[1]));
							m.SendMessage("MinDelay = {0}", defMinDelay);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "spawnrange")
						try
						{
							defSpawnRange = Convert.ToInt32(e.Arguments[1]);
							m.SendMessage("SpawnRange = {0}", defSpawnRange);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "homerange")
						try
						{
							defHomeRange = Convert.ToInt32(e.Arguments[1]);
							m.SendMessage("HomeRange = {0}", defHomeRange);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "relativehome")
						try
						{
							defRelativeHome = Convert.ToBoolean(e.Arguments[1]);
							m.SendMessage("RelativeHome = {0}", defRelativeHome);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "proximitytriggersound")
						try
						{
							defProximityTriggerSound = Convert.ToInt32(e.Arguments[1]);
							m.SendMessage("ProximityTriggerSound = {0}", defProximityTriggerSound);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "proximityrange")
						try
						{
							defProximityRange = Convert.ToInt32(e.Arguments[1]);
							m.SendMessage("ProximityRange = {0}", defProximityRange);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "triggerprobability")
						try
						{
							defTriggerProbability = Convert.ToDouble(e.Arguments[1]);
							m.SendMessage("TriggerProbability = {0}", defTriggerProbability);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "todstart")
						try
						{
							defTODStart = TimeSpan.FromMinutes(Convert.ToDouble(e.Arguments[1]));
							m.SendMessage("TODStart = {0}", defTODStart);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "todend")
						try
						{
							defTODEnd = TimeSpan.FromMinutes(Convert.ToDouble(e.Arguments[1]));
							m.SendMessage("TODEnd = {0}", defTODEnd);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "stackamount")
						try
						{
							defAmount = Convert.ToInt32(e.Arguments[1]);
							m.SendMessage("StackAmount = {0}", defAmount);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "duration")
						try
						{
							defDuration = TimeSpan.FromMinutes(Convert.ToDouble(e.Arguments[1]));
							m.SendMessage("Duration = {0}", defDuration);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "group")
						try
						{
							defIsGroup = Convert.ToBoolean(e.Arguments[1]);
							m.SendMessage("Group = {0}", defIsGroup);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "team")
						try
						{
							defTeam = Convert.ToInt32(e.Arguments[1]);
							m.SendMessage("Team = {0}", defTeam);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "todmode")
					{
						var todmode = (int)TODModeType.Realtime;
						try
						{
							todmode = Convert.ToInt32(e.Arguments[1]);
							switch (todmode)
							{
								case (int)TODModeType.Gametime:
									defTODMode = TODModeType.Gametime;
									break;
								case (int)TODModeType.Realtime:
									defTODMode = TODModeType.Realtime;
									break;
							}

							m.SendMessage("TODMode = {0}", defTODMode);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					}
					else if (e.Arguments[0].ToLower() == "maxrefractory")
						try
						{
							defMaxRefractory = TimeSpan.FromMinutes(Convert.ToDouble(e.Arguments[1]));
							m.SendMessage("MaxRefractory = {0}", defMaxRefractory);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else if (e.Arguments[0].ToLower() == "minrefractory")
						try
						{
							defMinRefractory = TimeSpan.FromMinutes(Convert.ToDouble(e.Arguments[1]));
							m.SendMessage("MinRefractory = {0}", defMinRefractory);
						}
						catch { m.SendMessage("invalid value : {0}", e.Arguments[1]); }
					else
						m.SendMessage("{0} : no such default value.", e.Arguments[0]);
				}
			}
			else
			{
				// just display the values
				m.SendMessage("TriggerProbability = {0}", defTriggerProbability);
				m.SendMessage("ProximityRange = {0}", defProximityRange);
				m.SendMessage("ProximityTriggerSound = {0}", defProximityTriggerSound);
				m.SendMessage("MinRefractory = {0}", defMinRefractory);
				m.SendMessage("MaxRefractory = {0}", defMaxRefractory);
				m.SendMessage("TODStart = {0}", defTODStart);
				m.SendMessage("TODEnd = {0}", defTODEnd);
				m.SendMessage("TODMode = {0}", defTODMode);
				m.SendMessage("StackAmount = {0}", defAmount);
				m.SendMessage("Duration = {0}", defDuration);
				m.SendMessage("Group = {0}", defIsGroup);
				m.SendMessage("Team = {0}", defTeam);
				m.SendMessage("RelativeHome = {0}", defRelativeHome);
				m.SendMessage("SpawnRange = {0}", defSpawnRange);
				m.SendMessage("HomeRange = {0}", defHomeRange);
				m.SendMessage("MinDelay = {0}", defMinDelay);
				m.SendMessage("MaxDelay = {0}", defMaxDelay);
			}
		}


		[Usage("XmlSpawnerShowAll")]
		[Aliases("XmlShow")]
		[Description(
			"Makes all XmlSpawner objects movable and also changes the item id to a blue ships mast for easy identification.")]
		public static void ShowSpawnPoints_OnCommand(CommandEventArgs e)
		{
			var ToShow = new List<Item>();
			foreach (var item in World.Items.Values)
				if (item is XmlSpawner)
				{
					//turned off visibility. Admins will still see masts but players will not.
					item.Visible = false; // set the spawn item visibility
					item.Movable = false; // Make the spawn item movable
					item.Hue = 88; // Bright blue colour so its easy to spot
					item.ItemID = ShowItemId; // Ship Mast (Very tall, easy to see if beneath other objects)

					// find container-held spawners to be marked with an external static
					if (item.Parent != null && item.RootParent is Container) ToShow.Add(item);
				}

			// place the statics
			foreach (XmlSpawner xml_item in ToShow)
				// does the spawner already have a static attached to it? could happen if two showall commands are issued in a row.
				// if so then dont add another
				if ((xml_item.m_ShowContainerStatic == null || xml_item.m_ShowContainerStatic.Deleted) &&
				    xml_item.RootParent is Container)
				{
					var root_item = (Container)xml_item.RootParent;
					// calculate a world location for the static.  Position it just above the container
					var x = root_item.Location.X;
					var y = root_item.Location.Y;
					var z = root_item.Location.Z + 10;

					var s = new Static(ShowItemId);
					s.Visible = false;
					s.MoveToWorld(new Point3D(x, y, z), root_item.Map);

					xml_item.m_ShowContainerStatic = s;
				}
		}

		[Usage("XmlSpawnerHideAll")]
		[Aliases("XmlHide")]
		[Description("Makes all XmlSpawner objects invisible and unmovable returns the object id to the default.")]
		public static void HideSpawnPoints_OnCommand(CommandEventArgs e)
		{
			var ToDelete = new List<Item>();
			foreach (var item in World.Items.Values)
				if (item is XmlSpawner)
				{
					item.Visible = false;
					item.Movable = false;
					item.Hue = 0;
					item.ItemID = BaseItemId;

					// get rid of the external static marker for container-held spawners
					// check anything that might have been tagged with a container static
					var xml_item = (XmlSpawner)item;
					if (xml_item.m_ShowContainerStatic != null && !xml_item.m_ShowContainerStatic.Deleted)
						ToDelete.Add(item);
				}

			foreach (XmlSpawner xml_item in ToDelete)
				if (xml_item.m_ShowContainerStatic != null && !xml_item.m_ShowContainerStatic.Deleted)
					xml_item.m_ShowContainerStatic.Delete();
		}

		[Usage("XmlGo <map> | <map> <x> <y> [z]")]
		[Description("Go command used with spawn editor, takes the name of the map as the first parameter.")]
		private static void SpawnEditorGo_OnCommand(CommandEventArgs e)
		{
			if (e == null) return;

			var from = e.Mobile;

			// Make sure a map name was given at least
			if (from != null && e.Length >= 1)
			{
				// Get the map
				Map NewMap = null;
				var MapName = e.Arguments[0];

				// Convert the xml map value to a real map object
				if (String.Compare(MapName, Map.Trammel.Name, true) == 0)
					NewMap = Map.Trammel;
				else if (String.Compare(MapName, Map.Felucca.Name, true) == 0)
					NewMap = Map.Felucca;
				else if (String.Compare(MapName, Map.Ilshenar.Name, true) == 0)
					NewMap = Map.Ilshenar;
				else if (String.Compare(MapName, Map.Malas.Name, true) == 0)
					NewMap = Map.Malas;
				else if (String.Compare(MapName, Map.Tokuno.Name, true) == 0)
					NewMap = Map.Tokuno;
				else
				{
					from.SendMessage("Map '{0}' does not exist!", MapName);
					return;
				}

				// Now that the map has been determined, continue
				// Check if the request is to simply change maps
				if (e.Length == 1)
					// Map Change ONLY
					@from.Map = NewMap;
				else if (e.Length == 3)
				{
					// Map & X Y ONLY
					if (NewMap != null)
					{
						var x = e.GetInt32(1);
						var y = e.GetInt32(2);
						var z = NewMap.GetAverageZ(x, y);
						from.Map = NewMap;
						from.Location = new Point3D(x, y, z);
					}
				}
				else if (e.Length == 4)
				{
					// Map & X Y Z
					from.Map = NewMap;
					from.Location = new Point3D(e.GetInt32(1), e.GetInt32(2), e.GetInt32(3));
				}
				else
					@from.SendMessage("Format: XmlGo <map> | <map> <x> <y> [z]");
			}
		}

		[Usage("SmartStat [accesslevel Player/Counselor/GameMaster/Seer/Administrator]")]
		[Description("Returns the spawn reduction due to SmartSpawning.")]
		public static void SmartStat_OnCommand(CommandEventArgs e)
		{
			if (e == null || e.Mobile == null) return;

			if (e.Arguments.Length > 1 && e.Arguments[0].ToLower() == "accesslevel" &&
			    e.Mobile.AccessLevel >= AccessLevel.Administrator)
				try
				{
					SmartSpawnAccessLevel = (AccessLevel)Enum.Parse(typeof(AccessLevel), e.Arguments[1], true);
				}
				catch { }

			// handle the
			// number of spawners
			var count = 0;
			// number of actual spawns
			var currentcount = 0;
			var smartcount = 0;
			var inactivecount = 0;
			// maximum possible spawns
			var totalcount = 0;
			var maxcount = 0;
			// maximum possible of spawns that are currently inactivated
			var savings = 0;
			foreach (var item in World.Items.Values)
				if (item is XmlSpawner)
				{
					var spawner = (XmlSpawner)item;

					if (spawner.Deleted) continue;

					totalcount += spawner.MaxCount;
					// get the current count without defragging
					currentcount += spawner.SafeCurrentCount;
					count++;

					// check to see if smartspawning is set
					if (spawner.SmartSpawning)
					{
						smartcount++;
						maxcount += spawner.MaxCount;
					}

					if (spawner.IsInactivated)
					{
						inactivecount++;
						savings += spawner.MaxCount;
					}
				}

			var percent = 0;
			//if((currentcount + savings) > 0)
			//{
			//	percent = 100*savings/(currentcount + savings);
			//}

			var maxpercent = 0;
			if (totalcount > 0)
			{
				percent = 100 * savings / totalcount;
				maxpercent = 100 * maxcount / totalcount;
			}

			e.Mobile.SendMessage(
				"Running XmlSpawner version {9}\n" +
				"Smartspawning access level is {11}\n" +
				"--------------------------------\n" +
				"{0} XmlSpawners\n" +
				"{1} are configured for SmartSpawning\n" +
				"{2} are currently inactivated\n" +
				"{10} sectors being monitored\n" +
				"Maximum possible spawn count is {3}\n" +
				"Maximum possible spawn reduction is {4}\n" +
				"Current spawn count is {5}\n" +
				"Current spawn reduction is {6}\n" +
				"Maximum possible savings is {7}%\n" +
				"Current savings is {8}%",
				count, smartcount, inactivecount, totalcount, maxcount, currentcount, savings, maxpercent,
				percent, Version, totalSectorsMonitored, SmartSpawnAccessLevel);
		}

		[Usage("OptimalSmartSpawning [max spawn/homerange diff]")]
		[Description("Activates SmartSpawning on XmlSpawners that are well-suited for use of this feature.")]
		public static void OptimalSmartSpawning_OnCommand(CommandEventArgs e)
		{
			var maxdiff = 1;
			if (e.Arguments.Length > 0)
				try
				{
					maxdiff = Int32.Parse(e.Arguments[0]);
				}
				catch { }

			var count = 0;
			var maxcount = 0;
			foreach (var item in World.Items.Values)
				if (item is XmlSpawner)
				{
					var spawner = (XmlSpawner)item;

					// determine whether this spawner is a good candidate

					if (spawner.Deleted) continue;

					// ignore spawners in towns
					//if (Region.Find(spawner.Location, spawner.Map) is Regions.TownRegion) continue;

					// dont bother setting it on triggered spawners
					if (spawner.ProximityRange >= 0) continue;

					// check the relative spawnrange and homerange.  Dont set it on spawners with a larger homerange than spawnrange
					var width = spawner.m_Width;
					var height = spawner.m_Height;

					if (spawner.HomeRange * 2 > width + maxdiff * 2 ||
					    spawner.HomeRange * 2 > height + maxdiff * 2 && spawner.m_Region != null) continue;

					var nso = 0;

					if (spawner.m_SpawnObjects != null) nso = spawner.m_SpawnObjects.Count;

					// empty spawner so skip it
					if (nso == 0) continue;

					var skipit = false;

					// check the spawn types
					for (var i = 0; i < nso; ++i)
					{
						var so = spawner.m_SpawnObjects[i];

						if (so == null) continue;

						var typestr = so.TypeName;

						var type = SpawnerType.GetType(typestr);

						// if it has basevendors on it or invalid types, then skip it
						if (typestr == null || type != null &&
						    (type == typeof(BaseVendor) || type.IsSubclassOf(typeof(BaseVendor))) ||
						    type == null && !BaseXmlSpawner.IsTypeOrItemKeyword(typestr) &&
						    typestr.IndexOf('{') == -1 && !typestr.StartsWith("*") && !typestr.StartsWith("#"))
						{
							skipit = true;
							break;
						}
					}

					if (!skipit)
					{
						count++;
						spawner.SmartSpawning = true;
						maxcount += spawner.MaxCount;
					}
				}

			e.Mobile.SendMessage("Configured {0} XmlSpawners for SmartSpawning using maxdiff of {1}", count, maxdiff);
			e.Mobile.SendMessage("Estimated item/mob reduction is {0}", maxcount);
		}


		[Usage("XmlSpawnerWipe [SpawnerPrefixFilter]")]
		[Description("Removes all XmlSpawner objects from the current map.")]
		public static void Wipe_OnCommand(CommandEventArgs e)
		{
			WipeSpawners(e, false);
		}

		[Usage("XmlSpawnerWipeAll [SpawnerPrefixFilter]")]
		[Description("Removes all XmlSpawner objects from the entire world.")]
		public static void WipeAll_OnCommand(CommandEventArgs e)
		{
			WipeSpawners(e, true);
		}

		public static void XmlUnLoadFromFile(string filename, string SpawnerPrefix, Mobile from, out int processedmaps,
			out int processedspawners)
		{
			processedmaps = 0;
			processedspawners = 0;
			if (filename == null || filename.Length <= 0) return;

			var total_processed_maps = 0;
			var total_processed_spawners = 0;

			// Check if the file exists
			if (File.Exists(filename) == true)
			{
				FileStream fs = null;
				try
				{
					fs = File.Open(filename, FileMode.Open, FileAccess.Read);
				}
				catch { }

				if (fs == null)
				{
					if (from != null)
						from.SendMessage("Unable to open {0} for unloading", filename);
					return;
				}

				XmlUnLoadFromStream(fs, filename, SpawnerPrefix, from, out processedmaps, out processedspawners);
			}
			else
				// check to see if it is a directory
			if (Directory.Exists(filename) == true)
			{
				// if so then import all of the .xml files in the directory
				string[] files = null;
				try
				{
					files = Directory.GetFiles(filename, "*.xml");
				}
				catch { }

				if (files != null && files.Length > 0)
				{
					if (from != null)
						from.SendMessage("UnLoading {0} .xml files from directory {1}", files.Length, filename);
					foreach (var file in files)
					{
						XmlUnLoadFromFile(file, SpawnerPrefix, from, out processedmaps, out processedspawners);
						total_processed_maps += processedmaps;
						total_processed_spawners += processedspawners;
					}
				}

				// recursively search subdirectories for more .xml files
				string[] dirs = null;
				try
				{
					dirs = Directory.GetDirectories(filename);
				}
				catch { }

				if (dirs != null && dirs.Length > 0)
					foreach (var dir in dirs)
					{
						XmlUnLoadFromFile(dir, SpawnerPrefix, @from, out processedmaps, out processedspawners);
						total_processed_maps += processedmaps;
						total_processed_spawners += processedspawners;
					}

				if (from != null)
					from.SendMessage("UnLoaded a total of {0} .xml files and {2} spawners from directory {1}",
						total_processed_maps, filename, total_processed_spawners);
				processedmaps = total_processed_maps;
				processedspawners = total_processed_spawners;
			}
			else
			{
				if (from != null)
					from.SendMessage("{0} does not exist", filename);
			}
		}

		public static void XmlUnLoadFromStream(Stream fs, string filename, string SpawnerPrefix, Mobile from,
			out int processedmaps, out int processedspawners)
		{
			processedmaps = 0;
			processedspawners = 0;

			if (fs == null) return;


			var TotalCount = 0;
			var TrammelCount = 0;
			var FeluccaCount = 0;
			var IlshenarCount = 0;
			var MalasCount = 0;
			var TokunoCount = 0;
			var OtherCount = 0;
			var bad_spawner_count = 0;
			var spawners_deleted = 0;


			if (from != null)
				from.SendMessage(String.Format("UnLoading {0} objects{1} from file {2}.",
					"XmlSpawner",
					SpawnerPrefix != null && SpawnerPrefix.Length > 0
						? " beginning with " + SpawnerPrefix
						: String.Empty, filename));

			// Create the data set
			var ds = new DataSet(SpawnDataSetName);

			// Read in the file
			//ds.ReadXml( e.Arguments[0].ToString() );
			var fileerror = false;
			try
			{
				ds.ReadXml(fs);
			}
			catch
			{
				if (from != null)
					from.SendMessage(33, "Error reading xml file {0}", filename);
				fileerror = true;
			}

			// close the file
			fs.Close();
			if (fileerror) return;

			// Check that at least a single table was loaded
			if (ds.Tables != null && ds.Tables.Count > 0)
				// Add each spawn point to the current map
				if (ds.Tables[SpawnTablePointName] != null && ds.Tables[SpawnTablePointName].Rows.Count > 0)
					foreach (DataRow dr in ds.Tables[SpawnTablePointName].Rows)
					{
						// load in the spawner info.  Certain fields are required and therefore cannot be ignored
						// the exception handler for those will flag bad_spawner and the result will be logged

						// Each row makes up a single spawner
						var SpawnName = "Spawner";
						try { SpawnName = (string)dr["Name"]; }
						catch { }

						// Check if there is any spawner name criteria specified on the unload
						if (SpawnerPrefix == null || SpawnerPrefix.Length == 0 ||
						    SpawnName.StartsWith(SpawnerPrefix) == true)
						{
							var bad_spawner = false;
							// Try load the GUID (might not work so create a new GUID)
							var SpawnId = Guid.NewGuid();
							try { SpawnId = new Guid((string)dr["UniqueId"]); }
							catch { bad_spawner = true; }

							// have to have a GUID or no point in continuing
							if (bad_spawner)
							{
								bad_spawner_count++;
								continue;
							}

							// Get the map (default to the mobiles map)
							var SpawnMap = Map.Internal;
							var XmlMapName = SpawnMap.Name;

							// Try to get the "map" field, but in case it doesn't exist, catch and discard the exception
							try { XmlMapName = (string)dr["Map"]; }
							catch { }

							// Convert the xml map value to a real map object
							if (String.Compare(XmlMapName, Map.Trammel.Name, true) == 0 || XmlMapName == "Trammel")
							{
								SpawnMap = Map.Trammel;
								TrammelCount++;
							}
							else if (String.Compare(XmlMapName, Map.Felucca.Name, true) == 0 || XmlMapName == "Felucca")
							{
								SpawnMap = Map.Felucca;
								FeluccaCount++;
							}
							else if (String.Compare(XmlMapName, Map.Ilshenar.Name, true) == 0 ||
							         XmlMapName == "Ilshenar")
							{
								SpawnMap = Map.Ilshenar;
								IlshenarCount++;
							}
							else if (String.Compare(XmlMapName, Map.Malas.Name, true) == 0 || XmlMapName == "Malas")
							{
								SpawnMap = Map.Malas;
								MalasCount++;
							}
							else if (String.Compare(XmlMapName, Map.Tokuno.Name, true) == 0 || XmlMapName == "Tokuno")
							{
								SpawnMap = Map.Tokuno;
								TokunoCount++;
							}
							else
							{
								try
								{
									SpawnMap = Map.Parse(XmlMapName);
								}
								catch { }

								OtherCount++;
							}

							// Check if this spawner already exists
							XmlSpawner OldSpawner = null;
							foreach (var i in World.Items.Values)
								if (i is XmlSpawner)
								{
									var CheckXmlSpawner = (XmlSpawner)i;
									// Check if the spawners GUID is the same as the one being unloaded
									// and that the spawners map is the same as the one being unloaded
									if (CheckXmlSpawner.UniqueId == SpawnId.ToString()
									    /*&& ( CheckXmlSpawner.Map == SpawnMap )*/)
									{
										OldSpawner = (XmlSpawner)i;
										if (OldSpawner != null)
										{
											spawners_deleted++;
											OldSpawner.Delete();
										}

										break;
									}
								}
						}

						TotalCount++;
					}

			try
			{
				fs.Close();
			}
			catch { }

			if (from != null)
				from.SendMessage(
					"{0}/{8} spawner(s) were unloaded using file {1} [Trammel={2}, Felucca={3}, Ilshenar={4}, Malas={5}, Tokuno={6}, Other={7}].",
					spawners_deleted, filename, TrammelCount, FeluccaCount, IlshenarCount, MalasCount, TokunoCount,
					OtherCount, TotalCount);
			if (bad_spawner_count > 0)
				if (@from != null)
					@from.SendMessage(33, "{0} bad spawners detected.", bad_spawner_count);
			processedmaps = 1;
			processedspawners = TotalCount;
		}

		[Usage("XmlSpawnerUnLoad <SpawnFile or directory> [SpawnerPrefixFilter]")]
		[Aliases("XmlUnload")]
		[Description("UnLoads XmlSpawner objects from the proper map as defined in the file supplied.")]
		public static void UnLoad_OnCommand(CommandEventArgs e)
		{
			if (e.Mobile.AccessLevel >= DiskAccessLevel)
			{
				if (e.Arguments.Length >= 1)
				{
					// Spawner unload criteria (if any)
					var SpawnerPrefix = String.Empty;

					// Check if there is an argument provided (load criteria)
					if (e.Arguments.Length > 1)
						SpawnerPrefix = e.Arguments[1];

					var filename = LocateFile(e.Arguments[0].ToString());
					int processedmaps;
					int processedspawners;
					XmlUnLoadFromFile(filename, SpawnerPrefix, e.Mobile, out processedmaps, out processedspawners);
				}
				else
					e.Mobile.SendMessage("Usage:  {0} <SpawnFile or directory>", e.Command);
			}
			else
				e.Mobile.SendMessage("You do not have rights to perform this command.");
		}

		[Usage("XmlImportMap <mapfile or directory>")]
		[Description("Loads spawner definitions from a .map file")]
		public static void XmlImportMap_OnCommand(CommandEventArgs e)
		{
			if (e.Mobile.AccessLevel >= DiskAccessLevel)
			{
				if (e.Arguments.Length >= 1)
				{
					var filename = e.Arguments[0].ToString();

					int processedmaps;
					int processedspawners;
					XmlImportMap(filename, e.Mobile, out processedmaps, out processedspawners);
				}
				else
					e.Mobile.SendMessage("Usage:  {0} <MapFile>", e.Command);
			}
			else
				e.Mobile.SendMessage("You do not have rights to perform this command.");
		}

		public static void XmlImportMap(string filename, Mobile from, out int processedmaps, out int processedspawners)
		{
			processedmaps = 0;
			processedspawners = 0;
			var total_processed_maps = 0;
			var total_processed_spawners = 0;
			if (filename == null || filename.Length <= 0 || from == null || from.Deleted) return;
			// Check if the file exists
			if (File.Exists(filename) == true)
			{
				var spawnercount = 0;
				var badspawnercount = 0;
				var linenumber = 0;
				// default is no map override, use the map spec from each spawn line
				var overridemap = -1;
				double overridemintime = -1;
				double overridemaxtime = -1;
				var newformat = false;
				try
				{
					// Create an instance of StreamReader to read from a file.
					// The using statement also closes the StreamReader.
					using (var sr = new StreamReader(filename))
					{
						string line;
						// Read and display lines from the file until the end of
						// the file is reached.
						while ((line = sr.ReadLine()) != null)
						{
							// the old format of each .map line is * Dragon:Wyvern 5209 965 -40 2 2 10 50 30 1
							//  * typename:typename:... x y z map mindelay maxdelay homerange spawnrange maxcount
							// * | typename:typename:... |s1 |s2 |s3 |s4 |s5 | x | y | z | map | mindelay maxdelay homerange spawnrange spawnid maxcount | maxcount1 | maxcount2 | maxcount3 | maxcount4 | maxcount5
							// where s1-5 are additional spawn type entries with their own maxcounts
							// the new format of each .map line is  * |Dragon:Wyvern| spawns:spawns| | | | | 5209 | 965 | -40 | 2 | 2 | 10 | 50 | 30 | 1

							linenumber++;
							// is this the new format?
							string[] args;
							if (line.IndexOf('|') >= 0)
							{
								args = line.Trim().Split('|');
								newformat = true;
							}
							else
								args = line.Trim().Split(' ');

							// determine the format of this line and parse accordingly
							if (newformat)
								ParseNewMapFormat(@from, filename, line, args, linenumber, ref spawnercount,
									ref badspawnercount, ref overridemap, ref overridemintime, ref overridemaxtime);
							else
								ParseOldMapFormat(@from, filename, line, args, linenumber, ref spawnercount,
									ref badspawnercount, ref overridemap, ref overridemintime, ref overridemaxtime);
						}

						sr.Close();
					}
				}
				catch (Exception e)
				{
					// Let the user know what went wrong.
					from.SendMessage("The file could not be read: {0}", e.Message);
				}

				from.SendMessage("Imported {0} spawners from {1}", spawnercount, filename);
				from.SendMessage("{0} bad spawners detected", badspawnercount);
				processedmaps = 1;
				processedspawners = spawnercount;
			}
			else
				// check to see if it is a directory
			if (Directory.Exists(filename) == true)
			{
				// if so then import all of the .map files in the directory
				string[] files = null;
				try
				{
					files = Directory.GetFiles(filename, "*.map");
				}
				catch { }

				if (files != null && files.Length > 0)
				{
					from.SendMessage("Importing {0} .map files from directory {1}", files.Length, filename);
					foreach (var file in files)
					{
						XmlImportMap(file, from, out processedmaps, out processedspawners);
						total_processed_maps += processedmaps;
						total_processed_spawners += processedspawners;
					}
				}

				// recursively search subdirectories for more .map files
				string[] dirs = null;
				try
				{
					dirs = Directory.GetDirectories(filename);
				}
				catch { }

				if (dirs != null && dirs.Length > 0)
					foreach (var dir in dirs)
					{
						XmlImportMap(dir, @from, out processedmaps, out processedspawners);
						total_processed_maps += processedmaps;
						total_processed_spawners += processedspawners;
					}

				from.SendMessage("Imported a total of {0} .map files and {2} spawners from directory {1}",
					total_processed_maps, filename, total_processed_spawners);
				processedmaps = total_processed_maps;
				processedspawners = total_processed_spawners;
			}
			else
				@from.SendMessage("{0} does not exist", filename);
		}

		private static void ParseNewMapFormat(Mobile from, string filename, string line, string[] args, int linenumber,
			ref int spawnercount, ref int badspawnercount, ref int overridemap, ref double overridemintime,
			ref double overridemaxtime)
		{
			// format of each .map line is * Dragon:Wyvern 5209 965 -40 2 2 10 50 30 1
			//  * typename:typename:... x y z map mindelay maxdelay homerange spawnrange maxcount
			// or
			//  * typename:typename:... x y z map mindelay maxdelay homerange spawnrange spawnid maxcount
			// ## are comments
			// overridemap mapnumber
			// map 0 is tram+fel
			// map 1 is fel
			// map 2 is tram
			// map 3 is ilsh
			// map 4 is mal
			// map 5 is tokuno
			//
			// * | typename:typename:... | | | | | | x | y | z | map | mindelay maxdelay homerange spawnrange spawnid maxcount1 | maxcount2 | maxcount2 | maxcount3 | maxcount4 | maxcount5 | maxcount6
			// the new format of each .map line is  * |Dragon:Wyvern| spawns:spawns| | | | | 5209 | 965 | -40 | 2 | 2 | 10 | 50 | 30 | 1

			if (args == null || from == null) return;

			// look for the override keyword
			if (args.Length == 2 && args[0].ToLower() == "overridemap")
				try
				{
					overridemap = Int32.Parse(args[1]);
				}
				catch { }
			else if (args.Length == 2 && args[0].ToLower() == "overridemintime")
				try
				{
					overridemintime = Double.Parse(args[1]);
				}
				catch { }
			else if (args.Length == 2 && args[0].ToLower() == "overridemaxtime")
				try
				{
					overridemaxtime = Double.Parse(args[1]);
				}
				catch { }
			else
				// look for a spawn spec line
			if (args.Length > 0 && args[0] == "*")
			{
				var badspawn = false;
				var x = 0;
				var y = 0;
				var z = 0;
				var map = 0;
				double mindelay = 0;
				double maxdelay = 0;
				var homerange = 0;
				var spawnrange = 0;

				var spawnid = 0;
				var typenames = new string[6][];

				var maxcount = new int[6];

				// parse the main args

				try
				{
					// get the list of spawns
					for (var k = 0; k < 6; k++)
						typenames[k] = args[k + 1].Split(':');

					x = Int32.Parse(args[7]);
					y = Int32.Parse(args[8]);
					z = Int32.Parse(args[9]);
					map = Int32.Parse(args[10]);
					mindelay = Double.Parse(args[11]);
					maxdelay = Double.Parse(args[12]);
					homerange = Int32.Parse(args[13]);
					spawnrange = Int32.Parse(args[14]);
					spawnid = Int32.Parse(args[15]);

					for (var k = 0; k < 6; k++)
						maxcount[k] = Int32.Parse(args[k + 16]);
				}
				catch
				{
					from.SendMessage("Parsing error at line {0}", linenumber);
					badspawn = true;
				}

				// compute the total number of spawns
				var totalspawns = 0;
				var totalmaxcount = 0;

				for (var k = 0; k < 6; k++)
				{
					if (typenames[k] == null) continue;

					for (var i = 0; i < typenames[k].Length; i++)
					{
						if (typenames[k][i] == null || typenames[k][i].Length == 0) continue;

						totalspawns++;
					}

					totalmaxcount += maxcount[k];
				}

				// apply min/maxdelay overrides
				if (overridemintime != -1) mindelay = overridemintime;
				if (overridemaxtime != -1) maxdelay = overridemaxtime;
				if (mindelay > maxdelay) maxdelay = mindelay;

				if (!badspawn && totalspawns > 0)
				{
					// everything seems ok so go ahead and make the spawner
					// check for map override
					if (overridemap >= 0) map = overridemap;
					var spawnmap = Map.Internal;
					switch (map)
					{
						case 0:
							spawnmap = Map.Felucca;
							// note it also does trammel
							break;
						case 1:
							spawnmap = Map.Felucca;
							break;
						case 2:
							spawnmap = Map.Trammel;
							break;
						case 3:
							spawnmap = Map.Ilshenar;
							break;
						case 4:
							spawnmap = Map.Malas;
							break;
						case 5:
							spawnmap = Map.Tokuno;
							break;
					}

					if (!IsValidMapLocation(x, y, spawnmap))
					{
						// invalid so dont spawn it
						badspawnercount++;
						from.SendMessage("Invalid map/location at line {0}", linenumber);
						from.SendMessage("Bad spawn at line {1}: {0}", line, linenumber);
						return;
					}

					// allow it to make an xmlspawner instead
					// first add all of the creatures on the list
					var so = new SpawnObject[totalspawns];
					var count = 0;
					var hasvendor = true;
					for (var k = 0; k < 6; k++)
					{
						if (typenames[k] == null) continue;

						for (var i = 0; i < typenames[k].Length; i++)
						{
							if (typenames[k][i] == null || typenames[k][i].Length == 0 || count > totalspawns) continue;

							so[count++] = new SpawnObject(typenames[k][i], maxcount[k]);

							// check the type to see if there are vendors on it
							var type = SpawnerType.GetType(typenames[k][i]);

							// check for vendor-only spawners which get special spawnrange treatment
							if (type != null && type != typeof(BaseVendor) && !type.IsSubclassOf(typeof(BaseVendor)))
								hasvendor = false;
						}
					}

					// assign it a unique id
					var SpawnId = Guid.NewGuid();

					// and give it a name based on the spawner count and file
					var spawnername =
						String.Format("{0}#{1}", Path.GetFileNameWithoutExtension(filename), spawnercount);

					// Create the new xml spawner
					var spawner = new XmlSpawner(SpawnId, x, y, 0, 0, spawnername, totalmaxcount,
						TimeSpan.FromMinutes(mindelay), TimeSpan.FromMinutes(maxdelay), TimeSpan.FromMinutes(0), -1,
						defaultTriggerSound, 1,
						0, homerange, false, so, TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(0),
						TimeSpan.FromMinutes(0),
						TimeSpan.FromMinutes(0), null, null, null, null, null,
						null, null, null, null, 1, null, false, defTODMode, defKillReset, false, -1, null, false, false,
						false, null,
						TimeSpan.FromHours(0), null, false, null);

					if (hasvendor)
						// force vendor spawners to behave like the distro
						spawner.SpawnRange = 0;
					else
						spawner.SpawnRange = spawnrange;

					spawner.m_PlayerCreated = true;
					string fromname = null;
					if (from != null) fromname = from.Name;
					spawner.LastModifiedBy = fromname;
					spawner.FirstModifiedBy = fromname;
					spawner.MoveToWorld(new Point3D(x, y, z), spawnmap);
					if (spawner.Map == Map.Internal)
					{
						badspawnercount++;
						spawner.Delete();
						from.SendMessage("Invalid map at line {0}", linenumber);
						from.SendMessage("Bad spawn at line {1}: {0}", line, linenumber);
						return;
					}

					spawnercount++;
					// handle the special case of map 0 that also needs to do trammel
					if (map == 0)
					{
						spawnmap = Map.Trammel;
						// assign it a unique id
						SpawnId = Guid.NewGuid();
						// Create the new xml spawner
						spawner = new XmlSpawner(SpawnId, x, y, 0, 0, spawnername, totalmaxcount,
							TimeSpan.FromMinutes(mindelay), TimeSpan.FromMinutes(maxdelay), TimeSpan.FromMinutes(0), -1,
							defaultTriggerSound, 1,
							0, homerange, false, so, TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(0),
							TimeSpan.FromMinutes(0),
							TimeSpan.FromMinutes(0), null, null, null, null, null,
							null, null, null, null, 1, null, false, defTODMode, defKillReset, false, -1, null, false,
							false, false, null,
							TimeSpan.FromHours(0), null, false, null);

						spawner.SpawnRange = spawnrange;
						spawner.m_PlayerCreated = true;

						spawner.LastModifiedBy = fromname;
						spawner.FirstModifiedBy = fromname;
						spawner.MoveToWorld(new Point3D(x, y, z), spawnmap);
						if (spawner.Map == Map.Internal)
						{
							badspawnercount++;
							spawner.Delete();
							from.SendMessage("Bad spawn at line {1}: {0}", line, linenumber);
							return;
						}

						spawnercount++;
					}
				}
				else
				{
					badspawnercount++;
					from.SendMessage("Bad spawn at line {1}: {0}", line, linenumber);
				}
			}
		}

		private static void ParseOldMapFormat(Mobile from, string filename, string line, string[] args, int linenumber,
			ref int spawnercount, ref int badspawnercount, ref int overridemap, ref double overridemintime,
			ref double overridemaxtime)
		{
			// format of each .map line is * Dragon:Wyvern 5209 965 -40 2 2 10 50 30 1
			//  * typename:typename:... x y z map mindelay maxdelay homerange spawnrange maxcount
			// or
			//  * typename:typename:... x y z map mindelay maxdelay homerange spawnrange spawnid maxcount
			// ## are comments
			// overridemap mapnumber
			// map 0 is tram+fel
			// map 1 is fel
			// map 2 is tram
			// map 3 is ilsh
			// map 4 is mal
			// map 5 is tokuno
			//
			// * | typename:typename:... | | | | | | x | y | z | map | mindelay maxdelay homerange spawnrange spawnid maxcount | maxcount2 | maxcount2 | maxcount3 | maxcount4 | maxcount5
			// the new format of each .map line is  * |Dragon:Wyvern| spawns:spawns| | | | | 5209 | 965 | -40 | 2 | 2 | 10 | 50 | 30 | 1

			if (args == null || from == null) return;

			// look for the override keyword
			if (args.Length == 2 && args[0].ToLower() == "overridemap")
				try
				{
					overridemap = Int32.Parse(args[1]);
				}
				catch { }
			else if (args.Length == 2 && args[0].ToLower() == "overridemintime")
				try
				{
					overridemintime = Double.Parse(args[1]);
				}
				catch { }
			else if (args.Length == 2 && args[0].ToLower() == "overridemaxtime")
				try
				{
					overridemaxtime = Double.Parse(args[1]);
				}
				catch { }
			else
				// look for a spawn spec line
			if (args.Length > 0 && args[0] == "*")
			{
				var badspawn = false;
				var x = 0;
				var y = 0;
				var z = 0;
				var map = 0;
				double mindelay = 0;
				double maxdelay = 0;
				var homerange = 0;
				var spawnrange = 0;
				var maxcount = 0;
				var spawnid = 0;
				string[] typenames = null;
				if (args.Length != 11 && args.Length != 12)
				{
					badspawn = true;
					from.SendMessage("Invalid arg count {1} at line {0}", linenumber, args.Length);
				}
				else
				{
					// get the list of spawns
					typenames = args[1].Split(':');
					// parse the rest of the args

					if (args.Length == 11)
						try
						{
							x = Int32.Parse(args[2]);
							y = Int32.Parse(args[3]);
							z = Int32.Parse(args[4]);
							map = Int32.Parse(args[5]);
							mindelay = Double.Parse(args[6]);
							maxdelay = Double.Parse(args[7]);
							homerange = Int32.Parse(args[8]);
							spawnrange = Int32.Parse(args[9]);
							maxcount = Int32.Parse(args[10]);
						}
						catch
						{
							@from.SendMessage("Parsing error at line {0}", linenumber);
							badspawn = true;
						}
					else if (args.Length == 12)
						try
						{
							x = Int32.Parse(args[2]);
							y = Int32.Parse(args[3]);
							z = Int32.Parse(args[4]);
							map = Int32.Parse(args[5]);
							mindelay = Double.Parse(args[6]);
							maxdelay = Double.Parse(args[7]);
							homerange = Int32.Parse(args[8]);
							spawnrange = Int32.Parse(args[9]);
							spawnid = Int32.Parse(args[10]);
							maxcount = Int32.Parse(args[11]);
						}
						catch
						{
							@from.SendMessage("Parsing error at line {0}", linenumber);
							badspawn = true;
						}
				}


				// apply mi/maxdelay overrides
				if (overridemintime != -1) mindelay = overridemintime;
				if (overridemaxtime != -1) maxdelay = overridemaxtime;
				if (mindelay > maxdelay) maxdelay = mindelay;

				if (!badspawn && typenames != null && typenames.Length > 0)
				{
					// everything seems ok so go ahead and make the spawner
					// check for map override
					if (overridemap >= 0) map = overridemap;
					var spawnmap = Map.Internal;
					switch (map)
					{
						case 0:
							spawnmap = Map.Felucca;
							// note it also does trammel
							break;
						case 1:
							spawnmap = Map.Felucca;
							break;
						case 2:
							spawnmap = Map.Trammel;
							break;
						case 3:
							spawnmap = Map.Ilshenar;
							break;
						case 4:
							spawnmap = Map.Malas;
							break;
						case 5:
							spawnmap = Map.Tokuno;
							break;
					}

					if (!IsValidMapLocation(x, y, spawnmap))
					{
						// invalid so dont spawn it
						badspawnercount++;
						from.SendMessage("Invalid map/location at line {0}", linenumber);
						from.SendMessage("Bad spawn at line {1}: {0}", line, linenumber);
						return;
					}

					// allow it to make an xmlspawner instead
					// first add all of the creatures on the list
					var so = new SpawnObject[typenames.Length];

					var hasvendor = true;
					for (var i = 0; i < typenames.Length; i++)
					{
						so[i] = new SpawnObject(typenames[i], maxcount);

						// check the type to see if there are vendors on it
						var type = SpawnerType.GetType(typenames[i]);

						// check for vendor-only spawners which get special spawnrange treatment
						if (type != null && type != typeof(BaseVendor) && !type.IsSubclassOf(typeof(BaseVendor)))
							hasvendor = false;
					}

					// assign it a unique id
					var SpawnId = Guid.NewGuid();

					// and give it a name based on the spawner count and file
					var spawnername =
						String.Format("{0}#{1}", Path.GetFileNameWithoutExtension(filename), spawnercount);

					// Create the new xml spawner
					var spawner = new XmlSpawner(SpawnId, x, y, 0, 0, spawnername, maxcount,
						TimeSpan.FromMinutes(mindelay), TimeSpan.FromMinutes(maxdelay), TimeSpan.FromMinutes(0), -1,
						defaultTriggerSound, 1,
						0, homerange, false, so, TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(0),
						TimeSpan.FromMinutes(0),
						TimeSpan.FromMinutes(0), null, null, null, null, null,
						null, null, null, null, 1, null, false, defTODMode, defKillReset, false, -1, null, false, false,
						false, null,
						TimeSpan.FromHours(0), null, false, null);

					if (hasvendor)
						// force vendor spawners to behave like the distro
						spawner.SpawnRange = 0;
					else
						spawner.SpawnRange = spawnrange;

					spawner.m_PlayerCreated = true;
					string fromname = null;
					if (from != null) fromname = from.Name;
					spawner.LastModifiedBy = fromname;
					spawner.FirstModifiedBy = fromname;
					spawner.MoveToWorld(new Point3D(x, y, z), spawnmap);
					if (spawner.Map == Map.Internal)
					{
						badspawnercount++;
						spawner.Delete();
						from.SendMessage("Invalid map at line {0}", linenumber);
						from.SendMessage("Bad spawn at line {1}: {0}", line, linenumber);
						return;
					}

					spawnercount++;
					// handle the special case of map 0 that also needs to do trammel
					if (map == 0)
					{
						spawnmap = Map.Trammel;
						// assign it a unique id
						SpawnId = Guid.NewGuid();
						// Create the new xml spawner
						spawner = new XmlSpawner(SpawnId, x, y, 0, 0, spawnername, maxcount,
							TimeSpan.FromMinutes(mindelay), TimeSpan.FromMinutes(maxdelay), TimeSpan.FromMinutes(0), -1,
							defaultTriggerSound, 1,
							0, homerange, false, so, TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(0),
							TimeSpan.FromMinutes(0),
							TimeSpan.FromMinutes(0), null, null, null, null, null,
							null, null, null, null, 1, null, false, defTODMode, defKillReset, false, -1, null, false,
							false, false, null,
							TimeSpan.FromHours(0), null, false, null);

						spawner.SpawnRange = spawnrange;
						spawner.m_PlayerCreated = true;

						spawner.LastModifiedBy = fromname;
						spawner.FirstModifiedBy = fromname;
						spawner.MoveToWorld(new Point3D(x, y, z), spawnmap);
						if (spawner.Map == Map.Internal)
						{
							badspawnercount++;
							spawner.Delete();
							from.SendMessage("Bad spawn at line {1}: {0}", line, linenumber);
							return;
						}

						spawnercount++;
					}
				}
				else
				{
					badspawnercount++;
					from.SendMessage("Bad spawn at line {1}: {0}", line, linenumber);
				}
			}
		}


		//------------------------------------------------------------------------------------------------------------------------------------
		// The following code was taken from Sno's xml exporter package and slightly modified to create xmlspawners instead of regular spawners
		//------------------------------------------------------------------------------------------------------------------------------------
		[Usage("XmlImportSpawners filename")]
		[Description("Loads xml files created by Sno's xml exporter as xmlspawners.")]
		public static void XmlImportSpawners_OnCommand(CommandEventArgs e)
		{
			if (e.Arguments.Length >= 1)
			{
				var filename = e.GetString(0);
				var filePath = Path.Combine("Saves/Spawners", filename);
				if (File.Exists(filePath))
				{
					var doc = new XmlDocument();
					try
					{
						doc.Load(filePath);
					}
					catch
					{
						e.Mobile.SendMessage("unable to load file {0}.", filePath);
						return;
					}

					var root = doc["spawners"];
					int successes = 0, failures = 0;
					if (root != null && root.GetElementsByTagName("spawner") != null)
						foreach (XmlElement spawner in root.GetElementsByTagName("spawner"))
							try
							{
								ImportSpawner(spawner, e.Mobile);
								successes++;
							}
							catch (Exception ex)
							{
								e.Mobile.SendMessage(33, "{0} {1}", ex.Message, spawner.InnerText);
								failures++;
							}

					e.Mobile.SendMessage("{0} spawners loaded successfully from {1}, {2} failures.", successes,
						filePath, failures);
				}
				else
					e.Mobile.SendMessage("File {0} does not exist.", filePath);
			}
			else
				e.Mobile.SendMessage("Usage: [XmlImportSpawners <filename>");
		}

		private static string GetText(XmlElement node, string defaultValue)
		{
			if (node == null)
				return defaultValue;
			return node.InnerText;
		}

		private static void ImportSpawner(XmlElement node, Mobile from)
		{
			var count = Int32.Parse(GetText(node["count"], "1"));
			var homeRange = Int32.Parse(GetText(node["homerange"], "4"));
			var walkingRange = Int32.Parse(GetText(node["walkingrange"], "-1"));
			// width of the spawning area
			var spawnwidth = homeRange * 2;
			if (walkingRange >= 0) spawnwidth = walkingRange * 2;

			var team = Int32.Parse(GetText(node["team"], "0"));
			var group = Boolean.Parse(GetText(node["group"], "False"));
			var maxDelay = TimeSpan.Parse(GetText(node["maxdelay"], "10:00"));
			var minDelay = TimeSpan.Parse(GetText(node["mindelay"], "05:00"));
			var creaturesName = LoadCreaturesName(node["creaturesname"]);
			var name = GetText(node["name"], "Spawner");
			var location = Point3D.Parse(GetText(node["location"], "Error"));
			var map = Map.Parse(GetText(node["map"], "Error"));

			// allow it to make an xmlspawner instead
			// first add all of the creatures on the list
			var so = new SpawnObject[creaturesName.Count];

			var hasvendor = false;

			for (var i = 0; i < creaturesName.Count; i++)
			{
				so[i] = new SpawnObject(creaturesName[i], count);
				// check the type to see if there are vendors on it
				var type = SpawnerType.GetType(creaturesName[i]);

				// if it has basevendors on it or invalid types, then skip it
				if (type != null && (type == typeof(BaseVendor) || type.IsSubclassOf(typeof(BaseVendor))))
					hasvendor = true;
			}

			// assign it a unique id
			var SpawnId = Guid.NewGuid();

			// Create the new xml spawner
			var spawner = new XmlSpawner(SpawnId, location.X, location.Y, spawnwidth, spawnwidth, name, count,
				minDelay, maxDelay, TimeSpan.FromMinutes(0), -1, defaultTriggerSound, 1,
				team, homeRange, false, so, TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(0),
				TimeSpan.FromMinutes(0), null, null, null, null, null,
				null, null, null, null, 1, null, group, defTODMode, defKillReset, false, -1, null, false, false, false,
				null, defDespawnTime, null, false, null);

			if (hasvendor)
				spawner.SpawnRange = 0;
			else
				spawner.SpawnRange = homeRange;
			spawner.m_PlayerCreated = true;
			string fromname = null;
			if (from != null) fromname = from.Name;
			spawner.LastModifiedBy = fromname;
			spawner.FirstModifiedBy = fromname;

			spawner.MoveToWorld(location, map);
			if (!IsValidMapLocation(location, spawner.Map))
			{
				spawner.Delete();
				throw new Exception("Invalid spawner location.");
			}
		}

		private static List<string> LoadCreaturesName(XmlElement node)
		{
			var names = new List<string>();

			if (node != null)
				foreach (XmlElement ele in node.GetElementsByTagName("creaturename"))
					if (ele != null)
						names.Add(ele.InnerText);
			return names;
		}
		//------------------------------------------------------------------------------------------------------------------------------------
		// end of modified xml importer by Sno
		//------------------------------------------------------------------------------------------------------------------------------------


		[Usage("XmlImportMSF filename")]
		[Description("Loads msf files created by Morxeton's megaspawner as xmlspawners.")]
		public static void XmlImportMSF_OnCommand(CommandEventArgs e)
		{
			if (e.Arguments.Length >= 1)
			{
				/*
				// I'm not sure what the default location for .msf files is
				string filename = e.GetString( 0 );
				string filePath = Path.Combine( "Data/Megaspawner", filename );
				*/
				var filePath = e.GetString(0);
				if (File.Exists(filePath))
				{
					var doc = new XmlDocument();
					doc.Load(filePath);
					var root = doc["MegaSpawners"];
					if (root != null)
					{
						int successes = 0, failures = 0;
						foreach (XmlElement spawner in root.GetElementsByTagName("MegaSpawner"))
							try
							{
								ImportMegaSpawner(e.Mobile, spawner);
								successes++;
							}
							catch (Exception ex)
							{
								e.Mobile.SendMessage(33, "{0} {1}", ex.Message, spawner.InnerText);
								failures++;
							}

						e.Mobile.SendMessage("{0} megaspawners loaded successfully from {1}, {2} failures.", successes,
							filePath, failures);
					}
					else
						e.Mobile.SendMessage("Invalid .msf file. No MegaSpawners node found");
				}
				else
					e.Mobile.SendMessage("File {0} does not exist.", filePath);
			}
			else
				e.Mobile.SendMessage("Usage: [XmlImportMSF <filename>");
		}

		private static void ImportMegaSpawner(Mobile from, XmlElement node)
		{
			var name = GetText(node["Name"], "MegaSpawner");
			var running = Boolean.Parse(GetText(node["Active"], "True"));
			var location = Point3D.Parse(GetText(node["Location"], "Error"));
			var map = Map.Parse(GetText(node["Map"], "Error"));


			var team = 0;
			var group = false;
			var maxcount = 0; // default maxcount of the spawner
			var homeRange = 4; // default homerange
			var spawnRange = 4; // default homerange
			var maxDelay = TimeSpan.FromMinutes(10);
			var minDelay = TimeSpan.FromMinutes(5);

			var listnode = node["EntryLists"];

			var nentries = 0;
			SpawnObject[] so = null;


			if (listnode != null)
			{
				// get the number of entries
				if (listnode.HasAttributes)
				{
					var attr = listnode.Attributes;

					nentries = Int32.Parse(attr.GetNamedItem("count").Value);
				}

				if (nentries > 0)
				{
					so = new SpawnObject[nentries];

					var entrycount = 0;
					var diff = false;
					foreach (XmlElement entrynode in listnode.GetElementsByTagName("EntryList"))
						// go through each entry and add a spawn object for it
						if (entrynode != null)
						{
							if (entrycount == 0)
							{
								// get the spawner defaults from the first entry
								// dont handle the individually specified entry attributes
								@group = Boolean.Parse(GetText(entrynode["GroupSpawn"], "False"));
								maxDelay = TimeSpan.FromSeconds(Int32.Parse(GetText(entrynode["MaxDelay"], "10:00")));
								minDelay = TimeSpan.FromSeconds(Int32.Parse(GetText(entrynode["MinDelay"], "05:00")));
								homeRange = Int32.Parse(GetText(entrynode["WalkRange"], "10"));
								spawnRange = Int32.Parse(GetText(entrynode["SpawnRange"], "4"));
							}
							else
							{
								// just check for consistency with other entries and report discrepancies
								if (@group != Boolean.Parse(GetText(entrynode["GroupSpawn"], "False")))
								{
									diff = true;
									// log it
									try
									{
										using (var op = new StreamWriter("badimport.log", true))
											op.WriteLine("MSFimport : individual group entry difference: {0} vs {1}",
												GetText(entrynode["GroupSpawn"], "False"), @group);
									}
									catch { }
								}

								if (minDelay !=
								    TimeSpan.FromSeconds(Int32.Parse(GetText(entrynode["MinDelay"], "05:00"))))
								{
									diff = true;
									// log it
									try
									{
										using (var op = new StreamWriter("badimport.log", true))
											op.WriteLine("MSFimport : individual mindelay entry difference: {0} vs {1}",
												GetText(entrynode["MinDelay"], "05:00"), minDelay);
									}
									catch { }
								}

								if (maxDelay !=
								    TimeSpan.FromSeconds(Int32.Parse(GetText(entrynode["MaxDelay"], "10:00"))))
								{
									diff = true;
									// log it
									try
									{
										using (var op = new StreamWriter("badimport.log", true))
											op.WriteLine("MSFimport : individual maxdelay entry difference: {0} vs {1}",
												GetText(entrynode["MaxDelay"], "10:00"), maxDelay);
									}
									catch { }
								}

								if (homeRange != Int32.Parse(GetText(entrynode["WalkRange"], "10")))
								{
									diff = true;
									// log it
									try
									{
										using (var op = new StreamWriter("badimport.log", true))
											op.WriteLine(
												"MSFimport : individual homerange entry difference: {0} vs {1}",
												GetText(entrynode["WalkRange"], "10"), homeRange);
									}
									catch { }
								}

								if (spawnRange != Int32.Parse(GetText(entrynode["SpawnRange"], "4")))
								{
									diff = true;
									// log it
									try
									{
										using (var op = new StreamWriter("badimport.log", true))
											op.WriteLine(
												"MSFimport : individual spawnrange entry difference: {0} vs {1}",
												GetText(entrynode["SpawnRange"], "4"), spawnRange);
									}
									catch { }
								}
							}

							// these apply to individual entries
							var amount = Int32.Parse(GetText(entrynode["Amount"], "1"));
							var entryname = GetText(entrynode["EntryType"], "");

							// keep track of the maxcount for the spawner by adding the individual amounts
							maxcount += amount;

							// add the creature entry
							so[entrycount] = new SpawnObject(entryname, amount);

							entrycount++;
							if (entrycount > nentries)
							{
								// log it
								try
								{
									using (var op = new StreamWriter("badimport.log", true))
									{
										op.WriteLine("{0} MSFImport Error; inconsistent entry count {1} {2}",
											DateTime.Now, location, map);
										op.WriteLine();
									}
								}
								catch { }

								@from.SendMessage("Inconsistent entry count detected at {0} {1}.", location, map);
								break;
							}
						}

					if (diff)
					{
						from.SendMessage("Individual entry setting detected at {0} {1}.", location, map);
						// log it
						try
						{
							using (var op = new StreamWriter("badimport.log", true))
							{
								op.WriteLine(
									"{0} MSFImport: Individual entry setting differences listed above from spawner at {1} {2}",
									DateTime.Now, location, map);
								op.WriteLine();
							}
						}
						catch { }
					}
				}
			}

			// assign it a unique id
			var SpawnId = Guid.NewGuid();
			// Create the new xml spawner
			var spawner = new XmlSpawner(SpawnId, location.X, location.Y, 0, 0, name, maxcount,
				minDelay, maxDelay, TimeSpan.FromMinutes(0), -1, defaultTriggerSound, 1,
				team, homeRange, false, so, TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(0),
				TimeSpan.FromMinutes(0), null, null, null, null, null,
				null, null, null, null, 1, null, group, defTODMode, defKillReset, false, -1, null, false, false, false,
				null, defDespawnTime, null, false, null);

			spawner.SpawnRange = spawnRange;
			spawner.m_PlayerCreated = true;
			string fromname = null;
			if (from != null) fromname = from.Name;
			spawner.LastModifiedBy = fromname;
			spawner.FirstModifiedBy = fromname;

			// Try to find a valid Z height if required (Z == -999)

			if (location.Z == -999)
			{
				var NewZ = map.GetAverageZ(location.X, location.Y);

				if (map.CanFit(location.X, location.Y, NewZ, SpawnFitSize) == false)
					for (var x = 1; x <= 39; x++)
						if (map.CanFit(location.X, location.Y, NewZ + x, SpawnFitSize))
						{
							NewZ += x;
							break;
						}

				location.Z = NewZ;
			}

			spawner.MoveToWorld(location, map);

			if (!IsValidMapLocation(location, spawner.Map))
			{
				spawner.Delete();
				throw new Exception("Invalid spawner location.");
			}
		}


		public static void XmlLoadFromFile(string filename, string SpawnerPrefix, Mobile from, Point3D fromloc,
			Map frommap, bool loadrelative, int maxrange, bool loadnew, out int processedmaps,
			out int processedspawners)
		{
			processedmaps = 0;
			processedspawners = 0;
			var total_processed_maps = 0;
			var total_processed_spawners = 0;

			if (filename == null || filename.Length <= 0) return;


			// Check if the file exists
			if (File.Exists(filename) == true)
			{
				FileStream fs = null;
				try
				{
					fs = File.Open(filename, FileMode.Open, FileAccess.Read);
				}
				catch { }

				if (fs == null)
				{
					if (from != null)
						from.SendMessage("Unable to open {0} for loading", filename);
					return;
				}

				// load the file
				XmlLoadFromStream(fs, filename, SpawnerPrefix, from, fromloc, frommap, loadrelative, maxrange, loadnew,
					out processedmaps, out processedspawners);
			}
			else
				// check to see if it is a directory
			if (Directory.Exists(filename) == true)
			{
				// if so then load all of the .xml files in the directory
				string[] files = null;
				try
				{
					files = Directory.GetFiles(filename, "*.xml");
				}
				catch { }

				if (files != null && files.Length > 0)
				{
					if (from != null)
						from.SendMessage("Loading {0} .xml files from directory {1}", files.Length, filename);
					foreach (var file in files)
					{
						XmlLoadFromFile(file, SpawnerPrefix, from, fromloc, frommap, loadrelative, maxrange, loadnew,
							out processedmaps, out processedspawners);
						total_processed_maps += processedmaps;
						total_processed_spawners += processedspawners;
					}
				}

				// recursively search subdirectories for more .xml files
				string[] dirs = null;
				try
				{
					dirs = Directory.GetDirectories(filename);
				}
				catch { }

				if (dirs != null && dirs.Length > 0)
					foreach (var dir in dirs)
					{
						XmlLoadFromFile(dir, SpawnerPrefix, @from, fromloc, frommap, loadrelative, maxrange, loadnew,
							out processedmaps, out processedspawners);
						total_processed_maps += processedmaps;
						total_processed_spawners += processedspawners;
					}

				if (from != null)
					from.SendMessage("Loaded a total of {0} .xml files and {2} spawners from directory {1}",
						total_processed_maps, filename, total_processed_spawners);
				processedmaps = total_processed_maps;
				processedspawners = total_processed_spawners;
			}
			else
			{
				if (from != null)
					from.SendMessage("{0} does not exist", filename);
			}
		}

		public static void XmlLoadFromFile(string filename, string SpawnerPrefix, Mobile from, bool loadrelative,
			int maxrange, bool loadnew, out int processedmaps, out int processedspawners)
		{
			processedmaps = 0;
			processedspawners = 0;

			if (from == null) return;

			XmlLoadFromFile(filename, SpawnerPrefix, from, from.Location, from.Map, loadrelative, maxrange, loadnew,
				out processedmaps, out processedspawners);
		}

		public static void XmlLoadFromFile(string filename, string SpawnerPrefix, Point3D fromloc, Map frommap,
			bool loadrelative, int maxrange, bool loadnew, out int processedmaps, out int processedspawners)
		{
			processedmaps = 0;
			processedspawners = 0;

			XmlLoadFromFile(filename, SpawnerPrefix, null, fromloc, frommap, loadrelative, maxrange, loadnew,
				out processedmaps, out processedspawners);
		}

		public static void XmlLoadFromFile(string filename, string SpawnerPrefix, bool loadnew, out int processedmaps,
			out int processedspawners)
		{
			processedmaps = 0;
			processedspawners = 0;

			XmlLoadFromFile(filename, SpawnerPrefix, null, Point3D.Zero, Map.Internal, false, 0, loadnew,
				out processedmaps, out processedspawners);
		}


		public static void XmlLoadFromStream(Stream fs, string filename, string SpawnerPrefix, Mobile from,
			Point3D fromloc, Map frommap, bool loadrelative, int maxrange, bool loadnew, out int processedmaps,
			out int processedspawners)
		{
			XmlLoadFromStream(fs, filename, SpawnerPrefix, from, fromloc, frommap, loadrelative, maxrange, loadnew,
				out processedmaps, out processedspawners, true);
		}

		public static void XmlLoadFromStream(Stream fs, string filename, string SpawnerPrefix, Mobile from,
			Point3D fromloc, Map frommap, bool loadrelative, int maxrange, bool loadnew, out int processedmaps,
			out int processedspawners, bool verbose)
		{
			processedmaps = 0;
			processedspawners = 0;

			if (fs == null) return;

			// assign an id that will be used to distinguish the newly loaded spawners by appending it to their name
			var newloadid = Guid.NewGuid();


			var TotalCount = 0;
			var TrammelCount = 0;
			var FeluccaCount = 0;
			var IlshenarCount = 0;
			var MalasCount = 0;
			var TokunoCount = 0;
			var OtherCount = 0;
			var questionable_spawner = false;
			var bad_spawner = false;
			var badcount = 0;
			var questionablecount = 0;

			var failedobjectitemcount = 0;
			var failedsetitemcount = 0;
			var relativex = -1;
			var relativey = -1;
			var relativez = 0;
			Map relativemap = null;

			if (from != null)
				from.SendMessage(String.Format("Loading {0} objects{1} from file {2}.", "XmlSpawner",
					SpawnerPrefix != null && SpawnerPrefix.Length > 0
						? " beginning with " + SpawnerPrefix
						: String.Empty, filename));

			// Create the data set
			var ds = new DataSet(SpawnDataSetName);

			// Read in the file
			var fileerror = false;
			try
			{
				ds.ReadXml(fs);
			}
			catch
			{
				if (from != null)
					from.SendMessage(33, "Error reading xml file {0}", filename);
				fileerror = true;
			}

			// close the file
			fs.Close();
			if (fileerror) return;

			// Check that at least a single table was loaded
			if (ds.Tables != null && ds.Tables.Count > 0)
			{
				// Add each spawn point to the current map
				if (ds.Tables[SpawnTablePointName] != null && ds.Tables[SpawnTablePointName].Rows.Count > 0)
					foreach (DataRow dr in ds.Tables[SpawnTablePointName].Rows)
					{
						// load in the spawner info.  Certain fields are required and therefore cannot be ignored
						// the exception handler for those will flag bad_spawner and the result will be logged

						// Each row makes up a single spawner
						var SpawnName = "Spawner";
						try { SpawnName = (string)dr["Name"]; }
						catch { questionable_spawner = true; }

						if (loadnew)
							// append the new id to the name
							SpawnName = String.Format("{0}-{1}", SpawnName, newloadid);

						// Check if there is any spawner name criteria specified on the load
						if (SpawnerPrefix == null || SpawnerPrefix.Length == 0 ||
						    SpawnName.StartsWith(SpawnerPrefix) == true)
						{
							// Try load the GUID (might not work so create a new GUID)
							var SpawnId = Guid.NewGuid();
							if (!loadnew)
								try { SpawnId = new Guid((string)dr["UniqueId"]); }
								catch { }
							else
								// change the dataset guid to the newly created one when new loading
								try
								{
									dr["UniqueId"] = SpawnId;
								}
								catch { Console.WriteLine("unable to set UniqueId"); }

							var SpawnCentreX = fromloc.X;
							var SpawnCentreY = fromloc.Y;
							var SpawnCentreZ = fromloc.Z;

							try { SpawnCentreX = Int32.Parse((string)dr["CentreX"]); }
							catch { bad_spawner = true; }

							try { SpawnCentreY = Int32.Parse((string)dr["CentreY"]); }
							catch { bad_spawner = true; }

							try { SpawnCentreZ = Int32.Parse((string)dr["CentreZ"]); }
							catch { bad_spawner = true; }

							var SpawnX = SpawnCentreX;
							var SpawnY = SpawnCentreY;
							var SpawnWidth = 0;
							var SpawnHeight = 0;
							try { SpawnX = Int32.Parse((string)dr["X"]); }
							catch { questionable_spawner = true; }

							try { SpawnY = Int32.Parse((string)dr["Y"]); }
							catch { questionable_spawner = true; }

							try { SpawnWidth = Int32.Parse((string)dr["Width"]); }
							catch { questionable_spawner = true; }

							try { SpawnHeight = Int32.Parse((string)dr["Height"]); }
							catch { questionable_spawner = true; }

							// Try load the InContainer (default to false)
							var InContainer = false;
							var ContainerX = 0;
							var ContainerY = 0;
							var ContainerZ = 0;
							try { InContainer = Boolean.Parse((string)dr["InContainer"]); }
							catch { }

							if (InContainer)
							{
								try { ContainerX = Int32.Parse((string)dr["ContainerX"]); }
								catch { }

								try { ContainerY = Int32.Parse((string)dr["ContainerY"]); }
								catch { }

								try { ContainerZ = Int32.Parse((string)dr["ContainerZ"]); }
								catch { }
							}

							// Get the map (default to the mobiles map) if the relative distance is too great, then use the defined map

							var SpawnMap = frommap;

							var XmlMapName = frommap.Name;

							//if(!loadrelative && !loadnew)
							{
								// Try to get the "map" field, but in case it doesn't exist, catch and discard the exception
								try { XmlMapName = (string)dr["Map"]; }
								catch { questionable_spawner = true; }

								// Convert the xml map value to a real map object
								if (String.Compare(XmlMapName, Map.Trammel.Name, true) == 0 || XmlMapName == "Trammel")
								{
									SpawnMap = Map.Trammel;
									TrammelCount++;
								}
								else if (String.Compare(XmlMapName, Map.Felucca.Name, true) == 0 ||
								         XmlMapName == "Felucca")
								{
									SpawnMap = Map.Felucca;
									FeluccaCount++;
								}
								else if (String.Compare(XmlMapName, Map.Ilshenar.Name, true) == 0 ||
								         XmlMapName == "Ilshenar")
								{
									SpawnMap = Map.Ilshenar;
									IlshenarCount++;
								}
								else if (String.Compare(XmlMapName, Map.Malas.Name, true) == 0 || XmlMapName == "Malas")
								{
									SpawnMap = Map.Malas;
									MalasCount++;
								}
								else if (String.Compare(XmlMapName, Map.Tokuno.Name, true) == 0 ||
								         XmlMapName == "Tokuno")
								{
									SpawnMap = Map.Tokuno;
									TokunoCount++;
								}
								else
								{
									try
									{
										SpawnMap = Map.Parse(XmlMapName);
									}
									catch { }

									OtherCount++;
								}
							}

							// test to see whether the distance between the relative center point and the spawner is too great.  If so then dont do relative
							if (relativex == -1 && relativey == -1)
							{
								// the first xml entry in the file will determine the origin
								relativex = SpawnCentreX;
								relativey = SpawnCentreY;
								relativez = SpawnCentreZ;

								// and also the relative map to relocate from
								relativemap = SpawnMap;
							}

							var SpawnRelZ = 0;
							var OrigZ = SpawnCentreZ;
							if (loadrelative && Math.Abs(relativex - SpawnCentreX) <= maxrange &&
							    Math.Abs(relativey - SpawnCentreY) <= maxrange
							    && SpawnMap == relativemap)
							{
								// its within range so shift it
								SpawnCentreX -= relativex - fromloc.X;
								SpawnCentreY -= relativey - fromloc.Y;
								SpawnX -= relativex - fromloc.X;
								SpawnY -= relativey - fromloc.Y;
								// force it to autosearch for Z when it places it but hold onto relative Z info just in case it can be placed there
								SpawnRelZ = relativez - fromloc.Z;
								SpawnCentreZ = Int16.MinValue;
							}

							// if relative loading has been specified, see if the loaded map is the same as the relativemap and relocate.
							// if it doesnt match then just leave it
							if (loadrelative && relativemap == SpawnMap) SpawnMap = frommap;


							if (SpawnMap == Map.Internal) bad_spawner = true;

							// Try load the IsRelativeHomeRange (default to true)
							var SpawnIsRelativeHomeRange = true;
							try { SpawnIsRelativeHomeRange = Boolean.Parse((string)dr["IsHomeRangeRelative"]); }
							catch { }


							var SpawnHomeRange = 5;
							try { SpawnHomeRange = Int32.Parse((string)dr["Range"]); }
							catch { questionable_spawner = true; }

							var SpawnMaxCount = 1;
							try { SpawnMaxCount = Int32.Parse((string)dr["MaxCount"]); }
							catch { questionable_spawner = true; }

							//deal with double format for delay.  default is the old minute format
							var delay_in_sec = false;
							try { delay_in_sec = Boolean.Parse((string)dr["DelayInSec"]); }
							catch { }

							var SpawnMinDelay = TimeSpan.FromMinutes(5);
							var SpawnMaxDelay = TimeSpan.FromMinutes(10);


							if (delay_in_sec)
							{
								try { SpawnMinDelay = TimeSpan.FromSeconds(Int32.Parse((string)dr["MinDelay"])); }
								catch { }

								try { SpawnMaxDelay = TimeSpan.FromSeconds(Int32.Parse((string)dr["MaxDelay"])); }
								catch { }
							}
							else
							{
								try { SpawnMinDelay = TimeSpan.FromMinutes(Int32.Parse((string)dr["MinDelay"])); }
								catch { }

								try { SpawnMaxDelay = TimeSpan.FromMinutes(Int32.Parse((string)dr["MaxDelay"])); }
								catch { }
							}

							var SpawnMinRefractory = TimeSpan.FromMinutes(0);
							try
							{
								SpawnMinRefractory = TimeSpan.FromMinutes(Double.Parse((string)dr["MinRefractory"]));
							}
							catch { }

							var SpawnMaxRefractory = TimeSpan.FromMinutes(0);
							try
							{
								SpawnMaxRefractory = TimeSpan.FromMinutes(Double.Parse((string)dr["MaxRefractory"]));
							}
							catch { }

							var SpawnTODStart = TimeSpan.FromMinutes(0);
							try { SpawnTODStart = TimeSpan.FromMinutes(Double.Parse((string)dr["TODStart"])); }
							catch { }

							var SpawnTODEnd = TimeSpan.FromMinutes(0);
							try { SpawnTODEnd = TimeSpan.FromMinutes(Double.Parse((string)dr["TODEnd"])); }
							catch { }

							var todmode = (int)TODModeType.Realtime;
							var SpawnTODMode = TODModeType.Realtime;
							try { todmode = Int32.Parse((string)dr["TODMode"]); }
							catch { }

							switch ((int)todmode)
							{
								case (int)TODModeType.Gametime:
									SpawnTODMode = TODModeType.Gametime;
									break;
								case (int)TODModeType.Realtime:
									SpawnTODMode = TODModeType.Realtime;
									break;
							}

							var SpawnKillReset = defKillReset;
							try { SpawnKillReset = Int32.Parse((string)dr["KillReset"]); }
							catch { }

							string SpawnProximityMessage = null;
							// proximity message
							try { SpawnProximityMessage = (string)dr["ProximityTriggerMessage"]; }
							catch { }

							string SpawnItemTriggerName = null;
							try { SpawnItemTriggerName = (string)dr["ItemTriggerName"]; }
							catch { }

							string SpawnNoItemTriggerName = null;
							try { SpawnNoItemTriggerName = (string)dr["NoItemTriggerName"]; }
							catch { }

							string SpawnSpeechTrigger = null;
							try { SpawnSpeechTrigger = (string)dr["SpeechTrigger"]; }
							catch { }

							string SpawnSkillTrigger = null;
							try { SpawnSkillTrigger = (string)dr["SkillTrigger"]; }
							catch { }

							string SpawnMobTriggerName = null;
							try { SpawnMobTriggerName = (string)dr["MobTriggerName"]; }
							catch { }

							string SpawnMobPropertyName = null;
							try { SpawnMobPropertyName = (string)dr["MobPropertyName"]; }
							catch { }

							string SpawnPlayerPropertyName = null;
							try { SpawnPlayerPropertyName = (string)dr["PlayerPropertyName"]; }
							catch { }

							double SpawnTriggerProbability = 1;
							try { SpawnTriggerProbability = Double.Parse((string)dr["TriggerProbability"]); }
							catch { }

							var SpawnSequentialSpawning = -1;
							try { SpawnSequentialSpawning = Int32.Parse((string)dr["SequentialSpawning"]); }
							catch { }

							string SpawnRegionName = null;
							try { SpawnRegionName = (string)dr["RegionName"]; }
							catch { }

							string SpawnConfigFile = null;
							try { SpawnConfigFile = (string)dr["ConfigFile"]; }
							catch { }

							var SpawnAllowGhost = false;
							try { SpawnAllowGhost = Boolean.Parse((string)dr["AllowGhostTriggering"]); }
							catch { }

							var SpawnAllowNPC = false;
							try { SpawnAllowNPC = Boolean.Parse((string)dr["AllowNPCTriggering"]); }
							catch { }

							var SpawnSpawnOnTrigger = false;
							try { SpawnSpawnOnTrigger = Boolean.Parse((string)dr["SpawnOnTrigger"]); }
							catch { }

							var SpawnSmartSpawning = false;
							try { SpawnSmartSpawning = Boolean.Parse((string)dr["SmartSpawning"]); }
							catch { }

							var TickReset = false;
							try { TickReset = Boolean.Parse((string)dr["TickReset"]); }
							catch { }

							string SpawnObjectPropertyName = null;
							try { SpawnObjectPropertyName = (string)dr["ObjectPropertyName"]; }
							catch { }

							// read in the object proximity target, this will be an object name, so have to do a search
							// to find the item in the world.  Also have to test for redundancy
							string triggerObjectName = null;
							try { triggerObjectName = (string)dr["ObjectPropertyItemName"]; }
							catch { }

							// read in the target for the set command, this will be an object name, so have to do a search
							// to find the item in the world.  Also have to test for redundancy
							string setObjectName = null;
							try { setObjectName = (string)dr["SetPropertyItemName"]; }
							catch { }

							// we will assign this during the self-reference resolution pass
							Item SpawnSetPropertyItem = null;

							// we will assign this during the self-reference resolution pass
							Item SpawnObjectPropertyItem = null;

							// read the duration parameter from the xml file
							// but older files wont have it so deal with that condition and set it to the default of "0", i.e. infinite duration
							// Try to get the "Duration" field, but in case it doesn't exist, catch and discard the exception
							var SpawnDuration = TimeSpan.FromMinutes(0);
							try { SpawnDuration = TimeSpan.FromMinutes(Double.Parse((string)dr["Duration"])); }
							catch { }

							var SpawnDespawnTime = TimeSpan.FromHours(0);
							try { SpawnDespawnTime = TimeSpan.FromHours(Double.Parse((string)dr["DespawnTime"])); }
							catch { }

							var SpawnProximityRange = -1;
							// Try to get the "ProximityRange" field, but in case it doesn't exist, catch and discard the exception
							try { SpawnProximityRange = Int32.Parse((string)dr["ProximityRange"]); }
							catch { }

							var SpawnProximityTriggerSound = 0;
							// Try to get the "ProximityTriggerSound" field, but in case it doesn't exist, catch and discard the exception
							try { SpawnProximityTriggerSound = Int32.Parse((string)dr["ProximityTriggerSound"]); }
							catch { }

							var SpawnAmount = 1;
							try { SpawnAmount = Int32.Parse((string)dr["Amount"]); }
							catch { }

							var SpawnExternalTriggering = false;
							try { SpawnExternalTriggering = Boolean.Parse((string)dr["ExternalTriggering"]); }
							catch { }

							string waypointstr = null;
							try { waypointstr = (string)dr["Waypoint"]; }
							catch { }

							var SpawnWaypoint = GetWaypoint(waypointstr);

							var SpawnTeam = 0;
							try { SpawnTeam = Int32.Parse((string)dr["Team"]); }
							catch { questionable_spawner = true; }

							var SpawnIsGroup = false;
							try { SpawnIsGroup = Boolean.Parse((string)dr["IsGroup"]); }
							catch { questionable_spawner = true; }

							var SpawnIsRunning = false;
							try { SpawnIsRunning = Boolean.Parse((string)dr["IsRunning"]); }
							catch { questionable_spawner = true; }

							// try loading the new spawn specifications first
							var Spawns = new SpawnObject[0];
							var havenew = true;
							try { Spawns = SpawnObject.LoadSpawnObjectsFromString2((string)dr["Objects2"]); }
							catch { havenew = false; }

							if (!havenew)
								// try loading the new spawn specifications
								try { Spawns = SpawnObject.LoadSpawnObjectsFromString((string)dr["Objects"]); }
								catch { questionable_spawner = true; }
							// can only have one of these defined

							// do a check on the location of the spawner
							if (!IsValidMapLocation(SpawnCentreX, SpawnCentreY, SpawnMap))
							{
								if (from != null)
									from.SendMessage(33, "Invalid location '{0}' at [{1} {2}] in {3}",
										SpawnName, SpawnCentreX, SpawnCentreY, XmlMapName);
								bad_spawner = true;
							}

							// Check if this spawner already exists
							XmlSpawner OldSpawner = null;
							var found_container = false;
							var found_spawner = false;
							Container spawn_container = null;
							if (!bad_spawner)
								foreach (var i in World.Items.Values)
								{
									if (i is XmlSpawner)
									{
										var CheckXmlSpawner = (XmlSpawner)i;

										// Check if the spawners GUID is the same as the one being loaded
										// and that the spawners map is the same as the one being loaded
										if (CheckXmlSpawner.UniqueId == SpawnId.ToString()
										    /* && ( CheckXmlSpawner.Map == SpawnMap || loadrelative)*/)
										{
											OldSpawner = (XmlSpawner)i;
											found_spawner = true;
										}
									}

									//look for containers with the spawn coordinates if the incontainer flag is set
									if (InContainer && !found_container && i is Container &&
									    SpawnCentreX == i.Location.X && SpawnCentreY == i.Location.Y &&
									    (SpawnCentreZ == i.Location.Z || SpawnCentreZ == Int16.MinValue))
									{
										// assume this is the container that the spawner was in
										found_container = true;
										spawn_container = i as Container;
									}

									// ok we can break if we have handled both the spawner and any containers
									if (found_spawner && (found_container || !InContainer))
										break;
								}

							// test to see whether the spawner specification was valid, bad, or questionable
							if (bad_spawner)
							{
								badcount++;
								if (from != null)
									from.SendMessage(33, "Invalid spawner");
								// log it
								long fileposition = -1;
								try { fileposition = fs.Position; }
								catch { }

								try
								{
									using (var op = new StreamWriter("badxml.log", true))
									{
										op.WriteLine("# Invalid spawner : {0}: Fileposition {1} {2}", DateTime.Now,
											fileposition, filename);
										op.WriteLine();
									}
								}
								catch { }
							}
							else if (questionable_spawner)
							{
								questionablecount++;
								if (from != null)
									from.SendMessage(33, "Questionable spawner '{0}' at [{1} {2}] in {3}",
										SpawnName, SpawnCentreX, SpawnCentreY, XmlMapName);
								// log it
								long fileposition = -1;
								try { fileposition = fs.Position; }
								catch { }

								try
								{
									using (var op = new StreamWriter("badxml.log", true))
									{
										op.WriteLine(
											"# Questionable spawner : {0}: Format: X Y Z Map SpawnerName Fileposition Xmlfile",
											DateTime.Now);
										op.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", SpawnCentreX, SpawnCentreY,
											SpawnCentreZ, XmlMapName, SpawnName, fileposition, filename);
										op.WriteLine();
									}
								}
								catch { }
							}

							if (!bad_spawner)
							{
								// Delete the old spawner if it exists
								if (OldSpawner != null)
									OldSpawner.Delete();

								// Create the new spawner
								var TheSpawn = new XmlSpawner(SpawnId, SpawnX, SpawnY, SpawnWidth, SpawnHeight,
									SpawnName, SpawnMaxCount,
									SpawnMinDelay, SpawnMaxDelay, SpawnDuration, SpawnProximityRange,
									SpawnProximityTriggerSound, SpawnAmount,
									SpawnTeam, SpawnHomeRange, SpawnIsRelativeHomeRange, Spawns, SpawnMinRefractory,
									SpawnMaxRefractory, SpawnTODStart,
									SpawnTODEnd, SpawnObjectPropertyItem, SpawnObjectPropertyName,
									SpawnProximityMessage, SpawnItemTriggerName, SpawnNoItemTriggerName,
									SpawnSpeechTrigger, SpawnMobTriggerName, SpawnMobPropertyName,
									SpawnPlayerPropertyName, SpawnTriggerProbability,
									SpawnSetPropertyItem, SpawnIsGroup, SpawnTODMode, SpawnKillReset,
									SpawnExternalTriggering, SpawnSequentialSpawning,
									SpawnRegionName, SpawnAllowGhost, SpawnAllowNPC, SpawnSpawnOnTrigger,
									SpawnConfigFile, SpawnDespawnTime, SpawnSkillTrigger, SpawnSmartSpawning,
									SpawnWaypoint);
								TheSpawn.m_DisableGlobalAutoReset = TickReset;
								//TheSpawn.Group = SpawnIsGroup;\

								string fromname = null;
								if (from != null) fromname = from.Name;
								TheSpawn.LastModifiedBy = fromname;
								TheSpawn.FirstModifiedBy = fromname;

								// Try to find a valid Z height if required (SpawnCentreZ = short.MinValue)
								var NewZ = 0;


								// Check if relative loading is set.  If so then try loading at the z-offset position first with no surface requirement, then try auto
								/*if(loadrelative && SpawnMap.CanFit( SpawnCentreX, SpawnCentreY, OrigZ - SpawnRelZ, SpawnFitSize,true, false,false )) */

								if (loadrelative && HasTileSurface(SpawnMap, SpawnCentreX, SpawnCentreY,
									    OrigZ - SpawnRelZ))
									NewZ = OrigZ - SpawnRelZ;
								else if (SpawnCentreZ == Int16.MinValue)
								{
									NewZ = SpawnMap.GetAverageZ(SpawnCentreX, SpawnCentreY);


									if (SpawnMap.CanFit(SpawnCentreX, SpawnCentreY, NewZ, SpawnFitSize) == false)
										for (var x = 1; x <= 39; x++)
											if (SpawnMap.CanFit(SpawnCentreX, SpawnCentreY, NewZ + x, SpawnFitSize))
											{
												NewZ += x;
												break;
											}
								}
								else
									// This spawn point already has a defined Z location, so use it
									NewZ = SpawnCentreZ;

								// if this is a container held spawner, drop it in the container
								if (found_container && spawn_container != null && !spawn_container.Deleted)
								{
									TheSpawn.Location = new Point3D(ContainerX, ContainerY, ContainerZ);
									spawn_container.AddItem(TheSpawn);
								}
								else
								{
									// disable the X_Y adjustments in OnLocationChange
									IgnoreLocationChange = true;
									TheSpawn.MoveToWorld(new Point3D(SpawnCentreX, SpawnCentreY, NewZ), SpawnMap);
								}

								// reset the spawner
								TheSpawn.Reset();
								TheSpawn.Running = SpawnIsRunning;

								// update subgroup-specific next spawn times
								TheSpawn.NextSpawn = TimeSpan.Zero;
								TheSpawn.ResetNextSpawnTimes();


								// Send a message to the client that the spawner is created
								if (from != null && verbose)
									from.SendMessage(188, "Created '{0}' in {1} at {2}", TheSpawn.Name,
										TheSpawn.Map.Name, TheSpawn.Location.ToString());

								// Do a total respawn
								//TheSpawn.Respawn();

								// Increment the count
								TotalCount++;
							}

							bad_spawner = false;
							questionable_spawner = false;
						}
					}

				if (from != null)
					from.SendMessage("Resolving spawner self references");

				if (ds.Tables[SpawnTablePointName] != null && ds.Tables[SpawnTablePointName].Rows.Count > 0)
					foreach (DataRow dr in ds.Tables[SpawnTablePointName].Rows)
					{
						// Try load the GUID
						var badid = false;
						var SpawnId = Guid.NewGuid();
						try { SpawnId = new Guid((string)dr["UniqueId"]); }
						catch { badid = true; }

						if (badid) continue;
						// Get the map
						var SpawnMap = frommap;
						var XmlMapName = frommap.Name;

						if (!loadrelative)
						{
							try { XmlMapName = (string)dr["Map"]; }
							catch { }

							// Convert the xml map value to a real map object
							try
							{
								SpawnMap = Map.Parse(XmlMapName);
							}
							catch { }
						}

						var found_spawner = false;
						XmlSpawner OldSpawner = null;
						foreach (var i in World.Items.Values)
						{
							if (i is XmlSpawner)
							{
								var CheckXmlSpawner = (XmlSpawner)i;

								// Check if the spawners GUID is the same as the one being loaded
								// and that the spawners map is the same as the one being loaded
								if (CheckXmlSpawner.UniqueId == SpawnId.ToString()
								    /* && ( CheckXmlSpawner.Map == SpawnMap || loadrelative) */)
								{
									OldSpawner = (XmlSpawner)i;
									found_spawner = true;
								}
							}

							if (found_spawner)
								break;
						}

						if (found_spawner && OldSpawner != null && !OldSpawner.Deleted)
						{
							// resolve item name references since they may have referred to spawners that were just created
							string setObjectName = null;
							try { setObjectName = (string)dr["SetPropertyItemName"]; }
							catch { }

							if (setObjectName != null && setObjectName.Length > 0)
							{
								// try to parse out the type information if it has also been saved
								var typeargs = setObjectName.Split(",".ToCharArray(), 2);
								string typestr = null;
								var namestr = setObjectName;

								if (typeargs.Length > 1)
								{
									namestr = typeargs[0];
									typestr = typeargs[1];
								}

								// if this is a new load then assume that it will be referring to another newly loaded object so append the newloadid
								if (loadnew)
								{
									var tmpsetObjectName = String.Format("{0}-{1}", namestr, newloadid);
									OldSpawner.m_SetPropertyItem =
										BaseXmlSpawner.FindItemByName(null, tmpsetObjectName, typestr);
								}

								// if this fails then try the original
								if (OldSpawner.m_SetPropertyItem == null)
									OldSpawner.m_SetPropertyItem =
										BaseXmlSpawner.FindItemByName(null, namestr, typestr);
								if (OldSpawner.m_SetPropertyItem == null)
								{
									failedsetitemcount++;
									if (from != null)
										from.SendMessage(33,
											"Failed to initialize SetItemProperty Object '{0}' on ' '{1}' at [{2} {3}] in {4}",
											setObjectName, OldSpawner.Name, OldSpawner.Location.X,
											OldSpawner.Location.Y, OldSpawner.Map);
									// log it
									try
									{
										using (var op = new StreamWriter("badxml.log", true))
										{
											op.WriteLine(
												"# Failed SetItemProperty Object initialization : {0}: Format: ObjectName X Y Z Map SpawnerName Xmlfile",
												DateTime.Now);
											op.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}",
												setObjectName, OldSpawner.Location.X, OldSpawner.Location.Y,
												OldSpawner.Location.Z, OldSpawner.Map, OldSpawner.Name, filename);
											op.WriteLine();
										}
									}
									catch { }
								}
							}

							string triggerObjectName = null;
							try { triggerObjectName = (string)dr["ObjectPropertyItemName"]; }
							catch { }

							if (triggerObjectName != null && triggerObjectName.Length > 0)
							{
								var typeargs = triggerObjectName.Split(",".ToCharArray(), 2);
								string typestr = null;
								var namestr = triggerObjectName;

								if (typeargs.Length > 1)
								{
									namestr = typeargs[0];
									typestr = typeargs[1];
								}

								// if this is a new load then assume that it will be referring to another newly loaded object so append the newloadid
								if (loadnew)
								{
									var tmptriggerObjectName = String.Format("{0}-{1}", namestr, newloadid);
									OldSpawner.m_ObjectPropertyItem =
										BaseXmlSpawner.FindItemByName(null, tmptriggerObjectName, typestr);
								}

								// if this fails then try the original
								if (OldSpawner.m_ObjectPropertyItem == null)
									OldSpawner.m_ObjectPropertyItem =
										BaseXmlSpawner.FindItemByName(null, namestr, typestr);
								if (OldSpawner.m_ObjectPropertyItem == null)
								{
									failedobjectitemcount++;
									if (from != null)
										from.SendMessage(33,
											"Failed to initialize TriggerObject '{0}' on ' '{1}' at [{2} {3}] in {4}",
											triggerObjectName, OldSpawner.Name, OldSpawner.Location.X,
											OldSpawner.Location.Y, OldSpawner.Map);
									// log it
									try
									{
										using (var op = new StreamWriter("badxml.log", true))
										{
											op.WriteLine(
												"# Failed TriggerObject initialization : {0}: Format: ObjectName X Y Z Map SpawnerName Xmlfile",
												DateTime.Now);
											op.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}",
												triggerObjectName, OldSpawner.Location.X, OldSpawner.Location.Y,
												OldSpawner.Location.Z, OldSpawner.Map, OldSpawner.Name, filename);
											op.WriteLine();
										}
									}
									catch { }
								}
							}
						}
					}
			}

			// close the file
			try
			{
				fs.Close();
			}
			catch { }

			if (from != null)
				from.SendMessage(
					"{0} spawner(s) were created from file {1} [Trammel={2}, Felucca={3}, Ilshenar={4}, Malas={5}, Tokuno={6} Other={7}].",
					TotalCount, filename, TrammelCount, FeluccaCount, IlshenarCount, MalasCount, TokunoCount,
					OtherCount);
			if (failedobjectitemcount > 0)
				if (@from != null)
					@from.SendMessage(33, "Failed to initialize TriggerObjects in {0} spawners. Saved to 'badxml.log'",
						failedobjectitemcount);
			if (failedsetitemcount > 0)
				if (@from != null)
					@from.SendMessage(33,
						"Failed to initialize SetItemProperty Objects in {0} spawners. Saved to 'badxml.log'",
						failedsetitemcount);
			if (badcount > 0)
				if (@from != null)
					@from.SendMessage(33, "{0} bad spawners detected. Saved to 'badxml.log'", badcount);
			if (questionablecount > 0)
				if (@from != null)
					@from.SendMessage(33, "{0} questionable spawners detected. Saved to 'badxml.log'",
						questionablecount);
			processedmaps = 1;
			processedspawners = TotalCount;
		}

		public static string LocateFile(string filename)
		{
			var found = false;

			string dirname = null;

			if (Directory.Exists(XmlSpawnDir) == true)
			{
				// get it from the defaults directory if it exists
				dirname = String.Format("{0}/{1}", XmlSpawnDir, filename);
				found = File.Exists(dirname) || Directory.Exists(dirname);
			}

			if (!found)
				// otherwise just get it from the main installation dir
				dirname = filename;

			return dirname;
		}

		public static string LocateMultiFile(string filename)
		{
			var found = false;

			string dirname = null;

			if (Directory.Exists(XmlMultiDir) == true)
			{
				// get it from the defaults directory if it exists
				dirname = String.Format("{0}/{1}", XmlMultiDir, filename);
				found = File.Exists(dirname) || Directory.Exists(dirname);
			}

			if (!found)
				// otherwise just get it from the main installation dir
				dirname = filename;

			return dirname;
		}

		[Usage("XmlNewLoad <SpawnFile or directory> [SpawnerPrefixFilter]")]
		[Description(
			"Loads new XmlSpawner objects with new GUIDs (no replacement) into the current map of the player.")]
		public static void NewLoad_OnCommand(CommandEventArgs e)
		{
			if (e.Mobile.AccessLevel >= DiskAccessLevel)
			{
				if (e.Arguments.Length >= 1)
				{
					var filename = LocateFile(e.Arguments[0].ToString());

					// Spawner load criteria (if any)
					var SpawnerPrefix = String.Empty;

					// Check if there is an argument provided (load criteria)
					if (e.Arguments.Length > 1)
						SpawnerPrefix = e.Arguments[1];
					int processedmaps;
					int processedspawners;

					XmlLoadFromFile(filename, SpawnerPrefix, e.Mobile, false, 0, true, out processedmaps,
						out processedspawners);
				}
				else
					e.Mobile.SendMessage("Usage:  {0} <SpawnFile or directory> [SpawnerPrefixFilter]", e.Command);
			}
			else
				e.Mobile.SendMessage("You do not have rights to perform this command.");
		}

		[Usage("XmlLoad <SpawnFile or directory> [SpawnerPrefixFilter]")]
		[Description(
			"Loads XmlSpawner objects (replacing existing spawners with matching GUIDs) into the proper map as defined in the file supplied.")]
		public static void Load_OnCommand(CommandEventArgs e)
		{
			if (e.Mobile.AccessLevel >= DiskAccessLevel)
			{
				if (e.Arguments.Length >= 1)
				{
					var filename = LocateFile(e.Arguments[0].ToString());

					// Spawner load criteria (if any)
					var SpawnerPrefix = String.Empty;

					// Check if there is an argument provided (load criteria)
					if (e.Arguments.Length > 1)
						SpawnerPrefix = e.Arguments[1];
					int processedmaps;
					int processedspawners;

					XmlLoadFromFile(filename, SpawnerPrefix, e.Mobile, false, 0, false, out processedmaps,
						out processedspawners);
				}
				else
					e.Mobile.SendMessage("Usage:  {0} <SpawnFile or directory> [SpawnerPrefixFilter]", e.Command);
			}
			else
				e.Mobile.SendMessage("You do not have rights to perform this command.");
		}

		[Usage("XmlNewLoadHere <SpawnFile or directory> [SpawnerPrefixFilter][-maxrange range]")]
		[Description(
			"Loads new XmlSpawner objects with new GUIDs (no replacement) to the current map and location of the player. Spawners beyond maxrange (default=48 tiles) are not moved relative to the player")]
		public static void NewLoadHere_OnCommand(CommandEventArgs e)
		{
			if (e.Mobile.AccessLevel >= DiskAccessLevel)
			{
				if (e.Arguments.Length >= 1)
				{
					var filename = LocateFile(e.Arguments[0].ToString());

					// Spawner load criteria (if any)
					var SpawnerPrefix = String.Empty;
					var badargs = false;
					var maxrange = 48;

					// Check if there is an argument provided (load criteria)
					try
					{
						// Check if there is an argument provided (load criteria)
						for (var nxtarg = 1; nxtarg < e.Arguments.Length; nxtarg++)
							// is it a maxrange option?
							if (e.Arguments[nxtarg].ToLower() == "-maxrange")
								maxrange = Int32.Parse(e.Arguments[++nxtarg]);
							else
								SpawnerPrefix = e.Arguments[nxtarg];
					}
					catch
					{
						e.Mobile.SendMessage(
							"Usage:  {0} <SpawnFile or directory> [SpawnerPrefixFilter][-maxrange range]", e.Command);
						badargs = true;
					}

					if (!badargs)
					{
						int processedmaps;
						int processedspawners;

						XmlLoadFromFile(filename, SpawnerPrefix, e.Mobile, true, maxrange, true, out processedmaps,
							out processedspawners);
					}
				}
				else
					e.Mobile.SendMessage("Usage:  {0} <SpawnFile or directory> [SpawnerPrefixFilter][-maxrange range]",
						e.Command);
			}
			else
				e.Mobile.SendMessage("You do not have rights to perform this command.");
		}

		[Usage("XmlLoadHere <SpawnFile or directory> [SpawnerPrefixFilter][-maxrange range]")]
		[Description(
			"Loads XmlSpawner objects to the current map and location of the player. Spawners beyond maxrange (default=48 tiles) are not moved relative to the player")]
		public static void LoadHere_OnCommand(CommandEventArgs e)
		{
			if (e.Mobile.AccessLevel >= DiskAccessLevel)
			{
				if (e.Arguments.Length >= 1)
				{
					var filename = LocateFile(e.Arguments[0].ToString());

					// Spawner load criteria (if any)
					var SpawnerPrefix = String.Empty;
					var badargs = false;
					var maxrange = 48;

					try
					{
						// Check if there is an argument provided (load criteria)
						for (var nxtarg = 1; nxtarg < e.Arguments.Length; nxtarg++)
							// is it a maxrange option?
							if (e.Arguments[nxtarg].ToLower() == "-maxrange")
								maxrange = Int32.Parse(e.Arguments[++nxtarg]);
							else
								SpawnerPrefix = e.Arguments[nxtarg];
					}
					catch
					{
						e.Mobile.SendMessage(
							"Usage:  {0} <SpawnFile or directory> [SpawnerPrefixFilter][-maxrange range]", e.Command);
						badargs = true;
					}

					if (!badargs)
					{
						int processedmaps;
						int processedspawners;

						XmlLoadFromFile(filename, SpawnerPrefix, e.Mobile, true, maxrange, false, out processedmaps,
							out processedspawners);
					}
				}
				else
					e.Mobile.SendMessage("Usage:  {0} <SpawnFile or directory> [SpawnerPrefixFilter][-maxrange range]",
						e.Command);
			}
			else
				e.Mobile.SendMessage("You do not have rights to perform this command.");
		}

		[Usage("XmlSaveOld <SpawnFile> [SpawnerPrefixFilter]")]
		[Description(
			"Saves all XmlSpawner objects from the current map into the file supplied in the old xmlspawner format.")]
		public static void SaveOld_OnCommand(CommandEventArgs e)
		{
			SaveSpawns(e, false, true);
		}

		[Usage("XmlSpawnerSave <SpawnFile> [SpawnerPrefixFilter]")]
		[Description("Saves all XmlSpawner objects from the current map into the file supplied.")]
		public static void Save_OnCommand(CommandEventArgs e)
		{
			SaveSpawns(e, false, false);
		}

		[Usage("XmlSpawnerSaveAll <SpawnFile> [SpawnerPrefixFilter]")]
		[Description("Saves ALL XmlSpawner objects from the entire world into the file supplied.")]
		public static void SaveAll_OnCommand(CommandEventArgs e)
		{
			SaveSpawns(e, true, false);
		}

		public class XmlSaveSingle : BaseCommand
		{
			public XmlSaveSingle()
			{
				AccessLevel = DiskAccessLevel;
				Supports = CommandSupport.Single;
				Commands = new string[] { "XmlSaveSingle" };
				ObjectTypes = ObjectTypes.Items;
				Usage = "XmlSaveSingle <filename>";
				Description = "Saves single xmlspawner to specified file.";
			}

			public override void Execute(CommandEventArgs e, object obj)
			{
				if (e == null || e.Mobile == null || e.Arguments == null) return;

				if (e.Arguments.Length < 1)
				{
					e.Mobile.SendMessage("Usage:  {0} <SpawnFile> (without spaces!!)", e.Command);
					return;
				}

				var filename = e.Arguments[0].ToString();

				var xmlspawner = obj as XmlSpawner;

				if (xmlspawner == null)
				{
					e.Mobile.SendMessage("You can select only XmlSpawner objects!");
					return;
				}

				var m = e.Mobile;

				CommandLogging.WriteLine(m, "{0} {1} Saving XmlSpawner {2} on file {3}", m.AccessLevel,
					CommandLogging.Format(m), CommandLogging.Format(xmlspawner), CommandLogging.Format(filename));
				SaveSpawns(m, xmlspawner, filename);
			}
		}

		private static void SaveSpawns(Mobile m, XmlSpawner xmlspawner, string filename)
		{
			if (m.AccessLevel < DiskAccessLevel)
			{
				m.SendMessage("You do not have rights to perform this command.");
				return;
			}

			string dirname;

			if (Directory.Exists(XmlSpawnDir) && filename != null && !filename.StartsWith("/") &&
			    !filename.StartsWith("\\"))
				// put it in the defaults directory if it exists
				dirname = String.Format("{0}/{1}", XmlSpawnDir, filename);
			else
				// otherwise just put it in the main installation dir
				dirname = filename;

			m.SendMessage("Saving object in folder {0} - file {1} - spawner {2}.", dirname, filename, xmlspawner);

			var saveslist = new List<XmlSpawner>(1);
			saveslist.Add(xmlspawner);
			SaveSpawnList(m, saveslist, dirname, false, true);
		}

		private static void SaveSpawns(CommandEventArgs e, bool SaveAllMaps, bool oldformat)
		{
			if (e == null || e.Mobile == null || e.Arguments == null || e.Arguments.Length < 1) return;

			if (e.Mobile.AccessLevel < DiskAccessLevel)
			{
				e.Mobile.SendMessage("You do not have rights to perform this command.");
				return;
			}

			if (e.Arguments != null && e.Arguments.Length < 1)
			{
				e.Mobile.SendMessage("Usage:  {0} <SpawnFile> [SpawnerPrefixFilter]", e.Command);
				return;
			}

			// Spawner save criteria (if any)
			var SpawnerPrefix = String.Empty;

			// Check if there is an argument provided (save criteria)
			if (e.Arguments.Length > 1)
				SpawnerPrefix = e.Arguments[1];

			var filename = e.Arguments[0].ToString();

			string dirname;
			if (Directory.Exists(XmlSpawnDir) && filename != null && !filename.StartsWith("/") &&
			    !filename.StartsWith("\\"))
				// put it in the defaults directory if it exists
				dirname = String.Format("{0}/{1}", XmlSpawnDir, filename);
			else
				// otherwise just put it in the main installation dir
				dirname = filename;

			if (SaveAllMaps == true)
				e.Mobile.SendMessage(String.Format("Saving {0} objects{1} to file {2} from {3}.", "XmlSpawner",
					SpawnerPrefix != null && SpawnerPrefix.Length > 0
						? " beginning with " + SpawnerPrefix
						: String.Empty, dirname, e.Mobile.Map));
			else
				e.Mobile.SendMessage(String.Format("Saving {0} obejcts{1} to file {2} from the entire world.",
					"XmlSpawner",
					SpawnerPrefix != null && SpawnerPrefix.Length > 0
						? " beginning with " + SpawnerPrefix
						: String.Empty, dirname));


			var saveslist = new List<XmlSpawner>();

			// Add each spawn point to the list
			foreach (var i in World.Items.Values)
				if (i is XmlSpawner && !i.Deleted && (SaveAllMaps == true || i.Map == e.Mobile.Map)
				    //check for mob carried spawners and ignore them
				    && !(i.RootParent is Mobile)
				    && (SpawnerPrefix == null || SpawnerPrefix.Length == 0 ||
				        i.Name != null && i.Name.StartsWith(SpawnerPrefix)))
					saveslist.Add((XmlSpawner)i);

			// save the list
			SaveSpawnList(e.Mobile, saveslist, dirname, oldformat, true);
		}

		public static bool SaveSpawnList(List<XmlSpawner> savelist, Stream stream)
		{
			return SaveSpawnList(null, savelist, null, stream, false, false);
		}

		public static bool SaveSpawnList(Mobile from, List<XmlSpawner> savelist, string dirname, bool oldformat,
			bool verbose)
		{
			if (dirname == null || dirname.Length == 0) return false;


			var save_ok = true;
			FileStream fs = null;

			try
			{
				// Create the FileStream to write with.
				fs = new FileStream(dirname, FileMode.Create);
			}
			catch
			{
				if (from != null)
					from.SendMessage("Error creating file {0}", dirname);
				save_ok = false;
			}

			// so far so good
			if (save_ok) save_ok = SaveSpawnList(@from, savelist, dirname, fs, oldformat, verbose);

			if (!save_ok && from != null) @from.SendMessage("Unable to complete save operation.");

			return save_ok;
		}


		public static bool SaveSpawnList(Mobile from, List<XmlSpawner> savelist, string dirname, Stream stream,
			bool oldformat, bool verbose)
		{
			if (savelist == null || stream == null) return false;

			var TotalCount = 0;
			var TrammelCount = 0;
			var FeluccaCount = 0;
			var IlshenarCount = 0;
			var MalasCount = 0;
			var TokunoCount = 0;
			var OtherCount = 0;


			// Create the data set
			var ds = new DataSet(SpawnDataSetName);

			// Load the data set up
			ds.Tables.Add(SpawnTablePointName);

			// Create spawn point schema
			ds.Tables[SpawnTablePointName].Columns.Add("Name");
			ds.Tables[SpawnTablePointName].Columns.Add("UniqueId");
			ds.Tables[SpawnTablePointName].Columns.Add("Map");
			ds.Tables[SpawnTablePointName].Columns.Add("X");
			ds.Tables[SpawnTablePointName].Columns.Add("Y");
			ds.Tables[SpawnTablePointName].Columns.Add("Width");
			ds.Tables[SpawnTablePointName].Columns.Add("Height");
			ds.Tables[SpawnTablePointName].Columns.Add("CentreX");
			ds.Tables[SpawnTablePointName].Columns.Add("CentreY");
			ds.Tables[SpawnTablePointName].Columns.Add("CentreZ");
			ds.Tables[SpawnTablePointName].Columns.Add("Range");
			ds.Tables[SpawnTablePointName].Columns.Add("MaxCount");
			ds.Tables[SpawnTablePointName].Columns.Add("MinDelay");
			ds.Tables[SpawnTablePointName].Columns.Add("MaxDelay");
			// deal with the double format for delay. old format stored them as minutes in int format. that meant that short delays were lost
			// proper solution would simply be to store as doubles, but older progs still assume int format (like spawneditor)
			// so this is the solution.  add a flag and do it both ways.
			ds.Tables[SpawnTablePointName].Columns.Add("DelayInSec");

			// add the duration and proximity range and sound parameters, and in container flag and coords inside the container
			ds.Tables[SpawnTablePointName].Columns.Add("Duration");
			ds.Tables[SpawnTablePointName].Columns.Add("DespawnTime");
			ds.Tables[SpawnTablePointName].Columns.Add("ProximityRange");
			ds.Tables[SpawnTablePointName].Columns.Add("ProximityTriggerSound");
			ds.Tables[SpawnTablePointName].Columns.Add("ProximityTriggerMessage");
			ds.Tables[SpawnTablePointName].Columns.Add("ObjectPropertyName");
			ds.Tables[SpawnTablePointName].Columns.Add("ObjectPropertyItemName");
			ds.Tables[SpawnTablePointName].Columns.Add("SetPropertyItemName");
			ds.Tables[SpawnTablePointName].Columns.Add("ItemTriggerName");
			ds.Tables[SpawnTablePointName].Columns.Add("NoItemTriggerName");
			ds.Tables[SpawnTablePointName].Columns.Add("MobTriggerName");
			ds.Tables[SpawnTablePointName].Columns.Add("MobPropertyName");
			ds.Tables[SpawnTablePointName].Columns.Add("PlayerPropertyName");
			ds.Tables[SpawnTablePointName].Columns.Add("TriggerProbability");
			ds.Tables[SpawnTablePointName].Columns.Add("SpeechTrigger");
			ds.Tables[SpawnTablePointName].Columns.Add("SkillTrigger");
			ds.Tables[SpawnTablePointName].Columns.Add("InContainer");
			ds.Tables[SpawnTablePointName].Columns.Add("ContainerX");
			ds.Tables[SpawnTablePointName].Columns.Add("ContainerY");
			ds.Tables[SpawnTablePointName].Columns.Add("ContainerZ");
			ds.Tables[SpawnTablePointName].Columns.Add("MinRefractory");
			ds.Tables[SpawnTablePointName].Columns.Add("MaxRefractory");
			ds.Tables[SpawnTablePointName].Columns.Add("TODStart");
			ds.Tables[SpawnTablePointName].Columns.Add("TODEnd");
			ds.Tables[SpawnTablePointName].Columns.Add("TODMode");
			ds.Tables[SpawnTablePointName].Columns.Add("KillReset");
			ds.Tables[SpawnTablePointName].Columns.Add("ExternalTriggering");
			ds.Tables[SpawnTablePointName].Columns.Add("SequentialSpawning");
			ds.Tables[SpawnTablePointName].Columns.Add("RegionName");
			ds.Tables[SpawnTablePointName].Columns.Add("AllowGhostTriggering");
			ds.Tables[SpawnTablePointName].Columns.Add("AllowNPCTriggering");
			ds.Tables[SpawnTablePointName].Columns.Add("SpawnOnTrigger");
			ds.Tables[SpawnTablePointName].Columns.Add("ConfigFile");
			ds.Tables[SpawnTablePointName].Columns.Add("SmartSpawning");
			ds.Tables[SpawnTablePointName].Columns.Add("TickReset");

			ds.Tables[SpawnTablePointName].Columns.Add("WayPoint");
			ds.Tables[SpawnTablePointName].Columns.Add("Team");
			// amount for stacked item spawns
			ds.Tables[SpawnTablePointName].Columns.Add("Amount");
			ds.Tables[SpawnTablePointName].Columns.Add("IsGroup");
			ds.Tables[SpawnTablePointName].Columns.Add("IsRunning");
			ds.Tables[SpawnTablePointName].Columns.Add("IsHomeRangeRelative");
			if (oldformat)
				ds.Tables[SpawnTablePointName].Columns.Add("Objects");
			else
				ds.Tables[SpawnTablePointName].Columns.Add("Objects2");

			// Add each spawn point to the new table
			foreach (var sp in savelist)
			{
				if (sp == null || sp.Map == null || sp.Deleted)
					continue;

				if (verbose && from != null)
					// Send a message to the client that the spawner is being saved
					from.SendMessage(68, "Saving '{0}' in {1} at {2}", sp.Name, sp.Map.Name, sp.Location.ToString());

				// Create a new data row
				var dr = ds.Tables[SpawnTablePointName].NewRow();

				// Populate the data
				dr["Name"] = (string)sp.Name;

				// Set the unqiue id
				dr["UniqueId"] = (string)sp.m_UniqueId;

				// Get the map name
				dr["Map"] = (string)sp.Map.Name;

				// Convert the xml map value to a real map object
				if (String.Compare(sp.Map.Name, Map.Trammel.Name, true) == 0)
					TrammelCount++;
				else if (String.Compare(sp.Map.Name, Map.Felucca.Name, true) == 0)
					FeluccaCount++;
				else if (String.Compare(sp.Map.Name, Map.Ilshenar.Name, true) == 0)
					IlshenarCount++;
				else if (String.Compare(sp.Map.Name, Map.Malas.Name, true) == 0)
					MalasCount++;
				else if (String.Compare(sp.Map.Name, Map.Tokuno.Name, true) == 0)
					TokunoCount++;
				else
					OtherCount++;

				dr["X"] = (int)sp.m_X;
				dr["Y"] = (int)sp.m_Y;
				dr["Width"] = (int)sp.m_Width;
				dr["Height"] = (int)sp.m_Height;

				// check to see if this is in a container
				if (sp.RootParent is Container)
				{
					dr["CentreX"] = (int)((Container)sp.RootParent).Location.X;
					dr["CentreY"] = (int)((Container)sp.RootParent).Location.Y;
					dr["CentreZ"] = (int)((Container)sp.RootParent).Location.Z;
					dr["ContainerX"] = (int)sp.Location.X;
					dr["ContainerY"] = (int)sp.Location.Y;
					dr["ContainerZ"] = (int)sp.Location.Z;
					dr["InContainer"] = true;
				}
				else
				{
					dr["CentreX"] = (int)sp.Location.X;
					dr["CentreY"] = (int)sp.Location.Y;
					dr["CentreZ"] = (int)sp.Location.Z;
					//dr["ContainerX"] = 0;
					//dr["ContainerY"] = 0;
					//dr["ContainerZ"] = 0;
					dr["InContainer"] = false;
				}

				dr["Range"] = (int)sp.m_HomeRange;
				dr["MaxCount"] = (int)sp.m_Count;

				// need to deal with the fact that the old xmlspawner xml format only saved delays in minutes as ints, so shorter spawn times
				// are lost
				// flag it then on reading it can be properly handled and still
				// maintain backward compatibility with older xml files
				if ((int)sp.m_MinDelay.TotalSeconds - 60 * (int)sp.m_MinDelay.TotalMinutes > 0 ||
				    (int)sp.m_MaxDelay.TotalSeconds - 60 * (int)sp.m_MaxDelay.TotalMinutes > 0)
				{
					dr["DelayInSec"] = true;
					dr["MinDelay"] = (int)sp.m_MinDelay.TotalSeconds;
					dr["MaxDelay"] = (int)sp.m_MaxDelay.TotalSeconds;
				}
				else
				{
					dr["DelayInSec"] = false;
					dr["MinDelay"] = (int)sp.m_MinDelay.TotalMinutes;
					dr["MaxDelay"] = (int)sp.m_MaxDelay.TotalMinutes;
				}

				// additional parameters
				dr["TODStart"] = (double)sp.m_TODStart.TotalMinutes;
				dr["TODEnd"] = (double)sp.m_TODEnd.TotalMinutes;
				dr["TODMode"] = (int)sp.m_TODMode;
				dr["KillReset"] = (int)sp.m_KillReset;
				dr["MinRefractory"] = (double)sp.m_MinRefractory.TotalMinutes;
				dr["MaxRefractory"] = (double)sp.m_MaxRefractory.TotalMinutes;
				dr["Duration"] = (double)sp.m_Duration.TotalMinutes;
				dr["DespawnTime"] = (double)sp.m_DespawnTime.TotalHours;
				dr["ExternalTriggering"] = (bool)sp.m_ExternalTriggering;

				dr["ProximityRange"] = (int)sp.m_ProximityRange;
				dr["ProximityTriggerSound"] = (int)sp.m_ProximityTriggerSound;
				dr["ProximityTriggerMessage"] = sp.m_ProximityTriggerMessage;
				if (sp.m_ObjectPropertyItem != null && !sp.m_ObjectPropertyItem.Deleted)
					dr["ObjectPropertyItemName"] = String.Format("{0},{1}", sp.m_ObjectPropertyItem.Name,
						sp.m_ObjectPropertyItem.GetType().Name);
				else
					dr["ObjectPropertyItemName"] = null;
				dr["ObjectPropertyName"] = sp.m_ObjectPropertyName;
				if (sp.m_SetPropertyItem != null && !sp.m_SetPropertyItem.Deleted)
					dr["SetPropertyItemName"] = String.Format("{0},{1}", sp.m_SetPropertyItem.Name,
						sp.m_SetPropertyItem.GetType().Name);
				else
					dr["SetPropertyItemName"] = null;
				dr["ItemTriggerName"] = sp.m_ItemTriggerName;
				dr["NoItemTriggerName"] = sp.m_NoItemTriggerName;
				dr["MobTriggerName"] = sp.m_MobTriggerName;
				dr["MobPropertyName"] = sp.m_MobPropertyName;
				dr["PlayerPropertyName"] = sp.m_PlayerPropertyName;
				dr["TriggerProbability"] = sp.m_TriggerProbability;
				dr["SequentialSpawning"] = sp.m_SequentialSpawning;
				dr["RegionName"] = sp.m_RegionName;
				dr["AllowGhostTriggering"] = sp.m_AllowGhostTriggering;
				dr["AllowNPCTriggering"] = sp.m_AllowNPCTriggering;
				dr["SpawnOnTrigger"] = sp.m_SpawnOnTrigger;
				dr["ConfigFile"] = sp.m_ConfigFile;
				dr["SmartSpawning"] = sp.m_SmartSpawning;
				dr["TickReset"] = sp.m_DisableGlobalAutoReset;

				dr["SpeechTrigger"] = sp.m_SpeechTrigger;
				dr["SkillTrigger"] = sp.m_SkillTrigger;
				dr["Amount"] = (int)sp.m_StackAmount;
				dr["Team"] = (int)sp.m_Team;

				// assign the waypoint based on the waypoint name if it deviates from the default waypoint name, otherwise do it by serial
				string waystr = null;
				if (sp.m_WayPoint != null)
				{
					if (sp.m_WayPoint.Name != defwaypointname && sp.m_WayPoint.Name != null &&
					    sp.m_WayPoint.Name.Length > 0)
						waystr = sp.m_WayPoint.Name;
					else
						waystr = String.Format("SERIAL,{0}", sp.m_WayPoint.Serial);
				}

				dr["WayPoint"] = waystr;

				dr["IsGroup"] = (bool)sp.m_Group;
				dr["IsRunning"] = (bool)sp.m_Running;
				dr["IsHomeRangeRelative"] = (bool)sp.m_HomeRangeIsRelative;
				if (oldformat)
					dr["Objects"] = (string)sp.GetSerializedObjectList();
				else
					dr["Objects2"] = (string)sp.GetSerializedObjectList2();

				// Add the row the the table
				ds.Tables[SpawnTablePointName].Rows.Add(dr);

				// Increment the count
				TotalCount++;
			}


			// Write out the file
			var file_error = false;
			if (TotalCount > 0)
			{
				try
				{
					ds.WriteXml(stream);
				}
				catch { file_error = true; }

				if (file_error) return false;
			}

			try
			{
				stream.Close();
			}
			catch { }

			// Indicate how many spawners were written
			if (from != null)
				@from.SendMessage(
					"{0} spawner(s) were saved to file {1} [Trammel={2}, Felucca={3}, Ilshenar={4}, Malas={5}, Tokuno={6}, Other={7}].",
					TotalCount, dirname, TrammelCount, FeluccaCount, IlshenarCount, MalasCount, TokunoCount,
					OtherCount);
			return true;
		}

		private static void WipeSpawners(CommandEventArgs e, bool WipeAll)
		{
			if (e == null || e.Mobile == null) return;

			if (e.Mobile.AccessLevel >= AccessLevel.Administrator)
			{
				// Spawner delete criteria (if any)
				var SpawnerPrefix = String.Empty;

				// Check if there is an argument provided (delete criteria)
				if (e.Arguments != null && e.Arguments.Length > 0)
					SpawnerPrefix = e.Arguments[0];

				if (WipeAll == true)
					e.Mobile.SendMessage("Removing ALL XmlSpawner objects from the world{0}.",
						SpawnerPrefix != null && SpawnerPrefix.Length > 0
							? " beginning with " + SpawnerPrefix
							: String.Empty);
				else
					e.Mobile.SendMessage("Removing ALL XmlSpawner objects from {0}{1}.", e.Mobile.Map,
						SpawnerPrefix != null && SpawnerPrefix.Length > 0
							? " beginning with " + SpawnerPrefix
							: String.Empty);

				// Delete Xml spawner's in the world based on the mobiles current map
				var Count = 0;
				var ToDelete = new List<Item>();
				foreach (var i in World.Items.Values)
					if (i is XmlSpawner && (WipeAll == true || i.Map == e.Mobile.Map) && i.Deleted == false)
						// Check if there is a delete condition
						if (SpawnerPrefix == null || SpawnerPrefix.Length == 0 || i.Name.StartsWith(SpawnerPrefix))
						{
							// Send a message to the client that the spawner is being deleted
							e.Mobile.SendMessage(33, "Removing '{0}' in {1} at {2}", i.Name, i.Map.Name,
								i.Location.ToString());

							ToDelete.Add(i);
							Count++;
						}

				// Delete the items in the array list
				foreach (var i in ToDelete)
					i.Delete();

				if (WipeAll == true)
					e.Mobile.SendMessage("Removed {0} XmlSpawner objects from the world.", Count);
				else
					e.Mobile.SendMessage("Removed {0} XmlSpawner objects from {1}.", Count, e.Mobile.Map);
			}
			else
				e.Mobile.SendMessage("You do not have rights to perform this command.");
		}


		[Usage("XmlSpawnerRespawn [SpawnerPrefixFilter]")]
		[Description("Respawns all XmlSpawner objects from the current map.")]
		public static void Respawn_OnCommand(CommandEventArgs e)
		{
			RespawnSpawners(e, false);
		}

		[Usage("XmlSpawnerRespawnAll [SpawnerPrefixFilter]")]
		[Description("Respawns all XmlSpawner objects from the entire world.")]
		public static void RespawnAll_OnCommand(CommandEventArgs e)
		{
			RespawnSpawners(e, true);
		}

		private static void RespawnSpawners(CommandEventArgs e, bool RespawnAll)
		{
			if (e == null || e.Mobile == null) return;

			if (e.Mobile.AccessLevel >= AccessLevel.Administrator)
			{
				// Spawner Respawn criteria (if any)
				var SpawnerPrefix = String.Empty;

				// Check if there is an argument provided (respawn criteria)
				if (e.Arguments != null && e.Arguments.Length > 0)
					SpawnerPrefix = e.Arguments[0];

				if (RespawnAll == true)
					e.Mobile.SendMessage("Respawning ALL XmlSpawner objects from the world{0}.",
						SpawnerPrefix != null && SpawnerPrefix.Length > 0
							? " beginning with " + SpawnerPrefix
							: String.Empty);
				else
					e.Mobile.SendMessage("Respawning ALL XmlSpawner objects from {0}{1}.", e.Mobile.Map,
						SpawnerPrefix != null && SpawnerPrefix.Length > 0
							? " beginning with " + SpawnerPrefix
							: String.Empty);

				// Respawn Xml spawner's in the world based on the mobiles current map
				var Count = 0;
				var ToRespawn = new List<Item>();
				foreach (var i in World.Items.Values)
					try
					{
						if (i is XmlSpawner && (RespawnAll == true || i.Map == e.Mobile.Map) && i.Deleted == false)
							// Check if there is a respawn condition
							if (SpawnerPrefix == null || SpawnerPrefix.Length == 0 ||
							    i.Name != null && i.Name.StartsWith(SpawnerPrefix))
							{
								ToRespawn.Add(i);
								Count++;
							}
					}
					catch (Exception ex) { Console.WriteLine("Error attempting to add {0}, {1}", i, ex.Message); }

				// Respawn the items in the array list
				foreach (var i in ToRespawn)
				{
					// Send a message to the client that the spawner is being respawned
					e.Mobile.SendMessage(33, "Respawning '{0}' in {1} at {2}", i.Name, i.Map.Name,
						i.Location.ToString());
					var CheckXmlSpawner = (XmlSpawner)i;
					CheckXmlSpawner.Respawn();
				}

				if (RespawnAll == true)
					e.Mobile.SendMessage("Respawned {0} XmlSpawner objects from the world.", Count);
				else
					e.Mobile.SendMessage("Respawned {0} XmlSpawner objects from {1}.", Count, e.Mobile.Map);
			}
			else
				e.Mobile.SendMessage("You do not have rights to perform this command.");
		}

#if(TRACE)
		public static void XmlMake_OnCommand( CommandEventArgs e )
		{

				if( e.Arguments.Length > 0 ){
				int count = 0;
				try{
				count = Convert.ToInt32( e.Arguments[0],10);
				} catch{}

			for(int i = 0;i<count;i++)
			{
				if( e.Arguments.Length > 2)
				{
				   Spawner x = new Spawner(10,1,1,0,2,e.Arguments[1]);
				   x.Location = new Point3D(5400+Utility.Random(700),1090+Utility.Random(180),0);
				   x.Map = Map.Trammel;
				} else
				if( e.Arguments.Length > 1)
				{
				  XmlSpawner x = new XmlSpawner(10,1,1,0,2,e.Arguments[1]);
				  x.Location = new Point3D(5400+Utility.Random(700),1090+Utility.Random(180),0);
				  x.Map = Map.Trammel;
				  //x.MinDelay = TimeSpan.FromSeconds(1);
				  //x.MaxDelay = TimeSpan.FromSeconds(1);
					//x.ProximityRange = 0;
				}
			}
			if( e.Arguments.Length > 2)
			{
				   e.Mobile.SendMessage( "Created {0} Spawner objects.", count );
			} else {
					e.Mobile.SendMessage( "Created {0} XmlSpawner objects.", count );
			}


		   }
		}

		public static void XmlTrace_OnCommand( )
		{
			XmlTrace_OnCommand( null );
		}

		public static void XmlTrace_OnCommand( CommandEventArgs e )
		{
			Process currentprocess = Process.GetCurrentProcess();
			TimeSpan runningtime = DateTime.Now - XmlSpawner._traceStartTime;
			double processtime = currentprocess.UserProcessorTime.TotalMilliseconds - _startProcessTime;
			double sysload = 0;

			if(runningtime.TotalMilliseconds > 0)
			{
				sysload = processtime/runningtime.TotalMilliseconds;
			}

			Console.WriteLine( "______________");
			Console.WriteLine( "Active Traces:");
			Console.WriteLine( "Running Time = {0}",runningtime);
			Console.WriteLine( "Adjusted Process Time = {0:####.####} secs",processtime/1000);
			Console.WriteLine( "Processor Time = {0} ({1:p3} avg sys load)",currentprocess.UserProcessorTime,sysload);

			for(int i = 0;i<MaxTraces;i++)
			{
				if( XmlSpawner._traceCount[i] > 0)
				{
					double load = 0;
					if(processtime > 0)
					{
						load = ((double)XmlSpawner._traceTotal[i].TotalMilliseconds)/processtime;
					}
						 Console.WriteLine( "{0} ({4}) {1,21} / {2} calls = {3:####.####} ms/call, {5:p3}",
						i,XmlSpawner._traceTotal[i], XmlSpawner._traceCount[i],((double)XmlSpawner._traceTotal[i].TotalMilliseconds)/XmlSpawner._traceCount[i],
						XmlSpawner._traceName[i], load);
				}
			}
		}

		public static void XmlResetTrace_OnCommand( CommandEventArgs e )
		{

			if( e.Arguments.Length >= 0 )
			{
				 for(int i = 0;i<MaxTraces;i++)
				 {
						XmlSpawner._traceCount[i] = 0;
						XmlSpawner._traceTotal[i] = TimeSpan.Zero;
				}
				XmlSpawner._traceStartTime = DateTime.Now;

				Process currentprocess = Process.GetCurrentProcess();
				_startProcessTime = currentprocess.UserProcessorTime.TotalMilliseconds;

				 Console.WriteLine( "Traces reset");
			}
		}
#endif

		#endregion

		#region Constructors

		[Constructable]
		public XmlSpawner()
			: base(BaseItemId)
		{
			m_PlayerCreated = true;
			m_UniqueId = Guid.NewGuid().ToString();
			SpawnRange = defSpawnRange;

			InitSpawn(0, 0, m_Width, m_Height, String.Empty, 0, defMinDelay, defMaxDelay, defDuration,
				defProximityRange, defProximityTriggerSound, defAmount, defTeam, defHomeRange, defRelativeHome,
				new SpawnObject[0], defMinRefractory, defMaxRefractory,
				defTODStart, defTODEnd, null, null, null, null, null, null, null, null, null, defTriggerProbability,
				null, defIsGroup, defTODMode,
				defKillReset, false, -1, null, false, false, false, null, defDespawnTime, null, false, null);
		}

		[Constructable]
		public XmlSpawner(int amount, int minDelay, int maxDelay, int team, int homeRange, string creatureName)
			: base(BaseItemId)
		{
			m_PlayerCreated = true;
			m_UniqueId = Guid.NewGuid().ToString();
			SpawnRange = homeRange;
			var so = new SpawnObject[1];
			so[0] = new SpawnObject(creatureName, amount);

			InitSpawn(0, 0, m_Width, m_Height, String.Empty, amount, TimeSpan.FromMinutes(minDelay),
				TimeSpan.FromMinutes(maxDelay), defDuration,
				defProximityRange, defProximityTriggerSound, defAmount, team, homeRange, defRelativeHome, so,
				defMinRefractory, defMaxRefractory,
				defTODStart, defTODEnd, null, null, null, null, null, null, null, null, null, defTriggerProbability,
				null, defIsGroup, defTODMode,
				defKillReset, false, -1, null, false, false, false, null, defDespawnTime, null, false, null);
		}

		[Constructable]
		public XmlSpawner(int amount, int minDelay, int maxDelay, int team, int homeRange, int spawnRange,
			string creatureName)
			: base(BaseItemId)
		{
			m_PlayerCreated = true;
			m_UniqueId = Guid.NewGuid().ToString();
			SpawnRange = spawnRange;
			var so = new SpawnObject[1];
			so[0] = new SpawnObject(creatureName, amount);

			InitSpawn(0, 0, m_Width, m_Height, String.Empty, amount, TimeSpan.FromMinutes(minDelay),
				TimeSpan.FromMinutes(maxDelay), defDuration,
				defProximityRange, defProximityTriggerSound, defAmount, team, homeRange, defRelativeHome, so,
				defMinRefractory, defMaxRefractory,
				defTODStart, defTODEnd, null, null, null, null, null, null, null, null, null, defTriggerProbability,
				null, defIsGroup, defTODMode,
				defKillReset, false, -1, null, false, false, false, null, defDespawnTime, null, false, null);
		}

		[Constructable]
		public XmlSpawner(string creatureName)
			: base(BaseItemId)
		{
			m_PlayerCreated = true;
			m_UniqueId = Guid.NewGuid().ToString();
			var so = new SpawnObject[1];
			so[0] = new SpawnObject(creatureName, 1);
			SpawnRange = defSpawnRange;

			InitSpawn(0, 0, m_Width, m_Height, String.Empty, 1, defMinDelay, defMaxDelay, defDuration,
				defProximityRange, defProximityTriggerSound, defAmount, defTeam, defHomeRange, defRelativeHome, so,
				defMinRefractory, defMaxRefractory,
				defTODStart, defTODEnd, null, null, null, null, null, null, null, null, null, defTriggerProbability,
				null, defIsGroup, defTODMode,
				defKillReset, false, -1, null, false, false, false, null, defDespawnTime, null, false, null);
		}

		public XmlSpawner(Guid uniqueId, int x, int y, int width, int height, string name, int maxCount,
			TimeSpan minDelay, TimeSpan maxDelay, TimeSpan duration,
			int proximityRange, int proximityTriggerSound, int amount, int team, int homeRange,
			bool isRelativeHomeRange, SpawnObject[] spawnObjects,
			TimeSpan minRefractory, TimeSpan maxRefractory, TimeSpan todstart, TimeSpan todend, Item objectPropertyItem,
			string objectPropertyName, string proximityMessage,
			string itemTriggerName, string noitemTriggerName, string speechTrigger, string mobTriggerName,
			string mobPropertyName, string playerPropertyName, double triggerProbability,
			Item setPropertyItem, bool isGroup, TODModeType todMode, int killReset, bool externalTriggering,
			int sequentialSpawning, string regionName,
			bool allowghost, bool allownpc, bool spawnontrigger, string configfile, TimeSpan despawnTime,
			string skillTrigger, bool smartSpawning, WayPoint wayPoint)
			: base(BaseItemId)
		{
			m_UniqueId = uniqueId.ToString();
			InitSpawn(x, y, width, height, name, maxCount, minDelay, maxDelay, duration,
				proximityRange, proximityTriggerSound, amount, team, homeRange, isRelativeHomeRange, spawnObjects,
				minRefractory, maxRefractory, todstart, todend,
				objectPropertyItem, objectPropertyName, proximityMessage, itemTriggerName, noitemTriggerName,
				speechTrigger, mobTriggerName, mobPropertyName, playerPropertyName,
				triggerProbability, setPropertyItem, isGroup, todMode, killReset, externalTriggering,
				sequentialSpawning, regionName, allowghost, allownpc, spawnontrigger, configfile,
				despawnTime, skillTrigger, smartSpawning, wayPoint);
		}


		public void InitSpawn(int x, int y, int width, int height, string name, int maxCount, TimeSpan minDelay,
			TimeSpan maxDelay, TimeSpan duration,
			int proximityRange, int proximityTriggerSound, int amount, int team, int homeRange,
			bool isRelativeHomeRange, SpawnObject[] objectsToSpawn,
			TimeSpan minRefractory, TimeSpan maxRefractory, TimeSpan todstart, TimeSpan todend, Item objectPropertyItem,
			string objectPropertyName, string proximityMessage,
			string itemTriggerName, string noitemTriggerName, string speechTrigger, string mobTriggerName,
			string mobPropertyName, string playerPropertyName, double triggerProbability,
			Item setPropertyItem, bool isGroup, TODModeType todMode, int killReset, bool externalTriggering,
			int sequentialSpawning, string regionName, bool allowghost, bool allownpc, bool spawnontrigger,
			string configfile, TimeSpan despawnTime, string skillTrigger, bool smartSpawning, WayPoint wayPoint)
		{
			Visible = false;
			Movable = false;
			m_X = x;
			m_Y = y;
			m_Width = width;
			m_Height = height;

			// init spawn range if compatible
			if (width == height)
				m_SpawnRange = width / 2;
			else
				m_SpawnRange = -1;
			m_Running = true;
			m_Group = isGroup;

			if (name != null && name.Length > 0)
				Name = name;
			else
				Name = "Spawner";

			m_MinDelay = minDelay;
			m_MaxDelay = maxDelay;

			// duration and proximity range parameter
			m_MinRefractory = minRefractory;
			m_MaxRefractory = maxRefractory;
			m_TODStart = todstart;
			m_TODEnd = todend;
			m_TODMode = todMode;
			m_KillReset = killReset;
			m_Duration = duration;
			m_DespawnTime = despawnTime;
			m_ProximityRange = proximityRange;
			m_ProximityTriggerSound = proximityTriggerSound;
			m_proximityActivated = false;
			m_durActivated = false;
			m_refractActivated = false;
			m_Count = maxCount;
			m_Team = team;
			m_StackAmount = amount;
			m_HomeRange = homeRange;
			m_HomeRangeIsRelative = isRelativeHomeRange;
			m_ObjectPropertyItem = objectPropertyItem;
			m_ObjectPropertyName = objectPropertyName;
			m_ProximityTriggerMessage = proximityMessage;
			m_ItemTriggerName = itemTriggerName;
			m_NoItemTriggerName = noitemTriggerName;
			m_SpeechTrigger = speechTrigger;
			SkillTrigger = skillTrigger; // note this will register the skill as well
			m_MobTriggerName = mobTriggerName;
			m_MobPropertyName = mobPropertyName;
			m_PlayerPropertyName = playerPropertyName;
			m_TriggerProbability = triggerProbability;
			m_SetPropertyItem = setPropertyItem;
			m_ExternalTriggering = externalTriggering;
			m_ExternalTrigger = false;
			m_SequentialSpawning = sequentialSpawning;
			RegionName = regionName;
			m_AllowGhostTriggering = allowghost;
			m_AllowNPCTriggering = allownpc;
			m_SpawnOnTrigger = spawnontrigger;
			m_SmartSpawning = smartSpawning;
			ConfigFile = configfile;
			m_WayPoint = wayPoint;

			// set the totalitem property to -1 so that it doesnt show up in the item count of containers
			//TotalItems = -1;
			//UpdateTotal(this, TotalType.Items, -1);

			// Create the array of spawned objects
			m_SpawnObjects = new List<SpawnObject>();

			// Assign the list of objects to spawn
			SpawnObjects = objectsToSpawn;

			// Kick off the process
			DoTimer(TimeSpan.FromSeconds(1));
		}

		public XmlSpawner(Serial serial)
			: base(serial)
		{
		}

		#endregion

		#region Defrag methods

		public void Defrag(bool killtest)
		{
			if (m_SpawnObjects == null) return;

			var removed = false;
			var total_removed = 0;

			var deleteilist = new List<Item>();
			var deletemlist = new List<Mobile>();
			foreach (var so in m_SpawnObjects)
				for (var x = 0; x < so.SpawnedObjects.Count; x++)
				{
					var o = so.SpawnedObjects[x];

					if (o is Item)
					{
						var item = (Item)o;
						var despawned = false;
						// check to see if the despawn time has elapsed.  If so, then delete it if it hasnt been picked up or stolen.
						if (DespawnTime.TotalHours > 0 && !item.Deleted &&
						    item.LastMoved < DateTime.Now - DespawnTime && item.Parent == Parent
						    && (!ItemFlags.GetTaken(item) ||
						        item.Parent != null &&
						        item.Parent == Parent)) // can despawn if just moved within the same container
						{
							//item.Delete();
							deleteilist.Add(item);
							despawned = true;
						}

						// Check if the items has been deleted or
						// if something else now owns the item (picked it up for example)
						// also check the stolen/placed in container flag.  If any of those are true then the spawner doesnt own it any more so take it off the list.
						// the stolen/container flag prevents spawns from being left on the list when players take them and lock them back down on the ground.
						// If you have made the changes to stealing.cs and container.cs described in xmlspawner2.txt then just uncomment the line below to
						// enable this check
						if (item.Deleted || despawned || item.Parent != Parent // different container
						    || ItemFlags.GetTaken(item) &&
						    (item.Parent == null ||
						     item.Parent != Parent)) // taken and in the world, or a different container
						{
							so.SpawnedObjects.Remove(o);
							x--;
							removed = true;
							// if sequential spawning is active and the RestrictKillsToSubgroup flag is set, then check to see if
							// the object is in the current subgroup before adding to the total
							if (SequentialSpawn >= 0 && so.RestrictKillsToSubgroup)
							{
								if (so.SubGroup == SequentialSpawn)
									total_removed++;
							}
							else
								// just add it
								total_removed++;
						}
					}
					else if (o is Mobile)
					{
						var m = (Mobile)o;

						// check to see if the spawn has been idle for a long time
						// if it has then reposition it because it might be in an inaccessible location
						// note, vendors and special cases of positioning from the Spawn method will not get relocated
						// i'm not saying I like those special cases, but they should be treated consistently
						// doesnt work properly under RunUO 2.0 and also doesnt properly take spawn control keywords into consideration
						// when repositioning, so disable this for now
						/*
						if (SpawnIdleTime > 0 && !m.Deleted && !(m is BaseVendor) && (m.CreationTime < DateTime.Now - TimeSpan.FromHours(SpawnIdleTime))
							&& m.Map != null && m.Map != Map.Internal && !m.Map.GetSector(m.Location).Active)
						{
							// determine whether the requiresurface flag is set
							m.Location = GetSpawnPosition(so.RequireSurface, m);

							// and reset the creation time (simulates respawning the identical mob at a new location)
							//m.CreationTime = DateTime.Now;
						}
						 * */
						var despawned = false;
						// check to see if the despawn time has elapsed.  If so, and the sector is not active then delete it.
						if (DespawnTime.TotalHours > 0 && !m.Deleted && m.CreationTime < DateTime.Now - DespawnTime
						    && m.Map != null && m.Map != Map.Internal && !m.Map.GetSector(m.Location).Active)
						{
							//m.Delete();
							deletemlist.Add(m);
							despawned = true;
						}

						if (m.Deleted || despawned)
						{
							// Remove the delete mobile from the list
							so.SpawnedObjects.Remove(o);
							x--;
							removed = true;
							// if sequential spawning is active and the RestrictKillsToSubgroup flag is set, then check to see if
							// the object is in the current subgroup before adding to the total
							if (SequentialSpawn >= 0 && so.RestrictKillsToSubgroup)
							{
								if (so.SubGroup == SequentialSpawn)
									total_removed++;
							}
							else
								// just add it
								total_removed++;
						}
						else if (m is BaseCreature)
						{
							var b = (BaseCreature)m;
							// Check if the creature has been tamed or previously tamed and released
							// and if it is, remove it from the list of spawns
							if (b.Controlled || b.IsStabled || b.Owners != null && b.Owners.Count > 0)
							{
								so.SpawnedObjects.Remove(o);
								x--;
								removed = true;
								// if sequential spawning is active and the RestrictKillsToSubgroup flag is set, then check to see if
								// the object is in the current subgroup before adding to the total
								if (SequentialSpawn >= 0 && so.RestrictKillsToSubgroup)
								{
									if (so.SubGroup == SequentialSpawn)
										total_removed++;
								}
								else
									// just add it
									total_removed++;
							}
						}
					}
					else if (o is BaseXmlSpawner.KeywordTag)
					{
						var tag = (BaseXmlSpawner.KeywordTag)o;
						if (tag.Deleted)
						{
							so.SpawnedObjects.Remove(o);
							x--;
							removed = true;
						}
					}
					else
					{
						// Don't know what this is, so remove it
						Console.WriteLine("removing unknown {0} from spawnlist", so);
						so.SpawnedObjects.Remove(o);
						x--;
						removed = true;
					}
				}

			DeleteFromList(deleteilist, deletemlist);

			// Check if anything has been removed
			if (removed == true)
				InvalidateProperties();

			// increment the killcount based upon the number of items that were removed from the spawnlist (i.e. were spawned but now are gone, presumed killed)
			if (killtest) m_killcount += total_removed;
		}

		// special defrag pass to remove GOTO keyword tags
		public void ClearGOTOTags()
		{
			if (m_SpawnObjects == null) return;

			var ToDelete = new List<BaseXmlSpawner.KeywordTag>();
			foreach (var so in m_SpawnObjects)
				for (var x = 0; x < so.SpawnedObjects.Count; x++)
				{
					var o = so.SpawnedObjects[x];
					if (o is BaseXmlSpawner.KeywordTag)
					{
						var sot = (BaseXmlSpawner.KeywordTag)o;
						// clear the tags except for gump and delay tags
						if (sot != null && sot.Type == 2)
						{
							ToDelete.Add(sot);
							so.SpawnedObjects.Remove(o);
							x--;
						}
					}
				}

			for (var x = ToDelete.Count - 1; x >= 0; --x) //BaseXmlSpawner.KeywordTag i in ToDelete)
			{
				var i = ToDelete[x];
				if (i != null && !i.Deleted) i.Delete();
			}
		}

		// special defrag pass to remove spawn object tags, which are placeholders for the special keyword spawn spec entries
		public void ClearTags(bool all)
		{
			if (m_SpawnObjects == null) return;
			var removed = false;
			var ToDelete = new List<BaseXmlSpawner.KeywordTag>();
			foreach (var so in m_SpawnObjects)
				for (var x = 0; x < so.SpawnedObjects.Count; x++)
				{
					var o = so.SpawnedObjects[x];
					if (o is BaseXmlSpawner.KeywordTag)
					{
						var sot = (BaseXmlSpawner.KeywordTag)o;
						// clear the tags except for gump and delay tags
						if (sot != null && (all || (sot.Flags & BaseXmlSpawner.KeywordFlags.Defrag) != 0))
						{
							ToDelete.Add(sot);
							so.SpawnedObjects.Remove(o);
							x--;
							removed = true;
						}
					}
				}

			for (var x = ToDelete.Count - 1; x >= 0; --x) //each (BaseXmlSpawner.KeywordTag i in ToDelete)
			{
				var i = ToDelete[x];
				if (i != null && !i.Deleted) i.Delete();
			}

			// full clear of the taglist
			if (all) m_KeywordTagList.Clear();

			// Check if anything has been removed
			if (removed == true)
				InvalidateProperties();
		}

		public void DeleteGumpTags()
		{
			if (m_SpawnObjects == null) return;
			var removed = false;
			var ToDelete = new List<BaseXmlSpawner.KeywordTag>();
			foreach (var so in m_SpawnObjects)
				for (var x = 0; x < so.SpawnedObjects.Count; x++)
				{
					var o = so.SpawnedObjects[x];
					if (o is BaseXmlSpawner.KeywordTag)
					{
						var sot = (BaseXmlSpawner.KeywordTag)o;
						// clear the gump tags
						if (sot != null && sot.Type == 1)
						{
							ToDelete.Add(sot);
							so.SpawnedObjects.Remove(o);
							x--;
							removed = true;
						}
					}
				}

			for (var x = ToDelete.Count - 1; x >= 0; --x) //BaseXmlSpawner.KeywordTag i in ToDelete)
			{
				var i = ToDelete[x];
				if (i != null && !i.Deleted) i.Delete();
			}

			// Check if anything has been removed
			if (removed == true)
				InvalidateProperties();
		}

		public void DeleteTag(BaseXmlSpawner.KeywordTag tag)
		{
			if (m_SpawnObjects == null) return;
			var removed = false;
			var ToDelete = new List<BaseXmlSpawner.KeywordTag>();
			foreach (var so in m_SpawnObjects)
				for (var x = 0; x < so.SpawnedObjects.Count; x++)
				{
					var o = so.SpawnedObjects[x];
					if (o is BaseXmlSpawner.KeywordTag)
					{
						var sot = (BaseXmlSpawner.KeywordTag)o;
						// clear the matching tags
						if (sot != null && sot == tag)
						{
							ToDelete.Add(sot);
							so.SpawnedObjects.Remove(o);
							x--;
							removed = true;
						}
					}
				}

			for (var x = ToDelete.Count - 1; x >= 0; --x) //BaseXmlSpawner.KeywordTag i in ToDelete)
			{
				var i = ToDelete[x];
				if (i != null && !i.Deleted) i.Delete();
			}

			// Check if anything has been removed
			if (removed == true)
				InvalidateProperties();
		}

		#endregion

		#region SequentialSpawning methods

		private int SubGroupCount(int sgroup)
		{
			if (m_SpawnObjects == null) return 0;

			var nsub = 0;
			for (var i = 0; i < m_SpawnObjects.Count; i++)
			{
				var s = m_SpawnObjects[i];

				if (s.SubGroup == sgroup)
					nsub++;
			}

			return nsub;
		}

		private int RandomAvailableSpawnIndex()
		{
			// get spawn indices randomly from all available spawns independent of group
			return RandomAvailableSpawnIndex(-1);
		}

		// get spawn indices randomly from all available spawns of a group
		private int RandomAvailableSpawnIndex(int sgroup)
		{
			if (m_SpawnObjects == null) return -1;

			var maxrange = 0;
			List<int> sgrouplist = null;
			var totalcount = 0;
			// make a pass to determine which subgroups are available for spawning
			// by finding any subgroups that do not have available spawns
			for (var i = 0; i < m_SpawnObjects.Count; i++)
			{
				var s = m_SpawnObjects[i];
				if (s.SubGroup > 0 && (s.Ignore || s.Disabled)) continue;

				totalcount += s.SpawnedObjects.Count;
				if (s.SubGroup > 0 && s.SpawnedObjects.Count >= s.MaxCount)
				{
					// this subgroup is not available so add it to the list
					if (sgrouplist == null) sgrouplist = new List<int>();
					sgrouplist.Add(s.SubGroup);
				}
			}

			for (var i = 0; i < m_SpawnObjects.Count; i++)
			{
				var s = m_SpawnObjects[i];

				if (s.SubGroup > 0 && (s.Ignore || s.Disabled)) continue;

				if (s.MaxCount > s.SpawnedObjects.Count && (sgroup < 0 || sgroup == s.SubGroup)
				                                        && (sgrouplist == null || !sgrouplist.Contains(s.SubGroup)) &&
				                                        (s.SubGroup <= 0 || SubGroupCount(s.SubGroup) + totalcount <=
					                                        MaxCount))
				{
					// keep track of the number of spawn objects that are not at max (hence available for spawning)
					// this will be used to compute the probabilistic weighting function based on the relative
					// maxcounts of each entry
					maxrange += s.MaxCount;
					s.Available = true;
				}
				else
					s.Available = false;
			}

			// now generate a random number over the available spawnobjects
			// but only if the entire subgroup is available for spawning
			// note, subgroup zero is exempt from this check.
			if (maxrange > 0)
			{
				var randindex = Utility.Random(maxrange);

				// and map it into the avail spawns
				var currentrange = 0;
				for (var i = 0; i < m_SpawnObjects.Count; i++)
				{
					var s = m_SpawnObjects[i];
					if (s.SubGroup > 0 && (s.Ignore || s.Disabled)) continue;

					// keep track of the number of spawn objects that are not at max (hence available for spawning)
					if (s.Available)
					{
						// check to see if the random value maps into the range of the current index
						if (randindex >= currentrange && randindex < currentrange + s.MaxCount) return i;

						currentrange += s.MaxCount;
					}
				}

				// should never get here
				return -1;
			}
			else
				// no spawns are available
				return -1;
		}


		// get spawn indices randomly from all available spawns of a group
		private int RandomSpawnIndex(int sgroup)
		{
			if (m_SpawnObjects == null) return -1;

			var avail = 0;
			var maxrange = 0;
			for (var i = 0; i < m_SpawnObjects.Count; i++)
			{
				var s = m_SpawnObjects[i];

				// keep track of the number of spawn objects that are not at max (hence available for spawning)
				if (sgroup < 0 || sgroup == s.SubGroup)
				{
					avail++;
					maxrange += s.MaxCount;
				}
			}

			// now generate a random number over the available spawnobjects
			if (avail > 0 && maxrange > 0)
			{
				var randindex = Utility.Random(maxrange);

				// and map it into the avail spawns
				var currentrange = 0;

				for (var i = 0; i < m_SpawnObjects.Count; i++)
				{
					var s = m_SpawnObjects[i];

					// keep track of the number of spawn objects that are not at max (hence available for spawning)
					if (sgroup < 0 || sgroup == s.SubGroup)
					{
						if (randindex >= currentrange && randindex < currentrange + s.MaxCount)
							return i;

						currentrange += s.MaxCount;
					}
				}

				// should never get here
				return -1;
			}
			else
				// no spawns are available
				return -1;
		}

		// return the next subgroup in the sequence.
		public int NextSequentialIndex(int sgroup)
		{
			if (m_SpawnObjects == null || m_SpawnObjects.Count == 0) return 0;

			var finddirection = 1;
			var largergroup = -1;

			//find the next subgroup that is greater than the current one
			for (var j = 0; j < m_SpawnObjects.Count; j++)
			{
				var s = m_SpawnObjects[j];
				if (s.SubGroup > 0 && (s.Ignore || s.Disabled)) continue;

				var thisgroup = s.SubGroup;

				// start off by finding a subgroup that is larger
				if (finddirection == 1)
				{
					if (thisgroup > sgroup)
					{
						largergroup = thisgroup;

						// then work backward to find the group that is less than this but still larger than the current
						finddirection = -1;
					}
				}
				else
				{
					if (thisgroup > sgroup && thisgroup < largergroup)
					{
						largergroup = thisgroup;

						finddirection = -1;
					}
				}
			}

			// if couldnt find one larger, then it is time to wraparound
			if (largergroup < 0 && sgroup >= 0)
				return NextSequentialIndex(-1);
			else
				return largergroup;
		}

		// returns the spawn index of a spawn entry in the current sequential subgroup
		public int GetCurrentAvailableSequentialSpawnIndex(int sgroup)
		{
			if (sgroup < 0) return -1;

			if (m_SpawnObjects == null) return -1;

			if (sgroup == 0) return RandomAvailableSpawnIndex(0);

			//return the first instance of a spawn object that is an available member of the requested subgroup
			for (var j = 0; j < m_SpawnObjects.Count; j++)
			{
				var s = m_SpawnObjects[j];

				if (s.SubGroup == sgroup && s.MaxCount > s.SpawnedObjects.Count) return j;
			}

			// failed to find any spawn entry of the requested subgroup
			return -1;
		}

		// returns the spawn index of a spawn entry in the current sequential subgroup
		public int GetCurrentSequentialSpawnIndex(int sgroup)
		{
			if (sgroup < 0) return -1;

			if (m_SpawnObjects == null) return -1;

			if (sgroup == 0) return RandomSpawnIndex(0);

			//return the first instance of a spawn object that is an available member of the requested subgroup
			for (var j = 0; j < m_SpawnObjects.Count; j++)
				if (m_SpawnObjects[j].SubGroup == sgroup)
					return j;
			// failed to find any spawn entry of the requested subgroup
			return -1;
		}

		private void SeqResetTo(int sgroup)
		{
			// check the SequentialResetTo on the subgroup
			// cant do resets on subgroup 0
			if (sgroup == 0) return;

			// this will get the index of the first spawn entry in the subgroup
			// it will have the subgroup timer settings
			var spawnindex = GetCurrentSequentialSpawnIndex(sgroup);

			if (spawnindex >= 0)
			{
				// if it is greater than zero then initiate reset
				var s = m_SpawnObjects[spawnindex];
				m_SequentialSpawning = s.SequentialResetTo;

				InitiateSequentialReset(sgroup);

				// clear the spawns
				//RemoveSpawnObjects();
				ClearSubgroup(s.SubGroup);

				// and reset the kill count
				KillCount = 0;
			}
		}

		private bool CheckForSequentialReset()
		{
			// check the SequentialResetTime on the subgroup
			// cant do resets on subgroup 0
			if (m_SequentialSpawning == 0) return false;

			// this will get the index of the first spawn entry in the subgroup
			// it will have the subgroup timer settings
			var spawnindex = GetCurrentSequentialSpawnIndex(m_SequentialSpawning);

			if (spawnindex >= 0)
			{
				// check the reset time on it
				var s = m_SpawnObjects[spawnindex];
				// if it is greater than zero then resetting is possible
				if (s.SequentialResetTime > 0)
					// so check the reset timer
					if (NextSeqReset <= TimeSpan.Zero)
						// it has expired so time to reset
						return true;
			}

			return false;
		}

		private void InitiateSequentialReset(int sgroup)
		{
			// check the SequentialResetTime on the subgroup
			// cant do resets on subgroup 0
			if (sgroup == 0) return;

			// this will get the index of the first spawn entry in the subgroup
			// it will have the subgroup timer settings
			var spawnindex = GetCurrentSequentialSpawnIndex(sgroup);

			if (spawnindex >= 0)
			{
				// if it is greater than zero then initiate reset
				var s = m_SpawnObjects[spawnindex];
				NextSeqReset = TimeSpan.FromMinutes(s.SequentialResetTime);
			}
		}


		public void ResetSequential()
		{
			// go back to the lowest level
			if (m_SequentialSpawning >= 0) m_SequentialSpawning = NextSequentialIndex(-1);

			// reset the nextspawn times
			ResetNextSpawnTimes();

			// and reset the kill count
			KillCount = 0;
		}

		public bool AdvanceSequential()
		{
			// check for a sequence hold

			if (HoldSequence) return false;

			// check for triggering
			if (!((m_proximityActivated || CanFreeSpawn) && TODInRange)) return false;

			// if kills needed is greater than zero then check the killcount as well
			var spawnindex = GetCurrentSequentialSpawnIndex(m_SequentialSpawning);

			var killsneeded = 0;
			var subgroup = -1;
			var clearedobjects = false;

			if (spawnindex >= 0)
			{
				var s = m_SpawnObjects[spawnindex];
				subgroup = s.SubGroup;
				killsneeded = s.KillsNeeded;
			}

			// advance the sequential spawn index if it is enabled and kills needed have been satisfied
			if (m_SequentialSpawning >= 0 && (killsneeded == 0 || KillCount >= killsneeded))
			{
				m_SequentialSpawning = NextSequentialIndex(m_SequentialSpawning);

				// set the sequential reset based on the current sequence state
				// this will be checked in the spawner OnTick to determine whether to Reset the sequential state
				InitiateSequentialReset(m_SequentialSpawning);

				// clear the spawns if there is a killcount on the level
				if (killsneeded >= 0)
				{
					ClearSubgroup(subgroup);
					clearedobjects = true;
				}

				// and reset the kill count
				KillCount = 0;
			}

			// returning true will indicate that all spawns have been cleared and therefore a new spawn can be initiated in the same OnTick
			return clearedobjects;
		}

		#endregion

		#region Spawn methods

		private int killcount_held = 0;

		public void OnTick()
		{
			_TraceStart(8);
			// start up the timer again for the next Ontick
			DoTimer();

			// reset the protection against runaway looping
			ClearSpawnedThisTick = true;

			// if regional spawning is enabled, update the region in case new regions were added after the initialization pass
			CheckRegionAssignment = true;

			// reset the killcount whenever a spawntick goes by in which it could have spawned, ie the spawner is full, or proximity triggered
			// spawns were not activated.  Note that killcount gets incremented within Defrag whenever a spawn is that had been generated is removed from the active list.
			// Check the count before and then after the spawn passes.
			// if the spawner is still refractory then dont do a reset of the killcount.
			//int startcount = this.m_killcount;
			var startcount = killcount_held;
			if (!m_skipped) killcount_held = m_killcount;

			// killcount will be updated in Defrag
			Defrag(true);

			// remove any keyword tags that were made
			// note, tags only last a single ontick except for WAIT type
			ClearTags(false);

			if (!m_DisableGlobalAutoReset && startcount == m_killcount && !m_refractActivated && !m_skipped)
				m_spawncheck--;
			m_skipped = false;

			// allow for some slack in the killcount reset by resetting after a certain number of spawn ticks without kills pass
			if (m_spawncheck <= 0)
			{
				m_killcount = 0;
				m_spawncheck =
					m_KillReset; // wait for 1 spawn ticks to pass before resetting.  This can be set to anything you like
			}

			// check for smart spawning
			if (SmartSpawning && IsFull && !HasActiveSectors && !HasDamagedOrDistantSpawns /*&& !HasHoldSmartSpawning */
			   )
			{
				IsInactivated = true;
				// for multiple sector spawning ranges use the sector timer, otherwise just rely on OnSectorActivate to detect sector activation
				//if(!UseSectorActivate)
				//DoSectorTimer(TimeSpan.FromSeconds(1));

				SmartRemoveSpawnObjects();
			}

			// dont process spawn ticks while inactivated if smart spawning is enabled
			if (SmartSpawning && IsInactivated)
			{
				_TraceEnd(8);
				return;
			}

			IsInactivated = false;

			// check to see if spawning is on hold due to a WAIT keyword
			if (!OnHold)
			{
				// look for  triggers that are not player activated.
				if (m_ProximityRange == -1 && CanSpawn) CheckTriggers(null, null, false);

				// check for proximity triggers without movement activation
				if (m_ProximityRange >= 0 && CanSpawn)
					// check all nearby players
					foreach (var p in GetMobilesInRange(m_ProximityRange))
						if (ValidPlayerTrig(p))
							CheckTriggers(p, null, true);

				if (m_Group)
				{
					// check the seq reset time on the current subgroup
					// if the reset time is greater than zero then check the timer
					// if it has expired then reset the sequential subgroup
					// only do this if it can actually spawn
					if (CheckForSequentialReset())
					{
						// it has expired so reset the sequential spawn level
						SeqResetTo(m_SequentialSpawning);

						var triedtospawn = Respawn();

						if (triedtospawn) ClearGOTOTags();

						// dont advance if the spawn isnt triggered after resetting
						if (!triedtospawn) HoldSequence = true;
					}
					else if (TotalSpawnedObjects <= 0)
					{
						// advance the sequential spawn index if it is enabled
						AdvanceSequential();

						//bool hadhold = HoldSequence;

						//HoldSequence = false;

						var triedtospawn = Respawn();

						if (triedtospawn) ClearGOTOTags();

						//if(!triedtospawn) HoldSequence = hadhold;
					}
				}
				else
				{
					if (CheckForSequentialReset())
					{
						// it has expired so reset the sequential spawn level
						SeqResetTo(m_SequentialSpawning);

						// dont advance if the spawn isnt triggered after resetting
						HoldSequence = true;
					}
					else
						// advance the sequence before spawning
						AdvanceSequential();

					// keep track of the hold flag before trying to spawn in case no spawn attempt is made
					//bool hadhold = HoldSequence;

					// clear the hold flag to see if any of the spawned entries try to set it
					//HoldSequence = false;

					// try to spawn.  If spawning conditions such as triggering or TOD are not met, then it returns false
					var triedtospawn = Spawn(false, 0);

					if (triedtospawn) ClearGOTOTags();
					// this will maintain any sequential holds if spawning was suppressed due to triggering
					// if nothing was spawned or triggered, then restore the hold status to previous state
					//if(!triedtospawn) HoldSequence = hadhold;

					if (!FreeRun)
					{
						m_mob_who_triggered = null;
						m_skill_that_triggered = XmlSpawnerSkillCheck.RegisteredSkill.Invalid;
					}
				}

				// remove any keyword tags that were made except for WAIT type
				ClearTags(false);


				// and clear triggering flags
				if (!OnHold && !FreeRun) m_proximityActivated = false;
			}

			if (FreeRun && SpawnOnTrigger && m_proximityActivated)
			{
				// if it is in free run and was triggered, then just keep spawning as though it was triggered immediately
				NextSpawn = TimeSpan.Zero;
				ResetNextSpawnTimes();
			}


			//this.m_ExternalTrigger = false;
			// if it is out of the TOD range then delete the spawns
			if (!TODInRange)
			{
				RemoveSpawnObjects();

				ResetAllFlags();
			}

			_TraceEnd(8);
		}

		public bool ClearSpawnedThisTick
		{
			set
			{
				if (m_SpawnObjects == null || value == false) return;

				for (var i = 0; i < m_SpawnObjects.Count; i++)
				{
					var sobj = m_SpawnObjects[i];
					if (sobj != null) sobj.SpawnedThisTick = false;
				}
			}
		}

		// select and spawn something
		// return false if it cannot spawn, e.g. there is nothing to spawn or it is a triggerable spawner and has not been triggered
		public bool Spawn(bool smartspawn, byte loops)
		{
			if (m_SpawnObjects != null && m_SpawnObjects.Count > 0 && (m_proximityActivated || CanFreeSpawn) &&
			    TODInRange)
			{
				m_HoldSequence = false;

				// if the spawner is full then dont bother
				if (IsFull)
				{
					ResetProximityActivated();
					return true;
				}

				// Pick a spawn object to spawn
				var SpawnIndex = 0;

				// see if sequential spawning has been selected
				if (m_SequentialSpawning >= 0)
					// if so then use its value to get the index of the first available spawn entry in the next subgroup to be spawned
					// note, if the current sequence finds a zero group then the spawn will be picked at random from it
					SpawnIndex = GetCurrentAvailableSequentialSpawnIndex(m_SequentialSpawning);
				else
					// if sequential spawning is not set then select the next spawn at random
					SpawnIndex = RandomAvailableSpawnIndex();

				// no spawns are available so no point in continuing
				if (SpawnIndex < 0)
				{
					ResetProximityActivated();
					return true;
				}

				var sobj = m_SpawnObjects[SpawnIndex];
				var sgroup = sobj.SubGroup;

				// if this is part of a non-zero group, then spawn all of the group members as well
				if (sgroup != 0)
					SpawnSubGroup(sgroup, smartspawn, loops);
				/*
					for( int j = 0; j < m_SpawnObjects.Count; j++)
					{
						SpawnObject so = (SpawnObject)m_SpawnObjects[j];

						if(so.SubGroup == sgroup)
						{
							// get the SpawnsPerTick count and spawn up to that number
							bool success = Spawn( j, smartspawn, so.SpawnsPerTick );
									
							if(success && !smartspawn)
								RefreshNextSpawnTime(so);
						}
					}
					 */
				else
				{
					// Found a valid spawn object so spawn it and see if it successful
					if (Spawn(SpawnIndex, smartspawn, sobj.SpawnsPerTick, loops))
						if (!smartspawn)
							RefreshNextSpawnTime(sobj);
				}

				ResetProximityActivated();
				return true;
			}

			ResetProximityActivated();
			return false;
		}

		// spawn an individual entry by index up to count times
		public bool Spawn(int index, bool smartspawn, int count, int packrange, Point3D packcoord,
			bool ignoreloopprotection, byte loops)
		{
			if (m_SpawnObjects == null || index >= m_SpawnObjects.Count) return false;

			var didspawn = false;

			var so = m_SpawnObjects[index];

			if (so == null) return false;

			Defrag(false);

			// make sure you dont go over the individual entry maxcount
			var somax = so.MaxCount;
			var socnt = so.SpawnedObjects.Count;
			var nspawn = so.SpawnsPerTick;
			var scnt = SafeCurrentCount;

			for (var k = 0; k < nspawn && k + socnt < somax && k + scnt < MaxCount; k++)
			{
				if (packrange >= 0 && so.SubGroup > 0 && packcoord == Point3D.Zero)
					packcoord = GetPackCoord(so.SubGroup);
				if (Spawn(index, smartspawn, packrange, packcoord, ignoreloopprotection, loops))
					// if any of the attempts were successful then flag it as having spawned
					didspawn = true;
			}

			return didspawn;
		}

		// spawn an individual entry by index up to count times
		public bool Spawn(int index, bool smartspawn, int count, byte loops)
		{
			return Spawn(index, smartspawn, count, false, loops);
		}

		// spawn an individual entry by index up to count times
		public bool Spawn(int index, bool smartspawn, int count, bool ignoreloopprotection, byte loops)
		{
			return Spawn(index, smartspawn, count, -1, Point3D.Zero, ignoreloopprotection, loops);
		}

		// spawn an individual entry by spawn object
		public void Spawn(string SpawnObjectTypeName, bool smartspawn, int packrange, Point3D packcoord, byte loops)
		{
			if (m_SpawnObjects == null) return;
			for (var i = 0; i < m_SpawnObjects.Count; i++)
				if (m_SpawnObjects[i].TypeName.ToUpper() == SpawnObjectTypeName.ToUpper())
				{
					if (Spawn(i, smartspawn, packrange, packcoord, loops)) RefreshNextSpawnTime(m_SpawnObjects[i]);
					break;
				}
				else
				{
				}
		}

		// spawn an individual entry by index
		public void Spawn(string SpawnObjectTypeName, bool smartspawn, byte loops)
		{
			Spawn(SpawnObjectTypeName, smartspawn, -1, Point3D.Zero, loops);
		}

		// spawn an individual entry by index
		public bool Spawn(int index, bool smartspawn, int packrange, Point3D packcoord, byte loops)
		{
			return Spawn(index, smartspawn, packrange, packcoord, false, loops);
		}

		// spawn an individual entry by index
		public bool Spawn(int index, bool smartspawn, int packrange, Point3D packcoord, bool ignoreloopprotection,
			byte loops)
		{
			var map = Map;

			// Make sure everything is ok to spawn an object
			if (map == null ||
			    map == Map.Internal ||
			    m_SpawnObjects == null ||
			    m_SpawnObjects.Count == 0 ||
			    index < 0 ||
			    index >= m_SpawnObjects.Count
			   )
				return false;

			// Remove any spawns that don't belong to the spawner any more.
			Defrag(false);

			// Get the spawn object at the required index
			var TheSpawn = m_SpawnObjects[index];

			// Check if the object retrieved is a valid SpawnObject
			if (TheSpawn != null)
			{
				// dont allow an entry to be spawned more than once per tick
				// this protects against runaway recursive looping
				if (TheSpawn.SpawnedThisTick && !ignoreloopprotection) return false;

				// check the nextspawn time to see if it is available
				if (TheSpawn.NextSpawn > DateTime.Now)
					return false;

				var CurrentCreatureMax = TheSpawn.MaxCount;
				var CurrentCreatureCount = TheSpawn.SpawnedObjects.Count;

				// Check that the current object to be spawned has not reached its maximum allowed
				// and make sure that the maximum spawner count has not been exceeded as well
				if (CurrentCreatureCount >= CurrentCreatureMax ||
				    TotalSpawnedObjects >= m_Count)
					return false;

				// check for string substitions
				var substitutedtypeName =
					BaseXmlSpawner.ApplySubstitution(this, this, m_mob_who_triggered, TheSpawn.TypeName);

				// random positioning is the default
				List<SpawnPositionInfo> spawnpositioning = null;

				// require valid surfaces by default
				var requiresurface = true;

				// parse the # function specification for the entry
				while (substitutedtypeName.StartsWith("#"))
				{
					var args = BaseXmlSpawner.ParseSemicolonArgs(substitutedtypeName, 2);

					if (args.Length > 0)
					{
						if (spawnpositioning == null) spawnpositioning = new List<SpawnPositionInfo>();
						// parse any comma args
						var keyvalueargs = BaseXmlSpawner.ParseCommaArgs(args[0], 10);

						if (keyvalueargs.Length > 0)
							switch (keyvalueargs[0])
							{
								case "#NOITEMID":
									spawnpositioning.Add(new SpawnPositionInfo(SpawnPositionType.NoItemID,
										m_mob_who_triggered, keyvalueargs));
									break;
								case "#ITEMID":
									spawnpositioning.Add(new SpawnPositionInfo(SpawnPositionType.ItemID,
										m_mob_who_triggered, keyvalueargs));
									break;
								case "#NOTILES":
									spawnpositioning.Add(new SpawnPositionInfo(SpawnPositionType.NoTiles,
										m_mob_who_triggered, keyvalueargs));
									break;
								case "#TILES":
									spawnpositioning.Add(new SpawnPositionInfo(SpawnPositionType.Tiles,
										m_mob_who_triggered, keyvalueargs));
									break;
								case "#WET":
									spawnpositioning.Add(new SpawnPositionInfo(SpawnPositionType.Wet,
										m_mob_who_triggered, keyvalueargs));
									break;
								case "#XFILL":
									spawnpositioning.Add(new SpawnPositionInfo(SpawnPositionType.RowFill,
										m_mob_who_triggered, keyvalueargs));
									break;
								case "#YFILL":
									spawnpositioning.Add(new SpawnPositionInfo(SpawnPositionType.ColFill,
										m_mob_who_triggered, keyvalueargs));
									break;
								case "#EDGE":
									spawnpositioning.Add(new SpawnPositionInfo(SpawnPositionType.Perimeter,
										m_mob_who_triggered, keyvalueargs));
									break;
								case "#PLAYER":
									spawnpositioning.Add(new SpawnPositionInfo(SpawnPositionType.Player,
										m_mob_who_triggered, keyvalueargs));
									break;
								case "#WAYPOINT":
									spawnpositioning.Add(new SpawnPositionInfo(SpawnPositionType.Waypoint,
										m_mob_who_triggered, keyvalueargs));
									break;
								case "#RELXY":
									spawnpositioning.Add(new SpawnPositionInfo(SpawnPositionType.RelXY,
										m_mob_who_triggered, keyvalueargs));
									break;
								case "#DXY":
									spawnpositioning.Add(new SpawnPositionInfo(SpawnPositionType.DeltaLocation,
										m_mob_who_triggered, keyvalueargs));
									break;
								case "#XY":
									spawnpositioning.Add(new SpawnPositionInfo(SpawnPositionType.Location,
										m_mob_who_triggered, keyvalueargs));
									break;
								case "#CONDITION":
									// test the specified condition string
									// syntax is #CONDITION,proptest
									// reparse with only one arg after the comma, this allows property tests that use commas as well
									var ckeyvalueargs = BaseXmlSpawner.ParseCommaArgs(args[0], 2);
									if (ckeyvalueargs.Length > 1)
									{
										// dont spawn if it fails the test
										if (!BaseXmlSpawner.CheckPropertyString(this, this, ckeyvalueargs[1],
											    m_mob_who_triggered, out status_str)) return false;
									}
									else
										status_str = "invalid #CONDITION specification: " + args[0];

									break;
								default:
									status_str = "invalid # specification: " + args[0];
									break;
							}
					}

					// get the rest of the spawn entry
					if (args.Length > 1)
						substitutedtypeName = args[1].Trim();
					else
						substitutedtypeName = String.Empty;
				}


				if (substitutedtypeName.StartsWith("*"))
				{
					requiresurface = false;
					substitutedtypeName = substitutedtypeName.TrimStart('*');
				}

				TheSpawn.RequireSurface = requiresurface;

				var typeName = BaseXmlSpawner.ParseObjectType(substitutedtypeName);

				if (BaseXmlSpawner.IsTypeOrItemKeyword(typeName))
				{
					string status_str = null;

					var completedtypespawn = BaseXmlSpawner.SpawnTypeKeyword(this, TheSpawn, typeName,
						substitutedtypeName, requiresurface, spawnpositioning,
						m_mob_who_triggered, Location, Map, new XmlGumpCallback(SpawnerGumpCallback), out status_str,
						loops);

					if (status_str != null) this.status_str = status_str;

					if (completedtypespawn)
					{
						// successfully spawned the keyword
						// note that returning true means that Spawn will assume that it worked and will not try to respawn something else
						// added the duration timer that begins on spawning
						DoTimer2(m_Duration);

						InvalidateProperties();

						return true;
					}
					else
						return false;
				}
				else
				{
					// its a regular type descriptor so find out what it is
					var type = SpawnerType.GetType(typeName);

					// dont try to spawn invalid types, or Mobile type spawns in containers
					if (type != null &&
					    !(Parent != null && (type == typeof(Mobile) || type.IsSubclassOf(typeof(Mobile)))))
					{
						var arglist = BaseXmlSpawner.ParseString(substitutedtypeName, 3, "/");

						var o = CreateObject(type, arglist[0]);

						if (o == null)
						{
							status_str = "invalid type specification: " + arglist[0];
							return true;
						}

						try
						{
							if (o is Mobile)
							{
								// if this is in any container such as a pack the xyz values are invalid as map coords so dont spawn the mob
								if (Parent is Container)
								{
									((Mobile)o).Delete();

									return true;
								}

								var m = (Mobile)o;

								// add the mobile to the spawned list
								TheSpawn.SpawnedObjects.Add(m);

								m.Spawner = this;

								Point3D loc;

								/*
								if( ( m is BaseVendor ) &&
									( this.Map.CanFit( this.Location, SpawnFitSize, true, false ) == true ) )
								{
									loc = this.Location;
								} 
								else
								{
									loc = GetSpawnPosition(requiresurface);
								}
								*/
								loc = GetSpawnPosition(requiresurface, packrange, packcoord, spawnpositioning, m);

								if (!smartspawn) m.OnBeforeSpawn(loc, map);

								m.MoveToWorld(loc, map);

								if (m is BaseCreature)
								{
									var c = (BaseCreature)m;
									c.RangeHome = m_HomeRange;
									c.CurrentWayPoint = m_WayPoint;

									if (m_Team > 0)
										c.Team = m_Team;

									// Check if this spawner uses absolute (from spawnER location)
									// or relative (from spawnED location) as the mobiles home point
									if (m_HomeRangeIsRelative == true)
										c.Home = m.Location; // Mobiles spawned location is the home point
									else
										c.Home = Location; // Spawners location is the home point
								}

								// if the object has an OnSpawned method, then invoke it
								if (!smartspawn) m.OnAfterSpawn();

								// apply the parsed arguments from the typestring using setcommand
								// be sure to do this after setting map and location so that errors dont place the mob on the internal map
								string status_str;

								BaseXmlSpawner.ApplyObjectStringProperties(this, substitutedtypeName, m,
									m_mob_who_triggered, this, out status_str);

								// if the object has an OnAfterSpawnAndModify method, then invoke it
								//BaseXmlSpawner.InvokeOnAfterSpawnAndModify(o);

								if (status_str != null) this.status_str = status_str;

								InvalidateProperties();

								// added the duration timer that begins on spawning
								DoTimer2(m_Duration);

								return true;
							}
							else if (o is Item)
							{
								var item = (Item)o;

								string status_str;

								BaseXmlSpawner.AddSpawnItem(this, TheSpawn, item, Location, map, m_mob_who_triggered,
									requiresurface, spawnpositioning, substitutedtypeName, smartspawn, out status_str);

								if (status_str != null) this.status_str = status_str;

								InvalidateProperties();

								// added the duration timer that begins on spawning
								DoTimer2(m_Duration);

								return true;
							}
						}
						catch (Exception ex) { Console.WriteLine("When spawning {0}, {1}", o, ex); }
					}
					else
					{
						status_str = "invalid type specification: " + typeName;
						return true;
					}
				}
			}

			return false;
		}

		public bool SpawnSubGroup(int sgroup, byte loops)
		{
			return SpawnSubGroup(sgroup, false, loops);
		}

		public bool SpawnSubGroup(int sgroup, bool smartspawn, byte loops)
		{
			return SpawnSubGroup(sgroup, false, false, loops);
		}

		public bool SpawnSubGroup(int sgroup, bool smartspawn, bool ignoreloopprotection, byte loops)
		{
			if (m_SpawnObjects == null) return false;

			if (sgroup >= 0)
			{
				var didspawn = false;
				var packcoord = Point3D.Zero;

				for (var j = 0; j < m_SpawnObjects.Count; j++)
				{
					var so = m_SpawnObjects[j];

					if (so != null && so.SubGroup == sgroup)
					{
						// find the first subgroup spawn to determine the packspawning reference coordinates
						if (so.PackRange >= 0 && packcoord == Point3D.Zero) packcoord = GetPackCoord(sgroup);

						// get the SpawnsPerTick count and spawn up to that number
						var success = Spawn(j, smartspawn, so.SpawnsPerTick, so.PackRange, packcoord,
							ignoreloopprotection, loops);

						if (success) didspawn = true;

						if (success && !smartspawn)
							RefreshNextSpawnTime(so);
					}
				}

				// success if any of the subgroup spawned
				if (didspawn) return true;
			}

			return false;
		}

		#endregion

		#region Spawn support methods

		public Point3D GetPackCoord(int sgroup)
		{
			var packcoord = Point3D.Zero;

			for (var j = 0; j < m_SpawnObjects.Count; j++)
			{
				var so = m_SpawnObjects[j];

				if (so != null && so.SubGroup == sgroup && so.SpawnedObjects.Count > 0 && so.PackRange >= 0)
					// if pack spawning is enabled for this subgroup, then get the
					// the origin for pack spawning using the first existing pack spawn
					// in the subgroup

					for (var i = 0; i < so.SpawnedObjects.Count; ++i)
					{
						var o = so.SpawnedObjects[i];
						if (o is Item)
							return ((Item)o).Location;
						else if (o is Mobile) return ((Mobile)o).Location;
					}
			}

			return Point3D.Zero;
		}


		//used by the reset button in the gump
		public void ResetAllFlags()
		{
			m_proximityActivated = false;
			m_ExternalTrigger = false;
			m_durActivated = false;
			m_refractActivated = false;
			m_mob_who_triggered = null;
			m_skill_that_triggered = XmlSpawnerSkillCheck.RegisteredSkill.Invalid;
			m_killcount = 0;
			m_GumpState = null;
			FreeRun = false;
		}

		public bool BringHome
		{
			set
			{
				if (value) BringToHome();
			}
		}

		public void BringToHome()
		{
			if (m_SpawnObjects == null) return;
			Defrag(false);

			foreach (var so in m_SpawnObjects)
				for (var i = 0; i < so.SpawnedObjects.Count; ++i)
				{
					var o = so.SpawnedObjects[i];

					if (o is Mobile)
					{
						var m = (Mobile)o;

						m.Map = Map;
						m.Location = new Point3D(Location);
					}
					else if (o is Item)
					{
						var item = (Item)o;

						item.MoveToWorld(Location, Map);
					}
				}
		}

		public bool CheckRegionAssignment
		{
			get => false;
			set
			{
				if (value == true)
					// see if a region definition needs updating
					if (m_Region == null && m_RegionName != null && RegionName != String.Empty)
					{
						RegionName = RegionName;

						if (SpawnRegion != null)
							// clear the status if successful
							status_str = null;
					}
			}
		}

		public void Start()
		{
			if (m_Running == false)
				if (m_SpawnObjects != null && m_SpawnObjects.Count > 0)
				{
					m_Running = true;
					DoTimer();
				}
		}


		public void Stop()
		{
			if (m_Running == true)
			{
				// turn off all timers
				if (m_Timer != null)
					m_Timer.Stop();
				if (m_DurTimer != null)
					m_DurTimer.Stop();
				if (m_RefractoryTimer != null)
					m_RefractoryTimer.Stop();
				m_Running = false;
				m_proximityActivated = false;
				m_ExternalTrigger = false;
				m_mob_who_triggered = null;
				m_skill_that_triggered = XmlSpawnerSkillCheck.RegisteredSkill.Invalid;
			}
		}

		public void Reset()
		{
			Stop();
			// reset the protection against runaway looping
			ClearSpawnedThisTick = true;
			RemoveSpawnObjects();
			ClearTags(true);
			ResetAllFlags();
			status_str = "";
			m_killcount = 0;
			OnHold = false;
			mostRecentSpawnPosition = Point3D.Zero;
			spawnPositionWayTable = null;
			// dont advance before the next spawn
			HoldSequence = true;
			IsInactivated = false;
			ResetSequential();
		}

		public bool Respawn()
		{
			inrespawn = true;
			IsInactivated = false;

			// reset the protection against runaway looping
			ClearSpawnedThisTick = true;

			// Delete all currently spawned objects
			RemoveSpawnObjects();

			// added the explicit start.  Previously it relied on the automatic start that occurred when the spawnobject list was updated.
			Start();

			ResetNextSpawnTimes();

			// Respawn all objects up to the spawners current maximum allowed
			// note that by default, for proximity sensing, the spawner will only trigger once, but for respawns allow them all
			var keepProximityActivated = m_proximityActivated;

			var triedtospawn = false;

			// attempt to spawn up to the MaxCount of the spawner
			for (var x = 0; x < m_Count; x++)
			{
				triedtospawn = Spawn(false, 0);

				if (x < m_Count - 1 || OnHold) m_proximityActivated = keepProximityActivated;
			}

			if (!FreeRun)
			{
				m_mob_who_triggered = null;
				m_skill_that_triggered = XmlSpawnerSkillCheck.RegisteredSkill.Invalid;
			}

			ClearTags(true);


			inrespawn = false;

			return triedtospawn;
		}

		// used to optimize smartspawning use of hasholdsmartspawning
		public void SmartRespawn()
		{
			inrespawn = true;
			IsInactivated = false;

			// reset the protection against runaway looping
			ClearSpawnedThisTick = true;

			// Delete all currently spawned objects
			SmartRemoveSpawnObjects();

			// added the explicit start.  Previously it relied on the automatic start that occurred when the spawnobject list was updated.
			Start();

			ResetNextSpawnTimes();

			// Respawn all objects up to the spawners current maximum allowed
			// note that by default, for proximity sensing, the spawner will only trigger once, but for respawns allow them all
			var keepProximityActivated = m_proximityActivated;

			// attempt to spawn up to the MaxCount of the spawner
			for (var x = 0; x < m_Count; x++)
			{
				Spawn(true, 0);

				if (x < m_Count - 1 || OnHold) m_proximityActivated = keepProximityActivated;
			}

			if (!FreeRun)
			{
				m_mob_who_triggered = null;
				m_skill_that_triggered = XmlSpawnerSkillCheck.RegisteredSkill.Invalid;
			}

			ClearTags(true);

			inrespawn = false;
		}


		public void SortSpawns()
		{
			if (m_SpawnObjects == null) return;

			// establish the entry order
			var count = 0;

			foreach (var so in m_SpawnObjects) so.EntryOrder = count++;

			m_SpawnObjects.Sort(new SubgroupSorter());
		}

		private class SubgroupSorter : IComparer<SpawnObject>
		{
			public int Compare(SpawnObject a, SpawnObject b)
			{
				if (a.SubGroup == b.SubGroup)
					// use the entry order as the secondary sort factor
					return a.EntryOrder - b.EntryOrder;
				else
					return a.SubGroup - b.SubGroup;
			}
		}


		public static SpawnObject GetSpawnObject(XmlSpawner spawner, int sgroup)
		{
			if (spawner == null || spawner.m_SpawnObjects == null) return null;
			for (var i = 0; i < spawner.m_SpawnObjects.Count; i++)
				// find the first entry with matching subgroup id
				if (spawner.m_SpawnObjects[i].SubGroup == sgroup)
					return spawner.m_SpawnObjects[i];
			return null;
		}


		public static object GetSpawned(XmlSpawner spawner, int sgroup)
		{
			if (spawner == null || spawner.m_SpawnObjects == null) return null;
			for (var i = 0; i < spawner.m_SpawnObjects.Count; i++)
				// find the first entry with matching subgroup id
				if (spawner.m_SpawnObjects[i].SubGroup == sgroup)
					// find the first spawned object in the entry
					if (spawner.m_SpawnObjects[i].SpawnedObjects.Count > 0)
						return spawner.m_SpawnObjects[i].SpawnedObjects[0];
			return null;
		}

		public static List<object> GetSpawnedList(XmlSpawner spawner, int sgroup)
		{
			var newlist = new List<object>();

			if (spawner == null || spawner.m_SpawnObjects == null) return null;
			for (var i = 0; i < spawner.m_SpawnObjects.Count; i++)
				// find the first entry with matching subgroup id
				if (spawner.m_SpawnObjects[i].SubGroup == sgroup)
					// find the first spawned object in the entry

					if (spawner.m_SpawnObjects[i].SpawnedObjects.Count > 0)
						for (var j = 0; j < spawner.m_SpawnObjects[i].SpawnedObjects.Count; j++)
							newlist.Add(spawner.m_SpawnObjects[i].SpawnedObjects[j]);
			return newlist;
		}


		public bool HasSubGroups()
		{
			if (m_SpawnObjects == null) return false;

			for (var j = 0; j < m_SpawnObjects.Count; j++)
				if (m_SpawnObjects[j].SubGroup > 0)
					return true;

			return false;
		}

		private void ResetProximityActivated()
		{
			// dont reset triggering if free run mode has been selected
			if (!FreeRun) m_proximityActivated = false;
		}

		private void RefreshNextSpawnTimes()
		{
			if (m_SpawnObjects != null && m_SpawnObjects.Count > 0)
				for (var i = 0; i < m_SpawnObjects.Count; i++)
				{
					var so = m_SpawnObjects[i];

					RefreshNextSpawnTime(so);
				}
		}

		public bool HasIndividualSpawnTimes()
		{
			if (m_SpawnObjects != null && m_SpawnObjects.Count > 0)
				for (var i = 0; i < m_SpawnObjects.Count; i++)
				{
					var so = m_SpawnObjects[i];

					if (so.MinDelay != -1 || so.MaxDelay != -1) return true;
				}

			return false;
		}

		private void ResetNextSpawnTimes()
		{
			if (m_SpawnObjects != null && m_SpawnObjects.Count > 0)
				for (var i = 0; i < m_SpawnObjects.Count; i++)
				{
					var so = m_SpawnObjects[i];

					so.NextSpawn = DateTime.Now;
				}
		}

		public void RefreshNextSpawnTime(SpawnObject so)
		{
			if (so == null) return;

			var mind = (int)(so.MinDelay * 60);
			var maxd = (int)(so.MaxDelay * 60);
			if (mind < 0 || maxd < 0)
				so.NextSpawn = DateTime.Now;
			else
			{
				var delay = TimeSpan.FromSeconds(Utility.RandomMinMax(mind, maxd));

				so.NextSpawn = DateTime.Now + delay;
			}
		}


		public static bool IsValidMapLocation(int X, int Y, Map map)
		{
			if (map == null || map == Map.Internal) return false;
			// check the location relative to the current map to make sure it is valid
			if (X < 0 || X > map.Width || Y < 0 || Y > map.Height) return false;
			return true;
		}

		public static bool IsValidMapLocation(Point3D location, Map map)
		{
			if (map == null || map == Map.Internal) return false;
			// check the location relative to the current map to make sure it is valid
			if (location.X < 0 || location.X > map.Width || location.Y < 0 || location.Y > map.Height) return false;
			return true;
		}

		public static bool IsValidMapLocation(Point2D location, Map map)
		{
			if (map == null || map == Map.Internal) return false;
			// check the location relative to the current map to make sure it is valid
			if (location.X < 0 || location.X > map.Width || location.Y < 0 || location.Y > map.Height) return false;
			return true;
		}

		private static WayPoint GetWaypoint(string waypointstr)
		{
			WayPoint waypoint = null;

			// try parsing the waypoint name to determine the waypoint. object syntax is "SERIAL,sernumber" or "waypointname"
			if (waypointstr != null && waypointstr.Length > 0)
			{
				var wayargs = BaseXmlSpawner.ParseString(waypointstr, 2, ",");
				if (wayargs != null && wayargs.Length > 0)
				{
					// is this a SERIAL specification?
					if (wayargs[0] == "SERIAL")
					{
						// look it up by serial
						if (wayargs.Length > 1)
						{
							var sernum = -1;
							try { sernum = (int)Convert.ToUInt64(wayargs[1].Substring(2), 16); }
							catch { }

							if (sernum > -1)
							{
								var e = World.FindEntity(new Serial(sernum));

								if (e is WayPoint)
									waypoint = e as WayPoint;
							}
						}
						else
						{
							// improper serial format
						}
					}
					else
					{
						// just look it up by name
						var wayitem = BaseXmlSpawner.FindItemByName(null, wayargs[0], "WayPoint");
						if (wayitem is WayPoint) waypoint = wayitem as WayPoint;
					}
				}
			}

			return waypoint;
		}

		private static bool HasTileSurface(Map map, int X, int Y, int Z)
		{
			if (map == null) return false;

			var tiles = map.Tiles.GetStaticTiles(X, Y, true);
			//List<Server.Tile> tiles = map.GetTilesAt(new Point2D(X, Y), true, true, true);

			if (tiles == null) return false;

			// go through the tiles and see if any are at the Z location
			foreach (object o in tiles)
				if (o is StaticTile)
				{
					var i = (StaticTile)o;

					if (i.Z + i.Height == Z) return true;
				}

			return false;
		}

		private bool CheckHoldSmartSpawning(object o)
		{
			if (o == null) return false;

			// try looking this up in the lookup table
			if (holdSmartSpawningHash == null) holdSmartSpawningHash = new Dictionary<Type, PropertyInfo>();
			PropertyInfo prop = null;

			if (!holdSmartSpawningHash.TryGetValue(o.GetType(), out prop))
			{
				prop = o.GetType().GetProperty("HoldSmartSpawning");
				// check to make sure the HoldSmartSpawning property for this object has the right type
				if (prop != null && (!prop.CanRead || prop.PropertyType != typeof(bool))) prop = null;

				holdSmartSpawningHash[o.GetType()] = prop;
			}
			//else
			//{
			// look it up in the hash table
			//prop = holdSmartSpawningHash[o.GetType()];
			//}

			if (prop != null)
				try
				{
					return (bool)prop.GetValue(o, null);
				}
				catch { }

			return false;
		}

		public bool HasHoldSmartSpawning
		{
			get
			{
				// go through the spawn lists
				foreach (var so in m_SpawnObjects)
					for (var x = 0; x < so.SpawnedObjects.Count; x++)
					{
						var o = so.SpawnedObjects[x];
						if (CheckHoldSmartSpawning(o)) return true;
					}

				return false;
			}
		}

		// if a non-null mob argument is passed, then check the canswim and cantwalk props to determine valid placement
		public bool CanFit(int x, int y, int z, int height, bool checkBlocksFit, bool checkMobiles, bool requireSurface,
			Mobile mob)
		{
			var map = Map;

			if (DebugThis) Console.WriteLine("CanFit mob {0}, map={1}", mob, map);
			if (map == null || map == Map.Internal)
				return false;

			if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
				return false;

			var hasSurface = false;
			var checkmob = false;
			var canswim = false;
			var cantwalk = false;

			if (mob != null)
			{
				checkmob = true;
				canswim = mob.CanSwim;
				cantwalk = mob.CantWalk;
			}

			if (DebugThis)
				Console.WriteLine("fitting mob {0} checkmob={1} swim={2} walk={3}", mob, checkmob, canswim, cantwalk);
			var lt = map.Tiles.GetLandTile(x, y);
			int lowZ = 0, avgZ = 0, topZ = 0;

			bool surface, impassable;
			var wet = false;

			map.GetAverageZ(x, y, ref lowZ, ref avgZ, ref topZ);
			var landFlags = TileData.LandTable[lt.ID & TileData.MaxLandValue].Flags;

			if (DebugThis)
				Console.WriteLine("landtile at {0},{1},{2} lowZ={3} avgZ={4} topZ={5}", x, y, z, lowZ, avgZ, topZ);

			impassable = (landFlags & TileFlag.Impassable) != 0;
			if (checkmob)
			{
				wet = (landFlags & TileFlag.Wet) != 0;
				// dont allow wateronly creatures on land
				if (cantwalk && !wet)
					impassable = true;
				// allow water creatures on water
				if (canswim && wet) impassable = false;
			}


			if (impassable && avgZ > z && z + height > lowZ)
				return false;
			else if (!impassable && z == avgZ && !lt.Ignored)
				hasSurface = true;

			if (DebugThis)
				Console.WriteLine("landtile at {0},{1},{2} wet={3} impassable={4} hassurface={5}", x, y, z, wet,
					impassable, hasSurface);

			var staticTiles = map.Tiles.GetStaticTiles(x, y, true);


			for (var i = 0; i < staticTiles.Length; ++i)
			{
				var id = TileData.ItemTable[staticTiles[i].ID & TileData.MaxItemValue];
				surface = id.Surface;
				impassable = id.Impassable;
				if (checkmob)
				{
					wet = (id.Flags & TileFlag.Wet) != 0;
					// dont allow wateronly creatures on land
					if (cantwalk && !wet)
						impassable = true;
					// allow water creatures on water
					if (canswim && wet)
					{
						surface = true;
						impassable = false;
					}
				}

				if ((surface || impassable) && staticTiles[i].Z + id.CalcHeight > z && z + height > staticTiles[i].Z)
					return false;
				else if (surface && !impassable && z == staticTiles[i].Z + id.CalcHeight)
					hasSurface = true;
			}

			if (DebugThis) Console.WriteLine("statics hassurface={0}", hasSurface);

			var sector = map.GetSector(x, y);

			foreach (var item in sector.Items)
				if (item.ItemID < 0x4000 && item.AtWorldPoint(x, y))
				{
					var id = item.ItemData;
					surface = id.Surface;
					impassable = id.Impassable;
					if (checkmob)
					{
						wet = (id.Flags & TileFlag.Wet) != 0;
						// dont allow wateronly creatures on land
						if (cantwalk && !wet)
							impassable = true;
						// allow water creatures on water
						if (canswim && wet)
						{
							surface = true;
							impassable = false;
						}
					}

					if ((surface || impassable || checkBlocksFit && item.BlocksFit) && item.Z + id.CalcHeight > z &&
					    z + height > item.Z)
						return false;
					else if (surface && !impassable && !item.Movable && z == item.Z + id.CalcHeight)
						hasSurface = true;
				}

			if (DebugThis) Console.WriteLine("items hassurface={0}", hasSurface);

			if (checkMobiles)
				foreach (var m in sector.Mobiles)
					if (m.Location.X == x && m.Location.Y == y && (m.AccessLevel == AccessLevel.Player || !m.Hidden))
						if (m.Z + 16 > z && z + height > m.Z)
							return false;

			if (DebugThis) Console.WriteLine("return requiresurface={0} hassurface={1}", requireSurface, hasSurface);

			return !requireSurface || hasSurface;
		}

		public bool CanSpawnMobile(int x, int y, int z, Mobile mob)
		{
			if (DebugThis) Console.WriteLine("CanSpawnMobile mob {0}", mob);
			if (!Region.Find(new Point3D(x, y, z), Map).AllowSpawn())
				return false;

			return CanFit(x, y, z, 16, false, true, true, mob);
		}

		public bool HasRegionPoints(Region r)
		{
			if (r != null && r.Area.Length > 0) return true;
			else
				return false;
		}

		public Rectangle2D SpawnerBounds => new Rectangle2D(m_X, m_Y, m_Width + 1, m_Height + 1);

		private void FindTileLocations(ref List<Point3D> locations, Map map, int startx, int starty, int width,
			int height, List<int> includetilelist, List<int> excludetilelist, TileFlag tileflag, bool checkitems,
			int spawnerZ)
		{
			if (width < 0 || height < 0 || map == null) return;

			if (locations == null) locations = new List<Point3D>();

			bool includetile;
			bool excludetile;
			for (var x = startx; x <= startx + width; x++)
			for (var y = starty; y <= starty + height; y++)
			{
				var allok = false;
				var p = Point3D.Zero;
				// go through all of the tiles at the location and find those that are in the allowed tiles list
				var ltile = map.Tiles.GetLandTile(x, y);
				var lflags = TileData.LandTable[ltile.ID & TileData.MaxLandValue].Flags;

				// check the land tile
				if (includetilelist != null && includetilelist.Count > 0)
					includetile = includetilelist.Contains(ltile.ID & TileData.MaxLandValue);
				else
					includetile = true;

				// non-excluded tiles must also be passable
				if (excludetilelist != null && excludetilelist.Count > 0)
					// also require the tile to be passable
					excludetile = (lflags & TileFlag.Impassable) != 0 ||
					              excludetilelist.Contains(ltile.ID & TileData.MaxLandValue);
				else
					excludetile = false;

				if (includetile && !excludetile && (lflags & tileflag) == tileflag)
				{
					//Console.WriteLine("found landtile {0}/{1} at {2},{3},{4}", ltile.ID, ltile.ID & 0x3fff, x, y, ltile.Z + ltile.Height);
					p = new Point3D(x, y, ltile.Z + ltile.Height);
					allok = true;
					//locations.Add(new Point3D(x, y, ltile.Z + ltile.Height));
					//continue;
				}

				var statictiles = map.Tiles.GetStaticTiles(x, y, true);

				// check the static tiles
				for (var i = 0; i < statictiles.Length; ++i)
				{
					var stile = statictiles[i];
					var sflags = TileData.ItemTable[stile.ID & TileData.MaxItemValue].Flags;

					if (includetilelist != null && includetilelist.Count > 0)
						includetile = includetilelist.Contains(stile.ID & TileData.MaxItemValue);
					else
						includetile = true;

					// non-excluded tiles must also be passable
					if (excludetilelist != null && excludetilelist.Count > 0)
						excludetile = (sflags & TileFlag.Impassable) != 0 ||
						              excludetilelist.Contains(stile.ID & TileData.MaxItemValue);
					else
						excludetile = false;

					if (includetile && !excludetile && (sflags & tileflag) == tileflag)
					{
						//Console.WriteLine("found statictile {0}/{1} at {2},{3},{4}", stile.ID, stile.ID & 0x3fff, x, y, stile.Z + stile.Height);
						if (p == Point3D.Zero)
							p = new Point3D(x, y, stile.Z + stile.Height);
						else if (!allok && p.Z - spawnerZ > Math.Abs(stile.Z - spawnerZ))
							p = new Point3D(x, y, stile.Z + stile.Height);
						else if (Math.Abs(ltile.Z - spawnerZ) >
						         Math.Abs(stile.Z - spawnerZ)) //maggiore distanza rispetto allo statico dallo spawner
							p = new Point3D(x, y, stile.Z + stile.Height);
						allok = true;
						//locations.Add(new Point3D(x, y, stile.Z + stile.Height));
						//break;
					}
				}

				if (checkitems)
				{
					IPooledEnumerable itemslist = map.GetItemsInRange(new Point3D(x, y, 0), 0);

					// check the itemsid
					foreach (Item i in itemslist)
					{
						if (i.ItemData.Impassable)
							excludetile = true;

						var iflags = TileData.ItemTable[i.ItemID & TileData.MaxItemValue].Flags;
						if (includetilelist != null && includetilelist.Count > 0)
							includetile = includetilelist.Contains(i.ItemID & TileData.MaxItemValue);
						else
							includetile = true;

						if (excludetilelist != null && excludetilelist.Count > 0)
							excludetile = excludetilelist.Contains(i.ItemID & TileData.MaxItemValue);
						else
							excludetile = false;

						if (includetile && !excludetile && (iflags & tileflag) == tileflag)
						{
							//prende precedenza su tutto il resto!
							p = new Point3D(x, y, i.Z + i.ItemData.Height);
							allok = true;
							//locations.Add(new Point3D(x, y, i.Z + i.ItemData.Height));
							//break;
						}
					}

					itemslist.Free();
				}

				if (allok && !excludetile)
					locations.Add(p);
			}
		}

		private void FindRegionTileLocations(ref List<Point3D> locations, Region r, List<int> includetilelist,
			List<int> excludetilelist, TileFlag tileflag, bool checkitems, int spawnerZ)
		{
			if (r == null || r.Area == null) return;

			var count = r.Area.Length;

			if (locations == null) locations = new List<Point3D>();

			// calculate fields of all rectangles (for probability calculating)
			for (var n = 0; n < count; n++)
			{
				var ra = r.Area[n];
				var sx = ra.Start.X;
				var sy = ra.Start.Y;
				var w = ra.Width;
				var h = ra.Height;

				// find all of the valid tile locations in the area
				FindTileLocations(ref locations, r.Map, sx, sy, w, h, includetilelist, excludetilelist, tileflag,
					checkitems, spawnerZ);
			}
		}

		// 2004.02.08 :: Omega Red
		public Point2D GetRandomRegionPoint(Region r)
		{
			var count = r.Area.Length;

			var FieldArray = new int[count];
			var total = 0;

			// calculate fields of all rectangles (for probability calculating)
			for (var i = 0; i < count; i++)
			{
				var ra = r.Area[i];
				total += FieldArray[i] = ra.Width * ra.Height;
			}

			var sum = 0;
			var rnd = 0;
			if (total > 0)
				rnd = Utility.Random(total);
			var x = 0;
			var y = 0;
			for (var i = 0; i < count; i++)
			{
				sum += FieldArray[i];
				if (sum > rnd)
				{
					var r3d = r.Area[i];
					if (r3d.Width >= 0)
						x = r3d.Start.X + Utility.Random(r3d.Width);
					if (r3d.Height >= 0)
						y = r3d.Start.Y + Utility.Random(r3d.Height);
					break;
				}
			}

			return new Point2D(x, y);
		}

		// used for getting non-mobile spawn positions
		public Point3D GetSpawnPosition(bool requiresurface)
		{
			// no pack spawning
			return GetSpawnPosition(requiresurface, -1, Point3D.Zero, null, null);
		}

		// used for getting mobile spawn positions
		public Point3D GetSpawnPosition(bool requiresurface, Mobile mob)
		{
			// no pack spawning
			return GetSpawnPosition(requiresurface, -1, Point3D.Zero, null, mob);
		}

		// used for getting non-mobile spawn positions
		public Point3D GetSpawnPosition(bool requiresurface, int packrange, Point3D packcoord,
			List<SpawnPositionInfo> spawnpositioning)
		{
			return GetSpawnPosition(requiresurface, packrange, packcoord, spawnpositioning, null);
		}

		public Point3D GetSpawnPosition(bool requiresurface, int packrange, Point3D packcoord,
			List<SpawnPositionInfo> spawnpositioning, Mobile mob)
		{
			var map = Map;

			if (map == null)
				return Location;

			// random positioning by default
			var positioning = SpawnPositionType.Random;
			Mobile trigmob = null;
			string[] positionargs = null;
			List<int> includetilelist = null;
			List<int> excludetilelist = null;
			var checkitems = false;
			// restrictions on tile flags
			var tileflag = TileFlag.None;
			List<Point3D> locations = null;

			var fillinc = 1;
			var positionrange = 0;
			string prefix = null;
			List<Item> WayList = null;
			var xinc = 0;
			var yinc = 0;
			var zinc = 0;
			if (spawnpositioning != null)
				foreach (var s in spawnpositioning)
				{
					if (s == null) continue;

					trigmob = s.trigMob;
					positionargs = s.positionArgs;

					// parse the possible args to the spawn position control keywords
					switch (s.positionType)
					{
						case SpawnPositionType.Wet:
							// syntax Wet
							// find all of the wet tiles
							tileflag |= TileFlag.Wet;
							requiresurface = false;
							break;
						case SpawnPositionType.ItemID:
							checkitems = true;
							goto case SpawnPositionType.Tiles;
						case SpawnPositionType.NoItemID:
							checkitems = true;
							goto case SpawnPositionType.NoTiles;
						case SpawnPositionType.Tiles:
						{
							// syntax Tiles,start[,end]
							// get the tiles in the range
							requiresurface = false;
							var start = -1;
							var end = -1;
							if (positionargs != null && positionargs.Length > 1)
								try
								{
									start = Int32.Parse(positionargs[1]);
								}
								catch { }

							if (positionargs != null && positionargs.Length > 2)
								try
								{
									end = Int32.Parse(positionargs[2]);
								}
								catch { }

							if (includetilelist == null) includetilelist = new List<int>();

							// add the tiles to the list
							if (start > -1 && end < 0)
								includetilelist.Add(start);
							else if (start > -1 && end > -1)
								for (var j = start; j <= end; j++)
									includetilelist.Add(j);
							break;
						}
						case SpawnPositionType.NoTiles:
						{
							// syntax Tiles,start[,end]
							// get the tiles in the range
							requiresurface = false;
							var start = -1;
							var end = -1;
							if (positionargs != null && positionargs.Length > 1)
								try
								{
									start = Int32.Parse(positionargs[1]);
								}
								catch { }

							if (positionargs != null && positionargs.Length > 2)
								try
								{
									end = Int32.Parse(positionargs[2]);
								}
								catch { }

							if (excludetilelist == null) excludetilelist = new List<int>();

							// add the tiles to the list
							if (start > -1 && end < 0)
								excludetilelist.Add(start);
							else if (start > -1 && end > -1)
								for (var j = start; j <= end; j++)
									excludetilelist.Add(j);
							break;
						}
						case SpawnPositionType.RowFill:
						case SpawnPositionType.ColFill:
						case SpawnPositionType.Perimeter:
							// syntax XFILL[,inc]
							// syntax YFILL[,inc]
							// syntax EDGE[,inc]
							positioning = s.positionType;
							if (positionargs != null && positionargs.Length > 1)
								try
								{
									fillinc = Int32.Parse(positionargs[1]);
								}
								catch { }

							break;
						case SpawnPositionType.RelXY:
						case SpawnPositionType.DeltaLocation:
						case SpawnPositionType.Location:
							// syntax RELXY,xinc,yinc[,zinc]
							// syntax XY,x,y[,z]
							// syntax DXY,dx,dy[,dz]
							positioning = s.positionType;
							if (positionargs != null && positionargs.Length > 2)
								try
								{
									xinc = Int32.Parse(positionargs[1]);
									yinc = Int32.Parse(positionargs[2]);
								}
								catch { }

							if (positionargs != null && positionargs.Length > 3)
								try
								{
									zinc = Int32.Parse(positionargs[3]);
								}
								catch { }

							break;
						case SpawnPositionType.Waypoint:
							// syntax WAYPOINT,prefix[,range]
							positioning = s.positionType;
							if (positionargs != null && positionargs.Length > 1) prefix = positionargs[1];

							if (positionargs != null && positionargs.Length > 2)
								try
								{
									positionrange = Int32.Parse(positionargs[2]);
								}
								catch { }

							// find a list of items that match the waypoint prefix
							if (prefix != null)
							{
								// see if there is an existing hashtable for the waypoint lists
								if (spawnPositionWayTable == null)
									spawnPositionWayTable = new Dictionary<string, List<Item>>();

								// try to find the waypoint list in the local table
								//WayList = spawnPositionWayTable[prefix];

								// no existing list so create a new one
								if (!spawnPositionWayTable.TryGetValue(prefix, out WayList) || WayList == null)
								{
									WayList = new List<Item>();

									foreach (var i in World.Items.Values)
										if (i is WayPoint && !String.IsNullOrEmpty(i.Name) && i.Map == Map &&
										    i.Name == prefix)
											// add it to the list of items
											WayList.Add(i);
									// add the new list to the local table
									spawnPositionWayTable[prefix] = WayList;
								}
							}

							break;
						case SpawnPositionType.Player:
							// syntax PLAYER[,range]
							positioning = s.positionType;
							if (positionargs != null && positionargs.Length > 1)
								try
								{
									positionrange = Int32.Parse(positionargs[1]);
								}
								catch { }

							break;
					}
				}

			// precalculate tile locations if they have been specified
			if (includetilelist != null || excludetilelist != null || tileflag != TileFlag.None)
			{
				if (m_Region != null && HasRegionPoints(m_Region))
					FindRegionTileLocations(ref locations, m_Region, includetilelist, excludetilelist, tileflag,
						checkitems, Z);
				else if (positioning == SpawnPositionType.Random)
					FindTileLocations(ref locations, Map, m_X, m_Y, m_Width, m_Height, includetilelist, excludetilelist,
						tileflag, checkitems, Z);
			}

			// Try 10 times to find a Spawnable location.
			// trace profiling indicates that this is a major bottleneck
			for (var i = 0; i < 10; i++)
			{
				var x = X;
				var y = Y;
				var z = Z;

				var defaultZ = Z;
				if (packrange >= 0 && packcoord != Point3D.Zero) defaultZ = packcoord.Z;

				if (packrange >= 0 && packcoord != Point3D.Zero)
				{
					// find a random coord relative to the packcoord
					x = packcoord.X - packrange + Utility.Random(packrange * 2 + 1);
					y = packcoord.Y - packrange + Utility.Random(packrange * 2 + 1);
				}
				else if (m_Region != null && HasRegionPoints(m_Region)) // 2004.02.08 :: Omega Red
				{
					// if region spawning is selected then use that to find an x,y loc instead of the spawn box


					if (includetilelist != null || excludetilelist != null || tileflag != TileFlag.None)
					{
						// use the precalculated tile locations
						if (locations != null && locations.Count > 0)
						{
							var p = locations[Utility.Random(locations.Count)];
							x = p.X;
							y = p.Y;
							defaultZ = p.Z;
						}
					}
					else
					{
						var p = GetRandomRegionPoint(m_Region);
						x = p.X;
						y = p.Y;
					}
				}
				else
				{
					switch (positioning)
					{
						case SpawnPositionType.Random:

							if (includetilelist != null || excludetilelist != null || tileflag != TileFlag.None)
							{
								if (locations != null && locations.Count > 0)
								{
									var p = locations[Utility.Random(locations.Count)];
									x = p.X;
									y = p.Y;
									defaultZ = p.Z;
								}
							}
							else
							{
								if (m_Width > 0)
									x = m_X + Utility.Random(m_Width + 1);
								if (m_Height > 0)
									y = m_Y + Utility.Random(m_Height + 1);
							}

							break;
						case SpawnPositionType.RelXY:

							x = mostRecentSpawnPosition.X + xinc;
							y = mostRecentSpawnPosition.Y + yinc;
							defaultZ = mostRecentSpawnPosition.Z + zinc;
							break;
						case SpawnPositionType.DeltaLocation:

							x = X + xinc;
							y = Y + yinc;
							defaultZ = Z + zinc;
							break;
						case SpawnPositionType.Location:

							x = xinc;
							y = yinc;
							defaultZ = zinc;
							break;
						case SpawnPositionType.RowFill:

							x = mostRecentSpawnPosition.X + fillinc;
							y = mostRecentSpawnPosition.Y;

							if (x < m_X) x = m_X;

							if (y < m_Y) y = m_Y;

							if (x > m_X + m_Width)
							{
								x = m_X + (x - m_X - m_Width - 1);
								y++;
							}

							if (y > m_Y + m_Height) y = m_Y;

							break;
						case SpawnPositionType.ColFill:

							x = mostRecentSpawnPosition.X;
							y = mostRecentSpawnPosition.Y + fillinc;

							if (x < m_X) x = m_X;

							if (y < m_Y) y = m_Y;

							if (y > m_Y + m_Height)
							{
								y = m_Y + (y - m_Y - m_Height - 1);
								x++;
							}

							if (x > m_X + m_Width) x = m_X;

							break;
						case SpawnPositionType.Perimeter:

							x = mostRecentSpawnPosition.X;
							y = mostRecentSpawnPosition.Y;

							// if the point is not on the perimeter, reset it to the corner
							if (x != m_X && x != m_X + m_Width && y != m_Y && y != m_Y + m_Height)
							{
								x = m_X;
								y = m_Y;
							}

							if (y == m_Y && x < m_X + m_Width) x += fillinc;
							else if (y == m_Y + m_Height && x > m_X) x -= fillinc;
							else if (x == m_X && y > m_Y) y -= fillinc;
							else if (x == m_X + m_Width && y < m_Y + m_Height) y += fillinc;

							if (x > m_X + m_Width) x = m_X + m_Width;

							if (y > m_Y + m_Height) y = m_Y + m_Height;

							if (x < m_X) x = m_X;

							if (y < m_Y) y = m_Y;

							break;
						case SpawnPositionType.Player:

							if (trigmob != null)
							{
								x = trigmob.Location.X;
								y = trigmob.Location.Y;
								if (positionrange > 0)
								{
									x += Utility.Random(positionrange * 2 + 1) - positionrange;
									y += Utility.Random(positionrange * 2 + 1) - positionrange;
								}
							}

							break;
						case SpawnPositionType.Waypoint:

							// pick an item randomly from the waylist
							if (WayList != null && WayList.Count > 0)
							{
								var index = Utility.Random(WayList.Count);
								var waypoint = WayList[index];
								if (waypoint != null)
								{
									x = waypoint.Location.X;
									y = waypoint.Location.Y;
									defaultZ = waypoint.Location.Z;
									if (positionrange > 0)
									{
										x += Utility.Random(positionrange * 2 + 1) - positionrange;
										y += Utility.Random(positionrange * 2 + 1) - positionrange;
									}
								}
							}

							break;
					}

					mostRecentSpawnPosition = new Point3D(x, y, defaultZ);
				}

				// skip invalid points
				if (x < 0 || y < 0 || x == 0 && y == 0) continue;

				// try to find a valid spawn location using the z coord of the spawner
				// relax the normal surface requirement for mobiles if the flag is set
				var fit = false;
				if (requiresurface)
					fit = CanSpawnMobile(x, y, defaultZ, mob);
				else
					fit = Map.CanFit(x, y, defaultZ, SpawnFitSize, true, false, false);

				// if that fails then try to find a valid z coord
				if (fit)
					return new Point3D(x, y, defaultZ);
				else
				{
					z = Map.GetAverageZ(x, y);

					if (requiresurface)
						fit = CanSpawnMobile(x, y, z, mob);
					else
						fit = Map.CanFit(x, y, z, SpawnFitSize, true, false, false);

					if (fit) return new Point3D(x, y, z);
				}
			}

			if (packrange >= 0 && packcoord != Point3D.Zero)
				return packcoord;
			else
				return Location;
		}

		public int GetCreatureMax(int index)
		{
			Defrag(false);

			if (m_SpawnObjects == null) return 0;

			return m_SpawnObjects[index].MaxCount;
		}

		private void DeleteFromList(List<object> list)
		{
			if (list == null) return;

			foreach (var o in list)
				if (o is Item)
					((Item)o).Delete();
				else if (o is Mobile)
					((Mobile)o).Delete();
		}

		private void DeleteFromList(List<Item> listi, List<Mobile> listm)
		{
			if (listi != null)
				for (var i = listi.Count - 1; i >= 0; --i)
					listi[i].Delete();
			if (listm != null)
				for (var i = listm.Count - 1; i >= 0; --i)
					listm[i].Delete();
		}

		public void RemoveSpawnObjects()
		{
			if (m_SpawnObjects == null) return;

			Defrag(false);

			ClearTags(true);
			var deletelist = new List<object>();
			foreach (var so in m_SpawnObjects)
				for (var i = 0; i < so.SpawnedObjects.Count; ++i)
				{
					var o = so.SpawnedObjects[i];

					if (o is Item || o is Mobile) deletelist.Add(o);
				}

			DeleteFromList(deletelist);

			// Defrag again
			Defrag(false);
		}


		public void RemoveSpawnObject(SpawnObject so)
		{
			if (so == null) return;

			Defrag(false);

			var deletelist = new List<object>();

			for (var i = 0; i < so.SpawnedObjects.Count; ++i)
			{
				var o = so.SpawnedObjects[i];

				if (o is Item || o is Mobile) deletelist.Add(o);
			}

			DeleteFromList(deletelist);

			// Defrag again
			Defrag(false);
		}

		public void ClearSubgroup(int subgroup)
		{
			if (m_SpawnObjects == null) return;

			Defrag(false);

			ClearTags(true);
			var deletelist = new List<object>();
			foreach (var so in m_SpawnObjects)
			{
				if (so.SubGroup != subgroup || !so.ClearOnAdvance) continue;

				for (var i = 0; i < so.SpawnedObjects.Count; ++i)
				{
					var o = so.SpawnedObjects[i];

					if (o is Item || o is Mobile) deletelist.Add(o);
				}
			}

			DeleteFromList(deletelist);

			// Defrag again
			Defrag(false);
		}


		// used to optimize smart spawning by removing all objects except those that have hold smartspawning
		public void SmartRemoveSpawnObjects()
		{
			if (m_SpawnObjects == null) return;

			Defrag(false);

			ClearTags(true);
			var deletelist = new List<object>();
			foreach (var so in m_SpawnObjects)
				for (var i = 0; i < so.SpawnedObjects.Count; ++i)
				{
					var o = so.SpawnedObjects[i];

					// new optimization for smart spawning to remove all objects except those with hold smartspawning enabled
					if (CheckHoldSmartSpawning(o)) continue;

					if (o is Item || o is Mobile) deletelist.Add(o);
				}

			DeleteFromList(deletelist);

			// Defrag again
			Defrag(false);
		}

		public void AddSpawnObject(string SpawnObjectName)
		{
			if (m_SpawnObjects == null) return;

			Defrag(false);

			// Find the spawn object and increment its count by one
			foreach (var so in m_SpawnObjects)
				if (so.TypeName.ToUpper() == SpawnObjectName.ToUpper())
				{
					// Add one to the total count
					m_Count++;

					// Increment the max count for the current creature
					so.ActualMaxCount++;

					//only spawn them immediately if the spawner is running
					if (Running)
						Spawn(SpawnObjectName, false, 0);
				}

			InvalidateProperties();
		}

		public void DeleteSpawnObject(Mobile from, string SpawnObjectName)
		{
			var WasRunning = m_Running;

			try
			{
				// Stop spawning for a moment
				Stop();

				// Clean up any spawns marked as deleted
				Defrag(false);

				// Keep a reference to the spawn object
				SpawnObject TheSpawn = null;

				// Find the spawn object and increment its count by one
				foreach (var so in m_SpawnObjects)
					if (so.TypeName.ToUpper() == SpawnObjectName.ToUpper())
					{
						// Set the spawn
						TheSpawn = so;
						break;
					}

				// Was the spawn object found
				if (TheSpawn != null)
				{
					var delete_this_entry = false;

					// Decrement the max count for the current creature
					TheSpawn.ActualMaxCount--;

					// Make sure the spawn count does not go negative
					if (TheSpawn.MaxCount < 0)
					{
						TheSpawn.MaxCount = 0;
						delete_this_entry = true;
					}

					if (!delete_this_entry)
						// Subtract one to the total count
						m_Count--;

					// Make sure the count does not go negative
					if (m_Count < 0) m_Count = 0;

					var deletelist = new List<object>();

					// Remove any spawns over the count
					while (TheSpawn.SpawnedObjects != null && TheSpawn.SpawnedObjects.Count > 0 &&
					       TheSpawn.SpawnedObjects.Count > TheSpawn.MaxCount)
					{
						var o = TheSpawn.SpawnedObjects[0];

						// Delete the object
						if (o is Item || o is Mobile) deletelist.Add(o);

						TheSpawn.SpawnedObjects.Remove(o);
					}

					DeleteFromList(deletelist);

					// Check if the spawn object should be removed
					//if( TheSpawn.MaxCount < 1 )
					if (delete_this_entry)
					{
						m_SpawnObjects.Remove(TheSpawn);
						if (from != null)
							CommandLogging.WriteLine(from,
								"{0} {1} removed from XmlSpawner {2} '{3}' [{4}, {5}] ({6}) : {7}", from.AccessLevel,
								CommandLogging.Format(from), Serial, Name, GetWorldLocation().X, GetWorldLocation().Y,
								Map, SpawnObjectName);
					}
				}

				InvalidateProperties();
			}
			finally
			{
				if (WasRunning)
					Start();
			}
		}

		#endregion

		#region Object Creation

		public static object CreateObject(Type type, string itemtypestring)
		{
			return CreateObject(type, itemtypestring, true);
		}

		public static object CreateObject(Type type, string itemtypestring, bool requireconstructable)
		{
			// look for constructor arguments to be passed to it with the syntax type,arg1,arg2,.../
			var typewordargs = BaseXmlSpawner.ParseObjectArgs(itemtypestring);

			return CreateObject(type, typewordargs, requireconstructable, false);
		}

		public static object CreateObject(Type type, string itemtypestring, bool requireconstructable,
			bool requireattachable)
		{
			// look for constructor arguments to be passed to it with the syntax type,arg1,arg2,.../
			var typewordargs = BaseXmlSpawner.ParseObjectArgs(itemtypestring);

			return CreateObject(type, typewordargs, requireconstructable, requireattachable);
		}

		public static object CreateObject(Type type, string[] typewordargs, bool requireconstructable,
			bool requireattachable)
		{
			if (type == null) return null;

			object o = null;

			var typearglen = 0;
			if (typewordargs != null)
				typearglen = typewordargs.Length;

			// ok, there are args in the typename, so we need to invoke the proper constructor
			var ctors = type.GetConstructors();

			if (ctors == null) return null;

			// go through all the constructors for this type
			for (var i = 0; i < ctors.Length; ++i)
			{
				var ctor = ctors[i];

				if (ctor == null) continue;

				// if both requireconstructable and requireattachable are true, then allow either condition
#if(RESTRICTCONSTRUCTABLE)
			   if (!(requireconstructable && Add.IsConstructable(ctor,requester)) && !(requireattachable && XmlAttach.IsAttachable(ctor, requester)))
					continue;
#else
				if (!(requireconstructable && IsConstructable(ctor)) &&
				    !(requireattachable && XmlAttach.IsAttachable(ctor)))
					continue;
#endif

				// check the parameter list of the constructor
				var paramList = ctor.GetParameters();

				// and compare with the argument list provided
				if (paramList != null && typearglen == paramList.Length)
				{
					// this is a constructor that takes args and matches the number of args passed in to CreateObject
					if (paramList.Length > 0)
					{
						object[] paramValues = null;

						try
						{
							paramValues = Add.ParseValues(paramList, typewordargs);
						}
						catch { }

						if (paramValues == null)
							continue;

						// ok, have a match on args, so try to construct it
						try
						{
							o = Activator.CreateInstance(type, paramValues);
						}
						catch { }
					}
					else
						// zero argument constructor
						try
						{
							o = Activator.CreateInstance(type);
						}
						catch { }

					// successfully constructed the object, otherwise try another matching constructor
					if (o != null) break;
				}
			}

			return o;
		}

		#endregion

		#region Timers

		private static void DoGlobalSectorTimer(TimeSpan delay)
		{
			if (m_GlobalSectorTimer != null)
				m_GlobalSectorTimer.Stop();

			m_GlobalSectorTimer = new GlobalSectorTimer(delay);

			m_GlobalSectorTimer.Start();
		}

		private class GlobalSectorTimer : Timer
		{
			public GlobalSectorTimer(TimeSpan delay)
				: base(delay, delay)
			{
				Priority = TimerPriority.OneSecond;
			}

			protected override void OnTick()
			{
				// check the sectors

				// check all active players
				if (NetState.Instances != null)
					foreach (var state in NetState.Instances)
					{
						var m = state.Mobile;

						if (m != null && (m.AccessLevel <= SmartSpawnAccessLevel || !m.Hidden))
							// activate any spawner in the sector they are in
							if (m.Map != null && m.Map != Map.Internal)
							{
								var s = m.Map.GetSector(m.Location);

								if (s != null && GlobalSectorTable[m.Map.MapID] != null)
								{
									List<XmlSpawner> spawnerlist; // = GlobalSectorTable[m.Map.MapID][s];
									if (GlobalSectorTable[m.Map.MapID].TryGetValue(s, out spawnerlist) &&
									    spawnerlist != null)
										foreach (var spawner in spawnerlist)
											if (spawner != null && !spawner.Deleted && spawner.Running &&
											    spawner.SmartSpawning && spawner.IsInactivated)
												spawner.SmartRespawn();
								}
							}
					}
			}
		}

		public void DoSectorTimer(TimeSpan delay)
		{
			if (m_SectorTimer != null)
				m_SectorTimer.Stop();

			m_SectorTimer = new SectorTimer(this, delay);

			m_SectorTimer.Start();
		}

		private class SectorTimer : Timer
		{
			private XmlSpawner m_Spawner;

			public SectorTimer(XmlSpawner spawner, TimeSpan delay)
				: base(delay, delay)
			{
				Priority = TimerPriority.OneSecond;
				m_Spawner = spawner;
			}

			protected override void OnTick()
			{
				// check the sectors

				if (m_Spawner != null && !m_Spawner.Deleted && m_Spawner.Running && m_Spawner.IsInactivated)
				{
					if (m_Spawner.SmartSpawning)
					{
						if (m_Spawner.HasActiveSectors)
						{
							Stop();

							m_Spawner.SmartRespawn();
						}
					}
					else
					{
						Stop();

						m_Spawner.IsInactivated = false;
					}
				}
				else
					Stop();
			}
		}

		private class WarnTimer2 : Timer
		{
			private List<WarnEntry2> m_List;

			private class WarnEntry2
			{
				public Point3D m_Point;
				public Map m_Map;
				public string m_Name;

				public WarnEntry2(Point3D p, Map map, string name)
				{
					m_Point = p;
					m_Map = map;
					m_Name = name;
				}
			}

			public WarnTimer2()
				: base(TimeSpan.FromSeconds(1.0))
			{
				m_List = new List<WarnEntry2>();
				Start();
			}

			public void Add(Point3D p, Map map, string name)
			{
				m_List.Add(new WarnEntry2(p, map, name));
			}

			protected override void OnTick()
			{
				try
				{
					Console.WriteLine("Warning: {0} bad spawns detected, logged: 'badspawn.log'", m_List.Count);

					using (var op = new StreamWriter("badspawn.log", true))
					{
						op.WriteLine("# Bad spawns : {0}", DateTime.Now);
						op.WriteLine("# Format: X Y Z F Name");
						op.WriteLine();

						foreach (var e in m_List)
							op.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}", e.m_Point.X, e.m_Point.Y, e.m_Point.Z, e.m_Map,
								e.m_Name);

						op.WriteLine();
						op.WriteLine();
					}
				}
				catch
				{
				}
			}
		}

		public void DoTimer()
		{
			if (!m_Running)
				return;

			var minSeconds = (int)m_MinDelay.TotalSeconds;
			var maxSeconds = (int)m_MaxDelay.TotalSeconds;

			var delay = TimeSpan.FromSeconds(Utility.RandomMinMax(minSeconds, maxSeconds));
			DoTimer(delay);
		}

		public void DoTimer(TimeSpan delay)
		{
			if (!m_Running)
				return;

			m_End = DateTime.Now + delay;

			if (m_Timer != null)
				m_Timer.Stop();

			m_Timer = new SpawnerTimer(this, delay);
			m_Timer.Start();
		}

		public void DoTimer2(TimeSpan delay)
		{
			m_DurEnd = DateTime.Now + delay;
			if (m_Duration > TimeSpan.FromMinutes(0) || m_durActivated == true)
			{
				if (m_DurTimer != null)
					m_DurTimer.Stop();
				m_DurTimer = new InternalTimer(this, delay);
				m_DurTimer.Start();
				m_durActivated = true;
			}
		}

		public void DoTimer3(TimeSpan delay)
		{
			//            if ( !m_proximityActivated )
			//                return;

			m_RefractEnd = DateTime.Now + delay;
			m_refractActivated = true;

			if (m_RefractoryTimer != null)
				m_RefractoryTimer.Stop();

			m_RefractoryTimer = new InternalTimer3(this, delay);
			m_RefractoryTimer.Start();
		}

		// added the duration timer that begins on spawning
		private class InternalTimer : Timer
		{
			private XmlSpawner m_spawner;

			public InternalTimer(XmlSpawner spawner, TimeSpan delay)
				: base(delay)
			{
				Priority = TimerPriority.OneSecond;
				m_spawner = spawner;
			}

			protected override void OnTick()
			{
				if (m_spawner != null && !m_spawner.Deleted)
				{
					m_spawner.RemoveSpawnObjects();
					m_spawner.m_durActivated = false;
				}
			}
		}

		private class SpawnerTimer : Timer
		{
			private XmlSpawner m_Spawner;

			public SpawnerTimer(XmlSpawner spawner, TimeSpan delay)
				: base(delay)
			{
				// reduce timer priority if spawner is inactivated
				if (spawner.IsInactivated)
					Priority = TimerPriority.FiveSeconds;
				else
					Priority = spawner.BasePriority;


				m_Spawner = spawner;
			}

			protected override void OnTick()
			{
				if (m_Spawner != null && !m_Spawner.Deleted) m_Spawner.OnTick();
			}
		}

		// added the refractory timer that begins on proximity triggering
		private class InternalTimer3 : Timer
		{
			private XmlSpawner m_spawner;

			public InternalTimer3(XmlSpawner spawner, TimeSpan delay)
				: base(delay)
			{
				Priority = TimerPriority.OneSecond;
				m_spawner = spawner;
			}

			protected override void OnTick()
			{
				if (m_spawner != null && !m_spawner.Deleted)
					// reenable triggering
					m_spawner.m_refractActivated = false;
			}
		}

		#endregion

		#region Serialization

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)32); // version
			// version 31
			writer.Write(m_DisableGlobalAutoReset);
			// Version 30
			writer.Write(m_AllowNPCTriggering);

			// Version 29
			if (m_SpawnObjects != null)
			{
				writer.Write(m_SpawnObjects.Count);
				for (var i = 0; i < m_SpawnObjects.Count; ++i)
					// Write the spawns per tick value
					writer.Write(m_SpawnObjects[i].SpawnsPerTick);
			}
			else
				// empty spawner
				writer.Write((int)0);

			// Version 28
			if (m_SpawnObjects != null)
				for (var i = 0; i < m_SpawnObjects.Count; ++i)
					// Write the pack range value
					writer.Write(m_SpawnObjects[i].PackRange);


			// Version 27
			if (m_SpawnObjects != null)
				for (var i = 0; i < m_SpawnObjects.Count; ++i)
					// Write the disable spawn flag
					writer.Write(m_SpawnObjects[i].Disabled);

			// Version 26
			writer.Write(m_SpawnOnTrigger);
			// writer.Write(m_FirstModified); //Version 32
			// writer.Write(m_LastModified); //Version 32

			// Version 25
			// eliminated the textentrybook serialization (they autodelete on deser now)

			// Version 24
			if (m_SpawnObjects != null)
				for (var i = 0; i < m_SpawnObjects.Count; ++i)
				{
					var so = m_SpawnObjects[i];
					// Write the restrict kills flag
					writer.Write(so.RestrictKillsToSubgroup);
					// Write the clear on advance flag
					writer.Write(so.ClearOnAdvance);
					// Write the mindelay
					writer.Write(so.MinDelay);
					// Write the maxdelay
					writer.Write(so.MaxDelay);
					// write the next spawn time for the subgrop
					writer.WriteDeltaTime(so.NextSpawn);
				}

			if (m_ShowBoundsItems != null && m_ShowBoundsItems.Count > 0)
			{
				writer.Write(true);
				writer.WriteItemList<Static>(m_ShowBoundsItems);
			}
			else
				// empty showbounds item list
				writer.Write(false);

			// Version 23
			writer.Write(IsInactivated);
			writer.Write(m_SmartSpawning);
			// Version 22
			writer.Write(m_SkillTrigger);
			writer.Write((int)m_skill_that_triggered);
			writer.Write(m_FreeRun);
			writer.Write(m_mob_who_triggered);
			// Version 21
			writer.Write(m_DespawnTime);
			// Version 20
			if (m_SpawnObjects != null)
				for (var i = 0; i < m_SpawnObjects.Count; ++i)
					// Write the requiresurface flag
					writer.Write(m_SpawnObjects[i].RequireSurface);
			// Version 19
			writer.Write(m_ConfigFile);
			writer.Write(m_OnHold);
			writer.Write(m_HoldSequence);
			// writer.Write(m_FirstModifiedBy); //Version 32
			// writer.Write(m_LastModifiedBy); //Version 32
			// compute the number of tags to save
			var tagcount = 0;
			for (var i = 0; i < m_KeywordTagList.Count; i++)
				// only save WAIT type keywords or other keywords that have the save flag set
				if ((m_KeywordTagList[i].Flags & BaseXmlSpawner.KeywordFlags.Serialize) != 0)
					tagcount++;
			writer.Write(tagcount);
			// and write them out
			for (var i = 0; i < m_KeywordTagList.Count; i++)
				if ((m_KeywordTagList[i].Flags & BaseXmlSpawner.KeywordFlags.Serialize) != 0)
					m_KeywordTagList[i].Serialize(writer);
			// Version 18
			writer.Write(m_AllowGhostTriggering);
			// Version 17
			// removed in version 25
			//writer.Write( m_TextEntryBook);
			// Version 16
			writer.Write(m_SequentialSpawning);
			// write out the remaining time until sequential reset
			writer.Write(NextSeqReset);
			// Write the spawn object list
			if (m_SpawnObjects != null)
				for (var i = 0; i < m_SpawnObjects.Count; ++i)
				{
					var so = m_SpawnObjects[i];
					// Write the subgroup and sequential reset time
					writer.Write(so.SubGroup);
					writer.Write(so.SequentialResetTime);
					writer.Write(so.SequentialResetTo);
					writer.Write(so.KillsNeeded);
				}

			writer.Write(m_RegionName); // 2004.02.08 :: Omega Red


			// Version 15
			writer.Write(m_ExternalTriggering);
			writer.Write(m_ExternalTrigger);

			// Version 14
			writer.Write(m_NoItemTriggerName);

			// Version 13
			writer.Write(m_GumpState);

			// Version 12
			var todtype = (int)m_TODMode;
			writer.Write(todtype);

			// Version 11
			writer.Write(m_KillReset);
			writer.Write(m_skipped);
			writer.Write(m_spawncheck);

			// Version 10
			writer.Write(m_SetPropertyItem);

			// Version 9
			writer.Write(m_TriggerProbability);

			// Version 8
			writer.Write(m_MobPropertyName);
			writer.Write(m_MobTriggerName);
			writer.Write(m_PlayerPropertyName);

			// Version 7
			writer.Write(m_SpeechTrigger);

			// Version 6
			writer.Write(m_ItemTriggerName);

			// Version 5
			writer.Write(m_ProximityTriggerMessage);
			writer.Write(m_ObjectPropertyItem);
			writer.Write(m_ObjectPropertyName);
			writer.Write(m_killcount);

			// Version 4
			writer.Write(m_ProximityRange);
			writer.Write(m_ProximityTriggerSound);
			writer.Write(m_proximityActivated);
			writer.Write(m_durActivated);
			writer.Write(m_refractActivated);
			writer.Write(m_StackAmount);
			writer.Write(m_TODStart);
			writer.Write(m_TODEnd);
			writer.Write(m_MinRefractory);
			writer.Write(m_MaxRefractory);
			if (m_refractActivated)
				writer.Write(m_RefractEnd - DateTime.Now);
			if (m_durActivated)
				writer.Write(m_DurEnd - DateTime.Now);
			// Version 3
			writer.Write(m_ShowContainerStatic);
			// Version 2
			writer.Write(m_Duration);

			// Version 1
			writer.Write(m_UniqueId);
			writer.Write(m_HomeRangeIsRelative);

			// Version 0
			writer.Write(m_Name);
			writer.Write(m_X);
			writer.Write(m_Y);
			writer.Write(m_Width);
			writer.Write(m_Height);
			writer.Write(m_WayPoint);
			writer.Write(m_Group);
			writer.Write(m_MinDelay);
			writer.Write(m_MaxDelay);
			writer.Write(m_Count);
			writer.Write(m_Team);
			writer.Write(m_HomeRange);
			writer.Write(m_Running);

			if (m_Running)
				writer.Write(m_End - DateTime.Now);

			// Write the spawn object list
			var nso = 0;
			if (m_SpawnObjects != null) nso = m_SpawnObjects.Count;
			writer.Write(nso);
			for (var i = 0; i < nso; ++i)
			{
				var so = m_SpawnObjects[i];

				// Write the type and maximum count
				writer.Write((string)so.TypeName);
				writer.Write((int)so.ActualMaxCount);

				// Write the spawned object information
				writer.Write(so.SpawnedObjects.Count);
				for (var x = 0; x < so.SpawnedObjects.Count; ++x)
				{
					var o = so.SpawnedObjects[x];

					if (o is Item)
						writer.Write((Item)o);
					else if (o is Mobile)
						writer.Write((Mobile)o);
					else
					{
						// if this is a keyword tag then add some more info
						if (o is BaseXmlSpawner.KeywordTag)
							writer.Write(-1 * ((BaseXmlSpawner.KeywordTag)o).Serial - 2);
						else
							writer.Write(Serial.MinusOne);
					}
				}
			}
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
			var haveproximityrange = false;
			var hasnewobjectinfo = false;
			var tmpSpawnListSize = 0;
			List<int> tmpSubGroup = null;
			List<double> tmpSequentialResetTime = null;
			List<int> tmpSequentialResetTo = null;
			List<int> tmpKillsNeeded = null;
			List<bool> tmpRequireSurface = null;
			List<bool> tmpRestrictKillsToSubgroup = null;
			List<bool> tmpClearOnAdvance = null;
			List<double> tmpMinDelay = null;
			List<double> tmpMaxDelay = null;
			List<DateTime> tmpNextSpawn = null;
			List<bool> tmpDisableSpawn = null;
			List<int> tmpPackRange = null;
			List<int> tmpSpawnsPer = null;

			switch (version)
			{
				case 32:
				case 31:
				{
					m_DisableGlobalAutoReset = reader.ReadBool();
					goto case 30;
				}
				case 30:
				{
					m_AllowNPCTriggering = reader.ReadBool();
					goto case 29;
				}
				case 29:
				{
					tmpSpawnListSize = reader.ReadInt();
					tmpSpawnsPer = new List<int>(tmpSpawnListSize);
					for (var i = 0; i < tmpSpawnListSize; ++i)
					{
						var spawnsper = reader.ReadInt();

						tmpSpawnsPer.Add(spawnsper);
					}

					goto case 28;
				}
				case 28:
				{
					if (version < 29)
						tmpSpawnListSize = reader.ReadInt();

					tmpPackRange = new List<int>(tmpSpawnListSize);
					for (var i = 0; i < tmpSpawnListSize; ++i)
					{
						var packrange = reader.ReadInt();

						tmpPackRange.Add(packrange);
					}

					goto case 27;
				}
				case 27:
				{
					if (version < 28)
						tmpSpawnListSize = reader.ReadInt();

					tmpDisableSpawn = new List<bool>(tmpSpawnListSize);
					for (var i = 0; i < tmpSpawnListSize; ++i)
					{
						var disablespawn = reader.ReadBool();

						tmpDisableSpawn.Add(disablespawn);
					}

					goto case 26;
				}
				case 26:
				{
					m_SpawnOnTrigger = reader.ReadBool();
					if (version < 32)
					{
						// Delete First & Last Modified
						reader.ReadDateTime();
						reader.ReadDateTime();
					}

					goto case 25;
				}
				case 25:
				{
					goto case 24;
				}
				case 24:
				{
					if (version < 27)
						tmpSpawnListSize = reader.ReadInt();
					tmpRestrictKillsToSubgroup = new List<bool>(tmpSpawnListSize);
					tmpClearOnAdvance = new List<bool>(tmpSpawnListSize);
					tmpMinDelay = new List<double>(tmpSpawnListSize);
					tmpMaxDelay = new List<double>(tmpSpawnListSize);
					tmpNextSpawn = new List<DateTime>(tmpSpawnListSize);
					for (var i = 0; i < tmpSpawnListSize; ++i)
					{
						var restrictkills = reader.ReadBool();
						var clearadvance = reader.ReadBool();
						var mind = reader.ReadDouble();
						var maxd = reader.ReadDouble();
						var nextspawn = reader.ReadDeltaTime();

						tmpRestrictKillsToSubgroup.Add(restrictkills);
						tmpClearOnAdvance.Add(clearadvance);
						tmpMinDelay.Add(mind);
						tmpMaxDelay.Add(maxd);
						tmpNextSpawn.Add(nextspawn);
					}

					var hasitems = reader.ReadBool();

					if (hasitems) m_ShowBoundsItems = reader.ReadStrongItemList<Static>();
					goto case 23;
				}
				case 23:
				{
					IsInactivated = reader.ReadBool();
					SmartSpawning = reader.ReadBool();

					goto case 22;
				}
				case 22:
				{
					SkillTrigger = reader.ReadString(); // note this will also register the skill
					m_skill_that_triggered = (SkillName)reader.ReadInt();
					m_FreeRun = reader.ReadBool();
					m_mob_who_triggered = reader.ReadMobile();
					goto case 21;
				}
				case 21:
				{
					m_DespawnTime = reader.ReadTimeSpan();
					goto case 20;
				}
				case 20:
				{
					if (version < 24)
						tmpSpawnListSize = reader.ReadInt();
					tmpRequireSurface = new List<bool>(tmpSpawnListSize);
					for (var i = 0; i < tmpSpawnListSize; ++i)
					{
						var requiresurface = reader.ReadBool();
						tmpRequireSurface.Add(requiresurface);
					}

					goto case 19;
				}
				case 19:
				{
					m_ConfigFile = reader.ReadString();
					m_OnHold = reader.ReadBool();
					m_HoldSequence = reader.ReadBool();
					if (version < 32)
					{
						// Delete First & Last Modified
						reader.ReadString();
						reader.ReadString();
					}

					// deserialize the keyword tag list
					var tagcount = reader.ReadInt();
					m_KeywordTagList = new List<BaseXmlSpawner.KeywordTag>(tagcount);
					for (var i = 0; i < tagcount; i++)
					{
						var tag = new BaseXmlSpawner.KeywordTag(null, this);
						tag.Deserialize(reader);
					}

					goto case 18;
				}
				case 18:
				{
					m_AllowGhostTriggering = reader.ReadBool();
					goto case 17;
				}
				case 17:
				{
					if (version < 25)
						// the textentrybooks are deleted on deserialization so no need to track them
						reader.ReadItem();
					goto case 16;
				}
				case 16:
				{
					hasnewobjectinfo = true;
					m_SequentialSpawning = reader.ReadInt();
					var seqdelay = reader.ReadTimeSpan();
					m_SeqEnd = DateTime.Now + seqdelay;
					if (version < 20) tmpSpawnListSize = reader.ReadInt();
					tmpSubGroup = new List<int>(tmpSpawnListSize);
					tmpSequentialResetTime = new List<double>(tmpSpawnListSize);
					tmpSequentialResetTo = new List<int>(tmpSpawnListSize);
					tmpKillsNeeded = new List<int>(tmpSpawnListSize);
					for (var i = 0; i < tmpSpawnListSize; ++i)
					{
						var subgroup = reader.ReadInt();
						var resettime = reader.ReadDouble();
						var resetto = reader.ReadInt();
						var killsneeded = reader.ReadInt();
						tmpSubGroup.Add(subgroup);
						tmpSequentialResetTime.Add(resettime);
						tmpSequentialResetTo.Add(resetto);
						tmpKillsNeeded.Add(killsneeded);
					}

					m_RegionName = reader.ReadString(); // 2004.02.08 :: Omega Red
					goto case 15;
				}
				case 15:
				{
					m_ExternalTriggering = reader.ReadBool();
					m_ExternalTrigger = reader.ReadBool();
					goto case 14;
				}
				case 14:
				{
					m_NoItemTriggerName = reader.ReadString();
					goto case 13;
				}
				case 13:
				{
					m_GumpState = reader.ReadString();
					goto case 12;
				}
				case 12:
				{
					var todtype = reader.ReadInt();
					switch (todtype)
					{
						case (int)TODModeType.Gametime:
							m_TODMode = TODModeType.Gametime;
							break;
						case (int)TODModeType.Realtime:
							m_TODMode = TODModeType.Realtime;
							break;
					}

					goto case 11;
				}
				case 11:
				{
					m_KillReset = reader.ReadInt();
					m_skipped = reader.ReadBool();
					m_spawncheck = reader.ReadInt();
					goto case 10;
				}
				case 10:
				{
					m_SetPropertyItem = reader.ReadItem();
					goto case 9;
				}
				case 9:
				{
					m_TriggerProbability = reader.ReadDouble();
					goto case 8;
				}
				case 8:
				{
					m_MobPropertyName = reader.ReadString();
					m_MobTriggerName = reader.ReadString();
					m_PlayerPropertyName = reader.ReadString();
					goto case 7;
				}
				case 7:
				{
					m_SpeechTrigger = reader.ReadString();
					goto case 6;
				}
				case 6:
				{
					m_ItemTriggerName = reader.ReadString();
					goto case 5;
				}
				case 5:
				{
					m_ProximityTriggerMessage = reader.ReadString();
					m_ObjectPropertyItem = reader.ReadItem();
					m_ObjectPropertyName = reader.ReadString();
					m_killcount = reader.ReadInt();
					goto case 4;
				}
				case 4:
				{
					haveproximityrange = true;
					m_ProximityRange = reader.ReadInt();
					m_ProximityTriggerSound = reader.ReadInt();
					m_proximityActivated = reader.ReadBool();
					m_durActivated = reader.ReadBool();
					m_refractActivated = reader.ReadBool();
					m_StackAmount = reader.ReadInt();
					m_TODStart = reader.ReadTimeSpan();
					m_TODEnd = reader.ReadTimeSpan();
					m_MinRefractory = reader.ReadTimeSpan();
					m_MaxRefractory = reader.ReadTimeSpan();
					if (m_refractActivated == true)
					{
						var delay = reader.ReadTimeSpan();
						DoTimer3(delay);
					}

					if (m_durActivated == true)
					{
						var delay = reader.ReadTimeSpan();
						DoTimer2(delay);
					}

					goto case 3;
				}
				case 3:
				{
					m_ShowContainerStatic = reader.ReadItem() as Static;
					goto case 2;
				}
				case 2:
				{
					m_Duration = reader.ReadTimeSpan();
					goto case 1;
				}
				case 1:
				{
					m_UniqueId = reader.ReadString();
					m_HomeRangeIsRelative = reader.ReadBool();
					goto case 0;
				}
				case 0:
				{
					m_Name = reader.ReadString();
					// backward compatibility with old name storage
					if (m_Name != null && m_Name != String.Empty) Name = m_Name;
					m_X = reader.ReadInt();
					m_Y = reader.ReadInt();
					m_Width = reader.ReadInt();
					m_Height = reader.ReadInt();
					//we HAVE to check if the area is even or if coordinates point to the original spawner, otherwise it's custom area!
					if (m_Width == m_Height && m_Width % 2 == 0 && m_X + m_Width / 2 == X && m_Y + m_Height / 2 == Y)
						m_SpawnRange = m_Width / 2;
					else
						m_SpawnRange = -1;
					if (!haveproximityrange) m_ProximityRange = -1;
					m_WayPoint = reader.ReadItem() as WayPoint;
					m_Group = reader.ReadBool();
					m_MinDelay = reader.ReadTimeSpan();
					m_MaxDelay = reader.ReadTimeSpan();
					m_Count = reader.ReadInt();
					m_Team = reader.ReadInt();
					m_HomeRange = reader.ReadInt();
					m_Running = reader.ReadBool();

					if (m_Running == true)
					{
						var delay = reader.ReadTimeSpan();
						DoTimer(delay);
					}

					// Read in the size of the spawn object list
					var SpawnListSize = reader.ReadInt();
					m_SpawnObjects = new List<SpawnObject>(SpawnListSize);
					for (var i = 0; i < SpawnListSize; ++i)
					{
						var TypeName = reader.ReadString();
						var TypeMaxCount = reader.ReadInt();

						var TheSpawnObject = new SpawnObject(TypeName, TypeMaxCount);

						m_SpawnObjects.Add(TheSpawnObject);

						var typeName = BaseXmlSpawner.ParseObjectType(TypeName);
						// does it have a substitution that might change its validity?
						// if so then let it go
						/*if(typeName!=null && typeName.StartsWith("#WAYPOINT"))
						{
							string[] args = BaseXmlSpawner.ParseSemicolonArgs(substitutedtypeName, 2);
							string[] keyvalueargs = BaseXmlSpawner.ParseCommaArgs(args[0], 10);
							SpawnPositionInfo spawnpositioning = new SpawnPositionInfo(SpawnPositionType.Waypoint, null, keyvalueargs);
							if (spawnPositionWayTable == null)
							{
								spawnPositionWayTable = new Dictionary<string, List<Item>>();
							}

							// try to find the waypoint list in the local table
							//WayList = spawnPositionWayTable[prefix];
							List<Item> WayList;

							// no existing list so create a new one
							if (!spawnPositionWayTable.TryGetValue(prefix, out WayList) || WayList == null)
							{
								WayList = new List<Item>();

								foreach (Item i in World.Items.Values)
								{
									if (i is WayPoint && i.Name != null && i.Map == Map && i.Name.StartsWith(prefix))
									{
										// add it to the list of items
										WayList.Add(i);
									}
								}
								// add the new list to the local table
								spawnPositionWayTable[prefix] = WayList;
							}
						}*/

						if (typeName == null || SpawnerType.GetType(typeName) == null &&
						    !BaseXmlSpawner.IsTypeOrItemKeyword(typeName) && typeName.IndexOf('{') == -1 &&
						    !typeName.StartsWith("*") && !typeName.StartsWith("#"))
						{
							if (m_WarnTimer == null)
								m_WarnTimer = new WarnTimer2();

							m_WarnTimer.Add(Location, Map, TypeName);

							status_str = "invalid type: " + typeName;
						}

						// Read in the number of spawns already
						var SpawnedCount = reader.ReadInt();

						TheSpawnObject.SpawnedObjects = new List<object>(SpawnedCount);

						for (var x = 0; x < SpawnedCount; ++x)
						{
							var serial = reader.ReadSerial();
							if (serial < -1)
							{
								// minusone is reserved for unknown types by default
								//  minustwo on is used for referencing keyword tags
								var tagserial = -1 * (serial + 2);
								// get the tag with that serial and add it
								var t = BaseXmlSpawner.GetFromTagList(this, tagserial);
								if (t != null) TheSpawnObject.SpawnedObjects.Add(t);
							}
							else
							{
								var e = World.FindEntity(serial);

								if (e != null)
									TheSpawnObject.SpawnedObjects.Add(e);
							}
						}
					}

					// now have to reintegrate the later version spawnobject information into the earlier version desered objects
					if (hasnewobjectinfo && tmpSpawnListSize == SpawnListSize)
						for (var i = 0; i < SpawnListSize; ++i)
						{
							var so = m_SpawnObjects[i];

							so.SubGroup = tmpSubGroup[i];
							so.SequentialResetTime = tmpSequentialResetTime[i];
							so.SequentialResetTo = tmpSequentialResetTo[i];
							so.KillsNeeded = tmpKillsNeeded[i];
							if (version > 19)
								so.RequireSurface = tmpRequireSurface[i];
							var restrictkills = false;
							var clearadvance = true;
							double mind = -1;
							double maxd = -1;
							var nextspawn = DateTime.MinValue;
							if (version > 23)
							{
								restrictkills = tmpRestrictKillsToSubgroup[i];
								clearadvance = tmpClearOnAdvance[i];
								mind = tmpMinDelay[i];
								maxd = tmpMaxDelay[i];
								nextspawn = tmpNextSpawn[i];
							}

							so.RestrictKillsToSubgroup = restrictkills;
							so.ClearOnAdvance = clearadvance;
							so.MinDelay = mind;
							so.MaxDelay = maxd;
							so.NextSpawn = nextspawn;

							var disablespawn = false;
							if (version > 26) disablespawn = tmpDisableSpawn[i];
							so.Disabled = disablespawn;

							var packrange = -1;
							if (version > 27) packrange = tmpPackRange[i];
							so.PackRange = packrange;

							var spawnsper = 1;
							if (version > 28) spawnsper = tmpSpawnsPer[i];
							so.SpawnsPerTick = spawnsper;
						}

					break;
				}
			}

			if (m_RegionName != null)
				Timer.DelayCall(delegate
				{
					if (!Deleted && m_RegionName != null) RegionName = m_RegionName;
				});
		}

		internal string GetSerializedObjectList()
		{
			var sb = new StringBuilder();

			foreach (var so in m_SpawnObjects)
			{
				if (sb.Length > 0)
					sb.Append(':'); // ':' Separates multiple object types

				sb.AppendFormat("{0}={1}", so.TypeName,
					so.ActualMaxCount); // '=' separates object name from maximum amount
			}

			return sb.ToString();
		}

		internal string GetSerializedObjectList2()
		{
			var sb = new StringBuilder();

			foreach (var so in m_SpawnObjects)
			{
				if (sb.Length > 0)
					sb.Append(":OBJ="); // Separates multiple object types

				sb.AppendFormat("{0}:MX={1}:SB={2}:RT={3}:TO={4}:KL={5}:RK={6}:CA={7}:DN={8}:DX={9}:SP={10}:PR={11}",
					so.TypeName, so.ActualMaxCount, so.SubGroup, so.SequentialResetTime, so.SequentialResetTo,
					so.KillsNeeded,
					so.RestrictKillsToSubgroup ? 1 : 0, so.ClearOnAdvance ? 1 : 0, so.MinDelay, so.MaxDelay,
					so.SpawnsPerTick, so.PackRange);
			}

			return sb.ToString();
		}

		#endregion

		#region Spawn classes

		public class SpawnObject
		{
			private string m_TypeName;
			private int m_MaxCount;
			private int m_SubGroup;
			private int m_SequentialResetTo;
			private int m_KillsNeeded;
			private bool m_RestrictKillsToSubgroup = false;
			private bool m_ClearOnAdvance = true;
			private double m_MinDelay = -1;
			private double m_MaxDelay = -1;
			private int m_SpawnsPerTick = 1;
			private bool m_Disabled = false;
			private int m_PackRange = -1;

			private bool m_Ignore = false;

			// temporary variable used to calculate weighted spawn probabilities
			public bool Available;


			public List<object> SpawnedObjects;
			public string[] PropertyArgs;
			public double SequentialResetTime;
			public int EntryOrder; // used for sorting
			public bool RequireSurface = true;
			public DateTime NextSpawn;
			public bool SpawnedThisTick;

			// these are externally accessible to the SETONSPAWNENTRY keyword
			public string TypeName
			{
				get => m_TypeName;
				set => m_TypeName = value;
			}

			public int MaxCount
			{
				get
				{
					if (Disabled)
						return 0;
					else
						return m_MaxCount;
				}
				set => m_MaxCount = value;
			}

			public int ActualMaxCount
			{
				get => m_MaxCount;
				set => m_MaxCount = value;
			}

			public int SubGroup
			{
				get => m_SubGroup;
				set => m_SubGroup = value;
			}

			public int SpawnsPerTick
			{
				get => m_SpawnsPerTick;
				set => m_SpawnsPerTick = value;
			}

			public int SequentialResetTo
			{
				get => m_SequentialResetTo;
				set => m_SequentialResetTo = value;
			}

			public int KillsNeeded
			{
				get => m_KillsNeeded;
				set => m_KillsNeeded = value;
			}

			public bool RestrictKillsToSubgroup
			{
				get => m_RestrictKillsToSubgroup;
				set => m_RestrictKillsToSubgroup = value;
			}

			public bool ClearOnAdvance
			{
				get => m_ClearOnAdvance;
				set => m_ClearOnAdvance = value;
			}

			public double MinDelay
			{
				get => m_MinDelay;
				set => m_MinDelay = value;
			}

			public double MaxDelay
			{
				get => m_MaxDelay;
				set => m_MaxDelay = value;
			}

			public bool Disabled
			{
				get => m_Disabled;
				set => m_Disabled = value;
			}

			public bool Ignore
			{
				get => m_Ignore;
				set => m_Ignore = value;
			}

			public int PackRange
			{
				get => m_PackRange;
				set => m_PackRange = value;
			}


			// command loggable constructor
			public SpawnObject(Mobile from, XmlSpawner spawner, string name, int maxamount)
			{
				if (from != null && spawner != null)
				{
					var found = false;
					// go through the current spawner objects and see if this is a new entry
					if (spawner.m_SpawnObjects != null)
						for (var i = 0; i < spawner.m_SpawnObjects.Count; i++)
						{
							var s = spawner.m_SpawnObjects[i];
							if (s != null && s.TypeName == name)
							{
								found = true;
								break;
							}
						}

					if (!found)
						CommandLogging.WriteLine(@from, "{0} {1} added to XmlSpawner {2} '{3}' [{4}, {5}] ({6}) : {7}",
							@from.AccessLevel, CommandLogging.Format(@from), spawner.Serial, spawner.Name,
							spawner.GetWorldLocation().X, spawner.GetWorldLocation().Y, spawner.Map, name);
				}

				TypeName = name;
				MaxCount = maxamount;
				SubGroup = 0;
				SequentialResetTime = 0;
				SequentialResetTo = 0;
				KillsNeeded = 0;
				RestrictKillsToSubgroup = false;
				ClearOnAdvance = true;
				SpawnedObjects = new List<object>();
			}

			public SpawnObject(string name, int maxamount)
			{
				TypeName = name;
				MaxCount = maxamount;
				SubGroup = 0;
				SequentialResetTime = 0;
				SequentialResetTo = 0;
				KillsNeeded = 0;
				RestrictKillsToSubgroup = false;
				ClearOnAdvance = true;
				SpawnedObjects = new List<object>();
			}

			public SpawnObject(string name, int maxamount, int subgroup, double sequentialresettime,
				int sequentialresetto, int killsneeded,
				bool restrictkills, bool clearadvance, double mindelay, double maxdelay, int spawnsper, int packrange)
			{
				TypeName = name;
				MaxCount = maxamount;
				SubGroup = subgroup;
				SequentialResetTime = sequentialresettime;
				SequentialResetTo = sequentialresetto;
				KillsNeeded = killsneeded;
				RestrictKillsToSubgroup = restrictkills;
				ClearOnAdvance = clearadvance;
				MinDelay = mindelay;
				MaxDelay = maxdelay;
				SpawnsPerTick = spawnsper;
				PackRange = packrange;
				SpawnedObjects = new List<object>();
			}

			internal static string GetParm(string str, string separator)
			{
				// find the parm separator in the string
				// then look for the termination at the ':'  or end of string
				// and return the stuff between
				var arg = BaseXmlSpawner.SplitString(str, separator);
				//should be 2 args
				if (arg.Length > 1)
				{
					// look for the end of parm terminator (could also be eol)
					var parm = arg[1].Split(':');
					if (parm.Length > 0) return parm[0];
				}

				return null;
			}


			internal static SpawnObject[] LoadSpawnObjectsFromString(string ObjectList)
			{
				// Clear the spawn object list
				var NewSpawnObjects = new List<SpawnObject>();

				if (ObjectList != null && ObjectList.Length > 0)
				{
					// Split the string based on the object separator first ':'
					var SpawnObjectList = ObjectList.Split(':');

					// Parse each item in the array
					foreach (var s in SpawnObjectList)
					{
						// Split the single spawn object item by the max count '='
						var SpawnObjectDetails = s.Split('=');

						// Should be two entries
						if (SpawnObjectDetails.Length == 2)
							// Validate the information

							// Make sure the spawn object name part has a valid length
							if (SpawnObjectDetails[0].Length > 0)
								// Make sure the max count part has a valid length
								if (SpawnObjectDetails[1].Length > 0)
								{
									var maxCount = 1;

									try
									{
										maxCount = Int32.Parse(SpawnObjectDetails[1]);
									}
									catch (Exception)
									{
										// Something went wrong, leave the default amount }
									}

									// Create the spawn object and store it in the array list
									var so = new SpawnObject(SpawnObjectDetails[0], maxCount);
									NewSpawnObjects.Add(so);
								}
					}
				}

				return NewSpawnObjects.ToArray();
			}


			internal static SpawnObject[] LoadSpawnObjectsFromString2(string ObjectList)
			{
				// Clear the spawn object list
				var NewSpawnObjects = new List<SpawnObject>();

				// spawn object definitions will take the form typestring:MX=int:SB=int:RT=double:TO=int:KL=int
				// or typestring:MX=int:SB=int:RT=double:TO=int:KL=int:OBJ=typestring...
				if (ObjectList != null && ObjectList.Length > 0)
				{
					var SpawnObjectList = BaseXmlSpawner.SplitString(ObjectList, ":OBJ=");

					// Parse each item in the array
					foreach (var s in SpawnObjectList)
					{
						// at this point each spawn string will take the form typestring:MX=int:SB=int:RT=double:TO=int:KL=int
						// Split the single spawn object item by the max count to get the typename and the remaining parms
						var SpawnObjectDetails = BaseXmlSpawner.SplitString(s, ":MX=");

						// Should be two entries
						if (SpawnObjectDetails.Length == 2)
							// Validate the information

							// Make sure the spawn object name part has a valid length
							if (SpawnObjectDetails[0].Length > 0)
								// Make sure the parm part has a valid length
								if (SpawnObjectDetails[1].Length > 0)
								{
									// now parse out the parms
									// MaxCount
									var parmstr = GetParm(s, ":MX=");
									var maxCount = 1;
									try { maxCount = Int32.Parse(parmstr); }
									catch { }

									// SubGroup
									parmstr = GetParm(s, ":SB=");

									var subGroup = 0;
									try { subGroup = Int32.Parse(parmstr); }
									catch { }

									// SequentialSpawnResetTime
									parmstr = GetParm(s, ":RT=");
									double resetTime = 0;
									try { resetTime = Double.Parse(parmstr); }
									catch { }

									// SequentialSpawnResetTo
									parmstr = GetParm(s, ":TO=");
									var resetTo = 0;
									try { resetTo = Int32.Parse(parmstr); }
									catch { }

									// KillsNeeded
									parmstr = GetParm(s, ":KL=");
									var killsNeeded = 0;
									try { killsNeeded = Int32.Parse(parmstr); }
									catch { }

									// RestrictKills
									parmstr = GetParm(s, ":RK=");
									var restrictKills = false;
									if (parmstr != null)
										try { restrictKills = Int32.Parse(parmstr) == 1; }
										catch { }

									// ClearOnAdvance
									parmstr = GetParm(s, ":CA=");
									var clearAdvance = true;
									// if kills needed is zero, then set CA to false by default.  This maintains consistency with the
									// previous default behavior for old spawn specs that havent specified CA
									if (killsNeeded == 0)
										clearAdvance = false;
									if (parmstr != null)
										try { clearAdvance = Int32.Parse(parmstr) == 1; }
										catch { }

									// MinDelay
									parmstr = GetParm(s, ":DN=");
									double minD = -1;
									try { minD = Double.Parse(parmstr); }
									catch { }

									// MaxDelay
									parmstr = GetParm(s, ":DX=");
									double maxD = -1;
									try { maxD = Double.Parse(parmstr); }
									catch { }

									// SpawnsPerTick
									parmstr = GetParm(s, ":SP=");
									var spawnsPer = 1;
									try { spawnsPer = Int32.Parse(parmstr); }
									catch { }

									// PackRange
									parmstr = GetParm(s, ":PR=");
									var packRange = -1;
									try { packRange = Int32.Parse(parmstr); }
									catch { }

									// Create the spawn object and store it in the array list
									var so = new SpawnObject(SpawnObjectDetails[0], maxCount, subGroup, resetTime,
										resetTo, killsNeeded,
										restrictKills, clearAdvance, minD, maxD, spawnsPer, packRange);

									NewSpawnObjects.Add(so);
								}
					}
				}

				return NewSpawnObjects.ToArray();
			}
		}

		#endregion
	}
}
