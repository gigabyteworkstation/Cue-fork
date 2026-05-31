using SimpleJSON;
using System;
using System.Collections.Generic;

namespace Cue.VamMoan
{
	sealed class Voice : IVoice
	{
		private const string PluginName = "VAMMoanPlugin.VAMMoan";
		private const float VoiceCheckInterval = 2;
		private const float ForceIntensityInterval = 1;

		struct Parameters
		{
			public Sys.Vam.BoolParameter   enabled;
			public Sys.Vam.BoolParameter   autoJaw;
			public Sys.Vam.StringChooserParameter voice;
			public Sys.Vam.FloatParameter  volume;
			public Sys.Vam.ActionParameter disabled;
			public Sys.Vam.ActionParameter breathing;
			public Sys.Vam.ActionParameter orgasm;
			public Sys.Vam.ActionParameter perpetualOrgasm;          // "Voice perpetual orgasm"
			public Sys.Vam.ActionParameter kissing;
			public Sys.Vam.BoolParameter   bjEnabled;                // >= v20
			public Sys.Vam.BoolParameter   breathingEnabled;
			public Sys.Vam.ActionParameter[] bjIntensities;          // <  v20 legacy
			public Sys.Vam.ActionParameter[] intensities;
			public Sys.Vam.FloatParameter  availableIntensities;
			public bool hasAvailableIntensities;
		}


		private Person person_ = null;
		private Logger log_;

		private string lastAction_ = "";
		private int intensitiesCount_ = 0;
		private Parameters p_;
		private float voiceCheckElapsed_ = 0;
		private float forceIntensityElapsed_ = 0;
		private string warning_ = "";
		private float oldVolume_ = 0;
		private Sys.Vam.ActionParameter currentAction_ = null;

		private string voice_ = "";


		private Voice()
		{
		}

		public Voice(JSONClass o)
		{
			Load(o, false);
		}

		public void Load(JSONClass o, bool inherited)
		{
			// no per-personality config needed for this provider
		}

		public IVoice Clone()
		{
			var b = new Voice();
			b.CopyFrom(this);
			return b;
		}

		private void CopyFrom(Voice v)
		{
			// nothing personality-specific to copy
		}

		public void Init(Person p)
		{
			person_ = p;
			log_ = new Logger(Logger.Integration, p, "vammoan");

			p_.enabled             = BP("enabled");
			p_.autoJaw             = BP("Enable auto-jaw animation");
			p_.voice               = SCP("voice");
			p_.volume              = FP("Voice volume");
			p_.disabled            = AP("Voice disabled");
			p_.breathing           = AP("Voice breathing");
			p_.kissing             = AP("Voice kissing");
			p_.intensities         = GetIntensities();
			p_.availableIntensities = FP("VAMM IntensitiesCount");
			p_.orgasm              = AP("Voice orgasm");
			p_.perpetualOrgasm     = AP("Voice perpetual orgasm");
			p_.bjEnabled           = BP("Enable blowjob sounds");
			p_.breathingEnabled    = BP("Enable breathing animation");

			p_.bjIntensities = new Sys.Vam.ActionParameter[]
			{
				AP("Voice blowjob"),
				AP("Voice blowjob intense")
			};

			// default is 1.0, but is set to 0.5 in the plugin
			oldVolume_ = 0.5f;

			CheckVoice();

			p_.enabled.Value  = true;
			p_.autoJaw.Value  = true;
			SetOptimalJaw();

			MacGruber.Voice.Disable(p);
		}

		private void SetOptimalJaw()
		{
			var atom   = CueMain.Instance.MVRPluginManager?.containingAtom;
			var atomUI = atom?.UITransform?.GetComponentInChildren<AtomUI>();
			var bs     = atomUI?.GetComponentsInChildren<UIDynamicButton>(true);

			if (bs == null)
				return;

			for (int i = 0; i < bs.Length; ++i)
			{
				if (bs[i]?.buttonText?.text == "Set optimal auto-jaw animation parameters")
				{
					bs[i]?.button?.onClick?.Invoke();
					break;
				}
			}
		}

		public static void Disable(Person p)
		{
			var e = Sys.Vam.Parameters.GetBool(p, PluginName, "enabled");

			if (e != null)
				e.val = false;
		}

		// ─────────────────────────────────────────────────────────────
		// IVoice – identity
		// ─────────────────────────────────────────────────────────────

		public string Name
		{
			get { return "vammoan"; }
		}

		// ─────────────────────────────────────────────────────────────
		// IVoice – mute
		// ─────────────────────────────────────────────────────────────

		public bool Muted
		{
			set
			{
				if (value)
				{
					oldVolume_ = p_.volume.Value;
					p_.volume.Value = 0;
				}
				else
				{
					p_.volume.Value = oldVolume_;
				}
			}
		}

		// ─────────────────────────────────────────────────────────────
		// IVoice – per-frame
		// ─────────────────────────────────────────────────────────────

		public void Update(float s)
		{
			voiceCheckElapsed_ += s;
			if (voiceCheckElapsed_ >= VoiceCheckInterval)
			{
				voiceCheckElapsed_ = 0;
				CheckVoice();
			}

			// The orgasm (and perpetual orgasm) actions are special: VAMMoan
			// blocks intensity changes while the clip is playing.  Re-fire
			// periodically so the correct action stays active.
			forceIntensityElapsed_ += s;
			if (forceIntensityElapsed_ >= ForceIntensityInterval)
			{
				forceIntensityElapsed_ = 0;
				Fire(true);
			}
		}

