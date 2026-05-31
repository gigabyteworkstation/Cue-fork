using System.Collections.Generic;
using System.Linq;
using Foost.Utils;
using UnityEngine;

namespace Foost {
namespace Penetration {

public class PersonOrificeCorner
{
    public Transform transform;
    public Vector3 offset;
    public float midPointWeight;
    public GameObject go;
    public Color debugColor;
    public float debugSize;

    public PersonOrificeCorner(Transform t, Vector3 o, float w)
    {
        transform = t;
        offset = o;
        midPointWeight = w;
        //SuperController.LogMessage($"tcorner {transform.name} <- {transform.parent.name}, offset {offset.ToString("F3")}, relpos {transform.localPosition.ToString("F3")}");
    }

    public PersonOrificeCorner(Transform t, float w) : this(t, Vector3.zero, w)
    {
    }

    public PersonOrificeCorner(Collider c, float w)
    {
        CapsuleCollider cc = c as CapsuleCollider;
        transform = c.transform;
        if (cc != null) {
            offset = cc.center;
        }
        else {
            offset = Vector3.zero;
        }
        midPointWeight = w;
        //SuperController.LogMessage($"ccorner {transform.name} <- {transform.parent.name} <- {transform.parent.parent.name}, offset {offset.ToString("F3")}, relpos {transform.localPosition.ToString("F3")} <- {transform.parent.localPosition.ToString("F3")}");
    }

    public void Initialize(string name, int debugId, float debugAlpha)
    {
        go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offset;
        debugColor = (debugId == 0) ? new Color(1.0f, 0.0f, 0.0f, debugAlpha) : (debugId == 1) ? new Color(0.0f, 1.0f, 0.0f, debugAlpha) : new Color(0.0f, 0.0f, 1.0f, debugAlpha);
        debugSize = 0.005f;
    }
}

public class PersonOrifice
{
    public readonly int id;
    public PersonOrificeCorner[] corners; // ccw order, if 4 the second plane is 3-1-0 (also ccw)
    public GameObject midObject;
    public float midPointSum;
    public Plane plane1;
    public Plane plane2;
    public bool isDualPlane { get { return corners.Length > 3;} }
    public bool isConvex { get; private set; } = true; /* from the pov of the body - in general mouth is concave, vagina is convex */
    public Color debugColor;
    public float debugSize;
    public bool debugDrawEnabled;

    public PersonOrifice(int id, PersonOrificeCorner c1, PersonOrificeCorner c2, PersonOrificeCorner c3)
    {
        this.id = id;
        corners = new PersonOrificeCorner[3]{ c1, c2, c3 };

        float alpha = 0.1f;
        for (int i = 0; i < 3; i++) {
            corners[i].Initialize($"{PenetrationInfo.GetOrificeName(id)}_{i}", i, alpha);
        }
        InitializeMidPoint(c1.midPointWeight + c2.midPointWeight + c3.midPointWeight, alpha);
        Update();
    }

    public PersonOrifice(int id, PersonOrificeCorner c1, PersonOrificeCorner c2, PersonOrificeCorner c3, PersonOrificeCorner c4)
    {
        this.id = id;
        corners = new PersonOrificeCorner[4]{ c1, c2, c3, c4 };

        float alpha = 0.1f;
        for (int i = 0; i < 4; i++) {
            corners[i].Initialize($"{PenetrationInfo.GetOrificeName(id)}_{i}", i <= 2 ? i : 2, alpha);
        }
        InitializeMidPoint(c1.midPointWeight + c2.midPointWeight + c3.midPointWeight + c4.midPointWeight, alpha);
        Update();
    }

    public PersonOrifice(int id, PersonOrificeCorner c1a, PersonOrificeCorner c1b, PersonOrificeCorner c2a, PersonOrificeCorner c2b, PersonOrificeCorner c3, PersonOrificeCorner c4)
    {
        this.id = id;
        corners = new PersonOrificeCorner[6]{ c1a, c2a, c3, c4, c1b, c2b };

        float alpha = 0.1f;
        for (int i = 0; i < 6; i++) {
            corners[i].Initialize($"{PenetrationInfo.GetOrificeName(id)}_{i}", (i == 0 || i == 4) ? 0 : (i == 1 || i == 5) ? 1 : 2, alpha);
        }
        InitializeMidPoint(c1a.midPointWeight + c2a.midPointWeight + c3.midPointWeight + c4.midPointWeight, alpha);
        Update();
    }

