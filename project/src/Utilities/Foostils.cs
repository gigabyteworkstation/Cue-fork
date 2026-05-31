using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleJSON;
using UnityEngine;
using UnityEngine.UI;

namespace Foost {
namespace Utils {

public static class JSONExtensions
{
    public static void SaveObject(this JSONClass jc, string key, JSONClass value, bool forceSave = false)
    {
        if (((value != null) && (value.Count > 0)) || forceSave) {
            jc[key] = value;
        }
        else {
            jc.Remove(key);
        }
    }

    public static void SaveArray(this JSONClass jc, string key, JSONArray value, bool forceSave = false)
    {
        if (((value != null) && (value.Count > 0)) || forceSave) {
            jc[key] = value;
        }
        else {
            jc.Remove(key);
        }
    }

    public static void SaveString(this JSONClass jc, string key, string value, string defValue, bool forceSave = false)
    {
        if ((value != defValue) || forceSave) {
            jc[key] = value;
        }
        else {
            jc.Remove(key);
        }
    }

    public static void SaveBool(this JSONClass jc, string key, bool value, bool defValue, bool forceSave = false)
    {
        if ((value != defValue) || forceSave) {
            jc[key].AsBool = value;
        }
        else {
            jc.Remove(key);
        }
    }

    public static void SaveInt(this JSONClass jc, string key, int value, int defValue, bool forceSave = false)
    {
        if ((value != defValue) || forceSave) {
            jc[key].AsInt = value;
        }
        else {
            jc.Remove(key);
        }
    }

    public static void SaveFloat(this JSONClass jc, string key, float value, float defValue, bool forceSave = false)
    {
        if ((value != defValue) || forceSave) {
            jc[key].AsFloat = value;
        }
        else {
            jc.Remove(key);
        }
    }

    public static void SaveVector3Class(this JSONClass jc, string key, Vector3 value, Vector3 defValue, bool forceSave = false)
    {
        if ((value != defValue) || forceSave) {
            JSONClass c = new JSONClass();
            c.SaveFloat("x", value.x, defValue.x, forceSave);
            c.SaveFloat("y", value.y, defValue.y, forceSave);
            c.SaveFloat("z", value.z, defValue.z, forceSave);
            jc.SaveObject(key, c);
        }
        else {
            jc.Remove(key);
        }
    }

    public static void SaveVector3(this JSONClass jc, string key, Vector3 value, Vector3 defValue, bool forceSave = false)
    {
        if ((value != defValue) || forceSave) {
            jc.SaveString(key, $"{value.x};{value.y};{value.z}", "", forceSave);
        }
        else {
            jc.Remove(key);
        }
    }

    public static void SaveQuaternionClass(this JSONClass jc, string key, Quaternion value, Quaternion defValue, bool forceSave = false)
    {
        if ((value != defValue) || forceSave) {
            JSONClass c = new JSONClass();
            c.SaveFloat("x", value.x, defValue.x, forceSave);
            c.SaveFloat("y", value.y, defValue.y, forceSave);
            c.SaveFloat("z", value.z, defValue.z, forceSave);
            c.SaveFloat("w", value.w, defValue.z, forceSave);
            jc.SaveObject(key, c);
        }
        else {
            jc.Remove(key);
        }
    }

    public static void SaveQuaternion(this JSONClass jc, string key, Quaternion value, Quaternion defValue, bool forceSave = false)
    {
        if ((value != defValue) || forceSave) {
            jc.SaveString(key, $"{value.x};{value.y};{value.z};{value.w}", "", forceSave);
        }
        else {
            jc.Remove(key);
        }
    }

    public static void SaveColorClass(this JSONClass jc, string key, Color value, Color defValue, bool forceSave = false)
    {
        if ((value != defValue) || forceSave) {
            JSONClass c = new JSONClass();
            c.SaveFloat("r", value.r, defValue.r, forceSave);
            c.SaveFloat("g", value.g, defValue.g, forceSave);
            c.SaveFloat("b", value.b, defValue.b, forceSave);
            c.SaveFloat("a", value.a, defValue.a, forceSave);
            jc.SaveObject(key, c);
        }
        else {
            jc.Remove(key);
        }
    }

    public static void SaveColor(this JSONClass jc, string key, Color value, Color defValue, bool forceSave = false)
    {
        if ((value != defValue) || forceSave) {
            if ((! forceSave) && (value.a == defValue.a)) {
                jc.SaveString(key, $"{value.r};{value.g};{value.b}", "", forceSave);
            }
            else {
                jc.SaveString(key, $"{value.r};{value.g};{value.b};{value.a}", "", forceSave);
            }
        }
        else {
            jc.Remove(key);
        }
    }

    public static void SaveString(this JSONArray ja, string value)
    {
        ja.Add(new JSONData(value));
    }

    public static void SaveBool(this JSONArray ja, bool value)
    {
        ja.Add(new JSONData(value));
    }