		public void Debug(DebugLines debug)
		{
			debug.Add("provider",         "vammoan");
			debug.Add("intensitiesCount", $"{intensitiesCount_}");
			debug.Add("lastAction",       lastAction_);
		}

		// ─────────────────────────────────────────────────────────────
		// IVoice – sound commands
		// ─────────────────────────────────────────────────────────────

		public void SetMoaning(float v)
		{
			if (p_.intensities.Length == 0)
				return;

			int index = (int)(v * p_.intensities.Length);
			index = U.Clamp(index, 0, p_.intensities.Length - 1);

			SetAction(p_.intensities[index], false);
			Fire();
		}

		public void SetBreathing()
		{
			SetAction(p_.breathing, false);
			Fire();
		}

		public void SetSilent()
		{
			SetAction(p_.disabled, false);
			Fire();
		}

		public void SetOrgasm()
		{
			SetAction(p_.orgasm, false);
			Fire();
		}

		/// <summary>
		/// Fires VAMMoan's "Voice perpetual orgasm" action (arousal 6.0 in the
		/// plugin), which loops the climax indefinitely.  The state machine calls
		/// this repeatedly while Mood.OrgasmState is active, so VAMMoan keeps
		/// the loop going.
		/// </summary>
		public void SetPerpetualOrgasm()
		{
			SetAction(p_.perpetualOrgasm, false);
			Fire();
		}

		public void SetKissing()
		{
			SetAction(p_.kissing, false);
			Fire();
		}

		public void SetBJ(float v)
		{
			if (p_.bjEnabled.Check())
			{
				// >= v20: plugin handles BJ sounds natively; just keep breathing
				SetAction(p_.breathing, true);
			}
			else
			{
				// < v20 legacy path
				if (p_.bjIntensities.Length == 0)
					return;

				int index = (int)(v * p_.bjIntensities.Length);
				index = U.Clamp(index, 0, p_.bjIntensities.Length - 1);

				SetAction(p_.bjIntensities[index], false);
			}

			Fire();
		}

		// ─────────────────────────────────────────────────────────────
		// IVoice – body animation toggles
		// ─────────────────────────────────────────────────────────────

		public bool MouthEnabled
		{
			get { return p_.autoJaw.Value; }
			set { p_.autoJaw.Value = value; }
		}

		public bool ChestEnabled
		{
			get { return p_.breathingEnabled.Value; }
			set { p_.breathingEnabled.Value = value; }
		}

		// ─────────────────────────────────────────────────────────────
		// IVoice – warning / lifecycle
		// ─────────────────────────────────────────────────────────────

		public string Warning
		{
			get { return warning_; }
		}

		public void Destroy()
		{
			// no-op
		}

		// ─────────────────────────────────────────────────────────────
		// Private helpers
		// ─────────────────────────────────────────────────────────────

		private void CheckVoice(bool force = false)
		{
			CheckVersion();

			var v = p_.voice.Value;

			if (force || v != voice_)
			{
				if (p_.hasAvailableIntensities)
				{
					float c = p_.availableIntensities.Value;
					intensitiesCount_ = U.Clamp((int)c, 0, p_.intensities.Length);
				}
				else
				{
					intensitiesCount_ = p_.intensities.Length;
				}

				Fire(true);
				SetOptimalJaw();
				voice_ = v;
			}
		}

		private void CheckVersion()
		{
			warning_ = "";

			if (p_.voice.Check())
			{
				if (p_.availableIntensities.Check())
				{
					p_.hasAvailableIntensities = true;
				}
				else
				{
					warning_ = "VAMMoan 11 or above required";
					p_.hasAvailableIntensities = false;
				}
			}
			else
			{
				warning_ = "VAMMoan missing";
			}
		}

		private Sys.Vam.ActionParameter[] GetIntensities()
		{
			var actions = new List<Sys.Vam.ActionParameter>();

			for (int i = 0; i < 5; ++i)
				actions.Add(AP($"Voice intensity {i}"));

			return actions.ToArray();
		}

		private void SetAction(Sys.Vam.ActionParameter a, bool bj)
		{
			currentAction_ = a;
			p_.bjEnabled.Value = bj;
		}

		private void Fire(bool force = false)
		{
			if (currentAction_ == null)
				return;

			var n = currentAction_.ParameterName;

			if (lastAction_ != n || force)
			{
				lastAction_ = n;
				log_.Info($"setting to '{n}'");
				currentAction_.Fire();
			}
		}

		// ─────────────────────────────────────────────────────────────
		// Parameter factory helpers
		// ─────────────────────────────────────────────────────────────

		private Sys.Vam.BoolParameter BP(string name)
		{
			return new Sys.Vam.BoolParameter(person_, PluginName, name);
		}

		private Sys.Vam.StringChooserParameter SCP(string name)
		{
			return new Sys.Vam.StringChooserParameter(person_, PluginName, name);
		}

		private Sys.Vam.ActionParameter AP(string name)
		{
			return new Sys.Vam.ActionParameter(person_, PluginName, name);
		}

		private Sys.Vam.FloatParameter FP(string name)
		{
			return new Sys.Vam.FloatParameter(person_, PluginName, name);
		}

		public override string ToString()
		{
			return $"VAMMoan v={voice_} i={lastAction_}";
		}
	}
}