    private void InitializeMidPoint(float weightSum, float debugAlpha)
    {
        midObject = new GameObject($"{PenetrationInfo.GetOrificeName(id)}_mid");
        midPointSum = weightSum;
        debugColor = new Color(1.0f, 0.92f, 0.016f, debugAlpha);
        debugSize = 0.01f;
    }

    public void Reset()
    {
        for (int i = 0; i < corners.Length; i++) {
            if (corners[i] != null) {
                UnityEngine.Object.Destroy(corners[i].go);
                corners[i] = null;
            }
        }
        if (midObject != null) {
            UnityEngine.Object.Destroy(midObject);
            midObject = null;
        }
    }

    public void Update()
    {
        Vector3 p0 = corners[0].transform.position + corners[0].transform.TransformDirection(corners[0].offset);
        Vector3 p1 = corners[1].transform.position + corners[1].transform.TransformDirection(corners[1].offset);
        if (corners.Length > 4) {
            Vector3 p0b = corners[4].transform.position + corners[4].transform.TransformDirection(corners[4].offset);
            p0 = ((1.0f - corners[4].midPointWeight) * p0) + (corners[4].midPointWeight * p0b);
            Vector3 p1b = corners[5].transform.position + corners[5].transform.TransformDirection(corners[5].offset);
            p1 = ((1.0f - corners[5].midPointWeight) * p1) + (corners[5].midPointWeight * p1b);
        }
        Vector3 p2 = corners[2].transform.position + corners[2].transform.TransformDirection(corners[2].offset);
        plane1.Set3Points(p2, p1, p0);

        if (isDualPlane) {
            Vector3 p3 = corners[3].transform.position + corners[3].transform.TransformDirection(corners[3].offset);
            plane2.Set3Points(p3, p0, p1);
            midObject.transform.position = (
                corners[0].midPointWeight * p0 +
                corners[1].midPointWeight * p1 +
                corners[2].midPointWeight * p2 +
                corners[3].midPointWeight * p3
            ) / midPointSum;
            isConvex = ! plane1.GetSide(p3);
        }
        else {
            midObject.transform.position = (
                corners[0].midPointWeight * p0 +
                corners[1].midPointWeight * p1 +
                corners[2].midPointWeight * p2
            ) / midPointSum;
            isConvex = true;
        }

        if (debugDrawEnabled) {
            for (int i = 0; i < corners.Length; ++i) {
                DebugDraw.Cross(corners[i].go.transform, corners[i].debugColor, corners[i].debugSize);
            }
            DebugDraw.Cross(midObject.transform, debugColor, debugSize);
        }
    }

    public override string ToString()
    {
        return PenetrationInfo.GetOrificeName(id);
    }
}

public class PersonInfo
{
    public Atom atom;
    public int id;
    public PersonOrifice[] orifices = new PersonOrifice[PenetrationInfo.ORIFICE_COUNT];
    public PersonOrifice vagina { get { return orifices[PenetrationInfo.VAGINA]; } }
    public PersonOrifice anus { get { return orifices[PenetrationInfo.ANUS]; } }
    public PersonOrifice mouth { get { return orifices[PenetrationInfo.MOUTH]; } }