    public static void SaveInt(this JSONArray ja, int value)
    {
        ja.Add(new JSONData(value));
    }

    public static void SaveFloat(this JSONArray ja, float value)
    {
        ja.Add(new JSONData(value));
    }

    public static void SaveColor(this JSONArray ja, Color value)
    {
        ja.SaveString($"{value.r};{value.g};{value.b};{value.a}");
    }

    public static void SaveVector3(this JSONArray ja, Vector3 value)
    {
        ja.SaveString($"{value.x};{value.y};{value.z}");
    }

    public static JSONNode LoadNode(this JSONClass jc, string key)
    {
        if (jc == null) {
            return null;
        }
        JSONNode node = jc[key];
        if (node == null) {
            return null;
        }
        return node;
    }

    public static JSONClass LoadObject(this JSONClass jc, string key)
    {
        return LoadNode(jc, key)?.AsObject;
    }

    public static JSONArray LoadArray(this JSONClass jc, string key)
    {
        return LoadNode(jc, key)?.AsArray;
    }

    public static string LoadString(this JSONClass jc, string key, string defValue, bool useDefaultWhenEmpty)
    {
        JSONNode node = LoadNode(jc, key);
        if (node == null) {
            return defValue;
        }

        if (useDefaultWhenEmpty && string.IsNullOrEmpty(node.Value)) {
            return defValue;
        }
        return node.Value;
    }

    public static bool LoadBool(this JSONClass jc, string key, bool defValue)
    {
        JSONNode node = LoadNode(jc, key);
        if (node == null) {
            return defValue;
        }
        return node.AsBool;
    }

    public static int LoadInt(this JSONClass jc, string key, int defValue)
    {
        JSONNode node = LoadNode(jc, key);
        if (node == null) {
            return defValue;
        }
        return node.AsInt;
    }

    public static float LoadFloat(this JSONClass jc, string key, float defValue)
    {
        JSONNode node = LoadNode(jc, key);
        if (node == null) {
            return defValue;
        }
        return node.AsFloat;
    }

    public static Vector3 LoadVector3(this JSONClass jc, string key, Vector3 defValue)
    {
        JSONNode node = LoadNode(jc, key);
        if (node == null) {
            return defValue;
        }

        Vector3 result = defValue;
        JSONClass c = node.AsObject;
        if (c != null) {
            result.x = c.LoadFloat("x", defValue.x);
            result.y = c.LoadFloat("y", defValue.y);
            result.z = c.LoadFloat("z", defValue.z);
        }
        else {
            string[] parts = node.Value.Split(';');
            for (int i = 0; i < Math.Min(parts.Length, 3); ++i) {
                float f;
                if (float.TryParse(parts[i], out f)) {
                    result[i] = f;
                }
            }
        }
        return result;
    }

    public static Quaternion LoadQuaternion(this JSONClass jc, string key, Quaternion defValue)
    {
        JSONNode node = LoadNode(jc, key);
        if (node == null) {
            return defValue;
        }

        Quaternion result = defValue;
        JSONClass c = node.AsObject;
        if (c != null) {
            result.x = c.LoadFloat("x", defValue.x);
            result.y = c.LoadFloat("y", defValue.y);
            result.z = c.LoadFloat("z", defValue.z);
            result.w = c.LoadFloat("w", defValue.w);
        }
        else {
            string[] parts = node.Value.Split(';');
            for (int i = 0; i < Math.Min(parts.Length, 4); ++i) {
                float f;
                if (float.TryParse(parts[i], out f)) {
                    result[i] = f;
                }
            }
        }
        return result;
    }

    public static Color LoadColor(this JSONClass jc, string key, Color defValue)
    {
        JSONNode node = LoadNode(jc, key);
        if (node == null) {
            return defValue;
        }

        Color result = defValue;
        JSONClass c = node.AsObject;
        if (c != null) {
            result.r = c.LoadFloat("r", defValue.r);
            result.g = c.LoadFloat("g", defValue.g);
            result.b = c.LoadFloat("b", defValue.b);
            result.a = c.LoadFloat("a", defValue.a);
        }
        else {
            string[] parts = node.Value.Split(';');
            for (int i = 0; i < Math.Min(parts.Length, 4); ++i) {
                float f;
                if (float.TryParse(parts[i], out f)) {
                    result[i] = f;
                }
            }
        }
        return result;
    }

    public static string LoadString(this JSONArray ja, int index)
    {
        if ((ja == null) || (index < 0) || (index >= ja.Count)) {
            return string.Empty;
        }
        return ja[index].Value;
    }

    public static bool LoadBool(this JSONArray ja, int index, bool defValue)
    {
        if ((ja == null) || (index < 0) || (index >= ja.Count)) {
            return defValue;
        }

        bool value;
        if (bool.TryParse(ja[index].Value, out value)) {
            return value;
        }
        return defValue;
    }

