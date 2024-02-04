using System;
using Server.Commands;

namespace Server.Mobiles
{
	public class SpawnersWithoutFitCmd
	{
		public static void Initialize()
		{
			CommandSystem.Register( "badspawners", AccessLevel.GameMaster, PrintBadSpawners); 
		} 
       	
		public static void PrintBadSpawners( CommandEventArgs e )
		{
			foreach (var kvp in XmlSpawner.SpawnersWithoutFit)
			{
				var spawner = kvp.Key;
				var count = kvp.Value;
				Console.WriteLine($"Unable to fit: {spawner.Serial} {spawner.Name} {spawner.Map} {spawner.Location} {count}");
			}
			XmlSpawner.SpawnersWithoutFit.Clear();
		}
	}
}
