using System.Collections.Generic;
using Study.Lesson;
using Study.Visuals;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.OpenXR;

namespace Study.EditorTools
{
    // Checkpoint 3 parts of the Lesson scene, added by StyleLabSetup.BuildLessonScene.
    public static class LessonScenePieces
    {
        const string RigPrefab = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        // The XRI starter rig with everything but controllers and their ray/grab interactors
        // switched off: no locomotion of any kind (the participant sits still), no poke, no gaze.
        // Saved inactive; ViewSetup turns it on in the headset conditions.
        public static (GameObject origin, GameObject[] interactors) AddXrRig()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab) ?? throw new System.Exception("XR rig prefab not found");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.name = "XR Origin";
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rig.GetComponent<XROrigin>().RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            var interactors = new List<GameObject>();
            foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Locomotion" || t.name == "Poke Interactor") t.gameObject.SetActive(false);
                if (t.name == "Near-Far Interactor") interactors.Add(t.gameObject);
            }
            var cam = rig.GetComponentInChildren<Camera>(true);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;

            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            rig.SetActive(false);
            return (rig, interactors.ToArray());
        }

        // Controllers only (spec): switch off every OpenXR hand-tracking feature for Quest.
        public static string DisableHandTracking()
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var disabled = new List<string>();
            foreach (var feature in settings.GetFeatures())
            {
                string type = feature.GetType().Name;
                if ((type.Contains("Hand") || type == "PalmPoseInteraction") && feature.enabled)
                {
                    feature.enabled = false;
                    EditorUtility.SetDirty(feature);
                    disabled.Add(type);
                }
            }
            AssetDatabase.SaveAssets();
            return disabled.Count == 0 ? "no hand features were enabled" : "disabled: " + string.Join(", ", disabled);
        }

        public static GameObject AddGrabTarget(GameObject sphere, LessonController lesson, QubitState state)
        {
            var go = new GameObject("Grab Target");
            go.transform.SetParent(sphere.transform, false);
            var collider = go.AddComponent<SphereCollider>();
            collider.radius = 0.04f;
            var interactable = go.AddComponent<XRSimpleInteractable>();
            var target = go.AddComponent<QubitGrabTarget>();
            var so = new SerializedObject(target);
            so.FindProperty("lesson").objectReferenceValue = lesson;
            so.FindProperty("state").objectReferenceValue = state;
            so.FindProperty("rig").objectReferenceValue = sphere.GetComponent<BlochSphereRig>();
            so.FindProperty("interactable").objectReferenceValue = interactable;
            so.FindProperty("grabCollider").objectReferenceValue = collider;
            so.ApplyModifiedProperties();
            return go;
        }

        // Histogram panel, upper right of the sphere (mirror of where prompts go), inside the
        // free band the 60 deg desktop frame leaves beside the sphere. Canvas units are mm.
        public static MeasurementPanelView AddMeasurementPanel(StylePalette palette, Vector3 sphereCenter, Vector3 eye)
        {
            var canvasObject = new GameObject("Measurement Panel", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var group = canvasObject.AddComponent<CanvasGroup>();
            var rect = (RectTransform)canvasObject.transform;
            rect.sizeDelta = new Vector2(196, 180);
            rect.localScale = Vector3.one * 0.001f;
            rect.position = sphereCenter + new Vector3(0.36f, 0.25f, -0.25f);
            rect.rotation = Quaternion.LookRotation(rect.position - eye);

            Image background = Box(rect, "Background", 0, 0, 196, 180);
            Image accent = Box(rect, "Accent Line", 0, 178, 196, 180);
            TextMeshProUGUI title = Text(rect, "Title", "MEASUREMENTS", 11, 10, 152, 130, 174, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI counter = Text(rect, "Counter", "0 / 100", 14, 120, 152, 186, 174, TextAlignmentOptions.TopRight);

            // Two columns; each bar is anchored to its column's bottom and grows upward.
            RectTransform barZero = Bar(rect, "Bar |0>", 28, palette.histogramZero);
            RectTransform barOne = Bar(rect, "Bar |1>", 118, palette.histogramOne);
            TextMeshProUGUI labelZero = Text(rect, "Label |0>", "|0⟩", 17, 28, 30, 78, 52, TextAlignmentOptions.Center);
            TextMeshProUGUI labelOne = Text(rect, "Label |1>", "|1⟩", 17, 118, 30, 168, 52, TextAlignmentOptions.Center);
            TextMeshProUGUI countZero = Text(rect, "Count |0>", "0", 14, 3, 8, 103, 30, TextAlignmentOptions.Center);
            TextMeshProUGUI countOne = Text(rect, "Count |1>", "0", 14, 93, 8, 193, 30, TextAlignmentOptions.Center);

            var view = canvasObject.AddComponent<MeasurementPanelView>();
            var so = new SerializedObject(view);
            so.FindProperty("palette").objectReferenceValue = palette;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("background").objectReferenceValue = background;
            var accents = so.FindProperty("accents");
            accents.arraySize = 1;
            accents.GetArrayElementAtIndex(0).objectReferenceValue = accent;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("counter").objectReferenceValue = counter;
            so.FindProperty("barZero").objectReferenceValue = barZero;
            so.FindProperty("barOne").objectReferenceValue = barOne;
            so.FindProperty("labelZero").objectReferenceValue = labelZero;
            so.FindProperty("labelOne").objectReferenceValue = labelOne;
            so.FindProperty("countZero").objectReferenceValue = countZero;
            so.FindProperty("countOne").objectReferenceValue = countOne;
            so.ApplyModifiedProperties();
            view.ApplyStyle();
            group.alpha = 0; // hidden until a demo starts
            return view;
        }

        // A column 50 mm wide from y = 56 to y = 146 holding a bar that fills it bottom-up.
        static RectTransform Bar(RectTransform parent, string name, float x, Color color)
        {
            var column = new GameObject(name + " Column", typeof(RectTransform));
            var columnRect = (RectTransform)column.transform;
            columnRect.SetParent(parent, false);
            Place(columnRect, x, 56, x + 50, 146);
            Image bar = Box(columnRect, name, 0, 0, 0, 0);
            bar.color = color;
            bar.rectTransform.anchorMin = Vector2.zero;
            bar.rectTransform.anchorMax = new Vector2(1, 0);
            bar.rectTransform.offsetMin = Vector2.zero;
            bar.rectTransform.offsetMax = Vector2.zero;
            return bar.rectTransform;
        }

        // Rectangle in parent millimetres from the bottom-left corner.
        static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x0, y0);
            rect.sizeDelta = new Vector2(x1 - x0, y1 - y0);
        }

        static Image Box(RectTransform parent, string name, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Place(rect, x0, y0, x1, y1);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        static TextMeshProUGUI Text(RectTransform parent, string name, string text, float size,
            float x0, float y0, float x1, float y1, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Place(rect, x0, y0, x1, y1);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