    public static int LoadInt(this JSONArray ja, int index, int defValue)
    {
        if ((ja == null) || (index < 0) || (index >= ja.Count)) {
            return defValue;
        }

        int value;
        if (int.TryParse(ja[index].Value, out value)) {
            return value;
        }
        return defValue;
    }

    public static float LoadFloat(this JSONArray ja, int index, float defValue)
    {
        if ((ja == null) || (index < 0) || (index >= ja.Count)) {
            return defValue;
        }

        float value;
        if (float.TryParse(ja[index].Value, out value)) {
            return value;
        }
        return defValue;
    }

    public static Color LoadColor(this JSONArray ja, int index, Color defValue)
    {
        if ((ja == null) || (index < 0) || (index >= ja.Count)) {
            return defValue;
        }

        Color value = defValue;
        string[] parts = ja[index].Value.Split(';');
        for (int i = 0; i < Math.Min(parts.Length, 4); ++i) {
            float f;
            if (float.TryParse(parts[i], out f)) {
                value[i] = f;
            }
        }
        return value;
    }

    public static Vector3 LoadVector3(this JSONArray ja, int index, Vector3 defValue)
    {
        if ((ja == null) || (index < 0) || (index >= ja.Count)) {
            return defValue;
        }

        Vector3 value = defValue;
        string[] parts = ja[index].Value.Split(';');
        for (int i = 0; i < Math.Min(parts.Length, 3); ++i) {
            float f;
            if (float.TryParse(parts[i], out f)) {
                value[i] = f;
            }
        }
        return value;
    }
}

public static class MVRScriptExtensions
{
    public static void RegisterNoStore(this MVRScript script, JSONStorableString storable)
    {
        storable.isStorable = false;
        storable.isRestorable = false;
        script.RegisterString(storable);
    }

    public static void RegisterNoStore(this MVRScript script, JSONStorableStringChooser storable)
    {
        storable.isStorable = false;
        storable.isRestorable = false;
        script.RegisterStringChooser(storable);
    }

    public static void RegisterNoStore(this MVRScript script, JSONStorableFloat storable)
    {
        storable.isStorable = false;
        storable.isRestorable = false;
        script.RegisterFloat(storable);
    }

    public static void RegisterNoStore(this MVRScript script, JSONStorableBool storable)
    {
        storable.isStorable = false;
        storable.isRestorable = false;
        script.RegisterBool(storable);
    }
}

public static class UIDynamicExtensions
{
    public static UIDynamicSlider SetReadOnly(this UIDynamicSlider slider, bool readOnly)
    {
        if (slider == null) {
            return null;
        }
        slider.labelText.color = readOnly ? Color.gray : Color.black;
        slider.slider.interactable = ! readOnly;
        slider.sliderControl.enabled = ! readOnly;
        slider.sliderValueTextFromFloat.UIInputField.interactable = ! readOnly;
        slider.quickButtonsEnabled = ! readOnly;
        slider.defaultButtonEnabled = ! readOnly;
        slider.enabled = ! readOnly;
        return slider;
    }

    public static UIDynamicSlider SetQuickButtonsEnabled(this UIDynamicSlider slider, bool value)
    {
        if (slider == null) {
            return null;
        }
        slider.quickButtonsEnabled = value;
        return slider;
    }

    public static UIDynamicSlider SetRangeAdjustEnabled(this UIDynamicSlider slider, bool value)
    {
        if (slider == null) {
            return null;
        }
        slider.rangeAdjustEnabled = value;
        return slider;
    }

    public static UIDynamicColorPicker SetReadOnly(this UIDynamicColorPicker color, bool readOnly)
    {
        if (color == null) {
            return null;
        }
        color.labelText.color = readOnly ? Color.gray : Color.white;
        color.colorPicker.enabled = ! readOnly;
        color.colorPicker.hueSlider.interactable = ! readOnly;
        color.colorPicker.redSlider.interactable = ! readOnly;
        color.colorPicker.greenSlider.interactable = ! readOnly;
        color.colorPicker.blueSlider.interactable = ! readOnly;
        color.enabled = ! readOnly;

        foreach (Button b in color.GetComponentsInChildren<Button>()) {
            if ((b.name == "DefaultValueButton") || (b.name == "PasteFromClipboardButton")) {
                b.interactable = ! readOnly;
            }
        }
        foreach (InputField f in color.GetComponentsInChildren<InputField>()) {
            f.interactable = ! readOnly;
        }
        return color;
    }

    public static UIDynamicButton SetReadOnly(this UIDynamicButton button, bool readOnly)
    {
        if (button == null) {
            return null;
        }
        button.button.interactable = ! readOnly;
        return button;
    }

    public static UIDynamicToggle SetReadOnly(this UIDynamicToggle toggle, bool readOnly)
    {
        if (toggle == null) {
            return null;
        }
        toggle.toggle.interactable = ! readOnly;
        return toggle;
    }
    
}

public static class TransformExtensions
{
    public static Transform FindChildByName(this Transform parent, string name, bool includeSelf = true)
    {
        if (includeSelf && (parent.name == name)) {
            return parent;
        }

        foreach (Transform child in parent) {
            Transform result = FindChildByName(child, name, true);
            if (result != null) {
                return result;
            }
        }

        return null;
    }

