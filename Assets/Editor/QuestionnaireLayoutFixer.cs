#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click layout for the mood questionnaire.
/// Menu: Tools > VR Biofeedback > Fix Questionnaire Layout
///
/// Expects (names matter):
///   Canvas (Screen Space - Overlay)
///     Image                     background
///     Questionnaire
///       Question1..3
///         Prompt                question text
///         SliderRow
///           Left                low-end label
///           [Something]Slider   the Slider
///           Right               high-end label
///     Button                    Submit
///
/// It sets every position, size and anchor from scratch using Layout Groups, so old offsets are
/// wiped out. Slider references on MoodScaleManager are untouched. Undo with Ctrl+Z.
/// Put this file in an "Editor" folder, e.g. Assets/Editor/.
/// </summary>
public static class QuestionnaireLayoutFixer
{
    // ---- sizes in reference pixels (1920 x 1080). Tweak and re-run if you want it bigger/smaller.
    const float BlockWidth = 1500f;
    const float BlockHeight = 620f;
    const float BlockPosY = 90f;
    const float QuestionSpacing = 40f;

    const float QuestionHeight = 140f;
    const float PromptHeight = 56f;
    const float RowHeight = 64f;

    const float LabelWidth = 380f;
    const float SliderWidth = 560f;
    const float SliderHeight = 36f;
    const float RowSpacing = 30f;

    const float PromptFontSize = 38f;
    const float LabelFontSize = 30f;

    const float ButtonWidth = 420f;
    const float ButtonHeight = 100f;
    const float ButtonPosY = -380f;
    const float ButtonFontSize = 40f;

    [MenuItem("Tools/VR Biofeedback/Fix Questionnaire Layout")]
    public static void Fix()
    {
        Transform questionnaire = FindQuestionnaire();
        if (questionnaire == null)
        {
            EditorUtility.DisplayDialog("Fix Questionnaire Layout",
                "Couldn't find a GameObject named 'Questionnaire' under a Canvas in the open scene.\n\n" +
                "Open MoodScene (or select the Questionnaire object) and try again.", "OK");
            return;
        }

        Canvas canvas = questionnaire.GetComponentInParent<Canvas>(true).rootCanvas;
        Transform canvasT = canvas.transform;
        Undo.RegisterFullObjectHierarchyUndo(canvasT.gameObject, "Fix Questionnaire Layout");

        // ---- Canvas
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = GetOrAdd<CanvasScaler>(canvasT.gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        GetOrAdd<GraphicRaycaster>(canvasT.gameObject);

        // Remove leftover nested canvases below the root (they keep their own old rectangles).
        foreach (var nested in canvasT.GetComponentsInChildren<Canvas>(true))
        {
            if (nested == canvas) continue;
            var go = nested.gameObject;
            foreach (var c in go.GetComponents<GraphicRaycaster>()) Undo.DestroyObjectImmediate(c);
            foreach (var c in go.GetComponents<CanvasScaler>()) Undo.DestroyObjectImmediate(c);
            Undo.DestroyObjectImmediate(nested);
            Debug.Log($"[Layout] Removed nested Canvas on '{go.name}'.");
        }

        // ---- Image (background)
        Transform image = canvasT.Find("Image");
        if (image != null)
        {
            var rt = EnsureRect(image);
            image = rt;
            Stretch(rt);
            image.SetSiblingIndex(0);
            var graphic = image.GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = false;   // never block the pointer
        }

        // ---- Questionnaire block
        var qRt = EnsureRect(questionnaire);
        questionnaire = qRt;
        Centre(qRt, BlockWidth, BlockHeight, 0f, BlockPosY);
        RemoveFitter(questionnaire);
        var qLayout = GetOrAdd<VerticalLayoutGroup>(questionnaire.gameObject);
        ConfigureVertical(qLayout, QuestionSpacing);
        questionnaire.SetSiblingIndex(image != null ? 1 : 0);

        int questions = 0;
        foreach (Transform qChild in Children(questionnaire))
        {
            questions++;
            Transform question = EnsureRect(qChild);
            ResetLocal(question);
            SetPreferred(question, -1f, QuestionHeight);
            ConfigureVertical(GetOrAdd<VerticalLayoutGroup>(question.gameObject), 6f);

            foreach (Transform pChild in Children(question))
            {
                Transform part = EnsureRect(pChild);
                ResetLocal(part);

                if (part.GetComponentInChildren<Slider>(true) != null && part.GetComponent<Slider>() == null)
                {
                    // SliderRow: label | slider | label
                    SetPreferred(part, -1f, RowHeight);
                    var h = GetOrAdd<HorizontalLayoutGroup>(part.gameObject);
                    h.childAlignment = TextAnchor.MiddleCenter;
                    h.spacing = RowSpacing;
                    h.padding = new RectOffset(0, 0, 0, 0);
                    h.childControlWidth = true;
                    h.childControlHeight = true;
                    h.childForceExpandWidth = false;
                    h.childForceExpandHeight = false;
                    h.childScaleWidth = false;
                    h.childScaleHeight = false;

                    var items = Children(part);
                    int count = items.Length;
                    for (int i = 0; i < count; i++)
                    {
                        Transform item = EnsureRect(items[i]);
                        ResetLocal(item);

                        if (item.GetComponent<Slider>() != null)
                        {
                            SetPreferred(item, SliderWidth, SliderHeight);
                        }
                        else
                        {
                            SetPreferred(item, LabelWidth, RowHeight);
                            bool isLeft = i < count / 2;   // labels before the slider align right, after it left
                            StyleText(item, LabelFontSize, isLeft ? TextAlign.Right : TextAlign.Left);
                        }
                    }
                }
                else
                {
                    // Prompt
                    SetPreferred(part, -1f, PromptHeight);
                    StyleText(part, PromptFontSize, TextAlign.Centre);
                }
            }
        }

        // ---- Submit button
        Transform button = canvasT.Find("Button");
        if (button != null)
        {
            var bRt = EnsureRect(button);
            button = bRt;
            Centre(bRt, ButtonWidth, ButtonHeight, 0f, ButtonPosY);
            button.SetAsLastSibling();
            StyleText(button, ButtonFontSize, TextAlign.Centre);
        }

        // Flatten any leftover depth/rotation/scale under the canvas.
        foreach (var rt in canvasT.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt == canvasT) continue;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            var p = rt.anchoredPosition3D; p.z = 0f; rt.anchoredPosition3D = p;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvasT);
        EditorSceneManager.MarkSceneDirty(canvasT.gameObject.scene);
        Selection.activeTransform = questionnaire;

        Debug.Log($"[Layout] Questionnaire laid out: {questions} questions, " +
                  $"{(image != null ? "background stretched" : "no 'Image' found")}, " +
                  $"{(button != null ? "Submit button placed" : "no 'Button' found")}. Save the scene to keep it (Ctrl+S).");
    }

