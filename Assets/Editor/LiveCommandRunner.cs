using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Watches LiveCommands/inbox for JSON command files and applies them
// immediately to the currently open scene, so changes are visible live
// in the running Editor without closing it or using batchmode.
[InitializeOnLoad]
public static class LiveCommandRunner
{
    static readonly string ProjectRoot = Directory.GetParent(Application.dataPath).FullName;
    static readonly string InboxDir = Path.Combine(ProjectRoot, "LiveCommands", "inbox");
    static readonly string ProcessedDir = Path.Combine(ProjectRoot, "LiveCommands", "processed");

    static LiveCommandRunner()
    {
        Directory.CreateDirectory(InboxDir);
        Directory.CreateDirectory(ProcessedDir);
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(InboxDir, "*.json").OrderBy(f => f).ToArray();
        }
        catch (IOException)
        {
            return; // file may be mid-write
        }

        foreach (var file in files)
        {
            string resultPath = Path.Combine(ProcessedDir, Path.GetFileNameWithoutExtension(file) + ".result.json");
            string destPath = Path.Combine(ProcessedDir, Path.GetFileName(file));
            try
            {
                string json = File.ReadAllText(file);
                var cmd = JsonUtility.FromJson<Command>(json);
                string message = Execute(cmd);
                File.WriteAllText(resultPath, JsonUtility.ToJson(new Result { ok = true, message = message }, true));
            }
            catch (Exception e)
            {
                File.WriteAllText(resultPath, JsonUtility.ToJson(new Result { ok = false, message = e.Message }, true));
            }
            finally
            {
                File.Move(file, destPath);
            }
        }
    }

    [Serializable]
    public class Command
    {
        public string action;
        public string name;
        public string newName;
        public string parentName;
        public string primitiveType; // Cube, Sphere, Capsule, Cylinder, Plane, Quad
        public string position; // "x,y,z"
        public string rotation; // "x,y,z" (euler)
        public string scale;    // "x,y,z"
        public string color;    // "r,g,b,a" 0-1
        public string path;     // scene path for open/save-as
        public string lightType; // Directional, Point, Spot, Area
        public float intensity = 1f;
        public string from;      // "x,y,z" for create_rod
        public string to;        // "x,y,z" for create_rod
        public float thickness = 0.02f;
        public string componentType; // for add_component / set_field_*
        public string fieldName;     // for set_field_*
        public string refObjectName; // for set_field_transform
        public float floatValue;     // for set_field_float
        public bool boolValue;       // for set_play_mode
    }

    static Type FindComponentType(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }
            foreach (var t in types)
            {
                if (t == null || !typeof(Component).IsAssignableFrom(t)) continue;
                if (t.Name == typeName || t.FullName == typeName) return t;
            }
        }
        return null;
    }

    [Serializable]
    public class Result
    {
        public bool ok;
        public string message;
    }

    static Vector3 ParseVec3(string s, Vector3 fallback)
    {
        if (string.IsNullOrEmpty(s)) return fallback;
        var parts = s.Split(',');
        return new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
    }

    static Color ParseColor(string s, Color fallback)
    {
        if (string.IsNullOrEmpty(s)) return fallback;
        var parts = s.Split(',');
        return new Color(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]),
            parts.Length > 3 ? float.Parse(parts[3]) : 1f);
    }

    static void ApplyColor(Renderer renderer, Color color)
    {
        var mat = renderer.sharedMaterial != null ? new Material(renderer.sharedMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        if (color.a < 0.999f)
        {
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        renderer.sharedMaterial = mat;
    }

    static string Execute(Command cmd)
    {
        var scene = EditorSceneManager.GetActiveScene();

        switch (cmd.action)
        {
            case "create_primitive":
            {
                if (!Enum.TryParse(cmd.primitiveType, true, out PrimitiveType type))
                    throw new Exception($"Unknown primitive type '{cmd.primitiveType}'");
                var go = GameObject.CreatePrimitive(type);
                go.name = string.IsNullOrEmpty(cmd.name) ? type.ToString() : cmd.name;
                go.transform.position = ParseVec3(cmd.position, Vector3.zero);
                go.transform.eulerAngles = ParseVec3(cmd.rotation, Vector3.zero);
                go.transform.localScale = ParseVec3(cmd.scale, Vector3.one);
                if (!string.IsNullOrEmpty(cmd.parentName))
                {
                    var parent = GameObject.Find(cmd.parentName);
                    if (parent != null) go.transform.SetParent(parent.transform, true);
                }
                Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
                EditorSceneManager.MarkSceneDirty(scene);
                return $"created {go.name}";
            }
            case "delete":
            {
                var go = GameObject.Find(cmd.name) ?? throw new Exception($"GameObject '{cmd.name}' not found");
                Undo.DestroyObjectImmediate(go);
                EditorSceneManager.MarkSceneDirty(scene);
                return $"deleted {cmd.name}";
            }
            case "rename":
            {
                var go = GameObject.Find(cmd.name) ?? throw new Exception($"GameObject '{cmd.name}' not found");
                Undo.RecordObject(go, "Rename");
                go.name = cmd.newName;
                EditorSceneManager.MarkSceneDirty(scene);
                return $"renamed {cmd.name} -> {cmd.newName}";
            }
            case "set_transform":
            {
                var go = GameObject.Find(cmd.name) ?? throw new Exception($"GameObject '{cmd.name}' not found");
                Undo.RecordObject(go.transform, "Set Transform");
                if (!string.IsNullOrEmpty(cmd.position)) go.transform.position = ParseVec3(cmd.position, go.transform.position);
                if (!string.IsNullOrEmpty(cmd.rotation)) go.transform.eulerAngles = ParseVec3(cmd.rotation, go.transform.eulerAngles);
                if (!string.IsNullOrEmpty(cmd.scale)) go.transform.localScale = ParseVec3(cmd.scale, go.transform.localScale);
                EditorSceneManager.MarkSceneDirty(scene);
                return $"updated transform of {cmd.name}";
            }
            case "set_parent":
            {
                var go = GameObject.Find(cmd.name) ?? throw new Exception($"GameObject '{cmd.name}' not found");
                Transform parent = null;
                if (!string.IsNullOrEmpty(cmd.parentName))
                {
                    var parentGo = GameObject.Find(cmd.parentName) ?? throw new Exception($"Parent '{cmd.parentName}' not found");
                    parent = parentGo.transform;
                }
                Undo.SetTransformParent(go.transform, parent, "Set Parent");
                EditorSceneManager.MarkSceneDirty(scene);
                return $"set parent of {cmd.name} to {(parent == null ? "<none>" : cmd.parentName)}";
            }
            case "add_light":
            {
                if (!Enum.TryParse(cmd.lightType, true, out LightType type))
                    throw new Exception($"Unknown light type '{cmd.lightType}'");
                var go = new GameObject(string.IsNullOrEmpty(cmd.name) ? type + " Light" : cmd.name);
                var light = go.AddComponent<Light>();
                light.type = type;
                light.color = ParseColor(cmd.color, Color.white);
                light.intensity = cmd.intensity;
                go.transform.position = ParseVec3(cmd.position, Vector3.zero);
                go.transform.eulerAngles = ParseVec3(cmd.rotation, type == LightType.Directional ? new Vector3(50, -30, 0) : Vector3.zero);
                Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
                EditorSceneManager.MarkSceneDirty(scene);
                return $"created {type} light '{go.name}'";
            }
            case "set_material_color":
            {
                var go = GameObject.Find(cmd.name) ?? throw new Exception($"GameObject '{cmd.name}' not found");
                var renderer = go.GetComponent<Renderer>() ?? throw new Exception($"'{cmd.name}' has no Renderer");
                ApplyColor(renderer, ParseColor(cmd.color, Color.white));
                EditorSceneManager.MarkSceneDirty(scene);
                return $"set color of {cmd.name}";
            }
            case "create_rod":
            {
                var a = ParseVec3(cmd.from, Vector3.zero);
                var b = ParseVec3(cmd.to, Vector3.up);
                var dir = b - a;
                var length = dir.magnitude;
                if (length < 0.0001f) throw new Exception("create_rod: from and to must differ");
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = string.IsNullOrEmpty(cmd.name) ? "Rod" : cmd.name;
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.position = (a + b) / 2f;
                go.transform.up = dir.normalized;
                go.transform.localScale = new Vector3(cmd.thickness, length / 2f, cmd.thickness);
                ApplyColor(go.GetComponent<Renderer>(), ParseColor(cmd.color, Color.white));
                if (!string.IsNullOrEmpty(cmd.parentName))
                {
                    var parent = GameObject.Find(cmd.parentName);
                    if (parent != null) go.transform.SetParent(parent.transform, true);
                }
                Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
                EditorSceneManager.MarkSceneDirty(scene);
                return $"created rod {go.name}";
            }
            case "instantiate_prefab":
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(cmd.path) ?? throw new Exception($"Prefab not found at '{cmd.path}'");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (!string.IsNullOrEmpty(cmd.name)) go.name = cmd.name;
                go.transform.position = ParseVec3(cmd.position, Vector3.zero);
                go.transform.eulerAngles = ParseVec3(cmd.rotation, Vector3.zero);
                Undo.RegisterCreatedObjectUndo(go, "Instantiate " + go.name);
                EditorSceneManager.MarkSceneDirty(scene);
                return $"instantiated {go.name} from {cmd.path}";
            }
            case "add_component":
            {
                var go = GameObject.Find(cmd.name) ?? throw new Exception($"GameObject '{cmd.name}' not found");
                var type = FindComponentType(cmd.componentType) ?? throw new Exception($"Component type '{cmd.componentType}' not found");
                if (go.GetComponent(type) == null) Undo.AddComponent(go, type);
                EditorSceneManager.MarkSceneDirty(scene);
                return $"ensured component {type.Name} on {cmd.name}";
            }
            case "set_field_transform":
            {
                var go = GameObject.Find(cmd.name) ?? throw new Exception($"GameObject '{cmd.name}' not found");
                var type = FindComponentType(cmd.componentType) ?? throw new Exception($"Component type '{cmd.componentType}' not found");
                var comp = go.GetComponent(type) ?? throw new Exception($"'{cmd.name}' has no component {cmd.componentType}");
                var field = type.GetField(cmd.fieldName) ?? throw new Exception($"Field '{cmd.fieldName}' not found on {cmd.componentType}");
                var refGo = GameObject.Find(cmd.refObjectName) ?? throw new Exception($"Reference object '{cmd.refObjectName}' not found");
                if (field.FieldType != typeof(Transform)) throw new Exception($"Field '{cmd.fieldName}' is not a Transform");
                Undo.RecordObject(comp, "Set Field");
                field.SetValue(comp, refGo.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                return $"set {cmd.componentType}.{cmd.fieldName} on {cmd.name} to {cmd.refObjectName}";
            }
            case "set_field_float":
            {
                var go = GameObject.Find(cmd.name) ?? throw new Exception($"GameObject '{cmd.name}' not found");
                var type = FindComponentType(cmd.componentType) ?? throw new Exception($"Component type '{cmd.componentType}' not found");
                var comp = go.GetComponent(type) ?? throw new Exception($"'{cmd.name}' has no component {cmd.componentType}");
                var field = type.GetField(cmd.fieldName) ?? throw new Exception($"Field '{cmd.fieldName}' not found on {cmd.componentType}");
                if (field.FieldType != typeof(float)) throw new Exception($"Field '{cmd.fieldName}' is not a float");
                Undo.RecordObject(comp, "Set Field");
                field.SetValue(comp, cmd.floatValue);
                EditorSceneManager.MarkSceneDirty(scene);
                return $"set {cmd.componentType}.{cmd.fieldName} on {cmd.name} to {cmd.floatValue}";
            }
            case "get_hierarchy":
            {
                var go = GameObject.Find(cmd.name) ?? throw new Exception($"GameObject '{cmd.name}' not found");
                var names = go.GetComponentsInChildren<Transform>(true).Select(t => t.name);
                return string.Join(", ", names);
            }
            case "set_play_mode":
            {
                EditorApplication.isPlaying = cmd.boolValue;
                return $"set play mode to {cmd.boolValue}";
            }
            case "save_scene":
            {
                EditorSceneManager.SaveScene(scene);
                return $"saved scene {scene.path}";
            }
            case "open_scene":
            {
                EditorSceneManager.OpenScene(cmd.path, OpenSceneMode.Single);
                return $"opened scene {cmd.path}";
            }
            case "list_objects":
            {
                var names = scene.GetRootGameObjects().Select(g => g.name);
                return string.Join(", ", names);
            }
            default:
                throw new Exception($"Unknown action '{cmd.action}'");
        }
    }
}