    public static void PrintHierarchy(this Transform t, bool withTypes = false, int depth = 0)
    {
        if (depth > 10) {
            return;
        }
        string prefix = new string(' ', depth);
        if (withTypes) {
            SuperController.LogMessage($"{prefix}{t.name} ({string.Join(", ", t.GetComponents<Component>().Select((x) => x.GetType().ToString()).ToArray())})");
        }
        else {
            SuperController.LogMessage($"{prefix}{t.name}");
        }
        for (int i = 0; i < t.childCount; ++i) {
            Transform c = t.GetChild(i);
            PrintHierarchy(c, withTypes, depth + 1);
        }
    }

    public static void PrintHierarchy(this RectTransform t, bool withTypes = false, bool withOffsets = false, int depth = 0)
    {
        if (depth > 10) {
            return;
        }
        string prefix = new string(' ', depth);
        string suffix = "";
        if (withTypes) {
            suffix = $" ({string.Join(", ", t.GetComponents<Component>().Select((x) => x.GetType().ToString()).ToArray())})";
        }
        if (withOffsets) {
            suffix += $" (anchor {t.anchorMin.ToString("F3")} to {t.anchorMin.ToString("F3")}, offset {t.offsetMin.ToString("F3")} to {t.offsetMax.ToString("F3")}, pivot {t.pivot.ToString("F3")}, anchorpos {t.anchoredPosition.ToString("F3")}, size {t.sizeDelta.ToString("F3")}, localpos {t.localPosition.ToString("F3")}, rect {t.rect.ToString("F3")}";
        }
        SuperController.LogMessage($"{prefix}{t.name}{suffix}");
        for (int i = 0; i < t.childCount; ++i) {
            Transform c = t.GetChild(i);
            RectTransform rc = c as RectTransform;
            if (rc != null) {
                PrintHierarchy(rc, withTypes, withOffsets, depth + 1);
            }
            else {
                PrintHierarchy(c, withTypes, depth + 1);
            }
        }
    }
}

public struct JSONArrayBuilder
{
    private JSONArray _array;
    private int _emptyCount;

    public void Add(JSONClass jc)
    {
        if ((jc == null) || (jc.Count == 0)) {
            ++_emptyCount;
            return;
        }

        if (_array == null) {
            _array = new JSONArray();
        }

        while (_emptyCount > 0) {
            _array.Add(new JSONClass());
            --_emptyCount;
        }

        _array.Add(jc);
    }

    public void Add(JSONArray ja)
    {
        if ((ja == null) || (ja.Count == 0)) {
            ++_emptyCount;
            return;
        }

        if (_array == null) {
            _array = new JSONArray();
        }

        while (_emptyCount > 0) {
            _array.Add(new JSONArray());
            --_emptyCount;
        }

        _array.Add(ja);
    }

    public JSONArray Finalize()
    {
        return _array;
    }
}

public struct JSONPackedArrayBuilder
{
    private char _separator;
    private StringBuilder _sb;
    private int _emptyCount;

    public JSONPackedArrayBuilder(char separator)
    {
        _separator = separator;
        _sb = null;
        _emptyCount = 0;
    }

    public void Add(string value) // up to the caller to never include separator in the string
    {
        if (_separator == '\0') {
            _separator = ';';
        }

        if (string.IsNullOrEmpty(value)) {
            ++_emptyCount;
            return;
        }

        if (_sb == null) {
            _sb = new StringBuilder();
        }
        else {
            _sb.Append(_separator);
        }

        if (_emptyCount > 0) {
            _sb.Append(_separator, _emptyCount);
            _emptyCount = 0;
        }

        _sb.Append(value);
    }

    public string Finalize()
    {
        if (_sb == null) {
            return string.Empty;
        }
        return _sb.ToString();
    }

    public static string[] Unpack(string packed, char separator = ';')
    {
        return packed.Split(separator);
    }
}

public static class MathExt
{
    public static Vector4 NewVector(Vector3 v, float w)
    {
        return new Vector4(v.x, v.y, v.z, w);
    }

    public static bool AnyLower(Vector3 a, Vector3 b)
    {
        return (a.x < b.x) || (a.y < b.y) | (a.z < b.z);
    }

    public static bool AnyHigher(Vector3 a, Vector3 b)
    {
        return (a.x > b.x) || (a.y > b.y) | (a.z > b.z);
    }

    public static int Lerp(int a, int b, float t)
    {
        return a + Mathf.RoundToInt((b - a) * Mathf.Clamp01(t));
    }

    public static int LerpUnclamped(int a, int b, float t)
    {
        return a + Mathf.RoundToInt((b - a) * t);
    }