    // ------------------------------------------------------------------ helpers

    enum TextAlign { Left, Centre, Right }

    static Transform[] Children(Transform t)
    {
        var list = new Transform[t.childCount];
        for (int i = 0; i < list.Length; i++) list[i] = t.GetChild(i);
        return list;
    }

    static Transform FindQuestionnaire()
    {
        var sel = Selection.activeTransform;
        if (sel != null)
        {
            for (var t = sel; t != null; t = t.parent)
                if (t.name == "Questionnaire") return t;
            var below = FindByName(sel, "Questionnaire");
            if (below != null) return below;
        }

        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            var found = FindByName(root.transform, "Questionnaire");
            if (found != null && found.GetComponentInParent<Canvas>(true) != null) return found;
        }
        return null;
    }

    static Transform FindByName(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var r = FindByName(c, name);
            if (r != null) return r;
        }
        return null;
    }

    static RectTransform EnsureRect(Transform t)
    {
        var rt = t as RectTransform;
        if (rt != null) return rt;
        rt = Undo.AddComponent<RectTransform>(t.gameObject);   // replaces the plain Transform
        Debug.Log($"[Layout] Gave '{t.name}' a Rect Transform.");
        return rt;
    }

    static void ResetLocal(Transform t)
    {
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        ResetLocal(rt);
    }

    static void Centre(RectTransform rt, float w, float h, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        ResetLocal(rt);
    }

    static void ConfigureVertical(VerticalLayoutGroup v, float spacing)
    {
        v.childAlignment = TextAnchor.MiddleCenter;
        v.spacing = spacing;
        v.padding = new RectOffset(0, 0, 0, 0);
        v.childControlWidth = true;
        v.childControlHeight = true;      // heights come from each child's Layout Element
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childScaleWidth = false;
        v.childScaleHeight = false;
    }

    static void SetPreferred(Transform t, float width, float height)
    {
        var le = GetOrAdd<LayoutElement>(t.gameObject);
        le.ignoreLayout = false;
        le.minWidth = -1f;
        le.minHeight = -1f;
        le.flexibleWidth = -1f;
        le.flexibleHeight = -1f;
        le.preferredWidth = width;
        le.preferredHeight = height;
    }

    static void RemoveFitter(Transform t)
    {
        foreach (var f in t.GetComponents<ContentSizeFitter>()) Undo.DestroyObjectImmediate(f);
    }

    static void StyleText(Transform t, float size, TextAlign align)
    {
        var tmp = t.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            Undo.RecordObject(tmp, "Style text");
            tmp.enableAutoSizing = false;
            tmp.fontSize = size;
            tmp.alignment = align == TextAlign.Left ? TextAlignmentOptions.Left
                          : align == TextAlign.Right ? TextAlignmentOptions.Right
                          : TextAlignmentOptions.Center;
            // let the text fill its parent so alignment means something
            if (tmp.transform != t) Stretch((RectTransform)tmp.transform);
            return;
        }

        var legacy = t.GetComponentInChildren<Text>(true);
        if (legacy != null)
        {
            Undo.RecordObject(legacy, "Style text");
            legacy.resizeTextForBestFit = false;
            legacy.fontSize = Mathf.RoundToInt(size);
            legacy.alignment = align == TextAlign.Left ? TextAnchor.MiddleLeft
                             : align == TextAlign.Right ? TextAnchor.MiddleRight
                             : TextAnchor.MiddleCenter;
            if (legacy.transform != t) Stretch((RectTransform)legacy.transform);
        }
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }
}
#endif
