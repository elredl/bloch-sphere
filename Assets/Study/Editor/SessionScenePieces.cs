using System.Collections.Generic;
using Study.Lesson;
using Study.Session;
using Study.Visuals;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static Study.EditorTools.LessonScenePieces;

namespace Study.EditorTools
{
    // Checkpoint 6 parts: the session screens and orientation objects in the Lesson scene,
    // and the Launch scene. Called by StyleLabSetup.BuildAll. Canvas units are mm.
    public static class SessionScenePieces
    {
        const string SessionTextPath = "Assets/Study/Data/session_text.json";
        const string BackingMaterialPath = "Assets/Study/Data/PanelBacking.mat";

        // A panel layout: rectangles in mm from the panel's bottom-left corner (x0, y0, x1, y1).
        class Layout
        {
            public float W, H;
            public Vector4 Body;
            public float BodyFont = 16;
            public Vector4? Footer;
            public float FooterFont = 11;
            public readonly List<Vector4> Buttons = new List<Vector4>();
            public float ButtonFont = 14;
            public Vector4? Ring;
        }

        static StudyPanelView AddPanel(string name, StylePalette palette, Layout l, Vector3 position, Quaternion rotation, Transform parent)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)canvasObject.transform;
            if (parent != null) rect.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.AddComponent<PointerSupport>();
            var group = canvasObject.AddComponent<CanvasGroup>();
            rect.sizeDelta = new Vector2(l.W, l.H);
            rect.localScale = Vector3.one * 0.001f;
            rect.SetPositionAndRotation(position, rotation);