    public static HSVColor Lerp(HSVColor a, HSVColor b, float t)
    {
        float h1 = a.H;
        float h2 = b.H;
        if (h1 - h2 < -0.5f) {
            h1 += 1.0f;
        }
        if (h1 - h2 > 0.5f) {
            h2 += 1.0f;
        }
        return new HSVColor{
            H = Mathf.Lerp(h1, h2, t) % 1.0f,
            S = Mathf.Lerp(a.S, b.S, t),
            V = Mathf.Lerp(a.V, b.V, t)
        };
    }

    public static float TriLerp(float a, float b, float c, float t)
    {
        if (t <= 0.5f) {
            return Mathf.Lerp(a, b, 2.0f * t);
        }
        return Mathf.Lerp(b, c, 2.0f * (t - 0.5f));
    }

    public static float FadeOutIn(float a, float b, float c, float t, float t0)
    {
        if (t < t0) {
            return EaseInOutCubic(a, b, t / t0);
        }
        if (t > 1.0f - t0) {
            return EaseInOutCubic(c, b, (1.0f - t) / t0);
        }
        return b;
    }

    public static float TriEaseInOut(float a, float b, float c, float t, float t0, Interpolator func)
    {
        if (t0 <= 0) {
            return func(b, c, t);
        }
        if (t0 >= 1) {
            return func(a, b, t);
        }

        if (t <= t0) {
            return func(a, b, t / t0);
        }
        return func(b, c, (t - t0) / (1.0f - t0));
    }

    public static float TriEaseInHoldOut(float a, float b, float c, float t, float t0, float hold, Interpolator func)
    {
        if ((t >= t0) && (t <= t0 + hold)) {
            return b;
        }
        if (t0 + hold <= 0) {
            return func(b, c, t);
        }
        if (t0 >= 1) {
            return func(a, b, t);
        }

        if (t <= t0) {
            return func(a, b, t / t0);
        }
        t0 += hold;
        if (t0 >= 1) {
            return b;
        }
        return func(b, c, (t - t0) / (1.0f - t0));
    }

    // https://easings.net
    public static float EasyInQuadratic(float a, float b, float t)
    {
        t = Mathf.Clamp01(t * t);
        return a * (1.0f - t) + b * t;
    }

    public static float EasyOutQuadratic(float a, float b, float t)
    {
        t = 1 - t;
        t = Mathf.Clamp01(1 - t * t);
        return a * (1.0f - t) + b * t;
    }

    public static float EaseInOutQuadratic(float a, float b, float t)
    {
        t = Mathf.Clamp01(t);

        if (t < 0.5f) {
            t = 2.0f * t * t;
        }
        else {
            float x = 2.0f * (1.0f - t);
            t = 1.0f - 0.5f * x * x;
        }

        return a * (1.0f - t) + b * t;
    }

    public static float EasyInCubic(float a, float b, float t)
    {
        t = Mathf.Clamp01(t * t * t);
        return a * (1.0f - t) + b * t;
    }

    public static float EasyOutCubic(float a, float b, float t)
    {
        t = 1 - t;
        t = Mathf.Clamp01(1 - t * t * t);
        return a * (1.0f - t) + b * t;
    }

    public static float EaseInOutCubic(float a, float b, float t)
    {
        t = Mathf.Clamp01(t);

        if (t < 0.5f) {
            t = 4.0f * t * t * t;
        }
        else {
            float x = 2.0f * (1.0f - t);
            t = 1.0f - 0.5f * x * x * x;
        }

        return a * (1.0f - t) + b * t;
    }

    public const float C5 = 2.0f * Mathf.PI / 4.5f;

    public static float EaseInOutElastic(float a, float b, float t)
    {
        if (t <= 0.0f) {
            return a;
        }
        if (t >= 1.0f) {
            return b;
        }

        if (t < 0.5f) {
            t = 0.5f * -Mathf.Pow(2.0f, 20.0f * t - 10.0f) * Mathf.Sin((20.0f * t - 11.125f) * C5);
        }
        else {
            t = 1.0f + 0.5f * Mathf.Pow(2.0f, -20.0f * t + 10.0f) * Mathf.Sin((20.0f * t - 11.125f) * C5);
        }

        return a * (1.0f - t) + b * t;
    }

    public delegate float Interpolator(float from, float to, float t);

    public static int GetInterpolatorCount()
    {
        return 5;
    }

    public static readonly List<string> INTERPOLATOR_NAMES = new List<string>{ "Linear", "Smoothstep", "Quadratic", "Cubic", "Elastic" };

    public static int GetInterpolatorIndex(string name)
    {
        return INTERPOLATOR_NAMES.IndexOf(name);
    }

    public static string GetInterpolatorName(int index)
    {
        if ((index >= 0) && (index < INTERPOLATOR_NAMES.Count)) {
            return INTERPOLATOR_NAMES[index];
        }
        return null;
    }