    public PersonInfo(Atom theAtom, int theId)
    {
        atom = theAtom;
        id = theId;

        Rigidbody[] rbs = atom.rigidbodies;
        Rigidbody vaginaLeftRb = rbs.FirstOrDefault((x) => x.name == "_JointGl");
        Rigidbody vaginaRightRb = rbs.FirstOrDefault((x) => x.name == "_JointGr");
        Rigidbody anusLeftRb = rbs.FirstOrDefault(x => x.name == "_JointAl");
        Rigidbody anusRightRb = rbs.FirstOrDefault((x) => x.name == "_JointAr");
        Rigidbody anusVaginaSeparatorRb = rbs.First((x) => x.name == "_JointB");
        Rigidbody lowerJawRb = rbs.FirstOrDefault((x) => x.name == "lowerJaw");

        /*foreach (CapsuleCollider c in lowerJawRb.GetComponentsInChildren<CapsuleCollider>()) {
            SuperController.LogMessage($"LowerJaw {c.name} <- {c.transform.parent.name} <- {c.transform.parent.parent.name}, offset {c.center.ToString("F3")}, relpos {c.transform.localPosition.ToString("F3")} {c.transform.parent.localPosition.ToString("F3")}");
        }*/

        AutoCollider mouthTop = null;
        AutoCollider mouthTopLeft = null;
        AutoCollider mouthTopRight = null;
        AutoCollider clitoris = null;
        AutoColliderGroup[] acgs = atom.GetComponentsInChildren<AutoColliderGroup>();
        foreach (AutoColliderGroup acg in acgs) {
            if (acg.name.Contains("FaceHardLeft")) {
                /*foreach (AutoCollider c in acg.GetAutoColliders()) {
                    if ((c.hardCollider != null) && (c.hardCollider is CapsuleCollider)) {
                        CapsuleCollider cc = c.hardCollider as CapsuleCollider;
                        SuperController.LogMessage($"FaceLeft {c.name} <- {c.transform.parent.name} <- {c.transform.parent.parent.name}, jc {c.jointCollider != null}, hc true, offset {cc.center.ToString("F3")}, relpos {cc.transform.localPosition.ToString("F3")} {cc.transform.parent.localPosition.ToString("F3")}");
                    }
                    else {
                        SuperController.LogMessage($"FaceLeft {c.name} <- {c.transform.parent.name} <- {c.transform.parent.parent.name}, jc {c.jointCollider != null}, hc {c.hardCollider != null}, relpos {c.transform.localPosition.ToString("F3")} {c.transform.parent.localPosition.ToString("F3")}");
                    }
                }*/
                mouthTopLeft = acg.GetAutoColliders().FirstOrDefault((x) => x.name.Contains("16"));
            }
            else if (acg.name.Contains("FaceHardRight")) {
                mouthTopRight = acg.GetAutoColliders().FirstOrDefault((x) => x.name.Contains("16"));
            }
            if (acg.name.Contains("FaceCentral")) {
                mouthTop = acg.GetAutoColliders().FirstOrDefault((x) => x.name.EndsWith("FaceCentral2"));
            }
            else if (acg.name.Contains("pelvis")) {
                clitoris = acg.GetAutoColliders().FirstOrDefault((x) => x.name == "AutoColliderpelvisF1");
            }
        }

        /*
        ccorner Collider1 <- _JointGl <- pelvis, offset (0.000, 0.000, 0.000), relpos (0.000, 0.014, 0.010) <- (-0.011, -0.186, 0.009)
        ccorner Collider1 <- _JointGr <- pelvis, offset (0.000, 0.000, 0.000), relpos (0.000, 0.014, 0.010) <- (0.011, -0.186, 0.012)
        ccorner _JointB <- pelvis <- hip, offset (0.000, 0.006, 0.000), relpos (0.000, -0.180, -0.018) <- (0.000, 0.018, 0.018)
        ccorner _JointAr <- pelvis <- hip, offset (0.000, 0.014, 0.000), relpos (0.012, -0.169, -0.032) <- (0.000, 0.018, 0.018)
        ccorner _JointAl <- pelvis <- hip, offset (0.000, 0.014, 0.000), relpos (-0.012, -0.169, -0.032) <- (0.000, 0.018, 0.018)
        ccorner _JointB <- pelvis <- hip, offset (0.000, 0.006, 0.000), relpos (0.000, -0.180, -0.018) <- (0.000, 0.018, 0.018)
        FaceLeft AutoColliderAutoCollidersFaceHardLeft16 <- AutoCollidersFaceHardLeft <- AutoColliders, jc False, hc true, offset (0.000, 0.000, -0.002), relpos (-0.024, 0.007, 0.090) (0.000, 0.000, 0.000)
        FaceLeft AutoColliderAutoCollidersFaceHardLeft17 <- AutoCollidersFaceHardLeft <- AutoColliders, jc False, hc true, offset (0.000, 0.000, -0.002), relpos (-0.012, 0.005, 0.100) (0.000, 0.000, 0.000)
        LowerJaw _ColliderL3r <- lowerJawStandardColliders <- lowerJaw, offset (0.000, 0.000, 0.000), relpos (0.043, 0.019, 0.046) (0.000, 0.000, 0.000)
        LowerJaw _ColliderL3l <- lowerJawStandardColliders <- lowerJaw, offset (0.000, 0.000, 0.000), relpos (-0.043, 0.019, 0.046) (0.000, 0.000, 0.000)
        LowerJaw _ColliderLipM <- lowerJawStandardColliders <- lowerJaw, offset (0.000, 0.000, 0.000), relpos (0.000, 0.007, 0.086) (0.000, 0.000, 0.000)
        */

        if ((vaginaLeftRb != null) && (vaginaRightRb != null) && (anusVaginaSeparatorRb != null) && (clitoris?.jointCollider != null)) {
            orifices[PenetrationInfo.VAGINA] = new PersonOrifice(
                PenetrationInfo.VAGINA,
                new PersonOrificeCorner(vaginaLeftRb.transform, new Vector3(0.0f, 0.0f, 0.005f), 1.0f),
                new PersonOrificeCorner(vaginaRightRb.transform, new Vector3(0.0f, 0.0f, 0.005f), 1.0f),
                new PersonOrificeCorner(anusVaginaSeparatorRb.transform, new Vector3(0.0f, 0.005f, 0.005f), 0.5f),
                new PersonOrificeCorner(clitoris.jointCollider, 0.5f)
            );
        }
        if ((anusLeftRb != null) && (anusRightRb != null) && (anusVaginaSeparatorRb != null)) {
            orifices[PenetrationInfo.ANUS] = new PersonOrifice(
                PenetrationInfo.ANUS,
                new PersonOrificeCorner(anusRightRb.transform, new Vector3(0.0f, 0.0f, 0.0f), 1.0f),
                new PersonOrificeCorner(anusLeftRb.transform, new Vector3(0.0f, 0.0f, 0.0f), 1.0f),
                new PersonOrificeCorner(anusVaginaSeparatorRb.transform, new Vector3(0.0f, 0.004f, -0.003f), 0.0f)
            );
        }
        if ((lowerJawRb != null) && (mouthTop?.hardCollider != null) && (mouthTopLeft?.hardCollider != null) && (mouthTopRight?.hardCollider != null)) {
            orifices[PenetrationInfo.MOUTH] = new PersonOrifice(
                PenetrationInfo.MOUTH,
                new PersonOrificeCorner(mouthTopRight.hardCollider.transform, new Vector3(-0.005f, 0.0f, -0.005f), 1.0f),
                new PersonOrificeCorner(lowerJawRb.transform, new Vector3(0.02f, 0.005f, 0.075f) /*right mouth corner, loosely based on ColliderL3r*/, 0.5f),
                new PersonOrificeCorner(mouthTopLeft.hardCollider.transform, new Vector3(0.005f, 0.0f, -0.005f), 1.0f),
                new PersonOrificeCorner(lowerJawRb.transform, new Vector3(-0.02f, 0.005f, 0.075f) /*left mouth corner, loosely based on ColliderL3l*/, 0.5f),
                new PersonOrificeCorner(mouthTop.hardCollider.transform, new Vector3(-0.005f, 0.0f, 0.005f), 1.0f),
                new PersonOrificeCorner(lowerJawRb.transform, new Vector3(0.0f, 0.007f, 0.086f) /*lower lip mid, loosely based on ColliderLipM*/, 1.0f)
            );
        }
    }

