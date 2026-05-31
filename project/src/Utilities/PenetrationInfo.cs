using System;
using System.Collections.Generic;
using Foost.Utils;
using UnityEngine;

namespace Foost {
namespace Penetration {

public class PenetrationStorables
{
    private MVRScript _script;
    public string prefix { get; private set; }

    public JSONStorableString penetratedAtom;
    public JSONStorableString penetratedOrifice;
    public JSONStorableFloat penetratedOrificeId;
    public JSONStorableString targetAtom;
    public JSONStorableString targetOrifice;
    public JSONStorableFloat targetOrificeId;
    public JSONStorableBool penetrating;
    public JSONStorableFloat penetrationFactor;
    public JSONStorableFloat penetrationDepth;
    public JSONStorableFloat penetrationGirth;
    public JSONStorableFloat penetratorLength;
    public JSONStorableFloat penetrationSpeed;
    public JSONStorableFloat penetrationMaxSpeed;
    public JSONStorableFloat penetrationStrength;

    public PenetrationStorables(MVRScript script, string prefix = "penetration")
    {
        _script = script;
        this.prefix = prefix;
        penetrating = new JSONStorableBool($"{prefix}:penetrating", false);
        penetratedAtom = new JSONStorableString($"{prefix}:atom", "");
        penetratedOrifice = new JSONStorableString($"{prefix}:orifice", "");
        penetratedOrificeId = new JSONStorableFloat($"{prefix}:orificeId", 0.0f, 0.0f, PenetrationInfo.ORIFICE_COUNT, true, false);
        targetAtom = new JSONStorableString($"{prefix}:targetAtom", "");
        targetOrifice = new JSONStorableString($"{prefix}:targetOrifice", "");
        targetOrificeId = new JSONStorableFloat($"{prefix}:targetOrificeId", 0.0f, 0.0f, PenetrationInfo.ORIFICE_COUNT, true, false);
        penetrationFactor = new JSONStorableFloat($"{prefix}:factor", 0.0f, 0.0f, 1.0f, true, false);
        penetrationDepth = new JSONStorableFloat($"{prefix}:depth", 0.0f, 0.0f, 30.0f, false, false);
        penetrationGirth = new JSONStorableFloat($"{prefix}:girth", 0.0f, 0.0f, 30.0f, false, false);
        penetratorLength = new JSONStorableFloat($"{prefix}:length", 0.0f, 0.0f, 30.0f, false, false);
        penetrationSpeed = new JSONStorableFloat($"{prefix}:speed", 0.0f, -100.0f, 100.0f, false, false);
        penetrationMaxSpeed = new JSONStorableFloat($"{prefix}:maxSpeed", 0.0f, 0.0f, 200.0f, false, false);
        penetrationStrength = new JSONStorableFloat($"{prefix}:strength", 0.0f, 0.0f, 100.0f, false, false);

        script.RegisterNoStore(penetrating);
        script.RegisterNoStore(penetratedAtom);
        script.RegisterNoStore(penetratedOrifice);
        script.RegisterNoStore(penetratedOrificeId);
        script.RegisterNoStore(targetAtom);
        script.RegisterNoStore(targetOrifice);
        script.RegisterNoStore(targetOrificeId);
        script.RegisterNoStore(penetrationFactor);
        script.RegisterNoStore(penetrationDepth);
        script.RegisterNoStore(penetrationGirth);
        script.RegisterNoStore(penetratorLength);
        script.RegisterNoStore(penetrationSpeed);
        script.RegisterNoStore(penetrationMaxSpeed);
        script.RegisterNoStore(penetrationStrength);
    }

