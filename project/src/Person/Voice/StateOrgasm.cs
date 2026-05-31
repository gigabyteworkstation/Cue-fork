using SimpleJSON;

namespace Cue
{
	public class VoiceStateOrgasm : BasicVoiceState
	{
		private const int OrgasmAction          = 1;
		private const int MoaningAction          = 2;
		private const int PerpetualOrgasmAction  = 3;  // sustained loop; uses SetPerpetualOrgasm()

		private int action_ = OrgasmAction;
		private float moaningIntensity_ = 0;

		private VoiceStateOrgasm()
		{
		}

		public VoiceStateOrgasm(JSONClass vo)
		{
			Load(vo, false);
		}

		protected override void DoLoad(JSONClass o, bool inherited)
		{
			string v = J.OptString(o, "voice", "");

			if (v == "orgasm")
			{
				action_ = OrgasmAction;
			}
			else if (v == "moaning")
			{
				action_ = MoaningAction;

				if (o.HasKey("moaningIntensity"))
					moaningIntensity_ = J.ReqFloat(o, "moaningIntensity");
				else if (!inherited)
					throw new LoadFailed("missing moaningIntensity");
			}
			else if (v == "perpetualOrgasm")
			{
				// Fires SetPerpetualOrgasm() which maps to VAMMoan "Voice perpetual orgasm".
				// Providers that do not support this (e.g. MacGruber) fall back to
				// SetOrgasm() inside their own implementation, so this is safe for all.
				action_ = PerpetualOrgasmAction;
			}
			else if (v != "")
			{
				throw new LoadFailed(
					"bad orgasmState voice, must be 'orgasm', 'moaning', or 'perpetualOrgasm'");
			}
		}

		public override string Name
		{
			get { return "orgasmState"; }
		}

		public override IVoiceState Clone()
		{
			var s = new VoiceStateOrgasm();
			s.CopyFrom(this);
			return s;
		}

		private void CopyFrom(VoiceStateOrgasm o)
		{
			base.CopyFrom(o);
			action_ = o.action_;
			moaningIntensity_ = o.moaningIntensity_;
		}

		protected override void DoStart()
		{
			switch (action_)
			{
				case OrgasmAction:
					v_.Provider.SetOrgasm();
					break;

				case PerpetualOrgasmAction:
					v_.Provider.SetPerpetualOrgasm();
					break;

				case MoaningAction:
					v_.Provider.SetMoaning(moaningIntensity_);
					break;
			}
		}

		protected override void DoUpdate(float s)
		{
			// For perpetual orgasm we re-fire every update so VAMMoan keeps
			// looping it rather than waiting for the one-shot clip to end.
			if (action_ == PerpetualOrgasmAction)
				v_.Provider.SetPerpetualOrgasm();

			if (v_.Person.Mood.State != Mood.OrgasmState)
				SetDone();
		}

		protected override int DoCanRun()
		{
			if (v_.Person.Mood.State == Mood.OrgasmState)
			{
				SetLastState("ok");
				return Emergency;
			}

			SetLastState("orgasm not active");
			return CannotRun;
		}

		protected override void DoDebug(DebugLines debug)
		{
			string actionName;
			switch (action_)
			{
				case OrgasmAction:         actionName = "orgasm";          break;
				case PerpetualOrgasmAction: actionName = "perpetualOrgasm"; break;
				default:                   actionName = "moaning";          break;
			}

			debug.Add("action", actionName);
			debug.Add("moaning", $"{moaningIntensity_:0.00}");
		}
	}
}