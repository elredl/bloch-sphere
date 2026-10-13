using System.Globalization;
using Study.Lesson;
using Study.Quantum;
using Study.Visuals;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Study.EditorTools
{
    // Editor helpers for Checkpoint 1.5, called through the live command runner
    // (action "call_static"). Creates the font assets, the StylePalette asset and
    // the Lesson scene, and sets preview states for screenshots.
    public static class StyleLabSetup
    {
        const string FontsFolder = "Assets/Study/Fonts";
        const string PalettePath = "Assets/Study/Data/StylePalette.asset";
        const string ScenePath = "Assets/Study/Scenes/Lesson.unity";
        const string TimelinePath = "Assets/Study/Data/lesson_timeline.json";

        // Seated participant: eye about 1.2 m above the floor. The sphere center sits
        // 0.25 m below eye level and 0.8 m forward. Calibration (Checkpoint 6) moves
        // the sphere to each participant's measured eye height; this is the default.
        static readonly Vector3 DefaultEye = new Vector3(0f, 1.2f, 0f);
        static readonly Vector3 SphereCenter = DefaultEye + new Vector3(0f, -0.25f, 0.8f);
        // Oblique textbook view: the sphere is turned so +x points toward the participant's
        // front-left instead of straight at them (where it would shrink to a dot).
        // Decided at Checkpoint 1.5; see docs/STUDY_SPEC.md, Coordinate convention.
        const float SphereYawDegrees = 30f;

        public static string BuildAll()
        {
            string fonts = EnsureFonts();
            EnsurePalette();
            BuildLessonScene();
            return fonts + " | palette and Lesson scene built | " + LessonScenePieces.DisableHandTracking();
        }

        static string EnsureFonts()
        {
            TMP_FontAsset inter = EnsureFontAsset("Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular.ttf", "Inter SDF");
            TMP_FontAsset orbitron = EnsureFontAsset(FontsFolder + "/Orbitron-Variable.ttf", "Orbitron SDF");
            // Inter has no ket bracket; Noto Sans Math (also sans, OFL) supplies it and other math symbols.
            TMP_FontAsset math = EnsureFontAsset(FontsFolder + "/NotoSansMath-Regular.ttf", "Noto Sans Math SDF");
            if (inter.fallbackFontAssetTable == null) inter.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            if (!inter.fallbackFontAssetTable.Contains(math))
            {
                inter.fallbackFontAssetTable.Add(math);
                EditorUtility.SetDirty(inter);
                AssetDatabase.SaveAssets();
            }
            ResetDynamicFonts();
            // The sphere labels need these glyphs; report any the body font lacks.
            string missing = "";
            foreach (char c in "|01⟩θφxy%")
                if (!inter.HasCharacter(c, true, true)) missing += c;
            return missing.Length == 0 ? "fonts ok" : "Inter is missing glyphs: " + missing;
        }

        // Empties the dynamic atlases so every glyph is re-rendered from its own source font.
        // Needed once after creating several dynamic assets back to back: glyphs requested
        // during creation can be rasterized from whichever face the font engine loaded last.
        public static string ResetDynamicFonts()
        {
            string done = "";
            foreach (string name in new[] { "Inter SDF", "Orbitron SDF", "Noto Sans Math SDF" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontsFolder}/{name}.asset");
                if (asset == null) continue;
                asset.ClearFontAssetData(true);
                EditorUtility.SetDirty(asset);
                done += name + "; ";
            }
            AssetDatabase.SaveAssets();
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, null);
            return "cleared " + done;
        }

        static TMP_FontAsset EnsureFontAsset(string fontPath, string assetName)
        {
            string path = $"{FontsFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath) ?? throw new System.Exception("Font not found: " + fontPath);
            // Dynamic atlas: glyphs are added on demand from the source font, so symbols
            // like the ket bracket and theta/phi work without a hand-picked character set.
            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, path);
            asset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.atlasTexture.name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static StylePalette EnsurePalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<StylePalette>(PalettePath);
            if (palette == null)
            {
                System.IO.Directory.CreateDirectory("Assets/Study/Data");
                palette = ScriptableObject.CreateInstance<StylePalette>();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }
            palette.neonLineShader = Shader.Find("Study/NeonLine");
            palette.shellShader = Shader.Find("Study/Shell");
            palette.gridFloorShader = Shader.Find("Study/GridFloor");
            palette.bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontsFolder + "/Inter SDF.asset");
            palette.titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontsFolder + "/Orbitron SDF.asset");
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            return palette;
        }

        static void BuildLessonScene()
        {
            System.IO.Directory.CreateDirectory("Assets/Study/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load after NewScene: opening a scene unloads unused assets, which would
            // destroy a palette loaded earlier and silently null every reference to it.
            var palette = AssetDatabase.LoadAssetAtPath<StylePalette>(PalettePath);

            var sphere = new GameObject("Bloch Sphere");
            sphere.transform.position = SphereCenter;
            sphere.transform.rotation = Quaternion.Euler(0f, SphereYawDegrees, 0f);
            var rig = sphere.AddComponent<BlochSphereRig>();
            Set(rig, "palette", palette);
            var state = sphere.AddComponent<QubitState>();
            var animator = sphere.AddComponent<GateArcAnimator>();
            Set(animator, "state", state);
            sphere.AddComponent<SphereDimmer>();
            AddChild<SphereFrameView>(sphere, "Frame");
            AddChild<AngleArcsView>(sphere, "Angle Arcs");
            AddChild<ArcTrailView>(sphere, "Arc Trail");
            AddChild<GateAxisView>(sphere, "Gate Axis");
            var vectorView = AddChild<StateVectorView>(sphere, "State Vector");
            var ghost = AddChild<StateVectorView>(sphere, "Ghost Vector");
            var ghostSo = new SerializedObject(ghost);
            ghostSo.FindProperty("followsState").boolValue = false;
            ghostSo.FindProperty("isGhost").boolValue = true;
            ghostSo.ApplyModifiedProperties();
            var flash = AddChild<CollapseFlashView>(sphere, "Collapse Flash");
            var effects = sphere.AddComponent<VectorEffects>();
            Set(effects, "state", state);
            Set(effects, "animator", animator);
            Set(effects, "vector", vectorView);
            Set(effects, "flash", flash);

            // Desktop (Condition 1) camera at the default eye point, pitched to center
            // the sphere, 60 deg vertical FOV. Post-processing (bloom) off by default.
            var cameraObject = new GameObject("Desktop Camera") { tag = "MainCamera" };
            cameraObject.transform.position = DefaultEye;
            cameraObject.transform.LookAt(SphereCenter);
            var cam = cameraObject.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;
            cam.allowMSAA = true;
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            cameraObject.AddComponent<AudioListener>();

            var environment = new GameObject("Environment");
            var styler = environment.AddComponent<EnvironmentStyler>();
            Set(styler, "palette", palette);
            Set(styler, "fadeCenter", sphere.transform);
            styler.enabled = false; // re-enable so it builds with the references set
            styler.enabled = true;

            // The lesson: narration, timeline and pause behavior.
            var lessonObject = new GameObject("Lesson");
            lessonObject.AddComponent<AudioSource>();
            var lesson = lessonObject.AddComponent<LessonController>();
            Set(lesson, "timelineJson", AssetDatabase.LoadAssetAtPath<TextAsset>(TimelinePath));
            Set(lesson, "state", state);
            Set(lesson, "animator", animator);
            Set(lesson, "angleArcs", sphere.GetComponentInChildren<AngleArcsView>());
            Set(lesson, "effects", effects);
            Set(lesson, "flash", flash);

            var panel = LessonScenePieces.AddMeasurementPanel(palette, SphereCenter, DefaultEye);
            var demo = lessonObject.AddComponent<MeasurementDemo>();
            Set(demo, "lesson", lesson);
            Set(demo, "state", state);
            Set(demo, "effects", effects);
            Set(demo, "ghost", ghost);
            Set(demo, "panel", panel);
            Set(lesson, "demo", demo);

            var director = lessonObject.AddComponent<PauseDirector>();
            Set(director, "lesson", lesson);
            Set(director, "animator", animator);
            Set(director, "dimmer", sphere.GetComponent<SphereDimmer>());
            Set(director, "demo", demo);

            LessonScenePieces.AddGrabTarget(sphere, lesson, state);

            // Headset rig (inactive in the saved scene) and the switch between it and the desktop camera.
            var (xrOrigin, interactors) = LessonScenePieces.AddXrRig();
            var view = lessonObject.AddComponent<ViewSetup>();
            Set(view, "lesson", lesson);
            Set(view, "desktopCamera", cameraObject);
            Set(view, "xrOrigin", xrOrigin);
            var viewSo = new SerializedObject(view);
            SetArray(viewSo, "interactors", interactors);
            viewSo.ApplyModifiedProperties();
            lessonObject.AddComponent<FrameStats>(); // frame timing to the log, for the Quest frame-rate check
            styler.Apply(); // style the XR camera's background too

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // Adds the style-lock sample panel to the open scene, for previews only.
        // Not part of the Lesson scene: it would be visible in every condition.
        public static string AddSamplePanel()
        {
            BuildSamplePanel(AssetDatabase.LoadAssetAtPath<StylePalette>(PalettePath));
            return "sample panel added (do not save it into Lesson.unity)";
        }

        // A sample prediction panel (pause 1's P(0) format) for the style lock.
        // Placed beside the sphere, angled toward the eye, never covering the sphere.
        static void BuildSamplePanel(StylePalette palette)
        {
            // Canvas units are millimetres. The panel sits ~0.6 m from the eye, upper left of
            // the sphere, inside the free band the 60 deg desktop frame leaves beside it.
            // Body text 17 mm em (about 12 mm cap height) is about 1.1 deg. Confirm on Quest 3.
            const float mm = 0.001f;
            var canvasObject = new GameObject("Sample Panel", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvasObject.transform;
            rect.sizeDelta = new Vector2(232, 196);
            rect.localScale = Vector3.one * mm;
            rect.position = SphereCenter + new Vector3(-0.38f, 0.06f, -0.25f);
            rect.rotation = Quaternion.LookRotation(rect.position - DefaultEye);

            Image background = UiImage(rect, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image accentBar = UiImage(rect, "Accent Bar", Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(3, 0));
            Image accentLine = UiImage(rect, "Accent Line", new Vector2(0, 1), Vector2.one, new Vector2(0, -2), Vector2.zero);

            TextMeshProUGUI title = UiText(rect, "Title", "PREDICT", 14, new Vector2(12, -9), new Vector2(-12, -30));
            TextMeshProUGUI body = UiText(rect, "Body",
                "After a quarter turn toward |1⟩, how often will a measurement give 0?", 17,
                new Vector2(12, -34), new Vector2(-12, -130));

            string[] choices = { "100%", "75%", "50%", "25%", "0%" };
            var frames = new Image[choices.Length];
            var fills = new Image[choices.Length];
            var labels = new TextMeshProUGUI[choices.Length];
            float width = 38, gap = 5, left = 12;
            for (int i = 0; i < choices.Length; i++)
            {
                float x = left + i * (width + gap);
                frames[i] = UiImage(rect, "Choice Frame " + choices[i], new Vector2(0, 0), new Vector2(0, 0),
                    new Vector2(x, 12), new Vector2(x + width, 46));
                fills[i] = UiImage(frames[i].rectTransform, "Fill", Vector2.zero, Vector2.one, new Vector2(1.2f, 1.2f), new Vector2(-1.2f, -1.2f));
                labels[i] = UiText(frames[i].rectTransform, "Label", choices[i], 14, Vector2.zero, Vector2.zero);
                labels[i].rectTransform.anchorMin = Vector2.zero;
                labels[i].rectTransform.anchorMax = Vector2.one;
                labels[i].alignment = TextAlignmentOptions.Center;
            }

            var panel = canvasObject.AddComponent<StyledPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("palette").objectReferenceValue = palette;
            so.FindProperty("background").objectReferenceValue = background;
            SetArray(so, "accents", accentBar, accentLine);
            so.FindProperty("title").objectReferenceValue = title;
            var bodies = new Object[labels.Length + 1];
            bodies[0] = body;
            for (int i = 0; i < labels.Length; i++) bodies[i + 1] = labels[i];
            SetArray(so, "bodyTexts", bodies);
            SetArray(so, "buttonBackgrounds", fills);
            SetArray(so, "buttonFrames", frames);
            so.ApplyModifiedProperties();
            panel.Apply();
        }

        // ---- Preview states for screenshots ----

        // Edit or Play Mode. mode = idle | angles | dimmed
        public static string Preview(string mode)
        {
            var state = Object.FindAnyObjectByType<QubitState>();
            var arcs = Object.FindAnyObjectByType<AngleArcsView>();
            var dimmer = Object.FindAnyObjectByType<SphereDimmer>();
            var gateAxis = Object.FindAnyObjectByType<GateAxisView>();
            arcs.Visible = false;
            dimmer.SetDimmedImmediate(false);
            gateAxis.Hide();
            switch (mode)
            {
                case "idle":
                    state.Set(Qubit.Zero);
                    break;
                case "angles":
                    // A generic state so both angles are clearly non-zero: theta 60, phi 55.
                    state.Set(Qubit.FromBlochAngles(60 * Mathf.Deg2Rad, 55 * Mathf.Deg2Rad));
                    arcs.Visible = true;
                    break;
                case "dimmed":
                    // Pause 2 as it would look before the learner turns: |+> with the z axis active.
                    state.Set(Qubit.Plus);
                    gateAxis.Show(Gate.Rz(Mathf.PI / 2));
                    dimmer.SetDimmedImmediate(true);
                    break;
                default:
                    return "unknown mode " + mode;
            }
            SceneView.RepaintAll();
            return $"preview {mode}: {state.Current.Bloch}";
        }

        // Play Mode only. arg = "gate,startState,seconds", e.g. "H,0,3".
        public static string PlayGate(string arg)
        {
            if (!EditorApplication.isPlaying) return "not in play mode";
            string[] parts = arg.Split(',');
            Preview("idle");
            Object.FindAnyObjectByType<QubitState>().Set(Qubit.FromName(parts[1]));
            Object.FindAnyObjectByType<GateArcAnimator>().Play(Gate.FromName(parts[0]), double.Parse(parts[2], CultureInfo.InvariantCulture));
            return $"playing {parts[0]} from |{parts[1]}> over {parts[2]} s";
        }

        // Rotates the whole sphere about the vertical, in degrees. Positive turns +x
        // toward the participant's left (the textbook oblique view). Preview only.
        public static string SetYaw(string degrees)
        {
            var sphere = GameObject.Find("Bloch Sphere").transform;
            sphere.rotation = Quaternion.Euler(0, float.Parse(degrees, CultureInfo.InvariantCulture), 0);
            return "yaw " + degrees;
        }

        // Turns URP post-processing (the Default Volume Profile's bloom) on the desktop camera on or off.
        public static string SetBloom(string on)
        {
            var data = GameObject.Find("Desktop Camera").GetComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = on == "true";
            return "post-processing " + (data.renderPostProcessing ? "on" : "off");
        }

        // ---- small helpers ----

        static T AddChild<T>(GameObject parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<T>();
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        static void SetArray(SerializedObject so, string field, params Object[] values)
        {
            var property = so.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // anchors in 0-1, offsets in canvas millimetres (offsetMin = bottom-left, offsetMax = top-right).
        static Image UiImage(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        // Text stretched across the top of the parent, between two offsets from its top-left/top-right.
        static TextMeshProUGUI UiText(RectTransform parent, string name, string text, float size, Vector2 topLeft, Vector2 bottomRight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(topLeft.x, bottomRight.y);
            rect.offsetMax = new Vector2(bottomRight.x, topLeft.y);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