    public void Reset()
    {
        for (int i = 0; i < PenetrationInfo.ORIFICE_COUNT; i++) {
            if (orifices[i] != null) {
                orifices[i].Reset();
                orifices[i] = null;
            }
        }
    }

    public void Update()
    {
        for (int i = 0; i < PenetrationInfo.ORIFICE_COUNT; i++) {
            if (orifices[i] != null) {
                orifices[i].Update();
            }
        }
    }

    public bool debugDrawEnabled {
        get {
            if (anus != null) {
                return anus.debugDrawEnabled;
            }
            if (mouth != null) {
                return mouth.debugDrawEnabled;
            }
            return false;
        }
        set {
            for (int i = 0; i < PenetrationInfo.ORIFICE_COUNT; i++) {
                if (orifices[i] != null) {
                    orifices[i].debugDrawEnabled = value;
                }
            }
        }
    }
}

public class PenetrationState
{
    public const int INVALID_PERSON_ID = 0;

    public int personIndex;
    public int personId;
    public int orificeIndex;

    public bool useBlInfo;
    public bool penetrating;
    public float depth; // depth in metres
    public float depthFactor; // 0-1 depth factor
    public float girth; // penetration circumference in m
    public float length; // total dildo's shaft length

    public void Reset()
    {
        personIndex = 0;
        personId = INVALID_PERSON_ID;
        orificeIndex = 0;
        useBlInfo = false;
        penetrating = false;
        depth = 0.0f;
        depthFactor = 0.0f;
        girth = 0.0f;
        length = 0.0f;
    }

