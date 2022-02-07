using System;

/*
* This script adds four attachments that will allow dynamic enhancment of Aos attributes
* XmlAosAttributes 
* XmlAosWeaponAttributes 
* XmlAosArmorAttributes 
* XmlAosElementAttributes 
*/
namespace Server.Engines.XmlSpawner2
{
	public class XmlAosAttributes : XmlBaseAttributes
	{
		// a serial constructor is REQUIRED
		public XmlAosAttributes(ASerial serial)
			: base(serial)
		{
		}

		[Attachable]
		public XmlAosAttributes()
			: base()
		{
		}

		[Attachable]
		public XmlAosAttributes(double expiresin)
			: base(expiresin)
		{
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int RegenHits
		{
			get => this[AosAttribute.RegenHits];
			set => this[AosAttribute.RegenHits] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int RegenStam
		{
			get => this[AosAttribute.RegenStam];
			set => this[AosAttribute.RegenStam] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int RegenMana
		{
			get => this[AosAttribute.RegenMana];
			set => this[AosAttribute.RegenMana] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int DefendChance
		{
			get => this[AosAttribute.DefendChance];
			set => this[AosAttribute.DefendChance] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int AttackChance
		{
			get => this[AosAttribute.AttackChance];
			set => this[AosAttribute.AttackChance] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int BonusStr
		{
			get => this[AosAttribute.BonusStr];
			set => this[AosAttribute.BonusStr] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int BonusDex
		{
			get => this[AosAttribute.BonusDex];
			set => this[AosAttribute.BonusDex] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int BonusInt
		{
			get => this[AosAttribute.BonusInt];
			set => this[AosAttribute.BonusInt] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int BonusHits
		{
			get => this[AosAttribute.BonusHits];
			set => this[AosAttribute.BonusHits] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int BonusStam
		{
			get => this[AosAttribute.BonusStam];
			set => this[AosAttribute.BonusStam] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int BonusMana
		{
			get => this[AosAttribute.BonusMana];
			set => this[AosAttribute.BonusMana] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int WeaponDamage
		{
			get => this[AosAttribute.WeaponDamage];
			set => this[AosAttribute.WeaponDamage] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int WeaponSpeed
		{
			get => this[AosAttribute.WeaponSpeed];
			set => this[AosAttribute.WeaponSpeed] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int SpellDamage
		{
			get => this[AosAttribute.SpellDamage];
			set => this[AosAttribute.SpellDamage] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int CastRecovery
		{
			get => this[AosAttribute.CastRecovery];
			set => this[AosAttribute.CastRecovery] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int CastSpeed
		{
			get => this[AosAttribute.CastSpeed];
			set => this[AosAttribute.CastSpeed] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int LowerManaCost
		{
			get => this[AosAttribute.LowerManaCost];
			set => this[AosAttribute.LowerManaCost] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int LowerRegCost
		{
			get => this[AosAttribute.LowerRegCost];
			set => this[AosAttribute.LowerRegCost] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int ReflectPhysical
		{
			get => this[AosAttribute.ReflectPhysical];
			set => this[AosAttribute.ReflectPhysical] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int EnhancePotions
		{
			get => this[AosAttribute.EnhancePotions];
			set => this[AosAttribute.EnhancePotions] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int Luck
		{
			get => this[AosAttribute.Luck];
			set => this[AosAttribute.Luck] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int SpellChanneling
		{
			get => this[AosAttribute.SpellChanneling];
			set => this[AosAttribute.SpellChanneling] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int NightSight
		{
			get => this[AosAttribute.NightSight];
			set => this[AosAttribute.NightSight] = value;
		}

		public int this[AosAttribute attribute]
		{
			get => GetValue((int)attribute);
			set => SetValue((int)attribute, value);
		}

		// These are the various ways in which the message attachment can be constructed.
		// These can be called via the [addatt interface, via scripts, via the spawner ATTACH keyword
		// Other overloads could be defined to handle other types of arguments
		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
			// version 0
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
			// version 0
		}
	}

	public class XmlAosWeaponAttributes : XmlBaseAttributes
	{
		// a serial constructor is REQUIRED
		public XmlAosWeaponAttributes(ASerial serial)
			: base(serial)
		{
		}

		[Attachable]
		public XmlAosWeaponAttributes()
			: base()
		{
		}

		[Attachable]
		public XmlAosWeaponAttributes(double expiresin)
			: base(expiresin)
		{
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int LowerStatReq
		{
			get => this[AosWeaponAttribute.LowerStatReq];
			set => this[AosWeaponAttribute.LowerStatReq] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int SelfRepair
		{
			get => this[AosWeaponAttribute.SelfRepair];
			set => this[AosWeaponAttribute.SelfRepair] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitLeechHits
		{
			get => this[AosWeaponAttribute.HitLeechHits];
			set => this[AosWeaponAttribute.HitLeechHits] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitLeechStam
		{
			get => this[AosWeaponAttribute.HitLeechStam];
			set => this[AosWeaponAttribute.HitLeechStam] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitLeechMana
		{
			get => this[AosWeaponAttribute.HitLeechMana];
			set => this[AosWeaponAttribute.HitLeechMana] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitLowerAttack
		{
			get => this[AosWeaponAttribute.HitLowerAttack];
			set => this[AosWeaponAttribute.HitLowerAttack] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitLowerDefend
		{
			get => this[AosWeaponAttribute.HitLowerDefend];
			set => this[AosWeaponAttribute.HitLowerDefend] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitMagicArrow
		{
			get => this[AosWeaponAttribute.HitMagicArrow];
			set => this[AosWeaponAttribute.HitMagicArrow] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitHarm
		{
			get => this[AosWeaponAttribute.HitHarm];
			set => this[AosWeaponAttribute.HitHarm] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitFireball
		{
			get => this[AosWeaponAttribute.HitFireball];
			set => this[AosWeaponAttribute.HitFireball] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitLightning
		{
			get => this[AosWeaponAttribute.HitLightning];
			set => this[AosWeaponAttribute.HitLightning] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitDispel
		{
			get => this[AosWeaponAttribute.HitDispel];
			set => this[AosWeaponAttribute.HitDispel] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitColdArea
		{
			get => this[AosWeaponAttribute.HitColdArea];
			set => this[AosWeaponAttribute.HitColdArea] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitFireArea
		{
			get => this[AosWeaponAttribute.HitFireArea];
			set => this[AosWeaponAttribute.HitFireArea] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitPoisonArea
		{
			get => this[AosWeaponAttribute.HitPoisonArea];
			set => this[AosWeaponAttribute.HitPoisonArea] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitEnergyArea
		{
			get => this[AosWeaponAttribute.HitEnergyArea];
			set => this[AosWeaponAttribute.HitEnergyArea] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitPhysicalArea
		{
			get => this[AosWeaponAttribute.HitPhysicalArea];
			set => this[AosWeaponAttribute.HitPhysicalArea] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int ResistPhysicalBonus
		{
			get => this[AosWeaponAttribute.ResistPhysicalBonus];
			set => this[AosWeaponAttribute.ResistPhysicalBonus] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int ResistFireBonus
		{
			get => this[AosWeaponAttribute.ResistFireBonus];
			set => this[AosWeaponAttribute.ResistFireBonus] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int ResistColdBonus
		{
			get => this[AosWeaponAttribute.ResistColdBonus];
			set => this[AosWeaponAttribute.ResistColdBonus] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int ResistPoisonBonus
		{
			get => this[AosWeaponAttribute.ResistPoisonBonus];
			set => this[AosWeaponAttribute.ResistPoisonBonus] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int ResistEnergyBonus
		{
			get => this[AosWeaponAttribute.ResistEnergyBonus];
			set => this[AosWeaponAttribute.ResistEnergyBonus] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int UseBestSkill
		{
			get => this[AosWeaponAttribute.UseBestSkill];
			set => this[AosWeaponAttribute.UseBestSkill] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int MageWeapon
		{
			get => this[AosWeaponAttribute.MageWeapon];
			set => this[AosWeaponAttribute.MageWeapon] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int DurabilityBonus
		{
			get => this[AosWeaponAttribute.DurabilityBonus];
			set => this[AosWeaponAttribute.DurabilityBonus] = value;
		}

		public int this[AosWeaponAttribute attribute]
		{
			get => GetValue((int)attribute);
			set => SetValue((int)attribute, value);
		}

		// These are the various ways in which the message attachment can be constructed.
		// These can be called via the [addatt interface, via scripts, via the spawner ATTACH keyword
		// Other overloads could be defined to handle other types of arguments
		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
			// version 0
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
			// version 0
		}
	}

	public class XmlAosArmorAttributes : XmlBaseAttributes
	{
		// a serial constructor is REQUIRED
		public XmlAosArmorAttributes(ASerial serial)
			: base(serial)
		{
		}

		[Attachable]
		public XmlAosArmorAttributes()
			: base()
		{
		}

		[Attachable]
		public XmlAosArmorAttributes(double expiresin)
			: base(expiresin)
		{
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int LowerStatReq
		{
			get => this[AosArmorAttribute.LowerStatReq];
			set => this[AosArmorAttribute.LowerStatReq] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int SelfRepair
		{
			get => this[AosArmorAttribute.SelfRepair];
			set => this[AosArmorAttribute.SelfRepair] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int MageArmor
		{
			get => this[AosArmorAttribute.MageArmor];
			set => this[AosArmorAttribute.MageArmor] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int DurabilityBonus
		{
			get => this[AosArmorAttribute.DurabilityBonus];
			set => this[AosArmorAttribute.DurabilityBonus] = value;
		}

		public int this[AosArmorAttribute attribute]
		{
			get => GetValue((int)attribute);
			set => SetValue((int)attribute, value);
		}

		// These are the various ways in which the message attachment can be constructed.
		// These can be called via the [addatt interface, via scripts, via the spawner ATTACH keyword
		// Other overloads could be defined to handle other types of arguments
		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
			// version 0
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
			// version 0
		}
	}

	public class XmlAosElementAttributes : XmlBaseAttributes
	{
		// a serial constructor is REQUIRED
		public XmlAosElementAttributes(ASerial serial)
			: base(serial)
		{
		}

		[Attachable]
		public XmlAosElementAttributes()
			: base()
		{
		}

		[Attachable]
		public XmlAosElementAttributes(double expiresin)
			: base(expiresin)
		{
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int Physical
		{
			get => this[AosElementAttribute.Physical];
			set => this[AosElementAttribute.Physical] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int Fire
		{
			get => this[AosElementAttribute.Fire];
			set => this[AosElementAttribute.Fire] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int Cold
		{
			get => this[AosElementAttribute.Cold];
			set => this[AosElementAttribute.Cold] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int Poison
		{
			get => this[AosElementAttribute.Poison];
			set => this[AosElementAttribute.Poison] = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int Energy
		{
			get => this[AosElementAttribute.Energy];
			set => this[AosElementAttribute.Energy] = value;
		}

		public int this[AosElementAttribute attribute]
		{
			get => GetValue((int)attribute);
			set => SetValue((int)attribute, value);
		}

		// These are the various ways in which the message attachment can be constructed.
		// These can be called via the [addatt interface, via scripts, via the spawner ATTACH keyword
		// Other overloads could be defined to handle other types of arguments
		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
			// version 0
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
			// version 0
		}
	}

	public class XmlBaseAttributes : XmlAttachment
	{
		private static readonly int[] m_Empty = new int[0];
		private uint m_Names;

		private int[] m_Values = new int[0];

		// a serial constructor is REQUIRED
		public XmlBaseAttributes(ASerial serial)
			: base(serial)
		{
		}

		[Attachable]
		public XmlBaseAttributes()
		{
		}

		[Attachable]
		public XmlBaseAttributes(double expiresin)
		{
			Expiration = TimeSpan.FromMinutes(expiresin);
		}

		public bool IsEmpty => m_Names == 0;

		// These are the various ways in which the message attachment can be constructed.
		// These can be called via the [addatt interface, via scripts, via the spawner ATTACH keyword
		// Other overloads could be defined to handle other types of arguments
		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
			// version 0
			writer.Write((uint)m_Names);
			writer.WriteEncodedInt((int)m_Values.Length);

			for (var i = 0; i < m_Values.Length; ++i)
				writer.WriteEncodedInt((int)m_Values[i]);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
			// version 0
			m_Names = reader.ReadUInt();
			m_Values = new int[reader.ReadEncodedInt()];

			for (var i = 0; i < m_Values.Length; ++i)
				m_Values[i] = reader.ReadEncodedInt();
		}

		public override void OnDelete()
		{
			base.OnDelete();

			// remove the mod
			if (AttachedTo is Item) ((Item)AttachedTo).InvalidateProperties();
		}

		public override void OnAttach()
		{
			base.OnAttach();

			if (AttachedTo is Item) ((Item)AttachedTo).InvalidateProperties();
		}

		public int GetValue(int bitmask)
		{
			var mask = (uint)bitmask;

			if ((m_Names & mask) == 0)
				return 0;

			var index = GetIndex(mask);

			if (index >= 0 && index < m_Values.Length)
				return m_Values[index];

			return 0;
		}

		public void SetValue(int bitmask, int value)
		{
			var mask = (uint)bitmask;

			if (value != 0)
			{
				if ((m_Names & mask) != 0)
				{
					var index = GetIndex(mask);

					if (index >= 0 && index < m_Values.Length)
						m_Values[index] = value;
				}
				else
				{
					var index = GetIndex(mask);

					if (index >= 0 && index <= m_Values.Length)
					{
						var old = m_Values;
						m_Values = new int[old.Length + 1];

						for (var i = 0; i < index; ++i)
							m_Values[i] = old[i];

						m_Values[index] = value;

						for (var i = index; i < old.Length; ++i)
							m_Values[i + 1] = old[i];

						m_Names |= mask;
					}
				}
			}
			else if ((m_Names & mask) != 0)
			{
				var index = GetIndex(mask);

				if (index >= 0 && index < m_Values.Length)
				{
					m_Names &= ~mask;

					if (m_Values.Length == 1)
						m_Values = m_Empty;
					else
					{
						var old = m_Values;
						m_Values = new int[old.Length - 1];

						for (var i = 0; i < index; ++i)
							m_Values[i] = old[i];

						for (var i = index + 1; i < old.Length; ++i)
							m_Values[i - 1] = old[i];
					}
				}
			}

			if (AttachedTo is Item) ((Item)AttachedTo).InvalidateProperties();
		}

		private int GetIndex(uint mask)
		{
			var index = 0;
			var ourNames = m_Names;
			uint currentBit = 1;

			while (currentBit != mask)
			{
				if ((ourNames & currentBit) != 0)
					++index;

				if (currentBit == 0x80000000)
					return -1;

				currentBit <<= 1;
			}

			return index;
		}
	}
}