    public static Interpolator GetInterpolatorFunc(int index)
    {
        switch (index) {
            case 0:
                return Mathf.Lerp;
            case 1:
                return Mathf.SmoothStep;
            case 2:
                return EaseInOutQuadratic;
            case 3:
                return EaseInOutCubic;
            case 4:
                return EaseInOutElastic;
        }
        return Mathf.Lerp;
    }

    public static Interpolator GetInterpolatorFunc(string name)
    {
        return GetInterpolatorFunc(GetInterpolatorIndex(name));
    }

    // https://en.wikipedia.org/wiki/Centripetal_Catmull%E2%80%93Rom_spline
    // Note: less efficient but nicer variant, assumes that the points are distinct (we could precompute t1/t2/t3 if needed)
    public static Vector3 CatmullRom(ref Vector3 p0, ref Vector3 p1, ref Vector3 p2, ref Vector3 p3, float t, float alpha = 0.5f)
    {
        const float t0 = 0.0f;
        float t1 = GetT(p0, p1, alpha);
        float t2 = GetT(p1, p2, alpha) + t1;
        float t3 = GetT(p2, p3, alpha) + t2;

        t = Mathf.LerpUnclamped(t1, t2, t);
        Vector3 a1 = Vector3.LerpUnclamped(p0, p1, (t - t0) / (t1 - t0));
        Vector3 a2 = Vector3.LerpUnclamped(p1, p2, (t - t1) / (t2 - t1));
        Vector3 a3 = Vector3.LerpUnclamped(p2, p3, (t - t2) / (t3 - t2));
        Vector3 b1 = Vector3.LerpUnclamped(a1, a2, (t - t0) / (t2 - t0));
        Vector3 b2 = Vector3.LerpUnclamped(a2, a3, (t - t1) / (t3 - t1));
        return Vector3.LerpUnclamped(b1, b2, (t - t1) / (t2 - t1));
    }

    // https://stackoverflow.com/a/66386250
    // Faster, can self-intersect if points are close
    public static Vector3 CatmullRomUniform(ref Vector3 p0, ref Vector3 p1, ref Vector3 p2, ref Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2.0f * p1) +
            (-p0 + p2) * t +
            (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
            (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3
        );
    }

    private static float GetT(Vector3 a, Vector3 b, float alpha)
    {
        return Mathf.Pow(Vector3.SqrMagnitude(a - b), 0.5f * alpha);
    }

    // https://www.desmos.com/calculator/g4qjffy0wl
    public static float AttackDecay(float attackFrom, float attackDuration /* 0 to 1 */, float decayTo /* 0 to 1 */, float speed /* 1 to inf */, float t /* 0 to 1 */)
    {
        if (t <= 0.0f) {
            return attackDuration > 0.0f ? attackFrom : 1.0f;
        }
        if (t >= 1.0f) {
            return decayTo;
        }
        if (t < attackDuration) {
            return attackFrom + (1.0f - attackFrom) * Mathf.Pow(t / attackDuration, 1.0f / speed);
        }
        if (t > attackDuration) {
            return 1.0f - (1.0f - decayTo) * Mathf.Pow((t - attackDuration) / (1.0f - attackDuration), speed);
        }
        return 1.0f;
    }

    public static float RandomUniform(float min, float max)
    {
        return UnityEngine.Random.Range(min, max);
    }

    // Normal distribution, with mean in the middle of min-max, and sigma one third of mean-min distance.
    // Clamped to the min-max range (uniformly redistributed if it falls outside of the range).
    // https://en.wikipedia.org/wiki/Box%E2%80%93Muller_transform
    public static float RandomNormal(float min, float max)
    {
        float mean = 0.5f * (min + max);
        if (max <= min) {
            return mean;
        }
        float u1 = Mathf.Max(UnityEngine.Random.value, 0.000001f);
        float u2 = UnityEngine.Random.value;
        float sigma = (max - min) / 6.0f;
        float value = sigma * Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Cos(2.0f * Mathf.PI * u2) + mean;
        if ((value < min) || (value > max)) {
            return RandomUniform(min, max);
        }
        return value;
    }

    // Generate random vector in cone with given angle (oriented in +Z)
    public static Vector3 RandomConeVectorCosine(float cosAngle)
    {
        float z = UnityEngine.Random.Range(cosAngle, 1.0f);
        float rot = UnityEngine.Random.Range(0.0f, 2.0f * Mathf.PI);
        return new Vector3(Mathf.Sqrt(1.0f - z * z) * Mathf.Cos(rot), Mathf.Sqrt(1.0f - z * z) * Mathf.Sin(rot), z);
    }

    public static Vector3 RandomConeVectorDegrees(float angle)
    {
        float angleRad = angle * (2.0f * Mathf.PI) / 360.0f;
        float cosAngle = Mathf.Cos(angleRad);
        return RandomConeVectorCosine(cosAngle);
    }