    public void Reset()
    {
        if (_script == null) {
            return;
        }

        _script.DeregisterBool(penetrating);
        _script.DeregisterString(penetratedAtom);
        _script.DeregisterString(penetratedOrifice);
        _script.DeregisterFloat(penetratedOrificeId);
        _script.DeregisterString(targetAtom);
        _script.DeregisterString(targetOrifice);
        _script.DeregisterFloat(targetOrificeId);
        _script.DeregisterFloat(penetrationFactor);
        _script.DeregisterFloat(penetrationDepth);
        _script.DeregisterFloat(penetrationGirth);
        _script.DeregisterFloat(penetratorLength);
        _script.DeregisterFloat(penetrationSpeed);
        _script.DeregisterFloat(penetrationMaxSpeed);
        _script.DeregisterFloat(penetrationStrength);
    }
}

public class PenetrationInfo
{
    // Keep the indices are compatible with BL. It also uses 3 for hands and 4 for cleavage, which to us means "no penetration".
    // Note that when used as orificeId in storable/trigger context, 0 is used for "none" and other values are shifted up by one.
    public const int ANUS = 0;
    public const int VAGINA = 1;
    public const int MOUTH = 2;
    public const int ORIFICE_COUNT = 3;
    public static readonly List<string> orificeIdChoices = new List<string>() { "None", "Anus", "Vagina", "Mouth" };

    public const int MODE_PENETRATOR = 0;
    public const int MODE_PENETRATED = 1;

    public bool manualTarget;
    public Atom targetPerson;
    public int targetOrificeIndex;
    public int targetOrificeId { get { return ((targetOrificeIndex >= 0) && (targetOrificeIndex < ORIFICE_COUNT)) ? (targetOrificeIndex + 1) : 0; } }
    public Atom penetratedPerson;
    public int penetratedOrificeIndex;
    public int penetratedOrificeId { get { return ((penetratedOrificeIndex >= 0) && (penetratedOrificeIndex < ORIFICE_COUNT)) ? (penetratedOrificeIndex + 1) : 0; } }
    public bool penetrating;
    public float depth; // depth in metres
    public float depthFactor; // 0-1 depth factor
    public float girth; // penetration circumference in m
    public float length; // total dildo's shaft length
    public float timeout;

    private const float DEPTH_HISTORY_SECONDS = 1.0f;
    private const float SPEED_WINDOW_SECONDS = 0.1f;
    private const int MAXSPEED_SAMPLE_COUNT = 3; // hardcoded in code too, edit UpdateDepthHistory if changed
    private float _timeOverflow; // used for interpolating fixed samples from normal Update
    private float[] _depthHistory; // at fixedDeltaTime but updated from normal Update
    private float _previousDepth;
    private float _depthSmoothFactor;
    private int _nextDepthIndex;
    private int _depthCount;
    public float speed; // signed, m/s
    public float maxSpeed; // unsigned, m/s
    public float strength; // unsigned, m/s

    public int mode { get; private set; }
    public string name { get; private set; }
    public PenetrationStorables storables { get; private set; }

    public PenetrationInfo(int mode, string name)
    {
        this.mode = mode;
        this.name = name;
    }

    public void Reset()
    {
        ResetInfo();
        _depthHistory = null;
        DisableStorables();
    }

    public void EnableStorables(MVRScript script, string prefix = "penetration")
    {
        if (storables != null) {
            return;
        }
        storables = new PenetrationStorables(script, prefix);
    }

    public void DisableStorables()
    {
        if (storables != null) {
            storables.Reset();
            storables = null;
        }
    }

    public static int GetOrificeIndex(string name)
    {
        if (name == "Anus") {
            return ANUS;
        }
        if (name == "Vagina") {
            return VAGINA;
        }
        if (name == "Mouth") {
            return MOUTH;
        }
        return ORIFICE_COUNT;
    }

    public static string GetOrificeName(int index, string noneName = "None")
    {
        if (index == ANUS) {
            return "Anus";
        }
        if (index == VAGINA) {
            return "Vagina";
        }
        if (index == MOUTH) {
            return "Mouth";
        }
        return noneName;
    }

