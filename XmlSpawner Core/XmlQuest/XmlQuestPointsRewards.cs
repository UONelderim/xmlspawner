using System;
using Server.Items;
using Server.Mobiles;
using System.Collections.Generic;

/*
** XmlQuestPointsRewards
** ArteGordon
** updated 9/18/05
**
** this class lets you specify rewards that can be purchased for XmlQuestPoints quest Credits.
** The items will be displayed in the QuestPointsRewardGump that is opened by the QuestPointsRewardStone
*/

namespace Server.Engines.XmlSpawner2
{
	public class XmlQuestPointsRewards
	{
		public int MinPoints;   // the minimum points requirement for the reward
		public Type RewardType; // this will be used to create an instance of the reward
		public string Name; // used to describe the reward in the gump
		public int Cost;       // cost of the reward in credits
		public int ItemID; // used for display purposes
		public int ItemHue;
		public int yOffset;
		public object[] RewardArgs; // arguments passed to the reward constructor

		private static List<XmlQuestPointsRewards> PointsRewardList = new List<XmlQuestPointsRewards>();

		public static List<XmlQuestPointsRewards> RewardsList => PointsRewardList;

		public XmlQuestPointsRewards(int minpoints, Type reward, string name, int cost, int id, int hue, int yoffset,
			object[] args)
		{
			MinPoints = minpoints;
			RewardType = reward;
			Name = name;
			Cost = cost;
			ItemID = id;
			ItemHue = hue;
			yOffset = yoffset;
			RewardArgs = args;
		}

		public static void Initialize()
		{
			// these are items as rewards. Note that the args list must match a constructor for the reward type specified.
            PointsRewardList.Add( new XmlQuestPointsRewards( 350, typeof(MinorArtifactDeed), "zwoj z wybieralny artefaktem minor", 350, 0x14F0, 0x495, 5, null ));
            PointsRewardList.Add( new XmlQuestPointsRewards( 250, typeof(ParoxysmusSwampDragonStatuette), "smok bagienny przedwiecznego", 250, 0x2619, 0x851, 5,null ));
            PointsRewardList.Add( new XmlQuestPointsRewards( 200, typeof(ScrappersCompendium), "Kompedium Wszelkiej Wiedzy", 200, 0xEFA, 0x494, 5, null ));
            PointsRewardList.Add( new XmlQuestPointsRewards( 150, typeof(PetBondingDeed), "Zwój Oswajacza", 150, 0x14F0, 0x23C, 5, null ));

            PointsRewardList.Add( new XmlQuestPointsRewards( 100, typeof(XmlEnemyMastery), "+100% Obrażeń przeciwko aniołom", 100, 0, 0, 5, new object[] { "EtherealWarrior", 50, 100, 1440.0 }));
            PointsRewardList.Add( new XmlQuestPointsRewards( 50, typeof(XmlStr), "+5 siły na 1 dzień", 50, 0, 0, 5, new object[] { 5, 86400.0 }));
		}
	}
}