    public void Reset(int index, PersonInfo person)
    {
        personIndex = index;
        personId = person.id;
        orificeIndex = 0;
        useBlInfo = false;
        penetrating = false;
        depth = 0.0f;
        depthFactor = 0.0f;
        girth = 0.0f;
        length = 0.0f;
    }
}

// depth - penetration depth in m
// depthFactor - 0-1 factor, 0 meaning no penetration, 1 full penetration (not necessarily depth/dildoLength, might not be linear along the length)
// girth - penetration circumference in m
// dildoLength - current shaft length in m
// distance - distance of the penetration point from the orifice center point ; treat as no penetration if > maxDistance
public delegate bool CheckPenetration(out float depth, out float depthFactor, out float girth, out float length, out float distance, PersonOrifice orifice, float maxDistance);

public class PenetrationMonitor
{
    private readonly int _verbosity;
    private List<PersonInfo> _people = new List<PersonInfo>();
    private PenetrationState _penetration = new PenetrationState();
    private int _nextId = 1;
    private int _debugDraw = 0;
    private PersonInfo _debugDrawPerson = null;

    public PenetrationState penetration { get { return _penetration; } }

    public PenetrationMonitor(int verbosity)
    {
        _verbosity = verbosity;
    }

    public void Init()
    {
        SuperController.singleton.onAtomAddedHandlers += AtomAdded;
        SuperController.singleton.onAtomRemovedHandlers += AtomRemoved;

        foreach (Atom atom in SuperController.singleton.GetAtoms()) {
            AtomAdded(atom);
        }
    }

    public void Reset()
    {
        foreach (PersonInfo person in _people) {
            person.Reset();
        }
        _people.Clear();
        SuperController.singleton.onAtomAddedHandlers -= AtomAdded;
        SuperController.singleton.onAtomRemovedHandlers -= AtomRemoved;
    }

    public bool Update(CheckPenetration checkPenetration, string blAtomName, int blOrificeIndex)
    {
        foreach (PersonInfo person in _people) {
            person.Update();
        }

        bool result = UpdateOrifice(checkPenetration, blAtomName, blOrificeIndex);
        if (_debugDraw != 1) {
            return result;
        }

        PersonInfo newPerson = _penetration.penetrating ? GetPerson(_penetration) : null;
        if (newPerson == _debugDrawPerson) {
            return result;
        }

        if (_debugDrawPerson != null) {
            _debugDrawPerson.debugDrawEnabled = false;
        }
        _debugDrawPerson = newPerson;
        if (_debugDrawPerson != null) {
            _debugDrawPerson.debugDrawEnabled = true;
        }
        return result;
    }

    public PersonInfo GetPerson(PenetrationState info)
    {
        if (info.personId == PenetrationState.INVALID_PERSON_ID) {
            return null;
        }

        if (_people.Count == 0) {
            return null;
        }

        if ((info.personIndex < 0) || (info.personIndex >= _people.Count) || (_people[info.personIndex].id != info.personId)) {
            int i = _people.FindIndex((x) => x.id == info.personId);
            if (i < 0) {
                return null;
            }
            info.personIndex = i;
        }

        return _people[info.personIndex];
    }

    public PersonInfo GetTargetPerson(PenetrationInfo info)
    {
        if (info.targetPerson == null) {
            return null;
        }
        return _people.FirstOrDefault((x) => x.atom == info.targetPerson);
    }

    public PersonInfo GetPenetratedPerson(PenetrationInfo info)
    {
        if (info.penetratedPerson == null) {
            return null;
        }
        return _people.FirstOrDefault((x) => x.atom == info.penetratedPerson);
    }

    public PersonInfo GetPerson(string uid)
    {
        return _people.FirstOrDefault((x) => x.atom.name == uid);
    }

    public static PersonOrifice GetOrifice(PersonInfo person, int orificeIndex)
    {
        if ((person != null) && (orificeIndex >= 0) && (orificeIndex < person.orifices.Length)) {
            return person.orifices[orificeIndex];
        }
        return null;
    }