    public static Vector3 RandomConeVectorCosine(System.Random random, float cosAngle)
    {
        float z = Mathf.Lerp(cosAngle, 1.0f, (float)random.NextDouble());
        float rot = (float)random.NextDouble() * 2.0f * Mathf.PI;
        return new Vector3(Mathf.Sqrt(1.0f - z * z) * Mathf.Cos(rot), Mathf.Sqrt(1.0f - z * z) * Mathf.Sin(rot), z);
    }

    public static Vector3 RandomConeVectorDegrees(System.Random random, float angle)
    {
        float angleRad = angle * (2.0f * Mathf.PI) / 360.0f;
        float cosAngle = Mathf.Cos(angleRad);
        return RandomConeVectorCosine(random, cosAngle);
    }

    // https://en.wikipedia.org/wiki/M%C3%B6ller%E2%80%93Trumbore_intersection_algorithm
    public static float RayCastTriangle(Vector3 origin, Vector3 direction, Vector3 v0, Vector3 v1, Vector3 v2, out float uv1, out float uv2, float uvTolerance = 0.0001f)
    {
        uv1 = 0.0f;
        uv2 = 0.0f;

        // Check if the ray is parallel to the triangle.

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 cross2 = Vector3.Cross(direction, edge2);
        float det = Vector3.Dot(edge1, cross2);

        if (Mathf.Abs(det) < Mathf.Epsilon) {
            return -1.0f;
        }

        // Use barycentric coordinates to check if the intersection is inside.

        float invDet = 1.0f / det;
        Vector3 s = origin - v0;
        float u = invDet * Vector3.Dot(s, cross2);

        if ((u < -uvTolerance) || (u > 1.0f + uvTolerance)) {
            return -1.0f;
        }

        Vector3 cross1 = Vector3.Cross(s, edge1);
        float v = invDet * Vector3.Dot(direction, cross1);

        if ((v < -uvTolerance) || (u + v > 1.0f + uvTolerance)) {
            return -1.0f;
        }

        uv1 = u;
        uv2 = v;
        return invDet * Vector3.Dot(edge2, cross1);
    }

    // https://gamedev.stackexchange.com/questions/23743/whats-the-most-efficient-way-to-find-barycentric-coordinates
    public static Vector3 GetTriangleBarycentric(Vector3 a, Vector3 b, Vector3 c, Vector3 point)
    {
        Vector3 v0 = b - a;
        Vector3 v1 = c - a;
        Vector3 v2 = point - a;

        float d00 = Vector3.Dot(v0, v0);
        float d01 = Vector3.Dot(v0, v1);
        float d11 = Vector3.Dot(v1, v1);
        float d20 = Vector3.Dot(v2, v0);
        float d21 = Vector3.Dot(v2, v1);
        float denom = d00 * d11 - d01 * d01;
        if (Mathf.Abs(denom) <= Mathf.Epsilon) {
            return new Vector3(0.333f, 0.333f, 0.334f);
        }

        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        return new Vector3(1.0f - v - w, v, w);
    }

    // https://en.wikipedia.org/wiki/Distance_from_a_point_to_a_line#Vector_formulation
    public static float RayPointDistance(Vector3 origin, Vector3 direction, Vector3 point)
    {
        Vector3 originToPoint = point - origin;
        float rayOffset = Vector3.Dot(direction, originToPoint);
        Vector3 projected = direction * rayOffset;
        Vector3 perpendicular = originToPoint - projected;
        return perpendicular.magnitude;
    }

    // https://web.archive.org/web/20210410192029/http://geomalgorithms.com/a07-_distance.html
    public static float RayLineDistance(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, out float lineFactor)
    {
        lineFactor = -1.0f; // for degenerate/parallel cases, intentionally use off-the-line point as the reference

        Vector3 aToB = b - a;
        float lineLength = aToB.magnitude;
        if (lineLength < 0.0001f) {
            //SuperController.LogMessage("degen");
            return RayPointDistance(origin, direction, a);
        }
        Vector3 lineDir = aToB / lineLength;
        Vector3 originToPoint = a - origin;

        float ru = Vector3.Dot(originToPoint, direction); // -d
        float rv = Vector3.Dot(originToPoint, lineDir); // -e
        float uv = Vector3.Dot(direction, lineDir); // b
        float denom = 1.0f - uv * uv;

        if (denom < 0.0001f) {  // parallel, really just distance of A from the ray now
            //SuperController.LogMessage("parallel");
            return RayPointDistance(origin, direction, a);
        }
        float s = (ru - rv * uv) / denom;
        float t = (ru * uv - rv) / denom;

        lineFactor = t / lineLength;
        Vector3 diff = originToPoint + t * lineDir - s * direction;
        //SuperController.LogMessage($"s = {s}, t = {t}, fact = {lineFactor}, dist = {diff.magnitude}");
        return diff.magnitude;
    }
}

public class CatmullRomSpline
{
    public float length { get; private set; }
    public int pointCount { get { return _points.Length - 2; } }
    public int sampleCount { get { return _samplePoints.Length; } }
    private Vector3[] _points;
    private Vector4[] _samplePoints; // time in .w