            Image background = Box(rect, "Background", 0, 0, l.W, l.H);
            background.raycastTarget = true; // catches rays aimed at the panel so they don't fall through
            Image accentBar = Box(rect, "Accent Bar", 0, 0, 3, l.H);
            Image accentLine = Box(rect, "Accent Line", 0, l.H - 2, l.W, l.H);
            TextMeshProUGUI title = Text(rect, "Title", "", 14, 12, l.H - 30, l.W - 12, l.H - 8, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI body = Text(rect, "Body", "", l.BodyFont, l.Body.x, l.Body.y, l.Body.z, l.Body.w, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            TextMeshProUGUI footer = null;
            if (l.Footer is Vector4 f)
            {
                footer = Text(rect, "Footer", "", l.FooterFont, f.x, f.y, f.z, f.w, TextAlignmentOptions.BottomLeft);
                footer.textWrappingMode = TextWrappingModes.Normal;
            }
            var buttons = new Object[l.Buttons.Count];
            var frames = new Object[l.Buttons.Count];
            var fills = new Object[l.Buttons.Count];
            var labels = new Object[l.Buttons.Count];
            for (int i = 0; i < l.Buttons.Count; i++)
            {
                Vector4 b = l.Buttons[i];
                var (button, frame, fill, label) = PanelButton(rect, "Button " + i, b.x, b.y, b.z, b.w, l.ButtonFont);
                label.enableAutoSizing = true;
                label.fontSizeMin = 8;
                label.fontSizeMax = l.ButtonFont;
                label.textWrappingMode = TextWrappingModes.Normal;
                (buttons[i], frames[i], fills[i], labels[i]) = (button, frame, fill, label);
            }
            Image ring = null;
            if (l.Ring is Vector4 r) ring = Box(rect, "Ring", r.x, r.y, r.z, r.w);

            var view = canvasObject.AddComponent<StudyPanelView>();
            var so = new SerializedObject(view);
            so.FindProperty("palette").objectReferenceValue = palette;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("background").objectReferenceValue = background;
            SetArray(so, "accents", accentBar, accentLine);
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("footer").objectReferenceValue = footer;
            SetArray(so, "buttons", buttons);
            SetArray(so, "frames", frames);
            SetArray(so, "fills", fills);
            SetArray(so, "labels", labels);
            so.FindProperty("ring").objectReferenceValue = ring;
            so.ApplyModifiedProperties();
            view.ApplyStyle();
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;
            return view;
        }

        // An opaque quad just behind the panel. It is drawn in the opaque pass and writes depth,
        // so the sphere's transparent glow behind it can't show through the panel.
        static void AddBacking(StudyPanelView panel, StylePalette palette)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BackingMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, BackingMaterialPath);
            }
            Color c = palette.panelBackground;
            material.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 1));
            EditorUtility.SetDirty(material);
            var rect = (RectTransform)panel.transform;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Backing";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(rect, false);
            quad.transform.localPosition = new Vector3(0, 0, 2); // 2 mm behind the canvas
            quad.transform.localScale = new Vector3(rect.sizeDelta.x, rect.sizeDelta.y, 1);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quad.SetActive(false);
            var so = new SerializedObject(panel);
            so.FindProperty("backing").objectReferenceValue = quad;
            so.ApplyModifiedProperties();
        }

        static T AddFaceViewer<T>(Component panel, float distance, bool follow) where T : FaceViewer
        {
            var viewer = panel.gameObject.AddComponent<T>();
            var so = new SerializedObject(viewer);
            so.FindProperty("distance").floatValue = distance;
            so.FindProperty("follow").boolValue = follow;
            so.ApplyModifiedProperties();
            return viewer;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        static T AddChild<T>(GameObject parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<T>();
        }

        // ---- Lesson scene ----

        // Orientation objects at the sphere's place (the sphere is hidden until the lesson):
        // the cube with its handle, arrow and axis, the target row above it, and the
        // instruction panel where the Condition 3 prompts appear. Saved inactive.
        public static Orientation AddOrientation(GameObject sessionObject, StylePalette palette, Transform stage,
            Vector3 sphereCenter, Vector3 eye)
        {
            var practice = new GameObject("Practice");
            practice.transform.SetParent(stage, false);
            practice.transform.position = sphereCenter;
            var rig = practice.AddComponent<BlochSphereRig>();
            var rigSo = new SerializedObject(rig);
            rigSo.FindProperty("palette").objectReferenceValue = palette;
            rigSo.FindProperty("radius").floatValue = 0.15f;
            rigSo.ApplyModifiedProperties();
            var cube = AddChild<PracticeCubeView>(practice, "Cube");
            var targets = AddChild<PracticeTargetsView>(practice, "Targets");
            var arrow = AddChild<GateDirectionView>(practice, "Direction Arrow");
            var axis = AddChild<GateAxisView>(practice, "Axis");

            var grabObject = new GameObject("Grab");
            grabObject.transform.SetParent(practice.transform, false);
            var collider = grabObject.AddComponent<SphereCollider>();
            collider.radius = 0.04f;
            var interactable = grabObject.AddComponent<XRSimpleInteractable>();
            var grab = grabObject.AddComponent<PracticeGrab>();
            var grabSo = new SerializedObject(grab);
            grabSo.FindProperty("cube").objectReferenceValue = cube;
            grabSo.FindProperty("rig").objectReferenceValue = rig;
            grabSo.FindProperty("interactable").objectReferenceValue = interactable;
            grabSo.FindProperty("grabCollider").objectReferenceValue = collider;
            grabSo.ApplyModifiedProperties();

            // Instruction panel: the prediction panel's place, 240 x 200 mm, one button.
            var layout = new Layout { W = 240, H = 200, Body = new Vector4(12, 60, 228, 164) };
            layout.Buttons.Add(new Vector4(30, 12, 210, 50));
            Vector3 panelPosition = sphereCenter + new Vector3(-0.38f, 0.12f, -0.25f);
            var panel = AddPanel("Practice Panel", palette, layout, panelPosition,
                Quaternion.LookRotation(panelPosition - eye), practice.transform);

            var orientation = sessionObject.AddComponent<Orientation>();
            var so = new SerializedObject(orientation);
            so.FindProperty("practiceRoot").objectReferenceValue = practice;
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("targets").objectReferenceValue = targets;
            so.FindProperty("cube").objectReferenceValue = cube;
            so.FindProperty("arrow").objectReferenceValue = arrow;
            so.FindProperty("axis").objectReferenceValue = axis;
            so.FindProperty("grab").objectReferenceValue = grab;
            so.ApplyModifiedProperties();
            practice.SetActive(false);
            return orientation;
        }

        // Calibration, ready, break and abort-hold messages: in front of the viewer, following the head.
        public static StudyPanelView AddOverlay(StylePalette palette)
        {
            var layout = new Layout { W = 280, H = 140, Body = new Vector4(12, 34, 230, 104), Footer = new Vector4(12, 8, 268, 30), FooterFont = 9, Ring = new Vector4(238, 90, 270, 122) };
            var panel = AddPanel("Session Overlay", palette, layout, Vector3.zero, Quaternion.identity, null);
            panel.GetComponent<Canvas>().sortingOrder = 10;
            AddBacking(panel, palette);
            AddFaceViewer<FaceViewer>(panel, 0.65f, true);
            return panel;
        }

        public static EndScreen AddEndScreen(GameObject sessionObject, StylePalette palette)
        {
            var layout = new Layout
            {
                W = 300, H = 240, Body = new Vector4(12, 110, 288, 204),
                Footer = new Vector4(12, 56, 288, 104), ButtonFont = 12,
            };
            AddButtonRow(layout, 4, 12, 50);
            var panel = AddPanel("End Screen", palette, layout, Vector3.zero, Quaternion.identity, null);
            AddBacking(panel, palette);
            AddFaceViewer<FaceViewer>(panel, 0.7f, false);
            var end = sessionObject.AddComponent<EndScreen>();
            Set(end, "panel", panel);
            return end;
        }

        static void AddButtonRow(Layout l, int count, float y0, float y1)
        {
            const float margin = 12, gap = 6;
            float w = (l.W - 2 * margin - (count - 1) * gap) / count;
            for (int i = 0; i < count; i++)
            {
                float x = margin + i * (w + gap);
                l.Buttons.Add(new Vector4(x, y0, x + w, y1));
            }
        }

        public static SessionFlow AddSessionFlow(GameObject sessionObject, LessonController lesson, PauseDirector pauses,
            EmbodiedPause embodied, Transform stage, GameObject sphere, GameObject xrOrigin, GameObject[] interactors,
            Orientation orientation, StudyPanelView overlay, EndScreen end)
        {
            var input = sessionObject.AddComponent<SessionInput>();
            var flow = sessionObject.AddComponent<SessionFlow>();
            var so = new SerializedObject(flow);
            so.FindProperty("lesson").objectReferenceValue = lesson;
            so.FindProperty("pauses").objectReferenceValue = pauses;
            so.FindProperty("embodied").objectReferenceValue = embodied;
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("sphere").objectReferenceValue = sphere;
            so.FindProperty("xrOrigin").objectReferenceValue = xrOrigin.GetComponent<XROrigin>();
            SetArray(so, "interactors", interactors);
            so.FindProperty("orientation").objectReferenceValue = orientation;
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("overlay").objectReferenceValue = overlay;
            so.FindProperty("endScreen").objectReferenceValue = end;
            so.FindProperty("textJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SessionTextPath);
            so.ApplyModifiedProperties();
            return flow;
        }

        // ---- Launch scene ----

        // 300 x 360 mm: code and messages at the top, 3 x 4 keypad, allocation and data folder
        // footer, four action buttons along the bottom.
        public static LaunchScreen AddLaunchScreen(StylePalette palette)
        {
            var layout = new Layout
            {
                W = 300, H = 360, Body = new Vector4(12, 262, 288, 324), BodyFont = 14,
                Footer = new Vector4(12, 52, 288, 100), FooterFont = 9, ButtonFont = 14,
            };
            const float kw = 60, kh = 34, gap = 6;
            float left = (layout.W - (3 * kw + 2 * gap)) / 2;
            for (int i = 0; i < 12; i++)
            {
                int row = i / 3, column = i % 3;
                float x = left + column * (kw + gap);
                float y = 102 + (3 - row) * (kh + gap);
                layout.Buttons.Add(new Vector4(x, y, x + kw, y + kh));
            }
            AddButtonRow(layout, 4, 12, 48);
            var panel = AddPanel("Launch Panel", palette, layout, new Vector3(0, 1.2f, 0.7f), Quaternion.identity, null);
            var panelSo = new SerializedObject(panel);
            panelSo.FindProperty("startHidden").boolValue = false;
            panelSo.ApplyModifiedProperties();
            AddFaceViewer<FaceViewer>(panel, 0.7f, false);
            var launch = new GameObject("Launch").AddComponent<LaunchScreen>();
            Set(launch, "panel", panel);
            return launch;
        }
    }
}