    public PersonOrifice GetOrifice(PenetrationState info)
    {
        return GetOrifice(GetPerson(info), info.orificeIndex);
    }

    public PersonOrifice GetTargetOrifice(PenetrationInfo info)
    {
        return GetOrifice(GetTargetPerson(info), info.targetOrificeIndex);
    }

    public PersonOrifice GetPenetratedOrifice(PenetrationInfo info)
    {
        return GetOrifice(GetPenetratedPerson(info), info.penetratedOrificeIndex);
    }

    public PersonOrifice GetOrifice(out PersonInfo person, PenetrationState info)
    {
        person = GetPerson(info);
        return GetOrifice(person, info.orificeIndex);
    }

    public PersonOrifice GetTargetOrifice(out PersonInfo person, PenetrationInfo info)
    {
        person = GetTargetPerson(info);
        return GetOrifice(person, info.targetOrificeIndex);
    }

    public PersonOrifice GetPenetratedOrifice(out PersonInfo person, PenetrationInfo info)
    {
        person = GetPenetratedPerson(info);
        return GetOrifice(person, info.penetratedOrificeIndex);
    }

    public List<string> GetPersonChoices()
    {
        List<string> result = new List<string>{ "None" };
        foreach (PersonInfo person in _people) {
            result.Add(person.atom.uid);
        }
        return result;
    }

    public void DrawOrifices(int level /*0 = off, 1 = penetrated person, 2 = all people*/)
    {
        if (level == _debugDraw) {
            return;
        }

        _debugDraw = level;
        _debugDrawPerson = null;
        foreach (PersonInfo person in _people) {
            person.debugDrawEnabled = (level >= 2);
        }
    }

    private bool UpdateOrifice(CheckPenetration checkPenetration, string blAtomName, int blOrificeIndex)
    {
        if (checkPenetration == null) {
            return false;
        }
        if (! Validate(_penetration, blAtomName, blOrificeIndex)) {
            return false;
        }

        // If we are penetrating, check if it's still true.

        float depth;
        float depthFactor;
        float girth;
        float length;
        float distance;
        if (_penetration.penetrating) {
            float maxDistance = _penetration.useBlInfo ? 0.2f : 0.06f;
            _penetration.penetrating = checkPenetration(out depth, out depthFactor, out girth, out length, out distance, GetOrifice(_penetration), maxDistance);
            _penetration.length = length;
            if (_penetration.penetrating) {
                _penetration.depth = depth;
                _penetration.depthFactor = depthFactor;
                _penetration.girth = girth;
                return true;
            }
            _penetration.penetrating = false;
            if ((! _penetration.useBlInfo) && (_verbosity >= 2)) {
                SuperController.LogMessage("Dildo no longer penetrating");
            }
        }

        // If we are not penetrating and BL is telling us the details, do not continue, nothing to search for.

        if (_penetration.useBlInfo) {
            return false;
        }

        // We are (no longer) penetrating, try to find the next orifice.

        NextOrifice(_penetration);
        PersonOrifice orifice = GetOrifice(_penetration);
        _penetration.penetrating = checkPenetration(out depth, out depthFactor, out girth, out length, out distance, orifice, 0.02f);
        _penetration.length = length;
        if (! _penetration.penetrating) {
            return false;
        }
        if (_verbosity >= 2) {
            SuperController.LogMessage($"Dildo entered orifice {_penetration.orificeIndex} of {_people[_penetration.personIndex].atom.uid} at depth {depth}, distance {distance}");
        }

        // Check if anus/vagina is closer.

        int index = _penetration.orificeIndex;
        int otherIndex;
        if (index == PenetrationInfo.VAGINA) {
            otherIndex = PenetrationInfo.ANUS;
        }
        else if (index == PenetrationInfo.ANUS) {
            otherIndex = PenetrationInfo.VAGINA;
        }
        else {
            _penetration.depth = depth;
            _penetration.depthFactor = depthFactor;
            _penetration.girth = girth;
            return true;
        }

        float otherDepth;
        float otherDepthFactor;
        float otherGirth;
        float otherDistance;
        _penetration.orificeIndex = otherIndex;
        PersonOrifice otherOrifice = GetOrifice(_penetration);
        bool otherPenetrating = checkPenetration(out otherDepth, out otherDepthFactor, out otherGirth, out length, out otherDistance, otherOrifice, distance);
        if (otherPenetrating) {
            _penetration.depth = otherDepth;
            _penetration.depthFactor = otherDepthFactor;
            _penetration.girth = otherGirth;
        }
        else {
            _penetration.orificeIndex = index;
            _penetration.depth = depth;
            _penetration.depthFactor = depthFactor;
            _penetration.girth = girth;
        }
        return true;
    }

