using System;

namespace Server.Engines.XmlSpawner2
{
	public class XmlAnimate : XmlAttachment
	{
		private int m_AnimationValue = 0; // default animation
		private int m_FrameCount = 7; // default framecount
		private int m_RepeatCount = 1; // default repeatcount
		private int m_AnimationDelay = 0; // default animation delay
		private bool m_Repeat = false; // default repeat
		private bool m_Forward = true; // default animation direction
		private string m_ActivationWord = null; // no word activation by default
		private TimeSpan m_Refractory = TimeSpan.FromSeconds(5); // 5 seconds default time between activations
		private DateTime m_EndTime;
		private int m_ProximityRange = 5; // default movement activation from 5 tiles away
		private LoopTimer m_Timer;
		private int m_LoopCount = 0; // repeat animations using a timed loop
		private int m_LoopDelay = 5; // interval in seconds between loop ticks

		private int m_CurrentCount = 0;

		// a serial constructor is REQUIRED
		public XmlAnimate(ASerial serial)
			: base(serial)
		{
		}

		[Attachable]
		public XmlAnimate()
		{
		}

		[Attachable]
		public XmlAnimate(int animation)
		{
			AnimationValue = animation;
		}

		[Attachable]
		public XmlAnimate(int animation, double refractory)
		{
			AnimationValue = animation;
			Refractory = TimeSpan.FromSeconds(refractory);
		}

		[Attachable]
		public XmlAnimate(int animation, int framecount, double refractory)
		{
			AnimationValue = animation;
			FrameCount = framecount;
			Refractory = TimeSpan.FromSeconds(refractory);
		}

		[Attachable]
		public XmlAnimate(int animation, double refractory, int loopcount, int loopdelay)
		{
			LoopCount = loopcount;
			LoopDelay = loopdelay;
			AnimationValue = animation;
			Refractory = TimeSpan.FromSeconds(refractory);
		}