    public PersonOrifice Update(PenetrationMonitor monitor, Vector3 tipPosition, float maxDistance, float maxTime)
    {
        PersonOrifice result = UpdateOrifice(monitor, tipPosition, maxDistance, maxTime);
        UpdateDepthHistory();

        if (storables != null) {
            storables.penetrating.val = penetrating;
            storables.penetratedAtom.val = (penetratedPerson != null) ? penetratedPerson.uid : "";
            storables.penetratedOrifice.val = GetOrificeName(penetratedOrificeIndex, "");
            storables.penetratedOrificeId.val = penetratedOrificeId;
            storables.targetAtom.val = (targetPerson != null) ? targetPerson.uid : "";
            storables.targetOrifice.val = GetOrificeName(targetOrificeIndex, "");
            storables.targetOrificeId.val = targetOrificeId;
            storables.penetrationFactor.val = depthFactor;
            storables.penetrationDepth.val = depth * 100.0f;
            storables.penetrationGirth.val = girth * 100.0f;
            storables.penetratorLength.val = length * 100.0f;
            storables.penetrationSpeed.val = speed * 100.0f;
            storables.penetrationMaxSpeed.val = maxSpeed * 100.0f;
            storables.penetrationStrength.val = strength * 100.0f;
        }

        return result;
    }

    private PersonOrifice UpdateOrifice(PenetrationMonitor monitor, Vector3 tipPosition, float maxDistance, float maxTime)
    {
        if (monitor == null) {
            ResetNonManual();
            return null;
        }

        // A bit of consistency.

        if ((targetOrificeIndex < 0) || (targetOrificeIndex > ORIFICE_COUNT)) {
            targetOrificeIndex = ORIFICE_COUNT;
        }
        if ((targetPerson == null) && (targetOrificeIndex != ORIFICE_COUNT)) {
            targetOrificeIndex = ORIFICE_COUNT;
        }

        // Update currently penetrated orifice.

        PenetrationState penetration = monitor.penetration;
        length = penetration.length;
        if (penetration.penetrating) {
            if (manualTarget) {

                // Using manual target, verify if we are actually penetrating that target.

                penetrating = (monitor.GetPerson(penetration).atom == targetPerson) && (penetration.orificeIndex == targetOrificeIndex);
                if (penetrating) {
                    penetratedPerson = targetPerson;
                    penetratedOrificeIndex = targetOrificeIndex;
                    depth = penetration.depth;
                    depthFactor = penetration.depthFactor;
                    girth = penetration.girth;
                }
                else {
                    penetratedPerson = null;
                    penetratedOrificeIndex = ORIFICE_COUNT;
                    depth = 0.0f;
                    depthFactor = 0.0f;
                    girth = 0.0f;
                }
            }
            else {
                // Automatic target, use it.

                penetrating = true;
                penetratedPerson = monitor.GetPerson(penetration).atom;
                penetratedOrificeIndex = penetration.orificeIndex;
                targetPerson = penetratedPerson;
                targetOrificeIndex = penetratedOrificeIndex;
                depth = penetration.depth;
                depthFactor = penetration.depthFactor;
                girth = penetration.girth;
                timeout = maxTime;
            }
        }
        else {
            penetrating = false;
            penetratedPerson = null;
            penetratedOrificeIndex = ORIFICE_COUNT;
            depth = 0.0f;
            depthFactor = 0.0f;
            girth = 0.0f;
        }

        // If the target is manual, or penetrating (so target==penetrated), or there is not target, just return the target.

        PersonOrifice targetOrifice = monitor.GetTargetOrifice(this);
        if (manualTarget || penetrating || (targetOrifice == null)) {
            return targetOrifice;
        }

        // The target is automatic and valid, but no longer penetrating. Check if we should retain the target.

        Vector3 orificePos = targetOrifice.midObject.transform.position;
        float distanceSq = (orificePos - tipPosition).sqrMagnitude;
        if (distanceSq > maxDistance * maxDistance) {
            ResetTarget();
            return null;
        }

        timeout -= Time.deltaTime;
        if (timeout < 0.0f) {
            ResetTarget();
            return null;
        }

        return targetOrifice;
    }