    private bool Validate(PenetrationState info, string blAtomName, int blOrificeIndex)
    {
        // If not person atoms are registered, always use "nothing penetrated" state.

        if (_people.Count == 0) {
            info.Reset();
            return false;
        }

        // Make sure the penetration info is referencing valid registered person.

        if (info.personId == PenetrationState.INVALID_PERSON_ID) {
            info.Reset(0, _people[0]);
            return true;
        }

        if ((info.personIndex < 0) || (info.personIndex >= _people.Count) || (_people[info.personIndex].id != info.personId)) {
            int i = _people.FindIndex((x) => x.id == info.personId);
            if (i < 0) {
                info.Reset(0, _people[0]);
                return true;
            }
            info.personIndex = i;
        }

        PersonInfo person = _people[info.personIndex];

        // If BL is telling us what's being penetrated, use that info.

        bool useBlInfo = false;
        if ((blOrificeIndex >= 0) && (! string.IsNullOrEmpty(blAtomName))) {
            if (person.atom.name == blAtomName) {
                useBlInfo = true;
            }
            else {
                int i = _people.FindIndex((x) => x.atom.name == blAtomName);
                if (i >= 0)  {
                    person = _people[i];
                    info.personId = person.id;
                    info.personIndex = i;
                    useBlInfo = true;
                }
            }
        }

        if (useBlInfo) {
            if ((! info.useBlInfo) || (blOrificeIndex != info.orificeIndex)) {
                info.useBlInfo = true;
                info.orificeIndex = blOrificeIndex;
                if (_verbosity >= 2) {
                    SuperController.LogMessage($"Tracking orifice {_penetration.orificeIndex} of {person.atom.uid} according to external info from BL");
                }
            }
            info.penetrating = (
                (info.orificeIndex >= 0) &&
                (info.orificeIndex < PenetrationInfo.ORIFICE_COUNT) &&
                (person.orifices[info.orificeIndex] != null)
            );
            return true;
        }

        // Not using penetration details from BL, so do final fixup ready for our own detection.

        info.useBlInfo = false;
        if (info.orificeIndex >= PenetrationInfo.ORIFICE_COUNT) {
            info.orificeIndex = PenetrationInfo.ORIFICE_COUNT - 1;
            info.penetrating = false;
        }
        if (person.orifices[info.orificeIndex] == null) {
            info.penetrating = false;
        }
        return true;
    }

    private void NextOrifice(PenetrationState info)
    {
        if (info.personId == PenetrationState.INVALID_PERSON_ID) {
            return;
        }

        int tries = 0;
        do {
            if (++info.orificeIndex >= PenetrationInfo.ORIFICE_COUNT) {
                info.orificeIndex = 0;
                if (++info.personIndex >= _people.Count) {
                    info.personIndex = 0;
                }
                info.personId = _people[info.personIndex].id;
            }
            tries++;
        } while ((GetOrifice(info) == null) && (tries < _people.Count * PenetrationInfo.ORIFICE_COUNT));
    }

    private void AtomAdded(Atom atom)
    {
        if (atom.type != "Person") {
            return;
        }
        if (_people.Find((x) => x.atom == atom) != null) {
            return;
        }

        if (_verbosity >= 2) {
            SuperController.LogMessage($"Registering Person {atom.uid} (name {atom.name})");
        }

        PersonInfo person = new PersonInfo(atom, _nextId++);
        _people.Add(person);
        person.debugDrawEnabled = (_debugDraw >= 2);
    }

    private void AtomRemoved(Atom atom)
    {
        int index = _people.FindIndex((x) => x.atom == atom);
        if (index < 0) {
            return;
        }
        if (_verbosity >= 2) {
            SuperController.LogMessage($"Forgetting Person {atom?.uid} (name {atom?.name})");
        }
        _people[index].Reset();
        _people.RemoveAt(index);
    }
}

} // namespace Foost.Penetration
} // namespace Foost