    public CatmullRomSpline(int pointCount, int sampleCount)
    {
        _points = new Vector3[pointCount + 2];
        _samplePoints = new Vector4[Math.Max(3, sampleCount)];
    }

    public void StartSetPoints()
    {
    }

    public void SetPoint(int index, Vector3 point)
    {
        _points[index + 1] = point;
        if (index == 0) {
            _points[0] = point;
        }
        else if (index == _points.Length - 3) {
            _points[_points.Length - 1] = point;
        }
    }

    public void AdjustStartingTangent(Vector3 direction, float weight)
    {
        float distance = weight * Vector3.Distance(_points[1], _points[2]);
        _points[0] = _points[1] - distance * direction;
    }

    public void EndSetPoints()
    {
        int sampleCount = _samplePoints.Length;
        float step = 1.0f / (sampleCount - 1);
        length = 0.0f;
        Vector3 prevPoint = _points[1];
        _samplePoints[0] = MathExt.NewVector(prevPoint, 0.0f);
        for (int i = 1; i < sampleCount; ++i) {
            float t = i * step;
            Vector3 p = GetPointAtTime(t, clampAtEnd: true);
            float distance = Vector3.Distance(prevPoint, p);
            length += distance;
            _samplePoints[i] = MathExt.NewVector(p, length);
            prevPoint = p;
        }
    }

    public Vector3 GetSamplePoint(int sampleIndex)
    {
        if ((sampleIndex < 0) || (sampleIndex >= _samplePoints.Length)) {
            return Vector3.zero;
        }
        return _samplePoints[sampleIndex];
    }

    public float GetSampleDistance(int sampleIndex)
    {
        if ((sampleIndex < 0) || (sampleIndex >= _samplePoints.Length)) {
            return 0.0f;
        }
        return _samplePoints[sampleIndex].w;
    }

    public Vector3 GetPointAtTime(float time, bool clampAtEnd)
    {
        // 0   1   2   3   4   5
        // +---+---+---+---+---+
        //     0           1
        int segmentCount = _points.Length - 3;
        float segmentFactor = time * segmentCount;
        int segment = Mathf.Clamp(Mathf.FloorToInt(segmentFactor), 0, segmentCount - 1);
        segmentFactor -= segment;
        if (time >= 1.0f) {
            Vector3 to = _points[segment + 2];
            if (clampAtEnd) {
                return to;
            }
            Vector3 from = _points[segment + 1];
            Vector3 dir = to - from;
            float dirLen = dir.magnitude;
            if (dirLen <= Mathf.Epsilon) {
                return to;
            }
            dir *= length / dirLen;
            return to + dir * (time - 1.0f);
        }
        return MathExt.CatmullRomUniform(ref _points[segment], ref _points[segment + 1], ref _points[segment + 2], ref _points[segment + 3], segmentFactor);
    }

    public Vector3 GetPointAtDistance(float distance, bool clampAtEnd)
    {
        return GetPointAtTime(GetTimeAtDistance(distance, clampAtEnd), clampAtEnd);
    }

    public float GetDistanceAtTime(float time, bool clampAtEnd)
    {
        int sampleCount = _samplePoints.Length;
        float step = 1.0f / (sampleCount - 1);
        for (int i = 1; i < sampleCount; ++i) {
            float t1 = i * step;
            if (time <= t1) {
                float t0 = (i - 1) * step;
                return Mathf.Lerp(_samplePoints[i - 1].w, _samplePoints[i].w, (time - t0) / (t1 - t0));
            }
        }
        if (clampAtEnd) {
            return length;
        }
        return length + (time - 1.0f) * length;
    }

    public float GetDistanceAtPointIndex(int index)
    {
        float time = Mathf.Clamp01((float)index / (_points.Length - 3));
        return GetDistanceAtTime(time, true);
    }

    public float GetTimeAtDistance(float distance, bool clampAtEnd)
    {
        int sampleCount = _samplePoints.Length;
        for (int i = 1; i < sampleCount; ++i) {
            float d1 = _samplePoints[i].w;
            if (distance <= d1) {
                float d0 = _samplePoints[i - 1].w;
                float step = 1.0f / (sampleCount - 1);
                return Mathf.Lerp((i - 1) * step, i * step, (distance - d0) / (d1 - d0));
            }
        }
        if (clampAtEnd) {
            return 1.0f;
        }
        return 1.0f + (distance - length) / length;
    }
}

public static class Lists
{
    public static List<T> NewRepeat<T>(int count)
    {
        return NewRepeat(count, default(T));
    }

    public static List<T> NewRepeat<T>(int count, T value)
    {
        List<T> list = new List<T>(count);
        for (int i = 0; i < count; ++i) {
            list.Add(value);
        }
        return list;
    }
}


} // namespace Foost.Utils
} // namespace Foost
