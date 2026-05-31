using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cue
{
    public class PenetrationStats
    {
        public bool Active;
        public float NormalisedDepth;
        public float NormalisedSpeed;
        public float DwellTime;
        public float InsertionDepthMetres;
        public float CurveParameter;
        public float ApproachAngleDegrees;
        public float SmoothedVelocity;
        public int EntryFrameCount;
        public bool WasActive;
        public Atom DetectedAtom;
        public string DetectedAtomName;

        public void Reset()
        {
            WasActive = Active;
            Active = false;
            NormalisedDepth = 0f;
            NormalisedSpeed = 0f;
            InsertionDepthMetres = 0f;
            CurveParameter = 0f;
            ApproachAngleDegrees = 0f;
            SmoothedVelocity = 0f;
            DetectedAtom = null;
            DetectedAtomName = null;
        }
    }
}