		[Attachable]
		public XmlAnimate(int animation, int framecount, double refractory, int loopcount, int loopdelay)
		{
			LoopCount = loopcount;
			LoopDelay = loopdelay;
			AnimationValue = animation;
			FrameCount = framecount;
			Refractory = TimeSpan.FromSeconds(refractory);
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int ProximityRange
		{
			get => m_ProximityRange;
			set => m_ProximityRange = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int FrameCount
		{
			get => m_FrameCount;
			set => m_FrameCount = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int RepeatCount
		{
			get => m_RepeatCount;
			set => m_RepeatCount = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int AnimationDelay
		{
			get => m_AnimationDelay;
			set => m_AnimationDelay = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool Repeat
		{
			get => m_Repeat;
			set => m_Repeat = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool Forward
		{
			get => m_Forward;
			set => m_Forward = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int AnimationValue
		{
			get => m_AnimationValue;
			set => m_AnimationValue = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public string ActivationWord
		{
			get => m_ActivationWord;
			set => m_ActivationWord = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public TimeSpan Refractory
		{
			get => m_Refractory;
			set => m_Refractory = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int LoopCount
		{
			get => m_LoopCount;
			set => m_LoopCount = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int LoopDelay
		{
			get => m_LoopDelay;
			set => m_LoopDelay = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int CurrentCount
		{
			get => m_CurrentCount;
			set => m_CurrentCount = value;
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool DoAnimate
		{
			get => false;
			set
			{
				if (value == true)
					OnTrigger(null, null);
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool DoReset
		{
			get => false;
			set
			{
				if (value == true)
					Reset();
			}
		}

		// These are the various ways in which the message attachment can be constructed.
		// These can be called via the [addatt interface, via scripts, via the spawner ATTACH keyword.
		// Other overloads could be defined to handle other types of arguments
		public override bool HandlesOnSpeech => ActivationWord != null;

		public override bool HandlesOnMovement => ProximityRange >= 0 && ActivationWord == null;

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)0);
			// version 0
			writer.Write(m_CurrentCount);
			writer.Write(m_LoopCount);
			writer.Write(m_LoopDelay);
			writer.Write(m_ProximityRange);
			writer.Write(m_AnimationValue);
			writer.Write(m_FrameCount);
			writer.Write(m_RepeatCount);
			writer.Write(m_AnimationDelay);
			writer.Write(m_Forward);
			writer.Write(m_Repeat);
			writer.Write(m_ActivationWord);
			writer.Write(m_Refractory);
			writer.Write(m_EndTime - DateTime.UtcNow);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();
			switch (version)
			{
				case 0:
					// version 0
					m_CurrentCount = reader.ReadInt();
					m_LoopCount = reader.ReadInt();
					m_LoopDelay = reader.ReadInt();
					m_ProximityRange = reader.ReadInt();
					m_AnimationValue = reader.ReadInt();
					m_FrameCount = reader.ReadInt();
					m_RepeatCount = reader.ReadInt();
					m_AnimationDelay = reader.ReadInt();
					m_Forward = reader.ReadBool();
					m_Repeat = reader.ReadBool();
					m_ActivationWord = reader.ReadString();
					m_Refractory = reader.ReadTimeSpan();
					var remaining = reader.ReadTimeSpan();
					m_EndTime = DateTime.UtcNow + remaining;
					break;
			}

			// restart any animation loops that were active
			if (CurrentCount > 0) DoTimer(TimeSpan.FromSeconds(LoopDelay));
		}

		public override string OnIdentify(Mobile from)
		{
			if (from == null || from.AccessLevel < AccessLevel.Counselor)
				return null;

			var msg = String.Format("Animation #{0},{1} : {2} secs between uses", AnimationValue, FrameCount,
				Refractory.TotalSeconds);

			if (ActivationWord == null)
				return msg;
			else
				return String.Format("{0} : trigger on '{1}'", msg, ActivationWord);
		}

		public override void OnSpeech(SpeechEventArgs e)
		{
			base.OnSpeech(e);

			if (e.Mobile == null || e.Mobile.AccessLevel > AccessLevel.Player)
				return;

			if (e.Speech == ActivationWord) OnTrigger(null, e.Mobile);
		}

		public override void OnMovement(MovementEventArgs e)
		{
			base.OnMovement(e);

			if (e.Mobile == null || e.Mobile.AccessLevel > AccessLevel.Player)
				return;

			if (AttachedTo is Item && ((Item)AttachedTo).Parent == null &&
			    Utility.InRange(e.Mobile.Location, ((Item)AttachedTo).Location, ProximityRange))
				OnTrigger(null, e.Mobile);
			else
				return;
		}

		public override void OnAttach()
		{
			base.OnAttach();

			// only attach to mobiles
			if (!(AttachedTo is Mobile)) Delete();
		}

		public void Reset()
		{
			if (m_Timer != null)
				m_Timer.Stop();

			CurrentCount = 0;
			m_EndTime = DateTime.UtcNow;
		}

		public void Animate()
		{
			// play a animation
			if (AttachedTo is Mobile && AnimationValue >= 0)
				((Mobile)AttachedTo).Animate(AnimationValue, FrameCount, RepeatCount, Forward, Repeat, AnimationDelay);

			UpdateRefractory();

			CurrentCount--;
		}

		public void UpdateRefractory()
		{
			m_EndTime = DateTime.UtcNow + Refractory;
		}

		public override void OnTrigger(object activator, Mobile m)
		{
			if (DateTime.UtcNow < m_EndTime)
				return;

			if (LoopCount > 0)
			{
				CurrentCount = LoopCount;
				// check to make sure the timer is running
				DoTimer(TimeSpan.FromSeconds(LoopDelay));
			}
			else
				Animate();
		}

		private void DoTimer(TimeSpan delay)
		{
			if (m_Timer != null)
				m_Timer.Stop();

			m_Timer = new LoopTimer(this, delay);
			m_Timer.Start();
		}

		private class LoopTimer : Timer
		{
			public readonly TimeSpan m_delay;
			private readonly XmlAnimate m_attachment;

			public LoopTimer(XmlAnimate attachment, TimeSpan delay)
				: base(delay, delay)
			{
				Priority = TimerPriority.OneSecond;

				m_attachment = attachment;
				m_delay = delay;
			}

			protected override void OnTick()
			{
				if (m_attachment != null && !m_attachment.Deleted)
				{
					m_attachment.Animate();

					if (m_attachment.CurrentCount <= 0)
						Stop();
				}
				else
					Stop();
			}
		}
	}
}