    private void UpdateDepthHistory()
    {
        if (Time.inFixedTimeStep) {
            throw new Exception("Penetration update tick should be the regular sim tick, not fixed");
            // but the depth history is interpolated to fixed ticks for easier storage and math
        }
        float delta = Time.deltaTime;
        float fixedDelta = Time.fixedDeltaTime;
        float invFixedDelta = 1.0f / fixedDelta;

        int maxCount = Math.Max(2, Mathf.RoundToInt(DEPTH_HISTORY_SECONDS * invFixedDelta));
        if ((_depthHistory == null) || (_depthHistory.Length != maxCount)) {
            _depthHistory = new float[maxCount];
            _depthSmoothFactor = Mathf.Exp(-5.0f * Time.fixedDeltaTime);
            _nextDepthIndex = 0;
            _depthCount = 0;
            _timeOverflow = 0.0f;
            _previousDepth = 0.0f;
        }
        int speedWindowSize = Math.Max(MAXSPEED_SAMPLE_COUNT, Mathf.RoundToInt(SPEED_WINDOW_SECONDS * invFixedDelta));

        //  0   _to                       delta
        // -+----|-------|-------|-------|--+----|
        //  ^prevDepth   ^new samples            ^ - delta = newTo

        float sampleTime = _timeOverflow;
        while (sampleTime < delta) {
            float timeFactor = sampleTime / delta;
            _depthHistory[_nextDepthIndex++] = Mathf.Lerp(_previousDepth, depth, timeFactor);
            if (_nextDepthIndex >= maxCount) {
                _nextDepthIndex = 0;
            }
            if (_depthCount < maxCount) {
                ++_depthCount;
            }
            sampleTime += fixedDelta;
        }
        _previousDepth = depth;
        _timeOverflow = sampleTime - delta;

        if (_depthCount < speedWindowSize) {
            return;
        }

        float pushSpeed0 = 0.0f;
        float pushSpeed1 = 0.0f;
        float targetMaxSpeed = 0.0f;
        float speedNum = 0.0f;
        float speedDenom = 0.0f;
        float avgNum = 0.0f;
        float avgDenom = 0.0f;
        int depthi = _nextDepthIndex - 1;
        if (depthi < 0) {
            depthi = maxCount - 1;
        }
        for (int i = 0; i < _depthCount - 1; ++i) {
            int previ = depthi - 1;
            if (previ < 0) {
                previ = maxCount - 1;
            }
            float speedSample = (_depthHistory[depthi] - _depthHistory[previ]) * invFixedDelta;
            float pushSpeedSample = Mathf.Max(0.0f, speedSample);

            if (i >= MAXSPEED_SAMPLE_COUNT) {
                float maxSpeed = (pushSpeed0 + pushSpeed1 + pushSpeedSample) / MAXSPEED_SAMPLE_COUNT;
                targetMaxSpeed = Mathf.Max(targetMaxSpeed, maxSpeed);
                pushSpeed0 = pushSpeed1;
                pushSpeed1 = pushSpeedSample;
            }
            if (i < speedWindowSize) {
                speedNum += speedSample * (speedWindowSize - i);
                speedDenom += speedWindowSize - i;
            }

            avgNum += pushSpeedSample * (_depthCount - i);
            avgDenom += _depthCount - i;

            depthi = previ;
        }

        speed = speedNum / speedDenom;
        strength = avgNum / avgDenom;

        maxSpeed = _depthSmoothFactor * maxSpeed + (1.0f - _depthSmoothFactor) * targetMaxSpeed;
    }

    private void ResetInfo()
    {
        manualTarget = false;
        targetPerson = null;
        targetOrificeIndex = ORIFICE_COUNT;
        penetratedPerson = null;
        penetratedOrificeIndex = ORIFICE_COUNT;
        penetrating = false;
        depth = 0.0f;
        depthFactor = 0.0f;
        girth = 0.0f;
        length = 0.0f;
    }

    private void ResetNonManual()
    {
        if (manualTarget) {
            penetratedPerson = null;
            penetratedOrificeIndex = ORIFICE_COUNT;
            penetrating = false;
            depth = 0.0f;
            depthFactor = 0.0f;
            girth = 0.0f;
            length = 0.0f;
        }
        else {
            ResetInfo();
        }
    }

    private void ResetTarget()
    {
        targetPerson = null;
        targetOrificeIndex = ORIFICE_COUNT;
    }
}

} // namespace Foost.Penetration
} // namespace Foost
