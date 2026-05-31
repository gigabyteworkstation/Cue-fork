using Cue.Sys;
using UnityEngine;

namespace Cue.DildoLanguage
{
	class PenetrationReader
	{
		private const string PluginName = "DildoLanguage";
		private const string PenPrefix  = "penetration";
		private const float  MaxSpeedMs = 0.4f;
		private const float  SmoothTime = 0.12f;

		private readonly Person person_;
		private readonly Logger log_;

		private IAtom penetratorAtom_ = null;

		private Sys.Vam.BoolParameter    penetrating_;
		private Sys.Vam.FloatParameter   depthFactor_;
		private Sys.Vam.FloatParameter   depthCm_;
		private Sys.Vam.FloatParameter   speedCmS_;
		private Sys.Vam.StringParameter  penetratedAtom_;

		private bool  available_    = false;
		private float smoothVelRef_ = 0f;

		private readonly PenetrationStats stats_ = new PenetrationStats();

		public PenetrationReader(Person p)
		{
			person_ = p;
			log_    = new Logger(Logger.Integration, p, "dildoLanguage");
		}

		public PenetrationStats Stats      { get { return stats_; } }
		public bool             Available  { get { return available_; } }

		public void SetPenetratorAtom(IAtom a)
		{
			if (a == penetratorAtom_) return;
			penetratorAtom_ = a;
			available_      = false;
			TryResolve();
		}

		public void Update(float s)
		{
			if (!available_)
			{
				stats_.Reset();
				return;
			}

			stats_.WasActive = stats_.Active;

			if (!penetrating_.Check() || !penetrating_.Value)
			{

				if (stats_.Active) stats_.Reset();
				smoothVelRef_ = 0f;
				return;
			}

			float depth  = depthFactor_.Value;
			float depthM = depthCm_.Value * 0.01f;
			float rawVel = speedCmS_.Value * 0.01f / MaxSpeedMs;

			stats_.Active               = true;
			stats_.NormalisedDepth      = Mathf.Clamp01(depth);
			stats_.InsertionDepthMetres = depthM;
			stats_.CurveParameter       = depth;
			stats_.DetectedAtomName     = penetratedAtom_.Value;

			stats_.SmoothedVelocity = Mathf.SmoothDamp(
				stats_.SmoothedVelocity, rawVel, ref smoothVelRef_, SmoothTime);
			stats_.NormalisedSpeed = Mathf.Clamp01(Mathf.Abs(stats_.SmoothedVelocity));

			if (!stats_.WasActive)
			{
				stats_.EntryFrameCount = 0;
				stats_.DwellTime       = 0f;
			}

			stats_.EntryFrameCount++;
			stats_.DwellTime += s;
		}

		public void Debug(DebugLines debug)
		{
			debug.Add("dl.available",  available_.ToString());
			debug.Add("dl.atom",       penetratorAtom_?.ID ?? "none");
			debug.Add("dl.active",     stats_.Active.ToString());
			debug.Add("dl.depth",      stats_.NormalisedDepth.ToString("0.00"));
			debug.Add("dl.depthM",     stats_.InsertionDepthMetres.ToString("0.000") + "m");
			debug.Add("dl.vel",        stats_.SmoothedVelocity.ToString("+0.00;-0.00;0.00"));
			debug.Add("dl.speed",      stats_.NormalisedSpeed.ToString("0.00"));
		}

		private void TryResolve()
		{
			available_ = false;

			if (penetratorAtom_ == null)
			{
				log_.Error("DildoLanguage penetratorAtom is null");
				return;
			}

			penetrating_    = new Sys.Vam.BoolParameter(   penetratorAtom_, PluginName, PenPrefix + ":penetrating");
			depthFactor_    = new Sys.Vam.FloatParameter(  penetratorAtom_, PluginName, PenPrefix + ":factor");
			depthCm_        = new Sys.Vam.FloatParameter(  penetratorAtom_, PluginName, PenPrefix + ":depth");
			speedCmS_       = new Sys.Vam.FloatParameter(  penetratorAtom_, PluginName, PenPrefix + ":speed");
			penetratedAtom_ = new Sys.Vam.StringParameter( penetratorAtom_, PluginName, PenPrefix + ":atom");

			if (!penetrating_.Check() || !depthFactor_.Check())
			{
				log_.Error("DildoLanguage found but penetrator storables missing on " + penetratorAtom_.ID);
				return;
			}

			available_ = true;
			log_.Error("DildoLanguage integration active on " + penetratorAtom_.ID);
		}
	}
}